import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 const page=await browser.newPage({viewport:{width:1360,height:900}}),writes=[],errors=[],documentQueries=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('dialog',d=>d.accept());
 const departments=['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','ADMINISTRATIVE','WAREHOUSE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','CALL_CENTER','TECHNICAL_FAILURE','SUPERVISION'];
 const reference={canAssignTickets:true,canResolveTickets:true,canManageUsers:false,canModerateSupportChat:false,canCreateTickets:true,canRequestWarehouse:true};
 await page.route('**/api/**',route=>{
  const req=route.request(),path=new URL(req.url()).pathname;
  if(path==='/api/admin/users'&&req.method()==='GET')return route.fulfill({json:{users:[]}});
  if(path==='/api/admin/groups-catalog')return route.fulfill({json:{groups:[]}});
  if(path==='/api/admin/access-profiles')return route.fulfill({json:{profiles:departments.map(department=>({department,profile:'SUPPORT',permissions:reference}))}});
  if(path==='/api/admin/users'&&req.method()==='POST'){const body=req.postDataJSON();writes.push(body);return route.fulfill({json:{...body,id:'qa-'+writes.length}});}
  if(path==='/api/institutional-documents'){documentQueries.push(req.url());return route.fulfill({json:[]});}
  if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies:[]}});
  if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[]});
  throw Error('Unexpected API request: '+path);
 });
 await page.goto('http://127.0.0.1:5190/scripts/qa-users-permissions.html');
 await page.getByRole('searchbox',{name:'Buscar usuarios'}).fill('ross');
 const contrast=await page.evaluate(()=>Object.fromEntries([
  ['search','.access-filters .access-search input'],['heading','.users-access-heading h1'],['summary','.access-summary strong']
 ].map(([name,selector])=>[name,getComputedStyle(document.querySelector(selector)).color])));
 for(const color of Object.values(contrast))assert.ok(Number(color.match(/^rgb\((\d+)/)?.[1])<100,`Expected dark text, got ${color}`);
 await page.getByRole('searchbox',{name:'Buscar usuarios'}).clear();
 for(const department of departments){
  await page.getByRole('button',{name:'Nuevo usuario',exact:true}).click();
  await page.getByLabel('Nombre completo',{exact:true}).fill('Soporte '+department);
  await page.getByLabel('Usuario',{exact:true}).fill('support.'+department.toLowerCase());
  await page.getByLabel('Departamento',{exact:true}).selectOption(department);
  await page.getByRole('button',{name:/^Permisos/}).click();
  assert.equal(await page.locator('.access-permission-row input:not(:disabled)').count(),0);
  await page.getByText('Permisos principales: Ross Mariel · Servicios Generales',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Guardar usuario',exact:true}).click();await page.getByRole('dialog').waitFor({state:'hidden'});
  for(const [key,value] of Object.entries(reference))assert.equal(writes.at(-1).permissions[key],value);
 }
 await page.getByRole('button',{name:'Nuevo usuario',exact:true}).click();
 await page.getByLabel('Nombre completo',{exact:true}).fill('Solicitante de Almacén');await page.getByLabel('Usuario',{exact:true}).fill('solicitante.qa');
 await page.getByLabel('Perfil predeterminado').selectOption('WAREHOUSE_REQUESTER');
 await page.getByRole('button',{name:/^Permisos/}).click();await page.screenshot({path:'outputs/qa-warehouse-requester-profile.png',fullPage:true});
 await page.getByRole('button',{name:'Guardar usuario',exact:true}).click();await page.getByRole('dialog').waitFor({state:'hidden'});
 const saved=writes.at(-1).permissions;assert.equal(saved['profile:WAREHOUSE_REQUESTER'],true);assert.equal(saved.canAssignTickets,false);assert.equal(saved.canManageUsers,false);assert.equal(saved.canRequestWarehouse,true);
 await page.goto('http://127.0.0.1:5190/scripts/qa-users-permissions.html?requester');
 await page.getByRole('heading',{name:'Solicitudes a Almacén',exact:true}).waitFor();await page.getByText('No hay solicitudes de equipo con este criterio',{exact:true}).waitFor();
 assert.ok(documentQueries.some(url=>new URL(url).searchParams.get('view')==='mine'&&!new URL(url).searchParams.has('department')),JSON.stringify(documentQueries));
 assert.equal(await page.getByRole('button',{name:'Nueva solicitud',exact:true}).count(),1);assert.equal(await page.getByText('Usuarios y permisos',{exact:true}).count(),0);
 await page.screenshot({path:'outputs/qa-warehouse-requester-workspace.png',fullPage:true});
 await page.setViewportSize({width:390,height:844});
 const nav=page.getByRole('navigation',{name:'Secciones de Almacén en móvil'});
 await page.screenshot({path:'outputs/qa-warehouse-requester-mobile.png',fullPage:true});
 await nav.getByRole('button',{name:'Nueva solicitud'}).click();
 await page.getByRole('dialog',{name:'Solicitud de equipos y materiales'}).waitFor();
 await page.getByRole('button',{name:'Cerrar',exact:true}).click();
 await nav.getByRole('button',{name:'Mi cuenta'}).click();
 assert.equal(await page.locator('.warehouse-requester-mobile-nav button').filter({hasText:'Mi cuenta'}).getAttribute('aria-current'),'page');
 await page.goto('http://127.0.0.1:5190/scripts/qa-users-permissions.html?hub');
 const workshopCard=page.locator('[data-destination="workshop"]');
 await workshopCard.getByText('Tu perfil no tiene acceso a Taller').waitFor();
 assert.equal(await workshopCard.isDisabled(),true);
 assert.deepEqual(errors,[]);console.log('PASS support profiles, requester mobile workspace, and Treasury hub without unauthorized Workshop API calls. Mocked API.');
}finally{await browser.close();}
