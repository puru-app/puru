using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AudioSwitch;

/// <summary>
/// 面板：套用 puru 系列 UI／UX 設計指南 v0.1＋2026-11-02 補充（亮色基準）。
/// 每次開啟才建立，關閉即銷毀。
/// </summary>
public sealed class PanelForm : Form
{
    // ---- puru 系列配色（07A）----
    public static readonly Color Paper   = Color.FromArgb(0xF5, 0xF5, 0xF2);  // 基底暖白：視窗背景
    public static readonly Color Rail    = Color.FromArgb(0xFA, 0xFA, 0xF8);  // 側欄白：標題區
    public static readonly Color White   = Color.FromArgb(0xFF, 0xFF, 0xFF);  // 內容白：控制項
    public static readonly Color Tray    = Color.FromArgb(0xEF, 0xEF, 0xEC);  // 承托灰：輕量群組
    public static readonly Color Ink     = Color.FromArgb(0x17, 0x17, 0x17);  // 品牌黑：文字、主要／選取
    public static readonly Color Ink2    = Color.FromArgb(0x64, 0x67, 0x60);  // 次要文字
    public static readonly Color Line    = Color.FromArgb(0xDA, 0xDB, 0xD6);  // 一般邊線
    public static readonly Color Red     = Color.FromArgb(0xD4, 0x11, 0x16);  // 產品紅：只做小焦點

    readonly float s;           // DPI scale
    readonly FlowLayoutPanel flow;
    readonly int headerH;
    int radius;

    // 輸出輸入聯動：點輸出裝置時，一併把同一支裝置的麥克風設為預設輸入（link.txt，預設開）
    static readonly string LinkPath = Path.Combine(AppContext.BaseDirectory, "link.txt");
    static bool linkEnabled = LoadLink();
    List<AudioDevice> inputs = new();

    static bool LoadLink()
    {
        try { if (File.Exists(LinkPath)) return File.ReadAllText(LinkPath).Trim() != "0"; } catch { }
        return true;
    }

    static void SaveLink(bool on)
    {
        try { File.WriteAllText(LinkPath, on ? "1" : "0"); } catch { }
    }

    public PanelForm()
    {
        s = DeviceDpi / 96f;
        radius = (int)(8 * s);          // 浮層導角 8px
        headerH = (int)(44 * s);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Paper;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10f);
        KeyPreview = true;
        DoubleBuffered = true;
        Width = (int)(320 * s);

        // 標題區：產品名稱（大寫、粗體）＋右下 8px 產品紅小點；無框無線
        var header = new Panel { Dock = DockStyle.Top, Height = headerH, BackColor = Rail };
        var title = new Label
        {
            Text = "AUDIO SWITCH",
            AutoSize = true,
            BackColor = Rail,
            ForeColor = Ink,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point((int)(16 * s), (int)(11 * s))
        };
        header.Controls.Add(title);
        header.Paint += (_, e) =>
        {
            int d = (int)(8 * s);
            using var b = new SolidBrush(Red);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillEllipse(b, title.Right + (int)(2 * s), title.Bottom - d - (int)(4 * s), d, d);
        };

        flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding((int)(16 * s), (int)(4 * s), (int)(16 * s), (int)(16 * s)),
            BackColor = Paper
        };

        Controls.Add(flow);
        Controls.Add(header);
        Rebuild();

        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Deactivate += (_, _) => Close();
    }

    void Rebuild()
    {
        flow.SuspendLayout();
        foreach (Control c in flow.Controls) c.Dispose();
        flow.Controls.Clear();

        inputs = CoreAudio.List(CoreAudio.Capture);
        AddSection("輸出", CoreAudio.List(CoreAudio.Render), isOutput: true);
        AddSection("輸入", inputs, isOutput: false);
        AddLinkToggle();

        flow.ResumeLayout(true);
        int h = 0;
        foreach (Control c in flow.Controls) h += c.Height + c.Margin.Vertical;
        Height = headerH + flow.Padding.Vertical + h;
        ApplyRoundedRegion();
    }

    void AddLinkToggle()
    {
        var cb = new CheckBox
        {
            Text = "切輸出時一併切同裝置的麥克風",
            Checked = linkEnabled,
            ForeColor = linkEnabled ? Ink : Ink2,
            BackColor = Paper,
            AutoSize = false,
            Width = Width - flow.Padding.Horizontal,
            Height = (int)(28 * s),
            Margin = new Padding(0, (int)(12 * s), 0, 0),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f)
        };
        cb.FlatAppearance.BorderSize = 0;
        cb.FlatAppearance.CheckedBackColor = Paper;
        cb.CheckedChanged += (_, _) =>
        {
            linkEnabled = cb.Checked;
            cb.ForeColor = linkEnabled ? Ink : Ink2;
            SaveLink(linkEnabled);
        };
        flow.Controls.Add(cb);
    }

    /// <summary>點了輸出裝置後，找同一支裝置的輸入一起設為預設；找不到或已是預設就不動。</summary>
    void LinkInput(AudioDevice output)
    {
        if (!linkEnabled) return;
        string key = CoreAudio.MatchKey(output.Name);
        if (string.IsNullOrEmpty(key)) return;
        var mic = inputs.FirstOrDefault(i => CoreAudio.MatchKey(i.Name) == key);
        if (mic == null || mic.IsDefault) return;
        try { CoreAudio.SetDefault(mic.Id); } catch { }
    }

    void AddSection(string title, List<AudioDevice> devices, bool isOutput)
    {
        int rowW = Width - flow.Padding.Horizontal;
        // 群組標題：次要文字色、半粗體、小字
        flow.Controls.Add(new Label
        {
            Text = title,
            ForeColor = Ink2,
            BackColor = Paper,
            Font = new Font("Segoe UI Semibold", 9f),
            AutoSize = false,
            Width = rowW,
            Height = (int)(26 * s),
            Margin = new Padding(0, (int)(12 * s), 0, (int)(2 * s)),
            TextAlign = ContentAlignment.BottomLeft
        });

        if (devices.Count == 0)
        {
            flow.Controls.Add(new Label { Text = "沒有可用裝置", ForeColor = Ink2, BackColor = Paper, AutoSize = false, Width = rowW, Height = (int)(30 * s) });
            return;
        }

        foreach (var d in devices)
        {
            // 選取＝黑底白字無框；一般＝白底黑字 1px 淡邊；滑過＝黑塊反白
            var btn = new Button
            {
                Text = d.Name,
                Tag = d,
                Width = rowW,
                Height = (int)(36 * s),
                Margin = new Padding(0, (int)(6 * s), 0, 0),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding((int)(12 * s), 0, (int)(8 * s), 0),
                BackColor = d.IsDefault ? Ink : White,
                ForeColor = d.IsDefault ? White : Ink,
                Font = new Font("Segoe UI", 9.5f, d.IsDefault ? FontStyle.Bold : FontStyle.Regular),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                AutoEllipsis = true
            };
            btn.FlatAppearance.BorderSize = d.IsDefault ? 0 : 1;
            btn.FlatAppearance.BorderColor = Line;
            btn.FlatAppearance.MouseOverBackColor = Ink;
            btn.FlatAppearance.MouseDownBackColor = Ink;
            if (!d.IsDefault)
            {
                btn.MouseEnter += (_, _) => btn.ForeColor = White;
                btn.MouseLeave += (_, _) => btn.ForeColor = Ink;
            }
            btn.Click += (_, _) =>
            {
                try { CoreAudio.SetDefault(d.Id); } catch { /* 裝置剛被拔除等情況 */ }
                if (isOutput) LinkInput(d);
                Rebuild();
            };
            btn.Region = RoundRegion(btn.Width, btn.Height, (int)(6 * s));   // 按鈕導角 6px
            flow.Controls.Add(btn);
        }
    }

    static Region RoundRegion(int w, int h, int r)
    {
        using var p = new GraphicsPath();
        int d = r * 2;
        p.AddArc(0, 0, d, d, 180, 90);
        p.AddArc(w - d, 0, d, d, 270, 90);
        p.AddArc(w - d, h - d, d, d, 0, 90);
        p.AddArc(0, h - d, d, d, 90, 90);
        p.CloseFigure();
        return new Region(p);
    }

    void ApplyRoundedRegion()
    {
        Region?.Dispose();
        Region = RoundRegion(Width, Height, radius);
    }

    protected override bool ShowWithoutActivation => false;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW：不出現在 Alt+Tab
            cp.ClassStyle |= 0x20000;  // CS_DROPSHADOW：浮層陰影（指南：只有浮層才有陰影）
            return cp;
        }
    }

    /// <summary>顯示在游標所在螢幕的右下角（工作列上方）。</summary>
    public void ShowNearTray()
    {
        var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(wa.Right - Width - (int)(12 * s), wa.Bottom - Height - (int)(12 * s));
        Show();
        Activate();
        SetForegroundWindow(Handle);
    }

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
}
