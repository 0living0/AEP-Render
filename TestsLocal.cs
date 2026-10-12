using System;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
namespace AepBatchRenderer {
internal static class TestsLocal {
 static int count;
 static void Check(bool ok,string text) { if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS: "+text); }
 static void Event(FolderMonitor monitor,Action change,string label) { change();Thread.Sleep(1100);Check(monitor.Due(),label);Check(!monitor.Due(),"event burst is consumed once"); }
 static string Request(string root,string content) { string id=Guid.NewGuid().ToString("N");File.WriteAllText(Path.Combine(root,"bridge","requests",id+".tmp"),content);File.Move(Path.Combine(root,"bridge","requests",id+".tmp"),Path.Combine(root,"bridge","requests",id+".json"));return id; }
 static string Reply(string root,string id) { return File.ReadAllText(Path.Combine(root,"bridge","results",id+".json")); }
 public static void Main() {
  string root=Path.Combine(Path.GetTempPath(),"AepLocalTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  string a=Path.Combine(root,"a"),b=Path.Combine(root,"b");Directory.CreateDirectory(a);Directory.CreateDirectory(b);
  using(var monitor=new FolderMonitor()) {
   monitor.Watch(a);Check(monitor.Due(),"initial fallback scan");Check(!monitor.Due(),"no scan while unchanged");
   string p=Path.Combine(a,"test.aep"),q=Path.Combine(a,"rename.aep");
   Event(monitor,()=>File.WriteAllText(p,"one"),"create detected");
   Event(monitor,()=>File.AppendAllText(p,"two"),"write detected");
   Event(monitor,()=>File.Move(p,q),"rename detected");
   Event(monitor,()=>File.Delete(q),"delete detected");
   monitor.Watch(b);monitor.Due();File.WriteAllText(p,"old folder");Thread.Sleep(1100);Check(!monitor.Due(),"old directory detached");
   Event(monitor,()=>File.WriteAllText(Path.Combine(b,"new.aep"),"new"),"new directory monitored");
   string nested=Path.Combine(b,"nested","deep");Directory.CreateDirectory(nested);monitor.Watch(b,true);monitor.Due();
   Event(monitor,()=>File.WriteAllText(Path.Combine(nested,"nested.aep"),"nested"),"recursive monitor detects nested project changes");
   monitor.Watch(b,false);monitor.Due();File.WriteAllText(Path.Combine(nested,"ignored.aep"),"top only");Thread.Sleep(1100);Check(!monitor.Due(),"top-level monitor ignores nested changes");
  }
  var bridge=new LocalBridge(root);var json=new JavaScriptSerializer();int calls=0;
  Func<System.Collections.Generic.Dictionary<string,object>,object> run=d=>{calls++;if(Convert.ToString(d["action"])=="fail")throw new Exception("expected failure");return new { action=d["action"] };};
  string valid=json.Serialize(new { action="status",expiresUtc=DateTime.UtcNow.AddSeconds(30).ToString("o") });
  string id=Request(root,valid);bridge.Poll(run);Check(calls==1 && Reply(root,id).Contains("\"ok\":true"),"valid command and atomic response");bridge.Poll(run);Check(calls==1,"no command replay");
  id=Request(root,"{bad");bridge.Poll(run);Check(Reply(root,id).Contains("\"ok\":false") && calls==1,"malformed JSON rejected");
  id=Request(root,json.Serialize(new { action="status",expiresUtc=DateTime.UtcNow.AddSeconds(-1).ToString("o") }));bridge.Poll(run);Check(Reply(root,id).Contains("\"ok\":false") && calls==1,"expired request rejected");
  id=Request(root,new string('x',65537));bridge.Poll(run);Check(Reply(root,id).Contains("\"ok\":false") && calls==1,"oversize request rejected");
  File.WriteAllText(Path.Combine(root,"bridge","requests",Guid.NewGuid().ToString("N")+".tmp"),valid);bridge.Poll(run);Check(calls==1,"partial request ignored");
  id=Request(root,json.Serialize(new { action="fail",expiresUtc=DateTime.UtcNow.AddSeconds(30).ToString("o") }));bridge.Poll(run);Check(Reply(root,id).Contains("expected failure"),"handler error returned");
  Console.WriteLine("SUCCESS: "+count+" local monitor/transport checks. Isolated fixtures: "+root);
 }
}}
