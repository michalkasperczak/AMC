@echo off
rem Uruchomienie AMC-wx-Lite na Windows.
rem
rem Skrypt NIE instaluje niczego globalnie i NIE dotyka pelnego AMC.
rem Szuka prywatnego runtime Michala (CPython 3.14.7 + wxPython 4.3.1), a
rem jesli go nie ma - lokalnego srodowiska .venv obok tego pliku.

setlocal

set "HERE=%~dp0"
set "RUNTIME=%AMC_WX_LITE_PYTHON%"

if not defined RUNTIME (
  if exist "%HERE%runtime\python.exe" set "RUNTIME=%HERE%runtime\python.exe"
)
if not defined RUNTIME (
  if exist "%HERE%.venv\Scripts\python.exe" set "RUNTIME=%HERE%.venv\Scripts\python.exe"
)
if not defined RUNTIME (
  echo Nie znalazlem srodowiska Pythona.
  echo Wskaz je zmienna AMC_WX_LITE_PYTHON albo utworz .venv obok tego pliku.
  echo Szczegoly: JAK_TESTOWAC.md
  exit /b 2
)

rem Silnik: domyslnie obok programu, w folderze host\.
if not defined AMC_LITE_HOST (
  if exist "%HERE%host\amc_lite_host.exe" set "AMC_LITE_HOST=%HERE%host\amc_lite_host.exe"
)

set "PYTHONPATH=%HERE%app;%PYTHONPATH%"
rem --sprawdz nie otwiera okna, tylko wypisuje stan srodowiska.
"%RUNTIME%" -m amc_wx_lite %*
exit /b %ERRORLEVEL%
