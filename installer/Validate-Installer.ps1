param(
    [Parameter(Mandatory)][string]$TestSetupPath,
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [Parameter(Mandatory)][string]$ValidationDirectory
)
$ErrorActionPreference = 'Stop'
$setup = (Resolve-Path -LiteralPath $TestSetupPath).Path
$payload = (Resolve-Path -LiteralPath $PayloadDirectory).Path
$validation = [IO.Path]::GetFullPath($ValidationDirectory)
$install = Join-Path $validation 'installed'
New-Item -ItemType Directory -Force -Path $validation | Out-Null
$checks = [Collections.Generic.List[string]]::new()
function Check([string]$Name, [bool]$Condition) {
    if (-not $Condition) { throw "FAILED: $Name" }
    $checks.Add($Name)
    Write-Output "PASS $Name"
}
function RunHidden([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { throw "Process timed out: $File" }
    if ($process.ExitCode -ne 0) { throw "Process failed: $File ($($process.ExitCode))" }
}
function DataHashes {
    $data = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Documents\Codex\CopilotPulse-data'
    $hashes = @{}
    if (Test-Path -LiteralPath $data) {
        Get-ChildItem -LiteralPath $data -Recurse -File | Where-Object { $_.Name -eq 'preferences.json' -or $_.Extension -eq '.jsonl' } | ForEach-Object {
            $hashes[$_.FullName] = [IO.File]::ReadAllBytes($_.FullName)
        }
    }
    return $hashes
}
$before = DataHashes
$installArgs = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/LANG=chinesesimp',('/DIR="' + $install + '"'),'/GROUP="Copilot Pulse Installer Test"','/TASKS=desktopicon',('/LOG="' + (Join-Path $validation 'install.log') + '"'))
RunHidden $setup $installArgs
Check 'per-user install succeeds' (Test-Path -LiteralPath (Join-Path $install 'CopilotPulse.exe'))
foreach ($name in @('CopilotPulse.exe','CopilotPulse.dll','pulse-usage-v4-c.ico','coreclr.dll','hostfxr.dll','WebView2Loader.dll')) {
    Check ("installed payload matches: " + $name) ((Get-FileHash -LiteralPath (Join-Path $install $name)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $payload $name)).Hash)
}
Check 'existing WebView2 is detected without reinstalling' ([IO.File]::ReadAllText((Join-Path $validation 'install.log')).Contains('WebView2 Runtime detected; prerequisite installation skipped.'))
$desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Copilot Pulse Installer Test.lnk'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Copilot Pulse Installer Test\Copilot Pulse Installer Test.lnk'
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcut in @($desktop,$menu)) {
    Check 'shortcut created' (Test-Path -LiteralPath $shortcut)
    $link = $shell.CreateShortcut($shortcut)
    Check 'shortcut points to installed app' ($link.TargetPath -eq (Join-Path $install 'CopilotPulse.exe'))
    Check 'shortcut uses selected C icon' ($link.IconLocation.StartsWith((Join-Path $install 'pulse-usage-v4-c.ico')))
}
$capture = Join-Path $validation 'installed-preview.png'
RunHidden (Join-Path $install 'CopilotPulse.exe') @('--demo','--capture',('"' + $capture + '"'))
Check 'installed app launches and renders' (Test-Path -LiteralPath $capture)
# Reinstall the same package to verify the upgrade path preserves existing files.
$sentinel = Join-Path $install 'upgrade-sentinel.txt'
[IO.File]::WriteAllText($sentinel,'installer-validation')
RunHidden $setup $installArgs
Check 'upgrade preserves existing extra files' ([IO.File]::ReadAllText($sentinel) -eq 'installer-validation')
# Exercise the real detached helper: close the old test app, install, then restart and render.
$updateStage = Join-Path ([IO.Path]::GetTempPath()) ('CopilotPulse-update\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $updateStage | Out-Null
$updateSetup = Join-Path $updateStage ([IO.Path]::GetFileName($setup))
Copy-Item -LiteralPath $setup -Destination $updateSetup
RunHidden (Join-Path $install 'CopilotPulse.exe') @('--review-update', ('"' + $updateSetup + '"'))
$restarted = Join-Path $updateStage 'restarted.png'
$deadline = (Get-Date).AddSeconds(90)
while (-not (Test-Path -LiteralPath $restarted) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
Check 'detached updater installs and automatically restarts client' (Test-Path -LiteralPath $restarted)
$tickets = Get-ChildItem -LiteralPath (Split-Path $updateStage) -Filter 'update-ticket.json' -Recurse -File | Where-Object { ([IO.File]::ReadAllText($_.FullName) | ConvertFrom-Json).Installer -eq $updateSetup }
Check 'updater helper confirms setup succeeded' (@($tickets).Count -eq 1 -and [IO.File]::ReadAllText($tickets[0].FullName + '.result') -eq 'setup-exit=0')
Check 'automatic update preserves extra installed files' ([IO.File]::ReadAllText($sentinel) -eq 'installer-validation')
Check 'automatic update installed expected payload' ((Get-FileHash -LiteralPath (Join-Path $install 'CopilotPulse.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $payload 'CopilotPulse.dll')).Hash)
# The capture client exits just after writing its image; wait for it to release installer files.
Get-Process CopilotPulse -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $install 'CopilotPulse.exe') } | ForEach-Object { if (-not $_.WaitForExit(15000)) { throw 'Restarted test client did not exit.' } }
RunHidden (Join-Path $install 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $validation 'uninstall.log') + '"'))
Check 'uninstall removes application' (-not (Test-Path -LiteralPath (Join-Path $install 'CopilotPulse.exe')))
Check 'uninstall removes test shortcuts' ((-not (Test-Path -LiteralPath $desktop)) -and (-not (Test-Path -LiteralPath $menu)))
$after = DataHashes
$preserved = $true
foreach ($name in $before.Keys) {
    if (-not $after.ContainsKey($name) -or $after[$name].Length -lt $before[$name].Length) { $preserved = $false; continue }
    # A running user's app may append real history during the test; all old bytes must survive.
    if ([IO.Path]::GetExtension($name) -ne '.jsonl' -and $after[$name].Length -ne $before[$name].Length) { $preserved = $false }
    for ($i = 0; $i -lt $before[$name].Length; $i++) { if ($after[$name][$i] -ne $before[$name][$i]) { $preserved = $false; break } }
}
Check 'all existing personal history and settings bytes are preserved' $preserved
$dllBytes = [IO.File]::ReadAllBytes((Join-Path $payload 'CopilotPulse.dll'))
$text = [Text.Encoding]::Unicode.GetString($dllBytes) + [Text.Encoding]::UTF8.GetString($dllBytes)
Check 'application contains no Azure key or internal domain marker' ($text -notmatch 'AAABACOG|FAREAST')
@{ passed = $checks.Count; checks = $checks; verifiedAt = (Get-Date).ToString('o'); missingWebViewInstallTested = $false } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $validation 'validation.json') -Encoding utf8
