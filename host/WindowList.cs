using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace McpPcHost
{
    /// <summary>Liệt kê cửa sổ top-level đang hiện + đọc/ghi clipboard (cần luồng STA).</summary>
    public static class WindowList
    {
        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        public static List<Dictionary<string, object>> List()
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            IntPtr fg = GetForegroundWindow();
            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h)) return true;
                int len = GetWindowTextLength(h);
                if (len == 0) return true;
                StringBuilder sb = new StringBuilder(len + 1);
                GetWindowText(h, sb, sb.Capacity);
                RECT r;
                GetWindowRect(h, out r);
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                string proc = "";
                try { proc = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { }
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["title"] = sb.ToString();
                d["process"] = proc;
                d["pid"] = (int)pid;
                d["x"] = r.Left; d["y"] = r.Top;
                d["width"] = r.Right - r.Left; d["height"] = r.Bottom - r.Top;
                d["minimized"] = IsIconic(h);
                d["foreground"] = h == fg;
                list.Add(d);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static string GetClipboard()
        {
            string result = "";
            RunSta(delegate { result = Clipboard.ContainsText() ? Clipboard.GetText() : ""; });
            return result;
        }

        public static void SetClipboard(string text)
        {
            RunSta(delegate { if (string.IsNullOrEmpty(text)) Clipboard.Clear(); else Clipboard.SetText(text); });
        }

        private static void RunSta(ThreadStart work)
        {
            Exception err = null;
            Thread t = new Thread(delegate () { try { work(); } catch (Exception ex) { err = ex; } });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (err != null) throw err;
        }
    }
}
