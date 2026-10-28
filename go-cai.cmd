@echo off
rem Go cai MCP PC Control (giong tools\go-cai-agent.cmd)
setlocal
cd /d "%~dp0"
net session >nul 2>&1
if errorlevel 1 (
  echo [LOI] Can quyen Administrator.
  pause
  exit /b 1
)

if exist "McpPcSetup.exe" (
  start "" /wait "McpPcSetup.exe" /uninstall
  exit /b %ERRORLEVEL%
)

set DEST=%ProgramFiles%\McpPcControl
taskkill /IM McpPcHost.exe /F >nul 2>&1
if exist "%DEST%\McpPcHost.exe" (
  pushd "%DEST%"
  McpPcHost.exe /uninstall
  popd
)
if exist "%DEST%" rmdir /s /q "%DEST%" 2>nul
echo Da go cai MCP PC Control.
pause
exit /b 0
