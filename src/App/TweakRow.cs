// Linha da lista de ajustes, desenhada à mão (mais leve que vários controles por linha).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public class TweakRow : Control
    {
        public readonly Tweak Tweak;
        public readonly bool Applicable;
        readonly string notApplicableReason;
        bool isChecked;
        bool hover;
        Rectangle detailsRect;
        int descHeight, warnHeight, titleHeight, pillHeight;

        TweakStatus status;

        public event EventHandler CheckedChanged;

        /// <summary>Situação deste ajuste no PC (já aplicado, parcial…).</summary>
        public TweakStatus Status
        {
            get { return status; }
            set { status = value; Invalidate(); }
        }

        public bool AlreadyApplied
        {
            get { return status != null && (status.State == TweakState.Applied || status.State == TweakState.NotApplicable); }
        }
        public event EventHandler DetailsRequested;

        public TweakRow(Tweak tweak, bool applicable, string notApplicableReason)
        {
            Tweak = tweak;
            Applicable = applicable;
            this.notApplicableReason = notApplicableReason;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Theme.Surface;
            Font = Theme.Body;
            Cursor = applicable ? Cursors.Hand : Cursors.Default;
            Margin = new Padding(0, 0, 0, Theme.S(6));
            TabStop = true;
            AccessibleName = tweak.Title;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                bool v = value && Applicable;
                if (v == isChecked) return;
                isChecked = v;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        int PadX { get { return Theme.S(16); } }
        int PadY { get { return Theme.S(12); } }
        int TextX { get { return Theme.S(48); } }

        /// <summary>Calcula a altura para a largura dada.</summary>
        public void LayoutForWidth(int width)
        {
            int textW = Math.Max(Theme.S(120), width - TextX - PadX);
            TextFormatFlags wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            titleHeight = TextRenderer.MeasureText(Tweak.Title, Theme.BodyBold, new Size(textW - Theme.S(110), int.MaxValue), wrap).Height;
            descHeight = TextRenderer.MeasureText(Tweak.Description, Theme.Body, new Size(textW, int.MaxValue), wrap).Height;
            warnHeight = Tweak.Warning != null
                ? TextRenderer.MeasureText("⚠ " + Tweak.Warning, Theme.Small, new Size(textW, int.MaxValue), wrap).Height
                : 0;
            using (Graphics g = CreateGraphics()) pillHeight = Theme.PillHeight(g, Theme.Small);
            int h = PadY + titleHeight + Theme.S(4) + descHeight;
            if (warnHeight > 0) h += Theme.S(6) + warnHeight;
            h += Theme.S(8) + pillHeight + PadY;
            Size = new Size(width, h);
        }

        List<string> Tags()
        {
            List<string> tags = new List<string>();
            if (!Applicable) tags.Add(notApplicableReason);
            else
            {
                if (Tweak.Windows == WinVersion.Win11) tags.Add("Windows 11");
                if (Tweak.Windows == WinVersion.Win10) tags.Add("Windows 10");
            }
            if (Tweak.ProOnly && Applicable) tags.Add("Pro/Enterprise");
            if (!Tweak.IsReversible) tags.Add("Sem desfazer");
            if (Tweak.NeedsRestart) tags.Add("Pede reinício");
            if (Tweak.Preset == 1) tags.Add("Básico");
            else if (Tweak.Preset == 2) tags.Add("Rígido");
            else if (Tweak.Preset == 3) tags.Add("Extremo");
            return tags;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color bg = !Applicable ? Theme.Surface : isChecked ? Theme.Selected : hover ? Theme.RowHover : Theme.Surface;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            Rectangle card = new Rectangle(0, 0, Width - 1, Height - 1);
            using (System.Drawing.Drawing2D.GraphicsPath p = Theme.Rounded(card, Theme.S(8)))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                using (Pen pen = new Pen(isChecked ? Theme.Accent : Theme.Border, isChecked ? 1.5f : 1f)) g.DrawPath(pen, p);
            }

            // caixa de seleção (desenhada à mão: fica certa no tema escuro)
            int cs = Theme.CheckSize;
            Theme.DrawCheck(g, new Rectangle(PadX, PadY + Math.Max(0, (titleHeight - cs) / 2), cs, cs), isChecked, Applicable, hover);

            Color title = Applicable ? Theme.Text : Theme.Disabled;
            Color muted = Applicable ? Theme.Muted : Theme.Disabled;
            int textW = Width - TextX - PadX;
            TextFormatFlags wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            int y = PadY;

            // risco no canto direito
            Color rf, rb;
            Theme.RiskColors(Tweak.Risk, out rf, out rb);
            if (!Applicable) { rf = Theme.Disabled; rb = Theme.TagBg; }
            string rt = Theme.RiskText(Tweak.Risk);
            int rw = TextRenderer.MeasureText(g, rt, Theme.SmallBold, Size.Empty, TextFormatFlags.NoPadding).Width + Theme.S(14);
            Theme.DrawPill(g, rt, Theme.SmallBold, rf, rb, Width - PadX - rw, y);

            TextRenderer.DrawText(g, Tweak.Title, Theme.BodyBold, new Rectangle(TextX, y, textW - Theme.S(110), titleHeight), title, wrap);
            y += titleHeight + Theme.S(4);
            TextRenderer.DrawText(g, Tweak.Description, Theme.Body, new Rectangle(TextX, y, textW, descHeight), muted, wrap);
            y += descHeight;
            if (warnHeight > 0)
            {
                y += Theme.S(6);
                TextRenderer.DrawText(g, "⚠ " + Tweak.Warning, Theme.Small, new Rectangle(TextX, y, textW, warnHeight),
                    Applicable ? Theme.RiskMidFg : Theme.Disabled, wrap);
                y += warnHeight;
            }
            y += Theme.S(8);

            int x = TextX;
            string stLabel = Applicable && status != null ? status.Label : null;
            if (stLabel != null)
            {
                Color sf = Theme.TagFg, sb = Theme.TagBg;
                if (status.State == TweakState.Applied) { sf = Theme.RiskLowFg; sb = Theme.RiskLowBg; }
                else if (status.State == TweakState.Partial) { sf = Theme.RiskMidFg; sb = Theme.RiskMidBg; }
                x += Theme.DrawPill(g, stLabel, Theme.SmallBold, sf, sb, x, y) + Theme.S(6);
            }
            foreach (string tag in Tags())
            {
                bool strong = tag == "Sem desfazer";
                x += Theme.DrawPill(g, tag, Theme.Small, strong && Applicable ? Theme.RiskHighFg : Theme.TagFg,
                    strong && Applicable ? Theme.RiskHighBg : Theme.TagBg, x, y) + Theme.S(6);
            }

            string link = "Ver detalhes";
            Size ls = TextRenderer.MeasureText(g, link, Theme.Small, Size.Empty, TextFormatFlags.NoPadding);
            detailsRect = new Rectangle(Width - PadX - ls.Width, y + Theme.S(2), ls.Width, ls.Height);
            using (Font u = new Font(Theme.Small, FontStyle.Underline))
                TextRenderer.DrawText(g, link, u, detailsRect.Location, Theme.Link, TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                Rectangle f = card; f.Inflate(-Theme.S(3), -Theme.S(3));
                ControlPaint.DrawFocusRectangle(g, f);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Cursor = Applicable ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = detailsRect.Contains(e.Location) || Applicable ? Cursors.Hand : Cursors.Default;
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            if (e.Button != MouseButtons.Left) return;
            if (detailsRect.Contains(e.Location))
            {
                if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
                return;
            }
            if (Applicable) Checked = !Checked;
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && Applicable) { Checked = !Checked; e.Handled = true; }
            if (e.KeyCode == Keys.Enter && DetailsRequested != null) { DetailsRequested(this, EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    /// <summary>Título de grupo na lista (usado em "Todos" e na busca).</summary>
    public class GroupHeader : Control
    {
        public GroupHeader(string text)
        {
            Text = text;
            Font = Theme.Subtitle;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Height = Theme.S(40);
            Margin = new Padding(0, Theme.S(4), 0, Theme.S(4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Theme.S(2), 0, Width, Height), Theme.Text,
                TextFormatFlags.Bottom | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Item do menu lateral (categoria) com contador.</summary>
    public class NavItem : Control
    {
        public object Key;
        public string Count = "";
        bool selected, hover;

        public NavItem(string text, object key)
        {
            Text = text; Key = key;
            Font = Theme.Body;
            Height = Theme.S(34);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);
            Rectangle r = new Rectangle(Theme.S(8), 0, Width - Theme.S(16), Height - 1);
            if (selected || hover)
                using (System.Drawing.Drawing2D.GraphicsPath p = Theme.Rounded(r, Theme.S(6)))
                using (SolidBrush b = new SolidBrush(selected ? Theme.AccentSoft : Theme.Back))
                    g.FillPath(b, p);
            Color fg = selected ? Theme.Accent : Theme.Text;
            TextRenderer.DrawText(g, Text, selected ? Theme.BodyBold : Theme.Body,
                new Rectangle(r.X + Theme.S(10), r.Y, r.Width - Theme.S(60), r.Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Count, Theme.Small, new Rectangle(r.Right - Theme.S(60), r.Y, Theme.S(50), r.Height),
                selected ? Theme.Accent : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
        }
    }
}
