import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {PDFDocument,PDFPage} from 'pdf-lib';
import {afterEach,expect,it,vi} from 'vitest';
import {buildAdministrativeFormPdf,type AdministrativeFormPdfInput} from '../src/components/vite/administrativeFormPdf';
import {buildReceiptCopy} from '../src/components/vite/institutionalReceiptPdf';
import {administrativeSealLayout,administrativeCustodyLayout} from '../src/components/vite/administrativeSealLayout';
import {buildWarehouseRequestPdf} from '../src/components/vite/warehouseRequestPdf';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
import QRCode from 'qrcode';

const logo='data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64');
const input:AdministrativeFormPdfInput={number:'SOL-ADM-QA-414',title:'Renovación de marbete',subtitle:'Formulario de prueba sin validez',department:'Administración',createdBy:'Administrador de prueba',createdByLogin:'qa@example.invalid',responsible:'Recepción de prueba',target:'Vehículos del grupo',fields:[{label:'Período',value:'2025-2026'},{label:'Empresa o grupo',value:'Grupo Tejeda'},{label:'Responsable',value:'Stiven'},{label:'Fecha límite',value:'2027-02-13'}],tables:[{title:'03 / VEHÍCULOS INCLUIDOS',headers:['N.º','Placa','Chasis','Marca / modelo','Año','Monto RD$'],rows:[['1','G123456','15198494','Vehículo de prueba','2025','20000']],widths:[12,25,40,55,20,34]}],notes:'Renovación de marbetes del período 2025-2026.',logoData:logo,createdAt:new Date('2026-09-24T18:00:00Z')};
const item={title:input.title,status:'received',receivedUtc:'2026-09-24T18:10:00Z',receiverDisplayName:'Administrador de prueba',receiverLogin:'qa@example.invalid',receiptCode:'REAL-REC-QA-414',verificationToken:'qa-sin-validez',decisionBy:'Administrador de prueba',decisionUtc:'2026-09-24T18:15:00Z'} as DocumentDetail;
const save=(name:string,data:Uint8Array)=>{if(process.env.PDF_SEAL_QA==='1'){mkdirSync('tmp/pdfs',{recursive:true});writeFileSync(`tmp/pdfs/${name}.pdf`,data);}};
afterEach(()=>vi.restoreAllMocks());

it('el sello administrativo histórico sin token conserva un QR válido del formulario',async()=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf(input));
 const qr=vi.spyOn(QRCode,'toDataURL');
 await buildReceiptCopy(source,{...item,status:'approved',verificationToken:null,administrativeRequest:{number:input.number,assetType:'OTRO',targetType:'EMPLEADO',targetName:'QA'}} as DocumentDetail,logo,'https://example.invalid');
 expect(qr.mock.calls.some(call=>call[0]===`https://example.invalid/api/institutional-documents/lookup/${input.number}`)).toBe(true);
 expect(qr.mock.calls.some(call=>String(call[0]).includes('equipment=REAL-MNT%7C'))).toBe(false);
});

it('autoriza el retiro administrativo con el QR y conserva el PDF original',async()=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf(input)),before=source.slice();
 const text=vi.spyOn(PDFPage.prototype,'drawText');
 const output=await buildReceiptCopy(source,{...item,status:'approved'},logo,'https://example.invalid');
 expect(source).toEqual(before);
 expect(text.mock.calls.some(call=>call[0]==='AUTORIZADO')).toBe(true);
 expect(text.mock.calls.some(call=>call[0]==='A RETIRAR')).toBe(true);
 expect((await PDFDocument.load(output)).getPageCount()).toBe((await PDFDocument.load(source)).getPageCount());
});

it.each([
 ['SIM',8,true],['MARBETE',4,true],['DEVICE',8,false],['FLEET',8,false],['MODEM',6,false],['PAYMENT',10,false],
 ['CONTRACT_REQUEST',10,false],['CONTRACT_RENEWAL',17,false],['RENTAL_CONTRACT',18,false],
 ['RENT_RECEIPT',12,false],['INCREASE_LETTER',10,false],['FUEL_LOG',4,true],['GENERAL',4,false],
] as const)('mantiene la zona del sello en el formato %s',async(kind,fieldCount,table)=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf({...input,title:kind,
  fields:Array.from({length:fieldCount},(_,i)=>({label:`Campo ${i+1}`,value:'Dato de prueba'})),
  tables:table?input.tables:[],bodySections:kind==='RENTAL_CONTRACT'?[{title:'CONDICIONES',text:'Condiciones del contrato. '.repeat(70)}]:[],
 }));
 const pdf=await PDFDocument.load(source),layout=administrativeSealLayout(pdf.getSubject()||'',pdf.getPages().map(p=>p.getSize()));
 expect(layout).not.toBeNull();
 const custody=administrativeCustodyLayout(pdf.getSubject()||'',pdf.getPages().map(p=>p.getSize()));
 expect(layout!.page).toBe(0);expect(custody).not.toBeNull();
 for(const box of [layout!,custody!.delivery,custody!.receipt])expect(box.y).toBeGreaterThan(15*72/25.4);
 for(const login of ['emma.mendoza','derihan.alisha']){
  const draw=vi.spyOn(PDFPage.prototype,'drawRectangle');
  const result=await buildReceiptCopy(source,{...item,receiverDisplayName:login,receiverLogin:login},logo,'https://example.invalid');
  if(layout!.page===0)expect(draw.mock.calls[0][0]).toMatchObject({x:layout!.x,y:layout!.y,width:layout!.width,height:layout!.height});
  for(const target of draw.mock.instances)expect(target.doc.getPages().indexOf(target)).toBe(0);
  expect((await PDFDocument.load(result)).getPageCount()).toBe(pdf.getPageCount());
  save(`${kind}-recibido`,result);
  draw.mockRestore();
 }
});

it('el informe de incidentes conserva solo el sello de autorización y elimina entrega y recibido',async()=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf({...input,title:'Informe de incidentes',detailsOnlyAfterFirstPage:true,hideCustodySpaces:true}));
 const pdf=await PDFDocument.load(source),subject=pdf.getSubject()||'',sizes=pdf.getPages().map(page=>page.getSize());
 expect(administrativeSealLayout(subject,sizes)).not.toBeNull();
 expect(administrativeCustodyLayout(subject,sizes)).toBeNull();
 expect(subject).toContain('|ADMIN_SEAL=1');
 expect(subject).not.toContain('|ADMIN_DELIVERY=');
 expect(subject).not.toContain('|ADMIN_RECEIPT=');
 save('incidente-sin-custodia',source);
});

it.each(['received','approved','rejected','cancelled'])('ubica el sello %s en la primera página',async status=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf(input)),original=await PDFDocument.load(source);
 const layout=administrativeSealLayout(original.getSubject()||'',original.getPages().map(p=>p.getSize()));
 expect(layout).not.toBeNull();
 const draw=vi.spyOn(PDFPage.prototype,'drawRectangle');
 const output=await buildReceiptCopy(source,{...item,status} as DocumentDetail,logo,'https://example.invalid');
 const result=await PDFDocument.load(output);
 expect(result.getPageCount()).toBe(original.getPageCount());
 if(layout!.page===0)expect(draw.mock.calls[0][0]).toMatchObject({x:layout!.x,y:layout!.y,width:layout!.width,height:layout!.height});
 for(const target of draw.mock.instances)expect(target.doc.getPages().indexOf(target)).toBe(0);
 save(`marbete-${status}`,output);
});
it('sella la primera página de PDF históricos sin agregar una constancia ni modificar el original',async()=>{
 const source=await PDFDocument.load(await buildAdministrativeFormPdf(input));
 source.setSubject('REAL-ADMINISTRATIVE-FORM-V376|RECEIPT_SIGNATURE_PAGE=1');
 const count=source.getPageCount(),draw=vi.spyOn(PDFPage.prototype,'drawRectangle');
 const original=await source.save(),snapshot=original.slice();
 const output=await buildReceiptCopy(original,item,logo,'https://example.invalid');
 expect(original).toEqual(snapshot);
 expect((await PDFDocument.load(output)).getPageCount()).toBe(count);
 for(const page of draw.mock.instances)expect(page.doc.getPages().indexOf(page)).toBe(0);
 save('historico-recibido',output);
});
it('reserva los sellos en la primera página aunque las tablas y motivos sean extensos',async()=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf({...input,tables:[{...input.tables![0],rows:Array.from({length:50},(_,i)=>[`${i+1}`,'G123456','15198494','Vehículo de prueba','2025','20000'])}],notes:'Observaciones completas de la solicitud. '.repeat(90)}));
 const pdf=await PDFDocument.load(source),layout=administrativeSealLayout(pdf.getSubject()||'',pdf.getPages().map(p=>p.getSize()));
 expect(layout!.page).toBe(0);
 expect(layout!.y).toBeGreaterThan(37*72/25.4);
 const draw=vi.spyOn(PDFPage.prototype,'drawRectangle');
 const output=await buildReceiptCopy(source,{...item,status:'approved'} as DocumentDetail,logo,'https://example.invalid');
 for(const target of draw.mock.instances)expect(target.doc.getPages().indexOf(target)).toBe(0);
 const custody=administrativeCustodyLayout(pdf.getSubject()||'',pdf.getPages().map(p=>p.getSize()))!;
 for(const box of [layout!,custody.delivery,custody.receipt])expect(box.y).toBeGreaterThan(15*72/25.4);
 expect((await PDFDocument.load(output)).getPageCount()).toBe(pdf.getPageCount());
 save('extenso-autorizado',output);
});
it('entrega y recepción de activos no reemplazan ninguna firma',async()=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf(input)),pdf=await PDFDocument.load(source);
 const draw=vi.spyOn(PDFPage.prototype,'drawRectangle'),text=vi.spyOn(PDFPage.prototype,'drawText');
 const output=await buildReceiptCopy(source,{...item,status:'approved',administrativeDelivery:{number:input.number,at:item.receivedUtc,deliveredBy:'Entrega QA',deliveredLogin:'qa.entrega',receivedBy:'Recibe QA',assetIdentifiers:Array.from({length:65},(_,i)=>`MODEM-${i+1}`)}} as DocumentDetail,logo,'https://example.invalid');
 expect((await PDFDocument.load(output)).getPageCount()).toBeGreaterThan(pdf.getPageCount()+1);
 for(const target of draw.mock.instances)expect(target.doc.getPages().indexOf(target)).toBe(0);
 for(const label of ['AUTORIZADO','ENTREGADO','RECIBIDO']){
  const index=text.mock.calls.findIndex(call=>call[0]===label);
  expect(index).toBeGreaterThanOrEqual(0);
  expect(text.mock.instances[index].doc.getPages().indexOf(text.mock.instances[index])).toBe(0);
 }
 for(let index=1;index<=65;index++)expect(text.mock.calls.some(call=>call[0]===`${index}. MODEM-${index}`)).toBe(true);
 save('entrega-activos',output);
});
it('internet administrativo utiliza el recuadro existente de Almacén',async()=>{
 const source=new Uint8Array(await buildWarehouseRequestPdf({number:input.number,department:'TECHNOLOGY',destination:'Administración',agency:{codigo:'123',terminal:'Agencia QA',grupo:'QA'},priority:'MEDIUM',notes:'Solicitud de módem de prueba',lines:[{kind:'EQUIPMENT',name:'Módem',quantity:1,serial:'',note:''}],createdBy:input.createdBy,createdByLogin:input.createdByLogin,logoData:logo}));
 const pdf=await PDFDocument.load(source),layout=administrativeSealLayout(pdf.getSubject()||'',pdf.getPages().map(p=>p.getSize()));
 expect(layout!.page).toBe(0);expect(layout!.x).toBe(110*72/25.4);
 save('internet-recibido',await buildReceiptCopy(source,item,logo,'https://example.invalid'));
});
it('internet conserva los sellos administrativos de autorización, entrega y recibido',async()=>{
 const source=new Uint8Array(await buildWarehouseRequestPdf({number:input.number,department:'TECHNOLOGY',destination:'Administración',agency:{codigo:'123',terminal:'Agencia QA',grupo:'QA'},priority:'MEDIUM',notes:'Internet QA',lines:[{kind:'EQUIPMENT',name:'Módem',quantity:1,serial:'',note:''}],createdBy:input.createdBy,createdByLogin:input.createdByLogin,logoData:logo}));
 const text=vi.spyOn(PDFPage.prototype,'drawText');
 const output=await buildReceiptCopy(source,{...item,status:'approved',recipientDepartment:'Administración',administrativeRequest:{number:input.number,assetType:'INTERNET',targetType:'AGENCIA',targetName:'Agencia QA'},administrativeDelivery:{number:input.number,at:item.receivedUtc,deliveredBy:'Gledys Tejeda',deliveredLogin:'gledys.tejeda',receivedBy:'Persona QA',assetIdentifiers:['MODEM-QA']}} as DocumentDetail,logo,'https://example.invalid');
 for(const label of ['AUTORIZADO','A RETIRAR','ENTREGADO','RECIBIDO'])expect(text.mock.calls.some(call=>call[0]===label)).toBe(true);
 expect(text.mock.calls.some(call=>call[0].includes('gledys.tejeda'))).toBe(true);
 expect((await PDFDocument.load(output)).getPageCount()).toBeGreaterThanOrEqual((await PDFDocument.load(source)).getPageCount());
});
it('mantiene intacta una solicitud pendiente sin sellos',async()=>{
 const source=new Uint8Array(await buildAdministrativeFormPdf(input));
 const draw=vi.spyOn(PDFPage.prototype,'drawRectangle'),scale=vi.spyOn(PDFPage.prototype,'scaleContent');
 const output=await buildReceiptCopy(source,{...item,status:'pending',receivedUtc:undefined},logo,'https://example.invalid');
 expect(draw).not.toHaveBeenCalled();expect(scale).not.toHaveBeenCalled();
 expect((await PDFDocument.load(output)).getPageCount()).toBe((await PDFDocument.load(source)).getPageCount());
});
it('rechaza ubicaciones fuera de página o tamaños inesperados',()=>{
 for(const marker of ['ADMIN_SEAL=99,110,194,87,33','ADMIN_SEAL=1,110,280,87,33','ADMIN_SEAL=1,110,194,300,33'])expect(administrativeSealLayout('|'+marker,[{width:595,height:842}])).toBeNull();
});
