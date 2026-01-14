@echo off
echo ==========================================
echo Starting Daily Uptime Widget (Direct EXE)...
echo ==========================================

REM Build first to ensure it's fresh
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo Build failed.
    pause
    exit /b
)

echo Launching EXE...
start "" "bin\Debug\net8.0-windows\TimmerDaily.exe"

