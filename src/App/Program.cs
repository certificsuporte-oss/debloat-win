// Ponto de entrada. Sem argumentos abre a tela; com argumentos roda sem tela.
//
//   DebloatWin.exe /preset basico|rigido|extremo [/semponto] [/exportar arquivo.ps1] [/ids id1,id2]
//
//   /preset     marca os ajustes do preset (aplicáveis a este Windows)
//   /ids        lista de ids a aplicar (soma com o preset, se houver)
//   /semponto   não cria ponto de restauração
//   /exportar   só gera o .ps1, não aplica nada
//
// Códigos de saída: 0 = ok, 1 = alguma falha, 2 = argumentos inválidos, 3 = sem administrador
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DebloatWin.Core;

namespace DebloatWin.App
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0) return RunCommandLine(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.SetMode(Theme.LoadMode(), false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { ShowCrash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { ShowCrash(e.ExceptionObject as Exception); };
            Application.Run(new MainForm(new WindowsSystem()));
            return 0;
        }

        static void ShowCrash(Exception ex)
        {
            string text = ex == null ? "Erro desconhecido" : ex.ToString();
            try
            {
                string dir = Path.Combine(Engine.DefaultDataDir(), "logs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "erro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"), text, new UTF8Encoding(true));
            }
            catch { }
            MessageBox.Show("Ocorreu um erro inesperado:\n\n" + (ex == null ? text : ex.Message) +
                "\n\nOs detalhes foram salvos na pasta de logs do " + AppInfo.Name + ".", AppInfo.Name,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static int RunCommandLine(string[] args)
        {
            try { AttachConsole(-1); } catch { }
            int level = 0;
            bool restore = true;
            string export = null;
            List<string> ids = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant().TrimStart('/', '-');
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (a)
                {
                    case "preset":
                        if (next == null) return Usage("faltou o nome do preset");
                        string p = next.ToLowerInvariant().Replace("á", "a").Replace("í", "i");
                        level = p == "basico" ? 1 : p == "rigido" ? 2 : p == "extremo" ? 3 : -1;
                        if (level < 0) return Usage("preset desconhecido: " + next);
                        i++; break;
                    case "ids":
                        if (next == null) return Usage("faltou a lista de ids");
                        foreach (string id in next.Split(',')) if (id.Trim().Length > 0) ids.Add(id.Trim());
                        i++; break;
                    case "semponto": restore = false; break;
                    case "exportar":
                        if (next == null) return Usage("faltou o arquivo de saída");
                        export = next; i++; break;
                    case "?":
                    case "h":
                    case "help":
                    case "ajuda":
                        return Usage(null);
                    default:
                        return Usage("argumento desconhecido: " + args[i]);
                }
            }
            if (level == 0 && ids.Count == 0) return Usage("informe /preset ou /ids");

            WindowsSystem sys = new WindowsSystem();
            WinInfo win = WinInfo.Detect(sys);
            List<Tweak> chosen = new List<Tweak>();
            List<Tweak> all = Catalog.All();
            HashSet<string> known = new HashSet<string>();
            foreach (Tweak t in all) known.Add(t.Id);
            foreach (string id in ids) if (!known.Contains(id)) return Usage("id desconhecido: " + id);
            foreach (Tweak t in all)
            {
                bool want = (level > 0 && t.InPreset(level)) || ids.Contains(t.Id);
                if (!want) continue;
                if (!t.AppliesTo(win.Version) || (t.ProOnly && win.IsHome)) continue;
                chosen.Add(t);
            }

            if (export != null)
            {
                File.WriteAllText(export, ScriptExporter.Build(chosen, restore), new UTF8Encoding(true));
                Console.WriteLine(string.Format("Script com {0} ajuste(s) salvo em {1}", chosen.Count, export));
                return 0;
            }

            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                Console.WriteLine("É preciso rodar como administrador.");
                return 3;
            }

            Engine engine = new Engine(sys, Engine.DefaultDataDir());
            Console.WriteLine(AppInfo.Name + " " + AppInfo.Version + " — " + win);
            if (restore)
            {
                string msg;
                bool ok = engine.CreateRestorePoint(out msg);
                Console.WriteLine((ok ? "[OK]    " : "[FALHA] ") + "Ponto de restauração: " + msg);
                engine.ExtraLogHeader = (ok ? "[OK]    " : "[FALHA] ") + "Ponto de restauração: " + msg;
                // Sem ponto de restauração, no modo sem tela, não seguimos: ninguém está olhando.
                if (!ok) { Console.WriteLine("Nada foi aplicado. Use /semponto para aplicar mesmo assim."); return 1; }
            }
            engine.TweakDone = delegate(TweakResult tr)
            {
                string head = tr.Outcome == ActionOutcome.Ok ? "[OK]    " : tr.Outcome == ActionOutcome.Skipped ? "[IGNOR] " : "[FALHA] ";
                Console.WriteLine(head + tr.Tweak.Title);
            };
            SessionResult r = engine.Apply(chosen, false, win.ToString(), null);
            Console.WriteLine(string.Format("Concluído: {0} aplicados, {1} ignorados, {2} com falha. Log: {3}",
                r.Count(ActionOutcome.Ok), r.Count(ActionOutcome.Skipped), r.Count(ActionOutcome.Failed), r.LogPath));
            return r.Count(ActionOutcome.Failed) > 0 ? 1 : 0;
        }

        static int Usage(string error)
        {
            if (error != null) Console.WriteLine("Erro: " + error);
            Console.WriteLine("Uso: DebloatWin.exe /preset basico|rigido|extremo [/ids id1,id2] [/semponto] [/exportar arquivo.ps1]");
            Console.WriteLine("Sem argumentos, abre a tela.");
            return 2;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);
    }
}
