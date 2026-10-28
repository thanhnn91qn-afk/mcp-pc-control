#!/usr/bin/env node
/**
 * MCP PC Control — stdio server cho Cursor / Claude Code / Codex / Antigravity.
 * Gọi host tray local http://127.0.0.1:<port> (SendInput + GDI như HospitalPC Tray).
 */
import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";
import { homedir } from "node:os";
import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const DATA =
  process.env.MCP_PC_DATA ||
  join(process.env.PROGRAMDATA || "C:\\ProgramData", "McpPcControl");

function loadConfig() {
  let port = Number(process.env.MCP_PC_PORT || 0) || 17310;
  let token = process.env.MCP_PC_TOKEN || "";
  const cfg = join(DATA, "config.ini");
  if (existsSync(cfg)) {
    for (const line of readFileSync(cfg, "utf8").split(/\r?\n/)) {
      const m = /^port\s*=\s*(\d+)/i.exec(line);
      if (m) port = Number(m[1]);
    }
  }
  const tf = join(DATA, "token.txt");
  if (!token && existsSync(tf)) token = readFileSync(tf, "utf8").trim();
  return { port, token, base: `http://127.0.0.1:${port}` };
}

async function api(method, path, body) {
  const { port, token, base } = loadConfig();
  const url = base + path;
  const headers = { "Content-Type": "application/json" };
  if (token) headers["X-Mcp-Token"] = token;
  const res = await fetch(url, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    throw new Error(`Host trả về không phải JSON (HTTP ${res.status}): ${text.slice(0, 200)}`);
  }
  if (!res.ok || data.ok === false) {
    throw new Error(data.error || `HTTP ${res.status}`);
  }
  return data;
}

/** Đọc bề ngang/cao từ header JPEG (marker SOFn). */
function jpegSize(buf) {
  let i = 2;
  while (i + 9 < buf.length) {
    if (buf[i] !== 0xff) return null;
    const m = buf[i + 1];
    if (m >= 0xc0 && m <= 0xcf && m !== 0xc4 && m !== 0xc8 && m !== 0xcc)
      return { height: buf.readUInt16BE(i + 5), width: buf.readUInt16BE(i + 7) };
    i += 2 + buf.readUInt16BE(i + 2);
  }
  return null;
}

const INSTRUCTIONS =
  "Điều khiển máy Windows như người. Quy trình: desktop_windows (app nào đang focus) → desktop_screenshot " +
  "(đọc công thức đổi tọa độ trong kết quả) → desktop_click/type/key/drag → desktop_screenshot để kiểm tra. " +
  "Ưu tiên phím tắt (desktop_key) hơn click khi có thể; văn bản dài thì desktop_clipboard set + ctrl+v. " +
  "Tham số thiếu/sai trả lỗi rõ ràng, không click mặc định. Không thao tác khi chưa chắc cửa sổ đang focus đúng.";

const TOOLS = [
  {
    name: "desktop_screen_info",
    description:
      "Thông tin màn hình ảo, từng monitor (tọa độ tuyệt đối), vị trí con trỏ. Gọi trước khi click theo tọa độ.",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "desktop_screenshot",
    description:
      "Chụp màn hình JPEG để nhìn UI. Ảnh có thể bị thu nhỏ (maxWidth): kết quả kèm công thức đổi tọa độ ảnh → tọa độ màn hình tuyệt đối dùng cho click/move/drag. Đặt maxWidth >= bề ngang màn hình để tọa độ 1:1.",
    inputSchema: {
      type: "object",
      properties: {
        monitor: {
          type: "number",
          description: "Chỉ số monitor 0..N; bỏ qua hoặc -1 = toàn màn hình ảo",
        },
        quality: { type: "number", description: "JPEG 10–95, mặc định 55" },
        maxWidth: {
          type: "number",
          description: "Thu nhỏ bề ngang (mặc định 1280) để tiết kiệm token",
        },
      },
      additionalProperties: false,
    },
  },
  {
    name: "desktop_click",
    description: "Click chuột tại tọa độ màn hình tuyệt đối (pixel).",
    inputSchema: {
      type: "object",
      properties: {
        x: { type: "number" },
        y: { type: "number" },
        button: { type: "string", enum: ["left", "right", "middle"] },
        clicks: { type: "number", description: "1 = click, 2 = double-click" },
      },
      required: ["x", "y"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_drag",
    description:
      "Kéo thả chuột từ (x1,y1) tới (x2,y2): chọn văn bản, di chuyển cửa sổ, kéo file.",
    inputSchema: {
      type: "object",
      properties: {
        x1: { type: "number" },
        y1: { type: "number" },
        x2: { type: "number" },
        y2: { type: "number" },
        button: { type: "string", enum: ["left", "right", "middle"] },
      },
      required: ["x1", "y1", "x2", "y2"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_move",
    description: "Di chuyển con trỏ tới (x,y).",
    inputSchema: {
      type: "object",
      properties: { x: { type: "number" }, y: { type: "number" } },
      required: ["x", "y"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_scroll",
    description: "Cuộn tại (x,y). delta âm = xuống, dương = lên (bội 120).",
    inputSchema: {
      type: "object",
      properties: {
        x: { type: "number" },
        y: { type: "number" },
        delta: { type: "number" },
      },
      required: ["x", "y"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_type",
    description: "Gõ văn bản Unicode tại vị trí focus hiện tại (click ô nhập trước).",
    inputSchema: {
      type: "object",
      properties: { text: { type: "string" } },
      required: ["text"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_key",
    description:
      'Tổ hợp phím: "enter", "ctrl+s", "alt+f4", "win+r", "ctrl+shift+esc", ...',
    inputSchema: {
      type: "object",
      properties: { keys: { type: "string" } },
      required: ["keys"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_hold_key",
    description:
      'Giữ tổ hợp phím trong ms mili-giây (tối đa 10000) rồi thả, vd {"keys":"shift","ms":1500} hoặc {"keys":"down","ms":2000}.',
    inputSchema: {
      type: "object",
      properties: { keys: { type: "string" }, ms: { type: "number" } },
      required: ["keys", "ms"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_clipboard",
    description:
      'Đọc/ghi clipboard (văn bản). {"action":"get"} đọc; {"action":"set","text":"..."} ghi rồi dùng desktop_key ctrl+v để dán — nhanh hơn desktop_type với văn bản dài.',
    inputSchema: {
      type: "object",
      properties: {
        action: { type: "string", enum: ["get", "set"] },
        text: { type: "string", description: "Bắt buộc khi action=set" },
      },
      required: ["action"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_windows",
    description:
      "Liệt kê cửa sổ đang hiện: title, process, pid, vị trí/kích thước, minimized, foreground. Dùng để biết app nào đang focus và click vào giữa cửa sổ mong muốn.",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "desktop_wait",
    description: "Chờ milliseconds (tối đa 60000) rồi tiếp tục.",
    inputSchema: {
      type: "object",
      properties: { ms: { type: "number" } },
      required: ["ms"],
      additionalProperties: false,
    },
  },
  {
    name: "desktop_health",
    description: "Kiểm tra host tray MCP PC Control có đang chạy không.",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
];

const server = new Server(
  { name: "mcp-pc-control", version: "1.0.0" },
  { capabilities: { tools: {} }, instructions: INSTRUCTIONS }
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({ tools: TOOLS }));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const name = req.params.name;
  const a = req.params.arguments || {};
  try {
    if (name === "desktop_health") {
      const { base, token } = loadConfig();
      const headers = {};
      if (token) headers["X-Mcp-Token"] = token;
      const res = await fetch(base + "/health", { headers });
      const data = await res.json();
      return {
        content: [{ type: "text", text: JSON.stringify({ ...data, base }, null, 2) }],
      };
    }
    if (name === "desktop_screen_info") {
      const data = await api("GET", "/screen/info");
      return {
        content: [{ type: "text", text: JSON.stringify(data.data, null, 2) }],
      };
    }
    if (name === "desktop_screenshot") {
      const monitor = a.monitor ?? -1;
      const data = await api("POST", "/screenshot", {
        monitor,
        quality: a.quality ?? 55,
        maxWidth: a.maxWidth ?? 1280,
      });
      const size = jpegSize(Buffer.from(data.base64, "base64"));
      let hint = "Ảnh màn hình (JPEG).";
      if (size) {
        const info = (await api("GET", "/screen/info")).data;
        const r = (monitor >= 0 && info.monitors[monitor]) || info.virtualScreen;
        const f = r.width / size.width;
        const round = (n) => Math.round(n * 100) / 100;
        hint =
          `Ảnh ${size.width}x${size.height} px ↔ vùng màn hình ${r.width}x${r.height} tại (${r.x},${r.y}). ` +
          (f === 1
            ? `Tọa độ ảnh = tọa độ màn hình + (${r.x},${r.y}).`
            : `Tọa độ màn hình = (${r.x} + px*${round(f)}, ${r.y} + py*${round(f)}).`) +
          ` Con trỏ hiện ở (${info.cursor.X},${info.cursor.Y}).`;
      }
      return {
        content: [
          { type: "text", text: hint },
          {
            type: "image",
            data: data.base64,
            mimeType: data.mimeType || "image/jpeg",
          },
        ],
      };
    }
    if (name === "desktop_click") {
      await api("POST", "/click", {
        x: a.x,
        y: a.y,
        button: a.button || "left",
        clicks: a.clicks || 1,
      });
      return { content: [{ type: "text", text: `Đã click (${a.x},${a.y})` }] };
    }
    if (name === "desktop_drag") {
      await api("POST", "/drag", {
        x1: a.x1,
        y1: a.y1,
        x2: a.x2,
        y2: a.y2,
        button: a.button || "left",
      });
      return {
        content: [{ type: "text", text: `Đã kéo (${a.x1},${a.y1}) → (${a.x2},${a.y2})` }],
      };
    }
    if (name === "desktop_move") {
      await api("POST", "/move", { x: a.x, y: a.y });
      return { content: [{ type: "text", text: `Đã move (${a.x},${a.y})` }] };
    }
    if (name === "desktop_scroll") {
      await api("POST", "/scroll", {
        x: a.x,
        y: a.y,
        delta: a.delta ?? -120,
      });
      return { content: [{ type: "text", text: "Đã scroll" }] };
    }
    if (name === "desktop_type") {
      await api("POST", "/type", { text: a.text });
      return { content: [{ type: "text", text: `Đã gõ ${String(a.text).length} ký tự` }] };
    }
    if (name === "desktop_key") {
      await api("POST", "/key", { keys: a.keys });
      return { content: [{ type: "text", text: `Đã nhấn ${a.keys}` }] };
    }
    if (name === "desktop_hold_key") {
      const data = await api("POST", "/hold", { keys: a.keys, ms: a.ms });
      return { content: [{ type: "text", text: `Đã giữ ${a.keys} ${data.ms} ms` }] };
    }
    if (name === "desktop_clipboard") {
      const data = await api("POST", "/clipboard", { action: a.action, text: a.text });
      const text =
        a.action === "set" ? `Đã ghi clipboard (${data.length} ký tự)` : data.text;
      return { content: [{ type: "text", text }] };
    }
    if (name === "desktop_windows") {
      const data = await api("GET", "/windows");
      return { content: [{ type: "text", text: JSON.stringify(data.data, null, 1) }] };
    }
    if (name === "desktop_wait") {
      await api("POST", "/wait", { ms: a.ms });
      return { content: [{ type: "text", text: `Đã chờ ${a.ms} ms` }] };
    }
    return {
      content: [{ type: "text", text: `Không biết tool: ${name}` }],
      isError: true,
    };
  } catch (e) {
    const hint = /fetch failed|ECONNREFUSED|không phải JSON/.test(String(e.message || e) + String(e.cause || ""))
      ? " Hãy chạy setup.cmd hoặc khởi động McpPcHost.exe (tray). Kiểm tra icon khay hệ thống."
      : "";
    return {
      content: [{ type: "text", text: String(e.message || e) + hint }],
      isError: true,
    };
  }
});

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
