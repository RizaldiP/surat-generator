#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "Surat Generator"
#define MyAppExeName "SuratGenerator.Desktop.exe"
#define MyAppIcon "..\SuratGenerator.Desktop\Resources\Icons\SuratGenerator.ico"

[Setup]
AppId=5F1D1B9A-6A9E-4E2C-9D7A-8E2B5B3C7A11
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
DefaultDirName={autopf}\SuratGenerator
DefaultGroupName=Surat Generator
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
CreateAppDir=yes
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputBaseFilename=SuratGenerator-Setup
OutputDir=..\artifacts
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\SuratGenerator.ico
UninstallDisplayName={#MyAppName} {#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
VersionInfoCompany=Surat Generator
VersionInfoDescription=Installer for Surat Generator
VersionInfoOriginalFileName=SuratGenerator-Setup.exe
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoVersion={#MyAppVersion}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\Release\SuratGenerator\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyAppIcon}"; DestDir: "{app}"; DestName: "SuratGenerator.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Surat Generator\Surat Generator"; Filename: "{app}\SuratGenerator.Desktop.exe"; IconFilename: "{app}\SuratGenerator.ico"; WorkingDir: "{app}"
Name: "{autodesktop}\Surat Generator"; Filename: "{app}\SuratGenerator.Desktop.exe"; IconFilename: "{app}\SuratGenerator.ico"; WorkingDir: "{app}"; Tasks: desktopicon

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Code]
function WordIsRegistered: Boolean;
begin
  Result := RegKeyExists(HKEY_CLASSES_ROOT, 'Word.Application\CLSID');
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not WordIsRegistered then
    MsgBox('Microsoft Word 2016 or later was not detected on this computer.' + #13#10 +
      'Word is required for PDF export, Word export, and printing. Setup will continue, but those features will be unavailable until Word is installed.',
      mbInformation, MB_OK);
end;
