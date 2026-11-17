// Componentes padronizados: botões, seleção, switch, chips de filtro, busca, aviso (toast) e confirmação.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public enum ButtonKind
    {
        Primary,
        Secondary,
        Ghost,
        Danger
    }

    /// <summary>Botão arredondado com estados de hover, clique, foco (teclado) e desabilitado.</summary>
    public class FlatButton : Button
    {
        public ButtonKind Kind;
        public string IconName;
        /// <summary>Cor do que está atrás do botão (quando ele fica sobre um cartão).</summary>
        public Color SurfaceColor = Color.Empty;
        bool hover, pressed;

        public FlatButton(string text, bool primary) : this(text, primary ? ButtonKind.Primary : ButtonKind.Secondary, null) { }

        public FlatButton(string text, ButtonKind kind, string icon)
        {
            Text = text;
            Kind = kind;
            IconName = icon;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            Height = Theme.S(38);
            AutoSize = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            FitWidth();
        }

        public bool Primary
        {
            get { return Kind == ButtonKind.Primary; }
            set { Kind = value ? ButtonKind.Primary : ButtonKind.Secondary; Invalidate(); }
        }

        /// <summary>Ajusta a largura ao texto e ao ícone.</summary>
        public void FitWidth()
        {
            int w = (Text.Length > 0 ? TextRenderer.MeasureText(Text, Font).Width : 0) + Theme.S(28);
            if (IconName != null) w += Theme.S(Text.Length > 0 ? 24 : 12);
            Width = Math.Max(Text.Length > 0 ? Theme.S(88) : Height, w);
        }

        public void SetText(string text) { Text = text; FitWidth(); Invalidate(); }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color parent = !SurfaceColor.IsEmpty ? SurfaceColor : Parent != null ? Parent.BackColor : Theme.Back;
            g.Clear(parent);
            Color bg, fg, border;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    bg = pressed ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent; fg = Color.White; border = bg; break;
                case ButtonKind.Ghost:
                    bg = pressed ? Theme.Border : hover ? Theme.TagBg : parent; fg = Theme.Text; border = bg; break;
                case ButtonKind.Danger:
                    bg = pressed ? Theme.Blend(Theme.Danger, 0.28f, Theme.Card) : hover ? Theme.Blend(Theme.Danger, 0.2f, Theme.Card) : Theme.RiskHighBg;
                    fg = Theme.RiskHighFg; border = Theme.Blend(Theme.Danger, hover ? 0.5f : 0.35f, Theme.Card); break;
                default:
                    bg = pressed ? Theme.Blend(Theme.Bright, 0.05f, Theme.Raised) : hover ? Theme.Blend(Theme.Bright, 0.10f, Theme.Raised) : Theme.Raised;
                    fg = Theme.Text; border = hover ? Theme.Blend(Theme.Text, 0.18f, Theme.Raised) : Theme.Border; break;
            }
            if (!Enabled)
            {
                bg = Theme.Blend(bg, 0.45f, parent); fg = Theme.Blend(fg, 0.45f, parent); border = Theme.Blend(border, 0.45f, parent);
            }
            Rectangle r = new Rectangle(0, pressed ? 1 : 0, Width - 1, Height - 2);
            using (GraphicsPath p = Theme.Rounded(r, Theme.S(8)))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
            int iconSize = Theme.S(16);
            Size ts = Text.Length > 0 ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding) : Size.Empty;
            int total = ts.Width + (IconName != null ? iconSize + (Text.Length > 0 ? Theme.S(8) : 0) : 0);
            int x = (Width - total) / 2;
            if (IconName != null)
            {
                Icons.Draw(g, IconName, new Rectangle(x, r.Y + (r.Height - iconSize) / 2, iconSize, iconSize), fg, 2.2f);
                x += iconSize + Theme.S(8);
            }
            if (Text.Length > 0)
                TextRenderer.DrawText(g, Text, Font, new Point(x, r.Y + (r.Height - ts.Height) / 2), fg, TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues)
                Theme.DrawFocus(g, Rectangle.Inflate(r, -Theme.S(2), -Theme.S(2)), Theme.S(7));
        }
    }

    /// <summary>Caixa de seleção com texto, desenhada à mão.</summary>
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
            Size = new Size(Theme.CheckSize + Theme.S(10) + sz.Width + Theme.S(4), Math.Max(Theme.CheckSize, sz.Height) + Theme.S(8));
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
            g.Clear(BackColor);
            int cs = Theme.CheckSize;
            Rectangle box = new Rectangle(0, (Height - cs) / 2, cs, cs);
            Theme.DrawCheck(g, box, isChecked, Enabled, hover);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(cs + Theme.S(10), 0, Width - cs - Theme.S(10), Height),
                Enabled ? Theme.Text : Theme.Disabled, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) Theme.DrawFocus(g, Rectangle.Inflate(box, Theme.S(2), Theme.S(2)), Theme.S(7));
        }
    }

    /// <summary>Chave liga/desliga com texto e descrição opcional.</summary>
    public class ToggleSwitch : Control
    {
        bool isChecked, hover;
        public string Description;
        public event EventHandler CheckedChanged;

        public ToggleSwitch(string text, string description)
        {
            Text = text;
            Description = description;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.CheckButton;
            AccessibleName = text;
            Height = Theme.S(description == null ? 36 : 54);
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            int tw = Theme.S(42), th = Theme.S(23);
            Rectangle track = new Rectangle(Width - tw - Theme.S(2), (Height - th) / 2, tw, th);
            Color tc = isChecked ? (hover ? Theme.AccentHover : Theme.Accent) : (hover ? Theme.Blend(Theme.Text, 0.30f, Theme.Card) : Theme.Blend(Theme.Text, 0.22f, Theme.Card));
            Theme.FillRounded(g, track, th / 2, tc, Color.Empty);
            int k = th - Theme.S(6);
            int kx = isChecked ? track.Right - k - Theme.S(3) : track.X + Theme.S(3);
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, kx, track.Y + Theme.S(3), k, k);
            if (Focused && ShowFocusCues) Theme.DrawFocus(g, Rectangle.Inflate(track, Theme.S(3), Theme.S(3)), th / 2 + Theme.S(3));
            int textW = track.X - Theme.S(16);
            if (Description == null)
                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, textW, Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            else
            {
                TextRenderer.DrawText(g, Text, Font, new Rectangle(0, Theme.S(6), textW, Theme.S(22)), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, Description, Theme.Small, new Rectangle(0, Theme.S(28), textW, Height - Theme.S(28)), Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>Chip de filtro (pílula clicável que fica marcada).</summary>
    public class FilterChip : Control
    {
        bool selected, hover;
        public object Key;

        public FilterChip(string text, object key)
        {
            Key = key;
            Font = Theme.SmallBold;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            AccessibleRole = AccessibleRole.CheckButton;
            Height = Theme.S(32);
            SetText(text);
        }

        public void SetText(string text)
        {
            Text = text;
            AccessibleName = text;
            Width = TextRenderer.MeasureText(text, Font).Width + Theme.S(24);
            Invalidate();
        }

        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color parent = BackColor;
            g.Clear(parent);
            Color bg = selected ? Theme.Blend(Theme.Bright, 0.16f, parent) : hover ? Theme.RowHover : Theme.Card;
            Color border = selected ? Theme.Blend(Theme.Bright, 0.5f, parent) : Theme.Border;
            Color fg = selected ? (Theme.Dark ? Color.FromArgb(0xBF, 0xDB, 0xFE) : Theme.Link) : Theme.TagFg;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, r.Height / 2, bg, border);
            TextRenderer.DrawText(g, Text, Font, r, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) Theme.DrawFocus(g, Rectangle.Inflate(r, -Theme.S(2), -Theme.S(2)), r.Height / 2);
        }
    }

    /// <summary>Campo de busca com ícone, borda arredondada e destaque no foco.</summary>
    public class SearchField : Panel
    {
        public readonly TextBox Box;

        public SearchField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = Theme.S(40);
            Padding = new Padding(Theme.S(38), Theme.S(11), Theme.S(12), Theme.S(4));
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
            g.Clear(BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool f = Box.Focused;
            Theme.FillRounded(g, r, Theme.S(8), Theme.Input, f ? Theme.Bright : Theme.Border);
            Icons.Draw(g, "search", new Rectangle(Theme.S(12), (Height - Theme.S(16)) / 2, Theme.S(16), Theme.S(16)), Theme.Muted, 2f);
        }
    }

    /// <summary>Aviso rápido no canto da janela, que some sozinho.</summary>
    public class Toast : Control
    {
        string title, message, icon;
        Color tone;
        readonly Timer timer = new Timer();

        public Toast()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Visible = false;
            Width = Theme.S(380);
            Height = Theme.S(70);
            timer.Tick += delegate { timer.Stop(); Visible = false; };
            Cursor = Cursors.Hand;
            Click += delegate { timer.Stop(); Visible = false; };
        }

        /// <summary>kind: ok, info, warn, err.</summary>
        public void Show(string kind, string title, string message, int ms)
        {
            this.title = title; this.message = message;
            icon = kind == "ok" ? "check" : kind == "err" ? "x" : kind == "warn" ? "alert" : "info";
            tone = kind == "ok" ? Theme.Success : kind == "err" ? Theme.Danger : kind == "warn" ? Theme.Warning : Theme.Bright;
            using (Graphics g = CreateGraphics())
            {
                int tw = Width - Theme.S(76);
                int h = TextRenderer.MeasureText(g, message ?? "", Theme.Small, new Size(tw, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
                Height = Theme.S(42) + h;
            }
            Visible = true;
            BringToFront();
            Invalidate();
            timer.Stop();
            timer.Interval = ms;
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, r, Theme.S(12), Theme.Raised, Theme.Border);
            Rectangle tile = new Rectangle(Theme.S(14), Theme.S(14), Theme.S(30), Theme.S(30));
            Theme.FillRounded(g, tile, Theme.S(8), Theme.Blend(tone, 0.16f, Theme.Raised), Color.Empty);
            Icons.Draw(g, icon, Rectangle.Inflate(tile, -Theme.S(7), -Theme.S(7)), tone, 2.6f);
            int x = tile.Right + Theme.S(12), tw = Width - x - Theme.S(14);
            TextRenderer.DrawText(g, title ?? "", Theme.BodyBold, new Rectangle(x, Theme.S(12), tw, Theme.S(22)), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, message ?? "", Theme.Small, new Rectangle(x, Theme.S(34), tw, Height - Theme.S(36)), Theme.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Janela de confirmação no estilo do app (substitui a caixa de mensagem do Windows).</summary>
    public class ConfirmDialog : ThemedForm
    {
        public ConfirmDialog(string title, string body, string details, string okText, string icon, bool danger)
        {
            Text = AppInfo.Name;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false; ShowIcon = false;
            BackColor = Theme.Raised;
            int w = Theme.S(560);
            Color tone = danger ? Theme.Warning : Theme.Bright;

            Panel head = new Panel();
            head.SetBounds(0, 0, w, Theme.S(70));
            head.Paint += delegate(object s, PaintEventArgs e)
            {
                Rectangle tile = new Rectangle(Theme.S(24), Theme.S(22), Theme.S(36), Theme.S(36));
                Theme.FillRounded(e.Graphics, tile, Theme.S(10), Theme.Blend(tone, 0.16f, Theme.Raised), Color.Empty);
                Icons.Draw(e.Graphics, icon, Rectangle.Inflate(tile, -Theme.S(8), -Theme.S(8)), tone, 2.2f);
                TextRenderer.DrawText(e.Graphics, title, Theme.Subtitle, new Rectangle(Theme.S(72), Theme.S(22), w - Theme.S(96), Theme.S(36)),
                    Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            };
            Controls.Add(head);

            int y = Theme.S(76);
            Label lb = new Label();
            lb.Text = body;
            lb.ForeColor = Theme.Text;
            lb.Font = Theme.Body;
            lb.AutoSize = false;
            int bh = TextRenderer.MeasureText(body, Theme.Body, new Size(w - Theme.S(48), int.MaxValue), TextFormatFlags.WordBreak).Height;
            lb.SetBounds(Theme.S(24), y, w - Theme.S(48), bh + Theme.S(4));
            Controls.Add(lb);
            y += lb.Height + Theme.S(12);

            if (!string.IsNullOrEmpty(details))
            {
                TextBox box = new TextBox();
                box.Multiline = true; box.ReadOnly = true; box.ScrollBars = ScrollBars.Vertical;
                box.BorderStyle = BorderStyle.None; box.BackColor = Theme.Card; box.ForeColor = Theme.Muted; box.Font = Theme.Small;
                box.Text = details.Replace("\r\n", "\n").Replace("\n", Environment.NewLine).TrimEnd();
                box.TabStop = false;
                int lines = box.Text.Split('\n').Length;
                int lh = TextRenderer.MeasureText("Ag", Theme.Small).Height;
                box.SetBounds(Theme.S(24), y, w - Theme.S(48), Math.Min(Theme.S(170), lh * lines + Theme.S(8)));
                Shown += delegate { box.SelectionStart = 0; box.SelectionLength = 0; };
                Native.Track(box);
                Controls.Add(box);
                y += box.Height + Theme.S(12);
            }

            FlatButton ok = new FlatButton(okText, ButtonKind.Primary, "play");
            FlatButton cancel = new FlatButton("Cancelar", ButtonKind.Secondary, null);
            ok.DialogResult = DialogResult.OK;
            cancel.DialogResult = DialogResult.Cancel;
            y += Theme.S(6);
            ok.Location = new Point(w - Theme.S(24) - ok.Width, y);
            cancel.Location = new Point(ok.Left - Theme.S(10) - cancel.Width, y);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(w, y + ok.Height + Theme.S(22));
        }

        public static bool Ask(IWin32Window owner, string title, string body, string details, string okText, string icon, bool danger)
        {
            using (ConfirmDialog d = new ConfirmDialog(title, body, details, okText, icon, danger))
                return d.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
