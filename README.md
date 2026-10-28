# MCP PC Control

**English** · [Tiếng Việt](#tiếng-việt)

An MCP server plus a small Windows tray host that lets AI agents (Claude Code, Cursor, Codex, Antigravity, …) control a Windows PC **like a human**: take screenshots, move/click/drag the mouse, type text, press shortcuts, use the clipboard and list windows.

## English

### Architecture

```
AI agent  --stdio MCP-->  node mcp/index.mjs
                               |
                        HTTP 127.0.0.1:17310 + token (X-Mcp-Token)
                               |
                        McpPcHost.exe /tray   (runs in the user session)
                          GDI screen capture + SendInput
```

- The API listens on `127.0.0.1` only and requires the token stored in `%ProgramData%\McpPcControl\token.txt`.
- A tray icon shows when an AI is acting.

### Requirements

- Windows 10/11, **Node.js 18+** on `PATH`
- To build: .NET SDK (the projects target `net48`)

### Build and install

```bat
git clone https://github.com/thanhnn91qn-afk/mcp-pc-control
cd mcp-pc-control
pack.cmd
```

Then run `out\Setup\McpPcSetup.exe` **as Administrator** and choose *Install* (or *Uninstall*). `cai-dat.cmd` / `go-cai.cmd` start the same wizard.

Installed to:

| What | Where |
|------|-------|
| Program | `%ProgramFiles%\McpPcControl\` |
| Token / config / MCP examples | `%ProgramData%\McpPcControl\` |
| Local API | `http://127.0.0.1:17310` |
| Scheduled tasks | `McpPcControlTray`, `McpPcControlWatchdog` (at logon) |

> The tray task runs with the highest privileges (`/RL HIGHEST`) so it can send input to elevated windows.

### Register the MCP server

Claude Code:

```bash
claude mcp add pc-control --scope user \
  -e MCP_PC_TOKEN=<contents of token.txt> -e MCP_PC_PORT=17310 \
  -- node "C:/Program Files/McpPcControl/mcp/index.mjs"
```

Cursor / other clients — see `%ProgramData%\McpPcControl\mcp.cursor.example.json`:

```json
{
  "mcpServers": {
    "pc-control": {
      "command": "node",
      "args": ["C:/Program Files/McpPcControl/mcp/index.mjs"],
      "env": { "MCP_PC_TOKEN": "<token>", "MCP_PC_PORT": "17310" }
    }
  }
}
```

### Tools

| Tool | Purpose |
|------|---------|
| `desktop_health` | Is the host alive? |
| `desktop_screen_info` | Monitors, absolute coordinates, cursor position |
| `desktop_screenshot` | JPEG screenshot + image→screen coordinate formula |
| `desktop_windows` | Visible windows and the focused one |
| `desktop_click` / `desktop_move` / `desktop_scroll` | Mouse |
| `desktop_drag` | Drag and drop (select text, move windows/files) |
| `desktop_type` / `desktop_key` / `desktop_hold_key` | Type Unicode text, shortcuts (`ctrl+s`, `win+r`), hold a key |
| `desktop_clipboard` | Read / write clipboard text |
| `desktop_wait` | Wait N ms (max 60000) |

Suggested loop: `desktop_windows` → `desktop_screenshot` → click/type/key → `desktop_screenshot` to verify. Invalid or missing parameters return an error instead of acting.

### Security

Anything holding the token can control this PC. Keep `token.txt` private, install only on machines you own, and uninstall with `McpPcSetup.exe /uninstall` (or `go-cai.cmd`) when not needed.

---

## Tiếng Việt

MCP server cùng một host khay hệ thống (tray) cho Windows, để AI agent (Claude Code, Cursor, Codex, Antigravity, …) điều khiển máy **như người**: chụp màn hình, di chuột/click/kéo thả, gõ chữ, nhấn phím tắt, dùng clipboard và liệt kê cửa sổ.

### Kiến trúc

```
AI agent  --stdio MCP-->  node mcp/index.mjs
                               |
                        HTTP 127.0.0.1:17310 + token (X-Mcp-Token)
                               |
                        McpPcHost.exe /tray   (chạy trong phiên người dùng)
                          Chụp màn hình GDI + SendInput
```

- API chỉ lắng nghe `127.0.0.1` và bắt buộc có token trong `%ProgramData%\McpPcControl\token.txt`.
- Icon khay báo khi AI đang thao tác.

### Yêu cầu

- Windows 10/11, **Node.js 18+** trong `PATH`
- Để build: .NET SDK (dự án nhắm `net48`)

### Build và cài đặt

```bat
git clone https://github.com/thanhnn91qn-afk/mcp-pc-control
cd mcp-pc-control
pack.cmd
```

Sau đó chạy `out\Setup\McpPcSetup.exe` bằng **quyền Administrator**, chọn *Cài đặt* (hoặc *Gỡ cài đặt*). `cai-dat.cmd` / `go-cai.cmd` mở cùng wizard này.

Vị trí sau khi cài:

| Mục | Đường dẫn |
|-----|-----------|
| Chương trình | `%ProgramFiles%\McpPcControl\` |
| Token / cấu hình / mẫu MCP | `%ProgramData%\McpPcControl\` |
| API local | `http://127.0.0.1:17310` |
| Task đã đăng ký | `McpPcControlTray`, `McpPcControlWatchdog` (khi đăng nhập) |

> Task tray chạy với quyền cao nhất (`/RL HIGHEST`) để gửi được phím/chuột vào cửa sổ chạy Admin.

### Đăng ký MCP

Claude Code:

```bash
claude mcp add pc-control --scope user \
  -e MCP_PC_TOKEN=<nội dung token.txt> -e MCP_PC_PORT=17310 \
  -- node "C:/Program Files/McpPcControl/mcp/index.mjs"
```

Cursor / client khác — xem `%ProgramData%\McpPcControl\mcp.cursor.example.json`:

```json
{
  "mcpServers": {
    "pc-control": {
      "command": "node",
      "args": ["C:/Program Files/McpPcControl/mcp/index.mjs"],
      "env": { "MCP_PC_TOKEN": "<token>", "MCP_PC_PORT": "17310" }
    }
  }
}
```

### Tool

| Tool | Việc |
|------|------|
| `desktop_health` | Host còn sống? |
| `desktop_screen_info` | Monitor, tọa độ tuyệt đối, vị trí con trỏ |
| `desktop_screenshot` | Ảnh JPEG + công thức đổi tọa độ ảnh → màn hình |
| `desktop_windows` | Cửa sổ đang hiện và cửa sổ đang focus |
| `desktop_click` / `desktop_move` / `desktop_scroll` | Chuột |
| `desktop_drag` | Kéo thả (chọn chữ, kéo cửa sổ/file) |
| `desktop_type` / `desktop_key` / `desktop_hold_key` | Gõ chữ Unicode, phím tắt (`ctrl+s`, `win+r`), giữ phím |
| `desktop_clipboard` | Đọc / ghi clipboard (văn bản) |
| `desktop_wait` | Chờ N ms (tối đa 60000) |

Quy trình gợi ý: `desktop_windows` → `desktop_screenshot` → click/type/key → `desktop_screenshot` kiểm tra. Tham số thiếu hoặc sai sẽ trả lỗi, không tự thao tác.

### Bảo mật

Ai có token đều điều khiển được máy này. Giữ kín `token.txt`, chỉ cài trên máy của bạn, và gỡ bằng `McpPcSetup.exe /uninstall` (hoặc `go-cai.cmd`) khi không dùng.

Chi tiết tích hợp: [docs/TICH-HOP-MCP.md](docs/TICH-HOP-MCP.md).
