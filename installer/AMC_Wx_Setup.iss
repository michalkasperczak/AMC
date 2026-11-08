; Instalator bieżącego AMC z interfejsem wxPython.
; Źródłem jest kompletny folder z bundle-manifest.json, zweryfikowany przed
; kompilacją przez Build-AMCInstaller.ps1. Profil jest zamkniętym wyborem:
; public albo test. Oba warianty mają inne AppId, foldery, skróty i muteksy.

#ifndef Wersja
  #define Wersja "0.0.0"
#endif
#ifndef Zrodlo
  #error "Podaj /DZrodlo=<zweryfikowany folder paczki>"
#endif
#ifndef Wyjscie
  #define Wyjscie ".\output"
#endif
#ifndef Profil
  #define Profil "public"
#endif
#ifndef EtykietaUzytkowa
  #error "Podaj /DEtykietaUzytkowa=<data i godzina paczki>"
#endif
#ifndef EtykietaPliku
  #error "Podaj /DEtykietaPliku=<bezpieczna data i godzina pliku>"
#endif
#ifdef NvdaAddonPath
  #ifndef NvdaAddonFileName
    #error "Podaj /DNvdaAddonFileName=<nazwa paczki dodatku NVDA>"
  #endif
  #ifndef NvdaAddonVersion
    #error "Podaj /DNvdaAddonVersion=<wersja dodatku NVDA>"
  #endif
#endif

#if Profil == "public"
  #define Produkt "AMC Public"
  #define KrotkaNazwa "AMC Public"
  #define AppIdentity "{{1E437448-30B4-4E4A-BCA1-CDA114B096A9}"
  #define FolderProgramu "AMC Public"
  #define NazwaWyjscia "AMC-Public-" + EtykietaPliku
  #define AppPathName "AMC.exe"
  #define ExpectedInstallId "AMC.Public.win-x64"
  #define LauncherMutex "Local\AccessibleMultimediaController.PythonLauncher.Public.SingleInstance"
  #define ShellProfileKey "Public"
  #define ShellRegisteredApplication "Accessible Multimedia Controller"
  #define ShellDisplayName "AMC Public"
  #define ShellOpenLabel "Otwórz w AMC Public"
  #define ShellQueueLabel "Dodaj do kolejki AMC Public"
#elif Profil == "test"
  #define Produkt "AMC Test"
  #define KrotkaNazwa "AMC Test"
  #define AppIdentity "{{0B807571-BDC5-4287-BD7C-4F4A7C71438F}"
  #define FolderProgramu "AMC Test"
  #define NazwaWyjscia "AMC-Test-" + EtykietaPliku
  #define AppPathName "AMC-Test.exe"
  #define ExpectedInstallId "AMC.Test.win-x64"
  #define LauncherMutex "Local\AccessibleMultimediaController.PythonLauncher.SingleInstance"
  #define ShellProfileKey "Test"
  #define ShellRegisteredApplication "Accessible Multimedia Controller — Test"
  #define ShellDisplayName "AMC Test"
  #define ShellOpenLabel "Otwórz w AMC Test"
  #define ShellQueueLabel "Dodaj do kolejki AMC Test"
#else
  #error "Nieznany profil. Dozwolone sa tylko public i test."
#endif

[Setup]
AppId={#AppIdentity}
AppName={#Produkt}
AppVersion={#Wersja}
AppVerName={#KrotkaNazwa} — {#EtykietaUzytkowa}
AppPublisher=Michał Kasperczak
DefaultDirName={localappdata}\Programs\{#FolderProgramu}
DefaultGroupName={#KrotkaNazwa}
UninstallDisplayName={#Produkt}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2/max
SolidCompression=yes
OutputBaseFilename={#NazwaWyjscia}
OutputDir={#Wyjscie}
SourceDir={#Zrodlo}
PrivilegesRequired=lowest
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableProgramGroupPage=auto
AllowNoIcons=yes
DisableStartupPrompt=yes
DisableWelcomePage=no
DisableReadyPage=no
Uninstallable=yes
SetupLogging=yes
WizardStyle=classic
ChangesAssociations=yes

; Instalator nigdy nie zamyka AMC przez Restart Manager. AppMutex zatrzymuje
; podmianę, gdy właściwy profil nadal działa. Użytkownik zamyka program zwykłą
; drogą, a ta droga odmawia zamknięcia podczas nagrywania lub finalizacji.
AppMutex={#LauncherMutex}
CloseApplications=no
RestartApplications=no

[Languages]
Name: "polski"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "shellcontext"; Description: "Dodaj polecenia Otwórz i Dodaj do kolejki w menu kontekstowym obsługiwanych plików"; \
    GroupDescription: "Integracja z Eksploratorem Windows:"; Flags: unchecked
Name: "shellopenwith"; Description: "Pokaż {#KrotkaNazwa} na liście Otwórz za pomocą i w ustawieniach aplikacji domyślnych"; \
    GroupDescription: "Integracja z Eksploratorem Windows:"; Flags: unchecked
Name: "desktopicon"; Description: "Utwórz skrót na pulpicie"; \
    GroupDescription: "Dodatkowe skróty:"; Flags: unchecked

[Files]
Source: "*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifdef NvdaAddonPath
Source: "{#NvdaAddonPath}"; DestDir: "{tmp}"; DestName: "{#NvdaAddonFileName}"; \
    Flags: ignoreversion deleteafterinstall
#endif

; Inno Setup kopiuje i nadpisuje pliki, ale sam nie usuwa plików, które były
; obecne w poprzedniej wersji paczki. Publiczna aktualizacja musi więc usunąć
; źródła usług niedostępnych w tym profilu także przy instalacji na istniejący
; katalog. Bez tego wycofany moduł pozostawał po aktualizacji mimo jego braku
; w bundle-manifest.json. Frontier Smart jest obsługiwany przez profil
; publiczny, a wycofany z Public moduł Sonosa musi zniknąć przy aktualizacji.
[InstallDelete]
; LibreSpot nie jest juz odtwarzaczem AMC. Usun stary host przy aktualizacji
; obu profili; ustawienia i dane kont uzytkownika pozostaja nietkniete.
Type: filesandordirs; Name: "{app}\host\LibrespotHost"
Type: filesandordirs; Name: "{app}\host\licenses\librespot-dependencies"
Type: files; Name: "{app}\host\licenses\librespot-MIT.txt"
#if Profil == "public"
Type: files; Name: "{app}\app\amc_wx_lite\sonos_source.py"
Type: files; Name: "{app}\app\amc_wx_lite\spotify_playlist_dialog.py"
Type: files; Name: "{app}\app\amc_wx_lite\spotify_source.py"
Type: files; Name: "{app}\app\amc_wx_lite\tidal_source.py"
Type: files; Name: "{app}\app\amc_wx_lite\__pycache__\sonos_source.*.pyc"
Type: files; Name: "{app}\app\amc_wx_lite\__pycache__\spotify_playlist_dialog.*.pyc"
Type: files; Name: "{app}\app\amc_wx_lite\__pycache__\spotify_source.*.pyc"
Type: files; Name: "{app}\app\amc_wx_lite\__pycache__\tidal_source.*.pyc"
#endif

[Icons]
Name: "{group}\{#KrotkaNazwa}"; Filename: "{app}\AMC.exe"; WorkingDir: "{app}"; \
    Check: not WizardNoIcons
Name: "{group}\Odinstaluj {#KrotkaNazwa}"; Filename: "{uninstallexe}"; \
    Check: not WizardNoIcons
Name: "{autodesktop}\{#KrotkaNazwa}"; Filename: "{app}\AMC.exe"; WorkingDir: "{app}"; \
    Comment: "Uruchom lub przywołaj {#KrotkaNazwa}"; Tasks: desktopicon; \
    Check: not WizardNoIcons

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppPathName}"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\AMC.exe"; Flags: uninsdeletekey

[Run]
#ifdef NvdaAddonPath
Filename: "{tmp}\{#NvdaAddonFileName}"; \
    Description: "Uruchom instalację dodatku NVDA Quick App Switcher {#NvdaAddonVersion}"; \
    Flags: shellexec postinstall skipifsilent; Check: ShouldOfferNvdaAddon
#endif
Filename: "{app}\AMC.exe"; Description: "Uruchom {#KrotkaNazwa} teraz"; \
    Flags: nowait postinstall skipifsilent

[Code]
const
  ShellExtensions = '.mp3|.m4a|.aac|.flac|.wav|.ogg|.oga|.opus|.wma|' +
    '.mp2|.wave|.rf64|.bwf|.adts|.asf|.aif|.aiff|.aifc|.mka|.ac3|' +
    '.eac3|.ec3|.amr|.mkv|.webm|.mp4|.m4v|.mov|.ogv|.avi|.wmv|' +
    '.mpeg|.mpg|.mpe|.ts|.mts|.m2ts|.vob|.flv|.3gp|.3g2|.3gp2|' +
    '.3gpp|.m3u|.m3u8|.pls|.xspf|.amc-partial';
  ShellClassesRoot = 'Software\Classes\';
  ShellCapabilitiesRoot = 'Software\AccessibleMultimediaController\{#ShellProfileKey}\Capabilities';
  ShellSettingsRoot = 'Software\AccessibleMultimediaController\{#ShellProfileKey}\ShellIntegration';
  ShellRegisteredApplication = '{#ShellRegisteredApplication}';
  ShellClassPrefix = 'AccessibleMediaController.{#ShellProfileKey}.';
  ShellVerbPrefix = 'AccessibleMediaController.{#ShellProfileKey}.';

var
  SelectedShellExtensions: string;
  ShellContextEnabled: Boolean;
  ShellOpenWithEnabled: Boolean;

function DirectoryHasEntries(const Directory: string): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if not FindFirst(AddBackslash(Directory) + '*', FindRec) then
    Exit;
  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function ExistingDirectoryBelongsToThisProfile(const Directory: string): Boolean;
var
  Manifest: AnsiString;
  ManifestPath: string;
begin
  Result := False;
  ManifestPath := AddBackslash(Directory) + 'bundle-manifest.json';
  if (not FileExists(ManifestPath)) or
     (not LoadStringFromFile(ManifestPath, Manifest)) then
    Exit;
  Result := Pos('"installId": "{#ExpectedInstallId}"', String(Manifest)) > 0;
end;

#ifdef NvdaAddonPath
function NextVersionPart(var Value: string): Integer;
var
  Separator: Integer;
  Part: string;
begin
  Separator := Pos('.', Value);
  if Separator = 0 then
  begin
    Part := Value;
    Value := '';
  end
  else
  begin
    Part := Copy(Value, 1, Separator - 1);
    Delete(Value, 1, Separator);
  end;
  Result := StrToIntDef(Trim(Part), -1);
end;

function CompareDottedVersions(Left, Right: string): Integer;
var
  Index, LeftPart, RightPart: Integer;
begin
  Result := 0;
  for Index := 1 to 4 do
  begin
    LeftPart := NextVersionPart(Left);
    RightPart := NextVersionPart(Right);
    if (LeftPart < 0) or (RightPart < 0) then
    begin
      Result := -1;
      Exit;
    end;
    if LeftPart < RightPart then
    begin
      Result := -1;
      Exit;
    end;
    if LeftPart > RightPart then
    begin
      Result := 1;
      Exit;
    end;
  end;
end;

function InstalledQuickAppSwitcherVersion: string;
var
  Manifest: AnsiString;
  Text, Needle, Tail: string;
  Start, LineEnd: Integer;
begin
  Result := '';
  if not LoadStringFromFile(
      ExpandConstant('{userappdata}\nvda\addons\quickAppSwitcher\manifest.ini'),
      Manifest) then
    Exit;
  Text := #10 + String(Manifest);
  Needle := #10 + 'version =';
  Start := Pos(Needle, Text);
  if Start = 0 then
    Exit;
  Tail := Copy(Text, Start + Length(Needle), Length(Text));
  LineEnd := Pos(#10, Tail);
  if LineEnd > 0 then
    Delete(Tail, LineEnd, Length(Tail));
  Result := Trim(Tail);
end;

function ShouldOfferNvdaAddon: Boolean;
var
  InstalledVersion: string;
begin
  InstalledVersion := InstalledQuickAppSwitcherVersion;
  Result := (InstalledVersion = '') or
    (CompareDottedVersions(InstalledVersion, '{#NvdaAddonVersion}') < 0);
end;
#endif

function PrepareToInstall(var NeedsRestart: Boolean): string;
var
  Target: string;
begin
  Result := '';
  Target := ExpandConstant('{app}');
  if (not DirExists(Target)) or (not DirectoryHasEntries(Target)) or
     ExistingDirectoryBelongsToThisProfile(Target) then
    Exit;

  Result :=
    'Wybrany folder zawiera inną albo historyczną wersję programu. ' +
    'Instalator nie nadpisze jej plików. Wróć i wybierz pusty folder albo ' +
    'najpierw odinstaluj poprzedni program.';
end;

function PopShellExtension(var Values: string): string;
var
  Separator: Integer;
begin
  Separator := Pos('|', Values);
  if Separator = 0 then
  begin
    Result := Values;
    Values := '';
  end
  else
  begin
    Result := Copy(Values, 1, Separator - 1);
    Delete(Values, 1, Separator);
  end;
end;

function ShellClassFor(const Extension: string): string;
begin
  Result := ShellClassPrefix + Copy(Extension, 2, Length(Extension) - 1);
end;

procedure LoadShellIntegrationSelection;
var
  Managed, ContextValue, OpenWithValue: Cardinal;
  StoredExtensions: string;
begin
  { Po pierwszym zapisie z Ustawień to aplikacja jest źródłem zamiaru
    użytkownika. Aktualizacja instalatora nie może z powrotem włączyć 48
    rozszerzeń tylko dlatego, że zapamiętała starszy zestaw zadań. }
  if RegQueryDWordValue(HKA, ShellSettingsRoot, 'ManagedByApplication', Managed)
     and (Managed = 1) then
  begin
    ShellContextEnabled :=
      RegQueryDWordValue(HKA, ShellSettingsRoot, 'ContextMenuEnabled', ContextValue)
      and (ContextValue = 1);
    ShellOpenWithEnabled :=
      RegQueryDWordValue(HKA, ShellSettingsRoot, 'OpenWithEnabled', OpenWithValue)
      and (OpenWithValue = 1);
    if not RegQueryStringValue(
         HKA, ShellSettingsRoot, 'SelectedExtensions', StoredExtensions) then
      StoredExtensions := '';
    SelectedShellExtensions := StoredExtensions;
  end
  else
  begin
    ShellContextEnabled := WizardIsTaskSelected('shellcontext');
    ShellOpenWithEnabled := WizardIsTaskSelected('shellopenwith');
    SelectedShellExtensions := ShellExtensions;
  end;
end;

function ShellExtensionSelected(const Extension: string): Boolean;
begin
  { Rejestr może zmienić także inny program. Iterujemy zawsze po zamkniętym
    katalogu instalatora, więc obcy tekst nie stanie się nazwą klucza. }
  Result := Pos(
    '|' + Lowercase(Extension) + '|',
    '|' + Lowercase(SelectedShellExtensions) + '|') > 0;
end;

procedure RemoveShellIntegration;
var
  Remaining, Extension, ClassName, AssociationRoot: string;
begin
  RegDeleteValue(HKA, 'Software\RegisteredApplications', ShellRegisteredApplication);
  RegDeleteKeyIncludingSubkeys(HKA, ShellCapabilitiesRoot);
  Remaining := ShellExtensions;
  while Remaining <> '' do
  begin
    Extension := PopShellExtension(Remaining);
    ClassName := ShellClassFor(Extension);
    AssociationRoot := ShellClassesRoot + 'SystemFileAssociations\' + Extension +
      '\shell\' + ShellVerbPrefix;
    RegDeleteKeyIncludingSubkeys(HKA, AssociationRoot + 'open');
    RegDeleteKeyIncludingSubkeys(HKA, AssociationRoot + 'queue');
    RegDeleteValue(
      HKA,
      ShellClassesRoot + Extension + '\OpenWithProgids',
      ClassName);
    RegDeleteKeyIncludingSubkeys(HKA, ShellClassesRoot + ClassName);
  end;
end;

procedure RegisterShellIntegration;
var
  Remaining, Extension, ClassName, ClassRoot, AssociationRoot,
    Executable, Command: string;
begin
  Executable := ExpandConstant('{app}\AMC.exe');
  Remaining := ShellExtensions;

  if ShellOpenWithEnabled and (SelectedShellExtensions <> '') then
  begin
    RegWriteStringValue(
      HKA,
      'Software\RegisteredApplications',
      ShellRegisteredApplication,
      ShellCapabilitiesRoot);
    RegWriteStringValue(
      HKA,
      ShellCapabilitiesRoot,
      'ApplicationName',
      '{#ShellDisplayName}');
    RegWriteStringValue(
      HKA,
      ShellCapabilitiesRoot,
      'ApplicationDescription',
      'Dostępny odtwarzacz i organizator multimediów');
  end;

  while Remaining <> '' do
  begin
    Extension := PopShellExtension(Remaining);
    if not ShellExtensionSelected(Extension) then
      Continue;
    ClassName := ShellClassFor(Extension);
    ClassRoot := ShellClassesRoot + ClassName;
    AssociationRoot := ShellClassesRoot + 'SystemFileAssociations\' + Extension +
      '\shell\' + ShellVerbPrefix;

    if ShellContextEnabled then
    begin
      RegWriteStringValue(HKA, AssociationRoot + 'open', '', '{#ShellOpenLabel}');
      RegWriteStringValue(HKA, AssociationRoot + 'open', 'Icon', Executable + ',0');
      Command := '"' + Executable + '" "%1"';
      RegWriteStringValue(HKA, AssociationRoot + 'open\command', '', Command);
      RegWriteStringValue(HKA, AssociationRoot + 'queue', '', '{#ShellQueueLabel}');
      RegWriteStringValue(HKA, AssociationRoot + 'queue', 'Icon', Executable + ',0');
      Command := '"' + Executable + '" /kolejka "%1"';
      RegWriteStringValue(HKA, AssociationRoot + 'queue\command', '', Command);
    end;

    if ShellOpenWithEnabled then
    begin
      RegWriteStringValue(
        HKA,
        ShellCapabilitiesRoot + '\FileAssociations',
        Extension,
        ClassName);
      RegWriteStringValue(HKA, ShellClassesRoot + Extension + '\OpenWithProgids', ClassName, '');
      RegWriteStringValue(HKA, ClassRoot, '', 'Plik ' + Uppercase(Copy(Extension, 2, Length(Extension) - 1)) + ' ({#KrotkaNazwa})');
      RegWriteStringValue(HKA, ClassRoot + '\DefaultIcon', '', Executable + ',0');
      RegWriteStringValue(HKA, ClassRoot + '\shell\open', '', '{#ShellOpenLabel}');
      Command := '"' + Executable + '" "%1"';
      RegWriteStringValue(HKA, ClassRoot + '\shell\open\command', '', Command);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    if (not Exec(
          ExpandConstant('{app}\AMC.exe'),
          '--sprawdz-pakiet-pelny-cicho',
          ExpandConstant('{app}'),
          SW_HIDE,
          ewWaitUntilTerminated,
          ResultCode)) or (ResultCode <> 0) then
    begin
      RaiseException(
        'Pełna kontrola integralności zainstalowanego pakietu nie powiodła się. ' +
        'Instalacja nie może zostać uznana za zakończoną.');
    end;
    if (not Exec(
          ExpandConstant('{app}\AMC.exe'),
          '--sprawdz-start-cicho',
          ExpandConstant('{app}'),
          SW_HIDE,
          ewWaitUntilTerminated,
          ResultCode)) or (ResultCode <> 0) then
    begin
      RaiseException(
        'Kontrolowany start interfejsu i silnika nie powiódł się. ' +
        'Program nie zostanie uruchomiony jako gotowa instalacja.');
    end;
    { Dopiero zweryfikowana instalacja zmienia wpisy TEGO profilu.
      Odznaczenie zadania jest dzięki temu prawdziwym wyłączeniem, nie tylko
      brakiem nowych wpisów. }
    LoadShellIntegrationSelection;
    RemoveShellIntegration;
    RegisterShellIntegration;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveShellIntegration;
end;
