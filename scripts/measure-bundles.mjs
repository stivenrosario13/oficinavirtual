import {build} from 'vite';
import {writeFileSync,mkdirSync} from 'node:fs';
import {gzipSync} from 'node:zlib';
const label=process.argv[2];
if(!/^[a-z0-9-]+$/.test(label??''))throw new Error('Expected measurement label');
mkdirSync('outputs',{recursive:true});
await build({build:{outDir:`outputs/performance-${label}`,manifest:true},plugins:[{
 name:'measure-production-bundles',
 generateBundle(_,bundle){
  const chunks=Object.values(bundle).filter(item=>item.type==='chunk');
  const closure=name=>{const seen=new Set();const visit=id=>{if(seen.has(id)||!bundle[id]||bundle[id].type!=='chunk')return;seen.add(id);bundle[id].imports.forEach(visit)};visit(name);return [...seen]};
  const result=chunks.map(c=>({file:c.fileName,entry:c.isEntry,facade:c.facadeModuleId?.replaceAll('\\','/').split('/registro-agencias/').at(-1),bytes:Buffer.byteLength(c.code),gzip:gzipSync(c.code).length,staticClosure:closure(c.fileName),staticGzip:closure(c.fileName).reduce((sum,id)=>sum+gzipSync(bundle[id].code).length,0),modules:Object.entries(c.modules).map(([name,m])=>({name:name.replaceAll('\\','/').split('/registro-agencias/').at(-1),bytes:m.renderedLength})).sort((a,b)=>b.bytes-a.bytes)}));
  writeFileSync(`outputs/performance-${label}.json`,JSON.stringify(result,null,2));
 }
}]});
