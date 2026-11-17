// Acesso ao sistema (registro e processos) atrás de uma interface,
// para o motor poder ser testado fora do Windows com um sistema simulado.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace DebloatWin.Core
{
    public enum RegKind
    {
        String,
        ExpandString,
        DWord,
        QWord,
        MultiString,
        Binary
    }

    public class RegValue
    {
        public RegKind Kind;
        public string Data;   // sempre em texto: DWORD/QWORD em decimal, binário em hex, multi separado por \n

        public RegValue(RegKind kind, string data)
        {
            Kind = kind; Data = data;
        }

        public bool SameAs(RegKind kind, string data)
        {
            return Kind == kind && string.Equals(Data, data, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Kind, Data);
        }
    }

    public class ProcResult
    {
        public int ExitCode;
        public string Output;
        public bool TimedOut;
    }

    public interface ISystem
    {
        bool KeyExists(string fullKey);
        RegValue ReadValue(string fullKey, string name);
        void WriteValue(string fullKey, string name, RegKind kind, string data);
        void DeleteValue(string fullKey, string name);
        void DeleteKeyTree(string fullKey);
        bool KeyIsEmpty(string fullKey);
        void DeleteEmptyKey(string fullKey);
        ProcResult RunPowerShell(string script, int timeoutSeconds);
    }

    public static class RegPath
    {
        /// <summary>Normaliza o prefixo da hive: HKLM, HKCU, HKU ou HKCR.</summary>
        public static void Split(string fullKey, out string hive, out string subKey)
        {
            if (string.IsNullOrEmpty(fullKey)) throw new ArgumentException("Chave vazia");
            string k = fullKey.Replace('/', '\\').Trim('\\');
            int i = k.IndexOf('\\');
            string h = i < 0 ? k : k.Substring(0, i);
            subKey = i < 0 ? "" : k.Substring(i + 1);
            switch (h.ToUpperInvariant())
            {
                case "HKLM":
                case "HKEY_LOCAL_MACHINE": hive = "HKLM"; break;
                case "HKCU":
                case "HKEY_CURRENT_USER": hive = "HKCU"; break;
                case "HKU":
                case "HKEY_USERS": hive = "HKU"; break;
                case "HKCR":
                case "HKEY_CLASSES_ROOT": hive = "HKCR"; break;
                default: throw new ArgumentException("Hive desconhecida: " + h);
            }
        }

        public static string Normalize(string fullKey)
        {
            string hive, sub;
            Split(fullKey, out hive, out sub);
            return sub.Length == 0 ? hive : hive + "\\" + sub;
        }

        /// <summary>Caminho no formato que o PowerShell aceita sem PSDrive.</summary>
        public static string ToPowerShellPath(string fullKey)
        {
            string hive, sub;
            Split(fullKey, out hive, out sub);
            string longHive;
            switch (hive)
            {
                case "HKLM": longHive = "HKEY_LOCAL_MACHINE"; break;
                case "HKCU": longHive = "HKEY_CURRENT_USER"; break;
                case "HKU": longHive = "HKEY_USERS"; break;
                default: longHive = "HKEY_CLASSES_ROOT"; break;
            }
            return "Registry::" + longHive + (sub.Length > 0 ? "\\" + sub : "");
        }

        public static string Parent(string fullKey)
        {
            string n = Normalize(fullKey);
            int i = n.LastIndexOf('\\');
            return i < 0 ? null : n.Substring(0, i);
        }
    }

    /// <summary>Implementação real, para o Windows.</summary>
    public class WindowsSystem : ISystem
    {
        static RegistryKey Base(string hive)
        {
            RegistryHive h;
            switch (hive)
            {
                case "HKLM": h = RegistryHive.LocalMachine; break;
                case "HKCU": h = RegistryHive.CurrentUser; break;
                case "HKU": h = RegistryHive.Users; break;
                default: h = RegistryHive.ClassesRoot; break;
            }
            // Sempre a visão de 64 bits, mesmo se o processo rodar em 32 bits.
            return RegistryKey.OpenBaseKey(h, RegistryView.Registry64);
        }

        public bool KeyExists(string fullKey)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            using (RegistryKey b = Base(hive))
            using (RegistryKey k = b.OpenSubKey(sub, false))
                return k != null;
        }

        public RegValue ReadValue(string fullKey, string name)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            using (RegistryKey b = Base(hive))
            using (RegistryKey k = b.OpenSubKey(sub, false))
            {
                if (k == null) return null;
                object v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (v == null) return null;
                RegistryValueKind kind = k.GetValueKind(name);
                switch (kind)
                {
                    case RegistryValueKind.DWord:
                        return new RegValue(RegKind.DWord, unchecked((uint)(int)v).ToString());
                    case RegistryValueKind.QWord:
                        return new RegValue(RegKind.QWord, unchecked((ulong)(long)v).ToString());
                    case RegistryValueKind.ExpandString:
                        return new RegValue(RegKind.ExpandString, (string)v);
                    case RegistryValueKind.MultiString:
                        return new RegValue(RegKind.MultiString, string.Join("\n", (string[])v));
                    case RegistryValueKind.Binary:
                        return new RegValue(RegKind.Binary, Hex.FromBytes((byte[])v));
                    case RegistryValueKind.String:
                        return new RegValue(RegKind.String, (string)v);
                    default:
                        // Tipos raros (REG_NONE etc.): guardamos como binário cru se possível.
                        byte[] raw = v as byte[];
                        return raw != null ? new RegValue(RegKind.Binary, Hex.FromBytes(raw)) : new RegValue(RegKind.String, v.ToString());
                }
            }
        }

        public void WriteValue(string fullKey, string name, RegKind kind, string data)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            using (RegistryKey b = Base(hive))
            using (RegistryKey k = b.CreateSubKey(sub, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (k == null) throw new InvalidOperationException("Não foi possível criar a chave " + fullKey);
                switch (kind)
                {
                    case RegKind.DWord:
                        k.SetValue(name, unchecked((int)uint.Parse(data)), RegistryValueKind.DWord); break;
                    case RegKind.QWord:
                        k.SetValue(name, unchecked((long)ulong.Parse(data)), RegistryValueKind.QWord); break;
                    case RegKind.ExpandString:
                        k.SetValue(name, data, RegistryValueKind.ExpandString); break;
                    case RegKind.MultiString:
                        k.SetValue(name, data.Length == 0 ? new string[0] : data.Split('\n'), RegistryValueKind.MultiString); break;
                    case RegKind.Binary:
                        k.SetValue(name, Hex.ToBytes(data), RegistryValueKind.Binary); break;
                    default:
                        k.SetValue(name, data, RegistryValueKind.String); break;
                }
            }
        }

        public void DeleteValue(string fullKey, string name)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            using (RegistryKey b = Base(hive))
            using (RegistryKey k = b.OpenSubKey(sub, true))
            {
                if (k != null) k.DeleteValue(name, false);
            }
        }

        public void DeleteKeyTree(string fullKey)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            if (sub.Length == 0) throw new InvalidOperationException("Recusado: apagar uma hive inteira.");
            using (RegistryKey b = Base(hive))
            {
                b.DeleteSubKeyTree(sub, false);
            }
        }

        public bool KeyIsEmpty(string fullKey)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            using (RegistryKey b = Base(hive))
            using (RegistryKey k = b.OpenSubKey(sub, false))
                return k != null && k.SubKeyCount == 0 && k.ValueCount == 0;
        }

        public void DeleteEmptyKey(string fullKey)
        {
            string hive, sub;
            RegPath.Split(fullKey, out hive, out sub);
            if (sub.Length == 0) throw new InvalidOperationException("Recusado: apagar uma hive inteira.");
            using (RegistryKey b = Base(hive))
                b.DeleteSubKey(sub, false);
        }

        public ProcResult RunPowerShell(string script, int timeoutSeconds)
        {
            string full = "$ProgressPreference='SilentlyContinue'\n" +
                          "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8\n" +
                          script;
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
            string exe = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(exe)) exe = "powershell.exe";

            ProcessStartInfo psi = new ProcessStartInfo(exe,
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            StringBuilder sb = new StringBuilder();
            object gate = new object();
            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (gate) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (gate) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                ProcResult r = new ProcResult();
                if (!p.WaitForExit(timeoutSeconds * 1000))
                {
                    try { p.Kill(); } catch { }
                    r.TimedOut = true;
                    r.ExitCode = -1;
                }
                else
                {
                    p.WaitForExit(); // garante que os buffers assíncronos terminaram
                    r.ExitCode = p.ExitCode;
                }
                lock (gate) r.Output = sb.ToString().TrimEnd();
                return r;
            }
        }
    }

    public static class Hex
    {
        public static string FromBytes(byte[] b)
        {
            StringBuilder sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        public static byte[] ToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace(",", "");
            if (hex.Length % 2 != 0) throw new FormatException("Hex com tamanho ímpar");
            byte[] r = new byte[hex.Length / 2];
            for (int i = 0; i < r.Length; i++)
                r[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return r;
        }
    }

    public static class Ps
    {
        /// <summary>String entre aspas simples, segura para o PowerShell.</summary>
        public static string Q(string s)
        {
            return "'" + (s ?? "").Replace("'", "''") + "'";
        }

        public static string List(IEnumerable<string> items)
        {
            List<string> q = new List<string>();
            foreach (string s in items) q.Add(Q(s));
            return "@(" + string.Join(",", q.ToArray()) + ")";
        }
    }
}
