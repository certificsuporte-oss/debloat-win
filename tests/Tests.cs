// Testes do catálogo e do motor. Rodam no Linux com o Mono, sem Windows.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DebloatWin.Core;

namespace DebloatWin.Tests
{
    public static class Program
    {
        static int failures;
        static int checks;

        static void Check(bool cond, string what)
        {
            checks++;
            if (!cond) { failures++; Console.WriteLine("  FALHOU: " + what); }
        }

        static void Section(string name) { Console.WriteLine("== " + name); }

        public static int Main(string[] args)
        {
            string dumpDir = args.Length > 0 ? args[0] : null;

            CatalogRules();
            RegistryApplyUndoAll();
            PreexistingValues();
            Services();
            PowerShellParsing();
            BackupRoundTrip();
            Exporter(dumpDir);
            if (dumpDir != null) DumpScripts(dumpDir);

            Console.WriteLine();
            Console.WriteLine(string.Format("{0} verificações, {1} falhas", checks, failures));
            return failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------
        static void CatalogRules()
        {
            Section("Regras do catálogo");
            List<Tweak> all = Catalog.All();
            Console.WriteLine("  ajustes: " + all.Count);
            HashSet<string> ids = new HashSet<string>();
            int[] presetCount = new int[4];

            // Serviços que nunca podem ser mexidos: impressão, token/cartão (A3), acesso remoto, rede básica, hora
            HashSet<string> protectedServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "Spooler", "PrintNotify", "SCardSvr", "ScDeviceEnum", "CertPropSvc", "TermService", "SessionEnv",
                "UmRdpService", "LanmanWorkstation", "LanmanServer", "Dnscache", "Dhcp", "W32Time", "BFE", "MpsSvc",
                "WinDefend", "CryptSvc", "EventLog", "Winmgmt", "RpcSs", "AudioSrv", "Audiosrv", "nsi", "NlaSvc", "netprofm" };

            foreach (Tweak t in all)
            {
                Check(!string.IsNullOrEmpty(t.Id) && ids.Add(t.Id), "Id único e preenchido: " + t.Id);
                Check(!string.IsNullOrEmpty(t.Title) && t.Title.Length <= 70, "título ok (≤70): " + t.Id);
                Check(!string.IsNullOrEmpty(t.Description), "descrição: " + t.Id);
                Check(t.Actions.Count > 0, "tem ações: " + t.Id);
                Check(t.Preset >= 0 && t.Preset <= 3, "preset válido: " + t.Id);
                Check(t.Id.Contains("."), "id com prefixo de categoria: " + t.Id);
                if (t.Risk == Risk.Alto) Check(t.Warning != null, "ajuste de risco Alto tem aviso: " + t.Id);
                if (t.Risk == Risk.Alto) Check(t.Preset == 0 || t.Preset == 3, "risco Alto só manual ou Extremo: " + t.Id);
                if (t.Preset == 1) Check(t.Risk == Risk.Baixo, "Básico só tem risco Baixo: " + t.Id);
                presetCount[t.Preset]++;
                foreach (TweakAction a in t.Actions)
                {
                    ServiceStartAction s = a as ServiceStartAction;
                    if (s != null && t.Id != "upd.disable")
                        Check(!protectedServices.Contains(s.Service), "não mexe em serviço protegido " + s.Service + ": " + t.Id);
                    Check(!string.IsNullOrEmpty(a.Describe()), "ação descrita: " + t.Id);
                    Check(!string.IsNullOrEmpty(a.ToPowerShell()), "ação exportável: " + t.Id);
                }
                if (t.Category == Category.Ferramentas)
                    Check(!t.IsReversible || t.Actions.TrueForAll(delegate(TweakAction x) { return x is RegSetAction; }),
                          "ferramentas não fingem ter desfazer: " + t.Id);
            }
            int basic = presetCount[1], strict = basic + presetCount[2], extreme = strict + presetCount[3];
            Console.WriteLine(string.Format("  presets: Básico={0} Rígido={1} Extremo={2} Manual={3}", basic, strict, extreme, presetCount[0]));
            Check(basic >= 15 && strict > basic && extreme > strict, "presets crescem do Básico ao Extremo");
            foreach (Category c in Enum.GetValues(typeof(Category)))
                Check(all.Exists(delegate(Tweak t) { return t.Category == c; }), "categoria com itens: " + c);
        }

        // ------------------------------------------------------------------
        static void RegistryApplyUndoAll()
        {
            Section("Aplicar e desfazer todos os ajustes de registro");
            FakeSystem sys = new FakeSystem();
            // simula programas instalados para os ajustes condicionais
            sys.WriteValue(@"HKCU\Software\Microsoft\Office\16.0", "x", RegKind.DWord, "1");
            sys.WriteValue(@"HKLM\SOFTWARE\NVIDIA Corporation", "x", RegKind.DWord, "1");
            string before = sys.Snapshot();

            List<Tweak> regOnly = new List<Tweak>();
            foreach (Tweak t in Catalog.All())
                if (t.Actions.TrueForAll(delegate(TweakAction a) { return a is RegSetAction; })) regOnly.Add(t);
            Console.WriteLine("  ajustes só de registro: " + regOnly.Count);

            string dir = Path.Combine(Path.GetTempPath(), "dbw-test-" + Guid.NewGuid().ToString("N"));
            Engine eng = new Engine(sys, dir);
            SessionResult res = eng.Apply(regOnly, false, "teste", null);
            Check(res.Count(ActionOutcome.Failed) == 0, "nenhuma falha ao aplicar");
            Check(res.Count(ActionOutcome.Ok) == regOnly.Count, "todos aplicados");

            // confere que cada valor ficou como o catálogo manda
            foreach (Tweak t in regOnly)
                foreach (TweakAction a in t.Actions)
                {
                    RegSetAction r = (RegSetAction)a;
                    RegValue v = sys.ReadValue(r.Key, r.Name);
                    Check(v != null && v.SameAs(r.Kind, r.Data), "valor gravado: " + r.Describe());
                }

            // reaplicar não muda nada e não gera backup novo
            SessionResult again = eng.Apply(regOnly, false, "teste", null);
            Check(again.Backup.Tweaks.Count == 0, "reaplicar não gera desfazer (já estava assim)");

            List<SessionBackup> sessions = eng.ListSessions();
            Check(sessions.Count >= 1, "sessão salva em disco");
            SessionBackup loaded = sessions[sessions.Count - 1];
            foreach (SessionBackup b in sessions) if (b.Id == res.Backup.Id) loaded = b;
            List<string> rep = eng.Undo(loaded, null);
            bool anyFail = rep.Exists(delegate(string s) { return s.StartsWith("FALHA"); });
            Check(!anyFail, "desfazer sem falhas");
            string after = sys.Snapshot();
            Check(after == before, "registro idêntico ao original depois de desfazer");
            if (after != before) PrintDiff(before, after);
            Check(Engine.LoadBackup(loaded.FilePath).Undone, "sessão marcada como desfeita");
            try { Directory.Delete(dir, true); } catch { }
        }

        static void PrintDiff(string a, string b)
        {
            HashSet<string> sa = new HashSet<string>(a.Split('\n'));
            HashSet<string> sb = new HashSet<string>(b.Split('\n'));
            int n = 0;
            foreach (string l in sa) if (!sb.Contains(l) && n++ < 15) Console.WriteLine("    - " + l);
            foreach (string l in sb) if (!sa.Contains(l) && n++ < 30) Console.WriteLine("    + " + l);
        }

        // ------------------------------------------------------------------
        static void PreexistingValues()
        {
            Section("Valores que já existiam voltam ao original");
            FakeSystem sys = new FakeSystem();
            const string adv = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
            sys.WriteValue(adv, "HideFileExt", RegKind.DWord, "1");
            sys.WriteValue(adv, "Outro", RegKind.String, "manter");
            sys.WriteValue(@"HKCU\Control Panel\Mouse", "MouseSpeed", RegKind.String, "1");
            string before = sys.Snapshot();

            Tweak ext = Catalog.All().Find(delegate(Tweak t) { return t.Id == "ui.ext"; });
            Tweak mouse = Catalog.All().Find(delegate(Tweak t) { return t.Id == "game.mouseacc"; });
            Engine eng = new Engine(sys, Path.Combine(Path.GetTempPath(), "dbw-" + Guid.NewGuid().ToString("N")));
            SessionResult r = eng.Apply(new List<Tweak> { ext, mouse }, false, "teste", null);
            Check(sys.ReadValue(adv, "HideFileExt").Data == "0", "HideFileExt aplicado");
            Check(sys.ReadValue(@"HKCU\Control Panel\Mouse", "MouseSpeed").Data == "0", "MouseSpeed aplicado");
            eng.Undo(r.Backup, null);
            Check(sys.Snapshot() == before, "voltou exatamente ao estado anterior (incluindo valor vizinho)");

            // duas sessões que criam a mesma chave: desfazer só a primeira não apaga a segunda
            FakeSystem s2 = new FakeSystem();
            Engine e2 = new Engine(s2, Path.Combine(Path.GetTempPath(), "dbw-" + Guid.NewGuid().ToString("N")));
            Tweak recall = Catalog.All().Find(delegate(Tweak t) { return t.Id == "apps.recall"; });
            Tweak iaapps = Catalog.All().Find(delegate(Tweak t) { return t.Id == "apps.iaapps"; });
            SessionResult first = e2.Apply(new List<Tweak> { recall }, false, "t", null);
            e2.Apply(new List<Tweak> { iaapps }, false, "t", null);
            e2.Undo(first.Backup, null);
            RegValue click = s2.ReadValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo");
            Check(click != null && click.Data == "1", "desfazer sessão antiga preserva valor de sessão mais nova na mesma chave");
            RegValue rec = s2.ReadValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis");
            Check(rec == null, "valor da sessão desfeita foi removido");
        }

        // ------------------------------------------------------------------
        static void Services()
        {
            Section("Serviços");
            FakeSystem sys = new FakeSystem();
            sys.AddService("MapsBroker", 2);
            sys.AddService("PcaSvc", 2);
            sys.AddService("TrkWks", 3);   // já manual
            // RetailDemo e RemoteRegistry não existem neste PC simulado
            string before = sys.Snapshot();
            Tweak svc = Catalog.All().Find(delegate(Tweak t) { return t.Id == "perf.servicos"; });
            Engine eng = new Engine(sys, Path.Combine(Path.GetTempPath(), "dbw-" + Guid.NewGuid().ToString("N")));
            SessionResult r = eng.Apply(new List<Tweak> { svc }, false, "t", null);
            Check(r.Results[0].Outcome == ActionOutcome.Ok, "aplicado mesmo com serviços ausentes");
            Check(sys.ReadValue(@"HKLM\SYSTEM\CurrentControlSet\Services\MapsBroker", "Start").Data == "3", "MapsBroker em Manual");
            Check(r.Results[0].Messages.Exists(delegate(string m) { return m.StartsWith("ignorado") && m.Contains("RetailDemo"); }), "serviço ausente é ignorado, não falha");
            Check(r.Backup.UndoCount == 2, "só 2 serviços mudaram, 2 registros de desfazer");
            eng.Undo(r.Backup, null);
            Check(sys.Snapshot() == before, "serviços voltaram ao tipo de inicialização original");
        }

        // ------------------------------------------------------------------
        static void PowerShellParsing()
        {
            Section("Leitura da saída do PowerShell");
            FakeSystem sys = new FakeSystem();
            TaskDisableAction tasks = new TaskDisableAction(@"\A\B", @"\A\C");

            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.Output = "SKIP:Nenhuma das tarefas existe"; return p; };
            ActionResult r = tasks.Apply(sys);
            Check(r.Outcome == ActionOutcome.Skipped, "SKIP vira ignorado");

            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.ExitCode = 1; p.Output = "FAIL:Acesso negado"; return p; };
            r = tasks.Apply(sys);
            Check(r.Outcome == ActionOutcome.Failed && r.Message.Contains("Acesso negado"), "FAIL vira falha com mensagem");

            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.Output = "UNDO:\\A\\B\r\nDesativada: \\A\\B\r\nUNDO:\\A\\C"; return p; };
            r = tasks.Apply(sys);
            Check(r.Outcome == ActionOutcome.Ok, "UNDO + texto vira ok");
            Check(r.Undo != null && r.Undo.Script.Contains("@('\\A\\B','\\A\\C')"), "valores UNDO entram no script de desfazer");
            Check(!r.Message.Contains("UNDO:"), "linhas UNDO não aparecem para o usuário");

            // falha no meio: o que já mudou continua com desfazer
            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.ExitCode = 1; p.Output = "UNDO:\\A\\B\nDesativada: \\A\\B\nFAIL:C: Acesso negado"; return p; };
            r = tasks.Apply(sys);
            Check(r.Outcome == ActionOutcome.Failed && r.Undo != null && r.Undo.Script.Contains("@('\\A\\B')"), "falha parcial guarda desfazer do que mudou");

            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.TimedOut = true; p.ExitCode = -1; return p; };
            Check(tasks.Apply(sys).Outcome == ActionOutcome.Failed, "tempo esgotado vira falha");

            AppxRemoveAction appx = new AppxRemoveAction("Microsoft.BingNews");
            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.Output = "Removido: Microsoft.BingNews"; return p; };
            r = appx.Apply(sys);
            Check(r.Outcome == ActionOutcome.Ok && r.Undo == null && !appx.CanUndo, "remoção de app: ok e sem desfazer");

            // scripts sempre vão embrulhados (try/catch com FAIL:)
            Check(sys.Scripts[sys.Scripts.Count - 1].Contains("FAIL:") && sys.Scripts[sys.Scripts.Count - 1].Contains("& {"), "script embrulhado com tratamento de erro");

            // Aspas simples em nomes não quebram o script
            Check(Ps.Q("O'Brien") == "'O''Brien'", "escape de aspas simples");
        }

        // ------------------------------------------------------------------
        static void BackupRoundTrip()
        {
            Section("Backup em JSON");
            SessionBackup b = new SessionBackup();
            b.Id = "20260101-000000"; b.Date = "01/01/2026"; b.Machine = "PC-ÇÃO"; b.Windows = "Windows 10";
            TweakUndo tu = new TweakUndo(); tu.TweakId = "x"; tu.TweakTitle = "Título com acentuação";
            tu.Records.Add(UndoRecord.RestoreValue(@"HKCU\A", "v", new RegValue(RegKind.MultiString, "a\nb")));
            tu.Records.Add(UndoRecord.PowerShell("ps", "Write-Output 'x'\n"));
            b.Tweaks.Add(tu);
            b.Irreversible.Add("Remover apps");
            string f = Path.Combine(Path.GetTempPath(), "dbw-" + Guid.NewGuid().ToString("N") + ".json");
            Engine.SaveBackup(b, f);
            SessionBackup l = Engine.LoadBackup(f);
            Check(l.Machine == "PC-ÇÃO" && l.Tweaks[0].TweakTitle == "Título com acentuação", "acentos preservados");
            Check(l.Tweaks[0].Records[0].Data == "a\nb" && l.Tweaks[0].Records[0].Kind == "MultiString", "dados preservados");
            Check(l.Irreversible.Count == 1 && l.UndoCount == 2, "contagens preservadas");
            File.Delete(f);
        }

        // ------------------------------------------------------------------
        static void Exporter(string dumpDir)
        {
            Section("Exportar .ps1");
            List<Tweak> all = Catalog.All();
            string ps = ScriptExporter.Build(all, true);
            foreach (Tweak t in all) Check(ps.Contains("# [" + CategoryInfo.Title(t.Category) + "] " + t.Id + "\n") || ps.Contains("] " + t.Id + Environment.NewLine), "ajuste no script: " + t.Id);
            Check(ps.Contains("Checkpoint-Computer"), "script cria ponto de restauração");
            Check(!ps.Contains("SilentlyContinue'\n$ErrorActionPreference = 'SilentlyContinue"), "não silencia todos os erros");
            if (dumpDir != null)
            {
                Directory.CreateDirectory(dumpDir);
                File.WriteAllText(Path.Combine(dumpDir, "_exportado-completo.ps1"), ps, new UTF8Encoding(true));
            }
        }

        // ------------------------------------------------------------------
        // Grava todos os scripts (aplicar e desfazer) para checagem de sintaxe com o pwsh
        static void DumpScripts(string dir)
        {
            Section("Gerando scripts para checagem de sintaxe");
            FakeSystem sys = new FakeSystem();
            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.Output = "UNDO:valor-de-teste\nok"; return p; };
            int n = 0;
            foreach (Tweak t in Catalog.All())
            {
                int i = 0;
                foreach (TweakAction a in t.Actions)
                {
                    PsAction pa = a as PsAction;
                    if (pa == null) continue;
                    File.WriteAllText(Path.Combine(dir, t.Id + "." + i + ".apply.ps1"), PsAction.Wrap(pa.ToPowerShell()), new UTF8Encoding(true));
                    ActionResult r = pa.Apply(sys);
                    if (r.Undo != null)
                        File.WriteAllText(Path.Combine(dir, t.Id + "." + i + ".undo.ps1"), PsAction.Wrap(r.Undo.Script), new UTF8Encoding(true));
                    i++; n++;
                }
            }
            string rp;
            new Engine(sys, Path.GetTempPath()).CreateRestorePoint(out rp);
            File.WriteAllText(Path.Combine(dir, "_ponto-restauracao.ps1"), sys.Scripts[sys.Scripts.Count - 1], new UTF8Encoding(true));
            Console.WriteLine("  scripts gravados: " + n + " ações em " + dir);
        }
    }
}
