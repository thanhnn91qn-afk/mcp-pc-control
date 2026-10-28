using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace McpPcHost
{
    public sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly LocalApiServer _api;
        private static TrayApp _instance;
        private DateTime _lastBalloon = DateTime.MinValue;

        public TrayApp(int port, string token)
        {
            _instance = this;
            _api = new LocalApiServer(port, token);
            _api.Start();

            _icon = new NotifyIcon();
            _icon.Text = "MCP PC Control · :" + port;
            _icon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            _icon.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Trạng thái API : " + port, null, delegate
            {
                MessageBox.Show(
                    "MCP PC Control đang chạy.\n\nAPI: http://127.0.0.1:" + port + "\nToken: " +
                    (string.IsNullOrEmpty(token) ? "(trống)" : token.Substring(0, Math.Min(8, token.Length)) + "…") +
                    "\n\nAI agent (Cursor / Claude / Codex) điều khiển máy qua MCP → API này.",
                    "MCP PC Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Items.Add("Mở thư mục cấu hình", null, delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", Paths.DataDir); } catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Thoát", null, delegate { Exit(); });
            _icon.ContextMenuStrip = menu;

            _icon.BalloonTipTitle = "MCP PC Control";
            _icon.BalloonTipText = "Sẵn sàng — AI có thể điều khiển máy qua MCP (cổng " + port + ").";
            _icon.ShowBalloonTip(4000);

            File.WriteAllText(Paths.StatusFile,
                "running=1\r\nport=" + port + "\r\npid=" + System.Diagnostics.Process.GetCurrentProcess().Id + "\r\n");
        }

        public static void NotifyAction(string action)
        {
            TrayApp app = _instance;
            if (app == null) return;
            try
            {
                if ((DateTime.UtcNow - app._lastBalloon).TotalSeconds < 3) return;
                app._lastBalloon = DateTime.UtcNow;
                app._icon.BalloonTipTitle = "MCP đang điều khiển";
                app._icon.BalloonTipText = action ?? "thao tác";
                app._icon.ShowBalloonTip(1500);
            }
            catch { }
        }

        private void Exit()
        {
            try { File.WriteAllText(Paths.StatusFile, "running=0\r\n"); } catch { }
            _api.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _api.Dispose(); } catch { }
                try { _icon.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    public static class Paths
    {
        public static string DataDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "McpPcControl");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string TokenFile { get { return Path.Combine(DataDir, "token.txt"); } }
        public static string StatusFile { get { return Path.Combine(DataDir, "status.txt"); } }
        public static string ConfigFile { get { return Path.Combine(DataDir, "config.ini"); } }

        public static string LoadOrCreateToken()
        {
            if (File.Exists(TokenFile))
            {
                string t = File.ReadAllText(TokenFile).Trim();
                if (!string.IsNullOrEmpty(t)) return t;
            }
            string token = Guid.NewGuid().ToString("N");
            File.WriteAllText(TokenFile, token);
            return token;
        }

        public static int LoadPort()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return 17310;
                foreach (string line in File.ReadAllLines(ConfigFile))
                {
                    if (line.StartsWith("port=", StringComparison.OrdinalIgnoreCase))
                    {
                        int p;
                        if (int.TryParse(line.Substring(5).Trim(), out p) && p > 0) return p;
                    }
                }
            }
            catch { }
            return 17310;
        }
    }
}
