// Janela principal.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using DebloatWin.Core;

namespace DebloatWin.App
{
    public class MainForm : ThemedForm
    {
        readonly Engine engine;
        readonly WinInfo win;
        readonly bool isAdmin;
        readonly List<Tweak> all;
        readonly List<TweakRow> rows = new List<TweakRow>();
        readonly Dictionary<object, NavItem> nav = new Dictionary<object, NavItem>();

        object currentCategory = "todos";   // "todos" ou Category
        string search = "";

        // controles
        Panel sidebar, content, header, bottom, contentTop;
        FlowLayoutPanel listPanel;
        Label catTitle, catSubtitle, summary, statusLine, titleLabel, verLabel;
        TextBox searchBox;
        SearchField searchField;
        CheckToggle restoreCheck;
        FlatButton themeBtn;
        LinkLabel clearLink;
        readonly List<Label> sideLabels = new List<Label>();
        UserPreferenceChangedEventHandler systemThemeHandler;
        FlatButton applyBtn, exportBtn;
        LinkLabel checkVisible, uncheckVisible;
        readonly FlatButton[] presetButtons = new FlatButton[3];
        static readonly string[] presetNames = { "Básico", "Rígido", "Extremo" };
        ToolTip tips = new ToolTip();
        Timer searchTimer;

        public MainForm(ISystem sys)
        {
            Theme.Init(this);
            engine = new Engine(sys, Engine.DefaultDataDir());
            win = WinInfo.Detect(sys);
            isAdmin = IsAdministrator();
            all = Catalog.All();

            Text = AppInfo.Name + " " + AppInfo.Version;
            BackColor = Theme.Back;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(1180), Theme.S(780));
            MinimumSize = new Size(Theme.S(900), Theme.S(600));
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildHeader();
            BuildSidebar();
            BuildBottom();
            BuildContent();

            Controls.Add(content);
            Controls.Add(bottom);
            Controls.Add(sidebar);
            Controls.Add(header);

            foreach (Tweak t in all)
            {
                string reason = null;
                bool ok = t.AppliesTo(win.Version);
                if (!ok) reason = t.Windows == WinVersion.Win11 ? "Só Windows 11" : "Só Windows 10";
                else if (t.ProOnly && win.IsHome) { ok = false; reason = "Não funciona no Home"; }
                TweakRow r = new TweakRow(t, ok, reason);
                r.CheckedChanged += delegate { UpdateSummary(); };
                r.DetailsRequested += delegate(object s, EventArgs e)
                {
                    using (DetailsForm d = new DetailsForm(((TweakRow)s).Tweak)) d.ShowDialog(this);
                };
                rows.Add(r);
            }

            // quantos ajustes de cada preset valem neste Windows
            for (int i = 0; i < 3; i++)
            {
                int n = 0;
                foreach (TweakRow r in rows) if (r.Applicable && r.Tweak.InPreset(i + 1)) n++;
                presetButtons[i].Text = presetNames[i] + "  ·  " + n + " ajustes";
            }

            SelectCategory("todos");
            UpdateSummary();

            // No modo "Sistema", acompanha o Windows quando o usuário troca claro/escuro
            systemThemeHandler = delegate(object s, UserPreferenceChangedEventArgs e)
            {
                if (Theme.Mode != ThemeMode.Sistema || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate
                {
                    if (Theme.SetMode(ThemeMode.Sistema, false)) ApplyTheme();
                });
            };
            try { SystemEvents.UserPreferenceChanged += systemThemeHandler; } catch { }
            FormClosed += delegate { try { SystemEvents.UserPreferenceChanged -= systemThemeHandler; } catch { } };
        }

        /// <summary>Reaplica as cores do tema atual em toda a janela, sem reabrir o programa.</summary>
        void ApplyTheme()
        {
            SuspendLayout();
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            header.BackColor = Theme.Surface;
            titleLabel.ForeColor = Theme.Text;
            verLabel.ForeColor = Theme.Muted;
            statusLine.ForeColor = isAdmin ? Theme.Muted : Theme.RiskHighFg;
            sidebar.BackColor = Theme.Surface;
            foreach (Label l in sideLabels) l.ForeColor = Theme.Muted;
            content.BackColor = Theme.Back;
            contentTop.BackColor = Theme.Back;
            catTitle.ForeColor = Theme.Text;
            catSubtitle.ForeColor = Theme.Muted;
            foreach (LinkLabel l in new LinkLabel[] { clearLink, checkVisible, uncheckVisible })
            {
                l.LinkColor = Theme.Link; l.ActiveLinkColor = Theme.AccentHover; l.VisitedLinkColor = Theme.Link;
            }
            searchField.ApplyColors();
            listPanel.BackColor = Theme.Back;
            bottom.BackColor = Theme.Surface;
            summary.ForeColor = Theme.Text;
            themeBtn.Text = Theme.ModeLabel(Theme.Mode);
            Native.TitleBar(this, Theme.Dark);
            Native.Control(listPanel, Theme.Dark);
            Native.Control(searchBox, Theme.Dark);
            ResumeLayout(true);
            foreach (TweakRow r in rows) r.Invalidate();
            Refresh();
        }

        void CycleTheme()
        {
            ThemeMode next = Theme.Mode == ThemeMode.Sistema ? ThemeMode.Escuro
                           : Theme.Mode == ThemeMode.Escuro ? ThemeMode.Claro : ThemeMode.Sistema;
            Theme.SetMode(next, true);
            ApplyTheme();
        }

        static bool IsAdministrator()
        {
            try
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------
        void BuildHeader()
        {
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = Theme.S(64);
            header.BackColor = Theme.Surface;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            Label title = new Label();
            titleLabel = title;
            title.Text = AppInfo.Name;
            title.Font = Theme.Title;
            title.ForeColor = Theme.Text;
            title.AutoSize = true;
            title.Location = new Point(Theme.S(20), Theme.S(16));
            header.Controls.Add(title);

            Label ver = new Label();
            verLabel = ver;
            ver.Text = "v" + AppInfo.Version;
            ver.Font = Theme.Small;
            ver.ForeColor = Theme.Muted;
            ver.AutoSize = true;
            header.Controls.Add(ver);
            header.Layout += delegate { ver.Location = new Point(title.Right + Theme.S(6), title.Top + Theme.S(10)); };

            statusLine = new Label();
            statusLine.AutoSize = false;
            statusLine.TextAlign = ContentAlignment.MiddleRight;
            statusLine.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            statusLine.Size = new Size(Theme.S(520), Theme.S(40));
            statusLine.Location = new Point(header.Width - Theme.S(540), Theme.S(12));
            statusLine.ForeColor = isAdmin ? Theme.Muted : Theme.RiskHighFg;
            statusLine.Text = win.ToString() + "   •   " + (isAdmin ? "Administrador" : "SEM permissão de administrador");
            header.Controls.Add(statusLine);

            themeBtn = new FlatButton("Tema: Sistema", false);
            themeBtn.Font = Theme.Body;
            themeBtn.Width = Theme.S(150);
            themeBtn.Height = Theme.S(32);
            themeBtn.Text = Theme.ModeLabel(Theme.Mode);
            themeBtn.Click += delegate { CycleTheme(); };
            tips.SetToolTip(themeBtn, "Alterna entre seguir o Windows, escuro e claro.");
            header.Controls.Add(themeBtn);

            header.Resize += delegate
            {
                themeBtn.Location = new Point(header.Width - themeBtn.Width - Theme.S(20), (header.Height - themeBtn.Height) / 2);
                statusLine.Left = themeBtn.Left - statusLine.Width - Theme.S(14);
            };
        }

        void BuildSidebar()
        {
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = Theme.S(250);
            sidebar.BackColor = Theme.Surface;
            sidebar.Padding = new Padding(0, Theme.S(12), 0, Theme.S(12));
            sidebar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            };

            int y = Theme.S(12);
            y = SideLabel("PRESETS", y);
            string[] desc = {
                "Só ajustes seguros: privacidade, anúncios, telemetria e manutenção leve. Bom ponto de partida para qualquer PC.",
                "Básico + remove apps pré-instalados, IA do Windows, mais privacidade e desempenho.",
                "Tudo do Rígido + ajustes agressivos (Xbox, OneDrive, serviços, efeitos visuais). Revise antes de aplicar." };
            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                FlatButton b = new FlatButton(presetNames[i], false);
                presetButtons[i] = b;
                b.Font = Theme.Body;
                b.Location = new Point(Theme.S(16), y);
                b.Width = Theme.S(218);
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.Click += delegate { ApplyPreset(level); };
                tips.SetToolTip(b, desc[i]);
                sidebar.Controls.Add(b);
                y += b.Height + Theme.S(6);
            }
            LinkLabel clear = new LinkLabel();
            clearLink = clear;
            clear.Text = "Limpar seleção";
            clear.AutoSize = true;
            clear.LinkColor = Theme.Link;
            clear.Location = new Point(Theme.S(18), y + Theme.S(2));
            clear.LinkClicked += delegate { foreach (TweakRow r in rows) r.Checked = false; Flash("Seleção limpa."); };
            sidebar.Controls.Add(clear);
            y += Theme.S(34);

            y = SideLabel("CATEGORIAS", y);
            NavItem allItem = new NavItem("Todos os ajustes", "todos");
            AddNav(allItem, ref y);
            foreach (Category c in Enum.GetValues(typeof(Category)))
                AddNav(new NavItem(CategoryInfo.Title(c), c), ref y);

            FlatButton hist = new FlatButton("Histórico e desfazer", false);
            hist.Font = Theme.Body;
            hist.Width = Theme.S(218);
            hist.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            hist.Location = new Point(Theme.S(16), Theme.S(700));
            hist.Click += delegate
            {
                using (HistoryForm h = new HistoryForm(engine)) h.ShowDialog(this);
            };
            sidebar.Controls.Add(hist);
            sidebar.Resize += delegate { hist.Top = sidebar.Height - hist.Height - Theme.S(16); };
        }

        int SideLabel(string text, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.SmallBold;
            l.ForeColor = Theme.Muted;
            l.AutoSize = true;
            l.Location = new Point(Theme.S(20), y);
            sidebar.Controls.Add(l);
            sideLabels.Add(l);
            return y + Theme.S(26);
        }

        void AddNav(NavItem item, ref int y)
        {
            item.Location = new Point(0, y);
            item.Width = sidebar.Width - 1;
            item.Click += delegate(object s, EventArgs e) { SelectCategory(((NavItem)s).Key); };
            nav[item.Key] = item;
            sidebar.Controls.Add(item);
            y += item.Height + Theme.S(2);
        }

        void BuildBottom()
        {
            bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = Theme.S(68);
            bottom.BackColor = Theme.Surface;
            bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 0, 0, bottom.Width, 0);
            };

            summary = new Label();
            summary.AutoSize = false;
            summary.Location = new Point(Theme.S(20), Theme.S(12));
            summary.Size = new Size(Theme.S(520), Theme.S(46));
            summary.ForeColor = Theme.Text;
            summary.TextAlign = ContentAlignment.MiddleLeft;
            summary.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            bottom.Controls.Add(summary);

            applyBtn = new FlatButton("Aplicar", true);
            applyBtn.Width = Theme.S(150);
            applyBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            applyBtn.Click += delegate { ApplySelected(); };
            applyBtn.Enabled = isAdmin;
            if (!isAdmin) tips.SetToolTip(applyBtn, "Abra o programa como administrador.");
            bottom.Controls.Add(applyBtn);

            exportBtn = new FlatButton("Exportar .ps1", false);
            exportBtn.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            exportBtn.Click += delegate { ExportScript(); };
            tips.SetToolTip(exportBtn, "Salva um script PowerShell com os ajustes marcados, para rodar em outro PC.");
            bottom.Controls.Add(exportBtn);

            restoreCheck = new CheckToggle("Criar ponto de restauração antes");
            restoreCheck.Checked = true;
            restoreCheck.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            bottom.Controls.Add(restoreCheck);

            bottom.Resize += delegate
            {
                int top = (bottom.Height - applyBtn.Height) / 2;
                applyBtn.Location = new Point(bottom.Width - applyBtn.Width - Theme.S(20), top);
                exportBtn.Location = new Point(applyBtn.Left - exportBtn.Width - Theme.S(10), top);
                restoreCheck.Location = new Point(exportBtn.Left - restoreCheck.Width - Theme.S(18), (bottom.Height - restoreCheck.Height) / 2);
                summary.Width = Math.Max(Theme.S(100), restoreCheck.Left - summary.Left - Theme.S(10));
            };
        }

        void BuildContent()
        {
            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Theme.Back;

            Panel top = new Panel();
            contentTop = top;
            top.Dock = DockStyle.Top;
            top.Height = Theme.S(92);
            top.BackColor = Theme.Back;

            catTitle = new Label();
            catTitle.Font = Theme.Title;
            catTitle.ForeColor = Theme.Text;
            catTitle.AutoSize = true;
            catTitle.Location = new Point(Theme.S(24), Theme.S(14));
            top.Controls.Add(catTitle);

            catSubtitle = new Label();
            catSubtitle.ForeColor = Theme.Muted;
            catSubtitle.AutoSize = false;
            catSubtitle.AutoEllipsis = true;
            catSubtitle.Location = new Point(Theme.S(26), Theme.S(50));
            catSubtitle.Size = new Size(Theme.S(500), Theme.S(22));
            catSubtitle.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            top.Controls.Add(catSubtitle);

            searchField = new SearchField();
            searchField.Width = Theme.S(260);
            searchField.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchBox = searchField.Box;
            SetCue(searchBox, "Buscar ajuste (Ctrl+F)");
            Native.Track(searchBox);
            top.Controls.Add(searchField);

            checkVisible = new LinkLabel();
            checkVisible.Text = "Marcar todos";
            checkVisible.AutoSize = true;
            checkVisible.LinkColor = Theme.Link;
            checkVisible.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            checkVisible.LinkClicked += delegate { SetVisible(true); };
            top.Controls.Add(checkVisible);

            uncheckVisible = new LinkLabel();
            uncheckVisible.Text = "Desmarcar";
            uncheckVisible.AutoSize = true;
            uncheckVisible.LinkColor = Theme.Link;
            uncheckVisible.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            uncheckVisible.LinkClicked += delegate { SetVisible(false); };
            top.Controls.Add(uncheckVisible);

            top.Resize += delegate
            {
                searchField.Location = new Point(top.Width - searchField.Width - Theme.S(28), Theme.S(14));
                uncheckVisible.Location = new Point(top.Width - uncheckVisible.Width - Theme.S(28), Theme.S(54));
                checkVisible.Location = new Point(uncheckVisible.Left - checkVisible.Width - Theme.S(12), Theme.S(54));
                catSubtitle.Width = Math.Max(Theme.S(100), checkVisible.Left - catSubtitle.Left - Theme.S(16));
            };

            searchTimer = new Timer();
            searchTimer.Interval = 250;
            searchTimer.Tick += delegate
            {
                searchTimer.Stop();
                search = searchBox.Text.Trim();
                RefreshList();
            };
            searchBox.TextChanged += delegate { searchTimer.Stop(); searchTimer.Start(); };

            listPanel = new FlowLayoutPanel();
            listPanel.Dock = DockStyle.Fill;
            listPanel.FlowDirection = FlowDirection.TopDown;
            listPanel.WrapContents = false;
            listPanel.AutoScroll = true;
            listPanel.BackColor = Theme.Back;
            listPanel.Padding = new Padding(Theme.S(24), Theme.S(4), Theme.S(16), Theme.S(16));
            listPanel.ClientSizeChanged += delegate { ResizeRows(); };
            Native.Track(listPanel);

            content.Controls.Add(listPanel);
            content.Controls.Add(top);
        }

        static void SetCue(TextBox box, string cue)
        {
            // EM_SETCUEBANNER: texto de dica dentro da caixa (só no Windows)
            box.HandleCreated += delegate
            {
                try { NativeMethods.SendMessage(box.Handle, 0x1501, (IntPtr)1, cue); }
                catch { }
            };
        }

        // ------------------------------------------------------------------
        void SelectCategory(object key)
        {
            currentCategory = key;
            foreach (KeyValuePair<object, NavItem> kv in nav) kv.Value.Selected = kv.Key.Equals(key);
            if (searchBox.Text.Length > 0) { searchBox.Text = ""; search = ""; }
            RefreshList();
        }

        bool Matches(Tweak t)
        {
            if (search.Length > 0)
            {
                string q = search.ToLowerInvariant();
                return t.Title.ToLowerInvariant().Contains(q) || t.Description.ToLowerInvariant().Contains(q) ||
                       t.Id.ToLowerInvariant().Contains(q) || (t.Warning != null && t.Warning.ToLowerInvariant().Contains(q));
            }
            return currentCategory.Equals("todos") || currentCategory.Equals(t.Category);
        }

        void RefreshList()
        {
            if (search.Length > 0)
            {
                catTitle.Text = "Busca";
                catSubtitle.Text = "Resultados para \"" + search + "\" em todas as categorias.";
            }
            else if (currentCategory.Equals("todos"))
            {
                catTitle.Text = "Todos os ajustes";
                catSubtitle.Text = all.Count + " ajustes. Marque um a um ou use um preset no menu ao lado.";
            }
            else
            {
                Category c = (Category)currentCategory;
                catTitle.Text = CategoryInfo.Title(c);
                catSubtitle.Text = CategoryInfo.Subtitle(c);
            }

            listPanel.SuspendLayout();
            listPanel.Controls.Clear();
            bool grouped = search.Length > 0 || currentCategory.Equals("todos");
            Category? lastCat = null;
            int shown = 0;
            List<Control> items = new List<Control>();
            foreach (TweakRow r in rows)
            {
                if (!Matches(r.Tweak)) continue;
                if (grouped && (lastCat == null || lastCat.Value != r.Tweak.Category))
                {
                    items.Add(new GroupHeader(CategoryInfo.Title(r.Tweak.Category)));
                    lastCat = r.Tweak.Category;
                }
                items.Add(r);
                shown++;
            }
            if (shown == 0)
            {
                Label none = new Label();
                none.Text = "Nenhum ajuste encontrado.";
                none.ForeColor = Theme.Muted;
                none.AutoSize = true;
                none.Margin = new Padding(0, Theme.S(16), 0, 0);
                items.Add(none);
            }
            listPanel.Controls.AddRange(items.ToArray());
            ResizeRows();
            listPanel.ResumeLayout(true);
            listPanel.AutoScrollPosition = new Point(0, 0);
        }

        void ResizeRows()
        {
            // reserva sempre a largura da barra de rolagem vertical, para não aparecer barra horizontal
            int w = listPanel.Width - listPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - Theme.S(2);
            if (w < Theme.S(200)) return;
            listPanel.SuspendLayout();
            foreach (Control c in listPanel.Controls)
            {
                TweakRow r = c as TweakRow;
                if (r != null) { if (r.Width != w) r.LayoutForWidth(w); }
                else if (c is GroupHeader) c.Width = w;
            }
            listPanel.ResumeLayout();
        }

        void SetVisible(bool value)
        {
            foreach (Control c in listPanel.Controls)
            {
                TweakRow r = c as TweakRow;
                if (r != null && r.Applicable) r.Checked = value;
            }
        }

        void ApplyPreset(int level)
        {
            int n = 0;
            foreach (TweakRow r in rows)
            {
                r.Checked = r.Applicable && r.Tweak.InPreset(level);
                if (r.Checked) n++;
            }
            string name = level == 1 ? "Básico" : level == 2 ? "Rígido" : "Extremo";
            Flash("Preset " + name + " marcado. Revise e clique em Aplicar.");
        }

        string flashText;
        void Flash(string text)
        {
            flashText = text;
            UpdateSummary();
            flashText = null;
        }

        List<Tweak> Selected()
        {
            List<Tweak> l = new List<Tweak>();
            foreach (TweakRow r in rows) if (r.Checked) l.Add(r.Tweak);
            return l;
        }

        void UpdateSummary()
        {
            List<Tweak> sel = Selected();
            int irreversible = 0, high = 0, restart = 0;
            foreach (Tweak t in sel)
            {
                if (!t.IsReversible) irreversible++;
                if (t.Risk == Risk.Alto) high++;
                if (t.NeedsRestart) restart++;
            }
            StringBuilder sb = new StringBuilder();
            if (flashText != null) sb.AppendLine(flashText);
            if (sel.Count == 0) sb.Append("Nenhum ajuste marcado.");
            else
            {
                sb.Append(sel.Count == 1 ? "1 ajuste marcado" : sel.Count + " ajustes marcados");
                if (irreversible > 0) sb.Append("  •  " + irreversible + " sem desfazer");
                if (high > 0) sb.Append("  •  " + high + " de risco alto");
                if (restart > 0) sb.Append("  •  pede reinício");
            }
            summary.Text = sb.ToString();
            applyBtn.Enabled = isAdmin && sel.Count > 0;
            exportBtn.Enabled = sel.Count > 0;

            // contadores do menu lateral
            Dictionary<object, int[]> counts = new Dictionary<object, int[]>();
            counts["todos"] = new int[2];
            foreach (Category c in Enum.GetValues(typeof(Category))) counts[c] = new int[2];
            foreach (TweakRow r in rows)
            {
                counts["todos"][1]++;
                counts[r.Tweak.Category][1]++;
                if (r.Checked) { counts["todos"][0]++; counts[r.Tweak.Category][0]++; }
            }
            foreach (KeyValuePair<object, NavItem> kv in nav)
            {
                int[] v = counts[kv.Key];
                kv.Value.Count = v[0] > 0 ? v[0] + "/" + v[1] : v[1].ToString();
                kv.Value.Invalidate();
            }
        }

        // ------------------------------------------------------------------
        void ApplySelected()
        {
            List<Tweak> sel = Selected();
            if (sel.Count == 0) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Format("Aplicar {0} ajuste(s) neste computador?", sel.Count));
            List<string> irr = new List<string>(), high = new List<string>();
            foreach (Tweak t in sel)
            {
                if (!t.IsReversible) irr.Add(t.Title);
                if (t.Risk == Risk.Alto) high.Add(t.Title);
            }
            if (high.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Risco alto:");
                foreach (string s in high) sb.AppendLine("  • " + s);
            }
            if (irr.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Sem desfazer pelo programa (só pelo ponto de restauração):");
                int n = 0;
                foreach (string s in irr) { if (n++ < 12) sb.AppendLine("  • " + s); }
                if (irr.Count > 12) sb.AppendLine("  … e mais " + (irr.Count - 12));
            }
            sb.AppendLine();
            sb.AppendLine(restoreCheck.Checked ? "Um ponto de restauração será criado antes." : "ATENÇÃO: nenhum ponto de restauração será criado.");
            if (MessageBox.Show(this, sb.ToString(), AppInfo.Name, MessageBoxButtons.YesNo,
                    high.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes) return;

            bool wantRestore = restoreCheck.Checked;
            SessionResult result = null;
            ProgressForm pf = null;
            pf = new ProgressForm("Aplicando ajustes", delegate(ProgressHandler progress, Action<string> log)
            {
                string rpNote = null;
                if (wantRestore)
                {
                    progress(0, sel.Count + 1, "Criando ponto de restauração…");
                    log("Criando ponto de restauração…");
                    string msg;
                    bool ok = engine.CreateRestorePoint(out msg);
                    log((ok ? "[OK] " : "[FALHA] ") + msg);
                    rpNote = (ok ? "[OK]    " : "[FALHA] ") + "Ponto de restauração: " + msg;
                    if (!ok)
                    {
                        bool go = (bool)pf.Invoke((Func<bool>)delegate
                        {
                            return MessageBox.Show(pf, "Não foi possível criar o ponto de restauração:\n\n" + msg +
                                "\n\nContinuar mesmo assim?", AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
                        });
                        if (!go) return "Cancelado. Nada foi alterado.";
                    }
                }
                engine.ExtraLogHeader = rpNote;
                engine.TweakDone = delegate(TweakResult tr)
                {
                    string head = tr.Outcome == ActionOutcome.Ok ? "[OK]     " : tr.Outcome == ActionOutcome.Skipped ? "[IGNOR.] " : "[FALHA]  ";
                    log(head + tr.Tweak.Title);
                    if (tr.Outcome != ActionOutcome.Ok)
                        foreach (string m in tr.Messages) if (!m.StartsWith("ok:")) log("           " + m);
                };
                result = engine.Apply(sel, false, win.ToString(), progress);
                engine.TweakDone = null;
                engine.ExtraLogHeader = null;
                pf.LogPath = result.LogPath;
                pf.OfferRestart = result.NeedsRestart || result.Count(ActionOutcome.Ok) > 0;
                return string.Format("Concluído: {0} aplicados, {1} ignorados, {2} com falha. Reinicie o PC para tudo valer.",
                    result.Count(ActionOutcome.Ok), result.Count(ActionOutcome.Skipped), result.Count(ActionOutcome.Failed));
            });
            pf.ShowDialog(this);
            pf.Dispose();
        }

        void ExportScript()
        {
            List<Tweak> sel = Selected();
            if (sel.Count == 0) return;
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "Script PowerShell (*.ps1)|*.ps1";
                d.FileName = "debloat-win-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".ps1";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(d.FileName, ScriptExporter.Build(sel, restoreCheck.Checked), new UTF8Encoding(true));
                MessageBox.Show(this, "Script salvo com " + sel.Count + " ajuste(s).\n\nNo outro PC, abra o PowerShell como administrador e rode:\n" +
                    "powershell -ExecutionPolicy Bypass -File \"" + Path.GetFileName(d.FileName) + "\"",
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { searchBox.Focus(); searchBox.SelectAll(); return true; }
            if (keyData == Keys.Escape && searchBox.Focused && searchBox.Text.Length > 0) { searchBox.Text = ""; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}
