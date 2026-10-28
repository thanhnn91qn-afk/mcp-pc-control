using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace McpPcSetup
{
    public sealed class InstallOptions
    {
        public SetupAction Action;
        public string PayloadDir;
        public string DestDir;
        public string McpSourceDir;
        public bool WriteMcpExamples = true;
    }

    public delegate void InstallProgressHandler(int percent, string message);

    /// <summary>
    /// Động cơ cài/gỡ giống Agent: dừng tiến trình → chép Program Files → /install hoặc /uninstall → ghi mẫu MCP.
    /// </summary>
    public sealed class InstallEngine
    {
        private readonly InstallOptions _opt;
        private readonly InstallProgressHandler _progress;

        public InstallEngine(InstallOptions opt, InstallProgressHandler progress)
        {
            _opt = opt;
            _progress = progress;
        }

        public void Run()
        {
            if (_opt.Action == SetupAction.Uninstall) Uninstall();
            else Install();
        }

        private void Report(int pct, string msg)
        {
            if (_progress != null) _progress(pct, msg);
        }

        private void Install()
        {
            string payload = _opt.PayloadDir;
            string dest = string.IsNullOrEmpty(_opt.DestDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "McpPcControl")
                : _opt.DestDir;

            string hostExe = Path.Combine(payload, "McpPcHost.exe");
            if (!File.Exists(hostExe))
                throw new FileNotFoundException("Thiếu McpPcHost.exe trong gói cài đặt.", hostExe);

            Report(5, "Dừng McpPcHost (giải phóng tệp)…");
            KillProcesses(new string[] { "McpPcHost" });
            Thread.Sleep(800);

            Report(15, "Tạo thư mục " + dest);
            EnsureDir(dest);
            EnsureDir(Path.Combine(dest, "mcp"));

            Report(35, "Sao chép host + MCP…");
            CopyFile(hostExe, Path.Combine(dest, "McpPcHost.exe"));
            string cfg = Path.Combine(payload, "McpPcHost.exe.config");
            if (File.Exists(cfg)) CopyFile(cfg, Path.Combine(dest, "McpPcHost.exe.config"));

            // MCP node: ưu tiên thư mục mcp cạnh setup; không thì cạnh repo
            string mcpSrc = !string.IsNullOrEmpty(_opt.McpSourceDir) && Directory.Exists(_opt.McpSourceDir)
                ? _opt.McpSourceDir
                : Path.Combine(payload, "mcp");
            if (!Directory.Exists(mcpSrc))
                mcpSrc = Path.GetFullPath(Path.Combine(payload, "..", "mcp"));
            if (Directory.Exists(mcpSrc))
                CopyDir(mcpSrc, Path.Combine(dest, "mcp"));

            // package.json + node_modules nếu có trong gói
            string pkg = Path.Combine(payload, "package.json");
            if (File.Exists(pkg)) CopyFile(pkg, Path.Combine(dest, "package.json"));
            string nm = Path.Combine(payload, "node_modules");
            if (Directory.Exists(nm))
            {
                Report(50, "Sao chép node_modules…");
                CopyDir(nm, Path.Combine(dest, "node_modules"));
            }

            string data = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "McpPcControl");
            EnsureDir(data);
            string ini = Path.Combine(data, "config.ini");
            if (!File.Exists(ini)) File.WriteAllText(ini, "port=17310\r\n", Encoding.UTF8);

            Report(70, "Đăng ký tray + watchdog (giống Agent)…");
            int code = RunProcess(Path.Combine(dest, "McpPcHost.exe"), "/install", dest, 60000);
            if (code != 0)
                throw new Exception("Cài tray/task thất bại (mã " + code + "). Chạy McpPcSetup.exe bằng quyền Admin.");

            if (_opt.WriteMcpExamples)
            {
                Report(88, "Ghi mẫu cấu hình MCP…");
                WriteMcpExamples(dest, data);
            }

            Report(100, "Cài đặt MCP PC Control hoàn tất (tự chạy khi đăng nhập).");
        }

        private void Uninstall()
        {
            string dest = string.IsNullOrEmpty(_opt.DestDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "McpPcControl")
                : _opt.DestDir;

            Report(10, "Dừng host…");
            string exe = Path.Combine(dest, "McpPcHost.exe");
            if (File.Exists(exe))
            {
                RunProcess(exe, "/uninstall", dest, 60000);
            }
            else
            {
                KillProcesses(new string[] { "McpPcHost" });
                RunProcess("schtasks", "/Delete /TN \"McpPcControlTray\" /F", null, 15000);
                RunProcess("schtasks", "/Delete /TN \"McpPcControlWatchdog\" /F", null, 15000);
            }

            KillProcesses(new string[] { "McpPcHost" });
            Thread.Sleep(800);

            Report(60, "Xóa thư mục chương trình…");
            try
            {
                if (Directory.Exists(dest))
                    Directory.Delete(dest, true);
            }
            catch (Exception ex)
            {
                Report(70, "Cảnh báo xóa thư mục: " + ex.Message);
                // thử xóa từng file
                try
                {
                    foreach (string f in Directory.GetFiles(dest, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                    }
                }
                catch { }
            }

            Report(100, "Gỡ cài đặt xong. %ProgramData%\\McpPcControl (token) được giữ lại.");
        }

        private static void WriteMcpExamples(string installDir, string dataDir)
        {
            string tokenFile = Path.Combine(dataDir, "token.txt");
            string token = File.Exists(tokenFile) ? File.ReadAllText(tokenFile).Trim() : "";
            string indexJs = Path.Combine(installDir, "mcp", "index.mjs").Replace("\\", "/");
            string indexJsWin = Path.Combine(installDir, "mcp", "index.mjs").Replace("\\", "\\\\");

            string cursor = "{\n  \"mcpServers\": {\n    \"pc-control\": {\n" +
                "      \"command\": \"node\",\n" +
                "      \"args\": [\"" + indexJs + "\"],\n" +
                "      \"env\": {\n" +
                "        \"MCP_PC_TOKEN\": \"" + token + "\",\n" +
                "        \"MCP_PC_PORT\": \"17310\"\n" +
                "      }\n    }\n  }\n}\n";
            File.WriteAllText(Path.Combine(dataDir, "mcp.cursor.example.json"), cursor, Encoding.UTF8);

            string claude = "{\n  \"mcpServers\": {\n    \"pc-control\": {\n" +
                "      \"command\": \"node\",\n" +
                "      \"args\": [\"" + indexJsWin + "\"],\n" +
                "      \"env\": {\n" +
                "        \"MCP_PC_TOKEN\": \"" + token + "\",\n" +
                "        \"MCP_PC_PORT\": \"17310\"\n" +
                "      }\n    }\n  }\n}\n";
            File.WriteAllText(Path.Combine(dataDir, "mcp.claude.example.json"), claude, Encoding.UTF8);

            // Lối tắt mở hướng dẫn
            File.WriteAllText(Path.Combine(dataDir, "HUONG-DAN-MCP.txt"),
                "MCP PC Control da cai.\n\n" +
                "1) Icon khay = host dang chay (API http://127.0.0.1:17310).\n" +
                "2) Ghep mau: mcp.cursor.example.json / mcp.claude.example.json\n" +
                "3) Can Node.js 18+ tren PATH.\n" +
                "4) Chi tiet: docs trong goi mcp-pc-control.\n",
                Encoding.UTF8);
        }

        private static void EnsureDir(string d)
        {
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
        }

        private static void CopyFile(string src, string dst)
        {
            EnsureDir(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
        }

        private static void CopyDir(string src, string dst)
        {
            EnsureDir(dst);
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(src.Length).TrimStart('\\', '/');
                EnsureDir(Path.Combine(dst, rel));
            }
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart('\\', '/');
                string target = Path.Combine(dst, rel);
                EnsureDir(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void KillProcesses(string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(name))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
                catch { }
            }
        }

        private static int RunProcess(string file, string args, string workDir, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                psi.Arguments = args ?? "";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                if (!string.IsNullOrEmpty(workDir)) psi.WorkingDirectory = workDir;
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
