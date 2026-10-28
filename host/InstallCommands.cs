using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace McpPcHost
{
    /// <summary>
    /// Cài / gỡ giống HPCAgent: Scheduled Task tray + watchdog, autostart khi đăng nhập.
    /// (Không dùng LocalSystem Service để bơm input — desktop chỉ nhận từ tiến trình phiên người dùng.)
    /// </summary>
    public static class InstallCommands
    {
        public const string TaskTray = "McpPcControlTray";
        public const string TaskWatchdog = "McpPcControlWatchdog";

        public static int Install()
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string dir = Path.GetDirectoryName(exe);
            Directory.CreateDirectory(Paths.DataDir);
            if (!File.Exists(Paths.ConfigFile))
                File.WriteAllText(Paths.ConfigFile, "port=17310\r\n");
            Paths.LoadOrCreateToken();

            // Xóa task cũ rồi tạo lại
            Run("schtasks", "/Delete /TN \"" + TaskTray + "\" /F", 15000);
            Run("schtasks", "/Delete /TN \"" + TaskWatchdog + "\" /F", 15000);

            int c1 = Run("schtasks",
                "/Create /TN \"" + TaskTray + "\" /SC ONLOGON /RL HIGHEST /F /TR \"" + exe + " /tray\"",
                30000);
            if (c1 != 0) return c1;

            Run("schtasks",
                "/Create /TN \"" + TaskWatchdog + "\" /SC ONLOGON /RL LIMITED /F /TR \"" + exe + " /watchdog\"",
                30000);

            // HKCU Run dự phòng (máy không cho schtasks)
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (k != null) k.SetValue("McpPcControl", "\"" + exe + "\" /tray");
                }
            }
            catch { }

            // Chạy tray ngay nếu có desktop
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, "/tray");
                psi.WorkingDirectory = dir;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }

            try { Run("schtasks", "/Run /TN \"" + TaskTray + "\"", 10000); } catch { }
            return 0;
        }

        public static int Uninstall()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("McpPcHost"))
                {
                    try { if (p.Id != Process.GetCurrentProcess().Id) p.Kill(); } catch { }
                }
            }
            catch { }
            Thread.Sleep(500);

            Run("schtasks", "/Delete /TN \"" + TaskTray + "\" /F", 15000);
            Run("schtasks", "/Delete /TN \"" + TaskWatchdog + "\" /F", 15000);

            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k != null) k.DeleteValue("McpPcControl", false);
                }
            }
            catch { }

            try { File.WriteAllText(Paths.StatusFile, "running=0\r\n"); } catch { }
            return 0;
        }

        private static int Run(string file, string args, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        return -2;
                    }
                    return p.ExitCode;
                }
            }
            catch
            {
                return -1;
            }
        }
    }
}
