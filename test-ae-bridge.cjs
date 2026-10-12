// Runs JSX against in-memory ExtendScript substitutes, not real After Effects.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const source=fs.readFileSync(__dirname+'/AE联动示例.jsx','utf8');
for(const saved of [true,false]){
 const requests=[],alerts=[],files=new Map();
 function File(path){return {exists:true,open(){return true;},close(){},write(s){files.set(path,s);},read(){return JSON.stringify({ok:true,data:{added:1,paused:true}});},rename(name){assert.match(name,/^[a-f0-9]{32}\.json$/);requests.push(JSON.parse(files.get(path)));return true;}};}
 const projectPath='C:\\项目\\shot "a".aep';
 vm.runInNewContext(source,{File,Folder(){return {exists:true};},$:{getenv(){return 'C:/Local';},sleep(){}},app:{project:{file:saved?{fsName:projectPath}:null}},alert(s){alerts.push(s);}});
 if(saved){assert.equal(requests.length,1);assert.equal(requests[0].action,'enqueue');assert.deepEqual(requests[0].paths,[projectPath]);assert.ok(Date.parse(requests[0].expiresUtc)>Date.now());assert.match(alerts[0],/加入队列请求已处理/);}
 else {assert.equal(requests.length,0);assert.match(alerts[0],/请先保存/);}
 console.log('PASS: '+(saved?'single enqueue only; escaped project path and valid expiry':'unsaved project rejected without requests'));
}
console.log('SUCCESS: JSX simulated bridge checks; no AE launched');
