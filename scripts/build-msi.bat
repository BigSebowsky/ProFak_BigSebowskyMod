@echo off
setlocal

set "NO_PAUSE="
if /I "%~1"=="--no-pause" set "NO_PAUSE=1"

set "ROOT=%~dp0.."
set "WIX_BIN=C:\Program Files (x86)\WiX Toolset v3.14\bin"

if not exist "%WIX_BIN%\heat.exe" goto :missing_wix

call "%~dp0publish-release.bat" --no-pause
if errorlevel 1 goto :error

pushd "%ROOT%\Instalator"

if not exist "..\bin\Install" mkdir "..\bin\Install"
if exist "ProFak-pliki.wxs" del /q "ProFak-pliki.wxs"
if exist "ProFak.wixobj" del /q "ProFak.wixobj"
if exist "ProFak-pliki.wixobj" del /q "ProFak-pliki.wixobj"
if exist "..\bin\Install\ProFak_BigSebowskyMod.msi" del /q "..\bin\Install\ProFak_BigSebowskyMod.msi"

echo.
echo [1/3] Generating WiX fragment ...
"%WIX_BIN%\heat.exe" dir "..\bin\Publish" -sreg -srd -sfrag -gg -template fragment -cg Pliki -var var.Zrodlo -dr KATALOGPROGRAMU -out "ProFak-pliki.wxs"
if errorlevel 1 goto :error_popd

echo.
echo [2/3] Compiling WiX sources ...
"%WIX_BIN%\candle.exe" "ProFak.wxs" "ProFak-pliki.wxs" -arch x64 -dZrodlo=..\bin\Publish\
if errorlevel 1 goto :error_popd

echo.
echo [3/3] Linking MSI ...
"%WIX_BIN%\light.exe" "ProFak.wixobj" "ProFak-pliki.wixobj" -ext WixUIExtension -cultures:pl-PL -out "..\bin\Install\ProFak_BigSebowskyMod.msi"
if errorlevel 1 goto :error_popd

echo.
echo MSI ready:
echo %ROOT%bin\Install\ProFak_BigSebowskyMod.msi
set "EXITCODE=0"
goto :end_popd

:missing_wix
echo.
echo WiX Toolset 3.14 not found:
echo %WIX_BIN%
echo Install WiX first, then run this script again.
set "EXITCODE=1"
goto :end

:error_popd
set "EXITCODE=1"
popd
goto :error

:end_popd
popd
goto :end

:error
echo.
echo MSI build failed.

:end
if not defined NO_PAUSE pause
exit /b %EXITCODE%
