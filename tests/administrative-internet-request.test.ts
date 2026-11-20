import {readFileSync} from 'node:fs';
import {describe,expect,it} from 'vitest';
import {PDFDocument} from 'pdf-lib';
import {buildWarehouseRequestPdf} from '../src/components/vite/warehouseRequestPdf';
import {buildReceiptCopy} from '../src/components/vite/institutionalReceiptPdf';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
import {linkedTicketAgency} from '../src/components/vite/AdministrativeInternetRequestComposer';

const logo='data:image/png;base64,'+readFileSync('public/loto-real-logo-transparent.png').toString('base64');
const number='SOL-ADM-QA-372';
const original=()=>buildWarehouseRequestPdf({number,department:'Tecnología',destination:'Administración',agency:{codigo:'6010183',terminal:'Agencia QA',grupo:'Grupo QA'},priority:'MEDIUM',notes:'Cambio por avería de conectividad',lines:[{kind:'EQUIPMENT',name:'Módem de internet',quantity:2,serial:'',note:'Claro · banda ancha'}],createdBy:'Solicitante Avería Técnica',createdByLogin:'averia@grupotejeda.com',logoData:logo,origin:'https://ejemplo.test',internetDetails:{serviceKind:'BANDA_ANCHA',serviceNumber:'809-555-0100',company:'CLARO',modemQuantity:2,itemKind:'EQUIPO',reason:'Cambio por avería de conectividad'}});
const item={id:372,title:'Solicitud de equipo de internet',department:'Tecnología',ownerDisplayName:'Solicitante Avería Técnica',ownerUserName:'averia',recipientDepartment:'Administración',channel:'requirements',status:'approved',receivedUtc:'2026-09-17T20:00:00Z',decisionUtc:'2026-09-17T20:05:00Z',decisionBy:'Administración',receiverLogin:'admin',receiverDisplayName:'Administración',receiptCode:'REAL-REC-372',verificationToken:'a'.repeat(48),administrativeRequest:{number,assetType:'INTERNET',targetType:'AGENCIA',targetName:'Agencia QA',agencyCode:'6010183',internet:{agencyId:'11111111-1111-1111-1111-111111111111',agencyCode:'6010183',agencyName:'Agencia QA',agencyGroup:'Grupo QA',serviceKind:'BANDA_ANCHA',serviceNumber:'809-555-0100',company:'CLARO',modemQuantity:2,itemKind:'EQUIPO',changeReason:'Cambio por avería de conectividad'}},administrativeDelivery:{number,deliveredBy:'Administración',deliveredLogin:'admin',receivedBy:'Agencia QA',at:'2026-09-17T20:10:00Z',assetIdentifiers:['MODEM-001','MODEM-002']}} as DocumentDetail;

describe('solicitud de internet de Avería Técnica',()=>{
 it('usa el formulario institucional de Almacén con anexo de conectividad',async()=>{
  const pdf=await PDFDocument.load(await original());
  expect(pdf.getSubject()).toContain('REAL-ADMINISTRATIVE-REQUEST-V372');
  expect(pdf.getPageCount()).toBe(2);
 });
 it('incorpora sellos de autorización, entrega y recepción en el mismo comprobante',async()=>{
  const stamped=await buildReceiptCopy(new Uint8Array(await original()),item,logo,'https://ejemplo.test');
  const pdf=await PDFDocument.load(stamped);
  expect(pdf.getPageCount()).toBe(3);
  expect(stamped.length).toBeGreaterThan(10000);
 });
 it('vincula la agencia por código aunque el nombre del grupo tenga una variación',()=>{
  const agencies=[
   {id:'agency-1',codigo:'0632084',terminal:'Villa Mella Antiguo 400',grupo:'GRUPO SANTO'},
   {id:'agency-2',codigo:'999999',terminal:'Otra agencia',grupo:'GRUPO SANTOS'},
  ];
  const ticket={id:'ticket-1',ticketNumber:2799,codigo:'632084',terminal:'632084 | Villa Mella Antiguo 400',grupo:'GRUPO SANTOS',subject:'Falla en módem'};
  expect(linkedTicketAgency(agencies,ticket)?.id).toBe('agency-1');
 });
});
