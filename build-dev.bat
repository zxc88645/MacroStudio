@echo off
REM Fast development build for MacroNex
REM This is much faster than publish-MacroNex.bat as it skips optimizations
cd /d "%~dp0"
echo Building MacroNex in Debug mode...
dotnet build -c Debug
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: src\MacroNex.Presentation\bin\Debug\net10.0-windows\MacroNex.Presentation.exe
) else (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)
