import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
const reference=path.resolve('release-reference/v558'),web=path.join(reference,'wwwroot');
function walk(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);}
const files=walk(web).map(file=>({path:path.relative(web,file).replaceAll('\\','/'),sha256:createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase()})).sort((a,b)=>a.path.localeCompare(b.path));
if(process.argv.includes('--create-manifest')){
 const qa=JSON.parse(fs.readFileSync(path.join(reference,'qa.json'),'utf8'));
 qa.stage='release-reference/v558';
 fs.writeFileSync(path.join(reference,'qa.json'),JSON.stringify(qa,null,2)+'\n');
 fs.writeFileSync(path.join(reference,'manifest.json'),JSON.stringify({release:qa.release,files},null,2)+'\n');
}
const expected=JSON.parse(fs.readFileSync(path.join(reference,'manifest.json'),'utf8'));
if(JSON.stringify(expected.files)!==JSON.stringify(files))throw Error('La referencia estable fue modificada o está incompleta.');
for(const {path:relative} of files)if(/(^|\/)(web\.config|appsettings[^/]*|\.env[^/]*|app_offline\.htm)$|(^|\/)(Data|App_Data|uploads|logs)\/|\.(dll|exe)$/.test(relative))throw Error('Archivo protegido: '+relative);
console.log(JSON.stringify({release:expected.release,verifiedFiles:files.length}));
