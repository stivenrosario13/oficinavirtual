import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import {readFileSync,writeFileSync} from 'node:fs';
import {createServer} from 'vite';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const server=await createServer({configFile:false,resolve:{alias:{'@':resolve('src')}},optimizeDeps:{entries:[],noDiscovery:true,include:['react','react/jsx-runtime','react/jsx-dev-runtime','react-dom/client','lucide-react']},esbuild:{jsx:'automatic'},server:{host:'127.0.0.1',port:5203,strictPort:true,watch:null}});
await server.listen();
const browser=await chromium.launch({headless:true,channel:'msedge'});

const report=[];
const css=[...readFileSync('src/main.tsx','utf8').matchAll(/import "(\.\/[^"\n]+\.css)"/g)].map(m=>`<link rel="stylesheet" href="/src/${m[1].slice(2)}">`).join('');
try{
 for(const width of [390,1366]){
  const page=await browser.newPage({viewport:{width,height:950}}),errors=[],requests=[];
  page.on('pageerror',e=>errors.push(e.message));
  const make=(id)=>({id,ticketNumber:Number(id),codigo:'QA',terminal:'Agencia QA',grupo:'Grupo QA',assignedDepartment:'TECHNOLOGY',ticketType:'INTERNAL',assignedTeam:'CALL_CENTER',assignedTechnicianId:'callcenter',assignedTechnicianName:'Call Center QA',category:'OTHER',status:'IN_PROGRESS',priority:'MEDIUM',subject:`Caso QA ${id}`,description:'Caso de prueba',createdByName:'Call Center QA',createdByUsername:'cc@test.invalid',createdAt:'2026-10-27T12:00:00Z',updatedAt:'2026-10-27T12:00:00Z',history:[],evidence:[],isAutomatic:false});
  const tickets=[make('1'),make('2')];let catalogAttempts=0,technicianFails=false;
  const fixtures={
   '/api/tickets':()=>({tickets}),'/api/tickets/agencies':()=>({agencies:[{id:'a',codigo:'QA',terminal:'Agencia QA',grupo:'Grupo QA',region:'Sur'}]}),
   '/api/tickets/technicians':()=>({technicians:[{id:'supervisor',name:'Supervisor QA',role:'GroupAdministrator',department:'',isAgencySupervisor:true,isTechnician:false,groups:['Grupo QA']}]}),
   '/api/tickets/findings':()=>({findings:[]}),'/api/support/catalog':()=>({departments:[{code:'TECHNOLOGY',name:'Tecnología',description:'Soporte',categories:[],isActive:true}]})
  };
  await page.route('**/*',async route=>{
   const path=new URL(route.request().url()).pathname;
   if(path==='/qa')return route.fulfill({contentType:'text/html',body:`<html><head><meta name="viewport" content="width=device-width, initial-scale=1">${css}</head><body><div id="root"></div></body></html>`});
   if(path.startsWith('/api/')){
    requests.push(path);
    if(path==='/api/tickets/technicians' && technicianFails)return route.fulfill({status:500,json:{error:'Directorio temporalmente no disponible QA'}});
    if(path==='/api/support/catalog' && catalogAttempts++===0)return route.fulfill({status:503,json:{error:'Temporal QA'}});
    const match=path.match(/^\/api\/tickets\/(\d+)\/route$/);
    if(match){const data=route.request().postDataJSON();const ticket=tickets.find(t=>t.id===match[1]);assert.equal(data.assignedDepartment,'TECHNOLOGY');ticket.assignedTeam=data.assignedTeam;ticket.assignedTechnicianId=data.assignedTechnicianId;ticket.assignedTechnicianName=data.assignedTechnicianId?'Supervisor QA':null;ticket.status=data.assignedTechnicianId?'IN_PROGRESS':'OPEN';ticket.history.push({id:crypto.randomUUID(),action:data.assignedTechnicianId?'ESCALATED_TO_SUPERVISOR':'ESCALATED',fromTeam:'CALL_CENTER',toTeam:data.assignedTeam,actorName:'Call Center QA',createdAt:new Date().toISOString()});return route.fulfill({json:{ok:true}});}
    if(!fixtures[path])return route.fulfill({status:404,json:{error:'Unexpected '+path}});
    return route.fulfill({json:fixtures[path]()});
   }
   return route.continue();
  });
  await page.goto('http://127.0.0.1:5203/qa');
  await page.evaluate(async()=>{
   const React=(await import('/node_modules/.vite/deps/react.js')).default;
   const dom=await import('/node_modules/.vite/deps/react-dom_client.js');const createRoot=dom.createRoot||dom.default.createRoot;
   const {permissions}=(await import('/src/lib/user-access-profiles.ts')).userAccessProfile('SUPPORT','CALL_CENTER');
   const session={authenticated:true,id:'callcenter',email:'cc@test.invalid',displayName:'Call Center QA',role:'Technology',permissions,mustChangePassword:false,scope:'all',supportDepartment:'TECHNOLOGY',supportTeam:'CALL_CENTER',groups:[]};
   const Component=(await import('/src/components/vite/TicketCenter.tsx')).default;
   createRoot(document.getElementById('root')).render(React.createElement(Component,{session,departmentEntry:{key:1,department:'TECHNOLOGY',section:'support'},onSessionExpired(){throw Error('Unexpected logout')}}));
  });
  await page.getByRole('button',{name:/Sur/}).click();
  await page.getByRole('navigation',{name:'Opciones de Soporte'}).getByRole('button',{name:/RECEPCIÓN Y CIERRE/}).click();
  
  await page.getByRole('button',{name:/En proceso/}).click();
  for(const id of ['1','2']){
   const card=page.locator(`[data-ticket-id="${id}"]`);
   await card.waitFor();
   const expand=card.getByRole('button',{name:`Ver ticket #${id} completo`,exact:true});if(await expand.isVisible())await expand.click();
   await card.getByRole('button',{name:'Compartir y escalar',exact:true}).click();
   if(id==='1'){
    await card.getByLabel('Supervisor de destino').selectOption('supervisor');
    await card.getByRole('button',{name:'Escalar a supervisor',exact:true}).click();
   }else await card.getByRole('button',{name:/Compartir y escalar a Avería/}).click();
   await card.waitFor({state:'detached'});
  }
  await page.getByRole('button',{name:/Volver al centro de soporte/}).click();
  await page.getByRole('button',{name:/Tickets transferidos/}).click();
  const destinations=page.getByRole('navigation',{name:'Destinos de tickets transferidos'});
  assert.equal(await destinations.getByRole('button').count(),2);
  for(const [destination,id] of [['supervisores','1'],['Avería Técnica','2']]){
   await destinations.getByRole('button',{name:new RegExp(`Transferidos a ${destination}`)}).click();
   const card=page.locator(`[data-ticket-id="${id}"]`);await card.waitFor();
   assert.equal(await card.getByRole('button',{name:'Compartir y escalar',exact:true}).count(),0);
   assert.equal(await page.locator(`[data-ticket-id="${id==='1'?'2':'1'}"]`).count(),0);
   tickets.find(t=>t.id===id).status='RESOLVED';tickets.find(t=>t.id===id).resolvedByName=id==='1'?'Supervisor QA':'Avería QA';
   await page.getByRole('button',{name:'Actualizar ahora',exact:true}).click();
   await card.getByText('Resuelto',{exact:true}).waitFor();
   await page.getByRole('button',{name:'Resueltos',exact:true}).click();await card.waitFor();
   await page.getByRole('button',{name:'Volver a Tickets transferidos',exact:true}).click();
  }
  assert.equal(requests.filter(p=>p==='/api/tickets/statistics').length,0);
  assert.equal(requests.filter(p=>p==='/api/support/catalog').length,4);
  assert.equal(requests.filter(p=>p==='/api/tickets/agencies').length,3);
  await destinations.getByRole('button',{name:/Transferidos a supervisores/}).click();
  technicianFails=true;await page.getByRole('button',{name:'Actualizar ahora',exact:true}).click();
  await page.getByText(/Directorio temporalmente no disponible QA/).waitFor();
  await page.locator('[data-ticket-id="1"]').waitFor();
  technicianFails=false;await page.getByRole('button',{name:'Reintentar consultas',exact:true}).click();
  await page.getByText(/Directorio temporalmente no disponible QA/).waitFor({state:'detached'});
  await page.getByRole('button',{name:'Volver a Tickets transferidos',exact:true}).click();
  assert.deepEqual(errors,[]);
  await page.screenshot({path:`outputs/qa-callcenter-transfers-${width}.png`,fullPage:true});
  report.push({width,transfers:'two destinations, routing payload, isolation, live resolved status, read-only followup',reads:'503 recovered; statistics removed; directory cached',errors});
  await page.close();console.log('PASS Call Center transfers',width);
 }
 writeFileSync('outputs/qa-call-center-transfers.json',JSON.stringify(report,null,2));
}finally{await browser.close();await server.close();}
