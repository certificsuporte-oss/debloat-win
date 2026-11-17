// Cartão de um ajuste na lista, item do menu lateral e título de grupo (desenhados à mão).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public class TweakRow : Control
    {
        public readonly Tweak Tweak;
        public readonly bool Applicable;
        readonly string notApplicableReason;
        readonly string iconName;
        bool isChecked;
        bool hover;
        Rectangle detailsRect;
        int descHeight, warnHeight, titleHeight, pillHeight;

        TweakStatus status;

        public event EventHandler CheckedChanged;
        public event EventHandler DetailsRequested;

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

        public TweakRow(Tweak tweak, bool applicable, string notApplicableReason)
        {
            Tweak = tweak;
            Applicable = applicable;
            this.notApplicableReason = notApplicableReason;
            iconName = IconFor(tweak);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = Theme.Body;
            Cursor = applicable ? Cursors.Hand : Cursors.Default;
            Margin = new Padding(0, 0, Theme.S(12), Theme.S(12));
            TabStop = true;
            AccessibleName = tweak.Title;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public static bool ChangesServices(Tweak t)
        {
            return t.Actions.Exists(delegate(TweakAction a) { return a is ServiceStartAction; });
        }

        public static string IconFor(Tweak t)
        {
            if (ChangesServices(t)) return "services";
            switch (t.Category)
            {
                case Category.Apps: return t.Title.StartsWith("Remover") ? "debloat" : "shield";
                case Category.Privacidade: return "shield";
                case Category.Telemetria: return "info";
                case Category.Atualizacoes: return "refresh";
                case Category.Desempenho: return "optimize";
                case Category.Jogos: return "play";
                case Category.Interface: return "dashboard";
                case Category.Ferramentas: return "clean";
                case Category.Programas: return "apps";
                case Category.Finalizar: return "download";
            }
            return "settings";
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

        int Pad { get { return Theme.S(16); } }
        int TextX { get { return Theme.S(104); } }

        /// <summary>Calcula a altura para a largura dada. <paramref name="minHeight"/> iguala cartões lado a lado.</summary>
        public int MeasureHeight(int width)
        {
            int textW = Math.Max(Theme.S(120), width - TextX - Pad);
            TextFormatFlags wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            titleHeight = TextRenderer.MeasureText(Tweak.Title, Theme.BodyBold, new Size(textW, int.MaxValue), wrap).Height;
            descHeight = TextRenderer.MeasureText(Tweak.Description, Theme.Body, new Size(textW, int.MaxValue), wrap).Height;
            warnHeight = Tweak.Warning != null
                ? TextRenderer.MeasureText(Tweak.Warning, Theme.Small, new Size(textW - Theme.S(20), int.MaxValue), wrap).Height
                : 0;
            using (Graphics g = CreateGraphics()) pillHeight = Theme.PillHeight(g, Theme.SmallBold);
            int h = Pad + titleHeight + Theme.S(4) + descHeight;
            if (warnHeight > 0) h += Theme.S(8) + warnHeight;
            h += Theme.S(10) + pillHeight + Pad;
            return Math.Max(h, Theme.S(76));
        }

        public void LayoutForWidth(int width) { LayoutForWidth(width, 0); }

        public void LayoutForWidth(int width, int minHeight)
        {
            int h = MeasureHeight(width);
            Size = new Size(width, Math.Max(h, minHeight));
        }

        List<KeyValuePair<string, int>> Tags()
        {
            // 0 = neutro, 1 = vermelho, 2 = amarelo
            List<KeyValuePair<string, int>> tags = new List<KeyValuePair<string, int>>();
            if (!Applicable) tags.Add(new KeyValuePair<string, int>(notApplicableReason, 0));
            if (!Tweak.IsReversible) tags.Add(new KeyValuePair<string, int>("Sem desfazer", 1));
            if (Tweak.Risk == Risk.Medio) tags.Add(new KeyValuePair<string, int>("Risco médio", 2));
            if (Tweak.Risk == Risk.Alto) tags.Add(new KeyValuePair<string, int>("Risco alto", 1));
            if (Tweak.NeedsRestart) tags.Add(new KeyValuePair<string, int>("Pede reinício", 0));
            if (Tweak.ProOnly && Applicable) tags.Add(new KeyValuePair<string, int>("Pro/Enterprise", 0));
            return tags;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = isChecked ? Theme.Selected : hover && Applicable ? Theme.RowHover : Theme.Card;
            Color border = isChecked ? Theme.SelectedBorder : Theme.Border;
            if (Applicable && !isChecked && status != null)
            {
                if (status.State == TweakState.Applied) { bg = hover ? Theme.AppliedHover : Theme.AppliedBg; border = Theme.AppliedBorder; }
                else if (status.State == TweakState.Partial) { bg = hover ? Theme.PartialHover : Theme.PartialBg; border = Theme.PartialBorder; }
            }
            g.Clear(BackColor);
            Rectangle card = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, card, Theme.S(12), bg, border);

            int cs = Theme.CheckSize;
            Rectangle box = new Rectangle(Pad, Pad + Theme.S(1), cs, cs);
            Theme.DrawCheck(g, box, isChecked, Applicable, hover);

            Rectangle tile = new Rectangle(Theme.S(50), Pad - Theme.S(2), Theme.S(38), Theme.S(38));
            Theme.FillRounded(g, tile, Theme.S(10), Theme.Blend(Theme.Muted, 0.10f, bg), Color.Empty);
            Icons.Draw(g, iconName, Rectangle.Inflate(tile, -Theme.S(10), -Theme.S(10)), Applicable ? Theme.TagFg : Theme.Disabled, 2f);

            Color title = Applicable ? Theme.Text : Theme.Disabled;
            Color muted = Applicable ? Theme.Muted : Theme.Disabled;
            int textW = Width - TextX - Pad;
            TextFormatFlags wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            int y = Pad;
            TextRenderer.DrawText(g, Tweak.Title, Theme.BodyBold, new Rectangle(TextX, y, textW, titleHeight), title, wrap);
            y += titleHeight + Theme.S(4);
            TextRenderer.DrawText(g, Tweak.Description, Theme.Body, new Rectangle(TextX, y, textW, descHeight), muted, wrap);
            y += descHeight;
            if (warnHeight > 0)
            {
                y += Theme.S(8);
                Icons.Draw(g, "alert", new Rectangle(TextX, y, Theme.S(14), Theme.S(14)), Applicable ? Theme.RiskMidFg : Theme.Disabled, 2.2f);
                TextRenderer.DrawText(g, Tweak.Warning, Theme.Small, new Rectangle(TextX + Theme.S(20), y, textW - Theme.S(20), warnHeight),
                    Applicable ? Theme.RiskMidFg : Theme.Disabled, wrap);
                y += warnHeight;
            }
            y += Theme.S(10);
            y = Math.Max(y, Height - Pad - pillHeight);

            int x = TextX;
            string stLabel = Applicable && status != null ? status.Label : null;
            if (stLabel != null && status.State == TweakState.Applied && Tweak.Actions.Exists(delegate(TweakAction a) { return a is WingetInstallAction; })) stLabel = "✓ Já instalado";
            else if (stLabel != null && status.State == TweakState.Applied && Tweak.Id == "fim.driverssdio") stLabel = "✓ Nenhum sem driver";
            if (stLabel == null && Applicable && status != null && status.State == TweakState.NotApplied) stLabel = "Pendente";
            if (stLabel != null)
            {
                Color sf = Theme.TagFg, sb = Theme.TagBg;
                if (status.State == TweakState.Applied) { sf = Theme.RiskLowFg; sb = Theme.RiskLowBg; }
                else if (status.State == TweakState.Partial) { sf = Theme.RiskMidFg; sb = Theme.RiskMidBg; }
                else if (status.State == TweakState.Checking) { sf = Theme.InfoFg; sb = Theme.InfoBg; }
                x += Theme.DrawPill(g, stLabel, Theme.SmallBold, sf, sb, x, y) + Theme.S(6);
            }
            foreach (KeyValuePair<string, int> tag in Tags())
            {
                Color fg = tag.Value == 1 ? Theme.RiskHighFg : tag.Value == 2 ? Theme.RiskMidFg : Theme.TagFg;
                Color tb = tag.Value == 1 ? Theme.RiskHighBg : tag.Value == 2 ? Theme.RiskMidBg : Theme.TagBg;
                if (!Applicable) { fg = Theme.Disabled; tb = Theme.TagBg; }
                int pw = Theme.PillWidth(g, tag.Key, Theme.SmallBold);
                if (x + pw > Width - Pad - Theme.S(70)) break;
                x += Theme.DrawPill(g, tag.Key, Theme.SmallBold, fg, tb, x, y) + Theme.S(6);
            }

            string link = "Detalhes";
            Size ls = TextRenderer.MeasureText(g, link, Theme.Small, Size.Empty, TextFormatFlags.NoPadding);
            detailsRect = new Rectangle(Width - Pad - ls.Width, y + (pillHeight - ls.Height) / 2, ls.Width, ls.Height);
            TextRenderer.DrawText(g, link, Theme.Small, detailsRect.Location, Theme.Link, TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
                Theme.DrawFocus(g, Rectangle.Inflate(card, -Theme.S(2), -Theme.S(2)), Theme.S(11));
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

    /// <summary>Título de grupo na lista (maiúsculas discretas).</summary>
    public class GroupHeader : Control
    {
        public GroupHeader(string text)
        {
            Text = text;
            Font = Theme.Caption;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Height = Theme.S(34);
            Margin = new Padding(0, Theme.S(4), 0, Theme.S(2));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), Font, new Rectangle(Theme.S(2), 0, Width, Height - Theme.S(6)), Theme.Muted,
                TextFormatFlags.Bottom | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Item do menu lateral: ícone, texto e contador; o selecionado ganha destaque azul.</summary>
    public class NavItem : Control
    {
        public object Key;
        public string Count = "";
        public string IconName;
        bool selected, hover;

        public NavItem(string text, object key, string icon)
        {
            Text = text; Key = key; IconName = icon;
            Font = Theme.Body;
            Height = Theme.S(40);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = text;
            AccessibleRole = AccessibleRole.Link;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Rectangle r = new Rectangle(Theme.S(12), 0, Width - Theme.S(24), Height - 1);
            if (selected || hover)
                Theme.FillRounded(g, r, Theme.S(8), selected ? Theme.AccentSoft : Theme.Blend(Theme.Text, 0.05f, Theme.Surface), Color.Empty);
            if (selected)
                Theme.FillRounded(g, new Rectangle(r.X, r.Y + Theme.S(10), Theme.S(3), r.Height - Theme.S(20)), Theme.S(1), Theme.Bright, Color.Empty);
            int isz = Theme.S(18);
            Icons.Draw(g, IconName, new Rectangle(r.X + Theme.S(14), r.Y + (r.Height - isz) / 2, isz, isz), selected ? Theme.Bright : Theme.Muted, 2f);
            Color fg = selected ? Theme.Text : Theme.Dark ? Color.FromArgb(0xCB, 0xD5, 0xE1) : Theme.Text;
            TextRenderer.DrawText(g, Text, selected ? Theme.BodyBold : Theme.Body,
                new Rectangle(r.X + Theme.S(44), r.Y, r.Width - Theme.S(90), r.Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Count, Theme.Small, new Rectangle(r.Right - Theme.S(56), r.Y, Theme.S(46), r.Height),
                Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) Theme.DrawFocus(g, Rectangle.Inflate(r, -Theme.S(1), -Theme.S(1)), Theme.S(7));
        }
    }
}
