const fs=require('fs'),vm=require('vm'),assert=require('assert');
const script=fs.readFileSync('OutputRedirect.cs','utf8').match(/\+ @"([\s\S]*?)";/)[1].replace(/""/g,'"');
let checks=0;
function check(ok,label){assert.ok(ok,label);console.log('PASS: '+label);checks++;}
function execute(items){
 let saved=[],closed=[],report='';
 function File(p){this.fsName=p;this.name=p.split('/').pop();this.open=()=>true;this.write=s=>report=s;this.close=()=>{};}
 function Folder(p){this.fsName=p;}
 const app={beginSuppressDialogs(){},endSuppressDialogs(){},open(f){this.opened=f.fsName;},project:{renderQueue:{numItems:items.length,item:i=>items[i-1]},save(f){saved.push(f.fsName);},close(v){closed.push(v);}}};
 vm.runInNewContext(script,{messages:["missing filename","no queue"],app,File,Folder,source:'C:/source.aep',destination:'D:/output 中文',copy:'C:/temporary/source.aep',result:'C:/temporary/result.txt',RQItemStatus:{QUEUED:1},CloseOptions:{DO_NOT_SAVE_CHANGES:0}});
 return {app,saved,closed,report};
}
function item(status,names){const modules=names.map(name=>({file:name===null?null:{name,fsName:'C:/old/'+name},format:'unchanged'}));return {status,numOutputModules:modules.length,outputModule:i=>modules[i-1],modules};}
const first=item(1,['movie.mov','sequence_[####].png']),second=item(1,['second.avi']),skipped=item(2,['done.mov']);
let result=execute([first,second,skipped]);
check(first.modules[0].file.fsName==='D:/output 中文/movie.mov','movie filename retained in new directory');
check(first.modules[1].file.fsName==='D:/output 中文/sequence_[####].png','sequence token retained');
check(second.modules[0].file.fsName==='D:/output 中文/second.avi','all queued items redirected');
check(skipped.modules[0].file.fsName==='C:/old/done.mov','nonqueued item unchanged');
check(first.modules.every(m=>m.format==='unchanged'),'output formats retained');
check(result.saved.join()==='C:/temporary/source.aep'&&result.closed[0]===0,'only temporary copy saved, source closed without saving');
check(result.report.startsWith('OK\n')&&result.report.split('\n').length===4,'all output module paths reported for collision check');
result=execute([skipped]);check(result.report.startsWith('ERROR\n')&&result.saved.length===0,'empty render queue fails without saving');
result=execute([item(1,[null])]);check(result.report.startsWith('ERROR\n')&&result.saved.length===0,'missing output filename fails without saving');
console.log('SUCCESS: '+checks+' output redirect script checks (simulated AE)');
