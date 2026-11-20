import {createRequire} from 'node:module';
import {mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const base=process.env.QA_URL||'http://127.0.0.1:5199';
await mkdir('outputs/department-destinations',{recursive:true});
const agencies=Array.from({length:350},(_,i)=>({id:`11111111-1111-1111-1111-${String(i+1).padStart(12,'0')}`,codigo:String(i+1),terminal:`Agencia ${i+1}`,grupo:i%2?'Grupo B':'Grupo A'}));
const catalog={departments:[{code:'TECHNOLOGY',name:'Tecnología',categories:[{code:'CPU_FAILURE',name:'Falla CPU',isActive:true,defaultPriority:'CRITICAL'}]},{code:'GENERAL_SERVICES',name:'Servicios Generales',categories:[{code:'EQUIPMENT_FAILURE',name:'Falla de equipo',isActive:true,defaultPriority:'HIGH'}]},{code:'HUMAN_RESOURCES',name:'Recursos Humanos',categories:[{code:'OTHER',name:'Otro servicio',isActive:true,defaultPriority:'MEDIUM'}]}]};
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const department of (process.env.QA_PROFILES_ONLY?[]:['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','CALL_CENTER','TECHNICAL_FAILURE','WORKSHOP','WAREHOUSE'])){
  const page=await browser.newPage({viewport:{width:1440,height:1000},serviceWorkers:'block'}),sent=[],errors=[];
  page.on('pageerror',error=>errors.push(error.message));
  await page.route('**/api/**',route=>{
   const req=route.request(),path=new URL(req.url()).pathname;
   if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies}});
   if(path==='/api/support/catalog')return route.fulfill({json:catalog});
   if(path==='/api/institutional-documents/warehouse-groups')return route.fulfill({json:{groups:['Grupo A','Grupo B']}});
   if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[{department:'TECHNOLOGY',productId:'QA',name:'Monitor QA',available:0,serials:[],warehouse:'Central',group:'Pantallas',division:'Equipos',unitId:'UND',tracksSerial:true}]});
   if(path==='/api/institutional-documents'&&req.method()==='POST'){
    const match=req.postDataBuffer().toString('utf8').match(/name="warehouseRequest"\r\n\r\n([^\r\n]+)/);assert.ok(match);sent.push(JSON.parse(match[1]));return route.fulfill({json:{id:sent.length}});
   }
   return route.fulfill({json:[]});
  });
  const choose=async(label,query,option)=>{const input=page.getByRole('combobox',{name:label,exact:true});await input.fill(query);await page.getByRole('option',{name:option,exact:false}).click()};
  const submit=async count=>{await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.waitForFunction(()=>!document.querySelector('.warehouse-request-composer button.primary')?.disabled);assert.equal(sent.length,count,await page.locator('[role=alert]').allTextContents().then(x=>x.join(' ')))};
  await page.route(/^https:\/\//,route=>route.abort());
  await page.goto(`${base}/scripts/qa-inventory-agencies.html?department=${department}`,{waitUntil:"domcontentloaded",timeout:60000});
  await page.getByLabel('Destino de la solicitud').selectOption('AGENCY');
  assert.equal(await page.getByRole('combobox',{name:'Agencia para artículo 1',exact:true}).count(),0);
  await choose('Agencia de la solicitud','350','350 · Agencia 350');
  await choose('Artículo 1 del catálogo','Monitor','Monitor QA');
  await page.getByRole('checkbox',{name:'Reemplazo por falla'}).check();
  await page.getByRole('combobox',{name:'Departamento de la falla · artículo 1'}).click();
  assert.equal(await page.locator('.search-select-dropdown [role=option]').count(),2);
  await page.getByRole('option',{name:'Tecnología',exact:true}).click();
  await choose('Falla · artículo 1','CPU','Falla CPU');
  await submit(1);assert.equal(sent[0].agencyId,agencies[349].id);assert.equal(sent[0].failureDepartment,'TECHNOLOGY');
  await choose('Departamento de la falla · artículo 1','Servicios','Servicios Generales');
  assert.equal(await page.getByRole('combobox',{name:'Falla · artículo 1',exact:true}).inputValue(),'');
  await choose('Falla · artículo 1','equipo','Falla de equipo');
  await submit(2);assert.equal(sent[1].failureDepartment,'GENERAL_SERVICES');
  await page.getByRole('checkbox',{name:'Reemplazo por falla'}).uncheck();await submit(3);assert.equal(sent[2].failureCategory,null);
  await page.getByLabel('Destino de la solicitud').selectOption('GROUP');
  assert.equal(await page.getByRole('combobox',{name:/Agencia/}).count(),0);assert.equal(await page.getByRole('checkbox',{name:'Reemplazo por falla'}).count(),0);
  await choose('Grupo de agencias','Grupo B','Grupo B');await submit(4);
  assert.equal(sent[3].destinationType,'GROUP');assert.equal(sent[3].agencyId,'00000000-0000-0000-0000-000000000000');assert.equal(sent[3].agencyGroup,'Grupo B');assert.equal(sent[3].failureDepartment,null);
  await page.getByLabel('Destino de la solicitud').selectOption('MULTI_AGENCY');
  await choose('Agencia para artículo 1','350','350 · Agencia 350');
  await page.getByRole('button',{name:'Agregar artículo'}).click();
  await choose('Agencia para artículo 2','349','349 · Agencia 349');await choose('Artículo 2 del catálogo','Monitor','Monitor QA');
  await page.getByRole('button',{name:'Agregar artículo'}).click();
  await choose('Agencia para artículo 3','350','350 · Agencia 350');await choose('Artículo 3 del catálogo','Monitor','Monitor QA');
  await submit(6);assert.equal(sent[4].lines.length,2);assert.equal(sent[5].lines.length,1);assert.notEqual(sent[4].number,sent[5].number);
  await page.getByLabel('Destino de la solicitud').selectOption('DEPARTMENT');await submit(7);assert.equal(sent[6].destinationType,'DEPARTMENT');assert.equal(sent[6].failureDepartment,null);
  assert.deepEqual(errors,[]);console.log(`PASS ${department}: 4 destinations, both failure departments, optional failure, 350 agencies, grouping and stock zero.`);
  if(department==='COMMUNICATION'){
   await page.getByLabel('Destino de la solicitud').selectOption('AGENCY');await choose('Agencia de la solicitud','350','350 · Agencia 350');
   await page.getByRole('combobox',{name:'Artículo 1 del catálogo',exact:true}).fill('Monitor');
   await page.screenshot({path:'outputs/department-destinations/autocomplete-1440.png',fullPage:true});
   await page.keyboard.press('Escape');await choose('Artículo 1 del catálogo','Monitor','Monitor QA');
   await page.setViewportSize({width:390,height:844});await page.screenshot({path:'outputs/department-destinations/warehouse-390.png',fullPage:true});
   assert.ok(await page.locator('.warehouse-request-composer').evaluate(el=>el.scrollWidth<=el.clientWidth+1),'No mobile overflow');
  }
  await page.close();
 }
 for(const [code,name] of [['COMMUNICATION','Comunicación'],['COMMERCIALIZATION','Comercialización'],['TREASURY','Tesorería'],['ACCOUNTING','Contabilidad'],['REQUESTER','Comunicación']]){
  const page=await browser.newPage({viewport:{width:1440,height:1000},serviceWorkers:'block'}),tickets=[],errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.addInitScript(()=>localStorage.setItem('push-prompt-dismissed','1'));
  await page.route('**/api/**',route=>{
   const req=route.request(),path=new URL(req.url()).pathname;
   if(path==='/api/admin/session')return route.fulfill({json:{authenticated:true,id:'qa',role:'Department',assignedDepartment:code==='REQUESTER'?'COMMUNICATION':code,email:'qa@example.invalid',displayName:'Departamento QA',scope:'all',permissions:{canView:true,canCreateTickets:true,canRequestWarehouse:true,...(code==='REQUESTER'?{'profile:WAREHOUSE_REQUESTER':true}:{} )}}});
   if(path==='/api/license/status')return route.fulfill({json:{enabled:true,allowed:true,status:'ACTIVE',code:'ACTIVE',message:'QA'}});
   if(path==='/api/support/catalog')return route.fulfill({json:catalog});
   if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies}});
   if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[{department:'TECHNOLOGY',productId:'QA',name:'Monitor QA',available:0,serials:[],warehouse:'Central',group:'Pantallas',division:'Equipos',unitId:'UND',tracksSerial:true}]});
   if(path==='/api/tickets'&&req.method()==='POST'){tickets.push(req.postDataJSON());return route.fulfill({json:{ticketNumber:101}})}
   if(path==='/api/tickets'||path==='/api/tickets/notifications')return route.fulfill({json:{tickets:[],notifications:[],unreadCount:0}});
   return route.fulfill({json:[]});
  });
  await page.route(/^https:\/\//,route=>route.abort());
  await page.goto(base,{waitUntil:"domcontentloaded",timeout:60000});await page.locator(code==='REQUESTER'?'.requester-sidebar':'#portal-sidebar').waitFor();
  if(code==='REQUESTER'){await page.getByRole('heading',{name:'Solicitudes de equipo',exact:true}).waitFor();await page.screenshot({path:'outputs/department-destinations/requester-1440.png',fullPage:true});await page.setViewportSize({width:390,height:844});await page.getByRole('button',{name:'Abrir menú',exact:true}).click();await page.getByRole('navigation',{name:'Secciones de solicitudes'}).getByRole('button',{name:'Solicitudes a Almacén',exact:true}).click();await page.screenshot({path:'outputs/department-destinations/requester-390.png',fullPage:true});assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));assert.deepEqual(errors,[]);console.log('PASS restricted requester: same request cards, sidebar and mobile layout.');await page.close();continue;}
  const sidebar=page.locator('#portal-sidebar');await sidebar.getByRole('button',{name:'Solicitudes a Almacén',exact:true}).click();
  await page.getByRole('heading',{name:'Solicitudes de equipo',exact:true}).waitFor();assert.equal(await page.locator('.request-hub-cards').count(),0);
  if(code==='COMMUNICATION'){
   await page.screenshot({path:'outputs/department-destinations/department-warehouse.png',fullPage:true});
   await page.getByRole('button',{name:'Nueva solicitud',exact:true}).click();
   await page.getByRole('combobox',{name:'Agencia de la solicitud',exact:true}).fill('350');await page.getByRole('option',{name:/350 · Agencia 350/}).click();
   await page.getByRole('combobox',{name:'Artículo 1 del catálogo',exact:true}).fill('Monitor');
   await page.screenshot({path:'outputs/department-destinations/form-1440.png',fullPage:true});
   await page.keyboard.press('Enter');await page.getByRole('checkbox',{name:'Reemplazo por falla'}).check();
   await page.getByRole('combobox',{name:'Departamento de la falla · artículo 1',exact:true}).fill('Servicios');await page.keyboard.press('Enter');
   await page.getByRole('combobox',{name:'Falla · artículo 1',exact:true}).fill('equipo');await page.keyboard.press('Enter');
   assert.equal(await page.locator('.warehouse-ticket-toggle').evaluate(el=>getComputedStyle(el).flexDirection),'row');await page.screenshot({path:'outputs/department-destinations/failure-1440.png',fullPage:true});
   await page.setViewportSize({width:390,height:844});await page.locator('.warehouse-request-composer').evaluate(el=>el.scrollTop=0);await page.screenshot({path:'outputs/department-destinations/form-390.png',fullPage:true});
   assert.ok(await page.locator('.warehouse-request-composer').evaluate(el=>el.scrollWidth<=el.clientWidth+1));
   await page.locator('.warehouse-request-composer').getByRole('button',{name:'Cerrar',exact:true}).click();await page.setViewportSize({width:1440,height:1000});
  }
  await sidebar.getByRole('button',{name:'Soporte',exact:true}).click();await page.getByRole('button',{name:/Crear ticket interno/}).click();
  await page.locator('.internal-department-grid').getByRole('button',{name:/Tecnología/}).click();await page.getByRole('button',{name:/Falla CPU/}).click();
  if(code==='COMMUNICATION')await page.screenshot({path:'outputs/department-destinations/department-support.png',fullPage:true});
  await page.getByRole('button',{name:/Continuar con 1 caso/}).click();await page.getByRole('combobox',{name:'Agencia',exact:true}).fill('350');await page.getByRole('option',{name:/350 · Agencia 350/}).click();
  await page.getByLabel('Asunto',{exact:true}).fill('Revisar CPU');await page.getByLabel('Descripción de la falla',{exact:true}).fill('Equipo sin encender.');await page.getByRole('button',{name:'Enviar ticket interno',exact:true}).click();await page.getByRole('dialog').waitFor({state:'hidden'});
  assert.equal(tickets.length,1);assert.equal(tickets[0].agencyId,agencies[349].id);assert.equal(tickets[0].ticketType,'INTERNAL');assert.deepEqual(errors,[]);console.log(`PASS profile ${name}: warehouse design and administrative support design with working ticket submission.`);await page.close();
 }
}finally{await browser.close()}
