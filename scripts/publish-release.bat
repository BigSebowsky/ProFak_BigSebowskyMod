@echo off
setlocal

set "NO_PAUSE="
if /I "%~1"=="--no-pause" set "NO_PAUSE=1"

set "ROOT=%~dp0.."
pushd "%ROOT%"

set "DOTNET_CLI_HOME=%CD%\.dotnet-home"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"

echo.
echo [1/1] Publishing release build to bin\Publish ...
if exist "bin\Publish" rd /s /q "bin\Publish"
dotnet publish "ProFak.csproj" -r win-x64 -c release --self-contained -o "bin\Publish"
if errorlevel 1 goto :error

echo.
echo Release publish ready:
echo %CD%\bin\Publish
goto :success

:error
echo.
echo Publish failed.
set "EXITCODE=1"
goto :end

:success
set "EXITCODE=0"

:end
popd
if not defined NO_PAUSE pause
exit /b %EXITCODE%
