import {createRequire} from 'node:module';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs'),{chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'}),page=await browser.newPage({viewport:{width:1366,height:900}}),errors=[],auditCalls=[],created=[];
page.on('pageerror',e=>errors.push(e.message));
page.setDefaultTimeout(60000);page.setDefaultNavigationTimeout(120000);
await page.route('**/api/**',async route=>{const req=route.request(),url=new URL(req.url()),path=url.pathname;let json={};
 if(path==='/api/department-audit'){auditCalls.push(url.searchParams.get('area'));json=[{id:'qa-event',at:new Date().toISOString(),actor:'Personal QA',module:'SUPPORT',action:'RESOLVED',reference:'#21',detail:'Actividad de prueba '+url.searchParams.get('area')}];}
 else if(path==='/api/tickets'){if(req.method()==='POST'){const body=req.postDataJSON();created.push(body);json={...body,id:'qa-ticket',ticketNumber:999,status:'OPEN',history:[],createdAt:new Date().toISOString()};}else json={tickets:[]};}
 else if(path==='/api/tickets/agencies')json={agencies:[{id:'11111111-1111-1111-1111-111111111111',codigo:'123',terminal:'Agencia QA',grupo:'QA'}]};
 else if(path==='/api/support/catalog')json={departments:[{code:'TECHNOLOGY',name:'Tecnología',categories:[{code:'CPU_FAILURE',name:'Falla de CPU',isActive:true,defaultPriority:'MEDIUM',requiresDetail:false}]},{code:'GENERAL_SERVICES',name:'Servicios Generales',categories:[{code:'ELECTRICITY',name:'Electricidad',isActive:true,defaultPriority:'MEDIUM',requiresDetail:false}]}]};
 else if(path==='/api/tickets/technicians')json={technicians:[]};
 else if(path==='/api/tickets/findings')json={findings:[]};
 else if(path==='/api/agency-transitions')json={projects:[]};
 else if(/inventory|movements|procurement/.test(path))json=[];
 return route.fulfill({json});
});
try{
 for(const scope of ['TECHNOLOGY','CALL_CENTER','TECHNICAL_FAILURE','GENERAL_SERVICES','WAREHOUSE','ADMIN']){
  await page.goto('http://127.0.0.1:5178/scripts/qa-audit-support.html?scope='+scope);
  await page.locator('.operations-center').waitFor();
  if(scope!=='ADMIN')await page.getByText('Personal QA',{exact:true}).waitFor();
  const options=await page.locator('select').first().locator('option').allTextContents();
  if(scope==='TECHNOLOGY'&&(!options.includes('Call Center')||!options.includes('Avería Técnica')))throw Error('Technology subarea options missing '+options);
  if(scope==='CALL_CENTER'&&options.some(x=>x==='Tecnología'||x==='Avería Técnica'))throw Error('Call Center scope widened');
  if(scope==='TECHNICAL_FAILURE'&&options.some(x=>x==='Tecnología'||x==='Call Center'))throw Error('Failure scope widened');
  if(scope==='TECHNOLOGY'){await page.locator('select').first().selectOption('CALL_CENTER');await page.waitForResponse(r=>r.url().includes('area=CALL_CENTER'));}
 }
 await page.goto('http://127.0.0.1:5178/scripts/qa-audit-support.html?support');await page.getByRole('button',{name:'Reportar incidencia',exact:true}).click();await page.getByRole('button',{name:/Agencia QA/}).click();
 await page.locator('.department-card-grid').waitFor();
 await page.locator('.department-card-grid button').filter({hasText:'Tecnología'}).first().click();
 await page.getByRole('button',{name:'Falla de CPUCrítica'}).click();
 await page.getByRole('button',{name:'Continuar con 1 caso',exact:true}).click();
 await page.screenshot({path:'tmp/pdfs/almacen-soporte-compartido.png'});
 await page.getByLabel('Detalle de Falla de CPU').fill('El equipo no enciende, requiere revisión de Tecnología.');
 await page.getByRole('button',{name:'Crear ticket',exact:true}).click();
 await page.waitForTimeout(500);
 if(created.length!==1||created[0].ticketType!=='INTERNAL'||created[0].assignedDepartment!=='TECHNOLOGY'||created[0].agencyId!=='11111111-1111-1111-1111-111111111111')throw Error('Incorrect ticket payload '+JSON.stringify(created));
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS shared audit views and department scope choices; warehouse opens shared report form.');
}finally{await browser.close();}
