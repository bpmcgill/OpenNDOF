; OpenNDOF installer — installs the dashboard AND registers the TDxInput COM
; server so CAD applications (SolidWorks, Solid Edge, AutoCAD, ...) receive
; input without any scripts.  Uninstall removes the COM registration.
;
; Build (from repo root):
;   ISCC.exe installer/OpenNDOF.iss /DAppVersion=1.0.0
;
; The publish output must exist at artifacts\OpenNDOF-Bridge first.

#define MyAppName "OpenNDOF"
; The published app exe is the single source of truth for the version
; (CI sets it via /p:Version). /DAppVersion can still override.
#ifndef AppVersion
#define AppVersion GetFileVersion("..\artifacts\OpenNDOF-Bridge\OpenNDOF.App.exe")
#endif
#define MyAppExeName "OpenNDOF.App.exe"

[Setup]
AppId={{6F87C2E4-9A3B-4B6F-B8D0-2C5E6A7D9F10}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=OpenNDOF
AppPublisherURL=https://github.com/bpmcgill/OpenNDOF
AppSupportURL=https://github.com/bpmcgill/OpenNDOF/issues
DefaultDirName={autopf}\OpenNDOF
DefaultGroupName=OpenNDOF
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=OpenNDOF-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\OpenNDOF-Bridge\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ── TDxInput COM registration (mirrors Register-ComServer.ps1) ────────────────
[Registry]
; 1) TDxInput.Device
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{82C5AB54-C92C-4D52-AAC5-27E25E22604C}";           ValueType: string; ValueName: "";               ValueData: "TDxInput.Device"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\InprocServer32"; ValueType: string; ValueName: "";         ValueData: "{app}\TDxInput.comhost.dll"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{82C5AB54-C92C-4D52-AAC5-27E25E22604C}\ProgID";     ValueType: string; ValueName: "";               ValueData: "TDxInput.Device"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Device";                                      ValueType: string; ValueName: "";               ValueData: "TDxInput.Device"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Device\CLSID";                                ValueType: string; ValueName: "";               ValueData: "{{82C5AB54-C92C-4D52-AAC5-27E25E22604C}"
; 2) TDxInput.Sensor
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{85004B00-1AA7-4777-B1CE-8427301B942D}";           ValueType: string; ValueName: "";               ValueData: "TDxInput.Sensor"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{85004B00-1AA7-4777-B1CE-8427301B942D}\InprocServer32"; ValueType: string; ValueName: "";         ValueData: "{app}\TDxInput.comhost.dll"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{85004B00-1AA7-4777-B1CE-8427301B942D}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{85004B00-1AA7-4777-B1CE-8427301B942D}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{85004B00-1AA7-4777-B1CE-8427301B942D}\ProgID";     ValueType: string; ValueName: "";               ValueData: "TDxInput.Sensor"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Sensor";                                      ValueType: string; ValueName: "";               ValueData: "TDxInput.Sensor"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Sensor\CLSID";                                ValueType: string; ValueName: "";               ValueData: "{{85004B00-1AA7-4777-B1CE-8427301B942D}"
; 3) TDxInput.Keyboard
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{25BBE090-583A-4903-A61B-D0EC629AC4EC}";           ValueType: string; ValueName: "";               ValueData: "TDxInput.Keyboard"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{25BBE090-583A-4903-A61B-D0EC629AC4EC}\InprocServer32"; ValueType: string; ValueName: "";         ValueData: "{app}\TDxInput.comhost.dll"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{25BBE090-583A-4903-A61B-D0EC629AC4EC}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{25BBE090-583A-4903-A61B-D0EC629AC4EC}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{25BBE090-583A-4903-A61B-D0EC629AC4EC}\ProgID";     ValueType: string; ValueName: "";               ValueData: "TDxInput.Keyboard"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Keyboard";                                    ValueType: string; ValueName: "";               ValueData: "TDxInput.Keyboard"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Keyboard\CLSID";                              ValueType: string; ValueName: "";               ValueData: "{{25BBE090-583A-4903-A61B-D0EC629AC4EC}"
; 4) TDxInput.TDxInfo
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{1A960ECE-0E57-4A68-B694-8373114F1FF4}";           ValueType: string; ValueName: "";               ValueData: "TDxInput.TDxInfo"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{1A960ECE-0E57-4A68-B694-8373114F1FF4}\InprocServer32"; ValueType: string; ValueName: "";         ValueData: "{app}\TDxInput.comhost.dll"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{1A960ECE-0E57-4A68-B694-8373114F1FF4}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{1A960ECE-0E57-4A68-B694-8373114F1FF4}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{1A960ECE-0E57-4A68-B694-8373114F1FF4}\ProgID";     ValueType: string; ValueName: "";               ValueData: "TDxInput.TDxInfo"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.TDxInfo";                                      ValueType: string; ValueName: "";               ValueData: "TDxInput.TDxInfo"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.TDxInfo\CLSID";                                ValueType: string; ValueName: "";               ValueData: "{{1A960ECE-0E57-4A68-B694-8373114F1FF4}"
; 5) TDxInput.AngleAxis
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{512A6C3E-3010-401B-8623-E413E2ACC138}";           ValueType: string; ValueName: "";               ValueData: "TDxInput.AngleAxis"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{512A6C3E-3010-401B-8623-E413E2ACC138}\InprocServer32"; ValueType: string; ValueName: "";         ValueData: "{app}\TDxInput.comhost.dll"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{512A6C3E-3010-401B-8623-E413E2ACC138}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{512A6C3E-3010-401B-8623-E413E2ACC138}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{512A6C3E-3010-401B-8623-E413E2ACC138}\ProgID";     ValueType: string; ValueName: "";               ValueData: "TDxInput.AngleAxis"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.AngleAxis";                                    ValueType: string; ValueName: "";               ValueData: "TDxInput.AngleAxis"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.AngleAxis\CLSID";                              ValueType: string; ValueName: "";               ValueData: "{{512A6C3E-3010-401B-8623-E413E2ACC138}"
; 6) TDxInput.Vector3D
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{740A7479-C7C1-44DA-8A84-B5DE63C78B32}";           ValueType: string; ValueName: "";               ValueData: "TDxInput.Vector3D"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{740A7479-C7C1-44DA-8A84-B5DE63C78B32}\InprocServer32"; ValueType: string; ValueName: "";         ValueData: "{app}\TDxInput.comhost.dll"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{740A7479-C7C1-44DA-8A84-B5DE63C78B32}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Both"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{740A7479-C7C1-44DA-8A84-B5DE63C78B32}\InprocServer32"; ValueType: string; ValueName: "RuntimeVersion"; ValueData: "v4.0.30319"
Root: HKLM; Subkey: "SOFTWARE\Classes\CLSID\{{740A7479-C7C1-44DA-8A84-B5DE63C78B32}\ProgID";     ValueType: string; ValueName: "";               ValueData: "TDxInput.Vector3D"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Vector3D";                                    ValueType: string; ValueName: "";               ValueData: "TDxInput.Vector3D"
Root: HKLM; Subkey: "SOFTWARE\Classes\TDxInput.Vector3D\CLSID";                              ValueType: string; ValueName: "";               ValueData: "{{740A7479-C7C1-44DA-8A84-B5DE63C78B32}"

[Icons]
Name: "{group}\OpenNDOF"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall OpenNDOF"; Filename: "{uninstallexe}"
Name: "{autodesktop}\OpenNDOF"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,OpenNDOF}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNetRuntimePresent(): Boolean;
var
  Pf, Ld: string;
begin
  Pf := ExpandConstant('{autopf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  Ld := ExpandConstant('{localappdata}\Microsoft\dotnet\shared\Microsoft.WindowsDesktop.App');
  Result := DirExists(Pf) or DirExists(Ld);
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not IsDotNetRuntimePresent() then
  begin
    Result := MsgBox(
      'OpenNDOF requires the Microsoft .NET 10 Desktop Runtime, which was not found.' #13#13 +
      'Download and install it now?', mbConfirmation, MB_YESNO) = IDYES;
    if Result then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '',
                SW_SHOWNORMAL, ewNoWait, ResultCode);
      Result := False; // refuse to install until the runtime is present
    end;
  end;
end;