using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
namespace AepBatchRenderer {
 internal static class UiText {
  private static readonly Dictionary<string,Dictionary<string,string>> Strings=Load();
  public static string Language="zh";
  public static string Normalize(string language) { return language=="en"||language=="ja"?language:"zh"; }
  private static Dictionary<string,Dictionary<string,string>> Load() {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("strings.json")) {
    if(stream==null) throw new InvalidOperationException("Missing embedded localization resource: strings.json");
    using(var reader=new StreamReader(stream,Encoding.UTF8)) return new JavaScriptSerializer{MaxJsonLength=8000000}.Deserialize<Dictionary<string,Dictionary<string,string>>>(reader.ReadToEnd());
   }
  }
  public static string Source(string key) { return Strings[key]["zh"]; }
  public static string Get(string key) { return Strings[key][Normalize(Language)]; }
  public static string Format(string key,params object[] values) { return String.Format(Get(key),values); }
 }
}
