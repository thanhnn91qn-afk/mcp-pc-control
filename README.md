# MCP PC Control

Mini-tool tách từ cách **điều khiển từ xa HospitalPC** (Tray: GDI chụp màn + `SendInput`):  
AI agent (Cursor, Claude Code, Codex, Antigravity…) điều khiển máy Windows **như người** qua MCP.

## Cài / gỡ — giống Agent

| HospitalPC Agent | MCP PC Control |
|------------------|----------------|
| `HPCSetupAgent.exe` | `McpPcSetup.exe` |
| Cài đặt / Cài lại · Gỡ cài đặt | Giống hệt wizard |
| `cai-dat.cmd` / `go-cai.cmd` | Có |
| `HPCAgent.exe /install` · `/uninstall` | `McpPcHost.exe /install` · `/uninstall` |
| Tray + task ONLOGON | Tray + watchdog ONLOGON |

```bat
cd mcp-pc-control
pack.cmd
:: Run as Admin:
out\Setup\McpPcSetup.exe
```

Hoặc `setup.cmd` (pack + mở wizard), `uninstall.cmd` (wizard gỡ).

## Kiến trúc

```
AI (Cursor/Claude/…)  --stdio MCP-->  node mcp/index.mjs
                                            |
                                     HTTP 127.0.0.1:17310 + token
                                            |
                                     McpPcHost.exe /tray   (phiên người dùng)
                                       ScreenCapture + InputInjector
```

## Tài liệu tích hợp MCP

Xem [docs/TICH-HOP-MCP.md](docs/TICH-HOP-MCP.md).

Sau cài, mẫu JSON nằm tại `%ProgramData%\McpPcControl\mcp.cursor.example.json`.
