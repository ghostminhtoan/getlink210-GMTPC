@echo off
setlocal
set "MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe"
set "PROJECT=%~dp0Comic-GMTPC.csproj"
set "VERIFY_SCRIPT=%~dp0tools\Verify-GitRestorePoint.ps1"

if exist "%VERIFY_SCRIPT%" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%VERIFY_SCRIPT%" -ProjectFile "Comic-GMTPC.csproj"
  if errorlevel 1 exit /b %errorlevel%
)

"%MSBUILD%" "%PROJECT%" /t:Restore /v:minimal /nologo
if errorlevel 1 exit /b %errorlevel%

"%MSBUILD%" "%PROJECT%" /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /p:AutoStampBuildInfo=true /p:AutoPublishRelease=true /v:minimal /nologo
if errorlevel 1 exit /b %errorlevel%

endlocal
