import {createRequire} from 'node:module';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'}),page=await browser.newPage(),sent=[],errors=[];
const agencies=[{id:'11111111-1111-1111-1111-111111111111',codigo:'123',terminal:'Agencia Uno',grupo:'Grupo A'},{id:'22222222-2222-2222-2222-222222222222',codigo:'456',terminal:'Agencia Dos',grupo:'Grupo B'}];
page.on('pageerror',e=>errors.push(e.message));
await page.route('**/api/**',async route=>{const req=route.request(),path=new URL(req.url()).pathname;
 if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies}});
 if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[{department:'TECHNOLOGY',productId:'QA',name:'Papel QA',available:20000,serials:[],warehouse:'QA',group:'Consumibles',division:'QA',unitId:'UND',tracksSerial:false}]});
 if(path==='/api/institutional-documents'&&req.method()==='POST'){const payload=req.postDataBuffer().toString('utf8'),match=payload.match(/name="warehouseRequest"\r\n\r\n([^\r\n]+)/);sent.push(JSON.parse(match[1]));return route.fulfill({json:{id:999}});}
 throw Error('Unexpected API '+path);
});
async function submit(){const response=page.waitForResponse(r=>r.url().endsWith('/api/institutional-documents')&&r.request().method()==='POST');await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await response;await page.getByRole('button',{name:'Enviar solicitud',exact:true}).waitFor();}
try{
 for(const department of ['WAREHOUSE','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','ADMINISTRATIVE']){
  await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department='+department);
  await page.getByLabel('Destino de la solicitud').selectOption('AGENCY');
  if(!await page.getByLabel('Agencia solicitante').isDisabled())throw Error('Agency enabled before group');
  await page.getByLabel('Grupo de agencias').locator('option[value="Grupo A"]').waitFor({state:'attached'});
  await page.getByLabel('Grupo de agencias').selectOption('Grupo A');
  if(await page.getByLabel('Agencia solicitante').locator('option[value="'+agencies[1].id+'"]').count())throw Error('Leaked other group');
  await page.getByLabel('Agencia solicitante').selectOption(agencies[0].id);
  await page.getByLabel('Grupo de agencias').selectOption('Grupo B');
  if(await page.getByLabel('Agencia solicitante').inputValue())throw Error('Stale agency');
  await page.getByLabel('Agencia solicitante').selectOption(agencies[1].id);
  const row=page.locator('.warehouse-request-lines article').first();await row.locator('select option[value="QA"]').waitFor({state:'attached'});await row.locator('select').selectOption('QA');
  await row.locator('input[type="number"]').fill('10000');await submit();
  if(sent.at(-1).agencyId!==agencies[1].id||sent.at(-1).agencyGroup!=='Grupo B')throw Error('Wrong agency destination');
  await page.getByLabel('Destino de la solicitud').selectOption('DEPARTMENT');await submit();
  if(sent.at(-1).agencyId!=='00000000-0000-0000-0000-000000000000'||sent.at(-1).ticketId!==null)throw Error('Wrong internal destination');
  await page.getByLabel('Destino de la solicitud').selectOption('AGENCY');
  if(await page.getByLabel('Grupo de agencias').inputValue()||await page.getByLabel('Agencia solicitante').inputValue())throw Error('Stale destination');
 }
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS: 10 submissions, 5 departments, internal and grouped agencies, resets and 10000 units. Mock API only.');
}finally{await browser.close();}
