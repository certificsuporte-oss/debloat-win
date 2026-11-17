// Janela principal: menu lateral, Dashboard, páginas de ajustes, Configurações.
// Só a camada visual mudou no design de 17/11/2026; Engine, Detector, catálogo e scripts são os mesmos.
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
        // ---- dados ----
        readonly Engine engine;
        readonly ISystem sys;
        Detector detector;
        readonly WinInfo win;
        readonly bool isAdmin;
        readonly List<Tweak> all;
        readonly List<TweakRow> rows = new List<TweakRow>();
        readonly SystemStats stats = new SystemStats();
        SystemSnapshot lastStats;

        // ---- páginas ----
        class ViewDef
        {
            public string Key, Title, Subtitle, Icon;
            public Predicate<Tweak> Filter;
            public Category[] Tabs;
        }
        readonly List<ViewDef> views = new List<ViewDef>();
        readonly Dictionary<string, NavItem> nav = new Dictionary<string, NavItem>();
        string currentView = "dashboard";
        Category currentTab = Category.Privacidade;
        string statusFilter = "todos";   // todos | pendentes | aplicados
        bool onlyNoUndo, onlyRisk;
        string search = "";

        // ---- controles ----
        Panel sidebar, main, listPage, listHeader, settingsPage, dashHost, actionBar;
        DashboardView dashboard;
        FlowLayoutPanel listPanel, chipsRow, tabsRow;
        Label viewTitle, viewSubtitle;
        SearchField searchField;
        TextBox searchBox;
        FilterChip chipAll, chipPending, chipApplied, chipNoUndo, chipRisk;
        FlatButton applyBtn, exportBtn, resultsBtn, markBtn, clearBtn, toolHistBtn, toolLogsBtn;
        Toast toast;
        Timer searchTimer, statsTimer;
        UserPreferenceChangedEventHandler systemThemeHandler;
        readonly ToolTip tips = new ToolTip();
        string selectionLine1 = "", selectionLine2 = "";

        public MainForm(ISystem sys) : this(sys, null) { }

        public MainForm(ISystem sys, string startupNotice)
        {
            this.sys = sys;
            Theme.Init(this);
            engine = new Engine(sys, Engine.DefaultDataDir());
            win = WinInfo.Detect(sys);
            isAdmin = IsAdministrator();
            all = Catalog.All();
            try
            {
                RegValue cpu = sys.ReadValue(@"HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
                if (cpu != null) stats.CpuName = cpu.Data;
            }
            catch { }

            Text = AppInfo.Name + " " + AppInfo.Version;
            BackColor = Theme.Back;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(1240), Theme.S(800));
            MinimumSize = new Size(Theme.S(980), Theme.S(640));
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            DefineViews();
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
                    using (DetailsForm d = new DetailsForm(((TweakRow)s).Tweak, detector)) d.ShowDialog(this);
                };
                rows.Add(r);
            }

            BuildSidebar();
            BuildMain();
            Controls.Add(main);
            Controls.Add(sidebar);

            detector = new Detector(sys);
            detector.DetectFast(ApplicableTweaks());
            UpdateStatuses();
            Shown += delegate { StartSlowDetection(); };

            statsTimer = new Timer();
            statsTimer.Interval = 2000;
            statsTimer.Tick += delegate { lastStats = stats.Read(); if (currentView == "dashboard") RefreshDashboard(); };
            lastStats = stats.Read();
            statsTimer.Start();

            SelectView("dashboard");
            UpdateSummary();
            if (startupNotice != null) Shown += delegate { ShowToast("info", "Debloat Win", startupNotice); };

            systemThemeHandler = delegate(object s, UserPreferenceChangedEventArgs e)
            {
                if (Theme.Mode != ThemeMode.Sistema || !IsHandleCreated) return;
                BeginInvoke((MethodInvoker)delegate { if (Theme.SetMode(ThemeMode.Sistema, false)) ApplyTheme(); });
            };
            try { SystemEvents.UserPreferenceChanged += systemThemeHandler; } catch { }
            FormClosed += delegate
            {
                statsTimer.Stop();
                try { SystemEvents.UserPreferenceChanged -= systemThemeHandler; } catch { }
            };
        }

        void DefineViews()
        {
            AddView("dashboard", "Dashboard", null, "dashboard", null, null);
            AddView("debloat", "Debloat", "Apps pré-instalados, IA e recursos que vêm ligados de fábrica.", "debloat",
                delegate(Tweak t) { return t.Category == Category.Apps; }, null);
            AddView("programas", "Programas", CategoryInfo.Subtitle(Category.Programas), "apps",
                delegate(Tweak t) { return t.Category == Category.Programas; }, null);
            AddView("servicos", "Serviços do Windows", "Ajustes que mudam serviços do Windows: telemetria, SysMain, indexação e serviços pouco usados.", "services",
                TweakRow.ChangesServices, null);
            AddView("limpeza", "Limpeza", "Temporários, cache do Windows Update e Lixeira. Rodam uma vez e não têm desfazer.", "clean",
                delegate(Tweak t) { return t.Category == Category.Ferramentas; }, null);
            AddView("otimizacoes", "Otimizações", "Privacidade, telemetria, Windows Update, desempenho, jogos e interface.", "optimize",
                delegate(Tweak t) { return t.Category == currentTab; },
                new Category[] { Category.Privacidade, Category.Telemetria, Category.Atualizacoes, Category.Desempenho, Category.Jogos, Category.Interface });
            AddView("ferramentas", "Ferramentas", "Drivers e atualizações (rodam sempre por último), histórico, desfazer e logs.", "tools",
                delegate(Tweak t) { return t.Category == Category.Finalizar; }, null);
            AddView("configuracoes", "Configurações", null, "settings", null, null);
        }

        void AddView(string key, string title, string subtitle, string icon, Predicate<Tweak> filter, Category[] tabs)
        {
            ViewDef v = new ViewDef();
            v.Key = key; v.Title = title; v.Subtitle = subtitle; v.Icon = icon; v.Filter = filter; v.Tabs = tabs;
            views.Add(v);
        }

        ViewDef View(string key) { return views.Find(delegate(ViewDef v) { return v.Key == key; }); }

        List<Tweak> ApplicableTweaks()
        {
            List<Tweak> l = new List<Tweak>();
            foreach (TweakRow r in rows) if (r.Applicable) l.Add(r.Tweak);
            return l;
        }

        void StartSlowDetection()
        {
            Detector d = detector;
            List<Tweak> list = ApplicableTweaks();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                d.DetectSlow(list, 240);
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (d != detector) return;
                        UpdateStatuses();
                        if (statusFilter != "todos") RefreshList();
                    });
            });
        }

        /// <summary>Verifica de novo o PC inteiro (depois de aplicar ou desfazer).</summary>
        void Redetect()
        {
            detector = new Detector(sys);
            detector.DetectFast(ApplicableTweaks());
            UpdateStatuses();
            if (statusFilter != "todos") RefreshList();
            StartSlowDetection();
        }

        void UpdateStatuses()
        {
            foreach (TweakRow r in rows)
                if (r.Applicable) r.Status = detector.Summarize(r.Tweak);
            UpdateSummary();
            UpdateChipCounts();
            RefreshDashboard();
            sidebar.Invalidate();
        }

        static bool IsAdministrator()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }

        // ==================================================================
        // Menu lateral
        void BuildSidebar()
        {
            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = Theme.S(256);
            sidebar.BackColor = Theme.Surface;
            sidebar.Paint += PaintSidebar;
            sidebar.Resize += delegate { sidebar.Invalidate(); };

            int y = Theme.S(84);
            foreach (ViewDef v in views)
            {
                NavItem item = new NavItem(v.Title, v.Key, v.Icon);
                item.Location = new Point(0, y);
                item.Width = sidebar.Width - 1;
                item.Click += delegate(object s, EventArgs e) { SelectView((string)((NavItem)s).Key); };
                nav[v.Key] = item;
                sidebar.Controls.Add(item);
                y += item.Height + Theme.S(4);
            }
        }

        int PendingCount()
        {
            int n = 0;
            foreach (TweakRow r in rows) if (r.Applicable && r.Tweak.Padrao && !r.AlreadyApplied) n++;
            return n;
        }

        void PaintSidebar(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);

            Rectangle logo = new Rectangle(Theme.S(20), Theme.S(22), Theme.S(38), Theme.S(38));
            Theme.FillRounded(g, logo, Theme.S(10), Theme.Accent, Color.Empty);
            Icons.Draw(g, "logo", Rectangle.Inflate(logo, -Theme.S(8), -Theme.S(8)), Color.White, 2.2f);
            TextRenderer.DrawText(g, AppInfo.Name, Theme.Subtitle, new Point(logo.Right + Theme.S(12), logo.Y + Theme.S(1)), Theme.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Versão " + AppInfo.Version, Theme.Small, new Point(logo.Right + Theme.S(12), logo.Y + Theme.S(22)), Theme.Muted, TextFormatFlags.NoPadding);

            // cartão de status do sistema
            int ch = Theme.S(100);
            Rectangle card = new Rectangle(Theme.S(14), sidebar.Height - ch - Theme.S(16), sidebar.Width - Theme.S(29), ch);
            Theme.FillRounded(g, card, Theme.S(10), Theme.Card, Theme.Border);
            int pending = PendingCount();
            int x = card.X + Theme.S(14), y = card.Y + Theme.S(12);
            using (SolidBrush b = new SolidBrush(pending > 0 ? Theme.Warning : Theme.Success))
                g.FillEllipse(b, x + Theme.S(3), y + Theme.S(5), Theme.S(8), Theme.S(8));
            TextRenderer.DrawText(g, pending > 0 ? pending + " itens pendentes" : "Padrão aplicado", Theme.SmallBold,
                new Point(x + Theme.S(20), y), Theme.Text, TextFormatFlags.NoPadding);
            y += Theme.S(26);
            Icons.Draw(g, "windows", new Rectangle(x, y, Theme.S(14), Theme.S(14)), Theme.Muted, 2f);
            TextRenderer.DrawText(g, ShortWindows(), Theme.Small, new Rectangle(x + Theme.S(20), y - Theme.S(1), card.Width - Theme.S(40), Theme.S(18)),
                Theme.TagFg, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            y += Theme.S(24);
            Icons.Draw(g, "shield", new Rectangle(x, y, Theme.S(14), Theme.S(14)), isAdmin ? Theme.Success : Theme.Danger, 2f);
            TextRenderer.DrawText(g, isAdmin ? "Administrador" : "Sem administrador", Theme.Small, new Point(x + Theme.S(20), y - Theme.S(1)),
                isAdmin ? Theme.RiskLowFg : Theme.RiskHighFg, TextFormatFlags.NoPadding);
        }

        string EditionName()
        {
            string ed = win.IsHome ? "Home" : win.Edition;
            if (ed.StartsWith("Professional", StringComparison.OrdinalIgnoreCase)) ed = "Pro";
            else if (ed.StartsWith("Enterprise", StringComparison.OrdinalIgnoreCase)) ed = "Enterprise";
            else if (ed.StartsWith("Education", StringComparison.OrdinalIgnoreCase)) ed = "Education";
            return ("Windows " + (win.Version == WinVersion.Win11 ? "11 " : "10 ") + ed).Trim();
        }

        string ShortWindows() { return (EditionName() + " " + win.DisplayVersion).Trim(); }

        // ==================================================================
        // Área principal
        void BuildMain()
        {
            main = new Panel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Theme.Back;

            // Dashboard (rola dentro do host)
            dashHost = new Panel();
            dashHost.Dock = DockStyle.Fill;
            dashHost.AutoScroll = true;
            dashHost.BackColor = Theme.Back;
            Native.Track(dashHost);
            dashboard = new DashboardView();
            dashboard.Location = new Point(0, 0);
            dashboard.Navigate += delegate(string key)
            {
                if (key == "history") ShowHistory();
                else if (key.StartsWith("otimizacoes:")) { currentTab = (Category)Enum.Parse(typeof(Category), key.Substring(12)); SelectView("otimizacoes"); }
                else { SelectView(key); if (key != "dashboard") SetStatusFilter("pendentes"); }
            };
            dashboard.ApplyButton.Click += delegate { ApplyPreset(); ApplySelected(); };
            dashboard.HistoryButton.Click += delegate { ShowHistory(); };
            dashHost.Controls.Add(dashboard);
            dashHost.ClientSizeChanged += delegate { FitDashboard(); };

            BuildListPage();
            BuildSettingsPage();
            BuildActionBar();

            toast = new Toast();
            main.Controls.Add(toast);
            main.Controls.Add(dashHost);
            main.Controls.Add(listPage);
            main.Controls.Add(settingsPage);
            main.Controls.Add(actionBar);
            main.Resize += delegate { PlaceToast(); };
        }

        void BuildListPage()
        {
            listPage = new Panel();
            listPage.Dock = DockStyle.Fill;
            listPage.BackColor = Theme.Back;

            listHeader = new Panel();
            listHeader.Dock = DockStyle.Top;
            listHeader.BackColor = Theme.Back;

            viewTitle = new Label();
            viewTitle.Font = Theme.Title;
            viewTitle.ForeColor = Theme.Text;
            viewTitle.AutoSize = true;
            viewTitle.Location = new Point(Theme.S(30), Theme.S(24));
            listHeader.Controls.Add(viewTitle);

            viewSubtitle = new Label();
            viewSubtitle.ForeColor = Theme.Muted;
            viewSubtitle.AutoSize = false;
            viewSubtitle.AutoEllipsis = true;
            viewSubtitle.Location = new Point(Theme.S(32), Theme.S(62));
            viewSubtitle.Size = new Size(Theme.S(500), Theme.S(22));
            listHeader.Controls.Add(viewSubtitle);

            searchField = new SearchField();
            searchField.Width = Theme.S(280);
            searchBox = searchField.Box;
            SetCue(searchBox, "Buscar ajuste (Ctrl+F)");
            Native.Track(searchBox);
            listHeader.Controls.Add(searchField);

            tabsRow = new FlowLayoutPanel();
            tabsRow.BackColor = Theme.Back;
            tabsRow.WrapContents = false;
            tabsRow.Height = Theme.S(40);
            listHeader.Controls.Add(tabsRow);

            chipsRow = new FlowLayoutPanel();
            chipsRow.BackColor = Theme.Back;
            chipsRow.WrapContents = false;
            chipsRow.Height = Theme.S(36);
            chipAll = Chip("Todos", "todos");
            chipPending = Chip("Pendentes", "pendentes");
            chipApplied = Chip("Já aplicados", "aplicados");
            Panel sep = new Panel();
            sep.Size = new Size(Theme.S(1), Theme.S(22));
            sep.Margin = new Padding(Theme.S(8), Theme.S(5), Theme.S(12), 0);
            sep.Paint += delegate(object s, PaintEventArgs e) { e.Graphics.Clear(Theme.Border); };
            chipsRow.Controls.Add(sep);
            chipNoUndo = Chip("Sem desfazer", "semdesfazer");
            chipRisk = Chip("Risco médio ou alto", "risco");
            listHeader.Controls.Add(chipsRow);

            toolHistBtn = new FlatButton("Histórico e desfazer", ButtonKind.Secondary, "history");
            toolHistBtn.Click += delegate { ShowHistory(); };
            toolLogsBtn = new FlatButton("Pasta de logs", ButtonKind.Secondary, "folder");
            toolLogsBtn.Click += delegate { OpenLogs(); };
            listHeader.Controls.Add(toolHistBtn);
            listHeader.Controls.Add(toolLogsBtn);

            listHeader.Resize += delegate { LayoutListHeader(); };

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
            listPanel.FlowDirection = FlowDirection.LeftToRight;
            listPanel.WrapContents = true;
            listPanel.AutoScroll = true;
            listPanel.BackColor = Theme.Back;
            listPanel.Padding = new Padding(Theme.S(30), Theme.S(4), Theme.S(18), Theme.S(20));
            listPanel.ClientSizeChanged += delegate { ResizeRows(); };
            Native.Track(listPanel);

            listPage.Controls.Add(listPanel);
            listPage.Controls.Add(listHeader);
        }

        FilterChip Chip(string text, string key)
        {
            FilterChip c = new FilterChip(text, key);
            c.Margin = new Padding(0, Theme.S(2), Theme.S(8), 0);
            c.Click += delegate(object s, EventArgs e)
            {
                string k = (string)((FilterChip)s).Key;
                if (k == "semdesfazer") { onlyNoUndo = !onlyNoUndo; RefreshList(); }
                else if (k == "risco") { onlyRisk = !onlyRisk; RefreshList(); }
                else SetStatusFilter(k);
            };
            chipsRow.Controls.Add(c);
            return c;
        }

        void SetStatusFilter(string k)
        {
            statusFilter = k;
            RefreshList();
        }

        void LayoutListHeader()
        {
            int w = listHeader.Width;
            searchField.Location = new Point(w - searchField.Width - Theme.S(30), Theme.S(28));
            int right = searchField.Left - Theme.S(16);
            viewSubtitle.Width = Math.Max(Theme.S(120), right - viewSubtitle.Left);
            int y = Theme.S(96);
            ViewDef v = View(currentView);
            tabsRow.Visible = v != null && v.Tabs != null && search.Length == 0;
            if (tabsRow.Visible)
            {
                tabsRow.SetBounds(Theme.S(30), y, w - Theme.S(60), Theme.S(40));
                y += Theme.S(52);
            }
            chipsRow.SetBounds(Theme.S(30), y, w - Theme.S(60), Theme.S(36));
            bool tools = currentView == "ferramentas" && search.Length == 0;
            toolHistBtn.Visible = toolLogsBtn.Visible = tools;
            if (tools)
            {
                int ty = searchField.Top + (searchField.Height - toolLogsBtn.Height) / 2;
                toolLogsBtn.Location = new Point(searchField.Left - Theme.S(12) - toolLogsBtn.Width, ty);
                toolHistBtn.Location = new Point(toolLogsBtn.Left - Theme.S(8) - toolHistBtn.Width, ty);
                viewSubtitle.Width = Math.Max(Theme.S(120), toolHistBtn.Left - Theme.S(16) - viewSubtitle.Left);
            }
            int h = y + Theme.S(36) + Theme.S(14);
            if (listHeader.Height != h) listHeader.Height = h;
        }

        void BuildTabs()
        {
            tabsRow.SuspendLayout();
            tabsRow.Controls.Clear();
            ViewDef v = View(currentView);
            if (v != null && v.Tabs != null)
                foreach (Category c in v.Tabs)
                {
                    TabButton t = new TabButton(CategoryInfo.Title(c), c);
                    t.Selected = c == currentTab;
                    t.Click += delegate(object s, EventArgs e) { currentTab = (Category)((TabButton)s).Key; BuildTabs(); RefreshList(); };
                    tabsRow.Controls.Add(t);
                }
            tabsRow.ResumeLayout();
        }

        void BuildSettingsPage()
        {
            settingsPage = new SettingsView(this);
            settingsPage.Dock = DockStyle.Fill;
        }

        void BuildActionBar()
        {
            actionBar = new Panel();
            actionBar.Dock = DockStyle.Bottom;
            actionBar.Height = Theme.S(70);
            actionBar.BackColor = Theme.Surface;
            actionBar.Paint += delegate(object s, PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, 0, actionBar.Width, 0);
                int right = markBtn.Left - Theme.S(16);
                TextRenderer.DrawText(g, selectionLine1, Theme.BodyBold, new Rectangle(Theme.S(30), Theme.S(15), right - Theme.S(30), Theme.S(22)),
                    Theme.Text, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, selectionLine2, Theme.Small, new Rectangle(Theme.S(30), Theme.S(38), right - Theme.S(30), Theme.S(20)),
                    Theme.Muted, TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };

            applyBtn = new FlatButton("Aplicar", ButtonKind.Primary, "play");
            applyBtn.Click += delegate { ApplySelected(); };
            if (!isAdmin) tips.SetToolTip(applyBtn, "Abra o programa como administrador.");
            exportBtn = new FlatButton("Exportar .ps1", ButtonKind.Secondary, "export");
            exportBtn.Click += delegate { ExportScript(); };
            tips.SetToolTip(exportBtn, "Salva um script PowerShell com os itens marcados, para rodar em outro PC.");
            resultsBtn = new FlatButton("Ver resultados", ButtonKind.Secondary, "history");
            resultsBtn.Click += delegate { ShowHistory(); };
            tips.SetToolTip(resultsBtn, "Histórico das sessões aplicadas, logs e desfazer.");
            clearBtn = new FlatButton("Desmarcar", ButtonKind.Ghost, null);
            clearBtn.Click += delegate { SetVisible(false); };
            markBtn = new FlatButton("Marcar pendentes", ButtonKind.Ghost, null);
            markBtn.Click += delegate { SetVisible(true); };
            tips.SetToolTip(markBtn, "Marca o que ainda falta nesta página.");
            foreach (Control c in new Control[] { applyBtn, exportBtn, resultsBtn, clearBtn, markBtn }) actionBar.Controls.Add(c);
            actionBar.Resize += delegate { LayoutActionBar(); };
        }

        void LayoutActionBar()
        {
            int top = (actionBar.Height - applyBtn.Height) / 2;
            int x = actionBar.Width - Theme.S(30);
            foreach (FlatButton b in new FlatButton[] { applyBtn, exportBtn, resultsBtn, clearBtn, markBtn })
            {
                x -= b.Width;
                b.Location = new Point(x, top);
                x -= Theme.S(8);
            }
            actionBar.Invalidate();
            PlaceToast();
        }

        void PlaceToast()
        {
            if (toast == null) return;
            int bottom = actionBar.Visible ? actionBar.Top : main.Height;
            toast.Location = new Point(main.Width - toast.Width - Theme.S(24), bottom - toast.Height - Theme.S(18));
        }

        public void ShowToast(string kind, string title, string message)
        {
            toast.Show(kind, title, message, 4500);
            PlaceToast();
            toast.BringToFront();
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

        // ==================================================================
        // Navegação
        void SelectView(string key)
        {
            if (View(key) == null) key = "dashboard";
            currentView = key;
            foreach (KeyValuePair<string, NavItem> kv in nav) kv.Value.Selected = kv.Key == key;
            bool isList = key != "dashboard" && key != "configuracoes";
            SuspendLayout();
            dashHost.Visible = key == "dashboard";
            settingsPage.Visible = key == "configuracoes";
            listPage.Visible = isList;
            actionBar.Visible = isList;
            if (isList)
            {
                if (searchBox.Text.Length > 0) { searchBox.Text = ""; search = ""; }
                statusFilter = "todos"; onlyNoUndo = false; onlyRisk = false;
                BuildTabs();
                RefreshList();
            }
            else if (key == "dashboard") RefreshDashboard();
            else ((SettingsView)settingsPage).Reload();
            ResumeLayout(true);
            PlaceToast();
        }

        /// <summary>Compatível com as prévias: aceita uma Category ou a chave de uma página.</summary>
        void SelectCategory(object key)
        {
            if (key is Category)
            {
                Category c = (Category)key;
                switch (c)
                {
                    case Category.Apps: SelectView("debloat"); return;
                    case Category.Programas: SelectView("programas"); return;
                    case Category.Ferramentas: SelectView("limpeza"); return;
                    case Category.Finalizar: SelectView("ferramentas"); return;
                    default: currentTab = c; SelectView("otimizacoes"); return;
                }
            }
            SelectView(key as string ?? "dashboard");
        }

        bool InView(TweakRow r)
        {
            Tweak t = r.Tweak;
            if (search.Length > 0)
            {
                string q = search.ToLowerInvariant();
                return t.Title.ToLowerInvariant().Contains(q) || t.Description.ToLowerInvariant().Contains(q) ||
                       t.Id.ToLowerInvariant().Contains(q) || (t.Warning != null && t.Warning.ToLowerInvariant().Contains(q));
            }
            ViewDef v = View(currentView);
            return v != null && v.Filter != null && v.Filter(t);
        }

        bool PassesFilters(TweakRow r)
        {
            if (statusFilter == "pendentes" && (!r.Applicable || r.AlreadyApplied)) return false;
            if (statusFilter == "aplicados" && !r.AlreadyApplied) return false;
            if (onlyNoUndo && r.Tweak.IsReversible) return false;
            if (onlyRisk && r.Tweak.Risk == Risk.Baixo) return false;
            return true;
        }

        void UpdateChipCounts()
        {
            if (chipAll == null) return;
            int total = 0, pending = 0, applied = 0;
            foreach (TweakRow r in rows)
            {
                if (!InView(r)) continue;
                total++;
                if (r.Applicable && !r.AlreadyApplied) pending++;
                if (r.AlreadyApplied) applied++;
            }
            chipAll.SetText("Todos · " + total);
            chipPending.SetText("Pendentes · " + pending);
            chipApplied.SetText("Já aplicados · " + applied);
            chipAll.Selected = statusFilter == "todos";
            chipPending.Selected = statusFilter == "pendentes";
            chipApplied.Selected = statusFilter == "aplicados";
            chipNoUndo.Selected = onlyNoUndo;
            chipRisk.Selected = onlyRisk;
        }

        void RefreshList()
        {
            if (currentView == "dashboard" || currentView == "configuracoes") { UpdateChipCounts(); return; }
            ViewDef v = View(currentView);
            if (search.Length > 0)
            {
                viewTitle.Text = "Busca";
                viewSubtitle.Text = "Resultados para \"" + search + "\" em todas as páginas.";
            }
            else
            {
                viewTitle.Text = v.Title;
                viewSubtitle.Text = v.Tabs != null ? CategoryInfo.Subtitle(currentTab) : v.Subtitle;
            }
            UpdateChipCounts();
            LayoutListHeader();

            listPanel.SuspendLayout();
            listPanel.Controls.Clear();
            bool grouped = search.Length > 0;
            string lastHeader = null;
            int shown = 0;
            List<Control> items = new List<Control>();
            foreach (TweakRow r in rows)
            {
                if (!InView(r) || !PassesFilters(r)) continue;
                string header = null;
                if (grouped) header = CategoryInfo.Title(r.Tweak.Category);
                if (r.Tweak.Group != null) header = grouped ? header + " · " + r.Tweak.Group : r.Tweak.Group;
                if (header != null && header != lastHeader)
                {
                    items.Add(new GroupHeader(header));
                    lastHeader = header;
                }
                items.Add(r);
                shown++;
            }
            if (shown == 0)
            {
                Label none = new Label();
                none.Text = statusFilter == "pendentes" ? "Nada pendente aqui: tudo já está aplicado neste PC." : "Nenhum item encontrado.";
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
            // reserva a largura da barra de rolagem vertical, para não aparecer barra horizontal
            int avail = listPanel.ClientSize.Width - listPanel.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth;
            if (avail < Theme.S(200)) return;
            int gap = Theme.S(12);
            int cols = avail >= Theme.S(820) ? 2 : 1;
            int cw = (avail - gap * (cols - 1)) / cols;
            listPanel.SuspendLayout();
            List<TweakRow> line = new List<TweakRow>();
            foreach (Control c in listPanel.Controls)
            {
                TweakRow r = c as TweakRow;
                if (r != null)
                {
                    line.Add(r);
                    if (line.Count == cols) { FinishLine(line, cw, gap); line.Clear(); }
                }
                else
                {
                    if (line.Count > 0) { FinishLine(line, cw, gap); line.Clear(); }
                    c.Width = avail;
                    listPanel.SetFlowBreak(c, true);
                }
            }
            if (line.Count > 0) FinishLine(line, cw, gap);
            listPanel.ResumeLayout();
        }

        void FinishLine(List<TweakRow> line, int cw, int gap)
        {
            int h = 0;
            foreach (TweakRow r in line) h = Math.Max(h, r.MeasureHeight(cw));
            for (int i = 0; i < line.Count; i++)
            {
                TweakRow r = line[i];
                r.LayoutForWidth(cw, h);
                r.Margin = new Padding(0, 0, i == line.Count - 1 ? 0 : gap, gap);
                listPanel.SetFlowBreak(r, i == line.Count - 1);
            }
        }

        void SetVisible(bool value)
        {
            foreach (Control c in listPanel.Controls)
            {
                TweakRow r = c as TweakRow;
                if (r != null && r.Applicable) r.Checked = value && !r.AlreadyApplied;
            }
        }

        void ApplyPreset()
        {
            int n = 0, done = 0;
            foreach (TweakRow r in rows)
            {
                bool inPreset = r.Applicable && r.Tweak.Padrao;
                if (inPreset && r.AlreadyApplied) done++;
                r.Checked = inPreset && !r.AlreadyApplied;
                if (r.Checked) n++;
            }
            ShowToast("info", "Padrão marcado", done > 0
                ? string.Format("{0} pendentes marcados; {1} já estavam aplicados.", n, done)
                : n + " itens marcados.");
        }

        // ==================================================================
        // Dashboard
        void RefreshDashboard()
        {
            if (dashboard == null) return;
            DashData d = new DashData();
            d.Machine = Environment.MachineName;
            d.Edition = EditionName();
            d.Version = (win.DisplayVersion + " · " + win.Build).Trim(' ', '·');
            foreach (TweakRow r in rows)
            {
                if (!r.Tweak.Padrao) continue;
                if (!r.Applicable) { d.OtherWindows++; continue; }
                if (r.AlreadyApplied) d.Applied++; else d.Pending++;
                if (r.Status != null && r.Status.State == TweakState.Checking) d.Checking = true;
                if (r.Tweak.Id == "fim.driverssdio")
                    d.DriverState = r.Status == null ? -1 : r.Status.State == TweakState.Applied ? 1 : r.Status.State == TweakState.NotApplied ? 0 : -1;
            }
            AddArea(d, "Debloat", "debloat", delegate(Tweak t) { return t.Category == Category.Apps; });
            foreach (Category c in new Category[] { Category.Privacidade, Category.Telemetria, Category.Atualizacoes, Category.Desempenho, Category.Jogos, Category.Interface })
            {
                Category cc = c;
                AddArea(d, CategoryInfo.Title(c), "otimizacoes:" + c, delegate(Tweak t) { return t.Category == cc; });
            }
            AddArea(d, "Limpeza", "limpeza", delegate(Tweak t) { return t.Category == Category.Ferramentas; });
            AddArea(d, "Programas", "programas", delegate(Tweak t) { return t.Category == Category.Programas; });
            AddArea(d, "Drivers e atualizações", "ferramentas", delegate(Tweak t) { return t.Category == Category.Finalizar; });
            try
            {
                foreach (SessionBackup s in engine.ListSessions())
                {
                    if (d.Activities.Count >= 4) break;
                    DashActivity a = new DashActivity();
                    int n = s.Tweaks.Count + s.Irreversible.Count;
                    if (n == 1) a.Title = s.Tweaks.Count == 1 ? s.Tweaks[0].TweakTitle : s.Irreversible[0];
                    else a.Title = n + " ajustes aplicados";
                    a.When = s.Date != null && s.Date.Length > 16 ? s.Date.Substring(0, 16) : s.Date;
                    a.Kind = s.Undone ? 1 : 0;
                    a.Pill = s.Undone ? "Desfeita" : "Concluída";
                    d.Activities.Add(a);
                }
            }
            catch { }
            d.Stats = lastStats;
            dashboard.SetData(d);
            FitDashboard();
        }

        /// <summary>Largura do Dashboard sem barra horizontal: desconta a barra vertical quando ela existe.</summary>
        void FitDashboard()
        {
            int w = dashHost.Width - (dashboard.Height > dashHost.Height ? SystemInformation.VerticalScrollBarWidth : 0);
            if (w > 0 && dashboard.Width != w) dashboard.Width = w;
        }

        void AddArea(DashData d, string name, string key, Predicate<Tweak> f)
        {
            DashArea a = new DashArea();
            a.Name = name; a.Key = key;
            foreach (TweakRow r in rows)
            {
                if (!r.Applicable || !f(r.Tweak)) continue;
                a.Total++;
                if (r.AlreadyApplied) a.Applied++;
            }
            if (a.Total > 0) d.Areas.Add(a);
        }

        // ==================================================================
        // Tema
        internal void ApplyTheme()
        {
            SuspendLayout();
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            sidebar.BackColor = Theme.Surface;
            foreach (Control c in new Control[] { main, dashHost, dashboard, listPage, listHeader, tabsRow, chipsRow, listPanel, settingsPage })
                c.BackColor = Theme.Back;
            actionBar.BackColor = Theme.Surface;
            viewTitle.ForeColor = Theme.Text;
            viewSubtitle.ForeColor = Theme.Muted;
            searchField.ApplyColors();
            ((SettingsView)settingsPage).ApplyColors();
            Native.TitleBar(this, Theme.Dark);
            Native.Control(listPanel, Theme.Dark);
            Native.Control(dashHost, Theme.Dark);
            Native.Control(searchBox, Theme.Dark);
            ResumeLayout(true);
            InvalidateAll(this);
            Refresh();
        }

        static void InvalidateAll(Control c)
        {
            c.Invalidate();
            foreach (Control k in c.Controls) InvalidateAll(k);
        }

        internal void SetTheme(ThemeMode m)
        {
            Theme.SetMode(m, true);
            ApplyTheme();
        }

        void CycleTheme()
        {
            SetTheme(Theme.Mode == ThemeMode.Escuro ? ThemeMode.Claro : Theme.Mode == ThemeMode.Claro ? ThemeMode.Sistema : ThemeMode.Escuro);
        }

        // ==================================================================
        List<Tweak> Selected()
        {
            List<Tweak> l = new List<Tweak>();
            foreach (TweakRow r in rows) if (r.Checked) l.Add(r.Tweak);
            return l;
        }

        bool RestorePointPref { get { return Theme.GetPref("PontoRestauracao", true); } }

        void UpdateSummary()
        {
            if (applyBtn == null) return;
            List<Tweak> sel = Selected();
            int irreversible = 0, high = 0, restart = 0;
            foreach (Tweak t in sel)
            {
                if (!t.IsReversible) irreversible++;
                if (t.Risk == Risk.Alto) high++;
                if (t.NeedsRestart) restart++;
            }
            if (sel.Count == 0)
            {
                selectionLine1 = "Nenhum item marcado";
                selectionLine2 = "Marque itens ou use \"Marcar pendentes\".";
            }
            else
            {
                selectionLine1 = sel.Count == 1 ? "1 item marcado" : sel.Count + " itens marcados";
                List<string> parts = new List<string>();
                if (irreversible > 0) parts.Add(irreversible + " sem desfazer");
                if (high > 0) parts.Add(high + " de risco alto");
                if (restart > 0) parts.Add("pede reinício");
                parts.Add(RestorePointPref ? "ponto de restauração antes" : "sem ponto de restauração");
                selectionLine2 = string.Join(" · ", parts.ToArray());
            }
            applyBtn.SetText(sel.Count > 0 ? "Aplicar " + sel.Count + (sel.Count == 1 ? " item" : " itens") : "Aplicar");
            applyBtn.Enabled = isAdmin && sel.Count > 0;
            exportBtn.Enabled = sel.Count > 0;
            clearBtn.Enabled = sel.Count > 0;
            LayoutActionBar();

            foreach (ViewDef v in views)
            {
                NavItem item;
                if (!nav.TryGetValue(v.Key, out item)) continue;
                if (v.Filter == null) { item.Count = ""; item.Invalidate(); continue; }
                int total = 0, marked = 0;
                foreach (TweakRow r in rows)
                {
                    bool inView = v.Tabs != null ? Array.IndexOf(v.Tabs, r.Tweak.Category) >= 0 : v.Filter(r.Tweak);
                    if (!inView || !r.Applicable) continue;
                    total++;
                    if (r.Checked) marked++;
                }
                item.Count = marked > 0 ? marked + "/" + total : total.ToString();
                item.Invalidate();
            }
            sidebar.Invalidate();
        }

        // ==================================================================
        void ApplySelected()
        {
            List<Tweak> sel = Selected();
            if (sel.Count == 0) return;
            if (!isAdmin)
            {
                ShowToast("err", "Sem administrador", "Abra o Debloat Win como administrador para aplicar.");
                return;
            }

            List<string> irr = new List<string>(), high = new List<string>();
            foreach (Tweak t in sel)
            {
                if (!t.IsReversible) irr.Add(t.Title);
                if (t.Risk == Risk.Alto) high.Add(t.Title);
            }
            bool wantRestore = RestorePointPref;
            StringBuilder body = new StringBuilder();
            if (irr.Count > 0) body.Append(irr.Count + " não têm desfazer (apps removidos, limpezas e instalações). ");
            body.Append(wantRestore ? "Um ponto de restauração será criado antes." : "ATENÇÃO: nenhum ponto de restauração será criado (veja Configurações).");
            if (sel.Exists(delegate(Tweak t) { return t.Category == Category.Finalizar; }))
                body.Append(" Drivers e Windows Update rodam por último e podem levar até 40 minutos.");
            StringBuilder det = new StringBuilder();
            if (high.Count > 0)
            {
                det.AppendLine("Risco alto:");
                foreach (string s in high) det.AppendLine("  • " + s);
                det.AppendLine();
            }
            if (irr.Count > 0)
            {
                det.AppendLine("Sem desfazer pelo programa (só pelo ponto de restauração):");
                foreach (string s in irr) det.AppendLine("  • " + s);
            }
            if (!ConfirmDialog.Ask(this, string.Format("Aplicar {0} {1} neste PC?", sel.Count, sel.Count == 1 ? "item" : "itens"),
                    body.ToString(), det.ToString(), "Aplicar", high.Count > 0 ? "alert" : "shield", high.Count > 0)) return;

            SessionResult result = null;
            ProgressForm pf = null;
            pf = new ProgressForm("Aplicando Padrão", delegate(ProgressHandler progress, Action<string> log)
            {
                string rpNote = null;
                if (wantRestore)
                {
                    progress(0, sel.Count + 1, "Criando ponto de restauração…");
                    log("Criando ponto de restauração…");
                    string msg;
                    bool ok = engine.CreateRestorePoint(out msg);
                    log((ok ? "[OK]     " : "[FALHA]  ") + "Ponto de restauração: " + msg);
                    pf.ReportResult("Ponto de restauração", ok ? ActionOutcome.Ok : ActionOutcome.Failed, msg);
                    rpNote = (ok ? "[OK]    " : "[FALHA] ") + "Ponto de restauração: " + msg;
                    if (!ok)
                    {
                        bool go = (bool)pf.Invoke((Func<bool>)delegate
                        {
                            return ConfirmDialog.Ask(pf, "Continuar sem ponto de restauração?", "Não foi possível criar o ponto de restauração: " + msg,
                                null, "Continuar", "alert", true);
                        });
                        if (!go) return "Cancelado. Nada foi alterado.";
                    }
                }
                engine.ExtraLogHeader = rpNote;
                engine.TweakDone = delegate(TweakResult tr)
                {
                    string head = tr.Outcome == ActionOutcome.Ok ? "[OK]     " : tr.Outcome == ActionOutcome.Skipped ? "[IGNOR.] " : "[FALHA]  ";
                    log(head + tr.Tweak.Title);
                    string detail = null;
                    if (tr.Outcome != ActionOutcome.Ok)
                        foreach (string m in tr.Messages)
                            if (!m.StartsWith("ok:"))
                            {
                                log("           " + m.Replace("\r", "").TrimEnd('\n').Replace("\n", "\n             "));
                                if (detail == null) detail = m;
                            }
                    pf.ReportResult(tr.Tweak.Title, tr.Outcome, detail);
                };
                result = engine.Apply(sel, false, win.ToString(), progress);
                engine.TweakDone = null;
                engine.ExtraLogHeader = null;
                pf.LogPath = result.LogPath;
                pf.OfferRestart = result.NeedsRestart || result.Count(ActionOutcome.Ok) > 0;
                return string.Format("Concluído: {0} aplicados, {1} ignorados, {2} com falha. Reinicie o PC para tudo valer.",
                    result.Count(ActionOutcome.Ok), result.Count(ActionOutcome.Skipped), result.Count(ActionOutcome.Failed));
            });
            pf.TotalItems = sel.Count;
            pf.ShowDialog(this);
            pf.Dispose();
            if (result != null)
            {
                int fails = result.Count(ActionOutcome.Failed);
                ShowToast(fails > 0 ? "err" : "ok", fails > 0 ? "Concluído com falhas" : "Concluído",
                    string.Format("{0} aplicados, {1} ignorados, {2} com falha. Reinicie o PC para tudo valer.",
                        result.Count(ActionOutcome.Ok), result.Count(ActionOutcome.Skipped), fails));
            }
            Redetect();
        }

        internal void ShowHistory()
        {
            using (HistoryForm h = new HistoryForm(engine)) h.ShowDialog(this);
            Redetect();
        }

        internal void OpenLogs()
        {
            try
            {
                Directory.CreateDirectory(engine.LogsFolder);
                Process.Start("explorer.exe", "\"" + engine.LogsFolder + "\"");
            }
            catch (Exception ex) { ShowToast("err", "Não abriu a pasta", ex.Message); }
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
                File.WriteAllText(d.FileName, ScriptExporter.Build(sel, RestorePointPref), new UTF8Encoding(true));
                ShowToast("ok", "Script salvo", sel.Count + " item(ns). No outro PC, rode como administrador: powershell -ExecutionPolicy Bypass -File \"" +
                    Path.GetFileName(d.FileName) + "\"");
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            {
                if (!listPage.Visible) SelectView("debloat");
                searchBox.Focus(); searchBox.SelectAll();
                return true;
            }
            if (keyData == Keys.Escape && searchBox.Focused && searchBox.Text.Length > 0) { searchBox.Text = ""; return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    /// <summary>Aba com sublinhado azul (usada em Otimizações).</summary>
    public class TabButton : Control
    {
        public object Key;
        bool selected, hover;

        public TabButton(string text, object key)
        {
            Text = text; Key = key;
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PageTab;
            AccessibleName = text;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Size = new Size(TextRenderer.MeasureText(text, Font).Width + Theme.S(28), Theme.S(40));
            Margin = new Padding(0);
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
            g.Clear(BackColor);
            using (Pen p = new Pen(Theme.Border)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
            Color fg = selected ? Theme.Text : hover ? Theme.TagFg : Theme.Muted;
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - Theme.S(3)), fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (selected)
                using (SolidBrush b = new SolidBrush(Theme.Bright)) g.FillRectangle(b, Theme.S(8), Height - Theme.S(3), Width - Theme.S(16), Theme.S(3));
            if (Focused && ShowFocusCues) Theme.DrawFocus(g, new Rectangle(Theme.S(2), Theme.S(4), Width - Theme.S(5), Height - Theme.S(10)), Theme.S(6));
        }
    }

    static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}
