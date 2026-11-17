// Janelas auxiliares: progresso, detalhes de um ajuste e histórico/desfazer.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    /// <summary>Roda um trabalho em segundo plano mostrando progresso e log ao vivo.</summary>
    public class ProgressForm : Form
    {
        public delegate string Work(ProgressHandler progress, Action<string> log);

        readonly Work work;
        readonly Label current;
        readonly ProgressBar bar;
        readonly TextBox logBox;
        readonly FlatButton closeBtn, logBtn, restartBtn;
        readonly FlowLayoutPanel buttons;
        bool running = true;

        public string LogPath;
        public bool OfferRestart;

        public ProgressForm(string title, Work work)
        {
            this.work = work;
            Text = title;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body;
            BackColor = Theme.Surface;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(Theme.S(720), Theme.S(480));
            MinimumSize = new Size(Theme.S(520), Theme.S(360));
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            ShowIcon = false;

            Label head = new Label();
            head.Text = title; head.Font = Theme.Subtitle; head.ForeColor = Theme.Text;
            head.Dock = DockStyle.Top; head.Height = Theme.S(36); head.Padding = new Padding(Theme.S(16), Theme.S(10), 0, 0);

            current = new Label();
            current.Text = "Preparando…"; current.ForeColor = Theme.Muted;
            current.Dock = DockStyle.Top; current.Height = Theme.S(28); current.Padding = new Padding(Theme.S(16), Theme.S(4), Theme.S(16), 0);
            current.AutoEllipsis = true;

            Panel barWrap = new Panel();
            barWrap.Dock = DockStyle.Top; barWrap.Height = Theme.S(30); barWrap.Padding = new Padding(Theme.S(16), Theme.S(4), Theme.S(16), Theme.S(6));
            bar = new ProgressBar();
            bar.Dock = DockStyle.Fill; bar.Style = ProgressBarStyle.Continuous;
            barWrap.Controls.Add(bar);

            Panel logWrap = new Panel();
            logWrap.Dock = DockStyle.Fill; logWrap.Padding = new Padding(Theme.S(16), Theme.S(6), Theme.S(16), Theme.S(6));
            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill; logBox.BackColor = Theme.Back; logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.Font = new Font(FontFamily.GenericMonospace, 9f);
            logBox.WordWrap = true;
            logWrap.Controls.Add(logBox);

            buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom; buttons.Height = Theme.S(60);
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(Theme.S(12), Theme.S(12), Theme.S(12), Theme.S(12));
            closeBtn = new FlatButton("Fechar", true);
            closeBtn.Enabled = false;
            closeBtn.Click += delegate { Close(); };
            logBtn = new FlatButton("Abrir log", false);
            logBtn.Visible = false;
            logBtn.Click += delegate { if (LogPath != null && File.Exists(LogPath)) Process.Start("notepad.exe", "\"" + LogPath + "\""); };
            restartBtn = new FlatButton("Reiniciar agora", false);
            restartBtn.Visible = false;
            restartBtn.Click += delegate
            {
                if (MessageBox.Show(this, "Salve o que estiver aberto. Reiniciar o computador agora?", AppInfo.Name,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Process.Start("shutdown.exe", "/r /t 5");
            };
            buttons.Controls.Add(closeBtn);
            buttons.Controls.Add(logBtn);
            buttons.Controls.Add(restartBtn);

            Controls.Add(logWrap);
            Controls.Add(buttons);
            Controls.Add(barWrap);
            Controls.Add(current);
            Controls.Add(head);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ThreadPool.QueueUserWorkItem(delegate
            {
                string summary;
                try
                {
                    summary = work(Report, Log);
                }
                catch (Exception ex)
                {
                    summary = "Erro inesperado: " + ex.Message;
                }
                BeginInvoke((MethodInvoker)delegate { Finish(summary); });
            });
        }

        void Report(int done, int total, string message)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate
            {
                bar.Maximum = Math.Max(1, total);
                bar.Value = Math.Min(bar.Maximum, Math.Max(0, done));
                current.Text = string.Format("{0} de {1} — {2}", Math.Min(done + 1, total), total, message);
            });
        }

        void Log(string line)
        {
            if (!IsHandleCreated) return;
            BeginInvoke((MethodInvoker)delegate { logBox.AppendText(line.Replace("\n", Environment.NewLine) + Environment.NewLine); });
        }

        void Finish(string summary)
        {
            running = false;
            bar.Value = bar.Maximum;
            current.Text = summary;
            current.ForeColor = Theme.Text;
            current.Font = Theme.BodyBold;
            closeBtn.Enabled = true;
            logBtn.Visible = LogPath != null;
            restartBtn.Visible = OfferRestart;
            closeBtn.Focus();
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
    public class DetailsForm : Form
    {
        public DetailsForm(Tweak t)
        {
            Text = t.Title;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body;
            BackColor = Theme.Surface;
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
            sb.AppendLine("Preset: " + (t.Preset == 0 ? "nenhum (só manual)" : t.Preset == 1 ? "Básico, Rígido e Extremo" : t.Preset == 2 ? "Rígido e Extremo" : "só Extremo"));
            sb.AppendLine("Desfazer: " + (t.IsReversible ? "sim, pelo Histórico" : "não (ou só em parte)"));
            if (t.NeedsRestart) sb.AppendLine("Pede reinício ou novo login para valer.");
            if (t.ProOnly) sb.AppendLine("Só tem efeito no Windows Pro, Enterprise ou Education.");
            sb.AppendLine("Id: " + t.Id);
            sb.AppendLine();
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
            box.Dock = DockStyle.Fill; box.BorderStyle = BorderStyle.None; box.BackColor = Theme.Surface;
            box.Font = new Font(FontFamily.GenericMonospace, 9.5f);
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
    public class HistoryForm : Form
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
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Body;
            BackColor = Theme.Surface;
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
            list.Dock = DockStyle.Top; list.Height = Theme.S(200); list.BorderStyle = BorderStyle.FixedSingle;
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
            details.Dock = DockStyle.Fill; details.BackColor = Theme.Back; details.BorderStyle = BorderStyle.FixedSingle;
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
                if (s.Undone) it.ForeColor = Theme.Muted;
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
