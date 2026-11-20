import {createRequire} from 'node:module';
import assert from 'node:assert/strict';
import {PDFDocument} from 'pdf-lib';
const require=createRequire('C:/Users/pc/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/loader.cjs');
const {chromium}=require('playwright');
const pdf=await PDFDocument.create();pdf.addPage();const bytes=Buffer.from(await pdf.save());
const browser=await chromium.launch({headless:true,channel:'msedge'});
try{
 for(const user of ['ana.rodriguez','diorly.abreu'])for(const mode of ['requests','stamped-pending','stamped-correction','documents','history','legacy-history','denied-history']){
  const documents=mode==='documents',history=mode.includes('history'),denied=mode==='denied-history';
  const page=await browser.newPage({viewport:{width:1365,height:1000}}),writes=[],errors=[];
  const rows=['Tecnología','Comunicación','Supervisión'].map((department,i)=>({id:i+1,title:`Solicitud QA ${i+1}`,status:'received',receivedUtc:i===2?null:'2026-10-17T12:00:00Z',receivedByUserName:'otro.operador',isRecipient:true,area:'communications',channel:'requirements',contentType:'application/pdf',originalFileName:'QA.pdf',fileUrl:`/api/institutional-documents/${i+1}/preview`,createdUtc:'2026-10-17T11:00:00Z',size:bytes.length,department:history?'Servicios Generales':department,canAuthorizeWarehouseRequest:!denied,recipientDepartment:'Almacén',ownerUserName:'remitente',ownerDisplayName:'Remitente QA',warehouseRequest:{number:`SOL-ALM-QA-${i+1}`,lines:[{kind:'EQUIPMENT',name:'Monitor',quantity:1}]},warehouseAssigned:[],sharedWith:[],history:[]}));
  if(mode.startsWith('stamped-'))rows[0].status='pending';
  if(mode==='stamped-correction'){rows[0].status='correction_requested';rows[0].correctionNote='Revisar cantidad';}
  if(mode==='legacy-history')rows[0].warehouseRequest=null;
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/api/**',r=>{
   const req=r.request(),url=new URL(req.url()),path=url.pathname;
   if(path.endsWith('/preview'))return r.fulfill({contentType:'application/pdf',body:bytes});
   if(path.endsWith('/users'))return r.fulfill({json:[]});
   if(path.endsWith('/overview'))return r.fulfill({json:{total:3,mine:0,shared:0,pending:0,received:3,approved:0,sent:0,rejected:0,departments:rows.map(row=>({name:row.department,count:1,received:row.status==='received'?1:0,approved:row.status==='approved'?1:0,pending:0,rejected:0}))}});
   const action=path.match(/\/(\d+)\/actions\/approve$/);
   if(action){const row=rows.find(row=>row.id===Number(action[1]));writes.push(row.id);row.status='approved';row.canAuthorizeWarehouseRequest=false;return r.fulfill({json:{ok:true,document:row}});}
   if(path==='/api/institutional-documents')return r.fulfill({json:rows.filter(row=>!url.searchParams.get('department')||row.department===url.searchParams.get('department'))});
   const detail=path.match(/\/(\d+)$/);if(detail)return r.fulfill({json:rows.find(row=>row.id===Number(detail[1]))});
   return r.fulfill({status:404,json:{error:'Unexpected mock request '+path}});
  });
  await page.goto(`http://127.0.0.1:5201/warehouse-auth-v446/scripts/qa-warehouse-authorization.html?user=${user}${user==='diorly.abreu'?'&legacy':''}${documents?'&documents':''}${history?'&history':''}${denied?'&denied':''}`);
  if(history){await page.getByRole('button',{name:'Ver formulario',exact:true}).first().click();}
  else if(documents){await page.getByRole('button').filter({has:page.getByText('Comunicación',{exact:true})}).click();await page.getByRole('button',{name:'Ver y responder',exact:true}).first().click();}
  else{
   await page.getByRole('button',{name:/Pendientes de autorizar/}).click();
   for(const row of rows)await page.getByText(row.title,{exact:true}).waitFor();
   if(mode.startsWith('stamped-')){assert.equal(await page.locator('article').filter({has:page.getByText('Solicitud QA 1',{exact:true})}).getByRole('button',{name:'Recibir',exact:true}).count(),0);await page.locator('article').filter({has:page.getByText('Solicitud QA 1',{exact:true})}).screenshot({path:`outputs/qa-v445-${user}-stamped-pending.png`});}
   await page.locator('article').filter({has:page.getByText(mode.startsWith('stamped-')?'Solicitud QA 1':'Solicitud QA 3',{exact:true})}).getByRole('button',{name:'Autorizar',exact:true}).click();
  }
  if(denied){await page.locator('.native-doc-detail').waitFor();assert.equal(await page.getByRole('button',{name:'Autorizar y colocar sello',exact:true}).count(),0);console.log(`PASS ${user}: history denies unauthorized reader`);await page.close();continue;}
  await page.getByRole('button',{name:'Autorizar y colocar sello',exact:true}).click();
  if(history)await page.screenshot({path:`outputs/qa-v444-${user}-${mode}.png`});
  await page.getByLabel('Motivo o indicaciones').fill('Autorizado por prueba de bandeja');
  await page.getByRole('button',{name:'Confirmar',exact:true}).click();
  await page.waitForFunction(()=>!document.querySelector('.warehouse-authorize'));
  assert.equal(writes.length,1);assert.deepEqual(errors,[]);
  console.log(`PASS ${user}: ${history?mode+' preserves server authorization':documents?'document folders include Comunicación and preserve authorization':'all departments and historical received requests are available for authorization'}`);
  await page.close();
 }
}finally{await browser.close();}
