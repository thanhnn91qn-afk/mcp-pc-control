# Tích hợp MCP PC Control

Host tray (`McpPcHost`) chụp màn + SendInput (cùng hướng HospitalPC Tray).  
MCP stdio (`mcp/index.mjs`) để Cursor / Claude Code / Codex / Antigravity gọi tool.

## Cài đặt (giống Agent)

1. Chạy `pack.cmd` trong thư mục `mcp-pc-control`.
2. Mở `out\Setup\McpPcSetup.exe` **Run as Administrator**.
3. Chọn **Cài đặt / Cài lại** hoặc **Gỡ cài đặt** (cùng kiểu `HPCSetupAgent`).

Hoặc script:

- `cai-dat.cmd` → mở wizard cài  
- `go-cai.cmd` / `McpPcSetup.exe /uninstall` → wizard gỡ  

Sau khi cài:

| Mục | Đường dẫn |
|-----|-----------|
| Chương trình | `%ProgramFiles%\McpPcControl\` |
| Token / mẫu MCP | `%ProgramData%\McpPcControl\` |
| API local | `http://127.0.0.1:17310` |
| Task | `McpPcControlTray`, `McpPcControlWatchdog` |

Cần **Node.js 18+** trên PATH.

## Tool MCP

| Tool | Việc |
|------|------|
| `desktop_health` | Host còn sống? |
| `desktop_screen_info` | Monitor + tọa độ |
| `desktop_screenshot` | Ảnh JPEG (nhìn UI) |
| `desktop_click` | Click / double-click |
| `desktop_move` / `desktop_scroll` | Chuột |
| `desktop_drag` | Kéo thả (chọn chữ, kéo cửa sổ/file) |
| `desktop_hold_key` | Giữ phím N ms (tối đa 10000) |
| `desktop_clipboard` | Đọc/ghi clipboard văn bản |
| `desktop_windows` | Liệt kê cửa sổ + cửa sổ đang focus |
| `desktop_type` / `desktop_key` | Gõ chữ / tổ hợp phím |
| `desktop_wait` | Chờ ms |

Quy trình AI: `screen_info` → `screenshot` → `click`/`type` → `screenshot` kiểm tra.

## Cursor

File thường: `%USERPROFILE%\.cursor\mcp.json` (hoặc MCP trong Settings).

Sao chép từ `%ProgramData%\McpPcControl\mcp.cursor.example.json`:

```json
{
  "mcpServers": {
    "pc-control": {
      "command": "node",
      "args": ["C:/Program Files/McpPcControl/mcp/index.mjs"],
      "env": {
        "MCP_PC_TOKEN": "<token trong token.txt>",
        "MCP_PC_PORT": "17310"
      }
    }
  }
}
```

Bật MCP trong Cursor → restart → thử `desktop_health`.

## Claude Code

`claude mcp add` hoặc file cấu hình MCP của Claude Code — dùng cùng `command`/`args`/`env` như trên.  
Mẫu: `mcp.claude.example.json`.

## Codex (OpenAI)

Thêm server MCP stdio trong cấu hình Codex (mcpServers), trỏ `node` + `index.mjs` + env token.

## Antigravity / agent khác

Bất kỳ client MCP stdio nào:

```text
command: node
args: ["…/McpPcControl/mcp/index.mjs"]
env: MCP_PC_TOKEN, MCP_PC_PORT
```

## Bảo mật

- API **chỉ** `127.0.0.1` + header `X-Mcp-Token`.
- Icon khay báo khi AI thao tác.
- Cửa sổ **Run as Admin** có thể chặn SendInput từ tiến trình thường — chạy app thường hoặc nâng host (không khuyến nghị).

## Gỡ

`McpPcSetup.exe` → **Gỡ cài đặt**, hoặc `go-cai.cmd`.
