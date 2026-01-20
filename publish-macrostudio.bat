@echo off
REM Build & publish MacroStudio as self-contained single-file EXE (win-x64)
cd /d "%~dp0"
dotnet publish src\MacroStudio.Presentation\MacroStudio.Presentation.csproj ^
  -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:PublishReadyToRun=true ^
  /p:DebugType=None /p:DebugSymbols=false ^
  -o artifacts\publish\win-x64

echo.
echo Publish completed. Output:
echo   artifacts\publish\win-x64\MacroStudio.Presentation.exe
pause

