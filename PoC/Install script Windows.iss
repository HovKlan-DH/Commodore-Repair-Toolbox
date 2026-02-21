; Commodore Repair Toolbox - Inno Setup Script
; Place this file in: D:\Data\Development\VSCode\PoC_1\PoC_1\

#define AppName "PoC 1"
#define AppVersion "2026-February-22"
#define AppPublisher "HovKlan-DH"
#define AppExeName "PoC_1.exe"
#define PublishDir "bin\Release\net10.0\win-x64\publish"

[Setup]
; Basic application info
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisherURL=https://github.com/HovKlan-DH/Commodore-Repair-Toolbox
AppPublisher={#AppPublisher}

; Where it gets installed on the user's machine
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

; Output - where the final installer EXE is created
OutputDir=installer_output
OutputBaseFilename=PoC1-Setup-{#AppVersion}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Minimum Windows version (Windows 7 = 6.1)
MinVersion=6.1

; Request admin rights for installation
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Optional: desktop shortcut checkbox during install
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Your main executable
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Required native DLLs (Avalonia/Skia - cannot be bundled into EXE)
Source: "{#PublishDir}\libSkiaSharp.dll";     DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\libHarfBuzzSharp.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcut
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop shortcut (only if user checked the box during install)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Optionally launch the app after installation finishes
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent