import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
const server=spawn(process.execPath,['node_modules/vite/bin/vite.js','--config','scripts/qa-inventory-agencies.config.ts','--host','127.0.0.1','--port','5176'],{stdio:'ignore'});
process.on('exit',()=>server.kill());
for(let attempt=0;attempt<50;attempt++){try{await fetch('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html');break}catch{await new Promise(resolve=>setTimeout(resolve,200))}}
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const page=await browser.newPage();const errors=[],saved=[];
page.setDefaultNavigationTimeout(30000);
page.on('requestfailed',request=>console.log('REQUEST FAILED',request.url(),request.failure()?.errorText));
page.on('pageerror',error=>errors.push(error.message));
const agencies=[1,2].map(n=>({id:`${n}`.repeat(8)+'-'+`${n}`.repeat(4)+'-'+`${n}`.repeat(4)+'-'+`${n}`.repeat(4)+'-'+`${n}`.repeat(12),codigo:`00${n}`,terminal:`Agencia QA ${n}`,grupo:'Grupo QA'}));
await page.route('**/api/**',async route=>{
 const path=new URL(route.request().url()).pathname;
 if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies}});
 if(path==='/api/support/catalog')return route.fulfill({json:{departments:[]}});
 if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[]});
 if(path==='/api/institutional-documents'&&route.request().method()==='POST'){
  const body=route.request().postDataBuffer().toString();
  const json=body.match(/name="warehouseRequest"\r\n\r\n([^\r]+)/)?.[1];
  assert.ok(json,'warehouse metadata included');saved.push(JSON.parse(json));
  assert.match(body,/name="sourceDepartment"\r\n\r\nAdministración/);
  return route.fulfill({json:{id:saved.length}});
 }
 if(path.includes('institutional-logo'))return route.fulfill({status:404,body:''});
 return route.fulfill({json:[]});
});
async function open(){await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department=ADMINISTRATIVE',{waitUntil:'domcontentloaded'});await page.getByText('Solicitar artículo sin existencia',{exact:true}).waitFor();console.log('Formulario administrativo abierto');}
async function choose(label,n){await page.getByRole('combobox',{name:label,exact:true}).click();await page.getByRole('option',{name:new RegExp(`Agencia QA ${n}`)}).click();}
async function manual(n){await page.getByRole('button',{name:'Solicitar artículo sin existencia',exact:true}).last().click();await page.locator('.warehouse-request-lines input[maxlength="100"]').last().fill(`Material QA ${n}`);}
try{
 await open();assert.equal(await page.getByLabel('Destino de la solicitud',{exact:true}).inputValue(),'AGENCY');await choose('Agencia de la solicitud',1);await manual(1);await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.waitForFunction(()=>!document.querySelector('button.primary')?.disabled);assert.equal(saved.length,1);assert.equal(saved[0].agencyId,agencies[0].id);
 await open();await page.getByLabel('Destino de la solicitud',{exact:true}).selectOption('MULTI_AGENCY');await choose('Agencia para artículo 1',1);await manual(1);await page.getByRole('button',{name:'Agregar artículo',exact:true}).click();await choose('Agencia para artículo 2',2);await manual(2);await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.waitForFunction(()=>!document.querySelector('button.primary')?.disabled);assert.equal(saved.length,3);assert.deepEqual(saved.slice(1).map(x=>x.agencyId),agencies.map(x=>x.id));assert.notEqual(saved[1].number,saved[2].number);
 await open();await page.getByLabel('Destino de la solicitud',{exact:true}).selectOption('DEPARTMENT');await manual(3);await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.waitForFunction(()=>!document.querySelector('button.primary')?.disabled);assert.equal(saved.length,4);assert.equal(saved[3].destinationType,'DEPARTMENT');assert.equal(saved[3].agencyId,'00000000-0000-0000-0000-000000000000');
 await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department=ADMIN_FORMS&login=sandra.ramirez',{waitUntil:'domcontentloaded'});
 for(const title of ['Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja']){await page.getByRole('button',{name:new RegExp(title)}).click();await page.getByRole('dialog').filter({hasText:title}).waitFor();await page.getByRole('button',{name:/Volver/}).click();}
 assert.deepEqual(errors,[]);console.log('PASS: Administrativo envía una agencia, dos agencias con folios separados y departamento; abre los tres formularios; sin errores de JavaScript. API simulada.');
}catch(error){console.log('PAGE',await page.locator('body').innerText().catch(()=>''));console.log('JS ERRORS',errors);throw error;}finally{await browser.close();server.kill();}
