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
            "}\n" +
            // winget export devolve, em JSON, os pacotes instalados que o winget reconhece (instalados por ele ou não)
            "$script:dbwWinget = $false\n" +
            "function Get-DbwWingetIds {\n" +
            "  if ($script:dbwWinget -eq $false) {\n" +
            "    $script:dbwWinget = $null\n" +
            "    try {\n" +
            "      $w = (Get-Command winget.exe -ErrorAction Stop).Source\n" +
            "      $tmp = Join-Path $env:TEMP ('dbw-winget-' + [guid]::NewGuid().ToString('N') + '.json')\n" +
            "      & $w export -o $tmp --accept-source-agreements | Out-Null\n" +
            "      if (Test-Path $tmp) {\n" +
            "        $j = Get-Content $tmp -Raw | ConvertFrom-Json\n" +
            "        $script:dbwWinget = @($j.Sources | ForEach-Object { $_.Packages } | ForEach-Object { $_.PackageIdentifier })\n" +
            "        Remove-Item $tmp -Force -ErrorAction SilentlyContinue\n" +
            "      }\n" +
            "    } catch { $script:dbwWinget = $null }\n" +
            "  }\n" +
            "  return $script:dbwWinget\n" +
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

        /// <summary>Chamado (em segundo plano) quando a primeira leva de verificações termina.</summary>
        public Action PartialDone;

        /// <summary>
        /// Verificações que precisam de PowerShell, em duas levas: primeiro as rápidas (apps, tarefas, recursos,
        /// energia, drivers…), depois as que dependem do winget, que é o mais lento e às vezes demora a responder.
        /// </summary>
        public void DetectSlow(IEnumerable<Tweak> tweaks, int timeoutSeconds)
        {
            List<TweakAction> fast = new List<TweakAction>(), winget = new List<TweakAction>();
            foreach (Tweak t in tweaks)
                foreach (TweakAction a in t.Actions)
                {
                    bool known;
                    lock (gate) known = results.ContainsKey(a);
                    string ds = a.DetectScript();
                    if (known || ds == null) continue;
                    if (ds.Contains("Get-DbwWingetIds")) winget.Add(a); else fast.Add(a);
                }

            RunBatch(fast, timeoutSeconds);
            if (PartialDone != null && winget.Count > 0) { try { PartialDone(); } catch { } }
            RunBatch(winget, timeoutSeconds);
            lock (gate) slowDone = true;
        }

        void RunBatch(List<TweakAction> pending, int timeoutSeconds)
        {
            if (pending.Count == 0) return;
            ProcResult r = null;
            try { r = sys.RunPowerShell(BuildScript(pending), timeoutSeconds); }
            catch { }
            Dictionary<int, DetectState> parsed = Parse(r == null ? null : r.Output);
            lock (gate)
                for (int i = 0; i < pending.Count; i++)
                {
                    DetectState st;
                    results[pending[i]] = parsed.TryGetValue(i, out st) ? st : DetectState.Unknown;
                }
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
