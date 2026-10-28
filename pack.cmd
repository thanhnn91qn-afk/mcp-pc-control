@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo === Build host + setup ===
dotnet build host\McpPcHost.csproj -c Release -v q
if errorlevel 1 exit /b 1
dotnet build setup\McpPcSetup.csproj -c Release -v q
if errorlevel 1 exit /b 1

echo === npm install ===
call npm install --silent

set "OUT=%~dp0out\Setup"
if exist "%OUT%" rd /s /q "%OUT%"
mkdir "%OUT%"
mkdir "%OUT%\mcp"

echo === Dong goi out\Setup ===
copy /Y "host\bin\Release\net48\McpPcHost.exe" "%OUT%\" >nul
if exist "host\bin\Release\net48\McpPcHost.exe.config" copy /Y "host\bin\Release\net48\McpPcHost.exe.config" "%OUT%\" >nul
copy /Y "setup\bin\Release\net48\McpPcSetup.exe" "%OUT%\" >nul
xcopy /E /I /Y /Q "mcp\*" "%OUT%\mcp\" >nul
copy /Y "package.json" "%OUT%\" >nul
if exist "node_modules" xcopy /E /I /Y /Q "node_modules" "%OUT%\node_modules\" >nul
copy /Y "cai-dat.cmd" "%OUT%\" >nul
copy /Y "go-cai.cmd" "%OUT%\" >nul
if exist "docs\TICH-HOP-MCP.md" copy /Y "docs\TICH-HOP-MCP.md" "%OUT%\HUONG-DAN-MCP.md" >nul
if exist "README.md" copy /Y "README.md" "%OUT%\" >nul

echo.
echo OK: %OUT%
echo   Mo McpPcSetup.exe (wizard Cai/Go, giong HPCSetupAgent)
echo   hoac cai-dat.cmd / go-cai.cmd
exit /b 0
