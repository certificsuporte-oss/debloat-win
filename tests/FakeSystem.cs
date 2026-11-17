// Sistema simulado: registro em memória e PowerShell com respostas programadas.
using System;
using System.Collections.Generic;
using DebloatWin.Core;

namespace DebloatWin.Tests
{
    public class FakeSystem : ISystem
    {
        // chave normalizada em minúsculas → (nome em minúsculas → valor)
        public readonly Dictionary<string, Dictionary<string, RegValue>> Keys =
            new Dictionary<string, Dictionary<string, RegValue>>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Scripts = new List<string>();
        public Func<string, ProcResult> PowerShellHandler;
        public Action<string> LiveOutput { get; set; }

        public FakeSystem()
        {
            foreach (string k in new string[] {
                "HKLM", "HKCU", "HKU", "HKCR",
                @"HKLM\SOFTWARE", @"HKLM\SOFTWARE\Policies", @"HKLM\SOFTWARE\Policies\Microsoft",
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows", @"HKLM\SOFTWARE\Microsoft",
                @"HKLM\SOFTWARE\Microsoft\Windows", @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion",
                @"HKLM\SYSTEM", @"HKLM\SYSTEM\CurrentControlSet", @"HKLM\SYSTEM\CurrentControlSet\Services",
                @"HKLM\SYSTEM\CurrentControlSet\Control",
                @"HKCU\Software", @"HKCU\Software\Microsoft", @"HKCU\Software\Microsoft\Windows",
                @"HKCU\Software\Microsoft\Windows\CurrentVersion", @"HKCU\Software\Policies",
                @"HKCU\Control Panel", @"HKU\.DEFAULT" })
                Keys[RegPath.Normalize(k)] = new Dictionary<string, RegValue>(StringComparer.OrdinalIgnoreCase);
        }

        public void AddService(string name, int start)
        {
            WriteValue(@"HKLM\SYSTEM\CurrentControlSet\Services\" + name, "Start", RegKind.DWord, start.ToString());
        }

        public bool KeyExists(string fullKey)
        {
            return Keys.ContainsKey(RegPath.Normalize(fullKey));
        }

        public RegValue ReadValue(string fullKey, string name)
        {
            Dictionary<string, RegValue> k;
            if (!Keys.TryGetValue(RegPath.Normalize(fullKey), out k)) return null;
            RegValue v;
            return k.TryGetValue(name, out v) ? new RegValue(v.Kind, v.Data) : null;
        }

        public void WriteValue(string fullKey, string name, RegKind kind, string data)
        {
            string n = RegPath.Normalize(fullKey);
            // cria a chave e as chaves-pai, como o registro real
            string p = n;
            while (p != null && !Keys.ContainsKey(p))
            {
                Keys[p] = new Dictionary<string, RegValue>(StringComparer.OrdinalIgnoreCase);
                p = RegPath.Parent(p);
            }
            Keys[n][name] = new RegValue(kind, data);
        }

        public void DeleteValue(string fullKey, string name)
        {
            Dictionary<string, RegValue> k;
            if (Keys.TryGetValue(RegPath.Normalize(fullKey), out k)) k.Remove(name);
        }

        public void DeleteKeyTree(string fullKey)
        {
            string n = RegPath.Normalize(fullKey);
            List<string> rm = new List<string>();
            foreach (string k in Keys.Keys)
                if (k.Equals(n, StringComparison.OrdinalIgnoreCase) || k.StartsWith(n + "\\", StringComparison.OrdinalIgnoreCase))
                    rm.Add(k);
            foreach (string k in rm) Keys.Remove(k);
        }

        public bool KeyIsEmpty(string fullKey)
        {
            string n = RegPath.Normalize(fullKey);
            Dictionary<string, RegValue> k;
            if (!Keys.TryGetValue(n, out k)) return false;
            if (k.Count > 0) return false;
            foreach (string other in Keys.Keys)
                if (other.StartsWith(n + "\\", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public void DeleteEmptyKey(string fullKey)
        {
            if (!KeyIsEmpty(fullKey)) throw new InvalidOperationException("chave não está vazia: " + fullKey);
            Keys.Remove(RegPath.Normalize(fullKey));
        }

        public ProcResult RunPowerShell(string script, int timeoutSeconds)
        {
            Scripts.Add(script);
            if (PowerShellHandler != null) return PowerShellHandler(script);
            ProcResult r = new ProcResult();
            r.ExitCode = 0; r.Output = "ok";
            return r;
        }

        /// <summary>Foto do registro inteiro, para comparar antes e depois.</summary>
        public string Snapshot()
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, Dictionary<string, RegValue>> k in Keys)
            {
                lines.Add("[" + k.Key.ToLowerInvariant() + "]");
                foreach (KeyValuePair<string, RegValue> v in k.Value)
                    lines.Add("[" + k.Key.ToLowerInvariant() + "] " + v.Key.ToLowerInvariant() + "=" + v.Value);
            }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines.ToArray());
        }
    }
}
