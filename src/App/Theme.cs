// Cores, fontes, escala de DPI e modo escuro nativo do Windows.
// Visual neutro: troque as cores das paletas abaixo para mudar o tema.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public enum ThemeMode
    {
        Sistema,
        Escuro,
        Claro
    }

    public static class Theme
    {
        // ---- paleta atual (trocada por Apply) ----
        public static Color Back, Surface, Border, Text, Muted, Accent, AccentHover, AccentSoft, Selected,
            RowHover, Disabled, Input, Link,
            RiskLowFg, RiskLowBg, RiskMidFg, RiskMidBg, RiskHighFg, RiskHighBg, TagFg, TagBg;

        public static bool Dark { get; private set; }
        public static ThemeMode Mode { get; private set; }

        static Theme()
        {
            ApplyPalette(false);
        }

        static void ApplyPalette(bool dark)
        {
            Dark = dark;
            if (!dark)
            {
                Back = Color.FromArgb(245, 246, 248);
                Surface = Color.White;
                Border = Color.FromArgb(222, 225, 230);
                Text = Color.FromArgb(31, 35, 40);
                Muted = Color.FromArgb(101, 109, 118);
                Accent = Color.FromArgb(47, 111, 237);
                AccentHover = Color.FromArgb(31, 92, 210);
                AccentSoft = Color.FromArgb(232, 240, 254);
                Selected = Color.FromArgb(240, 245, 255);
                RowHover = Color.FromArgb(250, 251, 252);
                Disabled = Color.FromArgb(170, 175, 182);
                Input = Color.White;
                Link = Color.FromArgb(47, 111, 237);
                RiskLowFg = Color.FromArgb(26, 127, 55); RiskLowBg = Color.FromArgb(218, 251, 225);
                RiskMidFg = Color.FromArgb(154, 103, 0); RiskMidBg = Color.FromArgb(255, 248, 197);
                RiskHighFg = Color.FromArgb(207, 34, 46); RiskHighBg = Color.FromArgb(255, 235, 233);
                TagFg = Color.FromArgb(87, 96, 106); TagBg = Color.FromArgb(234, 238, 242);
            }
            else
            {
                Back = Color.FromArgb(24, 26, 30);
                Surface = Color.FromArgb(33, 36, 41);
                Border = Color.FromArgb(52, 57, 64);
                Text = Color.FromArgb(230, 232, 235);
                Muted = Color.FromArgb(154, 163, 174);
                Accent = Color.FromArgb(76, 141, 255);
                AccentHover = Color.FromArgb(106, 161, 255);
                AccentSoft = Color.FromArgb(35, 50, 77);
                Selected = Color.FromArgb(31, 42, 61);
                RowHover = Color.FromArgb(39, 43, 49);
                Disabled = Color.FromArgb(92, 99, 108);
                Input = Color.FromArgb(28, 31, 35);
                Link = Color.FromArgb(110, 165, 255);
                RiskLowFg = Color.FromArgb(86, 211, 100); RiskLowBg = Color.FromArgb(28, 54, 38);
                RiskMidFg = Color.FromArgb(227, 179, 65); RiskMidBg = Color.FromArgb(58, 48, 22);
                RiskHighFg = Color.FromArgb(255, 123, 114); RiskHighBg = Color.FromArgb(69, 32, 31);
                TagFg = Color.FromArgb(174, 182, 192); TagBg = Color.FromArgb(46, 50, 56);
            }
        }

        // ---- preferência do usuário ----
        const string PrefKey = @"Software\" + AppInfo.DataFolderName;

        public static ThemeMode LoadMode()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(PrefKey))
                {
                    string v = k == null ? null : k.GetValue("Tema") as string;
                    if (v == "escuro") return ThemeMode.Escuro;
                    if (v == "claro") return ThemeMode.Claro;
                }
            }
            catch { }
            return ThemeMode.Sistema;
        }

        static void SaveMode(ThemeMode m)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(PrefKey))
                    k.SetValue("Tema", m == ThemeMode.Escuro ? "escuro" : m == ThemeMode.Claro ? "claro" : "sistema");
            }
            catch { }
        }

        /// <summary>O Windows está em modo escuro para apps?</summary>
        public static bool SystemIsDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>Define o modo e aplica a paleta. Devolve true se a paleta mudou.</summary>
        public static bool SetMode(ThemeMode m, bool save)
        {
            Mode = m;
            if (save) SaveMode(m);
            bool dark = m == ThemeMode.Escuro || (m == ThemeMode.Sistema && SystemIsDark());
            bool changed = dark != Dark;
            ApplyPalette(dark);
            Native.SetAppDarkMode(dark);
            return changed;
        }

        public static string ModeLabel(ThemeMode m)
        {
            switch (m)
            {
                case ThemeMode.Escuro: return "Tema: Escuro";
                case ThemeMode.Claro: return "Tema: Claro";
                default: return "Tema: Sistema";
            }
        }

        // ---- DPI ----
        static float scale = 1f;
        public static float Scale { get { return scale; } }

        public static void Init(Control c)
        {
            using (Graphics g = c.CreateGraphics())
                scale = Math.Max(1f, g.DpiX / 96f);
        }

        /// <summary>Converte pixels de projeto (96 DPI) para o DPI atual.</summary>
        public static int S(int px) { return (int)Math.Round(px * scale); }

        // ---- fontes ----
        static string family;
        static string Family
        {
            get
            {
                if (family == null)
                {
                    family = "Segoe UI";
                    using (Font f = new Font(family, 10f))
                        if (!string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase)) family = FontFamily.GenericSansSerif.Name;
                }
                return family;
            }
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font(Family, size, style, GraphicsUnit.Point);
        }

        public static readonly Font Body = Font(9.75f, FontStyle.Regular);
        public static readonly Font BodyBold = Font(9.75f, FontStyle.Bold);
        public static readonly Font Small = Font(8.25f, FontStyle.Regular);
        public static readonly Font SmallBold = Font(8.25f, FontStyle.Bold);
        public static readonly Font Title = Font(15f, FontStyle.Bold);
        public static readonly Font Subtitle = Font(11.25f, FontStyle.Bold);

        // ---- desenho ----
        public static void RiskColors(Risk r, out Color fg, out Color bg)
        {
            switch (r)
            {
                case Risk.Alto: fg = RiskHighFg; bg = RiskHighBg; break;
                case Risk.Medio: fg = RiskMidFg; bg = RiskMidBg; break;
                default: fg = RiskLowFg; bg = RiskLowBg; break;
            }
        }

        public static string RiskText(Risk r)
        {
            switch (r)
            {
                case Risk.Alto: return "Risco alto";
                case Risk.Medio: return "Risco médio";
                default: return "Seguro";
            }
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Desenha uma etiqueta arredondada e devolve a largura usada.</summary>
        public static int DrawPill(Graphics g, string text, Font font, Color fg, Color bg, int x, int y)
        {
            Size ts = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
            int padX = S(7), padY = S(2);
            Rectangle r = new Rectangle(x, y, ts.Width + padX * 2, ts.Height + padY * 2);
            using (GraphicsPath p = Rounded(r, r.Height / 2))
            using (SolidBrush b = new SolidBrush(bg))
                g.FillPath(b, p);
            TextRenderer.DrawText(g, text, font, new Point(x + padX, y + padY), fg, TextFormatFlags.NoPadding);
            return r.Width;
        }

        public static int PillHeight(Graphics g, Font font)
        {
            return TextRenderer.MeasureText(g, "Ag", font, Size.Empty, TextFormatFlags.NoPadding).Height + S(4);
        }

        public static int CheckSize { get { return S(18); } }

        /// <summary>Caixa de seleção própria, igual nos dois temas.</summary>
        public static void DrawCheck(Graphics g, Rectangle r, bool isChecked, bool enabled, bool hover)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color border = !enabled ? Disabled : isChecked ? Accent : hover ? Accent : Muted;
            Color fill = !enabled ? TagBg : isChecked ? Accent : Input;
            using (GraphicsPath p = Rounded(r, S(4)))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                using (Pen pen = new Pen(border, Math.Max(1f, S(1) * 1.4f))) g.DrawPath(pen, p);
            }
            if (isChecked)
            {
                using (Pen pen = new Pen(enabled ? Color.White : Disabled, Math.Max(2f, S(2))))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    PointF[] pts = {
                        new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.52f),
                        new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.70f),
                        new PointF(r.X + r.Width * 0.77f, r.Y + r.Height * 0.32f) };
                    g.DrawLines(pen, pts);
                }
            }
            g.SmoothingMode = old;
        }
    }

    /// <summary>Chamadas do Windows para o modo escuro nativo (barra de título, rolagem, menus).</summary>
    public static class Native
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        static extern int SetPreferredAppMode(int mode);

        [DllImport("uxtheme.dll", EntryPoint = "#136")]
        static extern void FlushMenuThemes();

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        static int build = -1;
        static int Build
        {
            get
            {
                if (build < 0)
                {
                    build = 0;
                    try
                    {
                        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                        {
                            object v = k == null ? null : k.GetValue("CurrentBuildNumber");
                            if (v != null) int.TryParse(v.ToString(), out build);
                        }
                    }
                    catch { }
                }
                return build;
            }
        }

        static bool IsWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }

        /// <summary>Menus de contexto e controles comuns seguem o tema (Windows 10 1903+).</summary>
        public static void SetAppDarkMode(bool dark)
        {
            if (!IsWindows || Build < 18362) return;
            try { SetPreferredAppMode(dark ? 2 : 3); FlushMenuThemes(); } catch { }
        }

        /// <summary>Barra de título escura (Windows 10 1809+ e Windows 11).</summary>
        public static void TitleBar(Form f, bool dark)
        {
            if (!IsWindows || !f.IsHandleCreated || Build < 17763) return;
            try
            {
                int v = dark ? 1 : 0;
                int attr = Build >= 18985 ? 20 : 19;
                DwmSetWindowAttribute(f.Handle, attr, ref v, 4);
                // força o Windows a redesenhar a moldura
                SetWindowPos(f.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0004 | 0x0020 | 0x0010);
            }
            catch { }
        }

        /// <summary>Barras de rolagem e controles nativos escuros (TextBox, ListView, painéis com rolagem).</summary>
        public static void Control(Control c, bool dark)
        {
            if (!IsWindows || !c.IsHandleCreated || Build < 17763) return;
            try
            {
                SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
                ListView lv = c as ListView;
                if (lv != null)
                {
                    IntPtr header = SendMessage(lv.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero); // LVM_GETHEADER
                    if (header != IntPtr.Zero) SetWindowTheme(header, dark ? "DarkMode_ItemsView" : "ItemsView", null);
                }
            }
            catch { }
        }

        /// <summary>Aplica o tema nativo a um controle agora e sempre que o handle for recriado.</summary>
        public static void Track(Control c)
        {
            c.HandleCreated += delegate { Control(c, Theme.Dark); };
            if (c.IsHandleCreated) Control(c, Theme.Dark);
        }
    }

    /// <summary>Janela com barra de título no tema atual.</summary>
    public class ThemedForm : Form
    {
        public ThemedForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body;
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TitleBar(this, Theme.Dark);
        }
    }

    /// <summary>Botão plano com cantos arredondados (primário ou secundário).</summary>
    public class FlatButton : Button
    {
        public bool Primary;
        bool hover;

        public FlatButton(string text, bool primary)
        {
            Text = text;
            Primary = primary;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            Height = Theme.S(36);
            Padding = new Padding(Theme.S(14), 0, Theme.S(14), 0);
            AutoSize = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            int w = TextRenderer.MeasureText(text, Font).Width + Theme.S(36);
            Width = Math.Max(Theme.S(110), w);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = Parent != null ? Parent.BackColor : Theme.Back;
            g.Clear(parent);
            Color bg, fg, border;
            if (!Enabled) { bg = Theme.TagBg; fg = Theme.Disabled; border = Theme.TagBg; }
            else if (Primary) { bg = hover ? Theme.AccentHover : Theme.Accent; fg = Color.White; border = bg; }
            else { bg = hover ? Theme.Selected : Theme.Surface; fg = Theme.Text; border = Theme.Border; }
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Rounded(r, Theme.S(6)))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Font, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Caixa de seleção com texto, desenhada à mão (fica correta no tema escuro).</summary>
    public class CheckToggle : Control
    {
        bool isChecked, hover;
        public event EventHandler CheckedChanged;

        public CheckToggle(string text)
        {
            Text = text;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleName = text;
            Size sz = TextRenderer.MeasureText(text, Font);
            Size = new Size(Theme.CheckSize + Theme.S(8) + sz.Width + Theme.S(4), Math.Max(Theme.CheckSize, sz.Height) + Theme.S(6));
        }

        public bool Checked
        {
            get { return isChecked; }
            set { if (value == isChecked) return; isChecked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);
            int cs = Theme.CheckSize;
            Rectangle box = new Rectangle(0, (Height - cs) / 2, cs, cs);
            Theme.DrawCheck(g, box, isChecked, Enabled, hover);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(cs + Theme.S(8), 0, Width - cs - Theme.S(8), Height),
                Enabled ? Theme.Text : Theme.Disabled, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, new Rectangle(cs + Theme.S(5), 1, Width - cs - Theme.S(6), Height - 2));
        }
    }

    /// <summary>Campo de texto com borda arredondada no tema atual.</summary>
    public class SearchField : Panel
    {
        public readonly TextBox Box;

        public SearchField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(34);
            Padding = new Padding(Theme.S(10), Theme.S(8), Theme.S(10), Theme.S(4));
            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.Dock = DockStyle.Fill;
            Box.Font = Theme.Body;
            Controls.Add(Box);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            ApplyColors();
        }

        public void ApplyColors()
        {
            BackColor = Theme.Input;
            Box.BackColor = Theme.Input;
            Box.ForeColor = Theme.Text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Rounded(r, Theme.S(6)))
            {
                using (SolidBrush b = new SolidBrush(Theme.Input)) g.FillPath(b, p);
                using (Pen pen = new Pen(Box.Focused ? Theme.Accent : Theme.Border, Box.Focused ? 1.5f : 1f)) g.DrawPath(pen, p);
            }
        }
    }
}
