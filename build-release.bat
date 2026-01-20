@echo off
REM Release build (faster than publish, but still optimized)
REM This is faster than publish-macrostudio.bat but produces optimized output
cd /d "%~dp0"
echo Building MacroStudio in Release mode...
dotnet build -c Release
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: src\MacroStudio.Presentation\bin\Release\net10.0-windows\MacroStudio.Presentation.exe
) else (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)
