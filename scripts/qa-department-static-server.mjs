import {createServer} from 'node:http';
import {readFile,stat} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
const roots=['tmp/inventory-agencies-preview','api/wwwroot'].map(path=>resolve(path));
const types={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.css':'text/css','.png':'image/png','.svg':'image/svg+xml','.json':'application/json','.woff2':'font/woff2'};
createServer(async(req,res)=>{
 const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
 if(pathname.startsWith('/api/')){res.writeHead(404);res.end();return}
 for(const root of roots){
  const file=resolve(root,pathname==='/'?'index.html':`.${pathname}`);
  if(!file.startsWith(root+sep))continue;
  try{if(!(await stat(file)).isFile())continue;res.writeHead(200,{'Content-Type':types[extname(file)]||'application/octet-stream','Cache-Control':'no-store'});res.end(await readFile(file));return}catch{}
 }
 res.writeHead(404);res.end();
}).listen(5201,'127.0.0.1',()=>console.log('QA static server http://127.0.0.1:5201'));
