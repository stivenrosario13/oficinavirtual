import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {resolve} from 'node:path';
import {readFileSync,writeFileSync} from 'node:fs';
import {createServer} from 'vite';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const server=await createServer({configFile:false,resolve:{alias:{'@':resolve('src')}},optimizeDeps:{entries:[],noDiscovery:true,include:['react','react/jsx-runtime','react/jsx-dev-runtime','react-dom/client','lucide-react']},esbuild:{jsx:'automatic'},server:{host:'127.0.0.1',port:5202,strictPort:true,watch:null}});
await server.listen();
const browser=await chromium.launch({headless:true,channel:'msedge'});
const regions=['Gran Santo Domingo','Norte','Este','Sur'];
const agencies=regions.map((region,i)=>({id:`agency-${i}`,codigo:`REG-${i}`,terminal:`Agencia ${region}`,grupo:'Grupo QA',region}));
const ticket=(agency,i,kind='SUPPORT',department='GENERAL_SERVICES')=>({id:`ticket-${department}-${kind}-${i}`,ticketNumber:i+1,codigo:agency?.codigo||'UNKNOWN',terminal:agency?.terminal||'Sin agencia',grupo:agency?.grupo||'',assignedDepartment:department,ticketType:kind,category:'GENERAL_OTHER',status:'OPEN',priority:'MEDIUM',subject:`Caso ${department} ${kind} ${agency?.region||'sin región'}`,description:'Descripción QA',createdByName:'QA',createdByUsername:'qa@test.invalid',createdAt:'2026-10-27T12:00:00Z',updatedAt:'2026-10-27T12:00:00Z',history:[],evidence:[],isAutomatic:false});
const tickets=agencies.flatMap((a,i)=>[ticket(a,i),ticket(a,i+10,'INTERNAL')]);
tickets.push(ticket(null,50),ticket(agencies[0],60,'SUPPORT','TECHNOLOGY'));
const findings=agencies.map((a,i)=>({id:`finding-${i}`,profileId:`profile-${i}`,codigo:a.codigo,grupo:a.grupo,terminal:a.terminal,department:'GENERAL_SERVICES',status:'OPEN',priority:'MEDIUM',findingKey:'GENERAL_OTHER',title:`Hallazgo ${a.region}`,detail:'Detalle QA',supervisorName:'Supervisor QA',supervisorUserId:'supervisor',supervisorLogin:'qa',submittedAt:'2026-10-27T12:00:00Z',evidence:[]}));
const fixtures={
 '/api/tickets':{tickets},'/api/tickets/agencies':{agencies},'/api/tickets/findings':{findings},
 '/api/tickets/technicians':{technicians:[]},'/api/tickets/statistics':{},
 '/api/support/catalog':{departments:['GENERAL_SERVICES','TECHNOLOGY'].map((code,i)=>({code,name:i?'Tecnología':'Servicios Generales',description:'Soporte',categories:[],isActive:true}))},
};
const report=[];
const css=[...readFileSync('src/main.tsx','utf8').matchAll(/import "(\.\/[^"\n]+\.css)"/g)].map(m=>`<link rel="stylesheet" href="/src/${m[1].slice(2)}">`).join('');
async function setup(viewport,mode){
 const page=await browser.newPage({viewport});const errors=[],requests=[];let passwordStatus=400,queueStatus=503;
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/*',route=>{
  const path=new URL(route.request().url()).pathname;
  if(path==='/qa')return route.fulfill({contentType:'text/html',body:`<html><head><meta name="viewport" content="width=device-width, initial-scale=1">${css}</head><body><div id="root"></div></body></html>`});
  if(path==='/api/account/password')return route.fulfill({status:passwordStatus,json:passwordStatus===200?{authenticated:true,id:'permanent-qa',email:'qa@test.invalid',displayName:'QA',role:'Administrator',permissions:{},mustChangePassword:false,groups:[],scope:'all',persistentSessionToken:'fresh-test-token'}:{error:'Contraseña actual incorrecta'}});
  if(path==='/api/institutional-documents/warehouse-queue'){requests.push(path);return route.fulfill({status:queueStatus,json:queueStatus===200?[{id:1,title:'Solicitud de cola QA',originalFileName:'qa.pdf',department:'Servicios Generales',recipientDepartment:'Almacén',ownerUserName:'qa',ownerDisplayName:'QA',status:'pending',createdUtc:new Date().toISOString()}]:{message:'Servicio no disponible'}});}
  if(path.startsWith('/api/')){requests.push(path);if(!(path in fixtures))throw Error('Missing fixture '+path);return route.fulfill({json:fixtures[path]});}
  return route.continue();
 });
 await page.goto('http://127.0.0.1:5202/qa');
 await page.evaluate(async mode=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;
  const dom=await import('/node_modules/.vite/deps/react-dom_client.js');const createRoot=dom.createRoot||dom.default.createRoot;
  const {permissions}=(await import('/src/lib/user-access-profiles.ts')).userAccessProfile('ADMINISTRATOR','TECHNOLOGY');
  const session={authenticated:true,id:'legacy-admin',email:'qa@test.invalid',displayName:'QA',role:'Administrator',permissions,mustChangePassword:false,scope:'all',supportDepartment:null,supportTeam:null,groups:[]};
  const Component=(await import(`/src/components/vite/${mode==='warehouse'?'MaintenanceOverview':mode==='password'?'ProfileSettings':'TicketCenter'}.tsx`)).default;
  window.root=createRoot(document.getElementById('root'));window.updatedSession=null;
  window.renderDepartment=(department='GENERAL_SERVICES',key=1,navigationTarget=null)=>window.root.render(React.createElement(Component,{session,departmentEntry:{key,department,section:'support'},navigationTarget,onSessionExpired(){},onSession(updated){window.updatedSession=updated},onClose(){},area:'WAREHOUSE',movements:[],loading:false,error:'',onRefresh(){},onDepartment(department,view){window.openedQueue={department,view}},onPanel(){},onDocument(){}}));
  window.renderDepartment();
 },mode);
 return {page,errors,requests,succeed(){passwordStatus=200;queueStatus=200},failQueue(){queueStatus=503}};
}
try{
 for(const width of [390,1366]){
  const {page,errors,requests}=await setup({width,height:900},'regions');
  const cards=page.getByRole('navigation',{name:'Regiones',exact:true});
  await cards.getByRole('button',{name:/Gran Santo Domingo/}).waitFor();
  await page.waitForFunction(()=>document.querySelector('[aria-label="Regiones"]')?.textContent.includes('2 tickets · 1 hallazgos'));
  assert.equal(await cards.getByRole('button').count(),4);
  assert.equal(await page.getByRole('navigation',{name:'Opciones de Soporte'}).count(),0);
  for(const region of regions){
   await cards.getByRole('button',{name:new RegExp(region)}).click();
   const options=page.getByRole('navigation',{name:'Opciones de Soporte'});
   assert.match(await options.innerText(),/1 registros/);assert.match(await options.innerText(),/1 pendientes/);
   await options.getByRole('button',{name:/CENTRO OPERATIVO/}).click();
   await page.getByRole('button',{name:/VISTA GENERAL Todo Servicios Generales/}).click();
   await page.getByRole('button',{name:/Bandeja activa/}).click();
   await page.getByText(`Caso GENERAL_SERVICES SUPPORT ${region}`,{exact:true}).first().waitFor({state:'attached'});
   await page.getByLabel(`Agencia Agencia ${region}, código REG-${regions.indexOf(region)}, grupo Grupo QA`,{exact:true}).waitFor({state:'attached'});
   for(const other of regions.filter(r=>r!==region))assert.equal(await page.getByText(`Caso GENERAL_SERVICES SUPPORT ${other}`,{exact:true}).count(),0);
   await page.getByRole('button',{name:new RegExp(`Regiones · ${region}`)}).click();
  }
  await page.getByRole('button',{name:/Sin región registrada: 1 tickets/}).click();
  await page.getByRole('navigation',{name:'Opciones de Soporte'}).getByRole('button',{name:/CENTRO OPERATIVO/}).click();
  await page.getByText('Caso GENERAL_SERVICES SUPPORT sin región',{exact:true}).first().waitFor({state:'attached'});
  await page.evaluate(()=>window.renderDepartment('TECHNOLOGY',2));
  await cards.waitFor();assert.match(await cards.getByRole('button',{name:/Gran Santo Domingo/}).innerText(),/1 tickets/);
  await cards.getByRole('button',{name:/Sur/}).click();
  assert.match(await page.getByRole('navigation',{name:'Opciones de Soporte'}).innerText(),/0 registros/);
  await page.evaluate(()=>window.renderDepartment('GENERAL_SERVICES',3,{kind:'TICKET',requestKey:100,assignedDepartment:'GENERAL_SERVICES',ticketId:'ticket-GENERAL_SERVICES-SUPPORT-0',ticketType:'SUPPORT',ticketStatus:'OPEN'}));
  await page.getByText('Caso GENERAL_SERVICES SUPPORT Gran Santo Domingo',{exact:true}).first().waitFor({state:'attached'});
  assert.deepEqual(errors,[]);assert(!requests.some(path=>path.includes('maintenance')));
  await page.evaluate(()=>window.renderDepartment('GENERAL_SERVICES',4));await cards.waitFor();
  await page.screenshot({path:`outputs/qa-regions-${width}.png`,fullPage:true});
  report.push({width,regions:'four cards, isolated cases/counts, unknown accessible, department reset, notification opens',errors});
  await page.close();
  const profile=await setup({width,height:900},'password');
  await profile.page.getByLabel('Contraseña actual',{exact:true}).fill('Wrong-test-123!');
  await profile.page.getByLabel('Nueva contraseña',{exact:true}).fill('New-test-password!');
  await profile.page.getByLabel('Confirmar nueva contraseña',{exact:true}).fill('New-test-password!');
  await profile.page.getByRole('button',{name:'Guardar nueva contraseña'}).click();
  await profile.page.getByText('Contraseña actual incorrecta',{exact:true}).waitFor();
  assert.equal(await profile.page.evaluate(()=>window.updatedSession),null);
  profile.succeed();await profile.page.getByRole('button',{name:'Guardar nueva contraseña'}).click();
  await profile.page.getByText('Contraseña actualizada. Se cerraron las sesiones de los demás dispositivos.',{exact:true}).waitFor();
  assert.equal(await profile.page.evaluate(()=>window.updatedSession.persistentSessionToken),'fresh-test-token');
  assert.equal(await profile.page.getByLabel('Nueva contraseña',{exact:true}).inputValue(),'');
  assert.deepEqual(profile.errors,[]);report.push({width,password:'failed change retained; successful change replaces session and clears secrets',errors:profile.errors});
  await profile.page.close();
  const queue=await setup({width,height:900},'warehouse');
  await queue.page.getByRole('alert').filter({hasText:'No se pudo cargar la cola de Almacén'}).waitFor();
  queue.succeed();await queue.page.getByRole('button',{name:'Reintentar',exact:true}).click();
  await queue.page.getByText('Solicitud de cola QA',{exact:true}).first().waitFor();
  queue.failQueue();await queue.page.evaluate(()=>window.dispatchEvent(new Event('support-live-refresh')));
  await queue.page.getByRole('alert').filter({hasText:'No se pudo cargar la cola de Almacén'}).waitFor();
  assert(await queue.page.getByText('Solicitud de cola QA',{exact:true}).count()>0);
  queue.succeed();await queue.page.getByRole('button',{name:'Reintentar',exact:true}).click();
  await queue.page.getByRole('button',{name:/Abrir solicitud/}).first().click();
  assert.deepEqual(await queue.page.evaluate(()=>window.openedQueue),{department:'GENERAL_SERVICES',view:'requests'});
  assert.deepEqual(queue.errors,[]);assert(queue.requests.every(path=>path==='/api/institutional-documents/warehouse-queue'));
  report.push({width,warehouse:'visible error, retry, retain data during outage, correct department link, summary endpoint only'});
  await queue.page.close();console.log('PASS regional navigation, password and warehouse queue UI',width);
 }
 writeFileSync('outputs/qa-regions-password.json',JSON.stringify(report,null,2));
}finally{await browser.close();await server.close();}
