@echo off
REM Fast development build and run for MacroNex
cd /d "%~dp0"
echo Building and running MacroNex in Debug mode...
dotnet run --project src\MacroNex.Presentation\MacroNex.Presentation.csproj -c Debug
pause
