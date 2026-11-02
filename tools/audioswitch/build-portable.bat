@echo off
rem 免安裝版：自帶 .NET 執行環境的單一 exe，打包到 Output\
rem 注意：不要開 EnableCompressionInSingleFile / IncludeNativeLibrariesForSelfExtract，
rem       自解壓到 Temp 的行為會被 PC-cillin 當可疑檔案封鎖（2026-11-03 實測）。
cd /d "%~dp0"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -o "%~dp0publish-portable"
if errorlevel 1 (
  echo BUILD FAILED
  pause
  exit /b 1
)
if not exist "%~dp0Output" mkdir "%~dp0Output"
for /f %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd"') do set D=%%d
powershell -NoProfile -Command "Compress-Archive -Path '%~dp0publish-portable\AudioSwitch.exe','%~dp0使用說明.txt' -DestinationPath '%~dp0Output\AudioSwitch-portable-x64-%D%.zip' -Force"
echo DONE: Output\AudioSwitch-portable-x64-%D%.zip
timeout /t 4 >nul
