using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AepBatchRenderer
{
    // Kill-on-close Job Object limits cancellation to this render process and descendants.
    internal sealed class ProcessScope : IDisposable
    {
        private IntPtr handle;
        public ProcessScope()
        {
            handle = CreateJobObject(IntPtr.Zero, null);
            if (handle == IntPtr.Zero) throw new Win32Exception();
            Extended limits = new Extended(); limits.Basic.Flags = 0x2000;
            int size = Marshal.SizeOf(limits); IntPtr p = Marshal.AllocHGlobal(size);
            try { Marshal.StructureToPtr(limits, p, false); if (!SetInformationJobObject(handle, 9, p, (uint)size)) throw new Win32Exception(); }
            catch { Dispose(); throw; }
            finally { Marshal.FreeHGlobal(p); }
        }
        public void Attach(Process process) { if (!AssignProcessToJobObject(handle, process.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), UiText.Get("process.error")); }
        public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
        [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long ProcessTime, JobTime; public uint Flags; public UIntPtr MinWorking, MaxWorking; public uint ActiveProcesses; public UIntPtr Affinity; public uint Priority, Scheduling; }
        [StructLayout(LayoutKind.Sequential)] struct IO { public ulong A, B, C, D, E, F; }
        [StructLayout(LayoutKind.Sequential)] struct Extended { public BasicLimits Basic; public IO Io; public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job, int type, IntPtr info, uint size);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    }
}
