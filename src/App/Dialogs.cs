// Janelas auxiliares: progresso, detalhes de um ajuste e histórico/desfazer.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    /// <summary>Roda um trabalho em segundo plano mostrando progresso, operações e log ao vivo.</summary>
    public class ProgressForm : ThemedForm
    {
        public delegate string Work(ProgressHandler progress, Action<string> log);

        class Op
        {
            public string Title, Detail;
            public int State; // 0 concluída, 1 falhou, 2 ignorada, 3 em andamento
        }

        readonly Work work;
        readonly string title;
        readonly Panel headerPanel, opsPanel;
        readonly TextBox logBox;
        readonly FlatButton closeBtn, logBtn, restartBtn;
        readonly Panel bottom;
        readonly List<Op> ops = new List<Op>();
        readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
        readonly DateTime started = DateTime.Now;
        bool running = true;
        int done, total = 1, okCount, failCount, skipCount;
        string currentTitle = "Preparando…", currentText = "";
        int currentPct = -1;
        float phase;
        string summary;

        public string LogPath;
        public bool OfferRestart;
        /// <summary>Quantos itens a sessão tem (para a contagem de "aguardando").</summary>
        public int TotalItems;
        /// <summary>Títulos na ordem em que vão rodar (opcional), para mostrar os próximos.</summary>
        public List<string> Queue;

        public ProgressForm(string title, Work work)
        {
            this.work = work;
            this.title = title;
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Theme.S(980), Theme.S(660));
            MinimumSize = new Size(Theme.S(760), Theme.S(520));
            MinimizeBox = false; MaximizeBox = true; ShowInTaskbar = false;
            ShowIcon = false;
            BackColor = Theme.Back;

            headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = Theme.S(250);
            headerPanel.BackColor = Theme.Back;
            headerPanel.Paint += PaintHeader;
            SetDouble(headerPanel);

            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Theme.S(68);
            bottom.BackColor = Theme.Surface;
            bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0);
                string note = running ? "Não feche o programa: interromper no meio pode deixar ajustes pela metade." : summary ?? "";
                int right = (logBtn.Visible ? logBtn.Left : closeBtn.Left);
                if (restartBtn.Visible) right = restartBtn.Left;
                TextRenderer.DrawText(e.Graphics, note, running ? Theme.Small : Theme.BodyBold,
                    new Rectangle(Theme.S(24), 0, right - Theme.S(40), bottom.Height), running ? Theme.Muted : Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            };
            closeBtn = new FlatButton("Fechar", ButtonKind.Primary, null);
            closeBtn.Enabled = false;
            closeBtn.Click += delegate { Close(); };
            logBtn = new FlatButton("Abrir log", ButtonKind.Secondary, "file");
            logBtn.Visible = false;
            logBtn.Click += delegate { if (LogPath != null && File.Exists(LogPath)) Process.Start("notepad.exe", "\"" + LogPath + "\""); };
            restartBtn = new FlatButton("Reiniciar agora", ButtonKind.Secondary, "restart");
            restartBtn.Visible = false;
            restartBtn.Click += delegate
            {
                if (ConfirmDialog.Ask(this, "Reiniciar o computador agora?", "Salve o que estiver aberto. O PC reinicia em 5 segundos.", null, "Reiniciar", "restart", true))
                    Process.Start("shutdown.exe", "/r /t 5");
            };
            bottom.Controls.Add(closeBtn);
            bottom.Controls.Add(logBtn);
            bottom.Controls.Add(restartBtn);
            bottom.Resize += delegate { LayoutBottom(); };

            // corpo: operações à esquerda, log à direita
            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Back;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            body.Padding = new Padding(Theme.S(18), 0, Theme.S(18), Theme.S(16));

            opsPanel = new Panel();
            opsPanel.Dock = DockStyle.Fill;
            opsPanel.Margin = new Padding(Theme.S(6), 0, Theme.S(8), 0);
            opsPanel.BackColor = Theme.Back;
            opsPanel.Paint += PaintOps;
            opsPanel.Resize += delegate { opsPanel.Invalidate(); };
            SetDouble(opsPanel);

            Panel logCard = new Panel();
            logCard.Dock = DockStyle.Fill;
            logCard.Margin = new Padding(Theme.S(8), 0, Theme.S(6), 0);
            logCard.BackColor = Theme.Back;
            logCard.Padding = new Padding(Theme.S(16), Theme.S(50), Theme.S(8), Theme.S(12));
            logCard.Paint += delegate(object s, PaintEventArgs e)
            {
                Rectangle r = new Rectangle(0, 0, logCard.Width - 1, logCard.Height - 1);
                Theme.FillRounded(e.Graphics, r, Theme.S(12), Theme.LogBack, Theme.Border);
                TextRenderer.DrawText(e.Graphics, "Log de execução", Theme.Subtitle, new Point(Theme.S(16), Theme.S(14)), Theme.Text, TextFormatFlags.NoPadding);
                using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 1, Theme.S(44), logCard.Width - 2, Theme.S(44));
            };
            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill; logBox.BackColor = Theme.LogBack; logBox.ForeColor = Theme.TagFg; logBox.BorderStyle = BorderStyle.None;
            logBox.Font = Theme.Mono;
            logBox.WordWrap = true;
            Native.Track(logBox);
            logCard.Controls.Add(logBox);

            body.Controls.Add(opsPanel, 0, 0);
            body.Controls.Add(logCard, 1, 0);

            Controls.Add(body);
            Controls.Add(bottom);
            Controls.Add(headerPanel);

            clock.Interval = 60;
            clock.Tick += delegate
            {
                phase += 0.012f;
                headerPanel.Invalidate();
            };
        }

        static void SetDouble(Control c)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(c, true, null);
        }

        void LayoutBottom()
        {
            int top = (bottom.Height - closeBtn.Height) / 2;
            int x = bottom.Width - Theme.S(24) - closeBtn.Width;
            closeBtn.Location = new Point(x, top);
            x -= Theme.S(10) + logBtn.Width;
            logBtn.Location = new Point(x, top);
            x -= (logBtn.Visible ? Theme.S(10) : 0) + restartBtn.Width;
            if (!logBtn.Visible) x = closeBtn.Left - Theme.S(10) - restartBtn.Width;
            restartBtn.Location = new Point(x, top);
            bottom.Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutBottom();
            clock.Start();
            ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try { result = work(Report, Log); }
                catch (Exception ex) { result = "Erro inesperado: " + ex.Message; }
                if (IsHandleCreated) BeginInvoke((MethodInvoker)delegate { Finish(result); });
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            clock.Stop();
            base.OnFormClosed(e);
        }

        void Report(int d, int t, string message)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate
            {
                done = d; total = Math.Max(1, t);
                if (message == "Concluído") return;
                // mensagens ao vivo: "Título — 45% · texto" (Engine.LiveMessage)
                int sep = message.IndexOf(" — ");
                string head = sep > 0 ? message.Substring(0, sep) : message;
                string rest = sep > 0 ? message.Substring(sep + 3) : "";
                int pct = -1;
                int pctEnd = rest.IndexOf("% · ");
                if (pctEnd < 0 && rest.EndsWith("%")) pctEnd = rest.Length - 1;
                if (pctEnd > 0) { int p; if (int.TryParse(rest.Substring(0, pctEnd), out p)) { pct = p; rest = rest.Length > pctEnd + 4 ? rest.Substring(pctEnd + 4) : ""; } }
                if (head != currentTitle)
                {
                    currentTitle = head;
                    Op last = ops.Count > 0 ? ops[ops.Count - 1] : null;
                    if (last == null || last.State != 3 || last.Title != head)
                    {
                        Op op = new Op(); op.Title = head; op.State = 3;
                        ops.Add(op);
                    }
                }
                currentPct = pct;
                currentText = rest;
                headerPanel.Invalidate();
                opsPanel.Invalidate();
            });
        }

        /// <summary>Resultado de um item (chamado de qualquer thread).</summary>
        public void ReportResult(string itemTitle, ActionOutcome outcome, string detail)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate
            {
                Op op = ops.FindLast(delegate(Op o) { return o.Title == itemTitle && o.State == 3; });
                if (op == null) { op = new Op(); op.Title = itemTitle; ops.Add(op); }
                op.State = outcome == ActionOutcome.Ok ? 0 : outcome == ActionOutcome.Failed ? 1 : 2;
                if (detail != null)
                {
                    int i = detail.IndexOf(" — ");
                    op.Detail = (i > 0 ? detail.Substring(i + 3) : detail).Replace("\r", "").Replace("\n", " ");
                    if (op.Detail.StartsWith("ignorado: ")) op.Detail = op.Detail.Substring(10);
                }
                if (op.State == 0) okCount++; else if (op.State == 1) failCount++; else skipCount++;
                headerPanel.Invalidate();
                opsPanel.Invalidate();
            });
        }

        void Log(string line)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate { logBox.AppendText(line.Replace("\r", "").Replace("\n", Environment.NewLine) + Environment.NewLine); });
        }

        void Finish(string result)
        {
            running = false;
            clock.Stop();
            done = total;
            summary = result;
            currentTitle = result.StartsWith("Cancelado") ? "Cancelado" : "Concluído";
            currentText = "";
            currentPct = 100;
            foreach (Op o in ops) if (o.State == 3) o.State = 0;
            closeBtn.Enabled = true;
            logBtn.Visible = LogPath != null;
            restartBtn.Visible = OfferRestart;
            LayoutBottom();
            headerPanel.Invalidate();
            opsPanel.Invalidate();
            closeBtn.Focus();
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Back);
            int pad = Theme.S(24), w = headerPanel.Width - pad * 2;
            Rectangle card = new Rectangle(pad, Theme.S(20), w, Theme.S(118));
            Theme.FillRounded(g, card, Theme.S(14), Theme.Card, Theme.Border);
            float overall = running ? (float)done / total : 1f;
            string pctText = (int)Math.Round(overall * 100) + "%";
            TimeSpan el = DateTime.Now - started;
            string elapsed = el.TotalHours >= 1 ? string.Format("{0}h{1:00} decorridos", (int)el.TotalHours, el.Minutes)
                : el.TotalMinutes >= 1 ? string.Format("{0} min decorridos", (int)el.TotalMinutes) : string.Format("{0} s decorridos", (int)el.TotalSeconds);
            Size ps = TextRenderer.MeasureText(g, pctText, Theme.Huge, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, pctText, Theme.Huge, new Point(card.Right - Theme.S(22) - ps.Width, card.Y + Theme.S(14)), Theme.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, running ? title : (failCount > 0 ? "Concluído com falhas" : currentTitle), Theme.Subtitle,
                new Rectangle(card.X + Theme.S(22), card.Y + Theme.S(18), card.Width - ps.Width - Theme.S(220), Theme.S(28)), Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Size es = TextRenderer.MeasureText(g, elapsed, Theme.Small, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, elapsed, Theme.Small, new Point(card.Right - Theme.S(34) - ps.Width - es.Width, card.Y + Theme.S(26)), Theme.Muted, TextFormatFlags.NoPadding);
            Theme.DrawBar(g, new Rectangle(card.X + Theme.S(22), card.Y + Theme.S(62), card.Width - Theme.S(44), Theme.S(8)), overall,
                !running && failCount > 0 ? Theme.Warning : running ? Theme.Bright : Theme.Success, 0);
            int x = card.X + Theme.S(22), y = card.Y + Theme.S(84);
            x = Count(g, x, y, Theme.Success, okCount + " concluídos");
            x = Count(g, x, y, Theme.Danger, failCount + " com falha");
            x = Count(g, x, y, Theme.Muted, skipCount + " ignorados");
            int items = Math.Max(TotalItems, okCount + failCount + skipCount);
            int waiting = Math.Max(0, items - okCount - failCount - skipCount - (running ? 1 : 0));
            if (running) Count(g, x, y, Theme.Bright, "1 em andamento · " + waiting + " aguardando");

            // operação atual
            Rectangle cur = new Rectangle(pad, card.Bottom + Theme.S(14), w, Theme.S(92));
            Color curBg = running ? Theme.Blend(Theme.Bright, 0.07f, Theme.Card) : Theme.Card;
            Theme.FillRounded(g, cur, Theme.S(14), curBg, running ? Theme.Blend(Theme.Bright, 0.35f, Theme.Card) : Theme.Border);
            Rectangle tile = new Rectangle(cur.X + Theme.S(18), cur.Y + Theme.S(18), Theme.S(32), Theme.S(32));
            Color tone = running ? Theme.Bright : failCount > 0 ? Theme.Warning : Theme.Success;
            Theme.FillRounded(g, tile, Theme.S(9), Theme.Blend(tone, 0.16f, curBg), Color.Empty);
            Icons.Draw(g, running ? "play" : failCount > 0 ? "alert" : "check", Rectangle.Inflate(tile, -Theme.S(8), -Theme.S(8)), tone, 2.4f);
            int tx = tile.Right + Theme.S(14), tw = cur.Right - tx - Theme.S(20);
            string pctRight = running && currentPct >= 0 ? currentPct + "%" : "";
            Size prs = pctRight.Length > 0 ? TextRenderer.MeasureText(g, pctRight, Theme.Subtitle, Size.Empty, TextFormatFlags.NoPadding) : Size.Empty;
            TextRenderer.DrawText(g, running ? currentTitle : (summary ?? ""), Theme.BodyBold, new Rectangle(tx, cur.Y + Theme.S(16), tw - prs.Width - Theme.S(10), Theme.S(22)),
                Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (pctRight.Length > 0)
                TextRenderer.DrawText(g, pctRight, Theme.Subtitle, new Point(cur.Right - Theme.S(20) - prs.Width, cur.Y + Theme.S(14)), Theme.Text, TextFormatFlags.NoPadding);
            string sub = running ? (currentText.Length > 0 ? currentText : "Em andamento…") : (OfferRestart ? "Reinicie o PC para tudo valer." : "");
            TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(tx, cur.Y + Theme.S(40), tw, Theme.S(20)), running ? Theme.InfoFg : Theme.Muted,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (running)
                Theme.DrawBar(g, new Rectangle(tx, cur.Y + Theme.S(66), tw, Theme.S(6)), currentPct >= 0 ? currentPct / 100f : -1f, Theme.Bright, phase);
        }

        int Count(Graphics g, int x, int y, Color c, string text)
        {
            using (SolidBrush b = new SolidBrush(c)) g.FillEllipse(b, x, y + Theme.S(5), Theme.S(8), Theme.S(8));
            TextRenderer.DrawText(g, text, Theme.Small, new Point(x + Theme.S(14), y), Theme.TagFg, TextFormatFlags.NoPadding);
            return x + Theme.S(14) + TextRenderer.MeasureText(g, text, Theme.Small, Size.Empty, TextFormatFlags.NoPadding).Width + Theme.S(22);
        }

        void PaintOps(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Back);
            Rectangle card = new Rectangle(0, 0, opsPanel.Width - 1, opsPanel.Height - 1);
            Theme.FillRounded(g, card, Theme.S(12), Theme.Card, Theme.Border);
            TextRenderer.DrawText(g, "Operações", Theme.Subtitle, new Point(Theme.S(16), Theme.S(14)), Theme.Text, TextFormatFlags.NoPadding);
            int rowH = Theme.S(50);
            int top = Theme.S(46);
            int fit = Math.Max(1, (card.Height - top - Theme.S(6)) / rowH);
            // mostra as mais recentes; a em andamento fica sempre visível
            List<KeyValuePair<Op, bool>> list = new List<KeyValuePair<Op, bool>>();
            foreach (Op o in ops) list.Add(new KeyValuePair<Op, bool>(o, false));
            if (running && Queue != null)
            {
                int next = okCount + failCount + skipCount + 1;
                for (int i = next; i < Queue.Count && i < next + 3; i++)
                {
                    Op w = new Op(); w.Title = Queue[i]; w.State = 4;
                    list.Add(new KeyValuePair<Op, bool>(w, true));
                }
            }
            int start = Math.Max(0, list.Count - fit);
            int y = top;
            for (int i = start; i < list.Count; i++)
            {
                Op o = list[i].Key;
                Rectangle row = new Rectangle(1, y, card.Width - 2, rowH);
                using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, row.X, row.Y, row.Right, row.Y);
                if (o.State == 3) using (SolidBrush b = new SolidBrush(Theme.Blend(Theme.Bright, 0.07f, Theme.Card))) g.FillRectangle(b, row.X, row.Y + 1, row.Width, row.Height - 1);
                Color tone = o.State == 0 ? Theme.Success : o.State == 1 ? Theme.Danger : o.State == 3 ? Theme.Bright : Theme.Muted;
                string icon = o.State == 0 ? "check" : o.State == 1 ? "x" : o.State == 3 ? "play" : o.State == 4 ? "clock" : "minus";
                string pill = o.State == 0 ? "Concluído" : o.State == 1 ? "Falhou" : o.State == 3 ? "Em andamento" : o.State == 4 ? "Aguardando" : "Ignorado";
                Color pf = o.State == 0 ? Theme.RiskLowFg : o.State == 1 ? Theme.RiskHighFg : o.State == 3 ? Theme.InfoFg : Theme.TagFg;
                Color pb = o.State == 0 ? Theme.RiskLowBg : o.State == 1 ? Theme.RiskHighBg : o.State == 3 ? Theme.InfoBg : Theme.TagBg;
                Rectangle tile = new Rectangle(row.X + Theme.S(14), row.Y + Theme.S(11), Theme.S(28), Theme.S(28));
                Theme.FillRounded(g, tile, Theme.S(8), Theme.Blend(tone, 0.15f, Theme.Card), Color.Empty);
                Icons.Draw(g, icon, Rectangle.Inflate(tile, -Theme.S(7), -Theme.S(7)), tone, 2.6f);
                int pw = Theme.PillWidth(g, pill, Theme.SmallBold);
                int tx = tile.Right + Theme.S(12), tw = row.Right - tx - pw - Theme.S(26);
                string detail = o.State == 3 ? (currentText.Length > 0 ? (currentPct >= 0 ? currentPct + "% · " : "") + currentText : "") : o.Detail;
                bool hasDetail = !string.IsNullOrEmpty(detail);
                TextRenderer.DrawText(g, o.Title, Theme.BodyBold, new Rectangle(tx, row.Y + (hasDetail ? Theme.S(7) : 0), tw, hasDetail ? Theme.S(20) : row.Height),
                    o.State == 4 ? Theme.Muted : Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | (hasDetail ? 0 : TextFormatFlags.VerticalCenter));
                if (hasDetail)
                    TextRenderer.DrawText(g, detail, Theme.Small, new Rectangle(tx, row.Y + Theme.S(27), tw, Theme.S(18)),
                        o.State == 1 ? Theme.RiskHighFg : o.State == 3 ? Theme.InfoFg : Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                Theme.DrawPill(g, pill, Theme.SmallBold, pf, pb, row.Right - Theme.S(14) - pw, row.Y + (rowH - Theme.PillHeight(g, Theme.SmallBold)) / 2);
                y += rowH;
            }
            if (list.Count == 0)
                TextRenderer.DrawText(g, "Preparando…", Theme.Body, new Point(Theme.S(16), top + Theme.S(12)), Theme.Muted, TextFormatFlags.NoPadding);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Aguarde terminar. Interromper no meio pode deixar ajustes pela metade.", AppInfo.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            base.OnFormClosing(e);
        }
    }

    /// <summary>Mostra exatamente o que um ajuste faz.</summary>
    public class DetailsForm : ThemedForm
    {
        public DetailsForm(Tweak t) : this(t, null) { }

        public DetailsForm(Tweak t, Detector detector)
        {
            Text = t.Title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Theme.S(760), Theme.S(520));
            MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(t.Title);
            sb.AppendLine(new string('=', Math.Min(70, t.Title.Length)));
            sb.AppendLine(t.Description);
            if (t.Warning != null) { sb.AppendLine(); sb.AppendLine("ATENÇÃO: " + t.Warning); }
            sb.AppendLine();
            sb.AppendLine("Categoria: " + CategoryInfo.Title(t.Category));
            sb.AppendLine("Risco: " + Theme.RiskText(t.Risk));
            sb.AppendLine("Windows: " + (t.Windows == WinVersion.Ambos ? "10 e 11" : t.Windows == WinVersion.Win11 ? "só 11" : "só 10"));
            sb.AppendLine("Preset Padrão: " + (t.Padrao ? "faz parte" : "não faz parte (só manual)"));
            sb.AppendLine("Desfazer: " + (t.IsReversible ? "sim, pelo Histórico" : "não (ou só em parte)"));
            if (t.NeedsRestart) sb.AppendLine("Pede reinício ou novo login para valer.");
            if (t.ProOnly) sb.AppendLine("Só tem efeito no Windows Pro, Enterprise ou Education.");
            sb.AppendLine("Id: " + t.Id);
            sb.AppendLine();
            if (detector != null)
            {
                TweakStatus st = detector.Summarize(t);
                string head = st.State == TweakState.Applied ? "já aplicado"
                    : st.State == TweakState.Partial ? string.Format("parcial ({0} de {1})", st.Applied, st.Known)
                    : st.State == TweakState.NotApplied ? "não aplicado"
                    : st.State == TweakState.NotApplicable ? "nada a fazer (o que este ajuste mexe não existe neste PC)"
                    : st.State == TweakState.Checking ? "verificando…" : "sem como verificar (ação de uma vez só)";
                sb.AppendLine("SITUAÇÃO NESTE PC: " + head.ToUpperInvariant());
                foreach (TweakAction a in t.Actions)
                {
                    DetectState ds = detector.StateOf(a);
                    string mark = ds == DetectState.Applied ? "[✓]" : ds == DetectState.NotApplied ? "[ ]" : ds == DetectState.NotApplicable ? "[–]" : "[?]";
                    sb.AppendLine(" " + mark + " " + a.Describe());
                }
                sb.AppendLine("   [✓] já está assim  [ ] falta fazer  [–] não existe neste PC  [?] sem como verificar");
                sb.AppendLine();
            }
            sb.AppendLine("O QUE ESTE AJUSTE FAZ");
            foreach (TweakAction a in t.Actions) sb.AppendLine(" • " + a.Describe());
            sb.AppendLine();
            sb.AppendLine("POWERSHELL EQUIVALENTE");
            foreach (TweakAction a in t.Actions)
            {
                sb.AppendLine(a.ToPowerShell().TrimEnd());
                sb.AppendLine();
            }

            TextBox box = new TextBox();
            box.Multiline = true; box.ReadOnly = true; box.ScrollBars = ScrollBars.Both; box.WordWrap = false;
            box.Dock = DockStyle.Fill; box.BorderStyle = BorderStyle.None; box.BackColor = Theme.Card; box.ForeColor = Theme.Text;
            Native.Track(box);
            box.Font = Theme.Mono;
            box.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            box.Select(0, 0);

            Panel wrap = new Panel();
            wrap.Dock = DockStyle.Fill; wrap.Padding = new Padding(Theme.S(16));
            wrap.Controls.Add(box);

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Bottom; bottom.Height = Theme.S(60); bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.Padding = new Padding(Theme.S(12));
            FlatButton ok = new FlatButton("Fechar", true);
            ok.Click += delegate { Close(); };
            FlatButton copy = new FlatButton("Copiar", false);
            copy.Click += delegate { Clipboard.SetText(box.Text); };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(copy);
            CancelButton = ok;

            Controls.Add(wrap);
            Controls.Add(bottom);
        }
    }

    /// <summary>Lista as sessões aplicadas e permite desfazer.</summary>
    public class HistoryForm : ThemedForm
    {
        readonly Engine engine;
        readonly ListView list;
        readonly TextBox details;
        readonly FlatButton undoBtn;
        List<SessionBackup> sessions = new List<SessionBackup>();

        public HistoryForm(Engine engine)
        {
            this.engine = engine;
            Text = "Histórico e desfazer";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Theme.S(820), Theme.S(560));
            MinimumSize = new Size(Theme.S(600), Theme.S(420));
            MinimizeBox = false; ShowInTaskbar = false; ShowIcon = false;

            Label head = new Label();
            head.Text = "Cada vez que você aplica ajustes, o valor anterior de cada configuração é guardado. Escolha uma sessão para desfazer.";
            head.ForeColor = Theme.Muted; head.Dock = DockStyle.Top; head.Height = Theme.S(48);
            head.Padding = new Padding(Theme.S(16), Theme.S(12), Theme.S(16), 0);

            list = new ListView();
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = false; list.HideSelection = false;
            list.Dock = DockStyle.Top; list.Height = Theme.S(200); list.BorderStyle = BorderStyle.None;
            list.BackColor = Theme.Input; list.ForeColor = Theme.Text;
            Native.Track(list);
            // desenhada à mão para cabeçalho e seleção ficarem certos no tema escuro
            list.OwnerDraw = true;
            list.DrawColumnHeader += delegate(object s, DrawListViewColumnHeaderEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
                using (Pen p = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                    e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + Theme.S(4), e.Bounds.Right - 1, e.Bounds.Bottom - Theme.S(4));
                }
                Rectangle tr = new Rectangle(e.Bounds.X + Theme.S(6), e.Bounds.Y, e.Bounds.Width - Theme.S(8), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, Theme.SmallBold, tr, Theme.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };
            list.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { };
            list.DrawSubItem += delegate(object s, DrawListViewSubItemEventArgs e)
            {
                bool sel = e.Item.Selected;
                using (SolidBrush b = new SolidBrush(sel ? Theme.AccentSoft : Theme.Input)) e.Graphics.FillRectangle(b, e.Bounds);
                Color fg = sel ? Theme.Text : e.Item.ForeColor;
                Rectangle tr = new Rectangle(e.Bounds.X + Theme.S(6), e.Bounds.Y, e.Bounds.Width - Theme.S(8), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, Theme.Body, tr, fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };
            list.Columns.Add("Data", Theme.S(170));
            list.Columns.Add("Com desfazer", Theme.S(120));
            list.Columns.Add("Sem desfazer", Theme.S(120));
            list.Columns.Add("Situação", Theme.S(150));
            list.Columns.Add("Windows", Theme.S(220));
            list.SelectedIndexChanged += delegate { ShowSelected(); };

            Panel listWrap = new Panel();
            listWrap.Dock = DockStyle.Top; listWrap.Height = Theme.S(212); listWrap.Padding = new Padding(Theme.S(16), 0, Theme.S(16), Theme.S(8));
            listWrap.Controls.Add(list);

            details = new TextBox();
            details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Vertical;
            details.Dock = DockStyle.Fill; details.BackColor = Theme.Input; details.ForeColor = Theme.Text; details.BorderStyle = BorderStyle.None;
            Native.Track(details);
            Panel detWrap = new Panel();
            detWrap.Dock = DockStyle.Fill; detWrap.Padding = new Padding(Theme.S(16), 0, Theme.S(16), 0);
            detWrap.Controls.Add(details);

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Bottom; bottom.Height = Theme.S(60); bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.Padding = new Padding(Theme.S(12));
            FlatButton close = new FlatButton("Fechar", false);
            close.Click += delegate { Close(); };
            undoBtn = new FlatButton("Desfazer esta sessão", true);
            undoBtn.Enabled = false;
            undoBtn.Click += delegate { UndoSelected(); };
            FlatButton logs = new FlatButton("Abrir pasta de logs", false);
            logs.Click += delegate
            {
                Directory.CreateDirectory(engine.LogsFolder);
                Process.Start("explorer.exe", "\"" + engine.LogsFolder + "\"");
            };
            bottom.Controls.Add(close);
            bottom.Controls.Add(undoBtn);
            bottom.Controls.Add(logs);
            CancelButton = close;

            Controls.Add(detWrap);
            Controls.Add(bottom);
            Controls.Add(listWrap);
            Controls.Add(head);
            Reload();
        }

        void Reload()
        {
            sessions = engine.ListSessions();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (SessionBackup s in sessions)
            {
                ListViewItem it = new ListViewItem(s.Date);
                it.SubItems.Add(s.Tweaks.Count + " ajuste(s)");
                it.SubItems.Add(s.Irreversible.Count.ToString());
                it.SubItems.Add(s.Undone ? "Desfeita" : s.UndoCount == 0 ? "Nada a desfazer" : "Aplicada");
                it.SubItems.Add(s.Windows ?? "");
                it.ForeColor = s.Undone ? Theme.Muted : Theme.Text;
                list.Items.Add(it);
            }
            list.EndUpdate();
            if (sessions.Count == 0) details.Text = "Nenhuma sessão ainda. Depois de aplicar ajustes, elas aparecem aqui.";
            else list.Items[0].Selected = true;
        }

        SessionBackup Current
        {
            get { return list.SelectedIndices.Count == 0 ? null : sessions[list.SelectedIndices[0]]; }
        }

        void ShowSelected()
        {
            SessionBackup s = Current;
            undoBtn.Enabled = s != null && !s.Undone && s.UndoCount > 0;
            if (s == null) return;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Sessão " + s.Id + " — " + s.Machine + " — " + s.Windows);
            sb.AppendLine();
            sb.AppendLine("Ajustes que podem ser desfeitos:");
            foreach (TweakUndo t in s.Tweaks) sb.AppendLine(" • " + t.TweakTitle + " (" + t.Records.Count + " alteração(ões))");
            if (s.Irreversible.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Sem desfazer (apps removidos, limpezas etc.):");
                foreach (string t in s.Irreversible) sb.AppendLine(" • " + t);
                sb.AppendLine("Para esses, use o ponto de restauração criado antes da sessão ou reinstale pela Store/winget.");
            }
            if (s.Undone) { sb.AppendLine(); sb.AppendLine("Esta sessão já foi desfeita."); }
            details.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
            details.SelectionStart = 0;
            details.SelectionLength = 0;
        }

        void UndoSelected()
        {
            SessionBackup s = Current;
            if (s == null) return;
            if (MessageBox.Show(this, string.Format("Desfazer {0} ajuste(s) da sessão de {1}?", s.Tweaks.Count, s.Date),
                    AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            ProgressForm pf = null;
            pf = new ProgressForm("Desfazendo sessão de " + s.Date, delegate(ProgressHandler progress, Action<string> log)
            {
                List<string> rep = engine.Undo(s, progress);
                int fails = 0;
                foreach (string r in rep) { log(r); if (r.StartsWith("FALHA")) fails++; }
                return fails == 0 ? "Sessão desfeita. Reinicie o computador para tudo voltar ao normal."
                                  : string.Format("Concluído com {0} falha(s). Veja o log acima.", fails);
            });
            pf.OfferRestart = true;
            pf.ShowDialog(this);
            Reload();
        }
    }
}
