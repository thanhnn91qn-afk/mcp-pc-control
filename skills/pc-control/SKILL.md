---
name: pc-control
description: Use when the task needs to see or operate the user's Windows desktop through the pc-control MCP server — taking screenshots, clicking, dragging, typing (including Unicode/Vietnamese), pressing shortcuts, reading or writing the clipboard, or listing open windows. Not for browsers that have their own automation tools.
---

# pc-control: operate a Windows PC like a person

Drives the real desktop through the `pc-control` MCP server (tools named `desktop_*`). Everything it does is visible on the user's screen, so work deliberately.

## Prerequisites

- MCP server `pc-control` is registered and connected. If a tool fails to connect, call `desktop_health`. If that fails too, tell the user the host tray (`McpPcHost.exe`, purple cursor icon in the system tray) is not running; they can start it with `schtasks /Run /TN McpPcControlTray`.
- Windows only. The host listens on `127.0.0.1` and uses a token; never print or share the token.

## Workflow

1. `desktop_windows` — see which window is in the foreground.
2. `desktop_screenshot` — read the result text: it gives the formula to turn image pixels into absolute screen coordinates (`screen = origin + px * factor`). Set `maxWidth` to the screen width for 1:1 coordinates.
3. Act: `desktop_click`, `desktop_drag`, `desktop_scroll`, `desktop_type`, `desktop_key`, `desktop_hold_key`.
4. `desktop_screenshot` again to confirm the result before the next step.

## Rules that prevent mistakes

- **Confirm focus before typing.** Keystrokes go to whatever window is in the foreground. If the chat app itself is focused, your text lands in the user's chat box. Click inside the target window, then take a screenshot to confirm the cursor is where you expect before typing.
- Do one small step, then verify. Windows can steal focus between calls (notifications, the chat app redrawing).
- Ask the user not to type or move the mouse while you work; their input mixes with yours.
- Prefer keyboard shortcuts (`desktop_key`: `win+r`, `ctrl+s`, `alt+f4`) over hunting for buttons.
- For long text use `desktop_clipboard` (`action: set`) then `desktop_key ctrl+v` instead of `desktop_type`.
- Ask before destructive or outward-facing actions: deleting files, sending messages, submitting forms, purchases, closing unsaved work.
- Do not enter passwords or payment details; let the user do that.
- Invalid or missing parameters return an error (they never fall back to clicking at 0,0). Read the error and fix the call.

## Tool reference

| Tool | Use |
|------|-----|
| `desktop_health` | Is the host alive? |
| `desktop_screen_info` | Monitors, absolute coordinates, cursor position |
| `desktop_screenshot` | JPEG of the screen + coordinate formula |
| `desktop_windows` | Visible windows, size/position, foreground flag |
| `desktop_click` | `x`, `y`, `button` (left/right/middle), `clicks` (2 = double-click) |
| `desktop_move` / `desktop_scroll` | Move the cursor; scroll (positive delta = up, multiples of 120) |
| `desktop_drag` | Drag from `(x1,y1)` to `(x2,y2)`: select text, move windows or files |
| `desktop_type` | Type Unicode text at the focused control |
| `desktop_key` / `desktop_hold_key` | Shortcuts like `ctrl+shift+esc`; hold a key up to 10000 ms |
| `desktop_clipboard` | `get` / `set` clipboard text |
| `desktop_wait` | Wait N ms (keep it under 55000) |

## Example: write a note in Notepad

1. `desktop_key` `win+r`, `desktop_type` `notepad`, `desktop_key` `enter`.
2. `desktop_screenshot`; click inside the Notepad text area.
3. `desktop_screenshot` to confirm the caret is in Notepad, then `desktop_type` your text.
4. `desktop_key` `ctrl+s` only if the user asked to save.

## Troubleshooting

- **Clicks land in the wrong place:** you skipped the coordinate formula in the screenshot result.
- **Input ignored in an admin window:** the host runs elevated by default (installed task uses highest privileges); if it was started manually it may not be. Reinstall or restart the tray task.
- **Accented characters garbled:** use a host build from this repo; older builds decoded request bodies as Latin-1.
