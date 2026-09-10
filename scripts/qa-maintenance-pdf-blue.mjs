import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
mkdirSync('tmp/pdfs',{recursive:true});mkdirSync('output/pdf',{recursive:true});
const {buildMaintenancePdf}=await import(pathToFileURL(resolve('src/components/vite/maintenanceDocumentPdf.ts')));
const item={id:'demo',codigo:'DEMO-001',terminal:'Agencia de demostración',grupo:'Grupo de prueba',department:'TECHNOLOGY',technicianName:'Técnico de demostración',movementType:'TRANSFER_TO_WORKSHOP',equipmentType:'Impresora térmica',componentType:'Cabezal de impresión',serialNumber:'DEMO-SERIAL-001',quantity:1,failureCause:'No imprime correctamente. Se entrega a Taller para diagnóstico y reparación.',notes:'Documento de demostración. No acredita una recepción real.',createdByName:'Usuario de demostración',createdAt:'2026-09-03T17:30:00Z',occurredAt:'2026-09-03T13:30:00',operationalArea:'WAREHOUSE',documentNumber:'ALM-20260903133000-DEMO123',qrToken:'REAL-MNT|ALM-20260903133000-DEMO123',deliveredByName:'Operador de demostración',receivedByName:'Receptor indicado',destinationName:'Taller de Tecnología'};
const assets={realLogo:'data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64')};
const {createCanvas}=await import(pathToFileURL(resolve(process.argv[2])));
function sampleSignature(text){const canvas=createCanvas(800,240),ctx=canvas.getContext('2d');ctx.font='italic 58px Arial';ctx.fillStyle='#16334a';ctx.fillText(text,30,145);return canvas.toDataURL('image/png');}
const marks={stampX:109,stampY:191,receivedAt:'2026-09-03T18:30:00Z',receiverName:'Receptor de demostración',receiverLogin:'demo.recepcion',deliverySignature:{data:sampleSignature('Entrega - DEMO'),name:'Entregador de demostración',login:'demo.entrega',signedAt:'2026-09-03T18:25:00Z'},receiptSignature:{data:sampleSignature('Recibe - DEMO'),name:'Receptor de demostración',login:'demo.recepcion',signedAt:'2026-09-03T18:30:00Z'}};
const pdf=await buildMaintenancePdf(item,marks,assets);
for(let page=1;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFontSize(6);pdf.setTextColor(150,50,50);pdf.text('DEMOSTRACIÓN - FIRMAS FICTICIAS - NO VÁLIDO COMO RECEPCIÓN',105,5.8,{align:'center'});}
writeFileSync('output/pdf/formulario_institucional_sello_azul.pdf',Buffer.from(pdf.output('arraybuffer')));
const template=await buildMaintenancePdf(item,{stampX:109,stampY:191},{...assets,template:true});
mkdirSync('public/documents',{recursive:true});
writeFileSync('public/documents/plantilla-conduce-institucional.pdf',Buffer.from(template.output('arraybuffer')));
writeFileSync('output/pdf/plantilla_conduce_institucional.pdf',Buffer.from(template.output('arraybuffer')));
console.log('Muestra:',pdf.getNumberOfPages(),'páginas; plantilla:',template.getNumberOfPages(),'páginas');

