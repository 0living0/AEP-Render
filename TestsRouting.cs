using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
namespace AepBatchRenderer
{
    internal static class TestsRouting
    {
        private static int checks;
        private static void Check(bool yes,string message){if(!yes)throw new Exception(message);checks++;Console.WriteLine("PASS: "+message);}
        private static void Reject(Action a,string message){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected,message);}
        public static int Main(string[] args)
        {
            try
            {
                string root=Environment.GetEnvironmentVariable("AEP_BATCH_DATA");if(String.IsNullOrWhiteSpace(root))throw new Exception("Use isolated data root");
                Directory.CreateDirectory(root);
                string ae23=Path.GetFullPath(args[0]),ae26=Path.GetFullPath(args[1]);
                var e23=EngineCatalog.Inspect(ae23);var e26=EngineCatalog.Inspect(ae26);
                Check(e23.Major==23 && e26.Major==26,"engine major comes from executable metadata");
                var prefs=new Preferences { Engines=new List<AeEngine>{e26,e23},EnginesInitialized=true,Options=new RenderOptions{Retries=0} };Store.Save("settings.json",prefs);
                Func<string,int,AepFileItem> file=(name,major)=>{string p=Path.Combine(root,name+".aep");TestFixtures.WriteProject(p,major);return AepFileItem.FromPath(p);};
                var project23=file("created_2026_named_t99_but_AE23",23);var project26=file("created_2023_named_t01_but_AE26",26);
                Check(AepVersion.Read(project23.FullPath).Major==23,"project filename never determines AE version");
                Check(AepVersion.Read(project26.FullPath).Major==26,"packed 26 header is decoded");
                Check(AepVersion.Decode(new byte[]{0,0x5e,0,1,0x0b,0x38,0x06,0x3b}).Text=="23.0.0 (Build 59)","observed local AE 23 header decodes exactly");
                Check(AepVersion.Decode(new byte[]{0,0x5f,0,6,0x0f,0x03,0x1e,1}).Text=="24.6.3 (Build 1)","published AE 24 header decodes exact minor and patch");
                foreach(int m in new[]{11,12,13,14,15,16,17,18,22,23,24,25,26})Check(AepVersion.Decode(TestFixtures.Head(m)).Major==m,"supported packed major "+m);
                string xml=Path.Combine(root,"project.aepx");File.WriteAllText(xml,"<AfterEffectsProject><head bdata=\""+BitConverter.ToString(TestFixtures.Head(23)).Replace("-","")+"\"/></AfterEffectsProject>");
                Check(AepVersion.Read(xml).Major==23,"AEPX header is supported without AE");
                File.WriteAllText(xml,"<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///C:/secret'>]><a><head bdata=\"005e00010b38063b\"/></a>");Reject(()=>AepVersion.Read(xml),"XML external entity / DTD rejected");
                string invalid=Path.Combine(root,"bad.aep");File.WriteAllText(invalid,"head fake");Reject(()=>AepVersion.Read(invalid),"non-AEP rejected");
                File.WriteAllBytes(invalid,new byte[]{82,73,70,88,255,255,255,255,69,103,103,33});Reject(()=>AepVersion.Read(invalid),"truncated RIFX rejected");
                var unknown=TestFixtures.Head(23);unknown[1]=0x62;Reject(()=>AepVersion.Decode(unknown),"unknown or contradictory format rejected");
                Reject(()=>EngineCatalog.Select(AepVersion.Read(project23.FullPath),new[]{e26}),"only AE26 cannot silently render AE23");
                Reject(()=>EngineCatalog.Select(AepVersion.Decode(TestFixtures.Head(23,0,0,true)),new[]{e23}),"Beta project does not route to stable engine");
                var newer=new AeEngine{Path="newer",Major=23,Minor=6,Available=true};Check(EngineCatalog.Select(AepVersion.Read(project23.FullPath),new[]{e23,newer,e26}).Path=="newer","latest patch within matching major preferred");
                Check(EngineCatalog.Refresh(new[]{e23,e23}).Count==1,"duplicate engine path deduplicated");
                var stale=EngineCatalog.Refresh(new[]{new AeEngine{Path=Path.Combine(root,"missing","aerender.exe"),Major=23,Label="AE 2023"}});
                Check(stale.Count==1&&!stale[0].Available,"missing engine retained as unavailable card");
                string scanRoot=Path.Combine(root,"standard");string scanDir=Path.Combine(scanRoot,"Adobe After Effects WRONG YEAR","Support Files");Directory.CreateDirectory(scanDir);File.Copy(ae23,Path.Combine(scanDir,"aerender.exe"));
                Check(EngineCatalog.Scan(new[]{scanRoot,scanRoot}).Single().Major==23,"scan deduplicates roots and ignores folder year");
                var migration=new Preferences{Aerender=ae23};EngineCatalog.Initialize(migration);Check(migration.EnginesInitialized&&migration.Engines.Any(e=>e.Path==ae23),"legacy single path is migrated");
                migration.Engines.Clear();EngineCatalog.Initialize(migration);Check(migration.Engines.Count==0,"removed engine is not resurrected by automatic scan");
                var q=new RenderQueue();q.Concurrency=2;q.Add(new[]{project23,project26},prefs);q.Start();Drain(q);
                Check(q.Snapshot().All(j=>j.State=="完成"),"mixed-version queue completes via mock processes");
                Check(File.ReadAllText(project23.FullPath+".engine")==ae23&&File.ReadAllText(project26.FullPath+".engine")==ae26,"actual child executable matches each project's version");
                Check(q.Snapshot().All(j=>j.EngineVersion.Length>0&&j.ProjectAeVersion.Length>0),"selected versions recorded per job");
                q.Paused=true;var changed=file("changed-after-enqueue",23);q.Add(new[]{changed},prefs);TestFixtures.WriteProject(changed.FullPath,26);q.Start();Drain(q);Check(q.Snapshot().Last().Engine==ae26,"project version is reread at execution, not frozen at enqueue");
                prefs.Engines=new List<AeEngine>{e26};Store.Save("settings.json",prefs);
                var missing=file("no-matching-engine",23);q.Add(new[]{missing},prefs);Drain(q);
                Check(q.Snapshot().Last().State=="失败"&&q.Snapshot().Last().Attempts==0&&!File.Exists(missing.FullPath+".engine"),"missing matching engine fails before any child process");
                Check(File.ReadAllText(q.Snapshot().Last().Log).Contains("没有可用"),"missing engine diagnostic is logged");
                prefs.Engines.Add(e23);Store.Save("settings.json",prefs);q.Add(new[]{AepFileItem.FromPath(q.Snapshot().Last().Project)},prefs);Drain(q);Check(q.Snapshot().Last().State=="完成","adding engine then re-enqueueing uses latest registry and retains failed history");
                var bogus=file("invalid-before-render",23);File.WriteAllText(bogus.FullPath,"not AEP");q.Add(new[]{bogus},prefs);Drain(q);Check(q.Snapshot().Last().State=="失败"&&!File.Exists(bogus.FullPath+".engine"),"invalid project never launches engine");
                prefs.Options=new RenderOptions{Retries=1,RetryDelay=1};var retryChanged=file("failonce",23);q.Add(new[]{retryChanged},prefs);
                var watch=Stopwatch.StartNew();while(q.Snapshot().Last().State!="重试等待"){q.Tick();Thread.Sleep(20);if(watch.ElapsedMilliseconds>5000)throw new Exception("Retry fixture did not enter wait");}
                TestFixtures.WriteProject(retryChanged.FullPath,26);Drain(q);
                Check(q.Snapshot().Last().State=="完成"&&q.Snapshot().Last().Engine==ae26&&q.Snapshot().Last().Attempts==2,"automatic retry rechecks project version before launching again");
                Store.Save("queue.json",new[]{new QueueJob{Project=project23.FullPath,Engine=ae26,Name="legacy job",Options=new RenderOptions{Retries=0}}});
                var restored=new RenderQueue();restored.Start();Drain(restored);
                Check(restored.Snapshot().Single().Engine==ae23&&restored.Snapshot().Single().State=="完成","legacy queued engine snapshot cannot bypass version matching");
                if(args.Length>2)
                {
                    var local=AepVersion.Read(args[2]);Check(local.Major==23,"read-only check of user's real AEP identifies AE23");
                    var localEngine=EngineCatalog.Inspect(args[3]);Check(localEngine.Major==23,"read-only check of installed AE23 metadata");
                    Console.WriteLine("LOCAL: "+local.Text+" -> "+localEngine.Version);
                }
                Console.WriteLine("SUCCESS: "+checks+" version/routing checks");return 0;
            }
            catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
        private static void Drain(RenderQueue q){var timer=Stopwatch.StartNew();do{q.Tick();if(!q.Snapshot().Any(j=>j.Active||j.State=="等待"))return;if(timer.ElapsedMilliseconds>12000)throw new Exception("Queue timeout");Thread.Sleep(40);}while(true);}
    }
}
