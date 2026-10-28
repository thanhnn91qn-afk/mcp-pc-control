@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
if exist "%~dp0out\Setup\McpPcSetup.exe" (
  start "" "%~dp0out\Setup\McpPcSetup.exe" /uninstall
  exit /b 0
)
if exist "%~dp0out\Setup\go-cai.cmd" (
  call "%~dp0out\Setup\go-cai.cmd"
  exit /b %ERRORLEVEL%
)
echo Chay pack.cmd truoc, hoac go-cai.cmd trong out\Setup.
pause
exit /b 1
