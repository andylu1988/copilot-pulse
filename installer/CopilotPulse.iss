#ifndef AppVersion
  #define AppVersion "1.11.0"
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputPath
  #error OutputPath is required
#endif
#ifndef Bootstrapper
  #error Bootstrapper is required
#endif
#ifndef PackageId
  #define PackageId "CopilotPulse.Desktop.User"
#endif
#ifndef ProductName
  #define ProductName "Copilot Pulse"
#endif
#ifndef ApplicationMutex
  #define ApplicationMutex "Local\CopilotPulse"
#endif

[Setup]
AppId={#PackageId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=Copilot Pulse
VersionInfoDescription=Copilot Pulse Windows Installer
DefaultDirName={localappdata}\Programs\CopilotPulse
DefaultGroupName={#ProductName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputPath}
OutputBaseFilename=CopilotPulse-Setup-{#AppVersion}-x64
SetupIconFile={#PayloadDir}\pulse-usage-v4-c.ico
UninstallDisplayIcon={app}\pulse-usage-v4-c.ico
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
AppMutex={#ApplicationMutex}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
InfoBeforeFile=分享与安装说明.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Bootstrapper}"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\CopilotPulse.exe"; WorkingDir: "{app}"; IconFilename: "{app}\pulse-usage-v4-c.ico"
Name: "{userdesktop}\{#ProductName}"; Filename: "{app}\CopilotPulse.exe"; WorkingDir: "{app}"; IconFilename: "{app}\pulse-usage-v4-c.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\CopilotPulse.exe"; Description: "{cm:LaunchProgram,{#ProductName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  WebViewClientKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function HasRuntimeAt(Root: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(Root, WebViewClientKey, 'pv', Version);
  if Result then
    Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function HasWebViewRuntime: Boolean;
begin
  Result := HasRuntimeAt(HKCU32) or HasRuntimeAt(HKCU64) or
    HasRuntimeAt(HKLM32) or HasRuntimeAt(HKLM64);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if HasWebViewRuntime then
  begin
    Log('WebView2 Runtime detected; prerequisite installation skipped.');
    exit;
  end;
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  Log('Installing Microsoft WebView2 Runtime for the current user.');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'),
    '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Unable to start Microsoft WebView2 setup. / 无法启动 WebView2 安装程序。';
    exit;
  end;
  if ((ExitCode <> 0) and (ExitCode <> 3010)) or not HasWebViewRuntime then
    Result := 'WebView2 installation did not complete. Connect to the internet, install Microsoft WebView2 Runtime and retry. / WebView2 安装未完成。请联网安装 Microsoft WebView2 Runtime 后重试。'
  else if ExitCode = 3010 then
    NeedsRestart := True;
end;

// Personal history, preferences and authentication profiles are outside {app}.
// No uninstall-delete rules: upgrades and removal retain these directories.
