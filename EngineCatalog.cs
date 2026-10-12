using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AepBatchRenderer
{
    public sealed class AeEngine
    {
        public string Path="", Version="", Label="", Error="";
        public int Major, Minor, Patch, Build;
        public bool Beta, Available;
    }
    public sealed class EngineMatch
    {
        public AepVersion Project;
        public AeEngine Engine;
    }
    internal static class EngineCatalog
    {
        public static string Label(int major)
        {
            if(major>=22 && major<=26) return "AE "+(2000+major);
            if(major==18) return "AE 2021";
            if(major==17) return "AE 2020";
            if(major==16) return "AE 2019";
            return "AE "+major+".x";
        }
        public static AeEngine Inspect(string path)
        {
            if(String.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path) || !String.Equals(System.IO.Path.GetFileName(path),"aerender.exe",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(UiText.Get("text.228"));
            path=System.IO.Path.GetFullPath(path);
            if(!File.Exists(path)) throw new FileNotFoundException(UiText.Get("text.229")+path);
            var info=FileVersionInfo.GetVersionInfo(path);
            if(info.FileMajorPart<11 || info.FileMajorPart>26 || (info.ProductName ?? "").IndexOf("After Effects",StringComparison.OrdinalIgnoreCase)<0 || (info.CompanyName ?? "").IndexOf("Adobe",StringComparison.OrdinalIgnoreCase)<0) throw new InvalidDataException(UiText.Get("text.230"));
            bool beta=info.IsPreRelease || path.IndexOf("(Beta)",StringComparison.OrdinalIgnoreCase)>=0 || (info.ProductName ?? "").IndexOf("Beta",StringComparison.OrdinalIgnoreCase)>=0;
            return new AeEngine { Path=path,Major=info.FileMajorPart,Minor=info.FileMinorPart,Patch=info.FileBuildPart,Build=info.FilePrivatePart,Version=info.FileMajorPart+"."+info.FileMinorPart+"."+info.FileBuildPart+"."+info.FilePrivatePart,Label=Label(info.FileMajorPart)+(beta ? " Beta" : ""),Beta=beta,Available=true };
        }
        public static List<AeEngine> Refresh(IEnumerable<AeEngine> engines)
        {
            var result=new List<AeEngine>();
            foreach(var e in engines ?? new AeEngine[0])
            {
                if(e==null || String.IsNullOrWhiteSpace(e.Path)) continue;
                if(result.Any(x=>String.Equals(x.Path,e.Path,StringComparison.OrdinalIgnoreCase))) continue;
                try { result.Add(Inspect(e.Path)); }
                catch(Exception ex) { result.Add(new AeEngine { Path=e.Path,Major=e.Major,Minor=e.Minor,Patch=e.Patch,Build=e.Build,Version=e.Version,Label=String.IsNullOrEmpty(e.Label) ? UiText.Get("text.018") : e.Label,Beta=e.Beta,Available=false,Error=ex.Message }); }
            }
            return result.OrderByDescending(e=>e.Major).ThenByDescending(e=>e.Minor).ThenByDescending(e=>e.Patch).ThenByDescending(e=>e.Build).ThenBy(e=>e.Path,StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static List<AeEngine> Scan()
        {
            var roots=new List<string>();
            foreach(var p in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
            {
                if(!String.IsNullOrWhiteSpace(p)) { roots.Add(System.IO.Path.Combine(p,"Adobe")); roots.Add(p); }
            }
            return Scan(roots);
        }
        internal static List<AeEngine> Scan(IEnumerable<string> roots)
        {
            var found=new List<AeEngine>();
            foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if(!Directory.Exists(root)) continue;
                    foreach(string dir in Directory.GetDirectories(root,"*After Effects*"))
                    {
                        string path=System.IO.Path.Combine(dir,"Support Files","aerender.exe");
                        if(File.Exists(path)) found.Add(new AeEngine { Path=path });
                    }
                }
                catch(UnauthorizedAccessException) { }
                catch(IOException) { }
            }
            return Refresh(found);
        }
        public static void Initialize(Preferences prefs)
        {
            prefs.Engines=prefs.Engines ?? new List<AeEngine>();
            if(!prefs.EnginesInitialized)
            {
                prefs.Engines.AddRange(Scan());
                if(!String.IsNullOrWhiteSpace(prefs.Aerender)) prefs.Engines.Add(new AeEngine { Path=prefs.Aerender });
                string extra=Environment.GetEnvironmentVariable("AEP_BATCH_AERENDER");
                if(!String.IsNullOrWhiteSpace(extra)) prefs.Engines.Add(new AeEngine { Path=extra });
                prefs.EnginesInitialized=true;
            }
            prefs.Engines=Refresh(prefs.Engines);
        }
        public static EngineMatch Resolve(string project)
        {
            var version=AepVersion.Read(project);
            return Resolve(version);
        }
        public static EngineMatch Resolve(AepVersion version)
        {
            var prefs=Store.Load("settings.json",new Preferences()) ?? new Preferences();
            var engines=Refresh(prefs.Engines);
            return new EngineMatch { Project=version,Engine=Select(version,engines) };
        }
        internal static AeEngine Select(AepVersion project,IEnumerable<AeEngine> engines)
        {
            var found=engines.Where(e=>e.Available && e.Major==project.Major && e.Beta==project.Beta).OrderByDescending(e=>e.Minor).ThenByDescending(e=>e.Patch).ThenByDescending(e=>e.Build).ThenBy(e=>e.Path,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if(found==null) throw new InvalidOperationException(UiText.Get("text.231")+Label(project.Major)+"（"+project.Text+UiText.Get("text.232")+(project.Beta ? " Beta" : "")+UiText.Get("text.233"));
            return found;
        }
    }
}
