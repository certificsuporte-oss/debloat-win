// Motor: aplica ajustes, grava o backup para desfazer, cria ponto de restauração e log.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DebloatWin.Core
{
    [DataContract]
    public class TweakUndo
    {
        [DataMember] public string TweakId;
        [DataMember] public string TweakTitle;
        [DataMember] public List<UndoRecord> Records = new List<UndoRecord>();
    }

    [DataContract]
    public class SessionBackup
    {
        [DataMember] public string Id;
        [DataMember] public string Date;
        [DataMember] public string Machine;
        [DataMember] public string Windows;
        [DataMember] public string AppVersion;
        [DataMember] public bool Undone;
        [DataMember] public List<TweakUndo> Tweaks = new List<TweakUndo>();
        [DataMember] public List<string> Irreversible = new List<string>();

        public string FilePath; // não serializado

        public int UndoCount
        {
            get
            {
                int n = 0;
                foreach (TweakUndo t in Tweaks) n += t.Records.Count;
                return n;
            }
        }
    }

    public class TweakResult
    {
        public Tweak Tweak;
        public ActionOutcome Outcome;
        public List<string> Messages = new List<string>();
    }

    public class SessionResult
    {
        public SessionBackup Backup;
        public List<TweakResult> Results = new List<TweakResult>();
        public bool RestorePointCreated;
        public string RestorePointMessage;
        public string LogPath;
        public bool NeedsRestart;

        public int Count(ActionOutcome o)
        {
            int n = 0;
            foreach (TweakResult r in Results) if (r.Outcome == o) n++;
            return n;
        }
    }

    public delegate void ProgressHandler(int done, int total, string message);

    public class Engine
    {
        readonly ISystem sys;
        readonly string dataDir;

        /// <summary>Chamado depois de cada ajuste (para log ao vivo na tela).</summary>
        public Action<TweakResult> TweakDone;
        /// <summary>Chamado depois da tentativa de criar o ponto de restauração.</summary>
        public Action<bool, string> RestorePointDone;
        /// <summary>Linha extra no início do log (ex.: resultado do ponto de restauração feito pela tela).</summary>
        public string ExtraLogHeader;

        public Engine(ISystem sys, string dataDir)
        {
            this.sys = sys;
            this.dataDir = dataDir;
        }

        public static string DefaultDataDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppInfo.DataFolderName);
        }

        string SessionsDir { get { return Path.Combine(dataDir, "sessoes"); } }
        string LogsDir { get { return Path.Combine(dataDir, "logs"); } }

        // ------------------------------------------------------------------
        public bool CreateRestorePoint(out string message)
        {
            string script =
                "$k = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'\n" +
                "try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" } catch { }\n" +
                "$old = (Get-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue).SystemRestorePointCreationFrequency\n" +
                "Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord\n" +
                "try { Checkpoint-Computer -Description " + Ps.Q(AppInfo.Name) + " -RestorePointType 'MODIFY_SETTINGS' }\n" +
                "finally {\n" +
                "  if ($null -eq $old) { Remove-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -ErrorAction SilentlyContinue }\n" +
                "  else { Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value $old -Type DWord }\n" +
                "}\n" +
                "Write-Output 'Ponto de restauração criado'\n";
            ProcResult r;
            try { r = sys.RunPowerShell(PsAction.Wrap(script), 600); }
            catch (Exception ex) { message = ex.Message; return false; }
            string outp = (r.Output ?? "").Replace("FAIL:", "").Trim();
            if (r.TimedOut) { message = "tempo esgotado ao criar o ponto de restauração"; return false; }
            if (r.ExitCode != 0) { message = outp.Length > 0 ? outp : "falhou (código " + r.ExitCode + ")"; return false; }
            message = "Ponto de restauração criado";
            return true;
        }

        // ------------------------------------------------------------------
        public SessionResult Apply(IList<Tweak> tweaks, bool restorePoint, string windowsInfo, ProgressHandler progress)
        {
            SessionResult result = new SessionResult();
            SessionBackup backup = new SessionBackup();
            backup.Id = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            backup.Date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            backup.Machine = Environment.MachineName;
            backup.Windows = windowsInfo;
            backup.AppVersion = AppInfo.Version;
            result.Backup = backup;

            StringBuilder log = new StringBuilder();
            log.AppendLine(string.Format("{0} {1} — sessão {2}", AppInfo.Name, AppInfo.Version, backup.Id));
            log.AppendLine("Computador: " + backup.Machine);
            log.AppendLine("Windows: " + windowsInfo);
            if (ExtraLogHeader != null) log.AppendLine(ExtraLogHeader);
            log.AppendLine();

            int total = tweaks.Count + (restorePoint ? 1 : 0);
            int done = 0;

            if (restorePoint)
            {
                if (progress != null) progress(done, total, "Criando ponto de restauração…");
                string msg;
                result.RestorePointCreated = CreateRestorePoint(out msg);
                result.RestorePointMessage = msg;
                log.AppendLine((result.RestorePointCreated ? "[OK]    " : "[FALHA] ") + "Ponto de restauração: " + msg);
                if (RestorePointDone != null) RestorePointDone(result.RestorePointCreated, msg);
                done++;
            }

            foreach (Tweak t in tweaks)
            {
                if (progress != null) progress(done, total, t.Title);
                TweakResult tr = new TweakResult();
                tr.Tweak = t;
                TweakUndo tu = new TweakUndo();
                tu.TweakId = t.Id;
                tu.TweakTitle = t.Title;

                bool anyOk = false, anyFail = false;
                foreach (TweakAction a in t.Actions)
                {
                    ActionResult ar;
                    try { ar = a.Apply(sys); }
                    catch (Exception ex) { ar = ActionResult.Fail(ex.Message); }

                    if (ar.Outcome == ActionOutcome.Ok) anyOk = true;
                    if (ar.Outcome == ActionOutcome.Failed) anyFail = true;
                    if (ar.Undo != null) tu.Records.Add(ar.Undo);
                    string tag = ar.Outcome == ActionOutcome.Ok ? "ok" : ar.Outcome == ActionOutcome.Skipped ? "ignorado" : "FALHA";
                    tr.Messages.Add(string.Format("{0}: {1} — {2}", tag, a.Describe(), ar.Message));
                }

                tr.Outcome = anyFail ? ActionOutcome.Failed : anyOk ? ActionOutcome.Ok : ActionOutcome.Skipped;
                if (tu.Records.Count > 0) backup.Tweaks.Add(tu);
                if (!t.IsReversible && anyOk) backup.Irreversible.Add(t.Title);
                if (t.NeedsRestart && anyOk) result.NeedsRestart = true;
                result.Results.Add(tr);

                string head = tr.Outcome == ActionOutcome.Ok ? "[OK]    " : tr.Outcome == ActionOutcome.Skipped ? "[IGNOR] " : "[FALHA] ";
                log.AppendLine(head + t.Title + "  (" + t.Id + ")");
                foreach (string m in tr.Messages) log.AppendLine("          " + m);
                if (TweakDone != null) TweakDone(tr);
                done++;
            }

            log.AppendLine();
            log.AppendLine(string.Format("Resumo: {0} aplicados, {1} ignorados, {2} com falha.",
                result.Count(ActionOutcome.Ok), result.Count(ActionOutcome.Skipped), result.Count(ActionOutcome.Failed)));

            try
            {
                Directory.CreateDirectory(SessionsDir);
                Directory.CreateDirectory(LogsDir);
                if (backup.Tweaks.Count > 0 || backup.Irreversible.Count > 0)
                    SaveBackup(backup, Path.Combine(SessionsDir, backup.Id + ".json"));
                result.LogPath = Path.Combine(LogsDir, backup.Id + ".log");
                File.WriteAllText(result.LogPath, log.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                result.LogPath = null;
                result.RestorePointMessage = (result.RestorePointMessage ?? "") + " | Não foi possível gravar o backup: " + ex.Message;
            }

            if (progress != null) progress(total, total, "Concluído");
            return result;
        }

        // ------------------------------------------------------------------
        public List<string> Undo(SessionBackup backup, ProgressHandler progress)
        {
            List<string> report = new List<string>();
            List<UndoRecord> all = new List<UndoRecord>();
            // Desfaz na ordem inversa da aplicação
            for (int i = backup.Tweaks.Count - 1; i >= 0; i--)
                for (int j = backup.Tweaks[i].Records.Count - 1; j >= 0; j--)
                    all.Add(backup.Tweaks[i].Records[j]);

            int done = 0;
            int fails = 0;
            foreach (UndoRecord u in all)
            {
                if (progress != null) progress(done, all.Count, u.Description);
                string err = UndoOne(u);
                if (err == null) report.Add("ok: " + u.Description);
                else { report.Add("FALHA: " + u.Description + " — " + err); fails++; }
                done++;
            }

            backup.Undone = fails == 0;
            if (backup.FilePath != null)
            {
                try { SaveBackup(backup, backup.FilePath); } catch { }
            }
            try
            {
                Directory.CreateDirectory(LogsDir);
                File.WriteAllText(Path.Combine(LogsDir, backup.Id + "-desfazer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"),
                    string.Join(Environment.NewLine, report.ToArray()), new UTF8Encoding(true));
            }
            catch { }
            if (progress != null) progress(all.Count, all.Count, "Concluído");
            return report;
        }

        string UndoOne(UndoRecord u)
        {
            try
            {
                switch (u.Type)
                {
                    case "reg":
                        if (u.Existed)
                            sys.WriteValue(u.Key, u.Name, (RegKind)Enum.Parse(typeof(RegKind), u.Kind), u.Data);
                        else
                            sys.DeleteValue(u.Key, u.Name);
                        return null;
                    case "regnew":
                        sys.DeleteValue(u.Key, u.Name);
                        // apaga, de baixo para cima, só as chaves que nós criamos e que ficaram vazias
                        string top = RegPath.Normalize(u.Data);
                        string k = RegPath.Normalize(u.Key);
                        while (k != null && k.Length >= top.Length && k.StartsWith(top, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!sys.KeyExists(k) || !sys.KeyIsEmpty(k)) break;
                            sys.DeleteEmptyKey(k);
                            k = RegPath.Parent(k);
                        }
                        return null;
                    case "ps":
                        ProcResult r = sys.RunPowerShell(PsAction.Wrap(u.Script), 900);
                        if (r.TimedOut) return "tempo esgotado";
                        if (r.ExitCode != 0) return (r.Output ?? "").Replace("FAIL:", "").Trim();
                        return null;
                }
                return "tipo desconhecido: " + u.Type;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ------------------------------------------------------------------
        public List<SessionBackup> ListSessions()
        {
            List<SessionBackup> list = new List<SessionBackup>();
            if (!Directory.Exists(SessionsDir)) return list;
            string[] files = Directory.GetFiles(SessionsDir, "*.json");
            Array.Sort(files);
            Array.Reverse(files);
            foreach (string f in files)
            {
                try
                {
                    SessionBackup b = LoadBackup(f);
                    b.FilePath = f;
                    list.Add(b);
                }
                catch { /* arquivo corrompido: ignora */ }
            }
            return list;
        }

        public string LogsFolder { get { return LogsDir; } }

        public static void SaveBackup(SessionBackup b, string path)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(SessionBackup));
            using (FileStream fs = File.Create(path))
                ser.WriteObject(fs, b);
            b.FilePath = path;
        }

        public static SessionBackup LoadBackup(string path)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(SessionBackup));
            using (FileStream fs = File.OpenRead(path))
            {
                SessionBackup b = (SessionBackup)ser.ReadObject(fs);
                if (b.Tweaks == null) b.Tweaks = new List<TweakUndo>();
                if (b.Irreversible == null) b.Irreversible = new List<string>();
                foreach (TweakUndo t in b.Tweaks) if (t.Records == null) t.Records = new List<UndoRecord>();
                return b;
            }
        }
    }

    /// <summary>Detecta a versão do Windows pelo registro (o ProductName mente no Windows 11).</summary>
    public class WinInfo
    {
        public WinVersion Version = WinVersion.Win10;
        public int Build;
        public string DisplayVersion = "";
        public string Edition = "";
        public bool IsHome;

        public static WinInfo Detect(ISystem sys)
        {
            WinInfo w = new WinInfo();
            const string k = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            try
            {
                RegValue b = sys.ReadValue(k, "CurrentBuildNumber") ?? sys.ReadValue(k, "CurrentBuild");
                if (b != null) int.TryParse(b.Data, out w.Build);
                RegValue dv = sys.ReadValue(k, "DisplayVersion") ?? sys.ReadValue(k, "ReleaseId");
                if (dv != null) w.DisplayVersion = dv.Data;
                RegValue ed = sys.ReadValue(k, "EditionID");
                if (ed != null) w.Edition = ed.Data;
            }
            catch { }
            w.Version = w.Build >= 22000 ? WinVersion.Win11 : WinVersion.Win10;
            w.IsHome = w.Edition.StartsWith("Core", StringComparison.OrdinalIgnoreCase);
            return w;
        }

        public override string ToString()
        {
            string ed = IsHome ? "Home" : (Edition.Length > 0 ? Edition : "?");
            return string.Format("Windows {0} {1} {2} (build {3})", Version == WinVersion.Win11 ? "11" : "10", ed, DisplayVersion, Build).Replace("  ", " ");
        }
    }
}
