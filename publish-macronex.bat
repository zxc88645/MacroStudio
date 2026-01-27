@echo off
setlocal

REM ========================
REM Build configuration
REM ========================
set CONFIG=Release
set RID=win-x64
set PROJECT=src\MacroNex.Presentation\MacroNex.Presentation.csproj
set OUTPUT=artifacts\publish\%RID%

cd /d "%~dp0"

dotnet publish "%PROJECT%" ^
  -c %CONFIG% ^
  -r %RID% ^
  --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:EnableCompressionInSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:PublishReadyToRun=true ^
  /p:DebugType=None ^
  /p:DebugSymbols=false ^
  -o "%OUTPUT%"

if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo.
echo Publish completed. Output:
echo   %OUTPUT%\MacroNex.Presentation.exe
pause
