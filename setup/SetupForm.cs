using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace McpPcSetup
{
    /// <summary>
    /// Màn hình giống HPCSetupAgent: chọn Cài đặt / Gỡ cài đặt → tiến độ → xong.
    /// </summary>
    public sealed class SetupForm : Form
    {
        private readonly string _payloadDir;
        private SetupAction _action = SetupAction.Install;
        private int _step;

        private Panel _body;
        private Panel _footer;
        private Button _btnNext;
        private Button _btnCancel;
        private Label _stepHint;
        private ProgressBar _bar;
        private Label _lblProgress;
        private TextBox _log;

        public SetupForm(string payloadDir, bool forceUninstall)
        {
            _payloadDir = payloadDir;
            Text = "MCP PC Control Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 440);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(248, 249, 251);

            _footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.FromArgb(240, 242, 245) };
            _btnCancel = new Button { Text = "Hủy", Dock = DockStyle.Right, Width = 90, FlatStyle = FlatStyle.System };
            _btnCancel.Click += delegate { Close(); };
            _btnNext = new Button { Text = "Tiếp", Dock = DockStyle.Right, Width = 120, FlatStyle = FlatStyle.System };
            _btnNext.Click += delegate { if (_step >= 2) Close(); };
            _stepHint = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                ForeColor = Color.FromArgb(90, 96, 110)
            };
            _footer.Controls.Add(_stepHint);
            _footer.Controls.Add(_btnNext);
            _footer.Controls.Add(_btnCancel);

            _body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 16, 24, 16) };
            Controls.Add(_body);
            Controls.Add(_footer);

            if (forceUninstall)
            {
                _action = SetupAction.Uninstall;
                ShowStep(1);
            }
            else
                ShowStep(0);
        }

        private void ClearBody() { _body.Controls.Clear(); }

        private void ShowStep(int step)
        {
            _step = step;
            ClearBody();
            if (step == 0)
            {
                _btnNext.Visible = false;
                _btnCancel.Enabled = true;
                BuildChoice();
                _stepHint.Text = "MCP PC Control  ·  v1.0.0";
            }
            else if (step == 1)
            {
                _btnNext.Visible = true;
                _btnNext.Enabled = false;
                _btnCancel.Enabled = false;
                BuildProgress();
                _stepHint.Text = (_action == SetupAction.Uninstall ? "Đang gỡ…  ·  " : "Đang cài…  ·  ") + "v1.0.0";
            }
        }

        private void BuildChoice()
        {
            int y = 8;
            Label h = new Label
            {
                Text = "MCP PC Control",
                Font = new Font("Segoe UI Semibold", 16f),
                AutoSize = false,
                Location = new Point(0, y),
                Size = new Size(580, 32)
            };
            _body.Controls.Add(h);
            y += 40;

            Label sub = new Label
            {
                Text = "Chọn thao tác. Cài đặt / cài lại sẽ dừng host đang chạy rồi ghi đè vào Program Files.\n" +
                       "Host tray chụp màn + chuột/phím (cùng cách HospitalPC Agent/Tray) cho AI qua MCP.",
                AutoSize = false,
                Location = new Point(0, y),
                Size = new Size(580, 52),
                ForeColor = Color.FromArgb(60, 64, 72)
            };
            _body.Controls.Add(sub);
            y += 64;

            Button btnInstall = new Button
            {
                Text = "Cài đặt / Cài lại",
                Location = new Point(0, y),
                Size = new Size(260, 44),
                FlatStyle = FlatStyle.System
            };
            btnInstall.Click += delegate
            {
                if (!PayloadOk()) return;
                _action = SetupAction.Install;
                ShowStep(1);
            };
            _body.Controls.Add(btnInstall);

            Button btnUninstall = new Button
            {
                Text = "Gỡ cài đặt",
                Location = new Point(280, y),
                Size = new Size(260, 44),
                FlatStyle = FlatStyle.System,
                ForeColor = Color.DarkRed
            };
            btnUninstall.Click += delegate
            {
                DialogResult r = MessageBox.Show(this,
                    "Gỡ MCP PC Control khỏi máy này?\n\n" +
                    "Task tray/watchdog và Program Files\\McpPcControl sẽ bị xóa.\n" +
                    "Thư mục %ProgramData%\\McpPcControl (token) được giữ lại.",
                    "Xác nhận gỡ cài",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
                _action = SetupAction.Uninstall;
                ShowStep(1);
            };
            _body.Controls.Add(btnUninstall);
            y += 60;

            Label hint = new Label
            {
                Text = "Gói hiện tại: " + _payloadDir + "\nTệp cần có khi cài: McpPcHost.exe (+ thư mục mcp\\)",
                AutoSize = false,
                Location = new Point(0, y),
                Size = new Size(580, 48),
                ForeColor = Color.FromArgb(110, 116, 128)
            };
            _body.Controls.Add(hint);
        }

        private bool PayloadOk()
        {
            if (File.Exists(Path.Combine(_payloadDir, "McpPcHost.exe"))) return true;
            MessageBox.Show(this,
                "Không tìm thấy McpPcHost.exe cạnh bộ cài.\nHãy chạy pack.cmd rồi mở McpPcSetup.exe trong out\\Setup.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private void BuildProgress()
        {
            _lblProgress = new Label
            {
                Text = _action == SetupAction.Uninstall ? "Đang gỡ cài đặt…" : "Đang cài đặt…",
                AutoSize = false,
                Location = new Point(0, 0),
                Size = new Size(580, 24)
            };
            _body.Controls.Add(_lblProgress);

            _bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Location = new Point(0, 32),
                Size = new Size(580, 22)
            };
            _body.Controls.Add(_bar);

            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(0, 64),
                Size = new Size(580, 250),
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(250, 250, 252)
            };
            _body.Controls.Add(_log);
            BeginWork();
        }

        private void BuildDone(bool ok, string error)
        {
            ClearBody();
            string title = ok
                ? (_action == SetupAction.Uninstall ? "Gỡ cài đặt thành công" : "Cài đặt thành công")
                : (_action == SetupAction.Uninstall ? "Gỡ cài thất bại" : "Cài đặt thất bại");

            Label h = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = ok ? Color.FromArgb(20, 120, 60) : Color.Firebrick,
                AutoSize = false,
                Location = new Point(0, 0),
                Size = new Size(580, 32)
            };
            _body.Controls.Add(h);

            string msg;
            if (ok)
            {
                if (_action == SetupAction.Uninstall)
                    msg = "MCP PC Control đã được gỡ khỏi máy này.\nToken trong %ProgramData%\\McpPcControl vẫn còn (xóa tay nếu muốn).";
                else
                    msg = "Đã cài → icon khay MCP PC Control.\n" +
                          "API: http://127.0.0.1:17310\n" +
                          "Mẫu MCP: %ProgramData%\\McpPcControl\\mcp.cursor.example.json\n" +
                          "Ghép vào Cursor / Claude Code / Codex — xem docs\\TICH-HOP-MCP.md";
            }
            else msg = error ?? "Đã xảy ra lỗi.";

            Label body = new Label
            {
                Text = msg,
                AutoSize = false,
                Location = new Point(0, 48),
                Size = new Size(580, 200)
            };
            _body.Controls.Add(body);

            _step = 2;
            _btnNext.Visible = true;
            _btnNext.Enabled = true;
            _btnNext.Text = "Đóng";
            _btnCancel.Visible = false;
            _stepHint.Text = "Hoàn tất  ·  v1.0.0";
        }

        private void BeginWork()
        {
            InstallOptions opt = new InstallOptions();
            opt.Action = _action;
            opt.PayloadDir = _payloadDir;
            // mcp nguồn: cạnh setup hoặc cha (repo mcp-pc-control)
            string mcpBeside = Path.Combine(_payloadDir, "mcp");
            string mcpParent = Path.GetFullPath(Path.Combine(_payloadDir, "..", "mcp"));
            opt.McpSourceDir = Directory.Exists(mcpBeside) ? mcpBeside
                : (Directory.Exists(mcpParent) ? mcpParent : mcpBeside);

            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerReportsProgress = true;
            bw.DoWork += delegate
            {
                InstallEngine eng = new InstallEngine(opt, delegate(int pct, string msg)
                {
                    bw.ReportProgress(pct, msg);
                });
                eng.Run();
            };
            bw.ProgressChanged += delegate(object s, ProgressChangedEventArgs e)
            {
                if (IsDisposed) return;
                int pct = e.ProgressPercentage;
                if (pct < 0) pct = 0;
                if (pct > 100) pct = 100;
                if (_bar != null) _bar.Value = pct;
                string msg = e.UserState as string;
                if (msg != null)
                {
                    if (_lblProgress != null) _lblProgress.Text = msg;
                    if (_log != null)
                        _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + msg + "\r\n");
                }
            };
            bw.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs e)
            {
                if (IsDisposed) return;
                if (e.Error != null) BuildDone(false, e.Error.Message);
                else BuildDone(true, null);
            };
            bw.RunWorkerAsync();
        }
    }
}
