using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace AepBatchRenderer
{
    // Local-user inbox. Atomic request rename prevents reading half-written JSON.
    // Claim before dispatch means a crashed command is never silently replayed.
    internal sealed class LocalBridge
    {
        private readonly string inbox,results,processing;
        private readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=65536 };
        public LocalBridge(string root)
        {
            inbox=Path.Combine(root,"bridge","requests"); results=Path.Combine(root,"bridge","results"); processing=Path.Combine(root,"bridge","processing");
            Directory.CreateDirectory(inbox); Directory.CreateDirectory(results); Directory.CreateDirectory(processing);
        }
        public void Poll(Func<Dictionary<string,object>,object> execute)
        {
            foreach(var file in Directory.EnumerateFiles(inbox,"*.json").Take(8))
            {
                string id=Path.GetFileNameWithoutExtension(file); Guid guid;
                if(!Guid.TryParseExact(id,"N",out guid)) continue;
                string claimed=Path.Combine(processing,id+".json"),response=Path.Combine(results,id+".json");
                if(File.Exists(claimed) || File.Exists(response)) continue;
                try { File.Move(file,claimed); } catch(IOException) { continue; }
                object reply;
                try {
                    if(new FileInfo(claimed).Length>65536) throw new Exception(UiText.Get("bridge.size"));
                    var request=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(claimed,Encoding.UTF8));
                    if(request==null || !request.ContainsKey("expiresUtc")) throw new Exception(UiText.Get("bridge.expiryMissing"));
                    DateTime expires;
                    if(!DateTime.TryParse(Convert.ToString(request["expiresUtc"]),System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out expires) || expires.ToUniversalTime()<DateTime.UtcNow || expires.ToUniversalTime()>DateTime.UtcNow.AddMinutes(2)) throw new Exception(UiText.Get("bridge.expired"));
                    reply=new { ok=true,id=id,data=execute(request) };
                } catch(Exception e) { reply=new { ok=false,id=id,error=e.Message }; }
                string temp=response+".tmp";
                File.WriteAllText(temp,json.Serialize(reply),new UTF8Encoding(false)); File.Move(temp,response);
                File.Delete(claimed);
            }
        }
    }
}
