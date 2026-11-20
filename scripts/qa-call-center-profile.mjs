import {createRequire} from 'node:module';
import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const canonical=JSON.parse(readFileSync('shared/call-center-support.json','utf8'));
const browser=await chromium.launch({headless:true,channel:'msedge'});
const base=process.env.QA_URL||'http://127.0.0.1:5202';
try{
 for(const width of process.env.QA_ONLY_DEFAULT?[]:[1365,390]){
  const page=await browser.newPage({viewport:{width,height:950}}),errors=[],calls=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.route(/^https:\/\//,r=>r.abort());
  await page.route('**/api/**',r=>{
   const path=new URL(r.request().url()).pathname;calls.push(path);
   if(path.endsWith('/statistics'))return r.fulfill({status:503,json:{error:'QA unavailable'}});
   if(path==='/api/institutional-documents'||path==='/api/warehouse/fulfillment/inventory')return r.fulfill({json:[]});
   let body={tickets:[],findings:[],agencies:[],technicians:[],departments:[],notifications:[],conversations:[],unreadCount:0,projects:[]};
   if(path==='/api/license/status')body={enabled:true,allowed:true,status:'ACTIVE',code:'OK',message:'QA',usingGracePeriod:false};
   if(path==='/api/admin/session')body={authenticated:true,id:'qa-call-center',email:'inocencio.avila@grupotejeda.local',displayName:'Call Center QA',role:'Technology',supportDepartment:'TECHNOLOGY',supportTeam:'CALL_CENTER',groups:[],permissions:canonical,mustChangePassword:false};
   if(path==='/api/support/catalog')body={departments:['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'].map((code,i)=>({code,name:['Tecnología','Servicios Generales','Recursos Humanos'][i],isActive:true,categories:[]}))};
   if(path==='/api/version')body={release:JSON.parse(readFileSync('public/version-web.json','utf8')).release};
   return r.fulfill({json:body});
  });
  await page.goto(base,{waitUntil:'domcontentloaded'});
  await page.getByRole('button',{name:'Ahora no',exact:true}).click();
  await page.locator('.department-card-grid button').filter({hasText:'Tecnología'}).click();
  try{await page.locator('.call-center-only').waitFor({timeout:15000});}catch(error){console.log({body:(await page.locator('body').innerText()).slice(0,4500),calls,errors});throw error;}
  assert.equal(await page.locator('.department-card-grid button').count(),0);
  const navText=await page.locator('#portal-sidebar').innerText();
  for(const label of ['Taller','Agencias en construcción','Actualizar agencias','Auditoría operativa','Usuarios y permisos'])assert.ok(!navText.includes(label),navText);
  for(const label of ['Soporte','Archivos y comunicaciones','Solicitudes a Almacén'])assert.ok(navText.includes(label),navText);
  assert.equal(await page.locator('.support-section-copy strong').filter({hasText:/^(Tickets|Tickets internos)$/}).count(),0);
  await page.screenshot({path:`outputs/qa-v442-call-center-${width}.png`,fullPage:true});
  await page.locator('.call-center-only').click();
  assert.ok(!calls.some(p=>p.startsWith('/api/maintenance/')||p.startsWith('/api/workshop')||p==='/api/agency-transitions'),calls.join('\n'));
  assert.deepEqual(errors,[]);
  console.log(`PASS Call Center ${width}px: own support card, restricted sidebar, no unrelated module calls`);
  await page.close();
 }
 const page=await browser.newPage({viewport:{width:1365,height:950}}),writes=[];
 await page.route('**/api/**',r=>{
  const req=r.request(),path=new URL(req.url()).pathname;
  if(path==='/api/admin/users'&&req.method()==='GET')return r.fulfill({json:{users:[]}});
  if(path==='/api/admin/groups-catalog')return r.fulfill({json:{groups:[]}});
  if(path==='/api/admin/access-profiles')return r.fulfill({json:{profiles:[{department:'CALL_CENTER',profile:'SUPPORT',permissions:{canViewAllSupportDepartments:true,canViewWorkshop:true,canManageDepartment:true}}]}});
  if(path==='/api/admin/users'&&req.method()==='POST'){writes.push(req.postDataJSON());return r.fulfill({json:{...writes.at(-1),id:'qa-new'}});}
  throw Error('Unexpected API call '+path);
 });
 await page.goto(base+'/v442/scripts/qa-users-permissions.html');
 await page.getByRole('button',{name:'Nuevo usuario',exact:true}).click();
 await page.getByLabel('Nombre completo',{exact:true}).fill('Call Center QA');
 await page.getByLabel('Usuario',{exact:true}).fill('nuevo.callcenter.qa');
 await page.getByLabel('Departamento',{exact:true}).selectOption('CALL_CENTER');
 assert.equal(await page.getByLabel('Perfil predeterminado').inputValue(),'SUPPORT');
 await page.getByText('Perfil principal: Call Center · Soporte',{exact:true}).waitFor();
 await page.screenshot({path:'outputs/qa-v442-call-center-default.png',fullPage:true});
 await page.getByRole('button',{name:'Guardar usuario',exact:true}).click();
 await page.getByRole('dialog').waitFor({state:'hidden'});
 assert.deepEqual(writes.at(-1).permissions,canonical);
 console.log('PASS default Call Center support profile ignores stale generic preset when creating users');
}finally{await browser.close();}
