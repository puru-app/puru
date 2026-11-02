using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AudioSwitch;

static class Program
{
    /// <summary>視窗用的程式圖示（app.ico 內嵌資源）。</summary>
    public static Icon AppIcon()
    {
        using var s = typeof(Program).Assembly.GetManifestResourceStream("app.ico");
        return s == null ? SystemIcons.Application : new Icon(s);
    }

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Local\MASAMOON.AudioSwitch", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }
}

/// <summary>
/// 常駐核心：沒有主視窗、沒有計時器、沒有輪詢。
/// 閒置時只有一個 message loop 阻塞在 GetMessage → CPU 0%。
/// </summary>
sealed class TrayContext : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "AudioSwitch";
    const int HotkeyId = 0xA5A5;

    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeyWindow;
    readonly ToolStripMenuItem openItem;
    PanelForm panel;

    uint curMods;
    Keys curKey;
    string curText;

    public TrayContext()
    {
        (curMods, curKey, curText) = LoadHotkey();

        hotkeyWindow = new HotkeyWindow(TogglePanel);
        if (!RegisterHotKey(hotkeyWindow.Handle, HotkeyId, curMods | 0x4000 /*NOREPEAT*/, (uint)curKey))
            MessageBox.Show($"快捷鍵 {curText} 已被其他程式佔用。\n請從系統匣右鍵選單「設定快捷鍵」換一組。", "Audio Switch");

        var menu = new ContextMenuStrip();
        openItem = new ToolStripMenuItem("", null, (_, _) => TogglePanel());
        menu.Items.Add(openItem);
        menu.Items.Add("設定快捷鍵…", null, (_, _) => ChangeHotkey());
        var auto = new ToolStripMenuItem("開機自動啟動") { Checked = IsAutostart(), CheckOnClick = true };
        auto.CheckedChanged += (_, _) => SetAutostart(auto.Checked);
        menu.Items.Add(auto);
        menu.Items.Add("關於 AudioSwitch…", null, (_, _) => { using var d = new AboutDialog(); d.ShowDialog(); });
        menu.Items.Add("結束", null, (_, _) => ExitThread());

        tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) TogglePanel(); };
        RefreshLabels();

        TrimMemory();
    }

    void RefreshLabels()
    {
        openItem.Text = $"開啟面板 ({curText})";
        tray.Text = $"Audio Switch  {curText}";
    }

    void TogglePanel()
    {
        if (panel != null && !panel.IsDisposed) { panel.Close(); return; }

        panel = new PanelForm();
        panel.FormClosed += (_, _) =>
        {
            panel.Dispose();
            panel = null;
            TrimMemory();   // 關閉後把記憶體還給系統
        };
        panel.ShowNearTray();
    }

    // ---------- 設定快捷鍵（即時生效，不用重啟）----------
    void ChangeHotkey()
    {
        using var dlg = new HotkeyDialog(curText);
        if (dlg.ShowDialog() != DialogResult.OK) return;

        UnregisterHotKey(hotkeyWindow.Handle, HotkeyId);
        if (RegisterHotKey(hotkeyWindow.Handle, HotkeyId, dlg.Mods | 0x4000, (uint)dlg.Key))
        {
            curMods = dlg.Mods; curKey = dlg.Key; curText = dlg.Text2;
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "hotkey.txt"), curText); } catch { }
            RefreshLabels();
        }
        else
        {
            RegisterHotKey(hotkeyWindow.Handle, HotkeyId, curMods | 0x4000, (uint)curKey);  // 還原舊的
            MessageBox.Show($"{dlg.Text2} 已被其他程式佔用，請換一組。", "Audio Switch");
        }
    }

    protected override void ExitThreadCore()
    {
        UnregisterHotKey(hotkeyWindow.Handle, HotkeyId);
        hotkeyWindow.DestroyHandle();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }

    // ---------- 快捷鍵設定檔（hotkey.txt，預設 Ctrl+Alt+A）----------
    static (uint mods, Keys key, string text) LoadHotkey()
    {
        string text = "Ctrl+Alt+A";
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "hotkey.txt");
            if (File.Exists(path)) text = File.ReadAllText(path).Trim();
            else File.WriteAllText(path, text);
        }
        catch { }

        uint mods = 0; Keys key = Keys.A;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "alt": mods |= 0x1; break;
                case "ctrl": case "control": mods |= 0x2; break;
                case "shift": mods |= 0x4; break;
                case "win": mods |= 0x8; break;
                default: if (KeyNames.TryParse(part, out var k)) key = k; break;
            }
        }
        if (mods == 0) { mods = 0x2 | 0x1; key = Keys.A; }
        // 顯示文字一律重組成可讀名稱（舊檔存的 Oem6 之類代號會變成 ]）
        var names = new List<string>();
        if ((mods & 0x2) != 0) names.Add("Ctrl");
        if ((mods & 0x1) != 0) names.Add("Alt");
        if ((mods & 0x4) != 0) names.Add("Shift");
        if ((mods & 0x8) != 0) names.Add("Win");
        names.Add(KeyNames.Label(key));
        text = string.Join("+", names);
        return (mods, key, text);
    }

    // ---------- 開機啟動 ----------
    static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(AppName) != null;
    }

    static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (k == null) return;
        if (on) k.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(AppName, false);
    }

    // ---------- 工具 ----------
    /// <summary>系統匣用 tray.ico（黑底反相版，依 puru 系列圖示邏輯）；沒有就退回 app.ico。</summary>
    static Icon LoadIcon()
    {
        var asm = typeof(Program).Assembly;
        using var s = asm.GetManifestResourceStream("tray.ico") ?? asm.GetManifestResourceStream("app.ico");
        return s == null ? SystemIcons.Application : new Icon(s, SystemInformation.SmallIconSize);
    }

    static void TrimMemory()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
    }

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr h, nint min, nint max);

    /// <summary>只負責收 WM_HOTKEY 的隱形視窗（無 UI、無繪製）。</summary>
    sealed class HotkeyWindow : NativeWindow
    {
        readonly Action onHotkey;
        public HotkeyWindow(Action onHotkey)
        {
            this.onHotkey = onHotkey;
            CreateHandle(new CreateParams());
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 /*WM_HOTKEY*/) onHotkey();
            base.WndProc(ref m);
        }
    }
}

/// <summary>關於：版本、版權聲明（© masamoon）、授權與原始碼連結。套 puru 系列亮色。</summary>
sealed class AboutDialog : Form
{
    public const string RepoUrl = "https://github.com/takhsu/puru";

    public AboutDialog()
    {
        float s = DeviceDpi / 96f;
        string ver = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

        Text = "關於 AudioSwitch";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = PanelForm.Paper;
        ForeColor = PanelForm.Ink;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size((int)(360 * s), (int)(190 * s));
        Icon = Program.AppIcon();

        var title = new Label
        {
            Text = "AUDIO SWITCH",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Left = (int)(20 * s), Top = (int)(18 * s)
        };
        var version = new Label
        {
            Text = $"版本 {ver}",
            ForeColor = PanelForm.Ink2,
            AutoSize = true,
            Left = (int)(20 * s), Top = (int)(50 * s)
        };
        var desc = new Label
        {
            Text = "一鍵切換 Windows 預設輸出／輸入裝置，耳麥麥克風自動跟著切。",
            AutoSize = false,
            Left = (int)(20 * s), Top = (int)(76 * s), Width = (int)(320 * s), Height = (int)(40 * s)
        };
        var copyright = new Label
        {
            Text = "© 2026 masamoon　MIT License",
            ForeColor = PanelForm.Ink2,
            AutoSize = true,
            Left = (int)(20 * s), Top = (int)(122 * s)
        };
        var link = new LinkLabel
        {
            Text = "原始碼與更新：github.com/takhsu/puru",
            LinkColor = PanelForm.Ink,
            ActiveLinkColor = PanelForm.Red,
            VisitedLinkColor = PanelForm.Ink,
            LinkBehavior = LinkBehavior.HoverUnderline,
            AutoSize = true,
            Left = (int)(20 * s), Top = (int)(146 * s)
        };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); } catch { }
        };
        var ok = new Button
        {
            Text = "確定", DialogResult = DialogResult.OK,
            Left = (int)(256 * s), Top = (int)(140 * s), Width = (int)(84 * s), Height = (int)(36 * s),
            FlatStyle = FlatStyle.Flat, BackColor = PanelForm.Ink, ForeColor = PanelForm.White,
            Font = new Font("Segoe UI Semibold", 9.5f)
        };
        ok.FlatAppearance.BorderSize = 0;
        // 產品紅小點：名稱右下
        Paint += (_, e) =>
        {
            int d = (int)(8 * s);
            using var b = new SolidBrush(PanelForm.Red);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillEllipse(b, title.Right + (int)(2 * s), title.Bottom - d - (int)(6 * s), d, d);
        };
        Controls.AddRange(new Control[] { title, version, desc, copyright, link, ok });
        AcceptButton = ok;
        CancelButton = ok;
    }
}

/// <summary>
/// 按鍵顯示名稱：把 Keys 列舉的代號（Oem6、D1、OemMinus…）換成鍵帽上的字（]、1、-）。
/// hotkey.txt 存可讀名稱；讀取時代號與可讀名稱都接受。
/// </summary>
static class KeyNames
{
    static readonly Dictionary<Keys, string> Labels = new()
    {
        [Keys.Oem1] = ";", [Keys.Oemplus] = "=", [Keys.Oemcomma] = ",", [Keys.OemMinus] = "-",
        [Keys.OemPeriod] = ".", [Keys.OemQuestion] = "/", [Keys.Oemtilde] = "`",
        [Keys.OemOpenBrackets] = "[", [Keys.Oem5] = "\\", [Keys.Oem6] = "]", [Keys.Oem7] = "'",
        [Keys.Oem102] = "\\", [Keys.Space] = "Space", [Keys.Return] = "Enter", [Keys.Back] = "Backspace",
        [Keys.Escape] = "Esc", [Keys.Tab] = "Tab", [Keys.Delete] = "Delete", [Keys.Insert] = "Insert",
        [Keys.Home] = "Home", [Keys.End] = "End", [Keys.PageUp] = "PageUp", [Keys.PageDown] = "PageDown",
        [Keys.Up] = "↑", [Keys.Down] = "↓", [Keys.Left] = "←", [Keys.Right] = "→",
        [Keys.Multiply] = "Num *", [Keys.Add] = "Num +", [Keys.Subtract] = "Num -", [Keys.Divide] = "Num /", [Keys.Decimal] = "Num .",
        [Keys.Scroll] = "ScrollLock", [Keys.Pause] = "Pause", [Keys.PrintScreen] = "PrintScreen", [Keys.Apps] = "Menu",
    };
    // 反查表：同一個字對到多個鍵時取第一個（例如「\」= Oem5 與 Oem102）
    static readonly Dictionary<string, Keys> Reverse = BuildReverse();
    static Dictionary<string, Keys> BuildReverse()
    {
        var d = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in Labels) d.TryAdd(kv.Value, kv.Key);
        return d;
    }

    public static string Label(Keys k)
    {
        if (Labels.TryGetValue(k, out var s)) return s;
        if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
        if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (k - Keys.NumPad0);
        return k.ToString();
    }

    public static bool TryParse(string text, out Keys k)
    {
        if (Reverse.TryGetValue(text, out k)) return true;
        if (text.Length == 1 && char.IsDigit(text[0])) { k = Keys.D0 + (text[0] - '0'); return true; }
        if (text.StartsWith("Num ", StringComparison.OrdinalIgnoreCase) && int.TryParse(text.AsSpan(4), out int n) && n is >= 0 and <= 9) { k = Keys.NumPad0 + n; return true; }
        return Enum.TryParse(text, true, out k);
    }
}

/// <summary>按鍵擷取對話框：直接按下想用的組合鍵即可（至少要有 Ctrl / Alt / Shift 其中一個）。</summary>
sealed class HotkeyDialog : Form
{
    readonly Label display;
    readonly Button ok;

    public uint Mods { get; private set; }
    public Keys Key { get; private set; }
    public string Text2 { get; private set; }

    public HotkeyDialog(string current)
    {
        float s = DeviceDpi / 96f;
        Text = "設定快捷鍵";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = MinimizeBox = false;
        TopMost = true;
        KeyPreview = true;
        // puru 系列亮色：暖白底、黑字；主要鈕黑底白字、次要鈕白底淡邊
        BackColor = PanelForm.Paper;
        ForeColor = PanelForm.Ink;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size((int)(340 * s), (int)(150 * s));

        var hint = new Label
        {
            Text = "請直接按下新的快捷鍵組合",
            Left = (int)(16 * s), Top = (int)(14 * s), Width = (int)(308 * s), Height = (int)(22 * s),
            ForeColor = PanelForm.Ink2
        };
        display = new Label
        {
            Text = current,
            Left = (int)(16 * s), Top = (int)(42 * s), Width = (int)(308 * s), Height = (int)(40 * s),
            BackColor = PanelForm.Tray,
            ForeColor = PanelForm.Ink,
            Font = new Font("Segoe UI Semibold", 13f),
            TextAlign = ContentAlignment.MiddleCenter
        };
        ok = new Button
        {
            Text = "確定", Enabled = false, DialogResult = DialogResult.OK,
            Left = (int)(150 * s), Top = (int)(100 * s), Width = (int)(84 * s), Height = (int)(36 * s),
            FlatStyle = FlatStyle.Flat, BackColor = PanelForm.Ink, ForeColor = PanelForm.White,
            Font = new Font("Segoe UI Semibold", 9.5f)
        };
        ok.FlatAppearance.BorderSize = 0;
        ok.EnabledChanged += (_, _) => { ok.BackColor = ok.Enabled ? PanelForm.Ink : PanelForm.Line; ok.ForeColor = ok.Enabled ? PanelForm.White : PanelForm.Ink2; };
        var cancel = new Button
        {
            Text = "取消", DialogResult = DialogResult.Cancel,
            Left = (int)(240 * s), Top = (int)(100 * s), Width = (int)(84 * s), Height = (int)(36 * s),
            FlatStyle = FlatStyle.Flat, BackColor = PanelForm.White, ForeColor = PanelForm.Ink
        };
        cancel.FlatAppearance.BorderSize = 1;
        cancel.FlatAppearance.BorderColor = PanelForm.Line;
        cancel.FlatAppearance.MouseOverBackColor = PanelForm.Ink;
        cancel.MouseEnter += (_, _) => cancel.ForeColor = PanelForm.White;
        cancel.MouseLeave += (_, _) => cancel.ForeColor = PanelForm.Ink;
        Controls.AddRange(new Control[] { hint, display, ok, cancel });
        CancelButton = cancel;

        // 按鍵由表單接收，不讓按鈕搶焦點吃掉空白鍵 / Enter
        ok.TabStop = cancel.TabStop = false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys code = keyData & Keys.KeyCode;
        if (code is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return true;
        if (keyData == Keys.Escape) return base.ProcessCmdKey(ref msg, keyData);

        uint mods = 0;
        var parts = new List<string>();
        if ((keyData & Keys.Control) != 0) { mods |= 0x2; parts.Add("Ctrl"); }
        if ((keyData & Keys.Alt) != 0) { mods |= 0x1; parts.Add("Alt"); }
        if ((keyData & Keys.Shift) != 0) { mods |= 0x4; parts.Add("Shift"); }

        if (mods == 0)
        {
            display.Text = "需要搭配 Ctrl / Alt / Shift";
            ok.Enabled = false;
            return true;
        }

        parts.Add(KeyNames.Label(code));
        Mods = mods;
        Key = code;
        Text2 = string.Join("+", parts);
        display.Text = Text2;
        ok.Enabled = true;
        return true;
    }
}
