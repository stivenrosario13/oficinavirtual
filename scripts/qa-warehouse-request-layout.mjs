import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import {readFileSync,writeFileSync} from 'node:fs';
import {createServer} from 'vite';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const baseline=process.argv.includes('--baseline');
const server=await createServer({configFile:false,resolve:{alias:{'@':resolve('src')}},optimizeDeps:{entries:[],noDiscovery:true,include:['react','react/jsx-runtime','react/jsx-dev-runtime','react-dom','react-dom/client','lucide-react']},esbuild:{jsx:'automatic'},server:{host:'127.0.0.1',port:5204,strictPort:true,watch:null}});
await server.listen();
const browser=await chromium.launch({headless:true,channel:'msedge'});
const css=[...readFileSync('src/main.tsx','utf8').matchAll(/import "(\.\/[^"\n]+\.css)"/g)].map(m=>`<link rel="stylesheet" href="/src/${m[1].slice(2)}">`).join('');
const rows=['pending','received','approved'].map((status,i)=>({id:i+1,title:`Solicitud QA ${i+1}`,status,receivedUtc:i?'2026-10-27T12:00:00Z':null,isRecipient:true,area:'communications',channel:'requirements',contentType:'application/pdf',originalFileName:`SOL-ALM-QA-${i+1}.pdf`,createdUtc:'2026-10-27T11:00:00Z',size:1024,department:'Supervisión',canAuthorizeWarehouseRequest:true,recipientDepartment:'Almacén',ownerUserName:'remitente',ownerDisplayName:'Remitente QA',warehouseRequest:{number:`SOL-ALM-QA-${i+1}`,lines:[{kind:'EQUIPMENT',name:'Monitor',quantity:1}]},warehouseAssigned:[],sharedWith:[],history:[]}));
const report=[];
try{
 for(const width of [1366,390]){
  const page=await browser.newPage({viewport:{width,height:1000}});const errors=[],requests=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/*',r=>{
   const path=new URL(r.request().url()).pathname;
   if(path==='/qa')return r.fulfill({contentType:'text/html',body:`<html><head><meta name="viewport" content="width=device-width,initial-scale=1">${css}</head><body><div id="root"></div></body></html>`});
   if(path.startsWith('/api/')){requests.push(path);assert.equal(path,'/api/institutional-documents');return r.fulfill({json:rows});}
   return r.continue();
  });
  await page.goto('http://127.0.0.1:5204/qa');
  await page.evaluate(async(width)=>{
   const React=(await import('/node_modules/.vite/deps/react.js')).default;
   const dom=await import('/node_modules/.vite/deps/react-dom_client.js');
   await import('/src/components/vite/maintenanceModern.css');
   const {default:Component}=await import('/src/components/vite/WarehousePdfRequests.tsx');
   const session={authenticated:true,id:'qa',email:'qa@test.invalid',role:'Warehouse',supportTeam:'WAREHOUSE',permissions:{canAuthorizeWarehouseRequests:true},groups:[]};
   (dom.createRoot||dom.default.createRoot)(document.getElementById('root')).render(React.createElement('main',{className:'ops-modern',style:{padding:width>760?'24px':'10px'}},React.createElement(Component,{session,embedded:true,department:'SUPERVISION',warehouseAccess:true})));
  },width);
  await page.getByText('Solicitud QA 1',{exact:true}).waitFor();
  const layout=await page.evaluate(()=>{
   const style=s=>getComputedStyle(document.querySelector(s));
   return {catalogDisplay:style('.mc-catalog-layout').display,summaryDisplay:style('.mc-summary').display,asideOrder:style('.mc-catalog-layout>aside').order,horizontalOverflow:document.documentElement.scrollWidth>innerWidth+1,styles:[...document.querySelectorAll('style[data-vite-dev-id]')].map(e=>e.dataset.viteDevId).filter(p=>/maintenance(Communications|Workflow)\.css$/.test(p))};
  });
  await page.screenshot({path:`outputs/warehouse-requests-${baseline?'before':'after'}-${width}.png`,fullPage:true});
  if(!baseline){
   assert.equal(layout.catalogDisplay,width>760?'grid':'flex');assert.equal(layout.summaryDisplay,'grid');assert.equal(layout.horizontalOverflow,false);assert.equal(layout.styles.length,2);
   if(width<760)assert.equal(layout.asideOrder,'-1');
   for(const [filter,title,action] of [['Pendientes de recibir','Solicitud QA 1','Recibir'],['Pendientes de autorizar','Solicitud QA 2','Autorizar'],['Autorizadas','Solicitud QA 3','Asignar equipo y serial']]){
    await page.locator('.mc-side-panel button').filter({hasText:filter}).click();
    assert.equal(await page.locator('.warehouse-pdf-list>article').count(),1);
    await page.getByText(title,{exact:true}).waitFor();await page.getByRole('button',{name:action,exact:true}).waitFor();
   }
   await page.getByRole('button',{name:'Vista de lista',exact:true}).click();assert.equal(await page.locator('.warehouse-pdf-list.list').count(),1);
   await page.getByRole('button',{name:'Vista de tarjetas',exact:true}).click();
   await page.getByPlaceholder('Buscar formulario, agencia o remitente…').fill('inexistente');await page.getByText('No hay solicitudes de equipo con estos filtros').waitFor();
   assert.equal(requests.length,1);assert.deepEqual(errors,[]);
  }
  report.push({width,...layout,errors});await page.close();
 }
 writeFileSync(`outputs/warehouse-requests-${baseline?'before':'after'}.json`,JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await server.close();}
