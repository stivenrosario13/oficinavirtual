import {createRequire} from 'node:module';
import {readFileSync,mkdirSync} from 'node:fs';
import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const base=process.env.QA_URL||'http://127.0.0.1:5211';
const sections=['Soporte','Archivos y comunicaciones','Formularios administrativos','Solicitudes a Almacén'];
const scenarios=[['ADMINISTRATIVE','Administración'],['TREASURY','Tesorería'],['TECHNOLOGY','Tecnología'],['WAREHOUSE','Almacén'],['ACCOUNTING','Contabilidad','legacy']];
mkdirSync('outputs/department-administrative-navigation',{recursive:true});
try{
 for(const width of [1365,390])for(const [department,name,legacy] of scenarios){
  const page=await browser.newPage({viewport:{width,height:950},serviceWorkers:'block'}),errors=[],calls=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.addInitScript(()=>{localStorage.setItem('push-prompt-dismissed','1');localStorage.setItem('portal-sidebar-collapsed','0')});
  await page.route(/^https:\/\//,route=>route.abort());
  await page.route('**/api/**',route=>{
   const request=route.request(),url=new URL(request.url()),path=url.pathname;calls.push(url);
   assert.equal(request.method(),'GET','Navigation must not write real or mock records');
   if(path==='/api/admin/session')return route.fulfill({json:{authenticated:true,id:'qa',email:'qa.administrativo@grupotejeda.local',displayName:'Administrativo QA',role:department==='ADMINISTRATIVE'?'Administrative':'Department',assignedDepartment:department==='ADMINISTRATIVE'?null:department,supportDepartment:department,supportTeam:null,mustChangePassword:false,permissions:{canView:true,canConfigureProfile:true,canCreateTickets:true,canViewDocuments:true,canUploadDocuments:true,canRequestWarehouse:true,usesExplicitPermissions:true,...(legacy?{}:{'profile:ADMINISTRATIVE':true})}}});
   if(path==='/api/license/status')return route.fulfill({json:{enabled:true,allowed:true,status:'ACTIVE',code:'OK',message:'QA'}});
   if(path==='/api/version')return route.fulfill({json:JSON.parse(readFileSync('public/version-web.json','utf8'))});
   if(path==='/api/support/catalog')return route.fulfill({json:{departments:[{code:'TECHNOLOGY',name:'Tecnología',categories:[]}]}});
   if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies:[]}});
   if(path.startsWith('/api/administrative-files')||path.startsWith('/api/institutional-documents')||path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[]});
   return route.fulfill({json:{tickets:[],notifications:[],unreadCount:0}});
  });
  await page.goto(base,{waitUntil:'domcontentloaded'});
  await page.locator('main .mc-page-heading h2').getByText('Formularios administrativos',{exact:true}).waitFor();
  const sidebar=page.locator('#portal-sidebar');
  for(const section of sections)assert.equal(await sidebar.getByRole('button',{name:section,exact:true}).count(),1);
  for(const section of ['Mis tickets','Crear ticket','Solicitudes a Taller','Auditoría operativa','Inventario administrativo'])assert.equal(await sidebar.getByRole('button',{name:section,exact:true}).count(),0);
  for(const section of sections){
   const trigger=page.getByRole('button',{name:'Abrir menú principal',exact:true});
   if(await trigger.isVisible())await trigger.click();
   await sidebar.getByRole('button',{name:section,exact:true}).click();
   await page.locator('main .mc-page-heading h2').getByText(section,{exact:true}).waitFor();
   assert.equal(await sidebar.getByRole('button',{name:section,exact:true}).getAttribute('aria-current'),'page');
   assert.ok((await page.locator('main .mc-page-heading small').first().innerText()).includes(name.toUpperCase()));
   if(section==='Archivos y comunicaciones')await page.getByRole('heading',{name:`Biblioteca de ${name}`,exact:true}).waitFor();
   if(section==='Solicitudes a Almacén')await page.getByRole('heading',{name:'Solicitudes de equipo',exact:true}).waitFor();
  }
  assert.ok(calls.some(url=>url.pathname==='/api/administrative-files'&&url.searchParams.get('department')===department),'Files must use the assigned department');
  assert.deepEqual(errors,[]);
  await page.screenshot({path:`outputs/department-administrative-navigation/${department.toLowerCase()}-${width}.png`,fullPage:true});
  console.log(`PASS ${name} ${width}px: four shared sections, active navigation, own department, no duplicate menus`);
  await page.close();
 }
}finally{await browser.close();}
