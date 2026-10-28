@echo off
rem Cai dat MCP PC Control (giong tools\cai-dat-agent.cmd)
rem Nen dung McpPcSetup.exe (wizard). Script nay goi cung wizard o che do cai.
setlocal
cd /d "%~dp0"
net session >nul 2>&1
if errorlevel 1 (
  echo [LOI] Can chay voi quyen Administrator.
  pause
  exit /b 1
)

if not exist "McpPcSetup.exe" (
  if exist "setup\bin\Release\net48\McpPcSetup.exe" (
    echo Chay pack.cmd truoc, hoac dung ban trong out\Setup.
  ) else (
    echo [LOI] Thieu McpPcSetup.exe — chay pack.cmd.
  )
  pause
  exit /b 1
)

start "" /wait "McpPcSetup.exe"
exit /b %ERRORLEVEL%
