using System;
using System.IO;
using System.Linq;
namespace AepBatchRenderer
{
    internal static class TestsExplorer
    {
        private static int checks;
        private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message); checks++; }
        public static int Main()
        {
            try
            {
                string root=Environment.GetEnvironmentVariable("AEP_BATCH_DATA"); if(String.IsNullOrWhiteSpace(root)) throw new Exception("Use isolated AEP_BATCH_DATA");
                root=Path.GetFullPath(root);
                string folder=Path.Combine(root,"项目文件"),other=Path.Combine(root,"other"); Directory.CreateDirectory(folder); Directory.CreateDirectory(other);
                foreach(string shot in new[]{"s01c001_t03","s01c002_t02","s01c003_t05","s01c004_t01","s01c005_t02","s01c006_t04","s01c001_t01"}) File.WriteAllText(Path.Combine(folder,"TYMSZS_S1E10_"+shot+".aep"),"Mock file, not an AE project");
                string nested=Path.Combine(folder,"第一集","镜头");Directory.CreateDirectory(nested);
                File.WriteAllText(Path.Combine(nested,"TYMSZS_S1E10_s01c001_t05.aep"),"nested version");
                File.WriteAllText(Path.Combine(nested,"TYMSZS_S1E10_s01c007_t01.aepx"),"nested project");
                var model=new ExplorerModel(); model.Navigate(folder);
                Check(!model.Settings.RecursiveFolders&&model.Files.Count==7,"folder traversal defaults off and scans only the selected directory");
                model.Settings.RecursiveFolders=true;model.Refresh();model.Save();
                Check(model.Files.Count==9&&model.Files.Any(f=>f.FullPath.StartsWith(nested,StringComparison.OrdinalIgnoreCase)),"folder traversal discovers nested AEP and AEPX files");
                var recursiveReopen=new ExplorerModel();recursiveReopen.Navigate(folder);
                Check(recursiveReopen.Settings.RecursiveFolders&&recursiveReopen.Files.Count==9,"folder traversal persists across restart");
                model.Settings.RecursiveFolders=false;model.Refresh();model.Save();
                Check(model.Files.Count==7,"disabling folder traversal immediately returns to the selected directory");
                Check(model.Settings.Language=="zh","old or new preferences default to Chinese");
                model.Settings.Language="ja";model.Save();Check(new ExplorerModel().Settings.Language=="ja","Japanese language persists");
                model.Settings.Language="unsupported";model.Save();Check(new ExplorerModel().Settings.Language=="zh","unsupported language falls back to Chinese");
                model.Settings.Language="zh";model.Save();
                Check(model.Settings.ConfirmClearHistory,"history clear confirmation defaults enabled");
                model.Settings.ConfirmClearHistory=false;model.Save();Check(!new ExplorerModel().Settings.ConfirmClearHistory&&new ExplorerModel().Settings.ConfirmClearQueue,"history confirmation persists independently");
                model.Settings.ConfirmClearHistory=true;model.Save();
                Check(model.Settings.ConfirmClearQueue,"clear queue confirmation defaults enabled");
                model.Settings.ConfirmClearQueue=false;model.Save();Check(!new ExplorerModel().Settings.ConfirmClearQueue,"disabled clear confirmation persists across restart");
                model.Settings.ConfirmClearQueue=true;model.Save();Check(new ExplorerModel().Settings.ConfirmClearQueue,"clear confirmation can be re-enabled persistently");
                Check(model.Settings.ItemSize=="medium" && model.Settings.ColumnWidths.SequenceEqual(new double[]{60,16,24}),"object size and column defaults");
                model.Settings.ItemSize="large"; model.Settings.ColumnWidths=new double[]{420,160,260}; model.Settings.WindowWidth=1320; model.Settings.WindowHeight=850; model.Settings.WindowDpi=144; model.Settings.WindowMaximized=true; model.Save();
                var layout=new ExplorerModel().Settings;
                Check(layout.ItemSize=="large" && layout.ColumnWidths.SequenceEqual(new double[]{420,160,260}) && layout.WindowWidth==1320 && layout.WindowHeight==850 && layout.WindowDpi==144 && layout.WindowMaximized,"object size, column widths and window geometry persist");
                model.Settings.ItemSize="medium"; model.Settings.ColumnWidths=new double[]{60,16,24}; model.Settings.WindowWidth=0; model.Settings.WindowHeight=0; model.Settings.WindowMaximized=false; model.Save();
                Check(model.Settings.TextSize=="medium" && model.Settings.FollowSystem,"new General defaults are medium and system theme");
                model.Settings.TextSize="large"; model.Settings.FollowSystem=false; model.Settings.Theme="dark"; model.Save();
                var appearance=new ExplorerModel();
                Check(appearance.Settings.TextSize=="large" && !appearance.Settings.FollowSystem && appearance.Settings.Theme=="dark","General appearance choices persist across restart");
                model.Settings.TextSize="medium"; model.Settings.FollowSystem=true; model.Settings.Theme="light"; model.Save();
                Check(model.Settings.FavoriteFolders.Count==0,"folders are not automatically favorited");
                Check(model.ToggleFavorite()&&model.IsFavorite(folder),"favorite star saves the current folder");
                Check(new ExplorerModel().Settings.FavoriteFolders.Contains(folder,StringComparer.OrdinalIgnoreCase),"favorite persists across restart");
                model.Navigate(other);Check(!model.IsFavorite(other)&&model.Settings.FavoriteFolders.Count==1,"ordinary navigation does not change favorites");
                Check(model.ToggleFavorite()&&model.IsFavorite(other),"a second folder can be favorited");
                model.Navigate(folder);Check(!model.ToggleFavorite()&&!model.IsFavorite(folder)&&model.IsFavorite(other),"clicking a filled star removes only the current favorite");
                Check(Store.Load("settings.json",new Preferences()).Folder==folder,"folder persisted immediately, before application close");
                string first=model.Files[0].FullPath; model.Toggle(first); Check(model.Selected.Contains(first),"file toggles on"); model.Refresh(); Check(model.Selected.Contains(first),"refresh retains selection");
                model.Navigate(other); model.History(-1); Check(model.Folder==folder&&model.Selected.Contains(first),"history navigation retains file state");
                model.Toggle(first); Check(!model.Selected.Contains(first),"second click toggles only that file off");
                model.SetMany(model.Files.Select(f=>f.FullPath),true); Check(model.Selected.Count==7,"select-all selects files independently");
                var reopened=new ExplorerModel(); Check(reopened.Settings.Folder==folder&&reopened.Selected.Count==7,"reopen restores folder and checked files");
                bool failed=false; try { model.Navigate(Path.Combine(root,"missing")); } catch(IOException) { failed=true; } Check(failed&&Store.Load("settings.json",new Preferences()).Folder==folder,"invalid address cannot replace saved location");
                model.SetMany(model.Files.Select(f=>f.FullPath),false); Check(model.Selected.Count==0,"clear-all explicitly clears checked files");
                model.Toggle(model.Files.Single(f=>f.DisplayName=="s01c001_t03").FullPath); model.Toggle(model.Files.Single(f=>f.DisplayName=="s01c002_t02").FullPath);
                Console.WriteLine("SUCCESS: "+checks+" explorer checks"); return 0;
            }
            catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        }
    }
}
