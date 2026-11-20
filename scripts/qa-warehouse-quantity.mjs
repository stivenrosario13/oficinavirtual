import {createRequire} from 'node:module';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs'),{chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'}),page=await browser.newPage(),sent=[],errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.route('**/api/**',async route=>{const req=route.request(),path=new URL(req.url()).pathname;
 if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies:[{id:'11111111-1111-1111-1111-111111111111',codigo:'123',terminal:'Agencia QA',grupo:'QA'}]}});
 if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[{department:'TECHNOLOGY',productId:'QA',name:'Papel QA',available:20000,serials:[],warehouse:'QA',group:'Consumibles',division:'QA',unitId:'UND',tracksSerial:false}]});
 if(path==='/api/institutional-documents'&&req.method()==='POST'){const payload=req.postDataBuffer().toString('utf8'),match=payload.match(/name="warehouseRequest"\r\n\r\n([^\r\n]+)/);if(!match)throw Error('Missing request');sent.push(JSON.parse(match[1]));return route.fulfill({json:{id:999}});}
 throw Error('Unexpected API '+path);
});
try{
 for(const department of ['WAREHOUSE','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','ADMINISTRATIVE']){
  await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department='+department);
  if(department!=='WAREHOUSE'){await page.locator('.warehouse-agency-picker select option[value="11111111-1111-1111-1111-111111111111"]').waitFor({state:'attached'});await page.locator('.warehouse-agency-picker select').selectOption('11111111-1111-1111-1111-111111111111');}
  const row=page.locator('.warehouse-request-lines article').first();await row.locator('select option[value="QA"]').waitFor({state:'attached'});await row.locator('select').selectOption('QA');
  for(const quantity of [1000,1001,10000]){const before=sent.length;await row.locator('input[type="number"]').fill(String(quantity));const response=page.waitForResponse(r=>r.url().endsWith('/api/institutional-documents')&&r.request().method()==='POST');await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await response;await page.getByRole('button',{name:'Enviar solicitud',exact:true}).waitFor();if(sent.length!==before+1||sent.at(-1).lines[0].quantity!==quantity)throw Error('Quantity mismatch');}
  const before=sent.length;for(const quantity of [10001,0,1.5]){await row.locator('input[type="number"]').fill(String(quantity));await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.getByText('Completa los artículos y cantidades: máximo 10,000 unidades en total por solicitud.',{exact:true}).waitFor();}
  await row.locator('input[type="number"]').fill('10000');await page.getByRole('button',{name:'Agregar artículo',exact:true}).click();await page.locator('.warehouse-request-lines article').nth(1).locator('select').selectOption('QA');await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.getByText('Completa los artículos y cantidades: máximo 10,000 unidades en total por solicitud.',{exact:true}).waitFor();if(sent.length!==before)throw Error('Invalid request submitted');
 }
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS: 15 valid submissions across 5 departments; rejects >10000 total, zero and fractions. No production writes.');
}finally{await browser.close();}
