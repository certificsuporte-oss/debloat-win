// Prévia da tela no Linux (Mono + tela virtual), com o sistema simulado. Gera PNGs.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using DebloatWin.App;
using DebloatWin.Core;
using DebloatWin.Tests;

static class PreviewProgram
{
    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        FakeSystem sys = new FakeSystem();
        fakeSys = sys;
        const string nt = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        sys.WriteValue(nt, "CurrentBuildNumber", RegKind.String, args.Length > 1 ? args[1] : "19045");
        sys.WriteValue(nt, "DisplayVersion", RegKind.String, args.Length > 1 && args[1] != "19045" ? "24H2" : "22H2");
        sys.WriteValue(nt, "EditionID", RegKind.String, "Professional");

        // PC simulado com alguns ajustes já feitos (à mão ou por outra ferramenta)
        sys.WriteValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", RegKind.DWord, "0");
        sys.WriteValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", RegKind.DWord, "0");
        sys.WriteValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", RegKind.DWord, "1");
        sys.WriteValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", RegKind.DWord, "0");
        sys.WriteValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", RegKind.DWord, "1");
        sys.AddService("DiagTrack", 4); sys.AddService("MapsBroker", 2); sys.AddService("PcaSvc", 3);
        sys.PowerShellHandler = delegate(string sc)
        {
            ProcResult pr = new ProcResult();
            if (!sc.Contains("Get-DbwAppx")) { pr.Output = "ok"; return pr; }
            System.Text.StringBuilder o = new System.Text.StringBuilder();
            int n = 0;
            foreach (string line in sc.Split('\n')) if (line.StartsWith("try { $r =")) n++;
            for (int i = 0; i < n; i++) o.AppendLine("DET:" + i + ":" + (i % 3 == 0 ? "1" : i % 3 == 1 ? "0" : "NA"));
            pr.Output = o.ToString();
            return pr;
        };

        sys.WriteValue(@"HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", RegKind.String, "Intel(R) Core(TM) i5-6500 CPU @ 3.20GHz");
        SystemStats.Override = delegate
        {
            SystemSnapshot ss = new SystemSnapshot();
            ss.CpuName = "Intel(R) Core(TM) i5-6500 CPU @ 3.20GHz"; ss.CpuPercent = 14;
            ss.RamTotalGb = 7.9; ss.RamUsedGb = 3.1; ss.DiskTotalGb = 237; ss.DiskFreeGb = 141; ss.DiskName = "C:";
            return ss;
        };
        Application.EnableVisualStyles();
        if (args.Length > 2) Theme.SetMode(args[2] == "escuro" ? ThemeMode.Escuro : ThemeMode.Claro, false);
        MainForm f = new MainForm(sys);
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(0, 0);
        Timer t = new Timer();
        int step = 0;
        t.Interval = 900;
        t.Tick += delegate
        {
            try
            {
                switch (step++)
                {
                    case 0: Shot(f, outDir + "/1-dashboard.png");
                            if (args.Length > 3 && args[3] == "alternar") { f.GetType().GetMethod("CycleTheme", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(f, null); }
                            Call(f, "SelectCategory", Category.Apps); f.GetType().GetMethod("ApplyPreset", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(f, null); break;
                    case 1: Shot(f, outDir + "/2-debloat.png"); Call(f, "SelectCategory", Category.Desempenho); break;
                    case 2: Shot(f, outDir + "/3-otimizacoes.png"); Call(f, "SelectCategory", "configuracoes"); break;
                    case 3: Shot(f, outDir + "/4-configuracoes.png"); Call(f, "SelectCategory", "ferramentas");
                            break;
                    case 4: Shot(f, outDir + "/5-ferramentas.png");
                            Tweak tw = Catalog.All().Find(delegate(Tweak x) { return x.Id == "perf.ultimate"; });
                            DetailsForm d = new DetailsForm(tw); d.Show(); d.Location = new Point(0, 0);
                            dlg = d; break;
                    case 5: Shot(dlg, outDir + "/6-detalhes.png"); dlg.Close();
                            ConfirmDialog cd = new ConfirmDialog("Aplicar 62 itens neste PC?", "41 não têm desfazer (apps removidos, limpezas e instalações). Um ponto de restauração será criado antes. Drivers e Windows Update rodam por último e podem levar até 40 minutos.",
                                "Sem desfazer pelo programa (só pelo ponto de restauração):\n  • Remover OneDrive\n  • Remover apps do Xbox", "Aplicar", "shield", false);
                            cd.StartPosition = FormStartPosition.Manual; cd.Location = new Point(0, 0); cd.Show(); dlg = cd; break;
                    case 6: Shot(dlg, outDir + "/7-confirmar.png"); dlg.Close();
                            fakeSys.PowerShellHandler = delegate(string sc) { System.Threading.Thread.Sleep(15); ProcResult pr = new ProcResult(); pr.Output = sc.Contains("Get-AppxPackage") ? "Removido: app" : "SKIP:não se aplica neste PC simulado"; return pr; };
                            Engine eng = new Engine(fakeSys, Engine.DefaultDataDir());
                            System.Collections.Generic.List<Tweak> sel = Catalog.All().FindAll(delegate(Tweak x) { return x.Padrao && x.AppliesTo(WinVersion.Win11); });
                            sel = sel.GetRange(0, 70);
                            ProgressForm pf = null;
                            pf = new ProgressForm("Aplicando Padrão", delegate(ProgressHandler progress, Action<string> log)
                            {
                                eng.TweakDone = delegate(TweakResult tr) { log((tr.Outcome == ActionOutcome.Ok ? "[OK]     " : tr.Outcome == ActionOutcome.Skipped ? "[IGNOR.] " : "[FALHA]  ") + tr.Tweak.Title); pf.ReportResult(tr.Tweak.Title, tr.Outcome, tr.Outcome == ActionOutcome.Ok ? null : (tr.Messages.Count > 0 ? tr.Messages[0] : null)); };
                                SessionResult r = eng.Apply(sel, false, "Windows 11 (simulado)", delegate(int dn, int tt, string m) { progress(dn, tt, m); if (dn == 40) progress(dn, tt, m + " — 45% · Baixando 2 de 5 (KB5034441)"); if (dn == 40) System.Threading.Thread.Sleep(6000); });
                                pf.LogPath = r.LogPath; pf.OfferRestart = true;
                                return string.Format("Concluído: {0} aplicados, {1} ignorados, {2} com falha.", r.Count(ActionOutcome.Ok), r.Count(ActionOutcome.Skipped), r.Count(ActionOutcome.Failed));
                            });
                            pf.TotalItems = sel.Count;
                            pf.Queue = sel.ConvertAll(delegate(Tweak x) { return x.Title; });
                            pf.StartPosition = FormStartPosition.Manual; pf.Location = new Point(0, 0);
                            pf.Show(); dlg = pf; t.Interval = 3500; break;
                    case 7: Shot(dlg, outDir + "/8-progresso.png"); t.Interval = 6000; break;
                    case 8: Shot(dlg, outDir + "/9-concluido.png"); dlg.Close(); t.Interval = 900;
                            HistoryForm h = new HistoryForm(new Engine(fakeSys, Engine.DefaultDataDir()));
                            h.StartPosition = FormStartPosition.Manual; h.Location = new Point(0, 0); h.Show(); dlg = h; break;
                    case 9: Shot(dlg, outDir + "/10-historico.png"); dlg.Close(); Call(f, "SelectCategory", "dashboard"); break;
                    case 10: Shot(f, outDir + "/11-dashboard-depois.png"); break;
                    default: t.Stop(); f.Close(); break;
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); t.Stop(); f.Close(); }
        };
        f.Shown += delegate { t.Start(); };
        Application.Run(f);
    }

    static Form dlg;
    static FakeSystem fakeSys;

    static void Call(object o, string method, object arg)
    {
        o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, new object[] { arg });
    }

    static void Shot(Form f, string path)
    {
        f.Refresh();
        Application.DoEvents();
        System.Threading.Thread.Sleep(300);
        Application.DoEvents();
        System.Diagnostics.Process p = System.Diagnostics.Process.Start("import",
            string.Format("-window root -crop {0}x{1}+{2}+{3} {4}", f.Width, f.Height, f.Left, f.Top, path));
        p.WaitForExit();
        Console.WriteLine("salvo " + path);
    }
}
