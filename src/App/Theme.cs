// Cores, fontes, escala de DPI e modo escuro nativo do Windows.
// Design "Dark premium" (17/11/2026): escuro é o tema principal; o claro segue a mesma estrutura.
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

    /// <summary>Tom da caixa de seleção conforme a situação do ajuste no PC.</summary>
    public enum CheckTone
    {
        Normal,
        Applied,
        Partial
    }

    public static class Theme
    {
        // ---- paleta atual (trocada por ApplyPalette) ----
        public static Color Back, Surface, Card, Raised, Border, Text, Muted, Accent, AccentHover, AccentPressed, Bright,
            AccentSoft, Selected, SelectedBorder, RowHover, Disabled, Input, Link, LogBack,
            Success, Warning, Danger,
            RiskLowFg, RiskLowBg, RiskMidFg, RiskMidBg, RiskHighFg, RiskHighBg, TagFg, TagBg, InfoFg, InfoBg,
            AppliedBg, AppliedHover, AppliedBorder, PartialBg, PartialHover, PartialBorder;

        public static bool Dark { get; private set; }
        public static ThemeMode Mode { get; private set; }

        static Theme()
        {
            ApplyPalette(true);
        }

        static Color Hex(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        /// <summary>Cor <paramref name="fg"/> com opacidade <paramref name="a"/> sobre o fundo <paramref name="bg"/>.</summary>
        public static Color Blend(Color fg, float a, Color bg)
        {
            return Color.FromArgb(
                (int)Math.Round(fg.R * a + bg.R * (1 - a)),
                (int)Math.Round(fg.G * a + bg.G * (1 - a)),
                (int)Math.Round(fg.B * a + bg.B * (1 - a)));
        }

        static void ApplyPalette(bool dark)
        {
            Dark = dark;
            Accent = Hex(0x2563EB); AccentHover = Hex(0x1D4ED8); AccentPressed = Hex(0x1E40AF); Bright = Hex(0x3B82F6);
            Success = Hex(0x22C55E); Warning = Hex(0xF59E0B); Danger = Hex(0xEF4444);
            if (dark)
            {
                Back = Hex(0x0B0F19);
                Surface = Hex(0x111827);
                Card = Hex(0x161F2E);
                Raised = Hex(0x1B2638);
                Border = Hex(0x263244);
                Text = Hex(0xF8FAFC);
                Muted = Hex(0x94A3B8);
                AccentSoft = Blend(Bright, 0.14f, Surface);
                Selected = Hex(0x172238);
                SelectedBorder = Blend(Bright, 0.55f, Card);
                RowHover = Hex(0x1A2436);
                Disabled = Hex(0x5B6B82);
                Input = Card;
                Link = Hex(0x60A5FA);
                LogBack = Hex(0x0E1422);
                RiskLowFg = Hex(0x86EFAC); RiskLowBg = Blend(Success, 0.12f, Card);
                RiskMidFg = Hex(0xFCD34D); RiskMidBg = Blend(Warning, 0.14f, Card);
                RiskHighFg = Hex(0xFCA5A5); RiskHighBg = Blend(Danger, 0.14f, Card);
                TagFg = Hex(0xCBD5E1); TagBg = Blend(Muted, 0.12f, Card);
                InfoFg = Hex(0x93C5FD); InfoBg = Blend(Bright, 0.14f, Card);
                AppliedBg = Hex(0x14211F); AppliedHover = Hex(0x172822); AppliedBorder = Blend(Success, 0.28f, Card);
                PartialBg = Blend(Warning, 0.05f, Card); PartialHover = Blend(Warning, 0.08f, Card); PartialBorder = Blend(Warning, 0.30f, Card);
            }
            else
            {
                Back = Hex(0xF1F5F9);
                Surface = Hex(0xFFFFFF);
                Card = Hex(0xFFFFFF);
                Raised = Hex(0xF8FAFC);
                Border = Hex(0xE2E8F0);
                Text = Hex(0x0F172A);
                Muted = Hex(0x475569);
                AccentSoft = Hex(0xEFF6FF);
                Selected = Hex(0xEFF6FF);
                SelectedBorder = Hex(0x93C5FD);
                RowHover = Hex(0xF8FAFC);
                Disabled = Hex(0x94A3B8);
                Input = Hex(0xFFFFFF);
                Link = Hex(0x1D4ED8);
                LogBack = Hex(0xF8FAFC);
                RiskLowFg = Hex(0x15803D); RiskLowBg = Hex(0xDCFCE7);
                RiskMidFg = Hex(0xA16207); RiskMidBg = Hex(0xFEF3C7);
                RiskHighFg = Hex(0xB91C1C); RiskHighBg = Hex(0xFEE2E2);
                TagFg = Hex(0x334155); TagBg = Hex(0xF1F5F9);
                InfoFg = Hex(0x1D4ED8); InfoBg = Hex(0xDBEAFE);
                AppliedBg = Hex(0xF0FDF4); AppliedHover = Hex(0xE7F9EE); AppliedBorder = Hex(0xBBF7D0);
                PartialBg = Hex(0xFFFBEB); PartialHover = Hex(0xFEF3C7); PartialBorder = Hex(0xFDE68A);
            }
        }

        // ---- preferências do usuário (HKCU\Software\DebloatWin) ----
        const string PrefKey = @"Software\" + AppInfo.DataFolderName;

        public static ThemeMode LoadMode()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(PrefKey))
                {
                    string v = k == null ? null : k.GetValue("Tema") as string;
                    if (v == "claro") return ThemeMode.Claro;
                    if (v == "sistema") return ThemeMode.Sistema;
                }
            }
            catch { }
            return ThemeMode.Escuro; // escuro é o tema principal
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

        /// <summary>Lê uma preferência DWORD (padrão quando não existe).</summary>
        public static bool GetPref(string name, bool def)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(PrefKey))
                {
                    object v = k == null ? null : k.GetValue(name);
                    if (v is int) return (int)v != 0;
                }
            }
            catch { }
            return def;
        }

        public static void SetPref(string name, bool value)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(PrefKey))
                    k.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
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
                case ThemeMode.Escuro: return "Escuro";
                case ThemeMode.Claro: return "Claro";
                default: return "Seguir o Windows";
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
        public static float SF(float px) { return px * scale; }

        // ---- fontes: Segoe UI Variable (Windows 11), Segoe UI (Windows 10) ----
        static string family, mono;
        static bool Exists(string name)
        {
            try { using (Font f = new Font(name, 10f)) return string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
        static string Family
        {
            get
            {
                if (family == null)
                {
                    family = FontFamily.GenericSansSerif.Name;
                    foreach (string n in new string[] { "Segoe UI Variable Text", "Segoe UI", "Inter" })
                        if (Exists(n)) { family = n; break; }
                }
                return family;
            }
        }
        static string MonoFamily
        {
            get
            {
                if (mono == null)
                {
                    mono = FontFamily.GenericMonospace.Name;
                    foreach (string n in new string[] { "Cascadia Mono", "Consolas", "DejaVu Sans Mono" })
                        if (Exists(n)) { mono = n; break; }
                }
                return mono;
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
        public static readonly Font Caption = Font(7.5f, FontStyle.Bold);
        public static readonly Font Title = Font(18f, FontStyle.Bold);
        public static readonly Font Subtitle = Font(11.25f, FontStyle.Bold);
        public static readonly Font Value = Font(15f, FontStyle.Bold);
        public static readonly Font Huge = Font(21f, FontStyle.Bold);
        public static readonly Font Mono = new Font(MonoFamily, 9f, FontStyle.Regular, GraphicsUnit.Point);

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
            d = Math.Min(d, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Rounded(r, radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border != Color.Empty) using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
            g.SmoothingMode = old;
        }

        /// <summary>Etiqueta arredondada (formato pílula). Devolve a largura usada.</summary>
        public static int DrawPill(Graphics g, string text, Font font, Color fg, Color bg, int x, int y)
        {
            Size ts = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
            int padX = S(9), padY = S(3);
            Rectangle r = new Rectangle(x, y, ts.Width + padX * 2, ts.Height + padY * 2);
            FillRounded(g, r, r.Height / 2, bg, Color.Empty);
            TextRenderer.DrawText(g, text, font, new Point(x + padX, y + padY), fg, TextFormatFlags.NoPadding);
            return r.Width;
        }

        public static int PillWidth(Graphics g, string text, Font font)
        {
            return TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding).Width + S(18);
        }

        public static int PillHeight(Graphics g, Font font)
        {
            return TextRenderer.MeasureText(g, "Ag", font, Size.Empty, TextFormatFlags.NoPadding).Height + S(6);
        }

        /// <summary>Barra de progresso arredondada. <paramref name="value"/> de 0 a 1; negativo = sem porcentagem (faixa em movimento).</summary>
        public static void DrawBar(Graphics g, Rectangle r, float value, Color fill, float phase)
        {
            FillRounded(g, r, r.Height / 2, Border, Color.Empty);
            if (value < 0)
            {
                int w = Math.Max(r.Height, r.Width * 28 / 100);
                int x = r.X + (int)((r.Width + w) * (phase - Math.Floor(phase))) - w;
                Rectangle seg = Rectangle.Intersect(new Rectangle(x, r.Y, w, r.Height), r);
                if (seg.Width > 0) FillRounded(g, seg, r.Height / 2, fill, Color.Empty);
                return;
            }
            int fw = (int)Math.Round(r.Width * Math.Max(0f, Math.Min(1f, value)));
            if (fw > 0) FillRounded(g, new Rectangle(r.X, r.Y, Math.Max(fw, r.Height), r.Height), r.Height / 2, fill, Color.Empty);
        }

        public static int CheckSize { get { return S(20); } }

        public static void DrawCheck(Graphics g, Rectangle r, bool isChecked, bool enabled, bool hover)
        {
            DrawCheck(g, r, isChecked, enabled, hover, CheckTone.Normal);
        }

        /// <summary>Caixa de seleção própria (fica igual nos dois temas).</summary>
        public static void DrawCheck(Graphics g, Rectangle r, bool isChecked, bool enabled, bool hover, CheckTone tone)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int rad = S(6);
            if (isChecked)
            {
                using (GraphicsPath p = Rounded(r, rad))
                using (SolidBrush b = new SolidBrush(enabled ? (hover ? AccentHover : Accent) : TagBg))
                    g.FillPath(b, p);
                Icons.Draw(g, "check", Rectangle.Inflate(r, -S(3), -S(3)), enabled ? Color.White : Disabled, 3f);
            }
            else
            {
                Color border = !enabled ? Border : hover ? Bright : (Dark ? Hex(0x475569) : Hex(0x94A3B8));
                Rectangle rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
                using (GraphicsPath p = Rounded(rr, rad))
                {
                    using (SolidBrush b = new SolidBrush(enabled ? Input : TagBg)) g.FillPath(b, p);
                    using (Pen pen = new Pen(border, Math.Max(1.5f, SF(1.5f)))) g.DrawPath(pen, p);
                }
            }
            g.SmoothingMode = old;
        }

        /// <summary>Anel de foco (navegação pelo teclado).</summary>
        public static void DrawFocus(Graphics g, Rectangle r, int radius)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Rounded(r, radius))
            using (Pen pen = new Pen(Dark ? Hex(0x93C5FD) : Accent, SF(2f)))
                g.DrawPath(pen, p);
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
        public static int Build
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

        /// <summary>Barra de título escura (Windows 10 1809+ e Windows 11) e, no 11, cantos e cor da moldura.</summary>
        public static void TitleBar(Form f, bool dark)
        {
            if (!IsWindows || !f.IsHandleCreated || Build < 17763) return;
            try
            {
                int v = dark ? 1 : 0;
                int attr = Build >= 18985 ? 20 : 19;
                DwmSetWindowAttribute(f.Handle, attr, ref v, 4);
                if (Build >= 22000)
                {
                    // DWMWA_CAPTION_COLOR (35) e DWMWA_BORDER_COLOR (34): barra de título na cor da barra lateral
                    Color c = Theme.Surface;
                    int bgr = c.R | (c.G << 8) | (c.B << 16);
                    DwmSetWindowAttribute(f.Handle, 35, ref bgr, 4);
                    Color b = Theme.Border;
                    int bb = b.R | (b.G << 8) | (b.B << 16);
                    DwmSetWindowAttribute(f.Handle, 34, ref bb, 4);
                }
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
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.TitleBar(this, Theme.Dark);
        }
    }
}
