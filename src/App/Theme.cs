// Cores, fontes e escala de DPI. Visual neutro: troque as cores aqui para mudar o tema.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public static class Theme
    {
        public static readonly Color Back = Color.FromArgb(245, 246, 248);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(222, 225, 230);
        public static readonly Color Text = Color.FromArgb(31, 35, 40);
        public static readonly Color Muted = Color.FromArgb(101, 109, 118);
        public static readonly Color Accent = Color.FromArgb(47, 111, 237);
        public static readonly Color AccentHover = Color.FromArgb(31, 92, 210);
        public static readonly Color AccentSoft = Color.FromArgb(232, 240, 254);
        public static readonly Color Selected = Color.FromArgb(240, 245, 255);
        public static readonly Color Disabled = Color.FromArgb(170, 175, 182);

        public static readonly Color RiskLowFg = Color.FromArgb(26, 127, 55);
        public static readonly Color RiskLowBg = Color.FromArgb(218, 251, 225);
        public static readonly Color RiskMidFg = Color.FromArgb(154, 103, 0);
        public static readonly Color RiskMidBg = Color.FromArgb(255, 248, 197);
        public static readonly Color RiskHighFg = Color.FromArgb(207, 34, 46);
        public static readonly Color RiskHighBg = Color.FromArgb(255, 235, 233);
        public static readonly Color TagFg = Color.FromArgb(87, 96, 106);
        public static readonly Color TagBg = Color.FromArgb(234, 238, 242);

        static float scale = 1f;
        public static float Scale { get { return scale; } }

        public static void Init(Control c)
        {
            using (Graphics g = c.CreateGraphics())
                scale = Math.Max(1f, g.DpiX / 96f);
        }

        /// <summary>Converte pixels de projeto (96 DPI) para o DPI atual.</summary>
        public static int S(int px) { return (int)Math.Round(px * scale); }

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
}
