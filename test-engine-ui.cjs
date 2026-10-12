// No browser/WebView process: unit-test engine-card markup and message handlers.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const html=fs.readFileSync(__dirname+'/ui.html','utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1].replace('/*__LOCALIZATION_RUNTIME__*/',fs.readFileSync(__dirname+'/Localization/runtime.js','utf8').replace('/*__STRINGS__*/','const UI_STRINGS='+fs.readFileSync(__dirname+'/Localization/strings.json','utf8')+';'));
const nodes=new Map(),messages=[],listeners={};let checks=0;
function node(id){
 if(!nodes.has(id)){
  const classes=new Set(),attrs=new Map(),events={};let value='',markup='';
  const element={id,disabled:false,checked:false,dataset:{},style:{setProperty(){}},textContent:'',classList:{add(...s){s.forEach(v=>classes.add(v));},remove(...s){s.forEach(v=>classes.delete(v));},contains(s){return classes.has(s);},toggle(s,on){if(on??!classes.has(s))classes.add(s);else classes.delete(s);}},
   removeAttribute(k){attrs.delete(k);},setAttribute(k,v){attrs.set(k,String(v));},getAttribute(k){return attrs.get(k);},appendChild(child){child.parentNode=this;return child;},events,addEventListener(k,f){events[k]=f;},focus(){},select(){},contains(row){return row?.dataset?.path?.startsWith('p');},getBoundingClientRect(){return {left:0,right:500,top:0,bottom:500};},setPointerCapture(){},hasPointerCapture(){return false;},releasePointerCapture(){},
   get value(){return value;},set value(v){value=String(v);},
   get innerHTML(){return markup;},set innerHTML(v){markup=v;for(const m of v.matchAll(/id="([^"]+)"/g))node(m[1]);}};
  nodes.set(id,element);
 }return nodes.get(id);
}
for(const m of html.matchAll(/id="([^"]+)"/g))node(m[1]);
for(const element of nodes.values()){element.querySelector=()=>node('focusedChild');element.querySelectorAll=()=>[];}
const document={getElementById(id){return nodes.get(id)||null;},querySelectorAll(){return [];},querySelector(s){return s==='.modal-shade.open'?null:node(s);},addEventListener(){},activeElement:{tagName:'BODY'},documentElement:node('root'),body:node('body')};
let frame;
const context=vm.createContext({document,console,structuredClone,Map,Set,JSON,Number,String,Math,Error,setInterval(){},setTimeout(){},clearTimeout(){},requestAnimationFrame(f){frame=f;return 1;},cancelAnimationFrame(){frame=null;},window:{chrome:{webview:{postMessage(m){messages.push(m);},addEventListener(k,f){const previous=listeners[k];listeners[k]=e=>{if(previous)previous(e);f(e);};}}},addEventListener(){}}});
context.window.addEventListener=()=>{};
vm.runInContext(script,context);
function run(code){return vm.runInContext(code,context);}
function check(ok,name){assert.ok(ok,name);checks++;console.log('PASS: '+name);}
const e23={Path:'C:\\custom\\AE23\\aerender.exe',Label:'AE 2023',Version:'23.0.0.59',Major:23,Minor:0,Patch:0,Build:59,Available:true};
const e26={...e23,Path:'D:\\AE26\\aerender.exe',Label:'AE 2026',Version:'26.0.0.1',Major:26};
run('state.settings='+JSON.stringify({Engines:[e23,e26],Options:{RetryDelay:3},Presets:{},Concurrency:1,TextSize:'medium',FollowSystem:true,FavoriteFolders:[]})+';openSettings()');
check((node('engineGrid').innerHTML.match(/class="engine-card/g)||[]).length===3,'two engine cards plus same-class add card');
check(node('engineGrid').innerHTML.includes('AE 2023')&&node('engineGrid').innerHTML.includes('AE 2026'),'both engine version labels displayed');
check(node('engineGrid').innerHTML.includes('engine-add')&&html.includes('border:1px dashed'),'add tile uses dashed style');
node('browseEngine').onclick();check(messages.at(-1).action==='browseEngine','plus tile requests native browse');
listeners.message({data:{type:'engineAdded',engine:{...e23,Path:e23.Path.toUpperCase(),Minor:6,Version:'23.6.0.1'}}});
check(run('draft.Engines.length')===2,'adding same path with different case updates without duplication');
listeners.message({data:{type:'engineAdded',engine:{...e23,Path:'C:\\bad\\aerender.exe',Label:'<img src=x>',Available:false,Error:'Missing'}}});
check(!node('engineGrid').innerHTML.includes('<img src=x>')&&node('engineGrid').innerHTML.includes('&lt;img src=x&gt;'),'engine metadata is HTML escaped');
check(node('engineGrid').innerHTML.includes('unavailable'),'missing engine has unavailable style');
node('engineGrid').onclick({target:{closest(){return {dataset:{removeEngine:'2'}};}}});
check(run('draft.Engines.length')===2&&run('state.settings.Engines.length')===2,'remove edits draft only');
node('scanEngines').onclick();check(messages.at(-1).action==='scanEngines'&&node('scanEngines').disabled,'rescan request disables button');
listeners.message({data:{type:'enginesScanned',engines:[e26]}});check(run('draft.Engines.length')===2&&!node('scanEngines').disabled,'rescan merges rather than losing custom paths');
node('saveSettings').onclick();const saved=messages.at(-1);check(saved.action==='saveSettings'&&saved.settings.Engines.length===2,'saving sends complete multi-engine registry');
check(run('state.settings.Engines[0].Minor')===0,'unsaved native state remains unchanged');
check(!html.includes('id="Aerender"'),'obsolete single engine text field removed');
run("settingsTab('advanced')");
const beforeSaveCount=messages.length;listeners.message({data:{type:'settingsSaved'}});
check(node('settingsModal').classList.contains('open')&&!node('advancedPane').classList.contains('hidden'),'save acknowledgement preserves open settings and current tab');
check(messages.length===beforeSaveCount,'save acknowledgement does not issue cancelAppearance');
check(node('toast').classList.contains('settings-toast')&&node('toast').style.display==='block','saved toast is centered within settings');
run("hide('settingsModal')");check(node('toast').style.display==='none','closing settings immediately hides its toast');run('openSettings()');
const rows=Array.from({length:6},(_,i)=>{const r=node('row'+i);r.dataset.path='p'+i;r.closest=()=>r;return r;});
document.elementFromPoint=(x,y)=>x===40?rows[4]:x===20?rows[2]:null;
function start(selected){
 run('state.files='+JSON.stringify(rows.map(r=>({path:r.dataset.path,selected})))+';visible=state.files;anchorPath=null');
 node('files').events.pointerdown({button:0,pointerId:1,clientX:10,clientY:100,shiftKey:false,target:rows[0],preventDefault(){}});
}
function move(x,y=100){node('files').events.pointermove({buttons:1,clientX:x,clientY:y});}
for(const initial of [false,true]){
 start(initial);move(40);move(100);move(40);move(20);node('files').events.pointerup();
 const selected=JSON.parse(run('JSON.stringify(state.files.map(f=>f.selected))'));
 check(selected.every((v,i)=>v===([0,2,4].includes(i)?!initial:initial)),(initial?'deselect':'select')+' touches only hovered projects; skipped rows and blank remain unchanged');
 check(JSON.stringify([...messages.at(-1).paths].sort())===JSON.stringify(['p0','p2','p4']),'persist only visited paths');
}
start(false);move(40,600);const callback=frame;callback();node('files').events.pointerup();
check(run('state.files.filter(f=>f.selected).length')===1,'moving outside list does not brush clamped edge items');
run("anchorPath='p0'");check(run("selectionRange('p4',true).length")===5,'explicit Shift range selection remains available');
listeners.message({data:{type:'filesChanged'}});
check(messages.at(-1).action==='autoRefresh','idle file notification requests latest file state');
start(false);const pendingCount=messages.length;
listeners.message({data:{type:'filesChanged'}});listeners.message({data:{type:'filesChanged'}});
check(messages.length===pendingCount && run('pendingFileRefresh'),'file changes deferred during brush gesture');
listeners.message({data:{type:'files',folder:run('state.folder'),files:[]}});
check(run('state.files.length')===6,'in-flight file reply does not replace dragged items');
node('files').events.pointerup();
check(messages.at(-2).action==='setSelection' && messages.at(-1).action==='autoRefresh','selection is persisted before deferred refresh');
check(!run('pendingFileRefresh'),'deferred refresh notifications coalesced');
context.window.confirm=()=>true;
const jobs=[{id:'w1',name:'B',state:'等待'},{id:'r',name:'Running',state:'运行',active:true},{id:'w2',name:'A',state:'等待'},{id:'h1',name:'Z',state:'完成',finishedOrder:100,finished:'one'},{id:'h2',name:'A',state:'失败',finishedOrder:200,finished:'two'},{id:'h0',name:'Old',state:'完成',finishedOrder:0}].map(j=>({...j,project:'C:/'+j.id+'.aep',attempts:1,progress:''}));
run('renderQueue('+JSON.stringify({jobs,paused:true})+')');
check(run('pageJobs().length')===3&&!node('queueList').innerHTML.includes('data-id="h1"'),'queue excludes completed and failed history');
check(node('queueList').innerHTML.includes('locked')&&node('queueList').innerHTML.includes('aria-disabled="true"'),'active render target has a locked checkbox');
check(!html.includes('.queue-row.locked .check:before')&&!html.includes('.queue-row.locked .check:after'),'active checkbox is simply greyed without a lock icon');
run("toggleQueue('r')");check(run("queueFocusId==='r'&&!queueSelections.queue.has('r')&&selectedLogJob().id==='r'"),'locked task click switches log without selecting checkbox');
listeners.message({data:{type:'queued',count:2,ids:['w1','w2'],source:'bridge'}});check(run("queueSelections.queue.has('w1')&&queueSelections.queue.has('w2')"),'AE bridge additions are selected in render queue');check(!node('queueModal').classList.contains('open')&&node('showQueue').classList.contains('task-impact')&&node('queueCount').classList.contains('task-impact')&&node('queueCount').textContent==='1','queue receipt counts from the previous total and animates without opening render center');node('queueSelectNone').onclick();
run("toggleQueue('w1');toggleQueue('w2')");check(messages.at(-1).id==='w2','multiple selection logs last selected task');
listeners.message({data:{type:'log',id:'w1',content:'stale'}});check(node('log').textContent!=='stale','out-of-order log responses are ignored');
run("toggleQueue('w2')");check(messages.at(-1).id==='w1','deselect latest returns to prior selected log');
node('queueSelectAll').onclick();check(run('queueSelected.size')===2&&!run("queueSelected.has('r')"),'select all excludes locked active tasks');
node('queueSelectNone').onclick();check(run('queueSelected.size')===0&&node('log').textContent==='等待选择任务。','clear selection clears log');check(node('toggleHeld').disabled&&node('toggleHeld').textContent==='搁置选中','hold button keeps its default label without a selection');
run("toggleQueue('w1');queueState.jobs.find(j=>j.id==='w1').state=C('queue.selectedPending');renderQueue(queueState)");
check(!node('toggleHeld').disabled&&node('toggleHeld').textContent==='搁置选中'&&node('queueList').innerHTML.includes('data-state="selectedPending"'),'ready-to-render selection exposes the hold action');
node('toggleHeld').onclick();check(messages.at(-1).action==='releaseSelected'&&messages.at(-1).paths.join()==='w1','hold returns selected ready task to ordinary waiting');
run("queueState.jobs.find(j=>j.id==='w1').state=C('text.038');queueSelected.clear();renderQueue(queueState)");
run("queueTab('history')");check(run('pageJobs().map(j=>j.id).join()')==='h2,h1,h0','history defaults newest first, unknown dates last');
check(!node('logs').classList.contains('hidden')&&node('dispatchTools').classList.contains('hidden'),'history owns log-folder button and hides dispatch');
check(node('queueActions').parentNode===node('queueFooterSlot')&&node('queueTop').classList.contains('hidden')&&node('queueBottom').classList.contains('hidden')&&!node('historyActions').classList.contains('hidden'),'history management buttons stay in footer and requeue stays above');
node('queueHead').onclick({target:{closest(){return {dataset:{historySort:'name'}};}}});check(run('pageJobs()[0].id')==='h2','history name ascending');
node('queueHead').onclick({target:{closest(){return {dataset:{historySort:'name'}};}}});check(run('pageJobs()[0].id')==='h1','history name descending');
run("toggleQueue('h1');toggleQueue('h2')");check(messages.at(-1).id==='h2','history multiple selection uses last selected log');
node('requeueHistory').onclick();check(messages.at(-1).action==='requeueHistory'&&messages.at(-1).paths.length===2,'history requeue submits selected records');
listeners.message({data:{type:'requeued',count:2,ids:['n1','n2']}});check(run("pendingQueueSelection.has('n1')&&pendingQueueSelection.has('n2')")&&node('queueTab').classList.contains('task-impact')&&node('queueCount').textContent==='3','requeued tasks count from the current total and queue tab reacts');
jobs.find(j=>j.id==='h1').requeued=true;listeners.message({data:{type:'queue',paused:true,jobs:jobs.concat([{id:'n1',name:'N1',state:'等待',project:'C:/n1.aep'},{id:'n2',name:'N2',state:'等待',project:'C:/n2.aep'}])}});check(run("queueSelections.queue.has('n1')&&queueSelections.queue.has('n2')")&&node('queueList').innerHTML.includes('data-state="requeued"'),'history remains and shows requeued status');
node('queueDeleteSelected').onclick();check(messages.at(-1).action==='deleteQueue'&&messages.at(-1).page==='history'&&!messages.at(-1).all,'history deletes scoped selected records');
node('queueClear').onclick();node('confirmYes').onclick();check(messages.at(-1).all&&messages.at(-1).page==='history','history clear cannot clear live queue');
run("queueTab('queue')");check(run('pageJobs().map(j=>j.id).join()')==='w1,r,w2,n1,n2','history sorting does not reorder queue');
check(!html.includes('id="retry"')&&!html.includes('id="cancel"')&&!html.includes('id="remove"')&&!html.includes('id="moveUp"'),'obsolete manual actions removed');
const queueRow=id=>({dataset:{id},classList:{add(){}},getBoundingClientRect(){return {top:0,height:40};}});
const sourceRow=queueRow('w2'),targetRow=queueRow('w1'),transfer={setData(){}};
node('queueList').ondragstart({target:{closest(){return sourceRow;}},dataTransfer:transfer,preventDefault(){}});
check(run('queueDrag.ids[0]')==='w2','drag starts with only the unselected dragged job');
node('queueList').ondrop({target:{closest(){return targetRow;}},clientY:5,preventDefault(){}});
check(messages.at(-1).action==='reorderQueue'&&messages.at(-1).target==='w1'&&messages.at(-1).position==='before','drop submits insertion target rather than adjacent swap');
check(run('queueDrag')===null,'drop releases drag state');
run("queueTab('history');historySort='state';historyDescending=false");const asc=run('pageJobs().map(j=>j.id).join()');run('historyDescending=true');check(run('pageJobs().map(j=>j.id).join()')!==asc,'history state supports both sort directions');
run("historySort='finishedOrder';historyDescending=false");check(run('pageJobs().map(j=>j.id).join()')==='h1,h2,h0','history time ascending keeps unknown timestamps last');
run("queueTab('queue');state.settings.ConfirmClearQueue=true");
const beforeConfirm=messages.length;node('queueClear').onclick();
check(node('confirmModal').classList.contains('open')&&messages.length===beforeConfirm,'clear opens app modal without executing');
check(!node('confirmRemember').classList.contains('hidden'),'queue clear shows do-not-ask checkbox');
node('skipClearPrompt').checked=true;node('skipClearPrompt').onchange();
check(messages.at(-1).action==='clearQueuePrompt'&&messages.at(-1).value===false&&!node('ConfirmClearQueue').checked,'do-not-ask immediately persists inverse advanced option');
const beforeCancel=messages.length;node('confirmNo').onclick();check(messages.length===beforeCancel,'cancel never deletes queue');
node('queueClear').onclick();check(messages.at(-1).action==='deleteQueue'&&!node('confirmModal').classList.contains('open'),'disabled clear prompt bypasses only queue clear confirmation');
run('openSettings()');node('ConfirmClearQueue').checked=true;node('saveSettings').onclick();check(messages.at(-1).settings.ConfirmClearQueue===true,'advanced option included in saved settings');
listeners.message({data:{type:'clearQueuePrompt',value:true}});check(!node('skipClearPrompt').checked&&node('ConfirmClearQueue').checked,'server preference acknowledgement keeps both controls in sync');
run("queueTab('history')");node('queueClear').onclick();check(node('confirmModal').classList.contains('open')&&!node('confirmRemember').classList.contains('hidden'),'history confirmation offers independent preference checkbox');node('confirmNo').onclick();
listeners.message({data:{type:'confirmClose'}});check(node('confirmTitle').textContent==='退出 AEP Render','exit uses same app confirmation');node('confirmNo').onclick();check(messages.at(-1).action==='closeAnswer'&&!messages.at(-1).confirmed,'exit cancellation returns to host');
check(!html.includes('window.confirm(')&&!html.includes('alert('),'no browser-native prompt calls remain');
check(html.indexOf('id="terminateAll"')<html.indexOf('id="renderSelected"')&&!html.includes('id="pauseQueue"'),'render selected replaces separate pause button');
const sortButtons=['name','versionNumber','modifiedOrder'].map(k=>{const b=node('sort-'+k);b.dataset.sort=k;return b;});
const priorQueryAll=document.querySelectorAll;document.querySelectorAll=s=>s==='[data-sort]'?sortButtons:priorQueryAll(s);
run("sort='name';descending=false;paintFileSort()");
check(sortButtons[0].innerHTML.includes('↑')&&sortButtons[0].classList.contains('sort-active'),'main name ascending arrow and active color');
run('descending=true;paintFileSort()');check(sortButtons[0].innerHTML.includes('↓'),'main name descending arrow');
run("sort='versionNumber';descending=false;paintFileSort()");check(sortButtons[1].innerHTML.includes('↑')&&sortButtons[0].innerHTML==='名称','sort indicator appears only on the active column');
run("sort='modifiedOrder';descending=true;paintFileSort()");check(sortButtons[2].innerHTML.includes('↓')&&sortButtons[2].getAttribute('aria-label').includes('降序'),'time arrow and accessible state agree');
check(run("sortPresentation('name','finishedOrder',false,'名称').html")==='名称','inactive history header uses text only');
run('state.settings.AllVersions=true;state.files='+JSON.stringify([{path:'late',name:'late',fileName:'late.aep',versionNumber:1,modified:'2026-09-14 12:00',modifiedOrder:200,selected:false},{path:'early',name:'early',fileName:'early.aep',versionNumber:1,modified:'2026-09-14 12:00',modifiedOrder:100,selected:false}])+';descending=false;renderFiles()');
check(run('visible[0].path')==='early','main date sorting uses true timestamp within same display minute');
run('state.settings.AllVersions=false;state.settings.RecursiveFolders=true;state.files='+JSON.stringify([{path:'C:/root/shot_t01.aep',name:'shot_t01',fileName:'shot_t01.aep',group:'shot',versionNumber:1,modifiedOrder:100,selected:false},{path:'C:/root/nested/deep/shot_t05.aep',name:'shot_t05',fileName:'shot_t05.aep',group:'shot',versionNumber:5,modifiedOrder:200,selected:false}])+';renderState(state)');
check(node('recursive').checked&&run("visible.length===1&&visible[0].path.includes('nested/deep')"),'folder traversal and latest-only filtering keep the newest nested version');
node('recursive').checked=false;node('recursive').onchange();check(messages.at(-1).action==='recursive'&&messages.at(-1).value===false,'folder traversal checkbox sends its disabled state with immediate elastic feedback');
run('state.settings.AllVersions=true;state.settings.RecursiveFolders=false;state.files='+JSON.stringify([{path:'late',name:'late',fileName:'late.aep',versionNumber:1,modified:'2026-09-14 12:00',modifiedOrder:200,selected:false},{path:'early',name:'early',fileName:'early.aep',versionNumber:1,modified:'2026-09-14 12:00',modifiedOrder:100,selected:false}])+';renderState(state)');
check(!node('filePanel').classList.contains('filter-remove-impact')&&run('fileCountMotion!==null'),'filter changes count without animating the whole panel');
node('recursive').checked=true;node('recursive').onchange();run('state.settings.RecursiveFolders=true;state.files.push({path:"C:/root/nested/new.aep",name:"new",fileName:"new.aep",group:"new",versionNumber:1,modifiedOrder:300,selected:false});renderState(state)');check(run('fileCountMotion!==null'),'enabling folder traversal uses elastic add feedback');run('state.settings.RecursiveFolders=false;state.files=state.files.filter(f=>f.path!=="C:/root/nested/new.aep");renderState(state)');
node('latest').checked=true;node('latest').onchange();run('state.settings.AllVersions=false;renderState(state)');check(run('fileCountMotion!==null'),'filter result reduction uses elastic remove feedback');run('state.settings.AllVersions=true;renderState(state)');
run("applyLocal(['early'],true)");check(!node('enqueue').disabled&&!node('clearAll').disabled,'local selection updates action buttons immediately');
run("applyLocal(['early','late'],true)");check(node('selectAll').disabled,'main select all disabled once all visible items selected');
run("applyLocal(['early','late'],false)");check(node('enqueue').disabled&&node('clearAll').disabled,'local deselection immediately disables empty actions');
run("queueTab('history')");check(node('historyTab').getAttribute('aria-selected')==='true'&&node('queueTab').tabIndex===-1,'queue tab semantics match settings tabs');
node('queueTabs').onkeydown({key:'ArrowLeft',preventDefault(){}});check(run('queuePage')==='queue','queue tabs support keyboard switching');
run("toggleQueue('w1')");node('log').textContent='previous';node('log').scrollTop=100;node('log').scrollHeight=1000;node('log').clientHeight=200;
listeners.message({data:{type:'log',id:'w1',content:'new lines'}});check(node('log').scrollTop===100,'log refresh preserves scroll while reading older lines');
node('log').scrollTop=800;listeners.message({data:{type:'log',id:'w1',content:'newer lines'}});check(node('log').scrollTop===1000,'log refresh follows tail only when already at bottom');
node('brandInfo').onclick();check(node('authorModal').classList.contains('open'),'brand left click opens author information');run("hide('authorModal')");
let prevented=false;node('brandInfo').oncontextmenu({preventDefault(){prevented=true;}});check(prevented&&node('authorModal').classList.contains('open'),'brand right click opens author information without context menu');run("hide('authorModal')");
let scrolled=false;node('engineGrid').scrollIntoView=()=>{scrolled=true;};node('engineShortcut').onclick();check(node('settingsModal').classList.contains('open')&&!node('advancedPane').classList.contains('hidden')&&scrolled,'engine shortcut opens Advanced and scrolls to engine cards');
node('brandInfo').onkeydown({key:'Enter',preventDefault(){}});check(node('authorModal').classList.contains('open'),'brand shortcut also supports keyboard activation');

check(!/id="(?:preset|savePreset|catalog|Composition|RenderTemplate|OutputTemplate|Extension|Start|End|Step|Overwrite|presetModal)"/.test(html),'composition/output controls removed completely');
check(html.includes('id="OutputFolder"')&&html.includes('id="browseOutput"'),'output folder and native browse retained');
run('fillOptions({OutputFolder:"C:\\\\output",Composition:"old",RenderTemplate:"old",OutputTemplate:"old",Start:"bad",Retries:2,RetryDelay:3,TimeoutMinutes:0})');
check(Object.keys(run('readOptions()')).sort().join(',')==='OutputFolder,Retries,RetryDelay,TimeoutMinutes','saving excludes removed legacy options');
check(fs.readFileSync(__dirname+'/Localization/strings.json','utf8').includes('3.8.8'),'version is 3.8.8');
const bootstrap=fs.readFileSync(__dirname+'/Bootstrap.cs','utf8');
check(bootstrap.includes('ActivateExisting()')&&bootstrap.includes('ShowWindowAsync(window,9)')&&bootstrap.includes('SetForegroundWindow(window)'),'second launch restores and foregrounds the existing main window');
check(!bootstrap.includes('UiText.Get("text.195")')&&!bootstrap.includes('--notice-test'),'duplicate-launch popup and its test entry are removed');
check(!html.includes('id="ShowRecent"'),'recent-folder visibility switch is removed from settings');
const filterResources=JSON.parse(fs.readFileSync(__dirname+'/Localization/strings.json','utf8'));check(filterResources['text.089'].zh==='仅最新版'&&filterResources['folder.recursive'].en==='Include subfolders'&&/id="recursive"(?![^>]*checked)/.test(html),'latest label is concise and folder traversal defaults off with three-language resources');
check(html.includes('padding:0 11px;width:200px;height:34px')&&html.includes('@media(max-width:1100px)')&&html.includes('.search{width:175px}'),'main search box is narrower at normal and compact widths');
run("state.folder='C:\\\\studio\\project\\shots';state.settings.FavoriteFolders=[];renderState(state)");
check(!node('toggleFavorite').classList.contains('active')&&node('toggleFavorite').getAttribute('aria-pressed')==='false'&&node('recents').innerHTML.includes('暂无收藏'),'empty favorites show an outlined star and empty message');
node('toggleFavorite').onclick();check(messages.at(-1).action==='toggleFavorite','address-bar star toggles the current folder through the native bridge');
run("state.settings.FavoriteFolders=['C:\\\\studio\\project\\shots'];renderState(state)");
const favoritePath='C:\\studio\\project\\shots';run('state.folder='+JSON.stringify(favoritePath)+';state.settings.FavoriteFolders=['+JSON.stringify(favoritePath)+'];renderState(state)');
check(node('toggleFavorite').classList.contains('active')&&node('toggleFavorite').getAttribute('aria-pressed')==='true'&&node('recents').innerHTML.includes('data-favorite'),'saved favorite fills the star and appears in the sidebar');
check(node('recents').innerHTML.includes('studio - project - shots'),'favorite label includes two parent folder levels separated by hyphens');
node('recents').onclick({target:{closest(){return {dataset:{favorite:'C:\\studio\\project\\shots'}};}}});check(messages.at(-1).action==='favorite','sidebar favorite opens its saved folder');
check(html.includes('.recent{width:100%;display:flex;align-items:center;gap:7px')&&html.includes('padding:7px 8px')&&html.includes('font-size:10px'),'favorite rows use smaller type and reduced horizontal padding');
check(script.includes('stripHoverTips')&&!script.includes("$('startQueue').title"),'hover tooltips are removed while accessible labels remain');
check(/id="authorModal"[\s\S]*?<footer class="modal-footer"><span class="author-version" data-i18n="text\.148"><\/span><button/.test(html),'author version is placed at the lower left of the close-button row');
check(/id="authorModal"[\s\S]*?<header class="modal-head"><div class="author-identity"><div class="brandmark"[\s\S]*?<h2 id="authorTitle" data-i18n="brand\.name"><\/h2><\/div>/.test(html)&&!html.includes('data-i18n="text.146"'),'author icon and product name replace the old author heading');
check(html.includes('id="authorModal"><section class="modal" style="width:540px"')&&html.includes('#authorModal .modal-body{text-align:center')&&html.includes('#authorModal .modal-head{position:relative;justify-content:flex-start'),'author window is wider and centers its heading and information');
check(html.includes('.brand{padding:7px 8px;gap:9px;margin:-7px 0 29px}'),'sidebar brand hover area leaves padding around the icon');
check(html.includes('#filePanel.cards .table-head{display:grid}'),'card view keeps the sortable and resizable project header');
check(html.includes('.modal-resize.nw')&&html.includes('.modal-resize.ne')&&html.includes('.modal-resize.sw')&&html.includes('.modal-resize.se')&&html.includes('beginModalGesture')&&html.includes('@keyframes task-impact')&&html.includes('animateAddedCount'),'secondary windows move and resize at four corners; elastic counting animation is defined');

run("queueTab('queue');renderQueue({...queueState,paused:false,selectedMode:false})");
check(node('startQueue').textContent==='自主渲染'&&node('startQueue').classList.contains('autonomous'),'autonomous state has glowing border');node('startQueue').onclick();check(messages.at(-1).action==='pauseQueue','active autonomous button pauses dispatch');
run("renderQueue({...queueState,paused:true,selectedMode:false});queueSelected.clear();toggleQueue('w1')");
check(node('startQueue').textContent==='暂停派发'&&!node('startQueue').classList.contains('autonomous'),'paused state removes glow');node('startQueue').onclick();check(messages.at(-1).action==='startQueue','paused autonomous button resumes dispatch');node('renderSelected').onclick();check(messages.at(-1).action==='renderSelected'&&messages.at(-1).paths.join()==='w1','selected render sends only selected waiting task IDs');
run("queueTab('history');state.settings.ConfirmClearHistory=true");node('queueClear').onclick();node('skipClearPrompt').checked=true;node('skipClearPrompt').onchange();check(messages.at(-1).action==='clearHistoryPrompt'&&!node('ConfirmClearHistory').checked&&node('ConfirmClearQueue').checked,'history preference independent from queue preference');node('confirmNo').onclick();node('queueClear').onclick();check(messages.at(-1).action==='deleteQueue'&&messages.at(-1).page==='history','disabled history prompt skips confirmation');
listeners.message({data:{type:'clearHistoryPrompt',value:true}});check(node('ConfirmClearHistory').checked,'history preference acknowledgement synchronizes settings');
check(run("formatDuration({state:'完成',renderSeconds:3661.1})")==='1时 1分 2秒'&&run("formatDuration({state:'失败',renderSeconds:10})")==='—'&&run("formatDuration({state:'完成'})")==='—','successful durations format correctly; failure and legacy duration unknown');
check(node('queueHead').innerHTML.includes('渲染用时'),'duration column follows finish time');
run("renderQueue({paused:true,jobs:[]})");check(node('queueSummary').innerHTML.includes('failed-count')&&!node('queueSummary').innerHTML.includes('has-fail')&&!node('queueSummary').innerHTML.includes('完成/失败'),'zero failures use the normal count color');
run("renderQueue({paused:true,jobs:[{id:'failed-color',name:'Failed',state:C('text.030'),active:false}]})");check(node('queueSummary').innerHTML.includes('failed-count has-fail'),'positive failures use the red count color');
check(!html.includes('id="queueHint"')&&html.indexOf('id="queueTop"')>html.indexOf('class="queue-footer-actions"'),'six queue actions moved to footer and old hint removed');
const resources=JSON.parse(fs.readFileSync(__dirname+'/Localization/strings.json','utf8'));
check(resources['text.080'].ja==='AEP バッチレンダリング'&&resources['text.081'].ja==='バッチレンダー','Japanese product name uses katakana batch wording');
check(Object.values(resources).every(r=>['zh','en','ja'].every(lang=>typeof r[lang]==='string'&&r[lang].length>0)),'all resource keys have three complete language values');
check(resources['text.147'].zh==='本软件开源免费发布\n作者：yuzhou-2023@foxmail.com（Copyright (C) 2026）\n使用和修改请遵守协议：GPL-3.0-only\nhttps://www.gnu.org/licenses/gpl-3.0.html'&&resources['text.147'].en==='This software is open source and distributed free of charge\nCopyright (C) 2026 yuzhou-2023@foxmail.com\nGNU GPL v3 only (GPL-3.0-only)\nhttps://www.gnu.org/licenses/gpl-3.0.html'&&resources['text.147'].ja.startsWith('本ソフトウェアはオープンソースとして無償公開されています\n作者：'),'author copyright, free open-source notice and license text are localized in Chinese, English and Japanese');
check(!html.includes('@keyframes filter-add-impact')&&script.includes('animateFileRows')&&script.includes('animateFileCount'),'latest and folder traversal changes use distinct elastic add and remove feedback');
check(run("fileMotionTranslation(20,false)")==='translateY(20px)'&&run("fileMotionTranslation(-20,true)")==='translateX(-20px)','list blocks move vertically and card blocks move horizontally');
const block=run('renderProjectBlock('+JSON.stringify({path:'C:/<test>.aep',name:'<project>',fileName:'test.aep',version:'t01',modified:'2026',selected:true})+')');
check((block.match(/class="file-row/g)||[]).length===1&&block.includes('&lt;project&gt;')&&block.includes('class="version"')&&block.includes('class="modified"'),'one reusable project block contains all escaped item content');
for(const lang of ['en','ja','zh']){run('setLanguage('+JSON.stringify(lang)+')');check(run("stateKey(C('text.029'))")==='completed','localized UI preserves completed state key: '+lang);check(run("stateLabel(C('text.029'))")===resources['text.029'][lang],'task states localized: '+lang);check(run("t('files.count',12)")===resources['files.count'][lang].replace('{0}','12'),'count template substitutes safely: '+lang);}
run("state.settings.Language='zh';openSettings()");node('Language').value='en';node('Language').onchange();check(run('activeLanguage')==='en'&&messages.at(-1).settings.Language==='en','language preview updates UI and native draft');run("hide('settingsModal')");check(run('activeLanguage')==='zh','cancel restores saved language');
check(run("localizedMessage('C:/项目/完成.aep')")==='C:/项目/完成.aep','translation preserves project paths');
console.log('SUCCESS: '+checks+' UI unit checks (no browser rendered)');

run("openSettings();settingsTab('instructions')");
check(node('instructionsTab').getAttribute('aria-selected')==='true'&&!node('instructionsPane').classList.contains('hidden'),'instructions is the third settings tab');
check(node('generalPane').classList.contains('hidden')&&node('advancedPane').classList.contains('hidden'),'only the selected settings pane is visible');
for(const key of ['ArrowRight','End','ArrowLeft','Home']){
 node('.settings-tabs').onkeydown({key,preventDefault(){}});
 const expected={ArrowRight:'general',End:'instructions',ArrowLeft:'advanced',Home:'general'}[key];
 check(node(expected+'Tab').getAttribute('aria-selected')==='true','three-tab keyboard navigation '+key);
}
for(const lang of ['zh','en','ja']){
 run('setLanguage('+JSON.stringify(lang)+');renderInstructions()');
 check(node('guideQrImage').classList.contains('hidden')&&!node('guideQrPlaceholder').classList.contains('hidden'),'unconfigured QR has a clear placeholder '+lang);
 check(node('guideQrCaption').textContent===resources['instructions.pending'][lang],'QR placeholder is localized '+lang);
}
run("INSTRUCTION_GUIDES.ja.qrDataUrl='https://invalid.example/qr.png';renderInstructions()");
check(node('guideQrImage').classList.contains('hidden'),'external image is not fetched');
run("INSTRUCTION_GUIDES.ja.qrDataUrl='data:image/png;base64,aGVsbG8=';renderInstructions()");
check(!node('guideQrImage').classList.contains('hidden')&&node('guideQrPlaceholder').classList.contains('hidden'),'embedded replacement image is selected');
node('guideQrImage').onerror();check(node('guideQrImage').classList.contains('hidden')&&!node('guideQrPlaceholder').classList.contains('hidden'),'invalid image restores the placeholder');
console.log('SUCCESS: '+checks+' UI unit checks including Instructions');
