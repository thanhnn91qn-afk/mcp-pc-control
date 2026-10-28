using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace McpPcHost
{
    /// <summary>
    /// Bơm chuột/phím qua SendInput (cùng cách HospitalPC Tray — union INPUT đúng căn x64).
    /// </summary>
    public static class InputInjector
    {
        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk, wScan;
            public uint dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        public static void Move(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public static void Click(int x, int y, string button, int clicks)
        {
            Move(x, y);
            Thread.Sleep(30);
            uint down, up;
            ParseButton(button, out down, out up);
            for (int i = 0; i < Math.Max(1, clicks); i++)
            {
                SendMouse(down, 0);
                Thread.Sleep(40);
                SendMouse(up, 0);
                if (i + 1 < clicks) Thread.Sleep(80);
            }
        }

        public static void Drag(int x1, int y1, int x2, int y2, string button)
        {
            uint down, up;
            ParseButton(button, out down, out up);
            Move(x1, y1);
            Thread.Sleep(60);
            SendMouse(down, 0);
            Thread.Sleep(60);
            const int steps = 15;
            for (int i = 1; i <= steps; i++)
            {
                Move(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps);
                Thread.Sleep(15);
            }
            Thread.Sleep(60);
            SendMouse(up, 0);
        }

        public static void Scroll(int x, int y, int delta)
        {
            Move(x, y);
            Thread.Sleep(20);
            // WHEEL_DELTA = 120
            SendMouse(MOUSEEVENTF_WHEEL, (uint)delta);
        }

        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char ch in text)
            {
                if (ch == '\n' || ch == '\r')
                {
                    KeyCombo("enter");
                    continue;
                }
                if (ch == '\t')
                {
                    KeyCombo("tab");
                    continue;
                }
                INPUT down = new INPUT();
                down.type = INPUT_KEYBOARD;
                down.u.ki.wScan = ch;
                down.u.ki.dwFlags = KEYEVENTF_UNICODE;
                INPUT up = down;
                up.u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
                Send(new INPUT[] { down, up });
                Thread.Sleep(8);
            }
        }

        /// <summary>Chuỗi phím kiểu "ctrl+s", "alt+f4", "enter", "win+r".</summary>
        public static void KeyCombo(string keys, int holdMs = 30)
        {
            if (string.IsNullOrWhiteSpace(keys))
                throw new ArgumentException("Thiếu tham số keys.");
            string[] parts = keys.Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries);
            ushort[] vks = new ushort[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                vks[i] = MapKey(parts[i].Trim());
                if (vks[i] == 0)
                    throw new ArgumentException("Phím không hợp lệ: '" + parts[i].Trim() + "' trong '" + keys + "'.");
            }

            for (int i = 0; i < vks.Length; i++)
                KeyEvent(vks[i], false, IsExtended(vks[i]));
            Thread.Sleep(holdMs);
            for (int i = vks.Length - 1; i >= 0; i--)
                KeyEvent(vks[i], true, IsExtended(vks[i]));
        }

        private static void ParseButton(string button, out uint down, out uint up)
        {
            string b = (button ?? "left").ToLowerInvariant();
            if (b == "right") { down = MOUSEEVENTF_RIGHTDOWN; up = MOUSEEVENTF_RIGHTUP; }
            else if (b == "middle") { down = MOUSEEVENTF_MIDDLEDOWN; up = MOUSEEVENTF_MIDDLEUP; }
            else if (b == "left") { down = MOUSEEVENTF_LEFTDOWN; up = MOUSEEVENTF_LEFTUP; }
            else throw new ArgumentException("button không hợp lệ: '" + button + "' (left|right|middle).");
        }

        private static void SendMouse(uint flags, uint data)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_MOUSE;
            inp.u.mi.dwFlags = flags;
            inp.u.mi.mouseData = data;
            Send(new INPUT[] { inp });
        }

        private static void KeyEvent(ushort vk, bool up, bool extended)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            inp.u.ki.wVk = vk;
            inp.u.ki.dwFlags = (up ? KEYEVENTF_KEYUP : 0u) | (extended ? KEYEVENTF_EXTENDEDKEY : 0u);
            Send(new INPUT[] { inp });
        }

        private static void Send(INPUT[] arr)
        {
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        private static bool IsExtended(ushort vk)
        {
            return vk == 0x21 || vk == 0x22 || vk == 0x23 || vk == 0x24 ||
                   vk == 0x25 || vk == 0x26 || vk == 0x27 || vk == 0x28 ||
                   vk == 0x2D || vk == 0x2E || vk == 0x5B || vk == 0x5C;
        }

        private static ushort MapKey(string name)
        {
            string n = name.ToLowerInvariant();
            switch (n)
            {
                case "ctrl": case "control": return 0x11;
                case "alt": return 0x12;
                case "shift": return 0x10;
                case "win": case "meta": case "cmd": return 0x5B;
                case "enter": case "return": return 0x0D;
                case "tab": return 0x09;
                case "esc": case "escape": return 0x1B;
                case "space": return 0x20;
                case "backspace": case "bs": return 0x08;
                case "delete": case "del": return 0x2E;
                case "insert": case "ins": return 0x2D;
                case "home": return 0x24;
                case "end": return 0x23;
                case "pageup": case "pgup": return 0x21;
                case "pagedown": case "pgdn": return 0x22;
                case "left": return 0x25;
                case "up": return 0x26;
                case "right": return 0x27;
                case "down": return 0x28;
                case "f1": return 0x70; case "f2": return 0x71; case "f3": return 0x72;
                case "f4": return 0x73; case "f5": return 0x74; case "f6": return 0x75;
                case "f7": return 0x76; case "f8": return 0x77; case "f9": return 0x78;
                case "f10": return 0x79; case "f11": return 0x7A; case "f12": return 0x7B;
            }
            if (n.Length == 1)
            {
                short scan = VkKeyScan(n[0]);
                if (scan != -1) return (ushort)(scan & 0xFF);
                return (ushort)char.ToUpperInvariant(n[0]);
            }
            return 0;
        }
    }
}
