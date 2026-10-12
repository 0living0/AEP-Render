using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyVersion("3.8.10.0")]
[assembly: AssemblyFileVersion("3.8.10.0")]

namespace AepBatchRenderer
{
    internal static class Bootstrap
    {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr LoadLibrary(string file);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
        public static string ResourceText(string name) { using(var r=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name))) return r.ReadToEnd(); }
        private static bool ActivateExisting()
        {
            int current=Process.GetCurrentProcess().Id;string name=Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            for(int attempt=0;attempt<20;attempt++)
            {
                foreach(Process process in Process.GetProcessesByName(name)) try
                {
                    if(process.Id==current) continue;process.Refresh();IntPtr window=process.MainWindowHandle;if(window==IntPtr.Zero) continue;
                    ShowWindowAsync(window,9);BringWindowToTop(window);SetForegroundWindow(window);return true;
                }
                catch { }
                Thread.Sleep(50);
            }
            return false;
        }
        [STAThread] private static void Main(string[] args)
        {
            UiText.Language=UiText.Normalize(Store.Load("settings.json",new Preferences()).Language);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            bool first; using(var mutex=new Mutex(true,"Local\\AepBatchRenderer.Explorer.v2"+(Array.IndexOf(args,"--ui-test")>=0?".Test."+Guid.NewGuid().ToString("N"):""),out first))
            {
                if(!first) { ActivateExisting(); return; }
                try
                {
                    AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs e) {
                        string name=new AssemblyName(e.Name).Name+".dll";
                        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                        { if(stream==null) return null; using(var bytes=new MemoryStream()) { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); } }
                    };
                    string native=Path.Combine(Store.Root,"runtime","1.0.4191.47","WebView2Loader.dll");
                    Directory.CreateDirectory(Path.GetDirectoryName(native));
                    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("WebView2Loader.dll"))
                    using(var bytes=new MemoryStream())
                    { stream.CopyTo(bytes); var content=bytes.ToArray(); if(!File.Exists(native) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(native),content)) File.WriteAllBytes(native,content); }
                    if(LoadLibrary(native)==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    Run(args);
                }
                catch(Exception e) { AppNotice.Show(UiText.Get("text.197"),e.Message); }
                finally { mutex.ReleaseMutex(); }
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Run(string[] args) { Application.Run(new WebForm(args)); }
    }
}
