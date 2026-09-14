; AMC_Setup.iss -- instalator Accessible Multimedia Controller (Inno Setup 6/7).
;
; Budowanie: najpierw dotnet publish do katalogu, potem
;   ISCC.exe /DZrodlo=<katalog z publish> /DWersja=0.1.0-alpha.355 AMC_Setup.iss
; Wynik: AMC_Setup.exe w OutputDir.
;
; Wzorowane na EdSharp_Setup.iss, ale z trzema istotnymi roznicami:
;   1. AMC wymaga srodowiska .NET 8 Desktop (EdSharp stoi na .NET Framework 4.8,
;      ktory jest w Windows od zawsze). Instalator sam sprawdza i dociaga runtime,
;      bo inaczej program po instalacji NIE WSTANIE, a uzytkownik dostanie
;      systemowe okno bledu, ktorego czytnik ekranu prawie nie tlumaczy.
;   2. Bez ngen - to narzedzie .NET Framework, dla .NET 8 nie istnieje.
;   3. Instalacja dla BIEZACEGO UZYTKOWNIKA (lowest), wiec aktualizacja w tle
;      nie potrzebuje podniesienia uprawnien. Przy PrivilegesRequired=admin
;      kazda cicha aktualizacja wywolywalaby okno kontroli konta uzytkownika,
;      czyli przestalaby byc cicha.

#ifndef Wersja
  #define Wersja "0.0.0"
#endif
#ifndef Zrodlo
  #define Zrodlo "C:\amc_publish"
#endif
#ifndef Wyjscie
  ; Domyslnie wynik ladzie tam, gdzie Michal odbiera paczki. Bez tego
  ; instalator zostawal w C:\amc_setup i wygladal, jakby sie nie zbudowal.
  #define Wyjscie "D:\Projekty Codex\Hermes"
#endif

[Setup]
AppId={{8F3A6C21-4E7B-4D59-9C08-A3C0919E7B21}
AppName=Accessible Multimedia Controller
AppVersion={#Wersja}
AppVerName=Accessible Multimedia Controller {#Wersja}
VersionInfoVersion=0.1.0
AppPublisher=Michal Kasperczak
AppPublisherURL=https://github.com/michalkasperczak/AMC
AppSupportURL=https://github.com/michalkasperczak/AMC/issues
AppUpdatesURL=https://github.com/michalkasperczak/AMC/releases
DefaultDirName={autopf}\AMC
DefaultGroupName=Accessible Multimedia Controller
UninstallDisplayName=Accessible Multimedia Controller
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
Compression=lzma2/max
SolidCompression=yes
OutputBaseFilename=AMC-Setup-{#Wersja}
OutputDir={#Wyjscie}
SourceDir={#Zrodlo}
; Instalacja per-uzytkownik: patrz uwaga 3 w naglowku.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes
DisableStartupPrompt=yes
DisableWelcomePage=yes
DisableReadyPage=no
Uninstallable=yes
SetupLogging=yes
WizardStyle=classic

; TRYB CICHY (zlecenie Kasperczaka 14.09.2026: "Cicha instalacja, pobieranie itp.").
; Inno Setup obsluguje /SILENT i /VERYSILENT samo z siebie, ale bez ponizszych
; ustawien cicha instalacja STAWALA, prosząc o zamkniecie dzialajacego AMC -
; a w trybie cichym nie ma komu tej prosby pokazac.
;   CloseApplications=force  - dzialajace AMC jest zamykane samo,
;   RestartApplications=no   - i NIE jest wznawiane przez instalator, bo robi to
;                              sam program (zeby wrocil do tej samej sesji),
;   AppMutex                 - po tym instalator poznaje, ze program zniknal
;                              z pamieci. Nazwa MUSI zgadzac sie z App.xaml.cs.
CloseApplications=force
RestartApplications=no
AppMutex=Local\AccessibleMultimediaController.SingleInstance

[Languages]
Name: "polski"; MessagesFile: "compiler:Languages\Polish.isl"

[Files]
; Cala zawartosc katalogu publish. Wykluczenia: pliki diagnostyczne kompilatora
; i pakiety dodatku NVDA, ktore instaluje sie osobno.
Source: "*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "*.pdb,*.xml.orig,*.nvda-addon,AMC_Setup.iss"

[Dirs]
Name: "{localappdata}\AccessibleMediaController"
Name: "{localappdata}\AccessibleMediaController\logs"
Name: "{userappdata}\AccessibleMediaController"

[Icons]
Name: "{group}\Accessible Multimedia Controller"; Filename: "{app}\AccessibleMediaController.exe"; WorkingDir: "{app}"
Name: "{group}\Odinstaluj Accessible Multimedia Controller"; Filename: "{uninstallexe}"
; Skrot na pulpicie BEZ globalnego skrotu klawiszowego - na klawiaturze polskiej
; prawy Alt jest widziany jako Ctrl+Alt, wiec skrot z Ctrl+Alt zjadalby polska
; litere w calym systemie. AMC jest jednoegzemplarzowy, wiec uruchomienie
; z pulpitu przywoluje dzialajaca kopie.
Name: "{autodesktop}\Accessible Multimedia Controller"; Filename: "{app}\AccessibleMediaController.exe"; WorkingDir: "{app}"; Comment: "Uruchom lub przywolaj AMC"

[Run]
; Strona koncowa ma JEDNO pole wyboru, nie wiecej: dla osoby niewidomej kazde
; dodatkowe pole to realny koszt przy KAZDEJ aktualizacji, a wersje testowe
; wychodza czesto (to samo ustalenie co w EdSharpie).
Filename: "{app}\AccessibleMediaController.exe"; Description: "Uruchom AMC teraz"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\AccessibleMediaController.exe"
Type: dirifempty; Name: "{app}"

[Code]
const
  // Adres instalatora srodowiska .NET 8 Desktop. Kanal "lts" nie zadziala:
  // .NET 8 jest LTS, ale adres z numerem wersji jest jednoznaczny i nie zmieni
  // sie pod nami przy nastepnym wydaniu Microsoftu.
  UrlRuntime = 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe';

var
  BrakRuntime: Boolean;

// Sprawdza, czy jest zainstalowane srodowisko .NET 8 Desktop.
// Szukamy KATALOGU wersji, nie wpisu w rejestrze: instalacje przenosne
// i rownolegle wersje nie zawsze zostawiaja slad w rejestrze, a katalog
// shared\Microsoft.WindowsDesktop.App jest tym, czego program faktycznie szuka.
// Sprawdzamy DWIE lokalizacje, bo runtime moze stac systemowo w Program Files
// albo lokalnie w profilu uzytkownika (instalacja skryptem dotnet-install).
function MaRuntime8W(sBaza: string): Boolean;
var
  szukaj: TFindRec;
begin
  result := false;
  if not DirExists(sBaza) then
    exit;
  if FindFirst(AddBackslash(sBaza) + '8.*', szukaj) then
  try
    repeat
      if (szukaj.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      begin
        result := true;
        exit;
      end;
    until not FindNext(szukaj);
  finally
    FindClose(szukaj);
  end;
end;

function MaRuntime8(): Boolean;
begin
  { UWAGA: sprawdzamy WYLACZNIE miejsca, w ktorych Windows SAM szuka
    srodowiska uruchomieniowego. Katalog .dotnet w profilu uzytkownika wygladal
    na dobre miejsce i byl tu wczesniej sprawdzany, ale program go NIE
    widzi - .NET zaglada tam tylko wtedy, gdy ustawiona jest zmienna
    DOTNET_ROOT. Skutek byl taki, ze instalator uznawal srodowisko za
    obecne, nic nie dociagal, a AMC po instalacji pokazywal komunikat
    "You must install .NET Desktop Runtime" zamiast sie uruchomic. }
  result := MaRuntime8W(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App'))
         or MaRuntime8W(ExpandConstant('{commonpf32}\dotnet\shared\Microsoft.WindowsDesktop.App'));
end;

function InitializeSetup(): Boolean;
begin
  BrakRuntime := not MaRuntime8();
  result := true;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  sciezka: string;
  kod: Integer;
begin
  if (CurStep <> ssPostInstall) or (not BrakRuntime) then
    exit;

  // Runtime dociagamy PO skopiowaniu plikow, a brak runtime NIE przerywa
  // instalacji: pliki programu maja zostac na dysku, zeby uzytkownik mogl
  // doinstalowac srodowisko sam, gdyby pobieranie sie nie udalo.
  sciezka := ExpandConstant('{tmp}\windowsdesktop-runtime-8.exe');
  try
    DownloadTemporaryFile(UrlRuntime, 'windowsdesktop-runtime-8.exe', '', nil);
  except
    // Blad pobierania obsluguje sprawdzenie FileExists ponizej.
  end;
  if FileExists(sciezka) then
  begin
    Exec(sciezka, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, kod);
    if kod <> 0 then
      MsgBox('Nie udalo sie zainstalowac srodowiska .NET 8 Desktop (kod ' + IntToStr(kod) + ').' + #13#10 +
             'AMC jest zainstalowany, ale nie uruchomi sie bez tego srodowiska.' + #13#10 +
             'Pobierz je ze strony dotnet.microsoft.com i uruchom instalacje ponownie.',
             mbError, MB_OK);
  end
  else
    MsgBox('Nie udalo sie pobrac srodowiska .NET 8 Desktop.' + #13#10 +
           'AMC jest zainstalowany, ale nie uruchomi sie bez tego srodowiska.' + #13#10 +
           'Pobierz je ze strony dotnet.microsoft.com.',
           mbError, MB_OK);
end;

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\AccessibleMediaController.exe"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\AccessibleMediaController.exe"; Flags: uninsdeletekey
