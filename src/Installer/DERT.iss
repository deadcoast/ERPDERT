; Compile through ../Build-Installer.ps1. The proof build changes names/roots,
; while running the same installation, PATH and removal code.
#if VER < EncodeVer(6, 7, 3) || VER >= EncodeVer(6, 7, 4)
  #error Use the pinned Inno Setup 6.7.3 compiler
#endif
#ifndef PackageDirectory
  #error PackageDirectory is required
#endif
#ifndef BuildVersion
  #define BuildVersion "0.0.0"
#endif
#ifdef ProofId
  #define ProductId "DERT.Proof." + ProofId
  #define ProductName "DERT installation proof"
  #define GroupName "DERT proof " + ProofId
  #define StateKey "Software\DERT\InstallationProof\" + ProofId
  #define ProgId "DERT.Proof." + ProofId
  #define Extension ".dert-proof-" + ProofId
  #define PathValue "DERT_Proof_" + ProofId
  #define ExamplesDirectory ProofRoot + "\Documents\DERT examples"
  #define DefaultDirectory ProofRoot + "\DERT"
  #define OutputName "DERT-Proof-" + ProofId
#else
  #define ProductId "DERT.NotesAndCli"
  #define ProductName "DERT"
  #define GroupName "DERT"
  #define StateKey "Software\DERT\Setup"
  #define ProgId "DERT.Note"
  #define Extension ".DERT"
  #define PathValue "Path"
  #define ExamplesDirectory "{userdocs}\DERT examples"
  #define DefaultDirectory "{localappdata}\Programs\DERT"
  #define OutputName "DERT-Setup"
#endif

[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion=workspace build
VersionInfoVersion={#BuildVersion}
AppVerName={#ProductName} / workspace build
AppPublisher=MIVIM
DefaultDirName={#DefaultDirectory}
DefaultGroupName={#GroupName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19044
WizardStyle=modern dark
WizardSizePercent=115
DisableWelcomePage=no
DisableDirPage=no
ChangesAssociations=yes
ChangesEnvironment=yes
CloseApplications=no
RestartApplications=no
RestartIfNeededByRun=no
AlwaysRestart=no
AllowUNCPath=no
AllowRootDirectory=no
AppMutex=Local\DERT.Application.Running
UninstallDisplayIcon={app}\notes\DERT.Notes.exe
OutputBaseFilename={#OutputName}
Compression=lzma2/fast
SolidCompression=yes
SetupLogging=yes

[Messages]
WelcomeLabel1=A little room for your words.
WelcomeLabel2=DERT brings expressive notes and its command line to this Windows account.%n%nYour notes can be the whole reason you stay.%n%nSetup includes everything needed to write. An editable example goes in Documents\DERT examples; future installs and removal keep your words.
SelectDirDesc=Choose where DERT's application files will live.
SelectDirLabel3=Your saved notes and editable examples live separately. Setup manages the application files in the folder below.
SelectTasksDesc=Choose how you will return to DERT.
SelectTasksLabel2=Start includes compact DERT Notes, DERT Help and your editable example. These additional connections are yours to choose:
ReadyLabel1=Ready to make room for DERT.
FinishedHeadingLabel=DERT is here.
FinishedLabel=Open your example below, or find DERT Notes in Start.%n%nChoose DERT Notes from a .DERT file's Open with menu to make it your default. Command-line users: open a new terminal for PATH changes.%n%nYour examples stay in Documents\DERT examples. Windows Installed apps can remove DERT while keeping your notes.
ConfirmUninstall=Remove DERT's application files and Windows connections?%n%nYour notes, editable examples and recovery copies will stay.
UninstalledAll=DERT's application files and connections have been removed.%n%nYour notes, editable examples and recovery copies are still yours.
SetupAppRunningError=Close DERT Notes and let any DERT commands finish before continuing.%n%nYour open work will stay open until you close it yourself.

[Tasks]
Name: "commandline"; Description: "Use DERT from a new terminal (add its folder to my PATH)"
Name: "fileassociation"; Description: "Offer DERT Notes when opening .DERT files (keep my current default)"

[Files]
Source: "{#PackageDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; These become the author's working copies, not uninstall-owned program files.
Source: "{#PackageDirectory}\examples\*.DERT"; DestDir: "{#ExamplesDirectory}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{userprograms}\{#GroupName}\DERT Notes"; Filename: "{app}\notes\DERT.Notes.exe"; WorkingDir: "{userdocs}"
Name: "{userprograms}\{#GroupName}\DERT Help"; Filename: "{app}\notes\DERT.Notes.exe"; Parameters: "--guide"; WorkingDir: "{userdocs}"
Name: "{userprograms}\{#GroupName}\Your DERT example"; Filename: "{app}\notes\DERT.Notes.exe"; Parameters: """{#ExamplesDirectory}\weekend.DERT"""; WorkingDir: "{#ExamplesDirectory}"

[Registry]
Root: HKCU; Subkey: "{#StateKey}"; ValueType: string; ValueName: "InstallDirectory"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueData: "DERT note"; Tasks: fileassociation; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueData: """{app}\notes\DERT.Notes.exe"",0"; Tasks: fileassociation
Root: HKCU; Subkey: "Software\Classes\{#ProgId}\Application"; ValueType: string; ValueName: "ApplicationName"; ValueData: "DERT Notes"; Tasks: fileassociation
Root: HKCU; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueData: """{app}\notes\DERT.Notes.exe"" ""%1"""; Tasks: fileassociation
Root: HKCU; Subkey: "Software\Classes\{#Extension}\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Tasks: fileassociation; Flags: uninsdeletevalue uninsdeletekeyifempty

[Run]
Filename: "{app}\notes\DERT.Notes.exe"; Parameters: """{#ExamplesDirectory}\weekend.DERT"""; Description: "Open my editable DERT example"; Flags: nowait postinstall skipifsilent; Check: IntegrationComplete

[UninstallDelete]
Type: files; Name: "{app}\.dert-install-owner"

#include "UserPath.iss"

[Code]
var IntegrationFailure: String;

function CreateFileForFlush(FileName: String; Access, Share: Cardinal;
  Security: Integer; Creation, Attributes: Cardinal; Template: Integer): Integer;
  external 'CreateFileW@kernel32.dll stdcall';
function FlushFileBuffers(Handle: Integer): Boolean;
  external 'FlushFileBuffers@kernel32.dll stdcall';
function CloseFileHandle(Handle: Integer): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function IntegrationComplete: Boolean;
begin
  Result := IntegrationFailure = '';
end;

function OwnershipText(Directory: String): AnsiString;
begin
  Result := 'DERT setup ownership v1: {#ProductId}' + #13#10 +
    GetSHA256OfUnicodeString(Uppercase(RemoveBackslashUnlessRoot(Directory)));
end;

function OwnsInterruptedDirectory(Directory: String): Boolean;
var Marker: AnsiString;
begin
  Result := LoadStringFromFile(AddBackslash(Directory) + '.dert-install-owner', Marker) and
    (Marker = OwnershipText(Directory));
end;

function WithdrawFileAssociation: String;
var Handle, Status: Integer; Kind, Size: Cardinal;
begin
  Result := '';
  Status := RegOpenKeyEx(HKCU, 'Software\Classes\{#Extension}\OpenWithProgids', 0, 3, Handle);
  if (Status <> 0) and (Status <> 2) then begin
    Result := 'Windows could not read the DERT Open with registration.';
    Exit;
  end;
  if Status = 0 then begin
    Kind := 0; Size := 0;
    Status := RegQueryValueType(Handle, '{#ProgId}', 0, Kind, 0, Size);
    RegCloseKey(Handle);
    if ((Status <> 0) and (Status <> 2)) or
      ((Status = 0) and not RegDeleteValue(HKCU, 'Software\Classes\{#Extension}\OpenWithProgids', '{#ProgId}')) then begin
      Result := 'Windows could not withdraw the DERT Open with choice.';
      Exit;
    end;
  end;
  Status := RegOpenKeyEx(HKCU, 'Software\Classes\{#ProgId}', 0, 1, Handle);
  if Status = 0 then begin
    RegCloseKey(Handle);
    if not RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\{#ProgId}') then
      Result := 'Windows could not withdraw the DERT file-opening command.';
  end else if Status <> 2 then Result := 'Windows could not read the DERT file-opening registration.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Directory, Registered: String; Find: TFindRec;
begin
  Result := '';
  { The startup AppMutex check predates the wizard. Recheck at the write boundary. }
  if CheckForMutexes('Local\DERT.Application.Running') then begin
    Result := 'Close DERT Notes and let any DERT commands finish before continuing. Your open work has been left alone.';
    Exit;
  end;
  if WizardIsTaskSelected('commandline') or RegValueExists(HKCU, '{#StateKey}', 'AddedPath') then begin
    Result := CheckUserPath;
    if Result <> '' then Exit;
  end;
  Directory := RemoveBackslashUnlessRoot(ExpandConstant('{app}'));
  if (Pos(';', Directory) > 0) or (Pos('%', Directory) > 0) then begin
    Result := 'Choose an application folder without semicolons or percent signs. Document names can still contain those characters.';
    Exit;
  end;
  if RegQueryStringValue(HKCU, '{#StateKey}', 'InstallDirectory', Registered) then begin
    if CompareText(RemoveBackslashUnlessRoot(Registered), Directory) <> 0 then
      Result := 'DERT is already installed at ' + Registered + '. Keep that folder, or remove DERT in Windows Installed apps before moving it.';
  end else if not OwnsInterruptedDirectory(Directory) and FindFirst(AddBackslash(Directory) + '*', Find) then begin
    try
      repeat
        if (Find.Name <> '.') and (Find.Name <> '..') then begin
          Result := 'That folder already contains files. Choose an empty application folder so your existing files stay yours.';
          Break;
        end;
      until not FindNext(Find);
    finally FindClose(Find); end;
  end;
end;

procedure CurStepChanged(Step: TSetupStep);
var AssociationFailure, MarkerPath: String; MarkerHandle: Integer;
  Flushed, MarkerWasAbsent: Boolean;
begin
  if (Step = ssInstall) and not OwnsInterruptedDirectory(ExpandConstant('{app}')) then begin
    { ssInstall exceptions are fatal before PerformInstall. This tiny marker is
      written before any payload copy, so a power interruption remains retryable.
      Reuse valid evidence: truncating it on retry would reopen that failure. }
    MarkerPath := ExpandConstant('{app}\.dert-install-owner');
    MarkerWasAbsent := not FileExists(MarkerPath);
    try
      if not ForceDirectories(ExpandConstant('{app}')) or
        not SaveStringToFile(MarkerPath, OwnershipText(ExpandConstant('{app}')), False) then
        RaiseException('Setup could not record ownership of its application folder. No application files have been copied. Choose a writable folder and try again.');
      MarkerHandle := CreateFileForFlush(MarkerPath, $40000000, 1, 0, 3, 0, 0);
      Flushed := False;
      if MarkerHandle <> -1 then begin
        Flushed := FlushFileBuffers(MarkerHandle);
        CloseFileHandle(MarkerHandle);
      end;
      if not Flushed or not OwnsInterruptedDirectory(ExpandConstant('{app}')) then
        RaiseException('Setup could not verify its recovery marker. No application files have been copied. Check available space and try again.');
    except
      { Before the payload, discard only the incomplete marker we just created.
        Never delete a pre-existing marker or an application's directory. }
      if MarkerWasAbsent and not DeleteFile(MarkerPath) and FileExists(MarkerPath) then
        Log('Could not remove the incomplete recovery marker: ' + MarkerPath);
      RaiseException(GetExceptionMessage);
    end;
  end;
  if Step = ssPostInstall then begin
#ifdef ProofId
    { Fault injection exists only in the test binary. Exercise a late Windows
      write failure through the same integration and completion paths. }
    if ExpandConstant('{param:ProofPathFailure|0}') = '1' then
      RegWriteDWordValue(HKCU, 'Environment', '{#PathValue}', 42);
#endif
    if WizardIsTaskSelected('commandline') then IntegrationFailure := AddUserPath
    else if RemoveUserPath then ForgetPathOwnership
    else IntegrationFailure := 'Windows could not withdraw the previous DERT command-path entry.';
    if not WizardIsTaskSelected('fileassociation') then begin
      AssociationFailure := WithdrawFileAssociation;
      if AssociationFailure <> '' then IntegrationFailure := IntegrationFailure + #13#10 + AssociationFailure;
    end;
    if IntegrationFailure <> '' then begin
      Log('DERT integration incomplete: ' + IntegrationFailure);
      SuppressibleMsgBox(IntegrationFailure + #13#10#13#10 + 'Application files are installed. Start still opens DERT Notes. The Windows connection described above needs attention.', mbError, MB_OK, IDOK);
    end;
  end;
end;

procedure CurPageChanged(PageID: Integer);
begin
  if (PageID = wpFinished) and not IntegrationComplete then begin
    WizardForm.FinishedHeadingLabel.Caption := 'DERT needs attention.';
    WizardForm.FinishedLabel.Caption := IntegrationFailure + #13#10#13#10 + 'Application files are installed and Start can open DERT Notes. Re-run setup to retry the Windows connection, or remove DERT in Installed apps.';
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  if IntegrationComplete then Result := 0 else Result := 1;
end;

function InitializeUninstall: Boolean;
begin
  Result := not CheckForMutexes('Local\DERT.Application.Running');
  if not Result then begin
    SuppressibleMsgBox('Close DERT Notes and let any DERT commands finish before removing DERT. Your open work has been left alone.', mbInformation, MB_OK, IDOK);
    Exit;
  end;
end;

procedure CurUninstallStepChanged(Step: TUninstallStep);
begin
  { Initialization precedes the user's confirmation. Only mutate after it. }
  if (Step = usUninstall) and not RemoveUserPath then
    RaiseException('Windows could not remove the DERT command-path entry. Keep the removal log and check your user environment permissions.');
end;
