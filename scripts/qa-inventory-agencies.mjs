import {createRequire} from 'node:module';import {readFileSync} from 'node:fs';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'}),page=await browser.newPage();let agencyCalls=0,imports=0;const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.route('**/api/**',async route=>{const request=route.request(),path=new URL(request.url()).pathname;
 if(path==='/api/tickets/agencies'){agencyCalls++;return route.fulfill({json:{agencies:[{id:'11111111-1111-1111-1111-111111111111',codigo:'123',terminal:'Agencia QA',grupo:'QA'}]}});}
 if(path==='/api/warehouse/fulfillment/inventory')return route.fulfill({json:[{department:'TECHNOLOGY',productId:'QA',name:'Monitor QA',available:3,serials:[],warehouse:'QA',group:'Equipos',division:'QA',unitId:'UND',tracksSerial:false}]});
 if(path==='/api/workshop/available-inventory/fiscalized'){if(request.method()==='POST'){imports++;if(request.postDataJSON().includeWithoutSerial!==true)throw Error('Checkbox not submitted');return route.fulfill({json:{added:83,alreadyPresent:0}});}return route.fulfill({json:JSON.parse(readFileSync('api/Data/workshop-fiscalized-20260930.json','utf8'))});}
 throw Error('Unexpected API: '+path);
});
try{
 for(const department of ['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','ADMINISTRATIVE']){
  await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department='+department);
  await page.getByRole('option',{name:'123 · Agencia QA · QA',exact:true}).waitFor({state:'attached'});
  await page.locator('.warehouse-agency-picker select').selectOption('11111111-1111-1111-1111-111111111111');
  await page.getByRole('option',{name:'Monitor QA · 3 disponible(s)'}).waitFor({state:'attached'});
  if(await page.getByText('No requiere agencia ni ticket.').count())throw Error('Unexpected internal mode');
 }
 if(agencyCalls!==4)throw Error('Agency load count');
 await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department=WAREHOUSE');await page.getByText('No requiere agencia ni ticket.').waitFor();
 if(agencyCalls!==4||await page.locator('.warehouse-agency-picker').count())throw Error('Warehouse own request regression');
 await page.goto('http://127.0.0.1:5176/scripts/qa-inventory-agencies.html?department=IMPORT');await page.getByRole('button',{name:'Cargar inventario fiscalizado del Excel'}).click();
 await page.getByRole('button',{name:'Agregar 82 equipos a Taller'}).waitFor();await page.getByRole('checkbox').check();await page.getByRole('button',{name:'Agregar 83 equipos a Taller'}).click();await page.getByRole('status').filter({hasText:'83 equipos agregados'}).waitFor();
 if(imports!==1||errors.length)throw Error(JSON.stringify({imports,errors}));console.log('PASS agencias en cuatro departamentos, catálogo cargado, Almacén interno, importación con vista previa y opción sin serial');
}finally{await browser.close();}
