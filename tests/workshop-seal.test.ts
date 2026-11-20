import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {PDFDocument} from 'pdf-lib';
import {buildMaintenancePdf,type MaintenanceDocumentMovement} from '../src/components/vite/maintenanceDocumentPdf';
it('renders workshop custody stamps in the reserved first-page boxes and preserves signatures',async()=>{
 const logo='data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64');
 const item:MaintenanceDocumentMovement={id:'qa',codigo:'INTERNO',terminal:'Taller',grupo:'Grupo Tejeda',department:'TECHNOLOGY',technicianName:'Taller',movementType:'COMPONENT_REPLACEMENT',equipmentType:'Monitor Dell',serialNumber:'QA-TALLER-001',quantity:1,failureCause:'Fuente dañada. Se reemplaza la pieza y se prueba.',createdByName:'Usuario QA',createdAt:'2026-09-29T16:00:00Z',occurredAt:'2026-09-29T16:00:00Z',operationalArea:'WORKSHOP',documentNumber:'TAL-QA-418',qrToken:'REAL-MNT|TAL-QA-418',deliveredByName:'Encargado de Taller',receivedByName:'Encargado de Almacén'};
 const pdf=await buildMaintenancePdf(item,{stampX:109,stampY:193,receivedAt:'2026-09-29T17:00:00Z',receiverName:'Encargado de Almacén',receiverLogin:'almacen.qa'},{realLogo:logo});
 const bytes=new Uint8Array(pdf.output('arraybuffer'));expect((await PDFDocument.load(bytes)).getPageCount()).toBe(1);
 if(process.env.PDF_SEAL_QA==='1'){mkdirSync('tmp/pdfs',{recursive:true});writeFileSync('tmp/pdfs/taller-sellos-v418.pdf',bytes);}
});
