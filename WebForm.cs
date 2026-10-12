using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AepBatchRenderer
{
    internal sealed class WebForm : Form
    {
        private readonly WebView2 web=new WebView2();
        private readonly ExplorerModel explorer=new ExplorerModel();
        private readonly RenderQueue queue=new RenderQueue();
        private readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=8000000 };
        private readonly Timer timer=new Timer();
        private readonly FolderMonitor monitor=new FolderMonitor();
        private LocalBridge bridge;
        private string monitorError="";
        private bool closing, ready;
        private bool closePromptOpen;
        private string lastQueue="";
        private Preferences preview;
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
        private bool SystemDark() { try { return Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0; } catch { return false; } }
        private void ThemeChanged(object sender,UserPreferenceChangedEventArgs e) { if(IsHandleCreated && !IsDisposed) BeginInvoke(new Action(()=>ApplyAppearance(preview ?? explorer.Settings))); }
        private void ApplyAppearance(Preferences p)
        {
            UiText.Language=UiText.Normalize(p.Language); Text=UiText.Get("text.080");
            bool dark=p.FollowSystem ? SystemDark() : p.Theme=="dark";
            int value=dark ? 1 : 0; try { DwmSetWindowAttribute(Handle,20,ref value,4); } catch { }
            BackColor=dark ? Color.FromArgb(29,29,41) : Color.FromArgb(247,248,252);
            web.DefaultBackgroundColor=BackColor;
            if(web.CoreWebView2==null) return;
            double scale=p.TextSize=="small" ? 0.8 : p.TextSize=="large" ? 1.1 : 0.95;
            double dpi; using(var graphics=CreateGraphics()) dpi=graphics.DpiX/96.0;
            web.ZoomFactor=scale*Math.Max(0.60,Math.Min(1.65,Math.Min(ClientSize.Width/(1300.0*dpi),ClientSize.Height/(795.0*dpi))));
            web.CoreWebView2.Profile.PreferredColorScheme=dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
            Send(new { type="appearance",dark=dark,language=UiText.Normalize(p.Language) });
        }
        private readonly string[] arguments;
        public WebForm(string[] args)
        {
            arguments=args; UiText.Language=UiText.Normalize(explorer.Settings.Language); Text=UiText.Get("text.080"); Font=new Font("Microsoft YaHei UI",9F); AutoScaleMode=AutoScaleMode.Dpi;
            MinimumSize=new Size(1000,660); Size=new Size(1450,940); StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(247,248,251);
            web.Dock=DockStyle.Fill; web.DefaultBackgroundColor=BackColor; Controls.Add(web);
            queue.Concurrency=explorer.Settings.Concurrency;
            string folder=args.FirstOrDefault(a=>Directory.Exists(a)) ?? explorer.Settings.Folder;
            if(!Directory.Exists(folder)) folder=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            explorer.Navigate(folder);
            EngineCatalog.Initialize(explorer.Settings); explorer.Save();
            if(args.Contains("--ui-test")) { ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; Location=new Point(-16000,0); }
            Shown += async delegate { RestoreWindowSize(); await Initialize(); };
            ResizeEnd += delegate { SaveWindowSize(); };
            timer.Interval=400; timer.Tick += delegate { queue.Tick(); PublishQueue(); if(!closing) PollLocalChanges(); if(closing && queue.ActiveCount==0) Close(); };
            FormClosing += OnClose;
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Resize += delegate { if(WindowState!=FormWindowState.Minimized) ApplyAppearance(preview ?? explorer.Settings); };
            SystemEvents.UserPreferenceChanged += ThemeChanged;
        }
        private void RestoreWindowSize()
        {
            var p=explorer.Settings; if(p.WindowWidth<=0 || p.WindowHeight<=0) return;
            double dpi; using(var g=CreateGraphics()) dpi=g.DpiX;
            double ratio=dpi/(p.WindowDpi>=48 && p.WindowDpi<=768 ? p.WindowDpi : 96);
            var area=Screen.FromHandle(Handle).WorkingArea;
            Size=new Size(Math.Max(MinimumSize.Width,Math.Min(area.Width,(int)(p.WindowWidth*ratio))),Math.Max(MinimumSize.Height,Math.Min(area.Height,(int)(p.WindowHeight*ratio))));
            if(!arguments.Contains("--ui-test")) Location=new Point(area.Left+Math.Max(0,(area.Width-Width)/2),area.Top+Math.Max(0,(area.Height-Height)/2));
            if(p.WindowMaximized) WindowState=FormWindowState.Maximized;
        }
        private void SaveWindowSize()
        {
            if(!ready || WindowState==FormWindowState.Minimized) return;
            var bounds=WindowState==FormWindowState.Normal ? Bounds : RestoreBounds;
            if(bounds.Width<=0 || bounds.Height<=0) return;
            explorer.Settings.WindowWidth=bounds.Width; explorer.Settings.WindowHeight=bounds.Height;
            using(var g=CreateGraphics()) explorer.Settings.WindowDpi=g.DpiX;
            explorer.Settings.WindowMaximized=WindowState==FormWindowState.Maximized; explorer.Save();
        }
        private async Task Initialize()
        {
            try
            {
                Trace("initializing");
                var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Store.Root,"webview"));
                Trace("environment created");
                await web.EnsureCoreWebView2Async(env);
                ApplyAppearance(explorer.Settings);
                Trace("control ready");
                var core=web.CoreWebView2; core.Settings.AreDefaultContextMenusEnabled=false; core.Settings.AreDevToolsEnabled=false; core.Settings.AreHostObjectsAllowed=false; core.Settings.IsStatusBarEnabled=false;
                core.NavigationStarting += delegate(object s,CoreWebView2NavigationStartingEventArgs e) {
                    bool initialHtml=!ready && (e.Uri=="about:blank" || e.Uri.StartsWith("data:text/html",StringComparison.OrdinalIgnoreCase));
                    if(!initialHtml) e.Cancel=true;
                    Trace("navigate URI: "+e.Uri.Substring(0,Math.Min(70,e.Uri.Length))+" allowed="+initialHtml);
                };
                core.NewWindowRequested += delegate(object s,CoreWebView2NewWindowRequestedEventArgs e) { e.Handled=true; };
                core.PermissionRequested += delegate(object s,CoreWebView2PermissionRequestedEventArgs e) { e.State=CoreWebView2PermissionState.Deny; };
                core.WebMessageReceived += OnMessage;
                core.NavigationCompleted += async delegate(object s,CoreWebView2NavigationCompletedEventArgs e) { Trace("navigation: "+e.IsSuccess+" "+e.WebErrorStatus); if(arguments.Contains("--ui-test")) Trace(await core.ExecuteScriptAsync("JSON.stringify({ready:document.readyState,files:typeof window.runUiTests,source:location.href})")); };
                core.NavigateToString(Bootstrap.ResourceText("ui.html").Replace("/*__LOCALIZATION_RUNTIME__*/",Bootstrap.ResourceText("localization.js").Replace("/*__STRINGS__*/","const UI_STRINGS="+Bootstrap.ResourceText("strings.json")+";")));
            }
            catch(Exception e) { if(arguments.Contains("--ui-test")) Trace(e.ToString()); else AppNotice.Show(UiText.Get("text.177"),UiText.Get("text.178")+e.Message+UiText.Get("text.179")); Close(); }
        }
        private void Trace(string message) { if(arguments.Contains("--ui-test")) File.AppendAllText(Path.Combine(Store.Root,"trace.txt"),DateTime.Now.ToString("HH:mm:ss")+" "+message+Environment.NewLine); }
        private string TextValue(Dictionary<string,object> data,string key) { return data.ContainsKey(key) && data[key]!=null ? Convert.ToString(data[key]) : ""; }
        private string FileStamp() { return String.Join("\n",explorer.Files.OrderBy(f=>f.FullPath,StringComparer.OrdinalIgnoreCase).Select(f=>f.FullPath+"|"+f.Length+"|"+f.ModifiedAt.Ticks)); }
        private void PollLocalChanges()
        {
            try {
                monitor.Watch(explorer.Folder,explorer.Settings.RecursiveFolders);
                if(monitor.Due()) {
                    string before=FileStamp();
                    if(Directory.Exists(explorer.Folder)) explorer.Refresh(); else explorer.Files.Clear();
                    if(before!=FileStamp()) Send(new { type="filesChanged" });
                }
                if(bridge==null) bridge=new LocalBridge(Store.Root);
                bridge.Poll(ExternalCommand); monitorError="";
            } catch(Exception e) {
                if(monitorError!=e.Message) { monitorError=e.Message; Send(new { type="error",message=UiText.Get("text.180")+e.Message }); }
            }
        }
        private object ExternalCommand(Dictionary<string,object> d)
        {
            string action=TextValue(d,"action");
            switch(action) {
                case "status": return new { folder=explorer.Folder,paused=queue.Paused,active=queue.ActiveCount,projects=explorer.Files.Select(f=>f.FullPath).ToArray() };
                case "refresh": explorer.Refresh(); Send(new { type="filesChanged" }); return new { folder=explorer.Folder,count=explorer.Files.Count };
                case "open": explorer.Navigate(TextValue(d,"path")); Publish(); return new { folder=explorer.Folder };
                case "select":
                    explorer.Refresh(); var paths=Paths(d);
                    if(paths.Length==0 || paths.Any(p=>!explorer.Files.Any(f=>String.Equals(f.FullPath,p,StringComparison.OrdinalIgnoreCase)))) throw new Exception(UiText.Get("text.181"));
                    explorer.SetMany(paths,!d.ContainsKey("selected") || Convert.ToBoolean(d["selected"])); Send(new { type="filesChanged" }); return new { count=paths.Length };
                case "enqueue":
                    var projects=Paths(d);
                    if(projects.Length==0 || projects.Length>100) throw new Exception(UiText.Get("text.182"));
                    if(projects.Any(p=>!Path.IsPathRooted(p) || !File.Exists(p) || !(String.Equals(Path.GetExtension(p),".aep",StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetExtension(p),".aepx",StringComparison.OrdinalIgnoreCase)))) throw new Exception(UiText.Get("text.183"));
                    var beforeIds=new HashSet<string>(queue.Snapshot().Select(j=>j.Id),StringComparer.OrdinalIgnoreCase);
                    int added=queue.Add(projects.Distinct(StringComparer.OrdinalIgnoreCase).Select(AepFileItem.FromPath).ToList(),explorer.Settings);
                    var addedIds=queue.Snapshot().Where(j=>!beforeIds.Contains(j.Id)).Select(j=>j.Id).ToArray();
                    PublishQueue(); Send(new { type="queued",count=added,ids=addedIds,source="bridge" }); return new { added=added,paused=queue.Paused };
                default: throw new Exception(UiText.Get("text.184"));
            }
        }
        private string[] Paths(Dictionary<string,object> data)
        { return data.ContainsKey("paths") ? json.Deserialize<string[]>(json.Serialize(data["paths"])) : new string[0]; }
        private async void OnMessage(object sender,CoreWebView2WebMessageReceivedEventArgs e)
        {
            Trace("message from "+e.Source);
            if(e.Source!="about:blank") return;
            try
            {
                var d=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson); string action=TextValue(d,"action");
                switch(action)
                {
                    case "ready": ready=true; ApplyAppearance(explorer.Settings); Publish(); PublishQueue(); timer.Start(); if(arguments.Contains("--ui-test")) await UiTest(); if(arguments.Contains("--picker-test")) { string picked=FolderPicker.Pick(Handle,explorer.Folder,UiText.Get("text.185")); File.WriteAllText(Path.Combine(Store.Root,"picker-result.txt"),picked ?? "CANCEL"); Close(); } return;
                    case "navigate": explorer.Navigate(TextValue(d,"path")); break;
                    case "back": explorer.History(-1); break;
                    case "forward": explorer.History(1); break;
                    case "up": explorer.Up(); break;
                    case "refresh": explorer.Refresh(); break;
                    case "autoRefresh": Send(new { type="files",folder=explorer.Folder,files=FilePayload() }); return;
                    case "browse": { string path=FolderPicker.Pick(Handle,explorer.Folder,UiText.Get("text.185")); if(path!=null) explorer.Navigate(path); } break;
                    case "favorite": if(explorer.Settings.FavoriteFolders.Contains(TextValue(d,"path"),StringComparer.OrdinalIgnoreCase)) explorer.Navigate(TextValue(d,"path")); break;
                    case "toggleFavorite": explorer.ToggleFavorite(); break;
                    case "toggle": explorer.Toggle(TextValue(d,"path")); break;
                    case "setSelection": explorer.SetMany(Paths(d),Convert.ToBoolean(d["selected"])); break;
                    case "selectAll": explorer.SetMany(Paths(d),true); break;
                    case "clearAll": explorer.SetMany(explorer.Files.Select(f=>f.FullPath),false); break;
                    case "itemSize": explorer.Settings.ItemSize=new[]{"small","medium","large"}.Contains(TextValue(d,"value")) ? TextValue(d,"value") : "medium"; explorer.Save(); break;
                    case "columns":
                        var widths=json.Deserialize<double[]>(json.Serialize(d["widths"]));
                        if(widths.Length!=3 || widths.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v)||v<1||v>10000)) throw new Exception(UiText.Get("text.186"));
                        explorer.Settings.ColumnWidths=widths; explorer.Save(); break;
                    case "view": explorer.Settings.LargeIcons=TextValue(d,"value")=="cards"; explorer.Save(); break;
                    case "versions": explorer.Settings.AllVersions=TextValue(d,"value")=="all"; explorer.Save(); break;
                    case "recursive": explorer.Settings.RecursiveFolders=Convert.ToBoolean(d["value"]); explorer.Refresh(); explorer.Save(); break;
                    case "openFolder": System.Diagnostics.Process.Start("explorer.exe",RenderCommand.Quote(explorer.Folder)); break;
                    case "enqueue":
                    case "render":
                        var requested=new HashSet<string>(Paths(d),StringComparer.OrdinalIgnoreCase);
                        var selected=explorer.Files.Where(f=>requested.Contains(f.FullPath) && explorer.Selected.Contains(f.FullPath)).ToList();
                        if(selected.Count==0) throw new Exception(UiText.Get("text.187"));
                        if(!explorer.Settings.Engines.Any(engine=>engine.Available)) { Send(new { type="openSettings" }); throw new Exception(UiText.Get("text.188")); }
                        var existingIds=new HashSet<string>(queue.Snapshot().Select(j=>j.Id),StringComparer.OrdinalIgnoreCase);
                        int added=queue.Add(selected,explorer.Settings); if(action=="render") queue.Start();
                        var queuedIds=queue.Snapshot().Where(j=>!existingIds.Contains(j.Id)).Select(j=>j.Id).ToArray();
                        Send(new { type="queued",count=added,ids=queuedIds,source=action=="enqueue"?"main":"render" }); break;
                    case "startQueue": queue.Start(); break;
                    case "pauseQueue": queue.Pause(); break;
                    case "renderSelected": queue.StartSelected(Paths(d)); break;
                    case "releaseSelected": queue.ReleaseSelected(Paths(d)); break;
                    case "requeueHistory":
                        var requeuedIds=queue.RequeueHistory(Paths(d),explorer.Settings);
                        Send(new { type="requeued",count=requeuedIds.Length,ids=requeuedIds }); break;
                    case "terminateAll": queue.TerminateAll(); break;
                    case "deleteQueue": queue.DeleteItems(Paths(d),TextValue(d,"page")=="history",d.ContainsKey("all") && Convert.ToBoolean(d["all"])); break;
                    case "clearQueuePrompt": explorer.Settings.ConfirmClearQueue=Convert.ToBoolean(d["value"]); explorer.Save(); Send(new { type="clearQueuePrompt",value=explorer.Settings.ConfirmClearQueue }); return;
                    case "clearHistoryPrompt": explorer.Settings.ConfirmClearHistory=Convert.ToBoolean(d["value"]); explorer.Save(); Send(new { type="clearHistoryPrompt",value=explorer.Settings.ConfirmClearHistory }); return;
                    case "closeAnswer":
                        if(!closePromptOpen) return;
                        closePromptOpen=false;
                        if(Convert.ToBoolean(d["confirmed"])) { closing=true; queue.Pause(); queue.Cancel(queue.Snapshot().Where(j=>j.Active).Select(j=>j.Id).ToArray()); if(queue.ActiveCount==0) Close(); else Enabled=false; }
                        return;
                    case "reorderQueue": queue.Reorder(Paths(d),TextValue(d,"target"),TextValue(d,"position")); break;
                    case "logs": Directory.CreateDirectory(Path.Combine(Store.Root,"logs")); System.Diagnostics.Process.Start(Path.Combine(Store.Root,"logs")); break;
                    case "log": PublishLog(TextValue(d,"id")); return;
                    case "browseEngine":
                        BeginInvoke(new Action(delegate {
                            try { using(var dialog=new OpenFileDialog { Filter=UiText.Get("text.189"),FileName="aerender.exe",CheckFileExists=true })
                                if(dialog.ShowDialog(this)==DialogResult.OK) Send(new { type="engineAdded",engine=EngineCatalog.Inspect(dialog.FileName) }); }
                            catch(Exception ex) { Send(new { type="error",message=ex.Message }); }
                        })); return;
                    case "scanEngines": Send(new { type="enginesScanned",engines=EngineCatalog.Scan() }); return;
                    case "browseOutput": { string path=FolderPicker.Pick(Handle,TextValue(d,"path"),UiText.Get("text.190")); if(path!=null) Send(new { type="field",key="OutputFolder",value=path }); } return;
                    case "previewAppearance": preview=json.Deserialize<Preferences>(json.Serialize(d["settings"])); ApplyAppearance(preview); return;
                    case "cancelAppearance": preview=null; ApplyAppearance(explorer.Settings); return;
                    case "saveSettings":
                        var settings=json.Deserialize<Preferences>(json.Serialize(d["settings"])); settings.Options.Validate();
                        if(settings.Concurrency<1 || settings.Concurrency>4) throw new Exception(UiText.Get("text.191"));

                        explorer.Settings.Engines=EngineCatalog.Refresh(settings.Engines); explorer.Settings.EnginesInitialized=true; explorer.Settings.Options=settings.Options; explorer.Settings.Concurrency=settings.Concurrency;
                        explorer.Settings.TextSize=new[]{"small","medium","large"}.Contains(settings.TextSize) ? settings.TextSize : "medium";
                        explorer.Settings.FollowSystem=settings.FollowSystem; explorer.Settings.Theme=settings.Theme=="dark" ? "dark" : "light";
                        explorer.Settings.Language=UiText.Normalize(settings.Language);
                        explorer.Settings.ConfirmClearQueue=settings.ConfirmClearQueue; explorer.Settings.ConfirmClearHistory=settings.ConfirmClearHistory;
                        preview=null; ApplyAppearance(explorer.Settings);
                        explorer.Save(); queue.Concurrency=settings.Concurrency; Send(new { type="settingsSaved" }); break;
                    default: return;
                }
                Publish(); PublishQueue();
            }
            catch(Exception ex) { Send(new { type="error",message=ex.Message }); Publish(); }
        }
        private void Send(object message) { if(ready && !IsDisposed) web.CoreWebView2.PostWebMessageAsJson(json.Serialize(message)); }
        private void Publish()
        {
            Send(new { type="state",folder=explorer.Folder,back=explorer.CanBack,forward=explorer.CanForward,settings=explorer.Settings,
                files=FilePayload() });
        }
        private object FilePayload() { return explorer.Files.Select(f=>new { path=f.FullPath,name=f.DisplayName,fileName=f.FileName,version=f.VersionText,versionNumber=f.Version,group=f.GroupKey,size=f.SizeText,length=f.Length,modified=f.ModifiedAt.ToString("yyyy-MM-dd HH:mm"),modifiedOrder=f.ModifiedAt.ToUniversalTime().Ticks/10000,selected=explorer.Selected.Contains(f.FullPath) }).ToArray(); }
        private void PublishQueue()
        {
            if(!ready) return; string payload=json.Serialize(new { type="queue",paused=queue.Paused,selectedMode=queue.SelectedMode,error=queue.StorageError,jobs=queue.Snapshot().Select(j=>new { id=j.Id,name=j.Name,state=j.State,attempts=j.Attempts,progress=j.Progress,error=j.Error,output=j.Output,project=j.Project,active=j.Active,history=j.History,requeued=j.Requeued,finished=j.FinishedUtc.HasValue?j.FinishedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):UiText.Get("text.019"),renderSeconds=j.RenderSeconds,finishedOrder=j.FinishedUtc.HasValue?j.FinishedUtc.Value.Ticks/10000:0,engine=j.Engine,engineVersion=j.EngineVersion,projectAeVersion=j.ProjectAeVersion }).ToArray() });
            if(payload!=lastQueue) { web.CoreWebView2.PostWebMessageAsJson(payload); lastQueue=payload; }
        }
        private void PublishLog(string id)
        {
            var j=queue.Snapshot().FirstOrDefault(x=>x.Id==id); if(j==null) return;
            string content=UiText.Get("text.192");
            if(File.Exists(j.Log)) using(var file=new FileStream(j.Log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) using(var reader=new StreamReader(file,Encoding.UTF8))
            { bool tail=file.Length>64000; if(tail) { file.Seek(-64000,SeekOrigin.End); reader.ReadLine(); } content=(tail ? UiText.Get("text.193") : "")+reader.ReadToEnd(); }
            Send(new { type="log",id=id,content=content });
        }
        private void OnClose(object sender,FormClosingEventArgs e)
        {
            if(queue.ActiveCount>0)
            {
                e.Cancel=true;
                if(!closing && !closePromptOpen) { closePromptOpen=true; Send(new { type="confirmClose" }); }
                return;
            }
            SaveWindowSize(); timer.Stop(); monitor.Dispose(); SystemEvents.UserPreferenceChanged-=ThemeChanged; web.Dispose();
        }
        private async Task UiTest()
        {
            // Test the real embedded HTML and bridge without desktop input.
            try
            {
                await Task.Delay(500);
                string results=await web.CoreWebView2.ExecuteScriptAsync("window.runUiTests()");
                File.WriteAllText(Path.Combine(Store.Root,"ui-results.json"),results);
                string clickedPath=json.Deserialize<string>(await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.file-row').dataset.path"));
                bool prior=explorer.Selected.Contains(clickedPath);
                await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.file-row').click()"); await Task.Delay(300);
                if(explorer.Selected.Contains(clickedPath)==prior) throw new Exception("HTML row click did not toggle the native selection model");
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('files').click();window.dispatchEvent(new Event('blur'));document.body.click()"); await Task.Delay(100);
                if(explorer.Selected.Contains(clickedPath)==prior) throw new Exception("Blank click or blur reset selection");
                await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.file-row').click()"); await Task.Delay(200);
                if(explorer.Selected.Contains(clickedPath)!=prior) throw new Exception("Second click failed to restore the prior checked state");
                string initialFolder=explorer.Folder;
                if(explorer.Settings.RecursiveFolders) throw new Exception("Folder traversal did not default to off");
                string recursiveFolder=Path.Combine(initialFolder,"recursive-ui","deep");Directory.CreateDirectory(recursiveFolder);
                string rootVersion=Path.Combine(initialFolder,"RecursiveUiShot_t01.aep"),nestedVersion=Path.Combine(recursiveFolder,"RecursiveUiShot_t05.aep");
                File.WriteAllText(rootVersion,"root version");File.WriteAllText(nestedVersion,"nested version");
                explorer.Refresh();Publish();await Task.Delay(160);
                await web.CoreWebView2.ExecuteScriptAsync("$('recursive').click()");await Task.Delay(220);
                if(!explorer.Settings.RecursiveFolders||!explorer.Files.Any(f=>String.Equals(f.FullPath,nestedVersion,StringComparison.OrdinalIgnoreCase))) throw new Exception("Folder traversal did not discover the nested AEP");
                if(await web.CoreWebView2.ExecuteScriptAsync("visible.filter(f=>f.group==='recursiveuishot').length===1&&visible.some(f=>f.path==="+json.Serialize(nestedVersion)+")")!="true") throw new Exception("Latest-only filtering did not choose the newest nested AEP");
                if(await web.CoreWebView2.ExecuteScriptAsync("window.matchMedia('(prefers-reduced-motion: reduce)').matches||(!$('filePanel').getAnimations().length&&[...$('files').querySelectorAll('.file-row')].some(el=>el.getAnimations().length>0))")!="true") throw new Exception("Enabling folder traversal did not show per-row feedback");
                await web.CoreWebView2.ExecuteScriptAsync("$('recursive').click()");await Task.Delay(220);
                if(explorer.Settings.RecursiveFolders||explorer.Files.Any(f=>String.Equals(f.FullPath,nestedVersion,StringComparison.OrdinalIgnoreCase))) throw new Exception("Disabling folder traversal did not return to the current directory");
                if(await web.CoreWebView2.ExecuteScriptAsync("window.matchMedia('(prefers-reduced-motion: reduce)').matches||(!$('filePanel').getAnimations().length&&[...$('files').querySelectorAll('.file-row')].some(el=>el.getAnimations().length>0))")!="true") throw new Exception("Disabling folder traversal did not show per-row feedback");
                string blockMotion=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const media=window.matchMedia.bind(window),saved=state.settings.LargeIcons;window.matchMedia=q=>q.includes('prefers-reduced-motion')?{matches:false}:media(q);try{for(const cards of [false,true]){stopFileMotions();state.settings.LargeIcons=cards;renderFiles();const row=$('files').querySelector('.file-row'),source=getComputedStyle(row),font=getComputedStyle(row.querySelector('.name-copy strong')).fontSize,grid=source.gridTemplateColumns;const rows=[...$('files').querySelectorAll('.file-row')].map((el,index)=>({path:el.dataset.path,index:index+1,rect:{...el.getBoundingClientRect().toJSON(),top:el.getBoundingClientRect().top-12},clone:el.cloneNode(true)}));animateFileRows(rows);const layer=$('filePanel').querySelector('.file-motion-layer'),ghost=layer.querySelector('.file-row');if(!ghost||getComputedStyle(ghost.querySelector('.name-copy strong')).fontSize!==font||getComputedStyle(ghost).gridTemplateColumns!==grid)return 'clone layout changed';const animation=row.getAnimations()[0];if(!animation)return 'missing block animation';const frames=animation.effect.getKeyframes();if(!frames.some(f=>String(f.transform).includes(cards?'translateX':'translateY')))return 'wrong movement axis';if([...row.querySelectorAll('*')].some(el=>el.getAnimations().length))return 'child animated independently';}return 'PASS';}finally{stopFileMotions();state.settings.LargeIcons=saved;renderFiles();window.matchMedia=media;}})()");
                if(blockMotion!="\"PASS\"") throw new Exception("Project block movement/layout check failed: "+blockMotion);
                await web.CoreWebView2.ExecuteScriptAsync("window.cardClipResult=null;(async()=>{const savedFiles=state.files,savedSettings={...state.settings},theme=document.documentElement.getAttribute('data-theme'),media=window.matchMedia.bind(window);window.matchMedia=q=>q.includes('prefers-reduced-motion')?{matches:false}:media(q);try{for(const mode of ['light','dark'])for(const size of ['small','medium','large']){stopFileMotions();document.documentElement.setAttribute('data-theme',mode);state.settings={...savedSettings,LargeIcons:true,AllVersions:true,ItemSize:size};state.files=Array.from({length:60},(_,i)=>({...savedFiles[0],path:'clip-test-'+i,group:'clip-test-'+i,name:'Card '+i,selected:false}));applyObjectLayout();renderFiles();const area=$('files');area.classList.remove('view-enter');area.scrollTop=area.scrollHeight;const height=area.clientHeight;const rows=[...area.querySelectorAll('.file-row')].map((el,index)=>({path:el.dataset.path,index:index+1,rect:{...el.getBoundingClientRect().toJSON(),left:el.getBoundingClientRect().left-12},clone:el.cloneNode(true)}));animateFileRows(rows);const layer=$('filePanel').querySelector('.file-motion-layer');if(!layer||Math.abs(layer.getBoundingClientRect().width-area.clientWidth)>1)throw Error('overlay overlaps scrollbar: '+mode+'/'+size);const motions=fileListMotions.slice();for(const time of [0,100,190,350,550]){motions.forEach(m=>{if(m.playState!=='idle')m.currentTime=time;});await new Promise(requestAnimationFrame);if(getComputedStyle(area).overflowX!=='hidden'||area.clientHeight!==height)throw Error('horizontal scrollbar flash: '+mode+'/'+size);}}window.cardClipResult='PASS';}catch(e){window.cardClipResult=String(e);}finally{stopFileMotions();state.files=savedFiles;state.settings=savedSettings;document.documentElement.setAttribute('data-theme',theme);renderState(state);window.matchMedia=media;}})()");
                await Task.Delay(1600);
                string cardClip=await web.CoreWebView2.ExecuteScriptAsync("window.cardClipResult");
                if(cardClip!="\"PASS\"") throw new Exception("Card animation clipping check failed: "+cardClip);
                await web.CoreWebView2.ExecuteScriptAsync("window.runInteractionTests().then(r=>window.interactionResult=r)");
                await Task.Delay(3500);
                string interaction=await web.CoreWebView2.ExecuteScriptAsync("window.interactionResult");
                File.WriteAllText(Path.Combine(Store.Root,"interaction-results.json"),interaction);
                if(interaction=="null" || interaction.Contains("\"error\"")) throw new Exception("Interaction tests failed: "+interaction);
                await TestDrag();
                await TestLayout();
                
                Trace("viewport: "+await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({width:innerWidth,height:innerHeight,dpr:devicePixelRatio})")+" zoom="+web.ZoomFactor);
                double originalZoom=web.ZoomFactor; Size originalSize=Size;
                Size=new Size((int)(Width*.85),(int)(Height*.85)); await Task.Delay(250);
                if(web.ZoomFactor>=originalZoom) throw new Exception("Window resize did not scale controls down");
                Size=originalSize; await Task.Delay(250);
                string target=Path.GetFullPath(Path.Combine(Store.Root,"ui-favorite")); Directory.CreateDirectory(target);
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('address').value="+json.Serialize(target)+";document.getElementById('address').dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}))"); await Task.Delay(250);
                if(Store.Load("settings.json",new Preferences()).Folder!=target) throw new Exception("HTML address navigation did not persist immediately");
                string favoriteReady=json.Deserialize<string>(await web.CoreWebView2.ExecuteScriptAsync("(()=>JSON.stringify({handler:typeof $('toggleFavorite').onclick,inert:document.querySelector('.app').inert,disabled:$('toggleFavorite').disabled,modal:!!document.querySelector('.modal-shade.open')}))()"));
                if(favoriteReady.Contains("\"inert\":true")||favoriteReady.Contains("\"disabled\":true")||!favoriteReady.Contains("\"handler\":\"function\"")) throw new Exception("Favorite button was not interactive: "+favoriteReady);
                await web.CoreWebView2.ExecuteScriptAsync("$('toggleFavorite').onclick()");await Task.Delay(500);
                if(!explorer.Settings.FavoriteFolders.Contains(target,StringComparer.OrdinalIgnoreCase)) throw new Exception("Address-bar star did not persist the current folder as a favorite");
                if(await web.CoreWebView2.ExecuteScriptAsync("$('toggleFavorite').classList.contains('active')&&$('toggleFavorite').getAttribute('aria-pressed')==='true'&&!!$('recents').querySelector('[data-favorite]')")!="true") throw new Exception("Favorite did not fill the star and appear in the sidebar");
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('back').click()"); await Task.Delay(250);
                if(explorer.Folder!=initialFolder) throw new Exception("Back navigation failed");
                await web.CoreWebView2.ExecuteScriptAsync("$('recents').onclick({target:$('recents').querySelector('[data-favorite]')})");await Task.Delay(250);
                if(explorer.Folder!=target) throw new Exception("Sidebar favorite did not open its saved folder");
                await web.CoreWebView2.ExecuteScriptAsync("$('toggleFavorite').onclick()");await Task.Delay(250);
                if(explorer.IsFavorite(target)||explorer.Folder!=target) throw new Exception("Filled star did not remove the current favorite");
                await web.CoreWebView2.ExecuteScriptAsync("$('back').onclick()");await Task.Delay(250);
                if(explorer.Folder!=initialFolder) throw new Exception("Removing a favorite changed navigation history");
                await web.CoreWebView2.ExecuteScriptAsync("$('toggleFavorite').onclick()");await Task.Delay(180);
                if(!explorer.IsFavorite(initialFolder)) throw new Exception("Current folder could not be favorited after navigation");
                string displayFavorite=Path.Combine(Store.Root,"动画项目","第一集","镜头文件");Directory.CreateDirectory(displayFavorite);explorer.Settings.FavoriteFolders.Insert(0,displayFavorite);explorer.Save();Publish();await Task.Delay(180);
                using(var output=File.Create(Path.Combine(Store.Root,"favorites.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                File.WriteAllText(Path.Combine(Store.Root,"bridge-results.txt"),"PASS: HTML click -> native toggle; blank and blur preserve; second click toggles; address Enter saves immediately; Back restores folder");
                string externalProject=explorer.Files.First().FullPath;
                ExternalCommand(new Dictionary<string,object>{{"action","enqueue"},{"paths",new object[]{externalProject}}}); await Task.Delay(250);
                string externalId=queue.Snapshot().First(j=>String.Equals(j.Project,externalProject,StringComparison.OrdinalIgnoreCase) && !j.History).Id;
                if(await web.CoreWebView2.ExecuteScriptAsync("queueSelections.queue.has("+json.Serialize(externalId)+")")!="true") throw new Exception("AE bridge enqueue did not select the new queue item");
                if(await web.CoreWebView2.ExecuteScriptAsync("$('queueModal').classList.contains('open')")!="false") throw new Exception("Queue receipt unexpectedly opened the render center");
                if(await web.CoreWebView2.ExecuteScriptAsync("$('showQueue').classList.contains('task-impact')&&$('queueCount').classList.contains('task-impact')&&Number($('queueCount').textContent)>=0&&Number($('queueCount').textContent)<=1")!="true") throw new Exception("Queue receipt count / elastic animation did not start");
                timer.Stop();queue.StartSelected(new[]{externalId});PublishQueue();await Task.Delay(180);
                if(queue.Snapshot().First(j=>j.Id==externalId).State!=UiText.Source("queue.selectedPending")) throw new Exception("Render selected did not expose the ready-to-render state");
                if(await web.CoreWebView2.ExecuteScriptAsync("(()=>{show('queueModal');queueTab('queue');renderQueue(queueState);const row=$('queueList').querySelector('[data-id=\""+externalId+"\"]');return row&&row.querySelector('.status').dataset.state==='selectedPending'&&!$('toggleHeld').disabled&&$('toggleHeld').textContent===t('queue.holdSelected')})()")!="true") throw new Exception("Ready-to-render task did not expose the hold action");
                await web.CoreWebView2.ExecuteScriptAsync("$('toggleHeld').click()");await Task.Delay(180);
                if(queue.Snapshot().First(j=>j.Id==externalId).State!=UiText.Source("text.038")) throw new Exception("Holding a ready task did not return it to waiting");
                timer.Start();
                queue.DeleteItems(new[]{externalId},false,false);PublishQueue();await Task.Delay(120);await web.CoreWebView2.ExecuteScriptAsync("hide('queueModal')");
                await Task.Delay(400);
                using(var output=File.Create(Path.Combine(Store.Root,"preview.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("show('authorModal')");await Task.Delay(180);
                if(await web.CoreWebView2.ExecuteScriptAsync("(()=>{const footer=$('authorModal').querySelector('.modal-footer'),version=footer.querySelector('.author-version'),close=footer.querySelector('[data-close]');return version&&close&&version.getBoundingClientRect().left<close.getBoundingClientRect().left&&Math.abs(version.getBoundingClientRect().top-close.getBoundingClientRect().top)<16})()")!="true") throw new Exception("Author version was not aligned at the lower left of the close-button row");
                if(await web.CoreWebView2.ExecuteScriptAsync("(()=>{const identity=$('authorModal').querySelector('.modal-head .author-identity'),mark=identity.querySelector('.brandmark'),name=identity.querySelector('h2'),brand=$('brandInfo'),sideMark=brand.querySelector('.brandmark'),mr=mark.getBoundingClientRect(),nr=name.getBoundingClientRect(),br=brand.getBoundingClientRect(),sr=sideMark.getBoundingClientRect();return getComputedStyle(identity).display==='flex'&&mr.right<nr.left&&Math.abs((mr.top+mr.height/2)-(nr.top+nr.height/2))<3&&sr.left-br.left>=7&&sr.top-br.top>=6&&br.right-sr.right>=7&&br.bottom-sr.bottom>=6})()")!="true") throw new Exception("Author identity heading or sidebar brand hover padding was incorrect");
                if(await web.CoreWebView2.ExecuteScriptAsync("(()=>{const modal=$('authorModal').querySelector('.modal'),head=modal.querySelector('.modal-head'),body=modal.querySelector('.modal-body'),copy=modal.querySelector('.author-copy');return modal.getBoundingClientRect().width>=530&&getComputedStyle(head).justifyContent==='flex-start'&&getComputedStyle(body).textAlign==='center'&&copy.textContent.split('\\n').length===4})()")!="true") throw new Exception("Author window was not wider, centered, or four lines long");
                using(var output=File.Create(Path.Combine(Store.Root,"author.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("hide('authorModal')");await Task.Delay(120);
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('cardView').click()"); await Task.Delay(250);
                string cardHeader=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const h=document.querySelector('.table-head');return getComputedStyle(h).display==='grid'&&h.querySelectorAll('[data-sort]').length===3&&h.querySelectorAll('.column-resizer').length===2})()");
                if(cardHeader!="true") throw new Exception("Card view did not retain the sortable, resizable Name / Version / Modified header");
                using(var output=File.Create(Path.Combine(Store.Root,"cards.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('listView').click();document.getElementById('showSettings').click()"); await Task.Delay(250);
                string windowCheck=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const m=document.querySelector('#settingsModal .modal'),h=m.querySelector('.modal-head'),before=m.getBoundingClientRect();if(m.querySelectorAll('.modal-resize').length!==4||getComputedStyle(h).cursor!=='move'||getComputedStyle(m).resize!=='none')return false;modalGesture={modal:m,kind:'move',pointerId:91,startX:before.left+20,startY:before.top+20,rect:before};updateModalGesture({pointerId:91,clientX:before.left+40,clientY:before.top+30});finishModalGesture();const moved=m.getBoundingClientRect(),didMove=Math.abs(moved.left-before.left)>5||Math.abs(moved.top-before.top)>5;modalGesture={modal:m,kind:'size',corner:'se',pointerId:92,startX:moved.right,startY:moved.bottom,rect:moved};updateModalGesture({pointerId:92,clientX:moved.right-30,clientY:moved.bottom-25});finishModalGesture();const resized=m.getBoundingClientRect();return didMove&&resized.width<moved.width&&resized.height<moved.height})()");
                if(windowCheck!="true") throw new Exception("Secondary windows did not support title dragging and four-corner resize");
                using(var output=File.Create(Path.Combine(Store.Root,"settings.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('FollowSystem').checked=false;document.getElementById('FollowSystem').dispatchEvent(new Event('change'));document.getElementById('lightTheme').click()"); await Task.Delay(350);
                using(var output=File.Create(Path.Combine(Store.Root,"settings-light.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('advancedTab').click()"); await Task.Delay(350);
                using(var output=File.Create(Path.Combine(Store.Root,"advanced.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("hide('settingsModal');send('previewAppearance',{settings:{...state.settings,FollowSystem:false,Theme:'light'}})"); await Task.Delay(350);
                using(var output=File.Create(Path.Combine(Store.Root,"preview-light.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("document.getElementById('showQueue').click();renderQueue({paused:false,jobs:['运行','等待','待渲染','重试等待','完成','失败','正在取消','取消','中断'].map((s,i)=>({id:'preview-'+i,name:'示例项目 · '+(i+1),state:s,attempts:s==='重试等待'?2:1,progress:s==='运行'?'正在渲染 · 第 48 帧':'状态样式预览',active:s==='运行'||s==='重试等待'}))})"); await Task.Delay(350);
                if(await web.CoreWebView2.ExecuteScriptAsync("(()=>{const row=$('queueList').querySelector('[data-id=\"preview-0\"]');row.click();return row.classList.contains('locked')&&row.getAttribute('aria-disabled')==='true'&&!queueSelections.queue.has('preview-0')&&selectedLogJob().id==='preview-0'})()")!="true") throw new Exception("Active task checkbox was not disabled while row log focus remained available");
                if(await web.CoreWebView2.ExecuteScriptAsync("$('queueSummary').querySelector('.failed-count').classList.contains('has-fail')")!="true") throw new Exception("Positive failure count did not enable red styling");
                if(await web.CoreWebView2.ExecuteScriptAsync("stripHoverTips();document.querySelectorAll('[title]').length")!="0") throw new Exception("Hover tooltips remained after dynamic queue rendering");
                using(var output=File.Create(Path.Combine(Store.Root,"queue-states.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                if(await web.CoreWebView2.ExecuteScriptAsync("renderQueue({paused:true,jobs:[]});$('queueSummary').querySelector('.failed-count').classList.contains('has-fail')")!="false") throw new Exception("Zero failure count retained red styling");
                await web.CoreWebView2.ExecuteScriptAsync("queueTab('queue');renderQueue({paused:false,selectedMode:false,jobs:Array.from({length:60},(_,i)=>({id:'visual-'+i,name:'项目名称 · '+(i+1),project:'C:/Example/project.aep',state:'等待',attempts:0,progress:'等待调度',active:false}))})"); await Task.Delay(250);
                string fixedCheck=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const tabs=$('queueTabs').getBoundingClientRect().top,toolbar=$('queueToolbar').getBoundingClientRect().top,list=$('queueList');list.scrollTop=500;return list.scrollTop>0&&tabs===$('queueTabs').getBoundingClientRect().top&&toolbar===$('queueToolbar').getBoundingClientRect().top&&getComputedStyle(document.querySelector('.queue-modal .modal-body')).overflowY==='hidden'&&$('queueTop').getBoundingClientRect().top>list.getBoundingClientRect().bottom})()");
                if(fixedCheck!="true") throw new Exception("Queue fixed controls / scrolling / footer layout failed");
                using(var output=File.Create(Path.Combine(Store.Root,"queue-autonomous.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("queueTab('history');renderQueue({paused:true,selectedMode:false,jobs:Array.from({length:30},(_,i)=>({id:'history-'+i,name:'测试项目 · '+(i+1),project:'C:/Example/project.aep',state:i%4===0?'失败':'完成',attempts:1,progress:'完成',active:false,finished:'2026-09-17 12:30:00',finishedOrder:i,renderSeconds:i%4===0?null:65+i*30}))})"); await Task.Delay(250);
                if(await web.CoreWebView2.ExecuteScriptAsync("$('queueActions').parentNode===$('queueFooterSlot')&&$('queueTop').classList.contains('hidden')&&$('queueBottom').classList.contains('hidden')&&!$('requeueHistory').closest('#historyActions').classList.contains('hidden')")!="true") throw new Exception("History controls were not placed in footer with Requeue above");
                using(var output=File.Create(Path.Combine(Store.Root,"queue-history.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("document.documentElement.dataset.theme='dark';queueTab('queue');renderQueue({...queueState,paused:false})"); await Task.Delay(250);
                using(var output=File.Create(Path.Combine(Store.Root,"queue-dark.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                string angleBefore=await web.CoreWebView2.ExecuteScriptAsync("getComputedStyle($('startQueue'),'::before').getPropertyValue('--dispatch-angle')"); await Task.Delay(180);
                string angleAfter=await web.CoreWebView2.ExecuteScriptAsync("getComputedStyle($('startQueue'),'::before').getPropertyValue('--dispatch-angle')");
                if(angleBefore==angleAfter && await web.CoreWebView2.ExecuteScriptAsync("matchMedia('(prefers-reduced-motion: reduce)').matches")!="true") throw new Exception("Autonomous border animation is not rotating");
                await web.CoreWebView2.ExecuteScriptAsync("queueTab('history');state.settings.ConfirmClearHistory=true;$('queueClear').click();$('skipClearPrompt').checked=true;$('skipClearPrompt').dispatchEvent(new Event('change'));"); await Task.Delay(250);
                if(Store.Load("settings.json",new Preferences()).ConfirmClearHistory) throw new Exception("History clear preference did not persist through native bridge");
                using(var output=File.Create(Path.Combine(Store.Root,"history-confirm.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("$('confirmNo').click();hide('queueModal');$('showSettings').click();settingsTab('advanced')"); await Task.Delay(200);
                if(await web.CoreWebView2.ExecuteScriptAsync("$('ConfirmClearHistory').checked")!="false") throw new Exception("History clear preference not synchronized with advanced settings");
                using(var output=File.Create(Path.Combine(Store.Root,"history-setting.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await TestLocalization();
                File.WriteAllText(Path.Combine(Store.Root,"queue-layout-results.txt"),"PASS: actual WebView scrolling keeps tabs and toolbar fixed; footer actions below list; autonomous/history/dark previews captured");
            }
            catch(Exception e) { File.WriteAllText(Path.Combine(Store.Root,"ui-error.txt"),e.ToString()); }
            finally { Close(); }
        }
        private async Task TestLocalization()
        {
            foreach(string language in new[]{"en","ja","zh"})
            {
                await web.CoreWebView2.ExecuteScriptAsync("hide('settingsModal');hide('queueModal');openSettings();$('Language').value="+json.Serialize(language)+";$('Language').dispatchEvent(new Event('change'))"); await Task.Delay(250);
                if(await web.CoreWebView2.ExecuteScriptAsync("activeLanguage")!=json.Serialize(language)) throw new Exception("Language preview failed: "+language);
                using(var output=File.Create(Path.Combine(Store.Root,"language-"+language+"-general.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("settingsTab('advanced')"); await Task.Delay(180);
                using(var output=File.Create(Path.Combine(Store.Root,"language-"+language+"-advanced.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("$('saveSettings').click()"); await Task.Delay(220);
                if(Store.Load("settings.json",new Preferences()).Language!=language) throw new Exception("Language preference was not saved: "+language);
                string toastCheck=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const el=$('toast'),style=getComputedStyle(el);return el.classList.contains('settings-toast')&&el.style.display==='block'&&Math.abs(el.getBoundingClientRect().left+$('toast').offsetWidth/2-innerWidth/2)<2&&style.transform!=='none'&&parseFloat(style.fontSize)>=14&&parseFloat(style.bottom)>=140})()");
                if(toastCheck!="true") throw new Exception("Settings-saved toast was not stably centered in the settings view");
                await web.CoreWebView2.ExecuteScriptAsync("hide('settingsModal')"); await Task.Delay(150);
                if(await web.CoreWebView2.ExecuteScriptAsync("$('toast').style.display")!="\"none\"") throw new Exception("Settings toast remained after the settings view closed");
                if(await web.CoreWebView2.ExecuteScriptAsync("activeLanguage")!=json.Serialize(language)) throw new Exception("Saved language was lost when settings closed: "+language);
                using(var output=File.Create(Path.Combine(Store.Root,"language-"+language+"-main.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("show('queueModal');queueTab('history');renderQueue({paused:true,jobs:[{id:'i18n-ok',name:'Sample-01',project:'C:/Example/sample.aep',state:C('text.029'),active:false,attempts:1,renderSeconds:125,finished:'2026-09-20 12:00:00',finishedOrder:1},{id:'i18n-fail',name:'Sample-02',project:'C:/Example/sample.aep',state:C('text.030'),active:false,attempts:1,finished:'2026-09-20 12:00:01',finishedOrder:2}]})"); await Task.Delay(180);
                string statusCheck=await web.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('.queue-row .status')].every(el=>['completed','failed'].includes(el.dataset.state))");
                if(statusCheck!="true") throw new Exception("Localized statuses changed stable styling keys");
                using(var output=File.Create(Path.Combine(Store.Root,"language-"+language+"-history.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("state.settings.ConfirmClearHistory=true;$('queueClear').click()"); await Task.Delay(160);
                using(var output=File.Create(Path.Combine(Store.Root,"language-"+language+"-confirm.png"))) await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
                await web.CoreWebView2.ExecuteScriptAsync("$('confirmNo').click();hide('queueModal');openSettings();$('Language').value="+json.Serialize(language=="en"?"ja":"en")+";$('Language').dispatchEvent(new Event('change'))");await Task.Delay(120);
                await web.CoreWebView2.ExecuteScriptAsync("hide('settingsModal')");await Task.Delay(120);
                if(await web.CoreWebView2.ExecuteScriptAsync("activeLanguage")!=json.Serialize(language) || Store.Load("settings.json",new Preferences()).Language!=language) throw new Exception("Cancel did not restore saved language");
            }
            File.WriteAllText(Path.Combine(Store.Root,"localization-results.txt"),"PASS: Chinese / English / Japanese preview, native persistence, close after save, cancel rollback, stable status keys; main/settings/history/confirmation screenshots captured");
        }
        private async Task TestDrag()
        {
            var original=explorer.Selected.ToArray();
            bool originalView=explorer.Settings.LargeIcons;
            for(int mode=0;mode<2;mode++)
            {
            explorer.Settings.LargeIcons=mode==1;
            explorer.SetMany(explorer.Files.Select(f=>f.FullPath),false); Publish(); await Task.Delay(200);
            for(int pass=0;pass<2;pass++)
            {
                string positions=await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify([...document.querySelectorAll('.file-row')].slice(0,3).map(e=>{const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}}))");
                var points=json.Deserialize<Dictionary<string,double>[]>(json.Deserialize<string>(positions));
                await MouseEvent("mousePressed",points[0],1);
                await MouseEvent("mouseMoved",points[1],1);
                await MouseEvent("mouseMoved",points[2],1);
                await MouseEvent("mouseReleased",points[2],0);
                await Task.Delay(250);
                string actual=await web.CoreWebView2.ExecuteScriptAsync("visible.slice(0,3).every(f=>f.selected==="+(pass==0 ? "true" : "false")+")");
                if(actual!="true") throw new Exception("Pointer drag "+(pass==0 ? "selection" : "deselection")+" failed");
                if(explorer.Selected.Count!=(pass==0 ? 3 : 0)) throw new Exception("Pointer selection not saved to native model");
            }
            }
            explorer.Settings.LargeIcons=originalView;
            explorer.SetMany(original,true); Publish(); await Task.Delay(200);
            File.WriteAllText(Path.Combine(Store.Root,"drag-results.txt"),"PASS: actual WebView pointer down / move / up selects and deselects three projects in BOTH list and card mode, persisted through native bridge");
        }
        private async Task TestLayout()
        {
            string checks=await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({noSize:!document.querySelector('.file-row .size'),handles:document.querySelectorAll('.column-resizer').length,adjacent:document.querySelector('.search').nextElementSibling.classList.contains('filter'),middle:document.getElementById('itemSize').value==='1',version:parseFloat(getComputedStyle(document.querySelector('.file-row .version')).fontSize)})");
            var result=json.Deserialize<Dictionary<string,object>>(json.Deserialize<string>(checks));
            if(!Convert.ToBoolean(result["noSize"]) || Convert.ToInt32(result["handles"])!=2 || !Convert.ToBoolean(result["adjacent"]) || !Convert.ToBoolean(result["middle"]) || Math.Abs(Convert.ToDouble(result["version"])-10.8)>0.05) throw new Exception("Layout defaults failed: "+checks);
            if(await web.CoreWebView2.ExecuteScriptAsync("getComputedStyle(document.querySelector('.file-row .modified')).fontSize===getComputedStyle(document.querySelector('.file-row .version')).fontSize")!="true") throw new Exception("Modified and version font sizes differ");
            double before=Convert.ToDouble(await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.file-row').getBoundingClientRect().height"));
            await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-item-size=\"2\"]').click()"); await Task.Delay(200);
            double after=Convert.ToDouble(await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('.file-row').getBoundingClientRect().height"));
            if(after<=before || Store.Load("settings.json",new Preferences()).ItemSize!="large") throw new Exception("Object size slider failed");
            await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-item-size=\"1\"]').click()"); await Task.Delay(200);
            string points=await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify([...document.querySelectorAll('.column-resizer')].map(e=>{const r=e.getBoundingClientRect();return {x:r.x+r.width/2,y:r.y+r.height/2}}))");
            var positions=json.Deserialize<Dictionary<string,double>[]>(json.Deserialize<string>(points));
            double columnBefore=Convert.ToDouble(await web.CoreWebView2.ExecuteScriptAsync("measuredColumns()[0]"));
            var target=new Dictionary<string,double>(positions[0]); target["x"]-=45;
            await MouseEvent("mousePressed",positions[0],1); await MouseEvent("mouseMoved",target,1); await MouseEvent("mouseReleased",target,0); await Task.Delay(250);
            double columnAfter=Convert.ToDouble(await web.CoreWebView2.ExecuteScriptAsync("measuredColumns()[0]"));
            if(columnBefore-columnAfter<35 || Store.Load("settings.json",new Preferences()).ColumnWidths[0]==60) throw new Exception("Resizable column bridge failed");
            explorer.Settings.ColumnWidths=new double[]{60,16,24}; explorer.Save(); Publish(); await Task.Delay(200);
            Size original=Size; Size=new Size((int)(Width*.85),(int)(Height*.85)); SaveWindowSize(); Size saved=Size;
            Size=original; RestoreWindowSize(); await Task.Delay(200);
            if(Size!=saved) throw new Exception("Window size did not restore saved dimensions");
            Size=original; SaveWindowSize(); await Task.Delay(200);
            File.WriteAllText(Path.Combine(Store.Root,"layout-results.txt"),"PASS: latest checkbox beside search; no file-size column; medium defaults; 1.5x version badge; item-size slider persists; actual pointer column resize persists; saved window dimensions restore");
        }
        private Task<string> MouseEvent(string kind,Dictionary<string,double> point,int buttons)
        {
            return web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",json.Serialize(new { type=kind,x=point["x"],y=point["y"],button="left",buttons=buttons,clickCount=1 }));
        }
    }
}
