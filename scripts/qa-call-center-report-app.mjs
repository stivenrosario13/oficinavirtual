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
  await page.getByRole('button',{name:/Reportes y estadísticas/}).first().click();
  assert.equal(await page.getByText('Rendimiento operativo completo',{exact:true}).count(),0);
  await page.getByRole('button',{name:'Descargar Excel',exact:true}).waitFor();
  await page.getByRole('button',{name:'Descargar PDF',exact:true}).waitFor();
  await page.getByLabel('Fecha del reporte Call Center').fill('2026-10-15');
  await page.getByLabel('Periodo del reporte Call Center').selectOption('15');
  await page.getByText('2026-10-01 al 2026-10-15 · 15 día(s). Horario de Santo Domingo.',{exact:true}).waitFor();
  await page.locator('.cc-agent-report').scrollIntoViewIfNeeded();
  assert.equal(await page.locator('.cc-agent-report').evaluate(el=>getComputedStyle(el).backgroundColor),'rgb(11, 32, 50)');
  assert.equal(await page.locator('.cc-agent-report').evaluate(el=>el.scrollWidth<=el.clientWidth+1),true);
  await page.screenshot({path:`outputs/qa-v448-report-app-${width}.png`,fullPage:false});
  assert.ok(!calls.some(p=>p.startsWith('/api/maintenance/')||p.startsWith('/api/workshop')||p==='/api/agency-transitions'),calls.join('\n'));
  assert.deepEqual(errors,[]);
  console.log(`PASS Call Center ${width}px: own support card, restricted sidebar, no unrelated module calls`);
  await page.close();
 }
}finally{await browser.close();}
