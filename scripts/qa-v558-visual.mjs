import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);
let chromium;
try{({chromium}=require('playwright'));}catch{
 if(!process.env.PLAYWRIGHT_LOADER)throw Error('Instala Playwright o define PLAYWRIGHT_LOADER con la ruta de su loader.');
 ({chromium}=createRequire(process.env.PLAYWRIGHT_LOADER)('playwright'));
}
const reference=true,original=true;
const result=JSON.parse(fs.readFileSync(process.env.QA_RESULT||'outputs/v558-package-result.json','utf8').replace(/^\uFEFF/,''));
const screenshotPrefix=process.env.QA_PREFIX||'v558';
const web=path.join(result.stage,'wwwroot');
const server=http.createServer((req,res)=>{
 const p=path.join(web,decodeURIComponent(new URL(req.url,'http://localhost').pathname));
 const file=fs.existsSync(p)&&fs.statSync(p).isFile()?p:path.join(web,'index.html');
 const mime={'.js':'text/javascript','.css':'text/css','.json':'application/json','.svg':'image/svg+xml','.png':'image/png','.html':'text/html'};
 res.setHeader('Content-Type',mime[path.extname(file)]||'application/octet-stream');res.end(fs.readFileSync(file));
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const browser=await chromium.launch({headless:true,...(process.platform==='win32'?{channel:'msedge'}:{})});
try{
 for(const width of [1920,390]){
  const page=await browser.newPage({viewport:{width,height:1040},deviceScaleFactor:original&&width>900?0.64:1,serviceWorkers:'block'});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  page.on('console',m=>{if(m.type()==='error')errors.push(m.text())});
  page.on('requestfailed',r=>{if(/\.(js|css)(?:\?|$)/.test(r.url()))errors.push('Asset failed: '+r.url()+' '+r.failure()?.errorText)});
  await page.addInitScript((release)=>{localStorage.setItem('real-agencias-active-release',release);localStorage.setItem('push-prompt-dismissed','1')},result.release);
  await page.route('**/api/**',route=>{
   const p=new URL(route.request().url()).pathname;
   if(p==='/api/admin/session')return route.fulfill({json:{authenticated:true,id:'qa',email:'admin.qa@grupotejeda.local',displayName:'Administrador principal',role:'Administrator',scope:'all',mustChangePassword:false,permissions:Object.fromEntries(['canViewTicketNotifications','canUseSupportChat','canView','canManage','canManageUsers','canManageAudits','canManageAgencies','canViewDashboard','canAudit','canCreateTickets','canAssignTickets','canResolveTickets','canConfigureProfile','canExportAudits','canViewSupportDashboard','canViewAllSupportDepartments','canConfigureSupport','canViewSupportFindings','canViewDocuments','canUploadDocuments','canRequestWarehouse','canViewWarehouseInventory','canManageMaintenance','canViewWorkshop','canCreateMaintenanceMovements'].map(k=>[k,true]))}});
   if(p==='/api/license/status')return route.fulfill({json:{enabled:false,allowed:true,status:'ACTIVE',code:'OK',message:'QA'}});
   if(p==='/api/version')return route.fulfill({json:{release:'V554-V542-DARWIN-20261120'}});
   if(p==='/api/support/catalog'||p==='/api/support/directory')return route.fulfill({json:{departments:[['GENERAL_SERVICES','Servicios Generales','Infraestructura, energía, mantenimiento y adecuaciones físicas.','#24b8ee',13],['TECHNOLOGY','Tecnología','Equipos, conectividad, redes, internet y sistemas.','#6755ee',11],['HUMAN_RESOURCES','Recursos Humanos','Personal, licencias, nómina e incentivos.','#ee4488',6],['ADMINISTRATIVE','Departamento Administrativo','Solicitudes y gestiones administrativas.','#159f98',1],['COMMERCIALIZATION','Comercialización','Archivos, comunicaciones y solicitudes del departamento.','#159f98',1],['TREASURY','Tesorería','Archivos, comunicaciones y solicitudes del departamento.','#159f98',1],['ACCOUNTING','Contabilidad','Archivos, comunicaciones y solicitudes del departamento.','#447fff',1],['COMMUNICATION','Comunicación','Soporte del departamento.','#159f98',1],['GENERAL_AUDIT','Auditoría General','Soporte del departamento.','#159f98',1],['CASHIER','Caja','Soporte del departamento.','#159f98',1],['SUPERVISION','Supervisión','Soporte del departamento.','#159f98',1],['VAULT','Bóveda','Soporte del departamento.','#159f98',1]].map(([code,name,description,accentColor,count])=>({code,name,description,accentColor,isActive:true,categories:Array.from({length:count},(_,i)=>({code:'C'+i,name:'Servicio '+i,isActive:true}))}))}});
   if(p==='/api/tickets/agencies')return route.fulfill({json:{agencies:[]}});
   if(p.startsWith('/api/institutional-documents')||p.startsWith('/api/administrative-files'))return route.fulfill({json:[]});
   return route.fulfill({json:{tickets:[],notifications:[],unreadCount:0,users:[],groups:[],profiles:[],items:[],departments:[],summary:{}}});
  });
  await page.goto(`http://127.0.0.1:${server.address().port}`,{waitUntil:'networkidle'});
  try{await page.locator('.office-shell').waitFor({timeout:15000})}catch(e){console.log('BODY',await page.locator('body').innerText());console.log('ERRORS',errors);await page.screenshot({path:'outputs/v555-debug.png'});throw e}
  const nav=page.locator(reference&&width>900?'#portal-sidebar':'.mobile-main-nav');
  await nav.getByRole('button',{name:'Departamentos',exact:true}).click();
  if(width>900){
   await page.locator('.department-primary-grid').waitFor();
   assert.equal(await page.locator('.portal-sidebar').isVisible(),reference,'Desktop sidebar must match reference');
   assert.equal(await page.locator('.mobile-main-nav').isVisible(),!reference,'Horizontal navigation must match reference');
   if(reference){
    assert.equal(await page.locator('.portal-global-search').isVisible(),false);
    if(original)assert.equal(await page.locator('.v520-header-search').isVisible(),false);
    await page.locator('.header-profile strong').getByText('Administrador principal',{exact:true}).waitFor();
    assert.equal(await page.locator('.sidebar-desktop-collapse').isVisible(),true);
    assert.equal(await page.locator('.header-account>.header-logout').isVisible(),true);
    assert.equal(await page.getByRole('button',{name:'Enviar notificación de prueba',exact:true}).isVisible(),true);
    assert.equal(await page.locator('.portal-mobile-dock').isVisible(),false);
    assert.equal(await page.locator('.department-primary-grid').evaluate(el=>getComputedStyle(el).gridTemplateColumns.split(' ').length),4);
   }
  }
  await page.locator('.department-primary-grid button').filter({hasText:'Tecnología'}).waitFor();
  assert.equal(await page.getByText('No pudimos mostrar este módulo',{exact:true}).count(),0);
  await page.screenshot({path:`outputs/${screenshotPrefix}-visual-${width}.png`,fullPage:true});
  await nav.getByRole('button',{name:reference&&width>900?'Formularios administrativos':'Formularios',exact:true}).click();
  await page.getByRole('button',{name:/Nueva solicitud/}).first().click();
  for(const title of result.forms){
   await page.getByRole('button',{name:new RegExp(title)}).first().waitFor();
  }
  await page.screenshot({path:`outputs/${screenshotPrefix}-forms-${width}.png`,fullPage:true});
  for(const title of result.forms){
   await page.getByRole('button',{name:new RegExp(title)}).first().click();
   await page.getByRole('dialog').filter({hasText:title}).waitFor();
   await page.getByRole('button',{name:/Volver/}).first().click();
  }
  if(original){
   assert.equal(await page.evaluate(async()=>typeof(await import('/assets/WarehouseRequestComposer-DjKr8DjI.js')).default),'function');
   if(result.preloadDependencies){
    const loaded=await page.evaluate(async({main,release,deps})=>{
     const app=await import('/assets/'+main+'?v='+release);
     await app._(()=>Promise.resolve(),deps);
     for(const file of deps.filter(name=>/\.js(?:\?|$)/.test(name)))await import('/'+file);
     return {total:deps.length,cssAsModule:Array.from(document.querySelectorAll('link[rel="modulepreload"]')).filter(link=>new URL(link.href).pathname.endsWith('.css')).length};
    },{main:result.main,release:result.release,deps:result.preloadDependencies});
    assert.equal(loaded.cssAsModule,0,'CSS must never be preloaded as JavaScript');
    console.log('PRELOAD CHECK',JSON.stringify(loaded));
   }
  }
  console.log(JSON.stringify({width,errors,sidebar:await page.locator('.portal-sidebar').isVisible(),horizontalNav:await page.locator('.mobile-main-nav').isVisible()}));
  if(errors.length)throw Error(errors.join('\n'));
  await page.close();
 }
}finally{await browser.close();server.close()}
