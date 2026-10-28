using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace McpPcHost
{
    /// <summary>HTTP chỉ lắng nghe 127.0.0.1 — MCP Node gọi localhost kèm token.</summary>
    public sealed class LocalApiServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly string _token;
        private readonly int _port;
        private Thread _thread;
        private volatile bool _running;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };

        public LocalApiServer(int port, string token)
        {
            _port = port;
            _token = token ?? "";
            _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
        }

        public int Port { get { return _port; } }

        public void Start()
        {
            if (_running) return;
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "McpPc-Http" };
            _thread.Start();
        }

        public void Dispose()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }

        private void Loop()
        {
            while (_running)
            {
                HttpListenerContext ctx = null;
                try { ctx = _listener.GetContext(); }
                catch { if (!_running) break; continue; }
                try { Handle(ctx); }
                catch (Exception ex)
                {
                    try { WriteJson(ctx.Response, ex is ArgumentException ? 400 : 500, new { ok = false, error = ex.Message }); }
                    catch { }
                }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse res = ctx.Response;
            string path = (req.Url.AbsolutePath ?? "/").TrimEnd('/').ToLowerInvariant();
            if (path.Length == 0) path = "/";

            if (path == "/health")
            {
                WriteJson(res, 200, new { ok = true, service = "McpPcHost", version = "1.0.0" });
                return;
            }

            if (!Authorize(req))
            {
                WriteJson(res, 401, new { ok = false, error = "Sai hoặc thiếu token (header X-Mcp-Token)." });
                return;
            }

            string body = ReadBody(req);
            Dictionary<string, object> args = ParseArgs(body);

            if (path == "/screen/info" && req.HttpMethod == "GET")
            {
                WriteJson(res, 200, new { ok = true, data = ScreenCapture.ScreenInfo() });
                return;
            }

            if (path == "/screenshot" && (req.HttpMethod == "GET" || req.HttpMethod == "POST"))
            {
                int monitor = GetInt(args, "monitor", -1);
                int quality = GetInt(args, "quality", 55);
                int maxWidth = GetInt(args, "maxWidth", 1280);
                if (req.HttpMethod == "GET")
                {
                    int.TryParse(req.QueryString["monitor"], out monitor);
                    if (monitor == 0 && string.IsNullOrEmpty(req.QueryString["monitor"])) monitor = -1;
                    int.TryParse(req.QueryString["quality"], out quality);
                    if (quality == 0) quality = 55;
                    int.TryParse(req.QueryString["maxWidth"], out maxWidth);
                    if (maxWidth == 0) maxWidth = 1280;
                }
                byte[] jpeg = ScreenCapture.CaptureJpeg(monitor, quality, maxWidth);
                WriteJson(res, 200, new
                {
                    ok = true,
                    mimeType = "image/jpeg",
                    widthHint = maxWidth,
                    base64 = Convert.ToBase64String(jpeg)
                });
                TrayApp.NotifyAction("screenshot");
                return;
            }

            if (path == "/click" && req.HttpMethod == "POST")
            {
                int x = GetReqInt(args, "x");
                int y = GetReqInt(args, "y");
                string button = GetStr(args, "button", "left");
                int clicks = GetInt(args, "clicks", 1);
                InputInjector.Click(x, y, button, clicks);
                TrayApp.NotifyAction("click " + x + "," + y);
                WriteJson(res, 200, new { ok = true });
                return;
            }

            if (path == "/drag" && req.HttpMethod == "POST")
            {
                int x1 = GetReqInt(args, "x1");
                int y1 = GetReqInt(args, "y1");
                int x2 = GetReqInt(args, "x2");
                int y2 = GetReqInt(args, "y2");
                InputInjector.Drag(x1, y1, x2, y2, GetStr(args, "button", "left"));
                TrayApp.NotifyAction("drag " + x1 + "," + y1 + " → " + x2 + "," + y2);
                WriteJson(res, 200, new { ok = true });
                return;
            }

            if (path == "/move" && req.HttpMethod == "POST")
            {
                InputInjector.Move(GetReqInt(args, "x"), GetReqInt(args, "y"));
                WriteJson(res, 200, new { ok = true });
                return;
            }

            if (path == "/scroll" && req.HttpMethod == "POST")
            {
                InputInjector.Scroll(GetReqInt(args, "x"), GetReqInt(args, "y"), GetInt(args, "delta", -120));
                TrayApp.NotifyAction("scroll");
                WriteJson(res, 200, new { ok = true });
                return;
            }

            if (path == "/type" && req.HttpMethod == "POST")
            {
                string text = GetStr(args, "text", null);
                if (text == null) throw new ArgumentException("Thiếu tham số text.");
                InputInjector.TypeText(text);
                TrayApp.NotifyAction("type");
                WriteJson(res, 200, new { ok = true, length = text != null ? text.Length : 0 });
                return;
            }

            if (path == "/key" && req.HttpMethod == "POST")
            {
                string keys = GetStr(args, "keys", "");
                InputInjector.KeyCombo(keys);
                TrayApp.NotifyAction("key " + keys);
                WriteJson(res, 200, new { ok = true });
                return;
            }

            if (path == "/hold" && req.HttpMethod == "POST")
            {
                string keys = GetStr(args, "keys", "");
                int ms = Math.Max(0, Math.Min(10000, GetInt(args, "ms", 500)));
                InputInjector.KeyCombo(keys, ms);
                TrayApp.NotifyAction("hold " + keys + " " + ms + "ms");
                WriteJson(res, 200, new { ok = true, ms });
                return;
            }

            if (path == "/windows" && req.HttpMethod == "GET")
            {
                WriteJson(res, 200, new { ok = true, data = WindowList.List() });
                return;
            }

            if (path == "/clipboard" && req.HttpMethod == "POST")
            {
                string action = GetStr(args, "action", "get");
                if (action == "set")
                {
                    string text = GetStr(args, "text", null);
                    if (text == null) throw new ArgumentException("Thiếu tham số text.");
                    WindowList.SetClipboard(text);
                    TrayApp.NotifyAction("clipboard set");
                    WriteJson(res, 200, new { ok = true, length = text.Length });
                }
                else if (action == "get")
                {
                    TrayApp.NotifyAction("clipboard get");
                    WriteJson(res, 200, new { ok = true, text = WindowList.GetClipboard() });
                }
                else throw new ArgumentException("action không hợp lệ: '" + action + "' (get|set).");
                return;
            }

            if (path == "/wait" && req.HttpMethod == "POST")
            {
                int ms = Math.Max(0, Math.Min(60000, GetInt(args, "ms", 500)));
                Thread.Sleep(ms);
                WriteJson(res, 200, new { ok = true, ms });
                return;
            }

            WriteJson(res, 404, new { ok = false, error = "Không có API: " + path });
        }

        private bool Authorize(HttpListenerRequest req)
        {
            if (string.IsNullOrEmpty(_token)) return true;
            string h = req.Headers["X-Mcp-Token"];
            if (string.Equals(h, _token, StringComparison.Ordinal)) return true;
            string q = req.QueryString["token"];
            return string.Equals(q, _token, StringComparison.Ordinal);
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            if (!req.HasEntityBody) return "";
            using (StreamReader sr = new StreamReader(req.InputStream, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private Dictionary<string, object> ParseArgs(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            try
            {
                object o = _json.DeserializeObject(body);
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d != null) return new Dictionary<string, object>(d, StringComparer.OrdinalIgnoreCase);
            }
            catch { }
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return def;
            try { return Convert.ToInt32(d[key]); }
            catch { return def; }
        }

        private static int GetReqInt(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                throw new ArgumentException("Thiếu tham số số: " + key);
            try { return Convert.ToInt32(d[key]); }
            catch { throw new ArgumentException("Tham số '" + key + "' phải là số, nhận được: " + d[key]); }
        }

        private static string GetStr(Dictionary<string, object> d, string key, string def)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return def;
            return Convert.ToString(d[key]);
        }

        private void WriteJson(HttpListenerResponse res, int code, object obj)
        {
            byte[] buf = Encoding.UTF8.GetBytes(_json.Serialize(obj));
            res.StatusCode = code;
            res.ContentType = "application/json; charset=utf-8";
            res.ContentLength64 = buf.Length;
            res.OutputStream.Write(buf, 0, buf.Length);
            res.OutputStream.Close();
        }
    }
}
