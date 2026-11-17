// Leitura (só leitura) de processador, memória e disco para o Dashboard. Não altera nada no PC.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DebloatWin.App
{
    public class SystemSnapshot
    {
        public string CpuName;
        public int CpuPercent = -1;      // -1 = ainda sem leitura
        public double RamTotalGb = -1, RamUsedGb = -1;
        public double DiskTotalGb = -1, DiskFreeGb = -1;
        public string DiskName = "C:";
        public int Processes = -1;
    }

    public class SystemStats
    {
        /// <summary>Usado só nas prévias (Linux), onde as chamadas do Windows não existem.</summary>
        public static Func<SystemSnapshot> Override;

        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME { public uint Low, High; public ulong Value { get { return ((ulong)High << 32) | Low; } } }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);

        ulong lastIdle, lastTotal;
        bool hasLast;
        public string CpuName;

        /// <summary>Lê os números atuais. A % do processador precisa de duas leituras seguidas.</summary>
        public SystemSnapshot Read()
        {
            if (Override != null) return Override();
            SystemSnapshot s = new SystemSnapshot();
            s.CpuName = CpuName;
            try
            {
                FILETIME idle, kernel, user;
                if (GetSystemTimes(out idle, out kernel, out user))
                {
                    ulong total = kernel.Value + user.Value; // kernel já inclui o tempo ocioso
                    if (hasLast && total > lastTotal)
                    {
                        double busy = 1.0 - (double)(idle.Value - lastIdle) / (total - lastTotal);
                        s.CpuPercent = (int)Math.Round(Math.Max(0, Math.Min(1, busy)) * 100);
                    }
                    lastIdle = idle.Value; lastTotal = total; hasLast = true;
                }
            }
            catch { }
            try
            {
                MEMORYSTATUSEX m = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(m))
                {
                    s.RamTotalGb = m.ullTotalPhys / 1073741824.0;
                    s.RamUsedGb = (m.ullTotalPhys - m.ullAvailPhys) / 1073741824.0;
                }
            }
            catch { }
            try
            {
                System.Diagnostics.Process[] ps = System.Diagnostics.Process.GetProcesses();
                s.Processes = ps.Length;
                foreach (System.Diagnostics.Process x in ps) x.Dispose();
            }
            catch { }
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.IsNullOrEmpty(root)) root = Path.GetPathRoot(Environment.CurrentDirectory);
                DriveInfo d = new DriveInfo(root);
                s.DiskName = root.TrimEnd('\\', '/');
                if (s.DiskName.Length == 0) s.DiskName = "/";
                s.DiskTotalGb = d.TotalSize / 1073741824.0;
                s.DiskFreeGb = d.AvailableFreeSpace / 1073741824.0;
            }
            catch { }
            return s;
        }
    }
}
