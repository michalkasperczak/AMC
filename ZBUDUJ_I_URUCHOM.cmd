@echo off
setlocal EnableExtensions
chcp 65001 >nul

set "AMC_ROOT=%~dp0"
set "AMC_LOG=%AMC_ROOT%build-log.txt"
set "AMC_OUTPUT=%AMC_ROOT%publish\win-x64"
set "AMC_EXE=%AMC_OUTPUT%\AccessibleMediaController.exe"
set "AMC_DOTNET="

cd /d "%AMC_ROOT%"
title Dostepny kontroler multimedialny - budowanie

> "%AMC_LOG%" echo Dostepny kontroler multimedialny - raport budowania
>> "%AMC_LOG%" echo Data: %date% %time%
>> "%AMC_LOG%" echo Katalog: %AMC_ROOT%
>> "%AMC_LOG%" echo.

call :BUILD >> "%AMC_LOG%" 2>&1
if errorlevel 1 goto BUILD_FAILED

if not exist "%AMC_EXE%" (
    >> "%AMC_LOG%" echo BLAD: po budowaniu nie znaleziono pliku "%AMC_EXE%".
    goto BUILD_FAILED
)

start "" "%AMC_EXE%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('Program został zbudowany i uruchomiony. Gotowa wersja znajduje się w folderze publish\win-x64.', 'Dostępny kontroler multimedialny') | Out-Null" >nul 2>&1
exit /b 0

:BUILD_FAILED
start "" notepad.exe "%AMC_LOG%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "Add-Type -AssemblyName PresentationFramework; [System.Windows.MessageBox]::Show('Nie udało się zbudować programu. Szczegóły zostały otwarte w Notatniku w pliku build-log.txt.', 'Błąd budowania') | Out-Null" >nul 2>&1
exit /b 1

:BUILD
echo Sprawdzanie zestawu .NET 8 SDK...
call :LOCATE_DOTNET
if errorlevel 1 goto INSTALL_DOTNET
call :HAS_DOTNET8_SDK
if not errorlevel 1 goto DOTNET_READY

:INSTALL_DOTNET

echo Nie znaleziono .NET 8 SDK. Rozpoczynam instalację przez Menedżera pakietów Windows.
where winget.exe >nul 2>&1
if errorlevel 1 (
    echo BLAD: nie znaleziono programu winget. Zainstaluj aktualizacje systemu Windows lub pakiet Instalator aplikacji ze sklepu Microsoft Store, a następnie uruchom ten plik ponownie.
    exit /b 10
)

winget.exe install --id Microsoft.DotNet.SDK.8 --exact --source winget --accept-package-agreements --accept-source-agreements
if errorlevel 1 (
    echo BLAD: instalacja .NET 8 SDK nie powiodła się albo została anulowana.
    exit /b 11
)

call :LOCATE_DOTNET
if errorlevel 1 (
    echo BLAD: instalator zakończył pracę, ale nadal nie można znaleźć programu dotnet.exe. Uruchom ponownie komputer i spróbuj jeszcze raz.
    exit /b 12
)
call :HAS_DOTNET8_SDK
if errorlevel 1 (
    echo BLAD: instalator zakończył pracę, ale nadal nie można znaleźć .NET 8 SDK. Uruchom ponownie komputer i spróbuj jeszcze raz.
    exit /b 13
)

:DOTNET_READY
echo Używany program: %AMC_DOTNET%
"%AMC_DOTNET%" --version
if errorlevel 1 exit /b 20

echo.
echo Przywracanie składników projektu...
"%AMC_DOTNET%" restore AccessibleMediaController.sln
if errorlevel 1 exit /b 21

echo.
echo Kompilowanie projektu...
"%AMC_DOTNET%" build AccessibleMediaController.sln --configuration Release --no-restore
if errorlevel 1 exit /b 22

echo.
echo Uruchamianie testów kontrolnych...
"%AMC_DOTNET%" run --project tests\AccessibleMediaController.Core.SmokeTests --configuration Release --no-build
if errorlevel 1 exit /b 23

echo.
echo Tworzenie samowystarczalnej wersji dla Windows x64...
"%AMC_DOTNET%" publish src\AccessibleMediaController.Windows\AccessibleMediaController.Windows.csproj --configuration Release --runtime win-x64 --self-contained true --output "%AMC_OUTPUT%" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 exit /b 24

echo.
echo Budowanie zakończone pomyślnie.
exit /b 0

:LOCATE_DOTNET
set "AMC_DOTNET="
for /f "delims=" %%D in ('where dotnet.exe 2^>nul') do if not defined AMC_DOTNET set "AMC_DOTNET=%%D"
if defined AMC_DOTNET exit /b 0

if exist "%ProgramFiles%\dotnet\dotnet.exe" (
    set "AMC_DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
    exit /b 0
)

if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" (
    set "AMC_DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"
    exit /b 0
)

exit /b 1

:HAS_DOTNET8_SDK
if not defined AMC_DOTNET exit /b 1
"%AMC_DOTNET%" --list-sdks 2>nul | findstr.exe /r /b /c:"8\." >nul
exit /b %errorlevel%
