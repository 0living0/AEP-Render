/*__STRINGS__*/
let activeLanguage='zh',previewLanguage=null,refreshingLanguage=false;
const normalizeLanguage=value=>['zh','en','ja'].includes(value)?value:'zh';
function C(key){return UI_STRINGS[key]?.zh??key;}
function t(key,...args){const value=UI_STRINGS[key]?.[activeLanguage]??C(key);return value.replace(/\{(\d+)\}/g,(all,n)=>args[n]===undefined?all:String(args[n]));}
function uiLocale(){return {zh:'zh-CN',en:'en-US',ja:'ja-JP'}[activeLanguage];}
const STATE_KEYS={waiting:'text.038',selectedPending:'queue.selectedPending',running:'text.201',retry:'text.202',cancelling:'text.203',cancelled:'status.cancelled',interrupted:'text.208',completed:'text.029',failed:'text.030'};
function stateKey(value){return Object.keys(STATE_KEYS).find(k=>C(STATE_KEYS[k])===value)||'unknown';}
function stateLabel(value){return STATE_KEYS[stateKey(value)]?t(STATE_KEYS[stateKey(value)]):value;}
function localizedMessage(value){
 const text=String(value??'');
 for(const [key,entry] of Object.entries(UI_STRINGS))if(Object.values(entry).includes(text))return t(key);
 // Only translate recognized message boundaries. Never replace text inside project paths or raw logs.
 for(const [key,entry] of Object.entries(UI_STRINGS))for(const source of Object.values(entry)){
  if(source.length<5||source.includes('<')||source.includes('{')||source===entry[activeLanguage])continue;
  if(/[:：]\s*$/.test(source)&&text.startsWith(source))return t(key)+text.slice(source.length);
  if((key==='text.224')&&text.endsWith(source))return text.slice(0,-source.length)+t(key);
 }
 return text;
}
function stripHoverTips(root=document){
 if(root.nodeType===1&&root.hasAttribute('title'))root.removeAttribute('title');
 root.querySelectorAll?.('[title],[data-i18n-title]').forEach(el=>{el.removeAttribute('title');el.removeAttribute('data-i18n-title');});
}
function applyStaticTranslations(){
 document.querySelectorAll('[data-i18n]').forEach(el=>el.textContent=t(el.dataset.i18n));
 for(const attr of ['placeholder','aria-label'])document.querySelectorAll('[data-i18n-'+attr+']').forEach(el=>el.setAttribute(attr,t(el.getAttribute('data-i18n-'+attr))));
 document.documentElement.lang=uiLocale();document.documentElement.setAttribute('lang',activeLanguage);document.title=t('text.080');
 stripHoverTips();
}
if(typeof MutationObserver!=='undefined')new MutationObserver(records=>records.forEach(record=>record.addedNodes.forEach(node=>stripHoverTips(node)))).observe(document.documentElement,{subtree:true,childList:true});
function setLanguage(value){const next=normalizeLanguage(value),changed=activeLanguage!==next;activeLanguage=next;if(changed){applyStaticTranslations();filesSignature='';}return changed;}
function refreshLanguage(value){setLanguage(value);$('toast').textContent=localizedMessage($('toast').textContent);refreshingLanguage=true;try{renderInstructions();if(state.folder!==undefined)renderState(state);else renderFiles();renderQueue(queueState);if($('settingsModal').classList.contains('open')){renderEngines();updateGeneral();}}finally{refreshingLanguage=false;}}
