// Dashboard: visão geral do sistema, situação por área, preset Padrão, recursos do PC e atividades recentes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DebloatWin.App
{
    public class DashArea
    {
        public string Name, Key;
        public int Applied, Total;
    }

    public class DashActivity
    {
        public string Title, When, Pill;
        public int Kind; // 0 = concluída, 1 = desfeita/neutra, 2 = falha
    }

    public class DashData
    {
        public string Machine = "", Edition = "", Version = "";
        public int Applied, Pending, OtherWindows;
        public bool Checking;
        public int DriverState; // 1 = nenhum sem driver, 0 = há dispositivos sem driver, -1 = verificando
        public List<DashArea> Areas = new List<DashArea>();
        public List<DashActivity> Activities = new List<DashActivity>();
        public SystemSnapshot Stats;
    }

    public class DashboardView : Control
    {
        public DashData Data = new DashData();
        public event Action<string> Navigate;
        public readonly FlatButton ApplyButton, HistoryButton;
        readonly List<KeyValuePair<Rectangle, string>> links = new List<KeyValuePair<Rectangle, string>>();
        string hoverKey;
        static readonly Font SmallUnderline = new Font(Theme.Small, FontStyle.Underline);

        int Pad { get { return Theme.S(32); } }
        int Gap { get { return Theme.S(14); } }

        public DashboardView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Back;
            ApplyButton = new FlatButton("Aplicar Padrão", ButtonKind.Primary, "play");
            HistoryButton = new FlatButton("Histórico", ButtonKind.Secondary, "history");
            Controls.Add(ApplyButton);
            Controls.Add(HistoryButton);
        }

        public void SetData(DashData d)
        {
            Data = d;
            ApplyButton.SetText(d.Pending > 0 ? "Aplicar Padrão · " + d.Pending + " pendentes" : "Padrão aplicado");
            ApplyButton.Enabled = d.Pending > 0;
            Height = ContentHeight();
            PerformLayout();
            Invalidate();
        }

        int ContentHeight()
        {
            int y = Theme.S(26) + Theme.S(66);
            y += Theme.S(124) + Gap;               // cartões de resumo
            y += AreasHeight() + Gap;               // áreas + coluna da direita
            y += ActivitiesHeight() + Pad;
            return y;
        }

        int AreasHeight() { return Theme.S(58) + Math.Max(1, Data.Areas.Count) * Theme.S(30) + Theme.S(12); }
        int ActivitiesHeight() { return Theme.S(58) + Math.Max(1, Math.Min(4, Data.Activities.Count)) * Theme.S(52) + Theme.S(4); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int top = Theme.S(30);
            ApplyButton.Location = new Point(Width - Pad - ApplyButton.Width, top);
            HistoryButton.Location = new Point(ApplyButton.Left - Theme.S(10) - HistoryButton.Width, top);
        }

        void Card(Graphics g, Rectangle r)
        {
            Theme.FillRounded(g, r, Theme.S(12), Theme.Card, Theme.Border);
        }

        void Heading(Graphics g, string text, Rectangle r)
        {
            TextRenderer.DrawText(g, text, Theme.Subtitle, new Rectangle(r.X + Theme.S(20), r.Y + Theme.S(16), r.Width - Theme.S(40), Theme.S(26)),
                Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        void Link(Graphics g, string text, string key, int right, int y)
        {
            Size s = TextRenderer.MeasureText(g, text, Theme.Small, Size.Empty, TextFormatFlags.NoPadding);
            Rectangle r = new Rectangle(right - s.Width, y, s.Width, s.Height);
            TextRenderer.DrawText(g, text, hoverKey == key ? SmallUnderline : Theme.Small, r.Location, Theme.Link, TextFormatFlags.NoPadding);
            links.Add(new KeyValuePair<Rectangle, string>(r, key));
        }

        static string Fmt(double gb) { return gb >= 100 ? gb.ToString("0") : gb.ToString("0.0"); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Back);
            links.Clear();
            int x0 = Pad, w = Width - Pad * 2;
            if (w < Theme.S(300)) return;
            int y = Theme.S(26);

            TextRenderer.DrawText(g, Data.Machine.ToUpperInvariant(), Theme.Caption, new Point(x0, y), Theme.Muted, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Visão geral do sistema", Theme.Title, new Point(x0 - Theme.S(2), y + Theme.S(18)), Theme.Text, TextFormatFlags.NoPadding);
            y += Theme.S(66);

            // ---- cartões de resumo ----
            int cw = (w - Gap * 3) / 4, ch = Theme.S(124);
            string status = Data.Checking ? "Verificando…" : Data.Pending == 0 ? "Tudo aplicado" : Data.Pending + " pendentes";
            string statusSub = Data.Pending == 0 ? "O Padrão já está neste PC" : "Itens do Padrão que faltam";
            Stat(g, new Rectangle(x0, y, cw, ch), "windows", "SISTEMA", Data.Edition, "Edição detectada", null, 0);
            Stat(g, new Rectangle(x0 + (cw + Gap), y, cw, ch), "file", "VERSÃO", Data.Version, "Versão e build do Windows", null, 0);
            Stat(g, new Rectangle(x0 + (cw + Gap) * 2, y, cw, ch), "shield", "STATUS GERAL", status, statusSub,
                Data.Pending == 0 ? "Em dia" : "Atenção", Data.Pending == 0 ? 1 : 2);
            string drv = Data.DriverState == 1 ? "0 sem driver" : Data.DriverState == 0 ? "Faltam drivers" : "Verificando…";
            Stat(g, new Rectangle(x0 + (cw + Gap) * 3, y, cw, ch), "drive", "DRIVERS", drv,
                Data.DriverState == 0 ? "Veja em Ferramentas" : "Gerenciador de Dispositivos",
                Data.DriverState == 1 ? "✓ Completo" : Data.DriverState == 0 ? "Pendente" : null, Data.DriverState == 1 ? 1 : 2);
            y += ch + Gap;

            // ---- situação por área (2/3) + coluna da direita (1/3) ----
            int ah = AreasHeight();
            int leftW = (w - Gap) * 2 / 3, rightW = w - Gap - leftW;
            Rectangle areas = new Rectangle(x0, y, leftW, ah);
            Card(g, areas);
            Heading(g, "Situação por área", areas);
            Link(g, "Ver itens pendentes", "debloat", areas.Right - Theme.S(20), areas.Y + Theme.S(20));
            int ry = areas.Y + Theme.S(54);
            foreach (DashArea a in Data.Areas)
            {
                Rectangle row = new Rectangle(areas.X + Theme.S(12), ry, areas.Width - Theme.S(24), Theme.S(30));
                if (hoverKey == "area:" + a.Key) Theme.FillRounded(g, row, Theme.S(6), Theme.RowHover, Color.Empty);
                links.Add(new KeyValuePair<Rectangle, string>(row, "area:" + a.Key));
                TextRenderer.DrawText(g, a.Name, Theme.Body, new Rectangle(row.X + Theme.S(8), row.Y, Theme.S(170), row.Height),
                    Theme.Dark ? Color.FromArgb(0xCB, 0xD5, 0xE1) : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Rectangle bar = new Rectangle(row.X + Theme.S(188), row.Y + row.Height / 2 - Theme.S(3), row.Width - Theme.S(188) - Theme.S(70), Theme.S(6));
                float v = a.Total == 0 ? 0 : (float)a.Applied / a.Total;
                Theme.DrawBar(g, bar, v, a.Total > 0 && a.Applied == a.Total ? Theme.Success : Theme.Bright, 0);
                TextRenderer.DrawText(g, a.Applied + "/" + a.Total, Theme.Small, new Rectangle(row.Right - Theme.S(62), row.Y, Theme.S(54), row.Height),
                    Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
                ry += Theme.S(30);
            }

            int rx = x0 + leftW + Gap;
            int ringH = Theme.S(176);
            Rectangle ring = new Rectangle(rx, y, rightW, ringH);
            Card(g, ring);
            Heading(g, "Preset Padrão", ring);
            int total = Data.Applied + Data.Pending;
            float pct = total == 0 ? 0 : (float)Data.Applied / total;
            Rectangle circle = new Rectangle(ring.X + Theme.S(20), ring.Y + Theme.S(56), Theme.S(96), Theme.S(96));
            using (Pen p = new Pen(Theme.Border, Theme.SF(10f)))
                g.DrawEllipse(p, Rectangle.Inflate(circle, -Theme.S(5), -Theme.S(5)));
            if (pct > 0)
                using (Pen p = new Pen(Theme.Bright, Theme.SF(10f)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawArc(p, Rectangle.Inflate(circle, -Theme.S(5), -Theme.S(5)), -90, Math.Max(1f, 360f * pct));
                }
            TextRenderer.DrawText(g, (int)Math.Round(pct * 100) + "%", Theme.Subtitle, circle, Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            int lx = circle.Right + Theme.S(18), ly = circle.Y + Theme.S(12);
            Legend(g, lx, ly, Theme.Bright, Data.Applied + " aplicados", Theme.Text);
            Legend(g, lx, ly + Theme.S(26), Theme.Warning, Data.Pending + " pendentes", Theme.Text);
            if (Data.OtherWindows > 0) Legend(g, lx, ly + Theme.S(52), Theme.Border, Data.OtherWindows + " de outro Windows", Theme.Muted);

            Rectangle res = new Rectangle(rx, y + ringH + Gap, rightW, ah - ringH - Gap);
            Card(g, res);
            Heading(g, "Recursos do PC", res);
            SystemSnapshot s = Data.Stats ?? new SystemSnapshot();
            int colW = (res.Width - Theme.S(40)) / 3;
            int vy = res.Y + Theme.S(54);
            Resource(g, new Rectangle(res.X + Theme.S(20), vy, colW, Theme.S(60)), "cpu",
                s.CpuPercent >= 0 ? s.CpuPercent + "%" : "—", "Processador", s.CpuPercent >= 0 ? s.CpuPercent / 100f : -1);
            Resource(g, new Rectangle(res.X + Theme.S(20) + colW, vy, colW, Theme.S(60)), "memory",
                s.RamTotalGb > 0 ? Fmt(s.RamUsedGb) + "/" + Fmt(s.RamTotalGb) : "—", "Memória (GB)", s.RamTotalGb > 0 ? (float)(s.RamUsedGb / s.RamTotalGb) : -1);
            Resource(g, new Rectangle(res.X + Theme.S(20) + colW * 2, vy, colW, Theme.S(60)), "drive",
                s.DiskTotalGb > 0 ? Fmt(s.DiskFreeGb) + " GB" : "—", "Livre em " + s.DiskName, s.DiskTotalGb > 0 ? (float)(1 - s.DiskFreeGb / s.DiskTotalGb) : -1);
            if (!string.IsNullOrEmpty(s.CpuName) && res.Height > Theme.S(150))
                TextRenderer.DrawText(g, s.CpuName.Trim(), Theme.Small, new Rectangle(res.X + Theme.S(20), res.Bottom - Theme.S(34), res.Width - Theme.S(40), Theme.S(20)),
                    Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            y += ah + Gap;

            // ---- atividades recentes ----
            Rectangle act = new Rectangle(x0, y, w, ActivitiesHeight());
            Card(g, act);
            Heading(g, "Atividades recentes", act);
            Link(g, "Histórico e desfazer", "history", act.Right - Theme.S(20), act.Y + Theme.S(20));
            int ay = act.Y + Theme.S(54);
            if (Data.Activities.Count == 0)
                TextRenderer.DrawText(g, "Nada aplicado ainda neste PC. Depois de aplicar, as sessões aparecem aqui.", Theme.Body,
                    new Rectangle(act.X + Theme.S(20), ay, act.Width - Theme.S(40), Theme.S(40)), Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            int n = 0;
            foreach (DashActivity a in Data.Activities)
            {
                if (n++ >= 4) break;
                using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, act.X + Theme.S(20), ay, act.Right - Theme.S(20), ay);
                Color tone = a.Kind == 0 ? Theme.Success : a.Kind == 2 ? Theme.Danger : Theme.Muted;
                Rectangle tile = new Rectangle(act.X + Theme.S(20), ay + Theme.S(11), Theme.S(30), Theme.S(30));
                Theme.FillRounded(g, tile, Theme.S(8), Theme.Blend(tone, 0.14f, Theme.Card), Color.Empty);
                Icons.Draw(g, a.Kind == 0 ? "check" : a.Kind == 2 ? "x" : "undo", Rectangle.Inflate(tile, -Theme.S(7), -Theme.S(7)), tone, 2.4f);
                int pillW = Theme.PillWidth(g, a.Pill, Theme.SmallBold);
                int whenW = Theme.S(150);
                TextRenderer.DrawText(g, a.Title, Theme.Body, new Rectangle(tile.Right + Theme.S(14), ay, act.Width - Theme.S(110) - pillW - whenW, Theme.S(52)),
                    Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, a.When, Theme.Small, new Rectangle(act.Right - Theme.S(32) - pillW - whenW, ay, whenW, Theme.S(52)),
                    Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.SingleLine);
                Color pf = a.Kind == 0 ? Theme.RiskLowFg : a.Kind == 2 ? Theme.RiskHighFg : Theme.TagFg;
                Color pb = a.Kind == 0 ? Theme.RiskLowBg : a.Kind == 2 ? Theme.RiskHighBg : Theme.TagBg;
                Theme.DrawPill(g, a.Pill, Theme.SmallBold, pf, pb, act.Right - Theme.S(20) - pillW, ay + (Theme.S(52) - Theme.PillHeight(g, Theme.SmallBold)) / 2);
                ay += Theme.S(52);
            }
        }

        void Legend(Graphics g, int x, int y, Color c, string text, Color fg)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x, y + Theme.S(5), Theme.S(9), Theme.S(9));
            TextRenderer.DrawText(g, text, Theme.Body, new Point(x + Theme.S(16), y), fg, TextFormatFlags.NoPadding);
        }

        void Stat(Graphics g, Rectangle r, string icon, string label, string value, string sub, string pill, int pillKind)
        {
            Card(g, r);
            Rectangle tile = new Rectangle(r.X + Theme.S(18), r.Y + Theme.S(16), Theme.S(30), Theme.S(30));
            Theme.FillRounded(g, tile, Theme.S(8), Theme.Blend(Theme.Bright, 0.12f, Theme.Card), Color.Empty);
            Icons.Draw(g, icon, Rectangle.Inflate(tile, -Theme.S(7), -Theme.S(7)), Theme.Bright, 2f);
            TextRenderer.DrawText(g, label, Theme.Caption, new Rectangle(tile.Right + Theme.S(10), tile.Y, r.Width - Theme.S(70), tile.Height),
                Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, value ?? "", Theme.Value, new Rectangle(r.X + Theme.S(16), r.Y + Theme.S(54), r.Width - Theme.S(32), Theme.S(32)),
                Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
            if (pill != null)
            {
                Color pf = pillKind == 1 ? Theme.RiskLowFg : Theme.RiskMidFg, pb = pillKind == 1 ? Theme.RiskLowBg : Theme.RiskMidBg;
                Theme.DrawPill(g, pill, Theme.SmallBold, pf, pb, r.X + Theme.S(18), r.Y + Theme.S(90));
            }
            else
                TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(r.X + Theme.S(18), r.Y + Theme.S(92), r.Width - Theme.S(36), Theme.S(20)),
                    Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        void Resource(Graphics g, Rectangle r, string icon, string value, string label, float level)
        {
            Icons.Draw(g, icon, new Rectangle(r.X, r.Y, Theme.S(16), Theme.S(16)), Theme.Muted, 2f);
            TextRenderer.DrawText(g, value, Theme.BodyBold, new Rectangle(r.X, r.Y + Theme.S(20), r.Width - Theme.S(6), Theme.S(22)),
                Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, label, Theme.Small, new Rectangle(r.X, r.Y + Theme.S(42), r.Width - Theme.S(6), Theme.S(18)),
                Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (level >= 0)
                Theme.DrawBar(g, new Rectangle(r.X, r.Y + Theme.S(64), r.Width - Theme.S(12), Theme.S(4)), level,
                    level > 0.9f ? Theme.Danger : level > 0.75f ? Theme.Warning : Theme.Bright, 0);
        }

        string HitTest(Point p)
        {
            foreach (KeyValuePair<Rectangle, string> kv in links) if (kv.Key.Contains(p)) return kv.Value;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            string k = HitTest(e.Location);
            Cursor = k != null ? Cursors.Hand : Cursors.Default;
            if (k != hoverKey) { hoverKey = k; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hoverKey != null) { hoverKey = null; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            string k = HitTest(e.Location);
            if (k != null && Navigate != null) Navigate(k.StartsWith("area:") ? k.Substring(5) : k);
            base.OnMouseClick(e);
        }
    }
}
