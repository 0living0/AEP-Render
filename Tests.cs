using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Reflection;
[assembly:AssemblyCompany("Adobe")]
[assembly:AssemblyProduct("Adobe After Effects TEST HARNESS - not a render engine")]
#if AE26
[assembly:AssemblyFileVersion("26.0.0.59")]
#else
[assembly:AssemblyFileVersion("23.0.0.59")]
#endif
namespace AepBatchRenderer
{
    internal static class Tests
    {
        private static int checks;
        private static void Assert(bool ok,string message) { if(!ok) throw new Exception("FAIL: "+message); checks++; Console.WriteLine("PASS: "+message); }
        public static int Main(string[] args)
        {
            if(args.Length>0 && args[0]=="--child") { Thread.Sleep(60000); return 0; }
            if(args.Length>0 && args[0]=="-project") return Mock(args);
            try
            {
                string root=Environment.GetEnvironmentVariable("AEP_BATCH_DATA"); if(String.IsNullOrWhiteSpace(root)) throw new Exception("Set isolated AEP_BATCH_DATA for tests.");
                Directory.CreateDirectory(root); string self=Process.GetCurrentProcess().MainModule.FileName;
                Func<string,AepFileItem> fixture=name=> { string path=Path.Combine(root,name+".aep"); TestFixtures.WriteProject(path); return AepFileItem.FromPath(path); };
                var prefs=new Preferences { Aerender=self }; prefs.Options.Retries=1; prefs.Options.RetryDelay=1;
                prefs.Engines.Add(EngineCatalog.Inspect(self)); prefs.EnginesInitialized=true; Store.Save("settings.json",prefs);
                Assert(fixture("TYMSZS_S1E10_s01c001_t02").DisplayName=="S1E10_s01c001_t02","display removes only the first segment");
                Assert(fixture("TYMSZS_S1E11_s04c091a_t02").DisplayName=="S1E11_s04c091a_t02","letter suffix preserved without shot parsing");
                Assert(fixture("s04c127_t02").DisplayName=="t02","first separator used regardless of naming format");
                Assert(fixture("plain name").DisplayName=="plain name","name without separator preserved");
                Assert(fixture("TYMSZS_S1E10_s01c001_t10").Version==10,"numeric version sorting");
                var q=new RenderQueue(); Assert(q.Paused,"startup never auto-renders");
                var success=fixture("success 中文 空格"); Assert(q.Add(new[]{success},prefs)==1,"enqueue"); Assert(q.Add(new[]{success},prefs)==0,"duplicate active project rejected");
                q.Start(); Assert(q.StorageError.Length==0,"persistence: "+q.StorageError); Drain(q); Assert(q.Snapshot().Single().State=="完成","successful child process"); Assert(!Directory.Exists(success.FullPath+" Logs"),"successful render removes its generated AE log folder");
                var legacy=new JavaScriptSerializer().Deserialize<RenderOptions>("{\"Composition\":\"old\",\"RenderTemplate\":\"old\",\"OutputTemplate\":\"old\",\"Start\":\"bad\",\"OutputFolder\":\"\"}");
                legacy.Validate(); Assert(RenderCommand.Build(new QueueJob{Project=success.FullPath,Options=legacy})=="-project "+RenderCommand.Quote(success.FullPath),"legacy composition and template overrides are discarded");
                prefs.Options=new RenderOptions { Retries=1,RetryDelay=1 };
                q.Add(new[]{fixture("zeroerror")},prefs); Drain(q); var error=q.Snapshot().Last(); Assert(error.State=="失败" && error.Attempts==2,"exit zero with AE output error fails and retries exactly once"); Assert(!Directory.Exists(error.Project+" Logs"),"failed attempts remove generated AE log folders");
                q.Add(new[]{fixture("failonce")},prefs); Drain(q); var retried=q.Snapshot().Last(); Assert(retried.State=="完成" && retried.Attempts==2,"transient failure retries to success");
                q.Paused=true; q.Add(new[]{fixture("paused")},prefs); q.Tick(); Assert(q.Snapshot().Last().Attempts==0,"pause blocks new dispatch"); q.Start(); Drain(q);
                prefs.Options=new RenderOptions { Retries=0,RetryDelay=1 }; q.Concurrency=2;
                q.Add(new[]{fixture("slow-a"),fixture("slow-b"),fixture("slow-c")},prefs); q.Tick(); Assert(q.ActiveCount==2,"two slots reserved before workers launch"); Drain(q); Assert(q.Snapshot().Skip(q.Snapshot().Count-3).All(j=>j.State=="完成"),"concurrent queue drains");
                q.Concurrency=1; var hanging=fixture("spawn"); q.Add(new[]{hanging},prefs); q.Tick();
                string pidFile=hanging.FullPath+".pid"; WaitUntil(()=>File.Exists(pidFile),5000); int child=Int32.Parse(File.ReadAllText(pidFile));
                q.Cancel(new[]{q.Snapshot().Last().Id}); Drain(q); Assert(q.Snapshot().Last().State=="取消","cancel marks state after process shutdown"); Assert(!Directory.Exists(hanging.FullPath+" Logs"),"cancel cleans logs after child process shutdown");
                Thread.Sleep(200); bool childAlive; try { childAlive=!Process.GetProcessById(child).HasExited; } catch(ArgumentException) { childAlive=false; } Assert(!childAlive,"cancel terminates descendant process");
                prefs.Options=new RenderOptions { Retries=0 }; q.Concurrency=2;
                AepFileItem terminateA=fixture("slow-terminate-a"),terminateB=fixture("slow-terminate-b"),terminateC=fixture("slow-terminate-c");
                q.Add(new[]{terminateA,terminateB,terminateC},prefs); q.Tick(); Assert(q.ActiveCount==2,"terminate-all fixture occupies both slots");
                q.TerminateAll(); Drain(q); var terminated=q.Snapshot().Skip(q.Snapshot().Count-3).ToList();
                Assert(q.Paused && terminated.All(j=>j.State=="取消"),"terminate-all stops active and waiting tasks and pauses dispatch");
                Assert(terminated.Last().Attempts==0,"terminate-all never launches pending work");
                q.Start();
                string output=Path.Combine(root,"out"); Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output,"protected.mov"),"KEEP ME");
                bool protectedOutput=false; try { OutputRedirect.ValidateOutputs(new[]{Path.Combine(output,"protected.mov")}); } catch { protectedOutput=true; }
                Assert(protectedOutput && File.ReadAllText(Path.Combine(output,"protected.mov"))=="KEEP ME","existing output preserved by default");
                File.WriteAllText(Path.Combine(output,"frame_0001.png"),"KEEP");
                bool sequenceProtected=false; try { OutputRedirect.ValidateOutputs(new[]{Path.Combine(output,"frame_[####].png")}); } catch { sequenceProtected=true; }
                Assert(sequenceProtected,"existing sequence frames are protected");
                bool duplicate=false; try { OutputRedirect.ValidateOutputs(new[]{Path.Combine(output,"new.mov"),Path.Combine(output,"new.mov")}); } catch { duplicate=true; }
                Assert(duplicate,"duplicate output modules rejected");
                new RenderOptions{OutputFolder=output}.Validate(); Assert(RenderCommand.OutputPath("test.aep",new RenderOptions{OutputFolder=output})==Path.GetFullPath(output),"output folder requires no composition or extension");
                q.Concurrency=1; q.Start(); q.Add(new[]{fixture("slow-already-running")},prefs); q.Tick();
                var previouslyRunning=q.Snapshot().Last();
                q.Add(new[]{fixture("slow-selected-one"),fixture("slow-selected-two"),fixture("unselected")},prefs);
                var batch=q.Snapshot().Where(j=>j.Name.StartsWith("slow-selected")).ToList();
                q.StartSelected(batch.Select(j=>j.Id)); Assert(previouslyRunning.Active&&!previouslyRunning.Cancellation.IsCancellationRequested,"switching to selected mode keeps already running task"); q.Tick();
                q.Add(new[]{fixture("late-arrival")},prefs);
                WaitUntil(()=>{q.Tick();return !q.SelectedMode;},10000);
                Assert(previouslyRunning.State=="完成","previously running task completes normally");
                Assert(q.Paused&&batch.All(j=>j.State=="完成"),"selected snapshot completes then stays paused");
                Assert(q.Snapshot().Where(j=>j.Name=="unselected"||j.Name=="late-arrival").All(j=>j.State=="等待"&&j.Attempts==0),"selected mode excludes unselected and later arrivals");
                Assert(batch.All(j=>j.RenderSeconds.HasValue&&j.RenderSeconds.Value>=0.5),"successful render duration records process time");
                var left=q.Snapshot().Where(j=>j.State=="等待").ToList();q.StartSelected(left.Select(j=>j.Id));q.Pause();q.Tick();
                Assert(left.All(j=>j.Attempts==0)&&!q.SelectedMode,"pause cancels pending selected dispatch");
                q.Start();Drain(q);Assert(left.All(j=>j.State=="完成")&&!q.Paused,"autonomous mode resumes all remaining jobs");
                q.Add(new[]{fixture("auto-later")},prefs);Drain(q);Assert(q.Snapshot().Last().State=="完成","autonomous mode still dispatches later arrivals");
                var recovered=new RenderQueue();
                Assert(recovered.Snapshot().First(j=>j.Id==batch[0].Id).RenderSeconds==batch[0].RenderSeconds,"render duration persists after restart"); Assert(recovered.Paused && recovered.Snapshot().Count==q.Snapshot().Count,"queue persists and reopens paused");
                Store.Save("queue.json",new[]{new QueueJob { State="运行" }}); var interrupted=new RenderQueue(); Assert(interrupted.Snapshot().Single().State=="中断","crashed running job requires explicit retry");
                Assert(RenderCommand.Failure("aerender ERROR: No queued items"),"empty queue diagnosed");
                bool invalid=false; try { new RenderOptions { OutputFolder="relative-folder" }.Validate(); } catch { invalid=true; } Assert(invalid,"relative output folder rejected");
                string cleanupProject=Path.Combine(root,"cleanup 中文.aep"), cleanupFolder=cleanupProject+" Logs";
                string header="Adobe After Effects v26.0 Windows 64 render log for project “cleanup 中文.aep”";
                Directory.CreateDirectory(cleanupFolder); string oldLog=Path.Combine(cleanupFolder,"AE old.txt"); File.WriteAllText(oldLog,header);
                using(var cleanup=new RenderLogCleanup(cleanupProject,null)) {
                    File.WriteAllText(Path.Combine(cleanupFolder,"AE new.txt"),header, System.Text.Encoding.Unicode);
                    File.WriteAllText(Path.Combine(cleanupFolder,"AE unrelated.txt"),"user notes");
                    File.WriteAllText(Path.Combine(cleanupFolder,"AE other-project.txt"),"Adobe After Effects v26.0 Windows 64 render log for project “other.aep”");
                    File.WriteAllText(Path.Combine(cleanupFolder,"video.mp4"),"KEEP");
                    Directory.CreateDirectory(Path.Combine(cleanupFolder,"nested"));
                }
                Assert(!File.Exists(Path.Combine(cleanupFolder,"AE new.txt")),"new matching UTF16 AE log removed");
                Assert(File.Exists(oldLog)&&File.Exists(Path.Combine(cleanupFolder,"AE unrelated.txt"))&&File.Exists(Path.Combine(cleanupFolder,"AE other-project.txt"))&&File.Exists(Path.Combine(cleanupFolder,"video.mp4"))&&Directory.Exists(Path.Combine(cleanupFolder,"nested")),"old logs, unrelated files, other project logs, video and subfolders preserved");
                string emptyProject=Path.Combine(root,"empty.aep"); Directory.CreateDirectory(emptyProject+" Logs"); using(var cleanup=new RenderLogCleanup(emptyProject,null)){} Assert(Directory.Exists(emptyProject+" Logs"),"pre-existing empty directory preserved");
                Console.WriteLine("SUCCESS: "+checks+" checks"); return 0;
            }
            catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        }
        private static int Mock(string[] args)
        {
            string project=args[1]; File.WriteAllText(project+".args",new JavaScriptSerializer().Serialize(args)); string name=Path.GetFileNameWithoutExtension(project);
            File.WriteAllText(project+".engine",Process.GetCurrentProcess().MainModule.FileName);
            Directory.CreateDirectory(project+" Logs"); File.WriteAllText(Path.Combine(project+" Logs","AE mock.txt"),"Adobe After Effects v26.0 Windows 64 render log for project “"+Path.GetFileName(project)+"”");
            Console.WriteLine("PROGRESS: Starting Frame: 0");
            if(name=="zeroerror") { Console.WriteLine("aerender ERROR: An output module failed"); return 0; }
            if(name=="failonce" && !File.Exists(project+".once")) { File.WriteAllText(project+".once","1"); return 8; }
            if(name.StartsWith("slow")) Thread.Sleep(700);
            if(name=="spawn") { var p=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--child") { UseShellExecute=false,CreateNoWindow=true }); File.WriteAllText(project+".pid",p.Id.ToString()); Thread.Sleep(60000); }
            Console.WriteLine("PROGRESS: 0:00:00:01 (1): 0 Seconds"); Console.WriteLine("PROGRESS: Total Time Elapsed: 1 Seconds"); return 0;
        }
        private static void Drain(RenderQueue q) { WaitUntil(delegate { q.Tick(); return !q.Snapshot().Any(j=>j.Active || j.State=="等待"); },15000); }
        private static void WaitUntil(Func<bool> test,int timeout) { var t=Stopwatch.StartNew(); while(!test()) { if(t.ElapsedMilliseconds>timeout) throw new Exception("Test timed out"); Thread.Sleep(40); } }
    }
}

