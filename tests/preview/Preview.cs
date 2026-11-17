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
                    case 0: Shot(f, outDir + "/1-todos.png"); Call(f, "ApplyPreset", 2);
                            if (args.Length > 3 && args[3] == "alternar") { f.GetType().GetMethod("CycleTheme", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(f, null); }
                            break;
                    case 1: Call(f, "SelectCategory", Category.Apps); break;
                    case 2: Shot(f, outDir + "/2-rigido-apps.png"); Call(f, "SelectCategory", Category.Desempenho); break;
                    case 3: Shot(f, outDir + "/3-desempenho.png");
                            Tweak tw = Catalog.All().Find(delegate(Tweak x) { return x.Id == "perf.highperf"; });
                            DetailsForm d = new DetailsForm(tw); d.Show(); d.Location = new Point(0, 0);
                            d.Shown += delegate { };
                            dlg = d; break;
                    case 4: Shot(dlg, outDir + "/4-detalhes.png"); dlg.Close();
                            fakeSys.PowerShellHandler = delegate(string sc) { System.Threading.Thread.Sleep(15); ProcResult pr = new ProcResult(); pr.Output = sc.Contains("Get-AppxPackage") ? "Removido: app" : "SKIP:não se aplica neste PC simulado"; return pr; };
                            Engine eng = new Engine(fakeSys, Engine.DefaultDataDir());
                            System.Collections.Generic.List<Tweak> sel = Catalog.All().FindAll(delegate(Tweak x) { return x.InPreset(2) && x.AppliesTo(WinVersion.Win11); });
                            ProgressForm pf = null;
                            pf = new ProgressForm("Aplicando ajustes", delegate(ProgressHandler progress, Action<string> log)
                            {
                                eng.TweakDone = delegate(TweakResult tr) { log((tr.Outcome == ActionOutcome.Ok ? "[OK]     " : tr.Outcome == ActionOutcome.Skipped ? "[IGNOR.] " : "[FALHA]  ") + tr.Tweak.Title); };
                                SessionResult r = eng.Apply(sel, false, "Windows 11 (simulado)", progress);
                                pf.LogPath = r.LogPath; pf.OfferRestart = true;
                                return string.Format("Concluído: {0} aplicados, {1} ignorados, {2} com falha.", r.Count(ActionOutcome.Ok), r.Count(ActionOutcome.Skipped), r.Count(ActionOutcome.Failed));
                            });
                            pf.StartPosition = FormStartPosition.Manual; pf.Location = new Point(0, 0);
                            pf.Show(); dlg = pf; t.Interval = 4000; break;
                    case 5: Shot(dlg, outDir + "/5-progresso.png"); dlg.Close(); t.Interval = 900;
                            HistoryForm h = new HistoryForm(new Engine(fakeSys, Engine.DefaultDataDir()));
                            h.StartPosition = FormStartPosition.Manual; h.Location = new Point(0, 0); h.Show(); dlg = h; break;
                    case 6: Shot(dlg, outDir + "/6-historico.png"); dlg.Close(); break;
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
