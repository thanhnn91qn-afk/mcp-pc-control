using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace McpPcHost
{
    /// <summary>
    /// Giống vai trò HPCAgent: chạy nền, bảo đảm tray (/tray) luôn có trong phiên người dùng.
    /// Cài bằng Task Scheduler ONLOGON (setup.cmd) — service LocalSystem không bơm được input vào desktop.
    /// </summary>
    public static class Watchdog
    {
        public static void Run()
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            while (true)
            {
                try
                {
                    if (!IsTrayRunning())
                    {
                        ProcessStartInfo psi = new ProcessStartInfo();
                        psi.FileName = exe;
                        psi.Arguments = "/tray";
                        psi.UseShellExecute = false;
                        psi.WorkingDirectory = Path.GetDirectoryName(exe);
                        Process.Start(psi);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(Paths.DataDir, "watchdog.log"),
                            DateTime.Now.ToString("s") + " " + ex.Message + "\r\n");
                    }
                    catch { }
                }
                Thread.Sleep(15000);
            }
        }

        private static bool IsTrayRunning()
        {
            try
            {
                if (!File.Exists(Paths.StatusFile)) return false;
                string[] lines = File.ReadAllLines(Paths.StatusFile);
                int pid = 0;
                bool running = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith("running=1")) running = true;
                    if (line.StartsWith("pid=")) int.TryParse(line.Substring(4), out pid);
                }
                if (!running || pid <= 0) return false;
                Process.GetProcessById(pid);
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>Windows Service mỏng — chỉ gọi Watchdog (tuỳ chọn; setup mặc định dùng Scheduled Task).</summary>
    public class McpPcService : ServiceBase
    {
        private Thread _thread;

        public McpPcService()
        {
            ServiceName = "McpPcControl";
        }

        protected override void OnStart(string[] args)
        {
            _thread = new Thread(Watchdog.Run) { IsBackground = true, Name = "McpPc-Watchdog" };
            _thread.Start();
        }

        protected override void OnStop()
        {
            // Watchdog vòng while — tiến trình service dừng khi SCM kill
        }
    }
}
