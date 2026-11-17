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
            UpdaterTests();
            DetectionTests(dumpDir);
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
            int padraoCount = 0;

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
                Check(t.Id.Contains("."), "id com prefixo de categoria: " + t.Id);
                if (t.Risk == Risk.Alto) Check(t.Warning != null, "ajuste de risco Alto tem aviso: " + t.Id);
                if (t.Padrao) padraoCount++;
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
            Console.WriteLine("  preset Padrão: " + padraoCount + " ajustes");
            HashSet<string> catalogIds = new HashSet<string>();
            foreach (Tweak t in all) catalogIds.Add(t.Id);
            foreach (string id in Catalog.PadraoIds) Check(catalogIds.Contains(id), "id do Padrão existe no catálogo: " + id);
            Check(padraoCount == Catalog.PadraoIds.Length && padraoCount == 101, "Padrão tem os 92 ajustes + 5 programas + apps padrão + drivers e atualizações");
            int apps = 0;
            foreach (Tweak t in all)
                foreach (TweakAction a in t.Actions)
                {
                    WingetInstallAction w = a as WingetInstallAction;
                    if (w == null) continue;
                    apps++;
                    Check(t.Category == Category.Programas && t.Group != null, "programa na categoria certa e com grupo: " + t.Id);
                    Check(w.PackageId.IndexOf(' ') < 0 && w.PackageId.Length > 3, "id do winget sem espaço: " + w.PackageId);
                    if (w.PackageId.StartsWith("msstore:")) Check(w.AppxName != null, "app da Store tem nome Appx para detecção: " + t.Id);
                    Check(w.CanUndo, "programa pode ser desinstalado pelo desfazer: " + t.Id);
                    Check(w.DetectScript() != null, "programa tem detecção: " + t.Id);
                }
            Console.WriteLine("  programas: " + apps);
            Check(apps == 5, "5 programas no catálogo");
            foreach (string id in new string[] { "apps.store", "upd.disable", "priv.camera", "priv.mic", "tool.netreset", "apps.ie" })
                Check(!Array.Exists(Catalog.PadraoIds, delegate(string x) { return x == id; }), "fora do Padrão: " + id);
            Check(all[all.Count - 1].Id == "fim.windowsupdate", "Windows Update é o último item da lista");
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

            sys.PowerShellHandler = delegate(string s) { ProcResult p = new ProcResult(); p.Output = "PROGRESS:45|Baixando\nInstalado"; return p; };
            r = tasks.Apply(sys);
            Check(r.Outcome == ActionOutcome.Ok && !r.Message.Contains("PROGRESS"), "linhas PROGRESS não vão para o log");
            Check(Engine.LiveMessage("Instalar atualizações do Windows", "45|Baixando 2 de 5 (KB5034441)") ==
                  "Instalar atualizações do Windows — 45% · Baixando 2 de 5 (KB5034441)", "progresso ao vivo com porcentagem");
            Check(Engine.LiveMessage("Drivers", "|Procurando…") == "Drivers — Procurando…", "progresso ao vivo sem porcentagem");
            Check(Engine.LiveMessage("Drivers", "100|") == "Drivers — 100%", "progresso só com porcentagem");
            List<string> vistos = new List<string>();
            Engine.LiveHandler(delegate(int d, int tot, string m) { vistos.Add(d + "/" + tot + " " + m); }, 3, 10, "WU")("7|Instalando");
            Check(vistos.Count == 1 && vistos[0] == "3/10 WU — 7% · Instalando", "progresso ao vivo mantém o item atual");

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
        static void DetectionTests(string dumpDir)
        {
            Section("Detecção do que já está aplicado");
            List<Tweak> all = Catalog.All();
            Tweak ext = all.Find(delegate(Tweak t) { return t.Id == "ui.ext"; });
            Tweak sug = all.Find(delegate(Tweak t) { return t.Id == "priv.sugestoes"; });
            Tweak svc = all.Find(delegate(Tweak t) { return t.Id == "perf.servicos"; });
            Tweak nvidia = all.Find(delegate(Tweak t) { return t.Id == "tel.nvidia"; });
            Tweak tasks = all.Find(delegate(Tweak t) { return t.Id == "tel.tasks"; });
            Tweak bing = all.Find(delegate(Tweak t) { return t.Id == "apps.bing"; });
            Tweak temp = all.Find(delegate(Tweak t) { return t.Id == "tool.temp"; });

            FakeSystem sys = new FakeSystem();
            sys.WriteValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", RegKind.DWord, "0"); // já feito à mão
            sys.WriteValue(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", RegKind.DWord, "0"); // 1 de 12
            sys.AddService("MapsBroker", 2); sys.AddService("PcaSvc", 3); sys.AddService("TrkWks", 4); sys.AddService("RemoteRegistry", 4);

            Detector d = new Detector(sys);
            d.DetectFast(all);
            Check(d.Summarize(ext).State == TweakState.Applied, "ajuste feito fora do app aparece como já aplicado");
            TweakStatus ps = d.Summarize(sug);
            Check(ps.State == TweakState.Partial && ps.Applied == 1 && ps.Known == 12, "parcial mostra quantos de quantos (1/12)");
            TweakStatus ss = d.Summarize(svc);
            Check(ss.State == TweakState.Partial && ss.Applied == 3 && ss.Known == 4, "serviços: Desativado conta como Manual; serviço ausente é ignorado");
            Check(d.Summarize(nvidia).State == TweakState.NotApplicable, "NVIDIA ausente: nada a fazer neste PC");
            Check(d.Summarize(tasks).State == TweakState.Checking, "tarefas ficam 'verificando' até o PowerShell responder");
            Check(d.Summarize(temp).State == TweakState.Unknown, "limpezas não têm estado (sem etiqueta)");

            // verificação lenta: uma única execução, respostas por índice
            string seen = null;
            sys.PowerShellHandler = delegate(string script)
            {
                seen = script;
                StringBuilder o = new StringBuilder();
                int n = 0;
                foreach (string line in script.Split('\n')) if (line.StartsWith("try { $r =")) n++;
                for (int i = 0; i < n; i++)
                {
                    // tarefas: 1, apps do Bing: 0, resto: NA
                    string v = i == 0 ? "1" : "NA";
                    o.AppendLine("DET:" + i + ":" + v);
                }
                ProcResult p = new ProcResult(); p.Output = o.ToString(); return p;
            };
            int before = sys.Scripts.Count;
            d.DetectSlow(new List<Tweak> { tasks, bing }, 60);
            Check(sys.Scripts.Count == before + 1, "verificação lenta roda o PowerShell uma vez só");
            Check(seen.Contains("function Get-DbwAppx") && seen.Contains("DET:"), "script de verificação tem o cache de apps e os marcadores");
            Check(d.Summarize(tasks).State == TweakState.Applied, "tarefas desativadas aparecem como aplicado");
            Check(d.Summarize(bing).State == TweakState.NotApplicable, "resposta NA vira 'nada a fazer'");
            Check(d.SlowDone, "marca a verificação lenta como concluída");

            Dictionary<int, DetectState> parsed = Detector.Parse("lixo\nDET:0:1\r\nDET:1:0\nDET:2:NA\nDET:3:?\nDET:x:1");
            Check(parsed.Count == 4 && parsed[0] == DetectState.Applied && parsed[1] == DetectState.NotApplied &&
                  parsed[2] == DetectState.NotApplicable && parsed[3] == DetectState.Unknown, "lê as respostas e ignora linhas estranhas");

            // aplicar e desfazer mudam o estado detectado
            FakeSystem s2 = new FakeSystem();
            Engine e2 = new Engine(s2, Path.Combine(Path.GetTempPath(), "dbw-" + Guid.NewGuid().ToString("N")));
            List<Tweak> regOnly = all.FindAll(delegate(Tweak t) { return t.Actions.TrueForAll(delegate(TweakAction a) { return a is RegSetAction; }); });
            Detector d2 = new Detector(s2); d2.DetectFast(regOnly);
            int appliedBefore = regOnly.FindAll(delegate(Tweak t) { return d2.Summarize(t).State == TweakState.Applied; }).Count;
            SessionResult sr = e2.Apply(regOnly, false, "t", null);
            Detector d3 = new Detector(s2); d3.DetectFast(regOnly);
            Check(regOnly.TrueForAll(delegate(Tweak t) { TweakState st = d3.Summarize(t).State; return st == TweakState.Applied || st == TweakState.NotApplicable; }),
                  "depois de aplicar, todos aparecem como aplicados");
            e2.Undo(sr.Backup, null);
            Detector d4 = new Detector(s2); d4.DetectFast(regOnly);
            int appliedAfter = regOnly.FindAll(delegate(Tweak t) { return d4.Summarize(t).State == TweakState.Applied; }).Count;
            Check(appliedAfter == appliedBefore, "depois de desfazer, volta ao estado detectado antes");

            if (dumpDir != null)
            {
                List<TweakAction> slow = new List<TweakAction>();
                foreach (Tweak t in all) foreach (TweakAction a in t.Actions) if (a.DetectScript() != null) slow.Add(a);
                File.WriteAllText(Path.Combine(dumpDir, "_deteccao.ps1"), Detector.BuildScript(slow), new UTF8Encoding(true));
                Console.WriteLine("  ações com verificação por PowerShell: " + slow.Count);
            }
        }

        // ------------------------------------------------------------------
        static void UpdaterTests()
        {
            Section("Atualização automática");
            UpdateInfo u = Updater.Parse("{\"versao\":\"1.2.0\",\"url\":\"https://exemplo/DebloatWin.exe\",\"sha256\":\"ab\",\"notas\":\"x\"}");
            Check(u.Version == "1.2.0" && u.Url.StartsWith("https://") && u.Notes == "x", "lê o versao.json");
            bool threw = false;
            try { Updater.Parse("{\"versao\":\"1.2.0\",\"url\":\"http://inseguro/x.exe\",\"sha256\":\"ab\"}"); } catch (FormatException) { threw = true; }
            Check(threw, "recusa URL sem https");
            threw = false;
            try { Updater.Parse("{\"versao\":\"1.2.0\"}"); } catch (FormatException) { threw = true; }
            Check(threw, "recusa versao.json incompleto");

            Check(Updater.IsNewer("1.2.0", "1.1.0"), "1.2.0 é mais nova que 1.1.0");
            Check(Updater.IsNewer("1.10.0", "1.9.0"), "compara número, não texto (1.10 > 1.9)");
            Check(!Updater.IsNewer("1.1.0", "1.1.0"), "mesma versão não atualiza");
            Check(!Updater.IsNewer("1.0.0", "1.1.0"), "nunca volta para versão anterior");
            Check(Updater.IsNewer("v1.2", "1.1.0"), "aceita v1.2");

            // Conferência do arquivo baixado: usa o próprio Tests.exe como "programa novo"
            string self = typeof(Program).Assembly.Location;
            string selfVer = typeof(Program).Assembly.GetName().Version.ToString();
            UpdateInfo ok = new UpdateInfo(); ok.Version = selfVer; ok.Sha256 = Updater.Sha256Of(self).ToUpperInvariant(); ok.Url = "https://x";
            Check(Updater.Verify(self, ok) == null, "aceita arquivo com SHA-256 e versão corretos");
            UpdateInfo badSha = new UpdateInfo(); badSha.Version = selfVer; badSha.Sha256 = new string('0', 64); badSha.Url = "https://x";
            Check(Updater.Verify(self, badSha) != null, "recusa SHA-256 diferente");
            UpdateInfo badVer = new UpdateInfo(); badVer.Version = "99.0.0"; badVer.Sha256 = ok.Sha256; badVer.Url = "https://x";
            Check(Updater.Verify(self, badVer) != null, "recusa versão diferente da anunciada (evita laço de atualização)");
            string txt = Path.Combine(Path.GetTempPath(), "dbw-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(txt, "não sou um programa");
            UpdateInfo notExe = new UpdateInfo(); notExe.Version = "1.0.0"; notExe.Sha256 = Updater.Sha256Of(txt); notExe.Url = "https://x";
            Check(Updater.Verify(txt, notExe) != null, "recusa arquivo que não é programa .NET");
            File.Delete(txt);

            // Troca do executável e limpeza
            string dir = Path.Combine(Path.GetTempPath(), "dbw-up-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "DebloatWin.exe");
            File.WriteAllText(exe, "versao velha");
            File.WriteAllText(Updater.NewPath(exe), "versao nova");
            Updater.Swap(exe, Updater.NewPath(exe));
            Check(File.ReadAllText(exe) == "versao nova", "troca o executável pelo novo");
            Check(File.ReadAllText(Updater.OldPath(exe)) == "versao velha", "guarda o antigo como .old");
            Check(!File.Exists(Updater.NewPath(exe)), "não sobra .new");
            // segunda atualização com .old ainda presente
            File.WriteAllText(Updater.NewPath(exe), "versao mais nova");
            Updater.Swap(exe, Updater.NewPath(exe));
            Check(File.ReadAllText(exe) == "versao mais nova", "atualiza de novo mesmo com .old anterior");
            Updater.CleanupOld(exe);
            Check(!File.Exists(Updater.OldPath(exe)), "limpa o .old na abertura seguinte");
            // falha na troca: o original volta
            File.WriteAllText(exe, "original");
            bool failed = false;
            try { Updater.Swap(exe, Path.Combine(dir, "nao-existe.new")); } catch { failed = true; }
            Check(failed && File.Exists(exe) && File.ReadAllText(exe) == "original", "se a troca falhar, o executável original volta");

            // Download de verdade por HTTP local
            int port = new Random().Next(20000, 40000);
            System.Net.HttpListener http = new System.Net.HttpListener();
            http.Prefixes.Add("http://127.0.0.1:" + port + "/");
            http.Start();
            byte[] payload = new byte[300 * 1024];
            new Random(7).NextBytes(payload);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Net.HttpListenerContext ctx = http.GetContext();
                ctx.Response.ContentLength64 = payload.Length;
                ctx.Response.OutputStream.Write(payload, 0, payload.Length);
                ctx.Response.Close();
            });
            UpdateInfo dl = new UpdateInfo(); dl.Url = "http://127.0.0.1:" + port + "/DebloatWin.exe"; dl.Version = "1.0.0"; dl.Sha256 = "x";
            string target = Path.Combine(dir, "baixado.bin");
            long last = 0;
            Updater.Download(dl, target, 10000, delegate(long got, long total) { last = got; });
            http.Stop();
            Check(File.Exists(target) && new FileInfo(target).Length == payload.Length && last == payload.Length, "baixa o arquivo inteiro com progresso");
            try { Directory.Delete(dir, true); } catch { }
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
