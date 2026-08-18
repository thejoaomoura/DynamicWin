@echo off
rem ---------------------------------------------------------------------------
rem  Registers Halo in the current user's Start Menu, which is the folder
rem  Windows Search indexes. After this, typing "Halo" in Start finds the app.
rem
rem  Usage:
rem    register-start-menu.bat                        detect Halo.App.exe
rem    register-start-menu.bat "C:\...\Halo.App.exe"  use this path
rem    register-start-menu.bat /remove                take the shortcut back out
rem
rem  No admin needed. Writes nothing to the Windows registry.
rem ---------------------------------------------------------------------------

setlocal

set "LNK=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Halo.lnk"

if /i "%~1"=="/remove" goto remove
if /i "%~1"=="-remove" goto remove
if /i "%~1"=="/r" goto remove

rem --- find the executable: argument, dist, Release build, or next to this file ---
set "EXE="
if not "%~1"=="" set "EXE=%~f1"
if not defined EXE if exist "%~dp0..\dist\Halo\Halo.App.exe" set "EXE=%~dp0..\dist\Halo\Halo.App.exe"
if not defined EXE if exist "%~dp0..\src\Halo.App\bin\Release\net9.0-windows10.0.19041.0\win-x64\Halo.App.exe" set "EXE=%~dp0..\src\Halo.App\bin\Release\net9.0-windows10.0.19041.0\win-x64\Halo.App.exe"
if not defined EXE if exist "%~dp0Halo.App.exe" set "EXE=%~dp0Halo.App.exe"

if not defined EXE goto notfound
if not exist "%EXE%" goto notfound

rem --- resolve to an absolute path, without the ".." ---
for %%I in ("%EXE%") do set "EXE=%%~fI"
for %%I in ("%EXE%") do set "DIR=%%~dpI"

echo Executable : %EXE%
echo Shortcut   : %LNK%
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = (New-Object -ComObject WScript.Shell).CreateShortcut('%LNK%'); $s.TargetPath = '%EXE%'; $s.WorkingDirectory = '%DIR%'; $s.IconLocation = '%EXE%,0'; $s.Description = 'Halo'; $s.Save()"

if errorlevel 1 goto failed
if not exist "%LNK%" goto failed

echo Done. Halo is in the Start Menu.
echo Windows Search usually picks it up within a few seconds.
echo To undo: register-start-menu.bat /remove
exit /b 0

:remove
if not exist "%LNK%" (
    echo No shortcut at: %LNK%
    exit /b 0
)
del "%LNK%"
if exist "%LNK%" (
    echo Could not delete: %LNK%
    exit /b 1
)
echo Removed: %LNK%
exit /b 0

:notfound
echo ERROR: Halo.App.exe not found.
echo.
echo Looked in:
echo   %%~dp0..\dist\Halo\
echo   %%~dp0..\src\Halo.App\bin\Release\net9.0-windows10.0.19041.0\win-x64\
echo   next to this .bat
echo.
echo Pass the path directly:
echo   register-start-menu.bat "C:\path\to\Halo.App.exe"
exit /b 1

:failed
echo ERROR: could not create the shortcut.
exit /b 1
