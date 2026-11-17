// Descobre o que já está aplicado neste PC, por qualquer meio (este app, outra ferramenta ou à mão).
using System;
using System.Collections.Generic;
using System.Text;

namespace DebloatWin.Core
{
    public enum TweakState
    {
        Unknown,        // sem como verificar (ex.: limpezas)
        Checking,       // verificação em andamento
        NotApplied,
        Partial,
        Applied,
        NotApplicable   // nada deste ajuste existe neste PC
    }

    public class TweakStatus
    {
        public TweakState State;
        public int Applied;   // ações já aplicadas
        public int Known;     // ações com estado conhecido (aplicadas + não aplicadas)

        public string Label
        {
            get
            {
                switch (State)
                {
                    case TweakState.Applied: return "✓ Já aplicado";
                    case TweakState.Partial: return string.Format("Parcial {0}/{1}", Applied, Known);
                    case TweakState.Checking: return "Verificando…";
                    case TweakState.NotApplicable: return "Nada a fazer neste PC";
                    default: return null;
                }
            }
        }
    }

    public class Detector
    {
        readonly ISystem sys;
        readonly Dictionary<TweakAction, DetectState> results = new Dictionary<TweakAction, DetectState>();
        readonly object gate = new object();
        bool slowDone;

        public Detector(ISystem sys) { this.sys = sys; }

        public bool SlowDone { get { lock (gate) return slowDone; } }

        public DetectState StateOf(TweakAction a)
        {
            lock (gate)
            {
                DetectState s;
                return results.TryGetValue(a, out s) ? s : DetectState.Unknown;
            }
        }

        /// <summary>Verificações instantâneas (registro e serviços).</summary>
        public void DetectFast(IEnumerable<Tweak> tweaks)
        {
            foreach (Tweak t in tweaks)
                foreach (TweakAction a in t.Actions)
                {
                    DetectState s = a.Detect(sys);
                    if (s != DetectState.Unknown || a.DetectScript() == null)
                        lock (gate) results[a] = s;
                }
        }

        public const string Preamble =
            "$ProgressPreference = 'SilentlyContinue'\n" +
            "$script:dbwAppx = $null\n" +
            "function Get-DbwAppx {\n" +
            "  if ($null -eq $script:dbwAppx) {\n" +
            "    $script:dbwAppx = @(Get-AppxPackage -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }) +\n" +
            "                      @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | ForEach-Object { $_.DisplayName })\n" +
            "  }\n" +
            "  return $script:dbwAppx\n" +
            "}\n" +
            "$script:dbwCaps = $null\n" +
            "function Get-DbwCaps {\n" +
            "  if ($null -eq $script:dbwCaps) {\n" +
            "    $script:dbwCaps = @(Get-WindowsCapability -Online -ErrorAction SilentlyContinue | Where-Object { [string]$_.State -eq 'Installed' } | ForEach-Object { $_.Name })\n" +
            "  }\n" +
            "  return $script:dbwCaps\n" +
            "}\n";

        /// <summary>Monta um único script com a verificação de todas as ações que precisam de PowerShell.</summary>
        public static string BuildScript(IList<TweakAction> actions)
        {
            StringBuilder sb = new StringBuilder(Preamble);
            for (int i = 0; i < actions.Count; i++)
            {
                sb.Append("try { $r = [string](& {\n");
                sb.Append(actions[i].DetectScript());
                sb.Append("\n} | Select-Object -Last 1); Write-Output ('DET:").Append(i).Append(":' + $r) }\n");
                sb.Append("catch { Write-Output 'DET:").Append(i).Append(":?' }\n");
            }
            return sb.ToString();
        }

        /// <summary>Verificações que precisam de PowerShell (apps, tarefas, recursos, energia), numa execução só.</summary>
        public void DetectSlow(IEnumerable<Tweak> tweaks, int timeoutSeconds)
        {
            List<TweakAction> pending = new List<TweakAction>();
            foreach (Tweak t in tweaks)
                foreach (TweakAction a in t.Actions)
                {
                    bool known;
                    lock (gate) known = results.ContainsKey(a);
                    if (!known && a.DetectScript() != null) pending.Add(a);
                }

            if (pending.Count > 0)
            {
                ProcResult r = null;
                try { r = sys.RunPowerShell(BuildScript(pending), timeoutSeconds); }
                catch { }
                Dictionary<int, DetectState> parsed = Parse(r == null ? null : r.Output);
                lock (gate)
                    for (int i = 0; i < pending.Count; i++)
                    {
                        DetectState s;
                        results[pending[i]] = parsed.TryGetValue(i, out s) ? s : DetectState.Unknown;
                    }
            }
            lock (gate) slowDone = true;
        }

        public static Dictionary<int, DetectState> Parse(string output)
        {
            Dictionary<int, DetectState> d = new Dictionary<int, DetectState>();
            if (output == null) return d;
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("DET:")) continue;
                string[] p = line.Split(':');
                int i;
                if (p.Length < 3 || !int.TryParse(p[1], out i)) continue;
                string v = p[2].Trim();
                d[i] = v == "1" ? DetectState.Applied
                     : v == "0" ? DetectState.NotApplied
                     : v.Equals("NA", StringComparison.OrdinalIgnoreCase) ? DetectState.NotApplicable
                     : DetectState.Unknown;
            }
            return d;
        }

        /// <summary>Resume o estado do ajuste a partir das suas ações.</summary>
        public TweakStatus Summarize(Tweak t)
        {
            TweakStatus st = new TweakStatus();
            bool pending = false, anyNa = false, anyUnknown = false;
            foreach (TweakAction a in t.Actions)
            {
                DetectState s;
                bool has;
                lock (gate) has = results.TryGetValue(a, out s);
                if (!has)
                {
                    if (a.DetectScript() != null && !SlowDone) pending = true;
                    else anyUnknown = true;
                    continue;
                }
                switch (s)
                {
                    case DetectState.Applied: st.Applied++; st.Known++; break;
                    case DetectState.NotApplied: st.Known++; break;
                    case DetectState.NotApplicable: anyNa = true; break;
                    default: anyUnknown = true; break;
                }
            }
            if (pending) st.State = TweakState.Checking;
            else if (st.Known == 0) st.State = anyNa && !anyUnknown ? TweakState.NotApplicable : TweakState.Unknown;
            else if (st.Applied == st.Known) st.State = TweakState.Applied;
            else if (st.Applied == 0) st.State = TweakState.NotApplied;
            else st.State = TweakState.Partial;
            return st;
        }
    }
}
