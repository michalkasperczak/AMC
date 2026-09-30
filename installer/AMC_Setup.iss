; AMC_Setup.iss -- instalator Accessible Multimedia Controller (Inno Setup 6/7).
;
; Budowanie: najpierw dotnet publish do katalogu, potem
;   ISCC.exe /DZrodlo=<katalog z publish> /DWersja=0.1.0-alpha.355 AMC_Setup.iss
; Wynik: AMC_Setup.exe w OutputDir.
;
; Wzorowane na EdSharp_Setup.iss, ale z trzema istotnymi roznicami:
;   1. AMC stoi na .NET 8, a EdSharp na .NET Framework 4.8 (ten jest w Windows
;      od zawsze). Instalator jednak NICZEGO NIE POBIERA i nie instaluje zadnego
;      srodowiska: build.ps1 publikuje AMC jako wersje SAMOWYSTARCZALNA
;      (--self-contained true, PublishSingleFile=true), wiec srodowisko
;      uruchomieniowe jest W PLIKU programu. Instalacja musi dzialac bez
;      dostepu do sieci i bez strony "You must install .NET Desktop Runtime".
;      NIE DODAWAC tu z powrotem pobierania runtime - byloby zbedne, wymagaloby
;      sieci w trakcie instalacji i dawaloby okna bledu, ktorych czytnik ekranu
;      prawie nie tlumaczy.
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
; ustawien cicha instalacja STAWALA, proszÄ…c o zamkniecie dzialajacego AMC -
; a w trybie cichym nie ma komu tej prosby pokazac.
;   CloseApplications=force  - dzialajace AMC jest zamykane samo,
;   RestartApplications=no   - i NIE jest wznawiane przez instalator, bo robi to
;                              sam program (zeby wrocil do tej samej sesji).
;
; ZMIERZONE 17.09.2026 - DLACZEGO NIE MA TU `AppMutex`:
; `AppMutex` NIE pomaga cichej instalacji, tylko ja ZABIJA. Inno sprawdza muteks
; ZANIM zdazy zadzialac `CloseApplications`, i gdy program jest w pamieci,
; pokazuje okno "aplikacja jest aktualnie uruchomiona" z wyborem OK/Anuluj.
; Przy `/VERYSILENT /SUPPRESSMSGBOXES` domyslna odpowiedzia na takie okno jest
; ANULUJ, wiec instalator konczy `Got EAbort exception` i kodem 1, nie tknawszy
; ani jednego pliku. W logu widac wtedy tylko "Deinitializing Setup" - zadnego
; bledu kopiowania, wiec wyglada to jak awaria bez przyczyny.
;
; Zamykaniem dzialajacego programu zajmuje sie `CloseApplications=force` przez
; Restart Managera i to wystarcza. WARUNEK: instalator musi byc uruchomiony
; w SESJI GRAFICZNEJ uzytkownika. Odpalony z sesji uslugowej (SSH, sesja 0)
; Restart Manager zwraca "Session Mismatch" i wymiana pliku pada na
; "DeleteFile; kod 5. Odmowa dostepu" - to nie wada instalatora, tylko brak
; pulpitu; zdalna instalacja idzie przez `schtasks /IT /RU <uzytkownik>`.
CloseApplications=force
RestartApplications=no

[Languages]
Name: "polski"; MessagesFile: "compiler:Languages\Polish.isl"

[InstallDelete]
; Starsze kopie z numerem wersji w nazwie. Bez tego katalog programu rosl
; o kolejne 160 MB przy KAZDEJ aktualizacji, a stare pliki zostawaly na dysku
; jako martwy balast, ktorego nikt nie uruchamia.
Type: files; Name: "{app}\AccessibleMediaController-*.exe"

[Files]
; GLOWNY PLIK PROGRAMU. Build nazywa go z numerem wersji
; (AccessibleMediaController-0.1.0-alpha.370.exe), ale skrot na pulpicie,
; menu Start, wpis App Paths i aktualizacja w tle szukaja STALEJ nazwy
; AccessibleMediaController.exe. Dlatego kopiujemy go pod nazwa docelowa.
;
; BLAD ZMIERZONY 15.09.2026: bez tego wiersza instalator kopiowal plik pod
; nazwa z numerem wersji, a AccessibleMediaController.exe zostawal ten stary.
; Instalacja konczyla sie komunikatem "Installation process succeeded",
; katalog programu mial obok siebie wersje 367, 368 i 370, a skrot z pulpitu
; caly czas uruchamial 367. Uzytkownik slyszal stara wersje po udanej
; aktualizacji i nie mial z czego wyczytac, dlaczego.
Source: "AccessibleMediaController-{#Wersja}.exe"; DestDir: "{app}"; \
    DestName: "AccessibleMediaController.exe"; Flags: ignoreversion
; Pozostala zawartosc katalogu publish. Wykluczenia: pliki diagnostyczne
; kompilatora, pakiety dodatku NVDA (instaluje sie osobno) oraz glowny plik
; programu, ktory zostal skopiowany wyzej pod stala nazwa.
Source: "*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; \
    Excludes: "*.pdb,*.xml.orig,*.nvda-addon,AMC_Setup.iss,AccessibleMediaController-*.exe"

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

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\AccessibleMediaController.exe"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\AccessibleMediaController.exe"; Flags: uninsdeletekey
