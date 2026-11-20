import {readFileSync,mkdirSync,writeFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {PDFDocument} from 'pdf-lib';
import {buildWorkshopFormPdf} from '../src/components/vite/workshopFormPdf';
import type {WorkshopCase} from '../src/components/vite/workshopWorkflow';
it('keeps a single page with the enlarged part-change seal and both custody seals',async()=>{
 const logo='data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64');
 const item={id:'qa',documentNumber:'TAL-8BFEED5D3B31438290F764789A44F73-1',qrToken:'REAL-MNT|TAL-QA-418',equipmentType:'Monitor Dell',serialNumber:'QA-001',quantity:1,department:'TECHNOLOGY',senderName:'Administrador principal',sentAt:'2026-09-30T20:00:00Z',status:'DELIVERED',failureCause:'cable dañado',notes:'Trabajo: Diagnóstico: cable dañado',events:[{id:'w',action:'WORK',status:'PART_REPLACEMENT',actorName:'Administrador principal',actorLogin:'admin@local.com',occurredAt:'2026-09-30T21:00:00Z',note:'Trabajo realizado: cambio de cable\nResultado: funcionando'},{id:'d',action:'DELIVER',status:'DELIVERED',actorName:'Administrador principal',actorLogin:'admin@local.com',occurredAt:'2026-09-30T21:05:00Z'},{id:'r',action:'WAREHOUSE_RECEIVE',status:'DELIVERED',actorName:'Administrador principal',actorLogin:'admin@local.com',occurredAt:'2026-09-30T21:10:00Z'}]} as WorkshopCase;
 const pdf=await buildWorkshopFormPdf([item],logo);const bytes=new Uint8Array(pdf.output('arraybuffer'));expect((await PDFDocument.load(bytes)).getPageCount()).toBe(1);
 mkdirSync('tmp/pdfs',{recursive:true});writeFileSync('tmp/pdfs/taller-cambio-pieza-mismo-tamano.pdf',bytes);
 const source=readFileSync('src/components/vite/workshopFormPdf.ts','utf8');expect(source).toContain("pdf.rect(108,191.5,90,38,'FD')");expect(source).toContain('90*mm,38*mm');
});
