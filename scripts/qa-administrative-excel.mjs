import {createRequire} from 'node:module';
import {mkdir} from 'node:fs/promises';
import assert from 'node:assert/strict';
import ExcelJS from 'exceljs';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const browser=await chromium.launch({headless:true,channel:'msedge'});
const output='outputs/administrative-excel';await mkdir(output,{recursive:true});
const page=await browser.newPage({viewport:{width:1365,height:950},serviceWorkers:'block'});
const errors=[],writes=[];let failSecond=true;
const number='SOL-ADM-EDIT-QA';
let existing={id:1,title:'Solicitud general QA',originalFileName:number+'.pdf',contentType:'application/pdf',size:1000,department:'Administración',recipientDepartment:'Administración',ownerUserName:'qa',ownerDisplayName:'Usuario QA',ownerLogin:'qa@example.invalid',createdUtc:'2026-10-28T12:00:00Z',status:'pending',area:'communications',channel:'requirements',isOwner:true,canEditAdministrativeForm:true,administrativeRevision:0,sharedWith:[],administrativeRequest:{number,assetType:'OTRO',targetType:'AGENCIA',targetName:'Destino anterior',reason:'Motivo anterior',formType:'GENERAL',formData:{targetName:'Destino anterior',reason:'Motivo anterior',targetType:'AGENCIA',assetType:'OTRO'}}};
page.on('pageerror',error=>errors.push(error.message));
await page.route('**/api/**',async route=>{
  const request=route.request(),path=new URL(request.url()).pathname;
  if(path==='/api/institutional-documents'&&request.method()==='GET')return route.fulfill({json:[existing]});
  if(path==='/api/institutional-documents/1'&&request.method()==='GET')return route.fulfill({json:existing});
  if(path==='/api/tickets/agencies')return route.fulfill({json:{agencies:[{id:'11111111-1111-1111-1111-111111111111',codigo:'00123',terminal:'Agencia QA',grupo:'Grupo QA'}]}});
  if(['POST','PUT'].includes(request.method())){
    const body=request.postDataBuffer().toString('utf8');
    const payload=JSON.parse(body.match(/name="administrativeRequest"\r\n\r\n([^\r\n]+)/)[1]);
    writes.push({method:request.method(),payload,body});
    if(request.method()==='PUT'){existing={...existing,administrativeRequest:payload,administrativeRevision:existing.administrativeRevision+1};return route.fulfill({json:{id:1}})}
    if(writes.filter(w=>w.method==='POST').length===2&&failSecond){failSecond=false;return route.fulfill({status:503,json:{message:'Interrupción simulada'}})}
    return route.fulfill({json:{id:10+writes.length}});
  }
  return route.fulfill({json:[]});
});
async function createFile(headers,rows){const book=new ExcelJS.Workbook();const sheet=book.addWorksheet('Formularios');sheet.addRow(headers);rows.forEach(row=>sheet.addRow(row));return Buffer.from(await book.xlsx.writeBuffer())}
try{
  await page.goto('http://127.0.0.1:5188/scripts/qa-administrative-excel.html',{waitUntil:'domcontentloaded',timeout:60000});
  await page.getByRole('button',{name:'Editar formulario',exact:true}).click();
  await page.getByLabel('Agencia o empleado',{exact:true}).fill('Destino corregido');
  const edited=page.waitForResponse(response=>response.request().method()==='PUT');
  await page.getByRole('button',{name:'Guardar cambios',exact:true}).click();await edited;
  await page.getByRole('button',{name:'Nueva solicitud',exact:true}).waitFor();
  assert.equal(writes[0].method,'PUT');assert.equal(writes[0].payload.number,number);assert.equal(writes[0].payload.formData.targetName,'Destino corregido');assert.match(writes[0].body,/name="revision"\r\n\r\n0/);
  await page.getByRole('button',{name:'Nueva solicitud',exact:true}).click();
  await page.getByRole('button',{name:/Solicitud general Otros/}).click();
  await page.getByRole('button',{name:'Importar masivo desde Excel',exact:true}).click();
  const downloadPromise=page.waitForEvent('download');await page.getByRole('button',{name:'Descargar plantilla Excel'}).click();const download=await downloadPromise;assert.equal(download.suggestedFilename(),'Plantilla-general.xlsx');
  const buffer=await createFile(['Formulario','Tipo de solicitud','Tipo de destino','Agencia o empleado','Motivo y detalles'],[['A','OTRO','AGENCIA','Agencia A','Reparación oficina'],['B','OTRO','EMPLEADO','Empleado B','']]);
  await page.locator('input[type=file]').setInputFiles({name:'general.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer});
  await page.getByText('Completa el motivo o concepto.').waitFor();assert.equal(await page.getByRole('button',{name:/Importar formularios/}).isDisabled(),true);
  await page.getByRole('button',{name:'Revisar / editar'}).nth(1).click();
  await page.getByLabel('Motivo y detalles').fill('Compra de materiales');
  await page.getByRole('button',{name:'Guardar revisión',exact:true}).click();
  await page.screenshot({path:output+'/revision-desktop.png',fullPage:true});
  await page.getByRole('button',{name:/Importar formularios/}).click();
  await page.getByRole('alert').filter({hasText:'Interrupción simulada'}).waitFor();
  assert.equal(await page.locator('tbody tr').count(),1);
  const secondAttemptNumber=writes.at(-1).payload.number;
  await page.getByRole('button',{name:/Importar formularios/}).click();
  await page.getByText(/2 guardados · 0 pendientes/).waitFor();assert.equal(writes.at(-1).payload.number,secondAttemptNumber);
  assert.equal(writes.filter(w=>w.method==='POST').length,3);
  await page.getByRole('button',{name:'Volver al listado'}).click();
  await page.setViewportSize({width:390,height:844});
  await page.getByRole('button',{name:'Nueva solicitud',exact:true}).click();await page.getByRole('button',{name:/Gasolina · gasto diario/}).click();await page.getByRole('button',{name:'Importar masivo desde Excel'}).click();
  const fuel=await createFile(['Formulario','Período','Detalle: Fecha de consumo','Detalle: Placa','Detalle: Monto RD$','Detalle: Departamento'],[['F1','2026-10','2026-10-28','000123',1250,'Administración'],['F1','','2026-10-28','000456',500,'Tecnología']]);
  await page.locator('input[type=file]').setInputFiles({name:'fuel.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:fuel});
  await page.getByText('Listo para importar',{exact:true}).waitFor();
  const bounds=await page.locator('.admin-excel-import').boundingBox();assert.ok(bounds.x>=0&&bounds.x+bounds.width<=391,'Mobile dialog fits viewport');
  await page.screenshot({path:output+'/revision-mobile.png',fullPage:true});
  await page.getByRole('button',{name:/Importar formularios/}).click();await page.getByText(/1 guardados · 0 pendientes/).waitFor();assert.equal(writes.at(-1).payload.formData.rows.length,2);assert.equal(writes.at(-1).payload.formData.rows[0].plate,'000123');
  await page.goto('http://127.0.0.1:5188/scripts/qa-administrative-excel.html?internet=1',{waitUntil:'domcontentloaded',timeout:60000});
  await page.getByRole('button',{name:/Equipo de internet/}).click();await page.getByRole('button',{name:'Importar masivo desde Excel'}).click();
  const internet=await createFile(['Formulario','Código de agencia','Tipo de servicio','Número de servicio','Empresa / compañía','Equipo o SIM','Cantidad de módems','Motivo del cambio'],[['I1','00123','BANDA_ANCHA','08095550101','CLARO','EQUIPO',2,'Cambio por avería']]);
  await page.locator('input[type=file]').setInputFiles({name:'internet.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:internet});
  await page.getByText('Listo para importar',{exact:true}).waitFor();await page.getByRole('button',{name:/Importar formularios/}).click();await page.getByText(/1 guardados · 0 pendientes/).waitFor();assert.equal(writes.at(-1).payload.internet.agencyCode,'00123');
  assert.deepEqual(errors,[]);
  console.log('PASS browser: editing existing request; Excel download, invalid-row correction, partial import retry with same folio; grouped fuel; mobile; internet import. API mocked.');
}catch(error){console.error(errors);await page.screenshot({path:output+'/failure.png',fullPage:true,timeout:10000}).catch(()=>{});throw error}finally{await browser.close()}
