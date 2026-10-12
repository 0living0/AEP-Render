using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
namespace AepBatchRenderer {
internal static class TestsQueue {
 [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
 static int n;static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS: "+s);n++;}
 public static void Main(){
  SetErrorMode(0x0002); // Keep automated test failures in the console instead of opening a Windows crash dialog.
  var history=new QueueJob{Id="h",State="完成",FinishedUtc=DateTime.UtcNow};
  Store.Save("queue.json",new List<QueueJob>{new QueueJob{Id="a"},history,new QueueJob{Id="b"},new QueueJob{Id="c"}});
  var q=new RenderQueue();
  q.Reorder(new[]{"c"},"","top");Check(String.Join(",",q.Snapshot().Select(j=>j.Id))=="c,h,a,b","top preserves history slots");
  q.Reorder(new[]{"c","a"},"","bottom");Check(String.Join(",",q.Snapshot().Select(j=>j.Id))=="b,h,c,a","multi bottom preserves relative order");
  q.Reorder(new[]{"a"},"c","before");Check(String.Join(",",q.Snapshot().Select(j=>j.Id))=="b,h,a,c","drag inserts before target");
  q.Reorder(new[]{"b"},"c","after");Check(String.Join(",",q.Snapshot().Select(j=>j.Id))=="a,h,c,b","drag inserts after target");
  Check(new RenderQueue().Snapshot().Last().Id=="b","reorder persists");
  var active=q.Snapshot().First(j=>j.Id=="a");active.State="运行";active.Cancellation=new CancellationTokenSource();
  q.Reorder(new[]{"a"},"","bottom");Check(q.Snapshot()[0].Id=="a","active cannot move");
  q.DeleteItems(new[]{"h"},false,false);Check(q.Snapshot().Any(j=>j.Id=="h"),"queue deletion cannot delete history");
  q.DeleteItems(new string[0],true,true);Check(!q.Snapshot().Any(j=>j.Id=="h")&&q.Snapshot().Count==3,"history clear preserves queue");
  q.DeleteItems(new[]{"a"},false,false);Check(active.DeleteWhenStopped&&active.Cancellation.IsCancellationRequested&&q.ActiveCount==1,"active deletion cancels before removing");
  typeof(RenderQueue).GetMethod("Change",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(q,new object[]{active,"取消","ended",""});
  Check(!q.Snapshot().Contains(active),"cancel completion removes pending-delete record");
  var completed=q.Snapshot()[0];typeof(RenderQueue).GetMethod("Change",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(q,new object[]{completed,"失败","failed","test"});
  Check(completed.FinishedUtc.HasValue&&completed.History,"terminal state records completion time");
  Check(new RenderQueue().Snapshot().First(j=>j.Id==completed.Id).FinishedUtc.HasValue,"completion time persists");
  q.Paused=false;q.DeleteItems(new string[0],false,true);Check(q.Paused&&q.Snapshot().Count==1&&q.Snapshot()[0].History,"clear queue pauses and preserves completed records");
  string project=Path.Combine(Store.Root,"requeue.aep");File.WriteAllText(project,"fixture");
  var kept=q.Snapshot()[0];kept.Project=project;kept.Name="requeue";
  string[] requeued=q.RequeueHistory(new[]{kept.Id},new Preferences());
  Check(requeued.Length==1&&q.Snapshot().Count==2&&q.Snapshot().Contains(kept),"requeue keeps original history and creates waiting copy");
  Check(kept.Requeued&&q.Snapshot().First(j=>j.Id==requeued[0]).State=="等待","requeue marks history and returns new task id");
  string[] duplicate=q.RequeueHistory(new[]{kept.Id},new Preferences());
  Check(duplicate.Length==0&&q.Snapshot().Count==2,"duplicate waiting project is not requeued again");
  var restored=new RenderQueue();Check(restored.Snapshot().First(j=>j.Id==kept.Id).Requeued,"requeued history marker persists");
  var waiting=q.Snapshot().First(j=>j.Id==requeued[0]);q.StartSelected(new[]{waiting.Id});
  Check(q.SelectedMode&&waiting.State=="待渲染","render selected marks the task ready to render");
  Check(q.RequeueHistory(new[]{kept.Id},new Preferences()).Length==0,"ready-to-render project blocks duplicate requeue");
  Check(q.ReleaseSelected(new[]{waiting.Id})==1&&waiting.State=="等待"&&!q.SelectedMode,"holding a ready task returns it to ordinary waiting");
  q.StartSelected(new[]{waiting.Id});
  Check(new RenderQueue().Snapshot().First(j=>j.Id==waiting.Id).State=="等待","unfinished selected batch returns to waiting after restart");
  Console.WriteLine("SUCCESS: "+n+" queue-management checks");
 }
}}
