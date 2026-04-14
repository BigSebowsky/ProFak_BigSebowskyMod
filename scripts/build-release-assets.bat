@echo off
setlocal

set "NO_PAUSE="
if /I "%~1"=="--no-pause" set "NO_PAUSE=1"

set "ROOT=%~dp0.."

call "%~dp0build-msi.bat" --no-pause
if errorlevel 1 goto :error

pushd "%ROOT%"

if exist "bin\Install\ProFak_BigSebowskyMod.zip" del /q "bin\Install\ProFak_BigSebowskyMod.zip"

echo.
echo [4/4] Creating ZIP package ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path 'bin\\Publish\\*' -DestinationPath 'bin\\Install\\ProFak_BigSebowskyMod.zip' -Force"
if errorlevel 1 goto :error_popd

echo.
echo Release assets ready:
echo %CD%\bin\Install\ProFak_BigSebowskyMod.msi
echo %CD%\bin\Install\ProFak_BigSebowskyMod.zip
set "EXITCODE=0"
goto :end_popd

:error_popd
set "EXITCODE=1"
popd
goto :error

:error
echo.
echo Release asset build failed.

:end_popd
popd
if not defined NO_PAUSE pause
exit /b %EXITCODE%
