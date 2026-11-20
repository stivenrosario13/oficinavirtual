import {describe,expect,it} from 'vitest';
import {Workbook} from 'exceljs';
import {administrativeColumns,administrativeRequiredColumns,administrativeTemplate,formFields,parseAdministrativeWorkbook,type AdministrativeFormKind} from '../src/lib/administrative-form-excel';
import {administrativeDefaults} from '../src/lib/administrative-form-defaults';
import {prepareAdministrativeForm,validateAdministrativeForm} from '../src/lib/administrative-form-data';
import {administrativeDraftFromDocument,validateInternetDraft} from '../src/lib/administrative-form-save';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';

async function workbook(kind:AdministrativeFormKind,rows:Record<string,unknown>[]){const book=new Workbook(),sheet=book.addWorksheet('Formularios');sheet.columns=administrativeColumns(kind).map(column=>({header:column.label,key:column.key}));rows.forEach(row=>sheet.addRow(row));return book}
describe('plantillas e importación administrativa',()=>{
  it.each((Object.keys(formFields) as AdministrativeFormKind[]).filter(kind=>kind!=='gas_log'))('lee el Excel real de %s sin columnas ambiguas',async kind=>{
    const columns=administrativeColumns(kind);
    const required=administrativeRequiredColumns(kind);
    expect(new Set(columns.map(c=>c.label)).size).toBe(columns.length);
    expect(new Set(columns.map(c=>c.key)).size).toBe(columns.length);
    expect(required.length).toBeGreaterThan(0);
    const template=await administrativeTemplate(kind),templateSheet=template.getWorksheet('Formularios')!;
    expect(template.worksheets).toHaveLength(1);
    expect(templateSheet.getRow(1).cellCount).toBe(required.length);
    const source=await workbook(kind,[{[required[0].key]:required[0].key.endsWith('Date')||required[0].key==='line.date'?'2026-11-05':'Prueba'}]);
    const restored=new Workbook();await restored.xlsx.load(await source.xlsx.writeBuffer());
    const forms=parseAdministrativeWorkbook(restored,kind);
    expect(forms).toHaveLength(1);expect(forms[0].group).toBe('Fila 2');
    expect(forms[0].number).toMatch(/^SOL-ADM-/);
  });
  it('crea la plantilla GAS con el formato institucional de cuatro columnas',async()=>{
    const book=await administrativeTemplate('gas_log'),sheet=book.getWorksheet('RELACION GAS')!;
    expect(sheet.getCell('A1').value).toBe('RELACION DE LLENADO DE VEHICULOS DE GAS');
    expect(['A3','B3','C3','D3'].map(address=>sheet.getCell(address).value)).toEqual(['FECHA','PLACA','MONTO','GESTOR RUTA']);
    expect(book.worksheets).toHaveLength(1);expect(sheet.getRow(3).values).toEqual([undefined,'FECHA','PLACA','MONTO','GESTOR RUTA']);
  });
  it('importa cada hoja mensual GAS aunque el encabezado cambie de fila y columna',()=>{
    const book=new Workbook(),april=book.addWorksheet('ABRIL 2026'),may=book.addWorksheet('MAYO2026');
    april.getRow(8).values=[null,'FECHA','PLACA','MONTO','GESTOR RUTA'];april.getRow(9).values=[null,new Date('2026-04-02T00:00:00Z'),'I001',1250,'RUTA NORTE'];april.getRow(10).values=[null,'03/04/2026','I002','2,500.50','RUTA SUR'];april.getRow(11).values=[null,'','TOTAL',{formula:'SUM(D9:D10)',result:3750.5},''];
    may.getRow(5).values=['FECHA','PLACA','MONTO','GESTOR RUTA'];may.getRow(6).values=['01/05/2026','L001',900,'GESTOR 1'];
    const forms=parseAdministrativeWorkbook(book,'gas_log');
    expect(forms).toHaveLength(2);expect(forms[0]).toMatchObject({group:'ABRIL 2026',sourceRows:[9,10],values:{fuelPeriod:'2026-04'}});
    expect(forms[0].rows).toEqual([{date:'2026-04-02',plate:'I001',amount:'1250',manager:'RUTA NORTE',model:'',department:''},{date:'2026-04-03',plate:'I002',amount:'2500.5',manager:'RUTA SUR',model:'',department:''}]);
    expect(forms[1].values.fuelPeriod).toBe('2026-05');
  });
  it('mantiene visibles las filas GAS inválidas para poder corregirlas antes de importar',()=>{
    const book=new Workbook(),sheet=book.addWorksheet('NOVIEMBRE 2026');sheet.addRow(['FECHA','PLACA','MONTO','GESTOR RUTA']);sheet.addRow(['20/05/202','I001',1000,'RUTA']);
    const parsed=parseAdministrativeWorkbook(book,'gas_log');expect(parsed[0].rows[0].date).toBe('20/05/202');
  });
  it('agrupa vehículos sin confundir el grupo empresarial con el identificador',async()=>{
    const book=await workbook('marbete',[{_form:'M1',group:'Grupo A','line.plate':'000123','line.chassis':'001234'}, {_form:'M1','line.plate':'000456','line.chassis':'005678'},{_form:'M2',group:'Grupo B','line.plate':'789','line.chassis':'789'}]);
    const result=parseAdministrativeWorkbook(book,'marbete');
    expect(result).toHaveLength(2);expect(result[0].values.group).toBe('Grupo A');expect(result[0].rows).toHaveLength(2);expect(result[0].sourceRows).toEqual([2,3]);expect(result[0].rows[0].plate).toBe('000123');
  });
  it('crea formularios distintos en filas sin identificador',async()=>{expect(parseAdministrativeWorkbook(await workbook('general',[{targetName:'A'},{targetName:'B'}]),'general')).toHaveLength(2)});
  it('no combina beneficiarios contradictorios bajo el mismo identificador',async()=>{
    const book=await workbook('general',[{_form:'A',targetName:'Ana'},{_form:'A',targetName:'Luis'}]);
    expect(()=>parseAdministrativeWorkbook(book,'general')).toThrow('valores distintos');
  });
  it('conserva teléfonos y ceros iniciales, y reconoce fechas Excel',async()=>{
    const book=await workbook('contract_request',[{agencyCode:'00123',ownerId:'00012345678',requiredDate:new Date('2026-10-28T00:00:00Z')}]);
    const parsed=parseAdministrativeWorkbook(book,'contract_request')[0];expect(parsed.values.agencyCode).toBe('00123');expect(parsed.values.ownerId).toBe('00012345678');expect(parsed.values.requiredDate).toBe('2026-10-28');
    const lines=parseAdministrativeWorkbook(await workbook('sim',[{_form:'SIM1','line.number':'08090001234'},{_form:'SIM1','line.number':'08090005678'}]),'sim');expect(lines[0].phones.map(p=>p.number)).toEqual(['08090001234','08090005678']);
  });
  it('rechaza fórmulas, columnas desconocidas y encabezados repetidos',async()=>{
    const book=await workbook('payment',[{amount:{formula:'1+1',result:2}}]);expect(()=>parseAdministrativeWorkbook(book,'payment')).toThrow('sin fórmulas');
    const sheet=book.getWorksheet('Formularios')!;sheet.getCell('A1').value='Columna ajena';expect(()=>parseAdministrativeWorkbook(book,'payment')).toThrow('desconocida');
    sheet.getCell('A1').value=sheet.getCell('B1').value;expect(()=>parseAdministrativeWorkbook(book,'payment')).toThrow('repetida');
  });
  it('rechaza archivos vacíos y admite cargas administrativas extensas',async()=>{
    expect(()=>parseAdministrativeWorkbook(new Workbook(),'general')).toThrow('no contiene hojas');
    const book=await workbook('general',[]);expect(()=>parseAdministrativeWorkbook(book,'general')).toThrow('No hay datos');
    const many=await workbook('general',Array.from({length:101},(_,i)=>({targetName:`Persona ${i}`})));expect(parseAdministrativeWorkbook(many,'general')).toHaveLength(101);
    const tooMany=await workbook('general',[]);tooMany.getWorksheet('Formularios')!.getCell('A50002').value='Fuera del límite';expect(()=>parseAdministrativeWorkbook(tooMany,'general')).toThrow('50,000 filas');
  });
  it('divide automáticamente relaciones mayores de 100 detalles',async()=>{
    const rows=Array.from({length:205},(_,i)=>({_form:'RELACION GRANDE','line.date':'2026-11-04','line.plate':`P${i}`,'line.amount':'100'}));
    const parsed=parseAdministrativeWorkbook(await workbook('fuel_log',rows),'fuel_log');expect(parsed.map(item=>item.rows.length)).toEqual([100,100,5]);expect(parsed[2].group).toContain('parte 3 de 3');
  });
  it('no pierde las secciones de combustible de las importaciones históricas',()=>{
    const draft=administrativeDraftFromDocument({department:'Administración',administrativeRequest:{number:'SOL-ADM-COMB-1',targetName:'Combustible',formData:{fuelDate:'2026-09-23',sections:[{name:'Servicios Generales',rows:[{cells:{B:'000123',C:'Camión',D:'Gestor'},amount:1250},{cells:{B:12},amount:null}]}]}}} as unknown as DocumentDetail);
    expect(draft.rows).toHaveLength(2);expect(draft.rows[0]).toMatchObject({plate:'000123',department:'Servicios Generales',amount:'1250',date:'2026-09-23'});expect(draft.rows[1].amount).toBe('');
  });
  it('valida todas las filas con monto y los períodos de contrato',()=>{
    const base=administrativeDefaults('fuel_log',{displayName:'QA'});const rows=[{date:'2026-10-28',plate:'P1',amount:'1250.50'},{date:'2026-02-30',plate:'P2',amount:'50'}];
    const prepared=prepareAdministrativeForm('fuel_log',base.values,rows,[],'Administración',{displayName:'QA'});
    expect(validateAdministrativeForm('fuel_log',base.values,rows,[],prepared)).toContain('fecha válida');
    rows[1].date='2026-10-28';expect(validateAdministrativeForm('fuel_log',base.values,rows,[],prepared)).toBe('');
  });
  it('valida cantidades de módem y opciones de internet',()=>{
    const values={agencyCode:'001',serviceKind:'BANDA_ANCHA',serviceNumber:'001',company:'CLARO',itemKind:'EQUIPO',modemQuantity:'2',changeReason:'Cambio por avería'};
    expect(validateInternetDraft(values)).toBe('');expect(validateInternetDraft({...values,modemQuantity:'2.5'})).toContain('Cantidad');expect(validateInternetDraft({...values,itemKind:'SIM',modemQuantity:'0'})).toBe('');
  });
  it('prepara y valida el flujo de prestaciones, cheques en blanco y cheques firmados',()=>{
    const session={displayName:'Usuario QA'};
    const benefits=administrativeDefaults('labor_benefits_request',session);
    benefits.rows=[{agency:'632626',beneficiaryName:'Persona beneficiaria',exitDate:'2026-11-10',exitReason:'Desahucio',amount:'14650.96',contact:'809-555-0101',paymentMethod:'Cheque'}];
    const benefitsPrepared=prepareAdministrativeForm('labor_benefits_request',benefits.values,benefits.rows,[],'Recursos Humanos',session);
    expect(benefitsPrepared).toMatchObject({target:'Contabilidad',title:'Solicitud de pago de prestaciones laborales en cheque'});
    expect(validateAdministrativeForm('labor_benefits_request',benefits.values,benefits.rows,[],benefitsPrepared)).toBe('');

    const blank=administrativeDefaults('blank_checks_delivery',session);blank.rows=[{checkNumber:'000123',concept:'Prestaciones laborales'}];
    expect(prepareAdministrativeForm('blank_checks_delivery',blank.values,blank.rows,[],'Administración',session).target).toBe('Contabilidad');
    expect(validateAdministrativeForm('blank_checks_delivery',blank.values,blank.rows,[],prepareAdministrativeForm('blank_checks_delivery',blank.values,blank.rows,[],'Administración',session))).toBe('');

    const signed=administrativeDefaults('signed_benefits_checks_delivery',session);signed.rows=[{checkNumber:'000123',amount:'14650.96',beneficiaryName:'Persona beneficiaria',beneficiaryId:'001-0000000-0',agencyGroup:'632626',concept:'Prestaciones laborales'}];
    const signedPrepared=prepareAdministrativeForm('signed_benefits_checks_delivery',signed.values,signed.rows,[],'Administración',session);
    expect(signedPrepared.target).toBe('Caja');expect(signedPrepared.fields).toContainEqual({label:'Total de cheques',value:'1'});
    expect(validateAdministrativeForm('signed_benefits_checks_delivery',signed.values,signed.rows,[],signedPrepared)).toBe('');
  });
});
