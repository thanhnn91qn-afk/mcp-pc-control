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
if errorlevel 1 exit /b 1
echo OK. Tiep: pack.cmd  roi  out\Setup\McpPcSetup.exe
exit /b 0
