using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AepBatchRenderer
{
    public sealed class RenderOptions
    {
        public string OutputFolder = "";
        public int Retries = 2, RetryDelay = 3, TimeoutMinutes = 0;
        public RenderOptions Copy() { return (RenderOptions)MemberwiseClone(); }
        public void Validate()
        {
            if (!String.IsNullOrEmpty(OutputFolder) && !Path.IsPathRooted(OutputFolder)) throw new Exception(UiText.Get("text.198"));
            if (Retries < 0 || Retries > 10 || RetryDelay < 1 || RetryDelay > 300 || TimeoutMinutes < 0 || TimeoutMinutes > 10080) throw new Exception(UiText.Get("text.199"));
        }
    }
    public sealed class Preferences
    {
        public string Language = "zh";
        public string Aerender = "", Folder = "";
        public List<AeEngine> Engines = new List<AeEngine>();
        public bool EnginesInitialized;
        public bool AllVersions, LargeIcons, RecursiveFolders;
        public int Concurrency = 1;
        public RenderOptions Options = new RenderOptions();
        public List<string> RecentFolders = new List<string>();
        public List<string> FavoriteFolders = new List<string>();
        public List<string> SelectedPaths = new List<string>();
        public string TextSize = "medium";
        public bool FollowSystem = true;
        public string Theme = "light";
        public bool ShowRecent = true;
        public bool ConfirmClearQueue = true;
        public bool ConfirmClearHistory = true;
        public string ItemSize = "medium";
        public double[] ColumnWidths = new double[] { 60,16,24 };
        public int WindowWidth, WindowHeight;
        public double WindowDpi = 96;
        public bool WindowMaximized;
    }
    public sealed class QueueJob
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Project = "", Engine = "", Name = "", Output = "";
        public string ProjectAeVersion = "", EngineVersion = "";
        public RenderOptions Options = new RenderOptions();
        public string State = UiText.Source("text.038"), Error = "", Progress = UiText.Get("text.200"), Log = "";
        public DateTime? FinishedUtc;
        public double? RenderSeconds;
        public bool DeleteWhenStopped, Requeued;
        public bool History { get { return State==UiText.Source("text.029") || State==UiText.Source("text.030"); } }
        public DateTime Created = DateTime.Now;
        public int Attempts;
        [ScriptIgnore] public CancellationTokenSource Cancellation;
        public bool Active { get { return State == UiText.Source("text.201") || State == UiText.Source("text.202") || State == UiText.Source("text.203"); } }
    }
    internal static class Store
    {
        public static readonly string Root = Environment.GetEnvironmentVariable("AEP_BATCH_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AepBatchRenderer");
        public static string Warning = "";
        private static readonly object Gate = new object();
        public static T Load<T>(string name, T fallback)
        {
            lock (Gate)
            {
                string path = Path.Combine(Root, name);
                if (!File.Exists(path)) return fallback;
                try { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)); }
                catch (Exception ex)
                {
                    Warning = UiText.Get("text.204") + name + UiText.Get("text.205") + ex.Message;
                    File.Copy(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), false);
                    if (File.Exists(path + ".bak")) try { return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path + ".bak", Encoding.UTF8)); } catch { }
                    return fallback;
                }
            }
        }
        public static void Save(string name, object value)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Root);
                string path = Path.Combine(Root, name), temp = path + ".tmp";
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(value), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", true);
                    if (!MoveFileEx(temp, path, 0x1 | 0x8)) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
                }
                else File.Move(temp, path);
            }
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode, SetLastError=true)]
        private static extern bool MoveFileEx(string source, string destination, int flags);
    }
    internal static class RenderCommand
    {
        public static string Quote(string value)
        {
            // Windows CommandLineToArgvW quoting, including trailing backslashes.
            var text = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                text.Append(c); slashes = 0;
            }
            text.Append('\\', slashes * 2); text.Append('"'); return text.ToString();
        }
        public static string OutputPath(string project, RenderOptions options)
        {
            return String.IsNullOrWhiteSpace(options.OutputFolder) ? "" : Path.GetFullPath(options.OutputFolder);
        }
        public static string Build(QueueJob job)
        {
            job.Options.Validate();
            return "-project " + Quote(job.Project);
        }
        public static bool Failure(string line)
        {
            string[] errors = { "aerender error", "After Effects error:", "Rendering error", "output module failed", "logged one error", "-1610153423", "no queued", "no render queue", "no renderable", "nothing to render", UiText.Source("text.206"), UiText.Source("text.207") };
            return errors.Any(s => line.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) || Regex.IsMatch(line, @"logged\s+[1-9]\d*\s+errors?", RegexOptions.IgnoreCase);
        }
    }
    internal sealed class RenderQueue
    {
        private readonly object gate = new object();
        private readonly List<QueueJob> jobs;
        public bool Paused = true;
        private HashSet<string> selectedBatch;
        public bool SelectedMode { get { lock(gate) return selectedBatch!=null; } }
        public int Concurrency = 1;
        public string StorageError = "";
        public RenderQueue()
        {
            jobs = Store.Load("queue.json", new List<QueueJob>()) ?? new List<QueueJob>();
            jobs.RemoveAll(j=>j.DeleteWhenStopped);
            foreach (var j in jobs) { if(j.Options==null) j.Options=new RenderOptions(); j.Output=RenderCommand.OutputPath(j.Project,j.Options); }
            foreach (QueueJob j in jobs.Where(j => j.Active)) { j.State = UiText.Source("text.208"); j.Progress = UiText.Get("text.209"); }
            bool recovered=false;foreach(QueueJob j in jobs.Where(j=>j.State==UiText.Source("queue.selectedPending")||j.State==UiText.Source("queue.held"))){j.State=UiText.Source("text.038");j.Progress=UiText.Get("queue.waitingProgress");recovered=true;}if(recovered)Save();
        }
        public List<QueueJob> Snapshot() { lock (gate) return jobs.ToList(); }
        public int ActiveCount { get { lock (gate) return jobs.Count(j => j.Active); } }
        private void Save()
        {
            try { Store.Save("queue.json", jobs); StorageError = ""; }
            catch (Exception e) { StorageError = UiText.Get("text.210") + e.Message; Paused = true; selectedBatch=null; }
        }
        public int Add(IEnumerable<AepFileItem> files, Preferences prefs)
        {
            prefs.Options.Validate();
            lock (gate)
            {
                var additions = new List<QueueJob>();
                foreach (var file in files)
                {
                    if (jobs.Concat(additions).Any(j => (j.Active || j.State == UiText.Source("text.038") || j.State == UiText.Source("queue.selectedPending") || j.State == UiText.Source("queue.held")) && String.Equals(j.Project, file.FullPath, StringComparison.OrdinalIgnoreCase))) continue;
                    string output = RenderCommand.OutputPath(file.FullPath, prefs.Options);
                    additions.Add(new QueueJob { Project = file.FullPath, Name = file.DisplayName, Options = prefs.Options.Copy(), Output = output });
                }
                jobs.AddRange(additions); Save(); return additions.Count;
            }
        }
        public string[] RequeueHistory(IEnumerable<string> ids,Preferences prefs)
        {
            prefs.Options.Validate();
            lock(gate)
            {
                var wanted=new HashSet<string>(ids);
                var sources=jobs.Where(j=>j.History && wanted.Contains(j.Id)).ToList();
                foreach(var source in sources) if(!File.Exists(source.Project)) throw new Exception(UiText.Get("text.213")+source.Project);
                var additions=new List<QueueJob>();
                foreach(var source in sources)
                {
                    var file=AepFileItem.FromPath(source.Project);
                    if(jobs.Concat(additions).Any(j=>(j.Active||j.State==UiText.Source("text.038")||j.State==UiText.Source("queue.selectedPending")||j.State==UiText.Source("queue.held")) && String.Equals(new FileInfo(j.Project).FullName,file.FullPath,StringComparison.OrdinalIgnoreCase))) continue;
                    additions.Add(new QueueJob { Project=file.FullPath,Name=file.DisplayName,Options=prefs.Options.Copy(),Output=RenderCommand.OutputPath(file.FullPath,prefs.Options) });
                    source.Requeued=true;
                }
                if(additions.Count>0){jobs.AddRange(additions);Save();}
                return additions.Select(j=>j.Id).ToArray();
            }
        }
        private void ReturnSelectedToWaiting(IEnumerable<string> ids=null)
        {
            HashSet<string> wanted=ids==null?null:new HashSet<string>(ids);
            foreach(var j in jobs.Where(j=>j.State==UiText.Source("queue.selectedPending")&&(wanted==null||wanted.Contains(j.Id)))){j.State=UiText.Source("text.038");j.Progress=UiText.Get("queue.waitingProgress");}
        }
        public void Start() { lock (gate) { ReturnSelectedToWaiting();selectedBatch=null;Save(); if (StorageError.Length == 0) Paused = false; } }
        public void Pause() { lock(gate) { ReturnSelectedToWaiting();Paused=true;selectedBatch=null;Save(); } }
        public void StartSelected(IEnumerable<string> ids)
        {
            lock(gate) {
                var wanted=new HashSet<string>(ids);
                var batch=new HashSet<string>(jobs.Where(j=>j.State==UiText.Source("text.038") && wanted.Contains(j.Id)).Select(j=>j.Id));
                if(batch.Count==0) throw new Exception(UiText.Get("text.211"));
                foreach(var j in jobs.Where(j=>batch.Contains(j.Id))){j.State=UiText.Source("queue.selectedPending");j.Progress=UiText.Get("queue.selectedPendingProgress");}
                Paused=true; selectedBatch=batch;Save();if(StorageError.Length>0){ReturnSelectedToWaiting(batch);selectedBatch=null;}
            }
        }
        public int ReleaseSelected(IEnumerable<string> ids)
        {
            lock(gate)
            {
                var wanted=new HashSet<string>(ids);
                var changed=jobs.Where(j=>wanted.Contains(j.Id)&&j.State==UiText.Source("queue.selectedPending")).ToList();
                ReturnSelectedToWaiting(changed.Select(j=>j.Id));
                if(selectedBatch!=null){selectedBatch.ExceptWith(changed.Select(j=>j.Id));if(selectedBatch.Count==0)selectedBatch=null;}
                if(changed.Count>0)Save();return changed.Count;
            }
        }
        public void Tick()
        {
            lock (gate)
            {
                if(selectedBatch!=null && !jobs.Any(j=>selectedBatch.Contains(j.Id) && (j.State==UiText.Source("queue.selectedPending") || j.Active))) selectedBatch=null;
                if (Paused && selectedBatch==null) return;
                while (jobs.Count(j => j.Active) < Math.Max(1, Math.Min(4, Concurrency)))
                {
                    QueueJob next = jobs.FirstOrDefault(j => ((!Paused && selectedBatch==null && j.State==UiText.Source("text.038")) || (selectedBatch!=null && selectedBatch.Contains(j.Id) && j.State==UiText.Source("queue.selectedPending"))) && !jobs.Any(a => a.Active && (String.Equals(a.Project, j.Project, StringComparison.OrdinalIgnoreCase) || (j.Output.Length>0 && String.Equals(a.Output.TrimEnd('\\','/'), j.Output.TrimEnd('\\','/'), StringComparison.OrdinalIgnoreCase)))));
                    if (next == null) break;
                    bool selected=next.State==UiText.Source("queue.selectedPending");next.State = UiText.Source("text.201"); next.Cancellation = new CancellationTokenSource(); Save();
                    if (StorageError.Length > 0) { next.State = UiText.Source(selected?"queue.selectedPending":"text.038"); break; }
                    Task.Run(() => Run(next));
                }
            }
        }
        public void Cancel(IEnumerable<string> ids)
        {
            lock (gate) foreach (QueueJob j in jobs.Where(j => ids.Contains(j.Id)))
            {
                if (j.Active) { j.State = UiText.Source("text.203"); j.Cancellation.Cancel(); }
                else if (j.State == UiText.Source("text.038") || j.State==UiText.Source("queue.selectedPending")) { j.State = UiText.Source("text.130"); j.Progress = UiText.Get("text.212"); }
            }
            lock (gate) Save();
        }
        public void TerminateAll()
        {
            lock (gate)
            {
                Pause();
                Cancel(jobs.Where(j => j.Active || j.State == UiText.Source("text.038") || j.State==UiText.Source("queue.selectedPending")).Select(j => j.Id).ToArray());
            }
        }
        public void DeleteItems(IEnumerable<string> ids,bool history,bool all)
        {
            lock(gate) {
                var wanted=new HashSet<string>(ids);
                if(all && !history) Pause();
                foreach(var j in jobs.Where(j=>j.History==history && (all||wanted.Contains(j.Id))).ToList()) {
                    if(j.Active) { j.DeleteWhenStopped=true; j.State=UiText.Source("text.203"); if(j.Cancellation!=null) j.Cancellation.Cancel(); }
                    else jobs.Remove(j);
                }
                Save();
            }
        }
        public void Reorder(IEnumerable<string> ids,string target,string position)
        {
            lock(gate) {
                var wanted=new HashSet<string>(ids);
                var movable=jobs.Where(j=>j.State==UiText.Source("text.038")).ToList();
                var moving=movable.Where(j=>wanted.Contains(j.Id)).ToList();
                if(moving.Count==0 || moving.Any(j=>j.Id==target)) return;
                var rest=movable.Except(moving).ToList();
                int at=position=="top"?0:position=="bottom"?rest.Count:rest.FindIndex(j=>j.Id==target);
                if(at<0) return;
                if(position=="after") at++;
                rest.InsertRange(at,moving); int i=0;
                for(int n=0;n<jobs.Count;n++) if(jobs[n].State==UiText.Source("text.038")) jobs[n]=rest[i++];
                Save();
            }
        }
        private void Change(QueueJob j, string state, string progress, string error)
        { lock (gate) { j.State = state; j.Progress = progress; j.Error = error; if(j.History) j.FinishedUtc=DateTime.UtcNow; if(j.DeleteWhenStopped && !j.Active) jobs.Remove(j); Save(); } }
        private void Run(QueueJob j)
        {
            CancellationToken token = j.Cancellation.Token;
            string temporaryProject = null;
            j.RenderSeconds=null;
            try
            {
                j.Engine=""; j.EngineVersion=""; j.ProjectAeVersion="";
                Directory.CreateDirectory(Path.Combine(Store.Root,"logs"));
                j.Log=Path.Combine(Store.Root,"logs",j.Id+".log");
                if (!File.Exists(j.Project)) throw new Exception(UiText.Get("text.213") + j.Project);
                string args = RenderCommand.Build(j);
                Directory.CreateDirectory(Path.Combine(Store.Root, "logs"));
                j.Log = Path.Combine(Store.Root, "logs", j.Id + ".log");
                using (var log = new StreamWriter(j.Log, true, new UTF8Encoding(false)))
                {
                    log.AutoFlush = true; object logGate = new object();
                    for (int attempt = 0; attempt <= j.Options.Retries; attempt++)
                    {
                        token.ThrowIfCancellationRequested();
                        Change(j,UiText.Source("text.201"),UiText.Get("text.214"),"");
                        var version=AepVersion.Read(j.Project); j.ProjectAeVersion=version.Text;
                        var match=EngineCatalog.Resolve(version);
                        j.Engine=match.Engine.Path; j.EngineVersion=match.Engine.Version;
                        log.WriteLine(UiText.Get("text.215"),DateTime.Now,j.ProjectAeVersion,j.EngineVersion,j.Engine);
                        token.ThrowIfCancellationRequested();
                        if (j.Output.Length>0 && temporaryProject==null)
                        {
                            Change(j,UiText.Source("text.201"),UiText.Get("text.216"),"");
                            temporaryProject=OutputRedirect.Prepare(j,token);
                            args="-project "+RenderCommand.Quote(temporaryProject);
                        }
                        j.Attempts++; Change(j, UiText.Source("text.201"), UiText.Get("text.217"), "");
                        bool failed = false; string diagnostic = "";
                        log.WriteLine(UiText.Get("text.218"), DateTime.Now, j.Attempts, j.Engine, args);
                        using (var renderLogs = new RenderLogCleanup(temporaryProject ?? j.Project, message => { lock(logGate) log.WriteLine("[{0}] {1}",DateTime.Now,message); }))
                        using (var p = new Process())
                        using (var tree = new ProcessScope())
                        {
                            p.StartInfo = new ProcessStartInfo(j.Engine, args) { WorkingDirectory = Path.GetDirectoryName(j.Engine), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.Default, StandardErrorEncoding = Encoding.Default };
                            DataReceivedEventHandler line = delegate(object sender, DataReceivedEventArgs e)
                            {
                                if (e.Data == null) return;
                                lock (logGate)
                                {
                                    log.WriteLine(e.Data);
                                    if (RenderCommand.Failure(e.Data)) { failed = true; diagnostic = e.Data; }
                                    if (e.Data.StartsWith("PROGRESS:", StringComparison.OrdinalIgnoreCase)) j.Progress = e.Data.Substring(9).Trim();
                                }
                            };
                            p.OutputDataReceived += line; p.ErrorDataReceived += line;
                            Stopwatch clock = Stopwatch.StartNew(); bool timeout = false;
                            p.Start();
                            try { tree.Attach(p); } catch { try { p.Kill(); p.WaitForExit(); } catch { } throw; }
                            p.BeginOutputReadLine(); p.BeginErrorReadLine();
                            while (!p.WaitForExit(100))
                            {
                                if (token.IsCancellationRequested || (j.Options.TimeoutMinutes > 0 && clock.Elapsed.TotalMinutes >= j.Options.TimeoutMinutes))
                                { timeout = !token.IsCancellationRequested; tree.Dispose(); p.WaitForExit(); break; }
                            }
                            p.WaitForExit(); // drain both asynchronous output readers before closing the log.
                            j.RenderSeconds=(j.RenderSeconds ?? 0)+clock.Elapsed.TotalSeconds;
                            token.ThrowIfCancellationRequested();
                            if (!timeout && p.ExitCode == 0 && !failed) { tree.Dispose(); renderLogs.Dispose(); Change(j, UiText.Source("text.029"), UiText.Get("text.219"), ""); return; }
                            diagnostic = timeout ? UiText.Get("text.220") : diagnostic.Length > 0 ? diagnostic : UiText.Get("text.221") + p.ExitCode;
                        }
                        log.WriteLine(UiText.Get("text.222"), DateTime.Now, diagnostic);
                        if (attempt == j.Options.Retries) { Change(j, UiText.Source("text.030"), UiText.Get("text.223"), diagnostic); return; }
                        Change(j, UiText.Source("text.202"), j.Options.RetryDelay + UiText.Get("text.224"), diagnostic);
                        if (token.WaitHandle.WaitOne(j.Options.RetryDelay * 1000)) token.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (OperationCanceledException) { Change(j, UiText.Source("text.130"), UiText.Get("text.225"), UiText.Get("text.226")); }
            catch (Exception e) { try { if(!String.IsNullOrEmpty(j.Log)) File.AppendAllText(j.Log,"["+DateTime.Now+"] "+e.Message+Environment.NewLine,Encoding.UTF8); } catch { } Change(j, UiText.Source("text.030"), UiText.Get("text.227"), e.Message); }
            finally { if(temporaryProject!=null) OutputRedirect.Cleanup(temporaryProject); }
        }
    }
}
