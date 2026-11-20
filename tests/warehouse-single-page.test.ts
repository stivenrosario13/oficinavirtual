import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {PDFDocument,PDFPage} from 'pdf-lib';
import {afterEach,expect,it,vi} from 'vitest';
import {buildWarehouseRequestPdf} from '../src/components/vite/warehouseRequestPdf';
import {buildReceiptCopy} from '../src/components/vite/institutionalReceiptPdf';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
const logo='data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64');
const input={number:'SOL-ALM-QA-418',department:'TECHNOLOGY',agency:{codigo:'USO INTERNO',terminal:'Departamento de Tecnología',grupo:'Grupo Tejeda'},priority:'MEDIUM',notes:'Solicitud de prueba. Sin validez operativa.',createdBy:'Solicitante de prueba',createdByLogin:'qa@example.invalid',logoData:logo,lines:Array.from({length:8},(_,i)=>({kind:'EQUIPMENT' as const,name:i?'Teclado Dell':'Monitor HP',quantity:1,serial:'',note:''}))};
const item={id:418,title:'Solicitud de Almacén',recipientDepartment:'Almacén',createdUtc:'2026-09-29T12:00:00Z',receivedUtc:'2026-09-29T12:10:00Z',receiverDisplayName:'Encargado de Almacén',receiverLogin:'almacen.qa',ownerDisplayName:'Solicitante de prueba',ownerUserName:'qa',decisionBy:'Encargado de Almacén',decisionUtc:'2026-09-29T12:15:00Z',verificationToken:'qa-no-valido',receiptCode:'REAL-REC-QA',warehouseRequest:{number:input.number,lines:input.lines},warehouseAssigned:input.lines.map((row,i)=>({lineIndex:i,name:row.name,serial:'SERIAL-QA-'+i,quantity:1}))} as DocumentDetail;
afterEach(()=>vi.restoreAllMocks());
it.each(['pending','received','approved','delivered','cancelled'])('mantiene solo la primera hoja en estado %s, sin alterar los datos',async state=>{
 const source=await PDFDocument.load(await buildWarehouseRequestPdf(input));expect(source.getPageCount()).toBe(1);
 source.addPage().drawText('Anexo histórico');const original=await source.save();
 const data={...item,status:state==='delivered'?'approved':state,receivedUtc:state==='pending'?undefined:item.receivedUtc,...(state==='delivered'?{warehouseDelivery:{number:'DEL-QA',deliveredBy:'Almacén QA',deliveredLogin:'almacen.qa',receivedBy:'Persona que retira',at:'2026-09-29T13:00:00Z'}}:{})} as DocumentDetail;
 const snapshot=JSON.stringify(data),text=vi.spyOn(PDFPage.prototype,'drawText');
 const result=await buildReceiptCopy(original,data,logo,'https://example.invalid');
 expect((await PDFDocument.load(result)).getPageCount()).toBe(1);expect((await PDFDocument.load(original)).getPageCount()).toBe(2);expect(JSON.stringify(data)).toBe(snapshot);
 const strings=text.mock.calls.map(call=>call[0]);expect(strings.join(' ')).not.toContain('en anexo');
 if(state==='delivered'){expect(strings).toContain('ENTREGADO');expect(strings).toContain('RECIBIDO')}
 if(state==='cancelled')expect(strings).toContain('ANULADO');
 if(process.env.PDF_SEAL_QA==='1'){mkdirSync('tmp/pdfs',{recursive:true});writeFileSync(`tmp/pdfs/almacen-una-hoja-${state}.pdf`,result)}
});
it('conserva los anexos de solicitudes administrativas',async()=>{const pdf=await PDFDocument.load(await buildWarehouseRequestPdf({...input,destination:'Administración'}));expect(pdf.getPageCount()).toBeGreaterThan(1)});
