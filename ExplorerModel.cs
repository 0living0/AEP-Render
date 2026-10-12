using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AepBatchRenderer
{
    internal sealed class ExplorerModel
    {
        public Preferences Settings;
        public string Folder = "";
        public List<AepFileItem> Files = new List<AepFileItem>();
        public readonly HashSet<string> Selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> history = new List<string>();
        private int position = -1;
        public ExplorerModel()
        {
            Settings = Store.Load("settings.json", new Preferences()) ?? new Preferences();
            Settings.Language=UiText.Normalize(Settings.Language);
            if(Settings.Options==null) Settings.Options=new RenderOptions();
            if(Settings.RecentFolders==null) Settings.RecentFolders=new List<string>();
            if(Settings.FavoriteFolders==null) Settings.FavoriteFolders=new List<string>();
            if(Settings.SelectedPaths!=null) foreach(var p in Settings.SelectedPaths) Selected.Add(p);
        }
        public bool CanBack { get { return position > 0; } }
        public bool CanForward { get { return position < history.Count-1; } }
        public void Navigate(string folder, bool remember = true)
        {
            if(File.Exists(folder)) folder=Path.GetDirectoryName(Path.GetFullPath(folder));
            if(!Directory.Exists(folder)) throw new IOException(UiText.Get("text.234"));
            string normalized=Path.GetFullPath(folder);
            var files=Scan(normalized); // A failed scan must not replace the last valid location.
            if(remember && !String.Equals(Folder,normalized,StringComparison.OrdinalIgnoreCase))
            { if(position<history.Count-1) history.RemoveRange(position+1,history.Count-position-1); history.Add(normalized); position=history.Count-1; }
            Folder=normalized; Files=files; Settings.Folder=normalized;
            Save(); // Persist immediately; independent of window close, focus or rendering.
        }
        public bool IsFavorite(string folder)
        { return Settings.FavoriteFolders.Any(p=>String.Equals(p,folder,StringComparison.OrdinalIgnoreCase)); }
        public bool ToggleFavorite()
        {
            string normalized=Path.GetFullPath(Folder);
            bool removed=Settings.FavoriteFolders.RemoveAll(p=>String.Equals(p,normalized,StringComparison.OrdinalIgnoreCase))>0;
            if(!removed) Settings.FavoriteFolders.Insert(0,normalized);
            Settings.FavoriteFolders=Settings.FavoriteFolders.Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList();
            Save(); return !removed;
        }
        public void History(int step)
        {
            int next=position+step; if(next<0 || next>=history.Count) return;
            Navigate(history[next],false); position=next;
        }
        public void Up() { var parent=Directory.GetParent(Folder); if(parent!=null) Navigate(parent.FullName); }
        public void Refresh() { Files=Scan(Folder); }
        private List<AepFileItem> Scan(string folder)
        {
            var paths=new List<string>();
            var pending=new Stack<string>(); pending.Push(folder);
            while(pending.Count>0)
            {
                string current=pending.Pop();
                try { paths.AddRange(Directory.EnumerateFiles(current).Where(IsProject)); }
                catch(UnauthorizedAccessException) { if(String.Equals(current,folder,StringComparison.OrdinalIgnoreCase)) throw; }
                catch(IOException) { if(String.Equals(current,folder,StringComparison.OrdinalIgnoreCase)) throw; }
                if(!Settings.RecursiveFolders) continue;
                try { foreach(string child in Directory.EnumerateDirectories(current)) pending.Push(child); }
                catch(UnauthorizedAccessException) { if(String.Equals(current,folder,StringComparison.OrdinalIgnoreCase)) throw; }
                catch(IOException) { if(String.Equals(current,folder,StringComparison.OrdinalIgnoreCase)) throw; }
            }
            return paths.Select(AepFileItem.FromPath).ToList();
        }
        private static bool IsProject(string path)
        { string extension=Path.GetExtension(path); return String.Equals(extension,".aep",StringComparison.OrdinalIgnoreCase)||String.Equals(extension,".aepx",StringComparison.OrdinalIgnoreCase); }
        public void Toggle(string path)
        { if(!Files.Any(f=>String.Equals(f.FullPath,path,StringComparison.OrdinalIgnoreCase))) return; if(!Selected.Add(path)) Selected.Remove(path); Save(); }
        public void SetMany(IEnumerable<string> paths,bool selected)
        {
            var valid=new HashSet<string>(Files.Select(f=>f.FullPath),StringComparer.OrdinalIgnoreCase);
            foreach(string p in paths.Where(valid.Contains)) { if(selected) Selected.Add(p); else Selected.Remove(p); } Save();
        }
        public void Save()
        { Settings.SelectedPaths=Selected.OrderBy(p=>p).ToList(); Store.Save("settings.json",Settings); }
    }
}
