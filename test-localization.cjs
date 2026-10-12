const fs=require('fs'),path=require('path'),assert=require('assert');
const base=__dirname,catalog=JSON.parse(fs.readFileSync(path.join(base,'Localization/strings.json'),'utf8'));
for(const [key,row] of Object.entries(catalog)){
 for(const lang of ['zh','en','ja'])assert.ok(typeof row[lang]==='string'&&row[lang].length,key+' '+lang);
 const placeholders=s=>(s.match(/\{\d+\}/g)||[]).sort().join();
 assert.equal(placeholders(row.en),placeholders(row.zh),key+' English placeholders');assert.equal(placeholders(row.ja),placeholders(row.zh),key+' Japanese placeholders');
 if(!key.startsWith('language.'))assert.ok(!/[\u3400-\u9fff]/.test(row.en),'Chinese leaked into English: '+key);
}
const refs=[];for(const name of fs.readdirSync(base).filter(n=>n.endsWith('.cs')&&!n.startsWith('Tests')&&!n.startsWith('TestFixtures'))){let source=fs.readFileSync(path.join(base,name),'utf8');if(name==='WebForm.cs')source=source.split('private async Task UiTest')[0];
 for(const m of source.matchAll(/UiText\.(?:Source|Get|Format)\("([^"]+)"/g))refs.push(m[1]);
 assert.ok(!/"[^"\r\n]*[\u3400-\u9fff][^"\r\n]*"/.test(source),'hard-coded native UI text: '+name);
}
const html=fs.readFileSync(path.join(base,'ui.html'),'utf8');for(const m of html.matchAll(/(?:\b[ tC]\(|data-i18n(?:-[\w-]+)?=)["']([^"']+)["']/g))if(m[1].includes('.'))refs.push(m[1]);for(const key of refs)assert.ok(catalog[key],'Missing key: '+key);
console.log('PASS: '+Object.keys(catalog).length+' three-language entries; placeholders and references verified; native UI text centralized');
