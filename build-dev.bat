@echo off
REM Fast development build for MacroStudio
REM This is much faster than publish-macrostudio.bat as it skips optimizations
cd /d "%~dp0"
echo Building MacroStudio in Debug mode...
dotnet build -c Debug
if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: src\MacroStudio.Presentation\bin\Debug\net10.0-windows\MacroStudio.Presentation.exe
) else (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)
