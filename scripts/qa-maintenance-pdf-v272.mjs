import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
mkdirSync('tmp/pdfs',{recursive:true});mkdirSync('output/pdf',{recursive:true});
const {buildMaintenancePdf}=await import(pathToFileURL(resolve('src/components/vite/maintenanceDocumentPdf.ts')));
const item={id:'demo',codigo:'DEMO-001',terminal:'Agencia de demostración',grupo:'Grupo de prueba',department:'TECHNOLOGY',technicianName:'Técnico de demostración',movementType:'TRANSFER_TO_WORKSHOP',equipmentType:'Impresora térmica',componentType:'Cabezal de impresión',serialNumber:'DEMO-SERIAL-001',quantity:1,failureCause:'No imprime correctamente. Se entrega a Taller para diagnóstico y reparación.',notes:'Documento de demostración. No acredita una recepción real.',createdByName:'Usuario de demostración',createdAt:'2026-09-03T17:30:00Z',occurredAt:'2026-09-03T13:30:00',operationalArea:'WAREHOUSE',documentNumber:'ALM-20260903133000-DEMO123',qrToken:'REAL-MNT|ALM-20260903133000-DEMO123',deliveredByName:'Operador de demostración',receivedByName:'Receptor indicado',destinationName:'Taller de Tecnología'};
const assets={realLogo:'data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64')};
for(const mode of ['recibido']){
 const marks=mode==='pendiente'?{stampX:109,stampY:237}:{stampX:109,stampY:237,receivedAt:'2026-09-03T18:30:00Z',receiverName:'Receptor de demostración',receiverLogin:'demo.recepcion'};
 const pdf=await buildMaintenancePdf(mode==='anexo'?{...item,notes:'Nota larga de demostración que debe conservarse completa. '.repeat(100)+'FIN DE NOTAS'}:item,marks,assets);
 for(let page=1;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFontSize(6);pdf.setTextColor(150,50,50);pdf.text('DEMOSTRACIÓN · DATOS FICTICIOS · NO VÁLIDO COMO RECEPCIÓN',105,5.8,{align:'center'});}
 writeFileSync(`output/pdf/formulario_v272_${mode}.pdf`,Buffer.from(pdf.output('arraybuffer')));
 console.log(mode,pdf.getNumberOfPages(),'páginas');
}
