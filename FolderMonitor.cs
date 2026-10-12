using System;
using System.IO;

namespace AepBatchRenderer
{
    // Events only mark dirty; the owning UI thread performs scans after a quiet period.
    internal sealed class FolderMonitor : IDisposable
    {
        private FileSystemWatcher watcher;
        private string folder="";
        private bool recursive;
        private readonly object gate=new object();
        private DateTime changed=DateTime.MinValue, fallback=DateTime.MinValue;
        private DateTime attachAfter=DateTime.MinValue;
        private void Dirty(object sender,FileSystemEventArgs e) { lock(gate) changed=DateTime.UtcNow; }
        private void Error(object sender,ErrorEventArgs e) { lock(gate) changed=DateTime.UtcNow; }
        public void Watch(string path,bool includeSubdirectories=false)
        {
            if(String.Equals(path,folder,StringComparison.OrdinalIgnoreCase) && recursive==includeSubdirectories && (watcher!=null || DateTime.UtcNow<attachAfter)) return;
            Dispose(); folder=path; recursive=includeSubdirectories;
            attachAfter=DateTime.UtcNow.AddSeconds(10);
            try {
                watcher=new FileSystemWatcher(path) { IncludeSubdirectories=includeSubdirectories, NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.Size|NotifyFilters.DirectoryName };
                watcher.Created+=Dirty; watcher.Deleted+=Dirty; watcher.Changed+=Dirty;
                watcher.Renamed+=Dirty; watcher.Error+=Error; watcher.EnableRaisingEvents=true;
            } catch(IOException) { Dispose(); } catch(UnauthorizedAccessException) { Dispose(); } catch(ArgumentException) { Dispose(); }
            fallback=DateTime.UtcNow;
        }
        public bool Due()
        {
            lock(gate) {
                var now=DateTime.UtcNow;
                if(now<fallback && (changed==DateTime.MinValue || (now-changed).TotalMilliseconds<650)) return false;
                changed=DateTime.MinValue; fallback=now.AddSeconds(10); return true;
            }
        }
        public void Dispose() { if(watcher!=null) { watcher.Dispose(); watcher=null; } }
    }
}
