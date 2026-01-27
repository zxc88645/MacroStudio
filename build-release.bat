@echo off
REM Release build (faster than publish, but still optimized)
REM This is faster than publish-MacroNex.bat but produces optimized output
cd /d "%~dp0"
echo Building MacroNex in Release mode...
dotnet build -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: src\MacroNex.Presentation\bin\Release\net10.0-windows\MacroNex.Presentation.exe
) else (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)
