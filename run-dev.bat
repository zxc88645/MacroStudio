@echo off
REM Fast development build and run for MacroStudio
cd /d "%~dp0"
echo Building and running MacroStudio in Debug mode...
dotnet run --project src\MacroStudio.Presentation\MacroStudio.Presentation.csproj -c Debug
pause
