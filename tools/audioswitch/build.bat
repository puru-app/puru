@echo off
taskkill /IM AudioSwitch.exe /F >nul 2>&1
cd /d "%~dp0"
dotnet publish -c Release -o "%~dp0publish"
if errorlevel 1 (
  echo.
  echo BUILD FAILED - copy the red error text and send it to Claude.
  pause
  exit /b 1
)
start "" "%~dp0publish\AudioSwitch.exe"
echo.
echo DONE - AudioSwitch is running in the system tray.
timeout /t 4 >nul
