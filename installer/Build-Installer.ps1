param(
    [Parameter(Mandatory)][string]$CompilerPath,
    [Parameter(Mandatory)][string]$WebViewBootstrapper,
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\CopilotPulse-Installer'),
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\..\..\work\installer-build'),
    [string]$PackageCache,
    [switch]$TestPackage
)
$ErrorActionPreference = 'Stop'
$arch = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64' }
$compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
$bootstrapper = (Resolve-Path -LiteralPath $WebViewBootstrapper).Path
if ([IO.Path]::GetFileName($bootstrapper) -ne 'MicrosoftEdgeWebview2Setup.exe') {
    throw 'Use the official bootstrapper named MicrosoftEdgeWebview2Setup.exe.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'WebView2 bootstrapper must have a valid Microsoft Corporation signature.'
}
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\CopilotPulse.csproj')
$version = [string]$project.Project.PropertyGroup.Version
$build = [IO.Path]::GetFullPath($BuildDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
# Every build gets a fresh payload; never package files from an installed/user-data directory.
$payload = Join-Path $build ('payload-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $payload,$output | Out-Null
$arguments = @('publish',(Join-Path $PSScriptRoot '..\CopilotPulse.csproj'),'-c','Release','-r',$Runtime,'--self-contained','true','-o',$payload,'-p:NuGetAudit=false','-p:DebugType=None','-p:DebugSymbols=false')
if ($PackageCache) { $arguments += '-p:RestorePackagesPath=' + [IO.Path]::GetFullPath($PackageCache) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\Assets\pulse-usage-v4-c.ico') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '分享与安装说明.txt') -Destination (Join-Path $payload '使用说明.txt')
$forbidden = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object {
    $_.Name -match '^(preferences|consumption|connection|migration|smoke-result)\.json$|\.jsonl$|^errors\.log$' -or
    $_.FullName -match '[\\/](history|WebView2|migration-backups)[\\/]'
}
if ($forbidden) { throw 'User data found in payload; packaging stopped.' }
$compileArgs = @('/Qp', ('/DAppVersion=' + $version), ('/DAppArch=' + $arch), ('/DPayloadDir=' + $payload), ('/DOutputPath=' + $output), ('/DBootstrapper=' + $bootstrapper))
if ($TestPackage) {
    $compileArgs += '/DPackageId=CopilotPulse.Installer.Validation'
    $compileArgs += '/DProductName=Copilot Pulse Installer Test'
    $compileArgs += '/DApplicationMutex=Local\CopilotPulse.Installer.Validation'
}
& $compiler @compileArgs (Join-Path $PSScriptRoot 'CopilotPulse.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$setup = Join-Path $output ('CopilotPulse-Setup-' + $version + '-' + $arch + '.exe')
Get-FileHash -LiteralPath $setup -Algorithm SHA256 | ForEach-Object {
    ($_.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_.Path)) | Set-Content -LiteralPath ($setup + '.sha256') -Encoding ascii
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '分享与安装说明.txt') -Destination $output
Get-Item -LiteralPath $setup | Select-Object FullName,Length
