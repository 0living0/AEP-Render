using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AepBatchRenderer
{
    // AE's per-project render logs are separate from aerender stdout and the application's diagnostics.
    internal sealed class RenderLogCleanup : IDisposable
    {
        private readonly string folder, projectName;
        private readonly HashSet<string> previous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Action<string> report;
        private readonly bool existed;
        private bool armed, disposed;

        public RenderLogCleanup(string project, Action<string> report)
        {
            this.report=report;
            folder=Path.GetFullPath(project)+" Logs";
            projectName=Path.GetFileName(project);
            try
            {
                existed=Directory.Exists(folder);
                if (existed)
                {
                    if (IsLink(folder)) return;
                    foreach(string file in Directory.GetFiles(folder)) previous.Add(file);
                }
                armed=true;
            }
            catch(Exception e) { Report(UiText.Get("text.254")+e.Message); }
        }

        private static bool IsLink(string path)
        { return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0; }

        private bool IsOwnedLog(string file)
        {
            if (!Path.GetFileName(file).StartsWith("AE ",StringComparison.OrdinalIgnoreCase) || !String.Equals(Path.GetExtension(file),".txt",StringComparison.OrdinalIgnoreCase) || IsLink(file)) return false;
            using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var reader=new StreamReader(stream,Encoding.UTF8,true))
            {
                // Bound the header read: unrelated files in this directory are never parsed in full.
                var header=new StringBuilder();
                for(int i=0;i<2048;i++) { int c=reader.Read(); if(c<0 || c=='\r' || c=='\n') break; header.Append((char)c); }
                string text=header.ToString();
                return text.StartsWith("Adobe After Effects v",StringComparison.Ordinal) &&
                    (text.EndsWith("render log for project “"+projectName+"”",StringComparison.OrdinalIgnoreCase) ||
                     text.EndsWith("render log for project \""+projectName+"\"",StringComparison.OrdinalIgnoreCase));
            }
        }

        private void Report(string text) { try { if(report!=null) report(text); } catch {} }

        public void Dispose()
        {
            if(disposed) return; disposed=true;
            if(!armed) return;
            try
            {
                if(!Directory.Exists(folder) || IsLink(folder)) return;
                int count=0;
                foreach(string file in Directory.GetFiles(folder))
                {
                    if(previous.Contains(file)) continue;
                    try { if(IsOwnedLog(file)) { File.Delete(file); count++; } }
                    catch(Exception e) { Report(UiText.Get("text.255")+file+"："+e.Message); }
                }
                // Non-recursive deletion only; preserve pre-existing directories and all unknown content.
                if(!existed && Directory.GetFileSystemEntries(folder).Length==0) Directory.Delete(folder);
                if(count>0) Report(UiText.Get("text.256")+count+UiText.Get("text.257"));
            }
            catch(Exception e) { Report(UiText.Get("text.258")+e.Message); }
        }
    }
}
