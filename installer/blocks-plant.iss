; Blocks Plant — Inno Setup installer
; Compiles with: ISCC.exe installer\blocks-plant.iss
; Expects published output under dist\publish\ (see scripts\publish-installer.ps1)

#define MyAppName "Blocks Plant"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Blocks Plant"
#define MyAppExeName "BlocksPlant.Desktop.exe"
#define MyAppId "{{A8F3C2E1-9B4D-4E6A-8F1C-2D5E7A9B0C3D}"

#ifndef PublishRoot
  #define PublishRoot "..\dist\publish"
#endif

#ifndef DistDir
  #define DistDir "..\dist"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\BlocksPlant
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#DistDir}
OutputBaseFilename=BlocksPlant-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Desktop\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Desktop POS
Source: "{#PublishRoot}\Desktop\*"; DestDir: "{app}\Desktop"; Flags: ignoreversion recursesubdirs createallsubdirs
; API backend (self-contained)
Source: "{#PublishRoot}\backend\*"; DestDir: "{app}\backend"; Flags: ignoreversion recursesubdirs createallsubdirs
; Web dashboard (self-contained)
Source: "{#PublishRoot}\web\*"; DestDir: "{app}\web"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; WorkingDir: "{app}\Desktop"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Desktop\{#MyAppExeName}"; WorkingDir: "{app}\Desktop"; Tasks: desktopicon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Desktop\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Leave SQLite DB / logos under backend (user data) — do not wipe on uninstall by default.
; Type: filesandordirs; Name: "{app}\backend\blocksplant.db"
