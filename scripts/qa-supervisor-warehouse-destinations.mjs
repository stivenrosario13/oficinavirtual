import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const agencies=[{id:'11111111-1111-1111-1111-111111111111',codigo:'101',terminal:'Agencia Uno',grupo:'Grupo A'},{id:'22222222-2222-2222-2222-222222222222',codigo:'102',terminal:'Agencia Dos',grupo:'Grupo B'}];
try{for(const width of [1365,390]){
 const page=await browser.newPage({viewport:{width,height:950}}),sent=[],errors=[];
 page.on('pageerror',e=>errors.push(e.message));await page.route(/^https:\/\//,r=>r.abort());
 await page.route('**/api/**',r=>{
  const req=r.request(),path=new URL(req.url()).pathname;
  if(path==='/api/tickets/agencies')return r.fulfill({json:{agencies}});
  if(path==='/api/institutional-documents/warehouse-groups')return r.fulfill({json:{groups:['Grupo A','Grupo B']}});
  if(path==='/api/warehouse/fulfillment/inventory')return r.fulfill({json:[{department:'TECHNOLOGY',productId:'QA',name:'Monitor QA',available:0,serials:[],warehouse:'QA',group:'Equipos',division:'QA',unitId:'UND',tracksSerial:false}]});
  if(path==='/api/support/catalog')return r.fulfill({json:{departments:[]}});
  if(path==='/api/institutional-documents'&&req.method()==='POST'){
   const body=req.postDataBuffer().toString('utf8'),match=body.match(/name="warehouseRequest"\r\n\r\n([^\r\n]+)/);assert.ok(match);assert.ok(body.includes('%PDF-'));
   sent.push(JSON.parse(match[1]));return r.fulfill({json:{id:sent.length}});
  }
  throw Error('Unexpected API '+path);
 });
 const choose=async(label,query,option)=>{await page.getByRole('combobox',{name:label,exact:true}).fill(query);await page.getByRole('option',{name:option,exact:false}).click();};
 const submit=async total=>{await page.getByRole('button',{name:'Enviar solicitud',exact:true}).click();await page.waitForFunction(()=>!document.querySelector('.warehouse-request-composer button.primary')?.disabled);assert.equal(sent.length,total,(await page.locator('[role=alert]').allTextContents()).join(' '));};
 await page.goto('http://127.0.0.1:5201/v443/scripts/qa-inventory-agencies.html?department=SUPERVISION');
 await page.getByLabel('Destino de la solicitud').waitFor();
 assert.deepEqual(await page.getByLabel('Destino de la solicitud').locator('option').allTextContents(),['Para un grupo','Para una agencia','Para varias agencias']);
 await page.getByLabel('Destino de la solicitud').selectOption('GROUP');
 await choose('Grupo de agencias','Grupo B','Grupo B');
 await choose('Artículo 1 del catálogo','Monitor','Monitor QA');
 assert.equal(await page.getByRole('combobox',{name:/Agencia/}).count(),0);
 assert.equal(await page.getByRole('checkbox',{name:'Reemplazo por falla'}).count(),0);
 await page.screenshot({path:`outputs/qa-v443-supervisor-group-${width}.png`,fullPage:true});
 await submit(1);assert.equal(sent[0].destinationType,'GROUP');assert.equal(sent[0].agencyId,'00000000-0000-0000-0000-000000000000');assert.equal(sent[0].agencyGroup,'Grupo B');
 await page.getByLabel('Destino de la solicitud').selectOption('AGENCY');
 await choose('Agencia de la solicitud','101','101 · Agencia Uno');
 assert.equal(await page.getByRole('combobox',{name:'Agencia para artículo 1',exact:true}).count(),0);
 await submit(2);assert.equal(sent[1].agencyId,agencies[0].id);
 await page.getByLabel('Destino de la solicitud').selectOption('MULTI_AGENCY');
 await choose('Agencia para artículo 1','101','101 · Agencia Uno');
 await page.getByRole('button',{name:'Agregar artículo'}).click();
 await choose('Agencia para artículo 2','102','102 · Agencia Dos');await choose('Artículo 2 del catálogo','Monitor','Monitor QA');
 await page.getByRole('button',{name:'Agregar artículo'}).click();
 await choose('Agencia para artículo 3','101','101 · Agencia Uno');await choose('Artículo 3 del catálogo','Monitor','Monitor QA');
 await submit(4);assert.equal(sent[2].lines.length,2);assert.equal(sent[3].lines.length,1);assert.notEqual(sent[2].number,sent[3].number);
 assert.deepEqual(errors,[]);assert.ok(await page.locator('.warehouse-request-composer').evaluate(el=>el.scrollWidth<=el.clientWidth+1));
 console.log(`PASS Supervisor ${width}px: group without agency, single agency, multiple agencies consolidated per destination; PDF included`);
 await page.close();
}}finally{await browser.close();}
