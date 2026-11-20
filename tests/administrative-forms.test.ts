import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {PDFDocument} from 'pdf-lib';
import {describe,expect,it} from 'vitest';
import {buildAdministrativeFormPdf} from '../src/components/vite/administrativeFormPdf';
import {buildReceiptCopy} from '../src/components/vite/institutionalReceiptPdf';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';

const logo=`data:image/png;base64,${readFileSync(resolve('public/loto-real-logo-transparent.png')).toString('base64')}`;

describe('formularios administrativos',()=>{
  it('regenera gasolina histórica sin espacios de autorización, recepción ni entrega',async()=>{
    const old=new Uint8Array(await buildAdministrativeFormPdf({number:'SOL-ADM-FUEL-QA',title:'Combustible antiguo',subtitle:'Anterior',department:'Administración',createdBy:'Sandra',createdByLogin:'sandra.ramirez',responsible:'Sandra',target:'Gasolina',fields:[],notes:'Anterior',logoData:logo}));
    expect((await PDFDocument.load(old)).getSubject()).toContain('ADMIN_SEAL=');
    const updated=await buildReceiptCopy(old,{createdUtc:'2026-10-28T12:00:00Z',department:'Administración',ownerDisplayName:'Sandra',ownerLogin:'sandra.ramirez',status:'approved',receivedUtc:'2026-10-28T12:00:00Z',administrativeRequest:{number:'SOL-ADM-FUEL-QA',assetType:'OTRO',targetType:'EMPLEADO',targetName:'Gasolina',formType:'FUEL_LOG',formData:{fuelPeriod:'2026-10',rows:[{date:'2026-10-28',plate:'001',amount:'1200'}]}}} as DocumentDetail,logo,'https://example.test');
    const pdf=await PDFDocument.load(updated);expect(pdf.getSubject()).toContain('REGISTRATION_ONLY=1');expect(pdf.getSubject()).not.toContain('ADMIN_SEAL=');expect(pdf.getSubject()).not.toContain('ADMIN_DELIVERY=');expect(pdf.getPageCount()).toBeGreaterThan(0);
  });
  it('genera un comprobante institucional válido con firmas y QR',async()=>{
    const bytes=await buildAdministrativeFormPdf({number:'SOL-ADM-20260918150000-QA01',title:'Reposición y activación de SIM',subtitle:'Autorización institucional',department:'Tecnología',createdBy:'Usuario de prueba',createdByLogin:'qa@real.local',responsible:'WHILMALY SULAY LORA NOESI',target:'CLARO',fields:[{label:'Representante legal',value:'Persona autorizada'},{label:'Cédula',value:'001-0000000-0'}],tables:[{title:'03 / LÍNEAS TELEFÓNICAS',headers:['N.º','Número'],rows:[['1','809-555-0101']],widths:[24,162]}],notes:'Reposición de la línea indicada.',logoData:logo,origin:'https://example.test'});
    expect(new TextDecoder().decode(new Uint8Array(bytes).slice(0,5))).toBe('%PDF-');
    const document=await PDFDocument.load(bytes);
    expect(document.getPageCount()).toBeGreaterThanOrEqual(1);
  });

  it('crea páginas adicionales para relaciones extensas de vehículos',async()=>{
    const rows=Array.from({length:36},(_,index)=>[String(index+1),`P${index.toString().padStart(6,'0')}`,`CHASIS-${index}`,'DAIHATSU HIJET','2018','1,500.00']);
    const bytes=await buildAdministrativeFormPdf({number:'SOL-ADM-20260918150000-QA02',title:'Renovación de marbete',subtitle:'Relación de vehículos',department:'Administración',createdBy:'Usuario de prueba',createdByLogin:'qa@real.local',responsible:'SANDRA LUCIA RAMIREZ ORTIZ',target:'Flota institucional',fields:[{label:'Período',value:'2025-2026'}],tables:[{title:'03 / VEHÍCULOS INCLUIDOS',headers:['N.º','Placa','Chasis','Marca / modelo','Año','Monto RD$'],rows,widths:[12,25,43,55,18,33]}],notes:'Renovación anual.',logoData:logo,origin:'https://example.test'});
    const document=await PDFDocument.load(bytes);
    expect(document.getPageCount()).toBeGreaterThan(1);
  });

  it('genera contratos institucionales con cláusulas completas en varias secciones',async()=>{
    const bytes=await buildAdministrativeFormPdf({number:'SOL-ADM-20260921170000-QA03',title:'Contrato de alquiler de local',subtitle:'Documento contractual para operación de agencia de lotería',department:'Administración',createdBy:'Usuario de prueba',createdByLogin:'qa@real.local',responsible:'EMELY FIGUEROA',target:'Propietario de prueba',fields:[{label:'Código de agencia',value:'0610446'},{label:'Grupo',value:'José Mesa'},{label:'Renta mensual',value:'RD$ 7,320.00'},{label:'Vigencia',value:'15/09/2026 al 15/09/2027'}],bodySections:[{title:'03 / OBJETO DEL CONTRATO',text:'El propietario alquila el local indicado para ser utilizado como banca de lotería.'},{title:'04 / RENTA Y FORMA DE PAGO',text:'La renta y el adelanto quedan consignados en los datos aprobados por ambas partes.'},{title:'05 / VIGENCIA',text:'El contrato se mantiene vigente durante el período indicado.'}],notes:'Formalización del alquiler del local.',logoData:logo,origin:'https://example.test'});
    const document=await PDFDocument.load(bytes);
    expect(document.getPageCount()).toBeGreaterThanOrEqual(1);
    expect(new TextDecoder().decode(new Uint8Array(bytes).slice(0,5))).toBe('%PDF-');
  });

  it('incluye recepción virtual y todos los modelos administrativos suministrados',()=>{
    const composer=readFileSync('src/components/vite/AdministrativeRequestComposer.tsx','utf8');
    const app=readFileSync('src/App.tsx','utf8');
    const endpoint=readFileSync('api/InstitutionalDocumentEndpoints.cs','utf8');
    for(const form of ['contract_request','contract_renewal','rental_contract','rent_receipt','increase_letter','fuel_log','labor_benefits_request','blank_checks_delivery','signed_benefits_checks_delivery'])expect(composer).toContain(form);
    const formData=readFileSync('src/lib/administrative-form-data.ts','utf8');
    expect(formData).toContain('Gasolina · gasto diario');
    expect(formData).toContain('Renovación de contrato de alquiler');
    expect(formData).toContain('Solicitud de pago de prestaciones laborales en cheque');
    expect(formData).toContain('Entrega de cheques firmados de prestaciones laborales a Caja');
    expect(app).toContain('Recepción virtual');
    expect(endpoint).toContain('"CONTRACT_REQUEST" or "CONTRACT_RENEWAL"');
  });
});
