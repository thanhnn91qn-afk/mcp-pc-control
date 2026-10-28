using System;
using System.IO;
using System.Windows.Forms;

namespace McpPcSetup
{
    /// <summary>Bộ cài .exe (Admin): Cài đặt / Gỡ cài — cùng mô hình HPCSetupAgent.</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool forceUninstall = false;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i].Trim().ToLowerInvariant();
                    if (a == "/uninstall" || a == "-uninstall" || a == "uninstall" || a == "/go-cai")
                        forceUninstall = true;
                }
            }

            string payloadDir = AppDomain.CurrentDomain.BaseDirectory;
            Application.Run(new SetupForm(payloadDir, forceUninstall));
        }
    }

    public enum SetupAction
    {
        Install = 0,
        Uninstall = 1
    }
}
