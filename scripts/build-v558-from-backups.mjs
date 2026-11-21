import fs from 'node:fs';
// Historical recovery builder. Stable packaging from a clean clone uses package-monster-stable.ps1.
import path from 'node:path';
import {parse} from '@babel/parser';
import generateModule from '@babel/generator';
const generate=generateModule.default;
const root=process.cwd(),out=path.join(root,'outputs');
const base=path.join(out,'_stage_v544_exact_original_design_20261119190000/wwwroot');
const original=path.join(out,'_stage_v539_20261117152000/wwwroot');
const latest=path.join(out,'_stage_v550_exact_design_20261120164104/wwwroot/wwwroot/assets');
const stamp=new Date().toISOString().replace(/\D/g,'').slice(0,14);
const stage=path.join(out,`_stage_v558_original_mime_fixed_${stamp}`),web=path.join(stage,'wwwroot');
fs.mkdirSync(stage,{recursive:true});fs.cpSync(base,web,{recursive:true});
const assets=path.join(web,'assets');
const read=(p)=>fs.readFileSync(p,'utf8');
const forms=['Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja'];
const canonical='index-ORIGINAL-V558.js';
const main=read(path.join(assets,'index-V545FORMS.js'));
if(main.includes('portal-global-search')||main.includes('portal-launcher')||!main.includes('header-logout'))throw Error('La base no contiene la interfaz original');
fs.writeFileSync(path.join(assets,canonical),main);
// Restore the current warehouse behavior while retaining the original UI graph.
const iconName='icons-BP13l6N_.js';
function iconExports(file){
 const ast=parse(read(file),{sourceType:'module'});
 const ex=ast.program.body.find(n=>n.type==='ExportNamedDeclaration');
 return new Map(ex.specifiers.map(s=>[s.exported.name,s.local.name]));
}
const oldIcons=iconExports(path.join(assets,iconName)),newIcons=iconExports(path.join(latest,'icons-CcERqIk_.js'));
const oldNames=new Map([...oldIcons].map(([exported,local])=>[local,exported]));
const warehouseName=fs.readdirSync(latest).find(n=>/^WarehouseRequestComposer-.*\.js$/.test(n));
const warehouseSource=read(path.join(latest,warehouseName));
const warehouse=parse(warehouseSource,{sourceType:'module'});
const oldWarehouseName='WarehouseRequestComposer-DjKr8DjI.js';
const importPaths={
 'index-CnvsZ8bt.js':canonical,
 'icons-CcERqIk_.js':iconName,
 'warehouseRequestPdf-CB84b3gc.js':'warehouseRequestPdf-BZxRTl2W.js',
 'SearchSelect-ozy2Qlxp.js':'SearchSelect-6Xu_t35g.js',
 'voice-operation-assistant-DmkzAcS5.js':'voice-operation-assistant-DmkzAcS5.js',
 'react-2B_n6cjE.js':'react-Bt2JFbsy.js',
};
for(const node of warehouse.program.body.filter(n=>n.type==='ImportDeclaration')){
 const key=path.basename(node.source.value.split('?')[0]);
 if(!importPaths[key])throw Error('Dependencia sin correspondencia: '+key);
 if(key.startsWith('icons-'))for(const s of node.specifiers){
  const localSymbol=newIcons.get(s.imported.name),mapped=oldNames.get(localSymbol);
  if(!mapped)throw Error('Icono no encontrado: '+s.imported.name);
  s.imported.name=mapped;
 }
 node.source.value='./'+importPaths[key];
}
const generated=generate(warehouse,{compact:true,comments:false}).code;
// Check every non-import node: warehouse logic is identical to V550.
const before=parse(warehouseSource,{sourceType:'module'}).program.body.filter(n=>n.type!=='ImportDeclaration');
const after=parse(generated,{sourceType:'module'}).program.body.filter(n=>n.type!=='ImportDeclaration');
if(generate({type:'Program',body:before},{compact:true,comments:false}).code!==generate({type:'Program',body:after},{compact:true,comments:false}).code)throw Error('Se alteró la lógica de Almacén');
fs.writeFileSync(path.join(assets,oldWarehouseName),generated);
// All dependencies must share one application root; the backup contained aliases.
for(const name of fs.readdirSync(assets).filter(n=>n.endsWith('.js'))){
 let text=read(path.join(assets,name));
 text=text.replace(/index-(?:Bdb8z7xx|V544FORMS|V545FORMS)\.js/g,canonical);
 fs.writeFileSync(path.join(assets,name),text);
}
const release='V558-ORIGINAL-MIME-CORREGIDO-'+stamp;
// Version the complete module graph: old immutable cached modules must not remount old roots.
for(const name of fs.readdirSync(assets).filter(n=>n.endsWith('.js'))){
 let text=read(path.join(assets,name)).replace(/(["'])([^"'\s]+\.(?:js|css))(?:\?[^"']*)?\1/g,(_match,q,url)=>q+url+'?v='+release+q);
 // Vite's preload helper must identify the pathname, including versioned CSS URLs.
 text=text.replace(/([\w$]+)\.endsWith\((['"])\.css\2\)/g,(_match,variable)=>`/\\.css(?:[?#]|$)/.test(${variable})`);
 if(/\.endsWith\((['"])\.css\1\)/.test(text))throw Error('Clasificador CSS sin corregir: '+name);
 fs.writeFileSync(path.join(assets,name),text);
}
// Preserve report enhancements and module search without changing the reference header.
const enhancementsPath=path.join(web,'v520-targeted-enhancements.js');
let enhancements=read(enhancementsPath);
if(!enhancements.includes('function installSearch(){'))throw Error('No se encontró el instalador del buscador');
enhancements=enhancements.replace('function installSearch(){','function installSearch(){\n    if(!window.__referenceSearchRequested)return;');
enhancements=enhancements.replace('  var observer=new MutationObserver',`  document.addEventListener('keydown',function(event){if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='k'){event.preventDefault();window.__referenceSearchRequested=true;installSearch();var input=document.querySelector('.v520-header-search input');if(input)input.focus();}});\n  var observer=new MutationObserver`);
fs.writeFileSync(enhancementsPath,enhancements);
let html=read(path.join(web,'index.html'));
html=html.replace(/(<meta name="app-release" content=")[^"]+/,'$1'+release)
 .replace(/index-V545FORMS\.js/g,canonical)
 .replace(/release-guard\.js\?v=[^"']+/,'release-guard.js?v='+release);
html=html.replace(/((?:src|href)="[^" ]+\.(?:js|css))(?:\?[^" ]*)?"/g,'$1?v='+release+'"');
fs.writeFileSync(path.join(web,'index.html'),html);
const version=JSON.parse(read(path.join(web,'version-web.json')).replace(/^\uFEFF/,''));
Object.assign(version,{release,cacheGuard:release,visualBase:'original-V539-V544',apiRelease:'backend-preservado',changes:['fix-css-preload-classification-with-version-query-v558','original-compiled-interface-no-reconstructed-css-preserve-three-forms-and-warehouse',...version.changes]});
fs.writeFileSync(path.join(web,'version-web.json'),JSON.stringify(version,null,2));
// CSS of the original interface is preserved byte for byte.
let styles=0;
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);}
for(const f of files(original).filter(f=>f.endsWith('.css'))){
 const rel=path.relative(original,f),target=path.join(web,rel);
 if(!fs.existsSync(target)||!fs.readFileSync(f).equals(fs.readFileSync(target)))throw Error('CSS original alterado: '+rel);styles++;
}
for(const f of files(web).filter(f=>f.endsWith('.css'))){
 if(!fs.readFileSync(f).equals(fs.readFileSync(path.join(base,path.relative(web,f)))))throw Error('CSS modificado');
}
for(const file of ['AdministrativeRequestComposer-V545FORMS.js','AdministrativeRequestComposer-V544FORMS.js'])for(const form of forms){
 if(!read(path.join(assets,file)).includes(form))throw Error('Falta formulario: '+form);
}
// Check every local static import, re-export and dynamic import.
let imports=0;
const preloadDependencies=new Set();
for(const f of files(assets).filter(f=>f.endsWith('.js'))){
 const ast=parse(read(f),{sourceType:'module'});
 function walk(n){if(!n||typeof n!=='object')return;
  if(n.type==='StringLiteral'&&n.value.startsWith('assets/')&&/\.(?:js|css)(?:\?|$)/.test(n.value)){
   if(!fs.existsSync(path.join(web,n.value.split('?')[0])))throw Error('Falta precarga: '+n.value);
   preloadDependencies.add(n.value);
  }
  const source=(n.type==='ImportDeclaration'||n.type==='ExportNamedDeclaration'||n.type==='ExportAllDeclaration')?n.source:(n.type==='CallExpression'&&n.callee?.type==='Import'?n.arguments[0]:null);
  if(source?.type==='StringLiteral'&&source.value.startsWith('.')){
   const target=path.resolve(path.dirname(f),source.value.split('?')[0]);
   if(!fs.existsSync(target))throw Error('Falta dependencia: '+source.value+' en '+path.basename(f));imports++;
  }
  for(const v of Object.values(n)){if(Array.isArray(v))v.forEach(walk);else if(v&&typeof v==='object')walk(v);}
 }walk(ast.program);
}
const result={stage,release,forms,styles,imports,main:canonical,preloadDependencies:[...preloadDependencies]};
fs.writeFileSync(path.join(out,'v558-package-result.json'),JSON.stringify(result,null,2));
fs.writeFileSync(path.join(stage,'INSTRUCCIONES_V558.txt'),`V558: corrige la clasificación MIME de CSS con parámetros de versión.\nInterfaz original y CSS originales preservados. Conserva tres formularios nuevos y lógica de Almacén V550. Backend y corrección de Darwin se conservan.\nDetener sitio, extraer sobre carpeta publicada confirmando reemplazos e iniciar.\n/version-web.json: ${release}\n`);
console.log(JSON.stringify({...result,preloadDependencies:preloadDependencies.size}));
