import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {mkdir} from 'node:fs/promises';

const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const output='outputs/qa-administrative-payroll';
await mkdir(output,{recursive:true});
const page=await browser.newPage({viewport:{width:1600,height:1050},serviceWorkers:'block'});
page.setDefaultTimeout(15000);
const errors=[];
page.on('pageerror',error=>errors.push(error.message));
await page.route('**/api/**',route=>route.fulfill({json:[]}));

try{
 await page.goto('http://127.0.0.1:5188/scripts/qa-administrative-excel.html',{waitUntil:'domcontentloaded',timeout:60000});
 assert.equal(await page.getByText('FLUJO DE PRESTACIONES LABORALES').count(),0,'The payroll workflow must not be rendered on the main page.');
 await page.getByRole('button',{name:'Nueva solicitud',exact:true}).click();
 const catalog=page.getByRole('dialog',{name:'Catálogo de solicitudes administrativas'});
 const forms=[
  ['Solicitud de pago de prestaciones en cheque','Fecha de solicitud *'],
  ['Entrega de cheques en blanco a Contabilidad','Número de cheque'],
  ['Entrega de cheques firmados de prestaciones a Caja','Cédula'],
 ];
 for(const [title,label] of forms){
  const card=catalog.getByRole('button',{name:new RegExp(title)});
  await card.waitFor();
 }
 await page.screenshot({path:`${output}/catalogo-administrador-principal.png`,fullPage:true});
 for(const [title,label] of forms){
  await catalog.getByRole('button',{name:new RegExp(title)}).click();
  const form=page.getByRole('dialog',{name:title});
  await form.getByLabel(label,{exact:true}).first().waitFor();
  if(title==='Solicitud de pago de prestaciones en cheque'){
   assert.equal(await form.getByLabel('Solicitado por Recursos Humanos *',{exact:true}).count(),0);
   assert.equal(await form.getByLabel('Solicitado por Administración *',{exact:true}).count(),0);
   await page.screenshot({path:`${output}/solicitud-prestaciones-sin-responsables.png`,fullPage:true});
  }
  await form.getByRole('button',{name:'Cerrar'}).click();
  await page.getByRole('button',{name:'Nueva solicitud',exact:true}).click();
 }
 assert.deepEqual(errors,[]);
 console.log('PASS browser: primary administrator catalog displays and opens the three requested administrative forms.');
}catch(error){
 await page.screenshot({path:`${output}/failure.png`,fullPage:true}).catch(()=>{});
 throw error;
}finally{
 await browser.close();
}
