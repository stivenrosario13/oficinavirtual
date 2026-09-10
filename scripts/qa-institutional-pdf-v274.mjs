import fs from 'node:fs';
import {createRequire} from 'node:module';
import {buildInstitutionalLetter,newLetterForm} from '../src/components/vite/institutionalLetterPdf.ts';
import {buildReceiptCopy} from '../src/components/vite/institutionalReceiptPdf.ts';
const require=createRequire(import.meta.url),{createCanvas}=require(process.argv[2]);
function demo(text){const canvas=createCanvas(800,240);const ctx=canvas.getContext('2d');ctx.fillStyle='#214d6a';ctx.font='italic 52px sans-serif';ctx.fillText(text,35,130);return canvas.toDataURL('image/png')}
const logo='data:image/png;base64,'+fs.readFileSync('public/loto-real-logo-transparent.png').toString('base64');
const form={...newLetterForm(),departmentHeading:'ALMACÉN CENTRAL',referenceCode:'REAL-DEMO-0001',documentDate:'03 de septiembre de 2026',recipientName:'Departamento de Tecnología',recipientTitle:'Responsable del departamento',subject:'Solicitud de revisión y entrega de equipos',body:'Por medio de la presente se remite el equipo detallado para su revisión y seguimiento.\n\nEquipo: impresora de prueba\nCantidad: 1 unidad\nSerial: DEMO-001\n\nSe solicita verificar el estado del equipo y registrar la recepción desde la cuenta de la persona responsable.\n\nDOCUMENTO DE DEMOSTRACIÓN. No acredita una entrega real.',closing:'Atentamente,',signerName:'Entrega DEMO',signerTitle:'Responsable de Almacén'};
const source=buildInstitutionalLetter(form,demo('Entrega - DEMO'),logo,false,{x:65,y:65},'entrega.demo');
const original=new Uint8Array(source.output('arraybuffer'));
const received=await buildReceiptCopy(original,{title:form.subject,receivedUtc:'2026-09-03T16:35:00Z',receiverLogin:'recibe.demo',receiverDisplayName:'Recepción DEMO',receiptCode:'REAL-REC-DEMO',verificationToken:'demostracion-sin-validez',sealX:59,sealY:66,status:'received',receiptSignature:demo('Recibe - DEMO')},logo,'https://example.invalid');
fs.mkdirSync('output/pdf',{recursive:true});fs.writeFileSync('output/pdf/comunicacion_real_v274.pdf',received);
console.log('Generated V274 communication QA PDF with two DEMO signatures.');
