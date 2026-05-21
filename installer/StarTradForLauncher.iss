#define AppName "StarTrad"
#define AppVersion "1.0.0"
#define AppPublisher "Circus"
#define AppExeName "StarTrad.exe"
#define SourceRoot ".."

[Setup]
AppId={{1D3EC3F2-785D-4D7E-8D3D-9BCE13A1D90D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\StarTrad
DisableDirPage=no
DefaultGroupName=Circus
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=StarTrad_Setup_v{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes

[Files]
Source: "{#SourceRoot}\dist\StarTrad.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\dist\startrad_launcher_version.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\dist\circus_launcher_startrad_version.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\StarTrad"; Filename: "{app}\{#AppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Circus\Tools\startrad"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Circus\Tools\startrad"; ValueType: string; ValueName: "ExecutablePath"; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Circus\Tools\startrad"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Lancer StarTrad"; Flags: nowait postinstall skipifsilent
