using System;
using System.ServiceProcess;
using System.Windows.Forms;

namespace McpPcHost
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string mode = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "/tray";

            if (mode == "/install" || mode == "-install")
            {
                int code = InstallCommands.Install();
                Environment.ExitCode = code;
                if (code != 0)
                    Console.Error.WriteLine("Cài task/tray thất bại, mã " + code);
                else
                    Console.WriteLine("Đã cài McpPcControl (tray + watchdog khi đăng nhập).");
                return;
            }

            if (mode == "/uninstall" || mode == "-uninstall")
            {
                Environment.ExitCode = InstallCommands.Uninstall();
                Console.WriteLine("Đã gỡ task/tray McpPcControl.");
                return;
            }

            if (mode == "/service")
            {
                ServiceBase.Run(new McpPcService());
                return;
            }

            if (mode == "/watchdog")
            {
                Watchdog.Run();
                return;
            }

            // Mặc định: tray + HTTP API trong phiên người dùng
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            int port = Paths.LoadPort();
            string token = Paths.LoadOrCreateToken();
            Application.Run(new TrayApp(port, token));
        }
    }
}
