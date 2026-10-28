@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
echo Dong goi roi mo wizard Cai dat / Go cai (giong HPCSetupAgent)...
call pack.cmd
if errorlevel 1 exit /b 1
start "" "%~dp0out\Setup\McpPcSetup.exe"
exit /b 0
