import labels from './administrative-form-labels.json';
import type {Row} from './administrative-form-data';
import type {Cell, Workbook} from 'exceljs';

export const formFields = {
  sim:'company carrier legalRepresentative representativeId authorizedPerson authorizedId action accountCharge notes',
  marbete:'period group listOwner dueDate notes',
  device:'recipient recipientId recipientDepartment model chip imei penalty condition notes',
  fleet:'recipient recipientId recipientDepartment model chip imei penalty condition notes',
  modem:'agencyName agencyCode model imei notes',
  payment:'payee payeeRnc bank accountNumber accountType amount policy insurancePeriod concept',
  general:'assetType targetType targetName agencyCode reason',
  contract_request:'supervisorName supervisorGroup ownerName ownerId ownerPhone agencyCode agencyName agencyGroup agencySector agencyAddress monthlyRent requiredDate requestReason',
  contract_renewal:'supervisorName supervisorGroup ownerName ownerId ownerPhone agencyCode agencyName agencyGroup agencySector agencyAddress currentEndDate renewalStartDate renewalEndDate paymentFrequency currentRent newRent increasePercent renewalReason',
  rental_contract:'agencyCode agencyGroup ownerName ownerId ownerRepresentative ownerRepresentativeId tenantCompany tenantRnc tenantRepresentative tenantRepresentativeId agencyAddress monthlyRent advanceMonths advanceAmount contractStart contractEnd signingPlace signingDate notaryName notaryNumber paymentTerms renewalTerms serviceTerms contractPurpose',
  rent_receipt:'agencyCode agencyGroup receiptDate paidBy amountWords amount periodStart periodEnd agencyAddress receivedBy receiverId receiverRole paymentConcept',
  increase_letter:'place requestDate recipientName recipientRole ownerName ownerId agencyCode agencyGroup increasePercent increaseYear increaseReason',
  fuel_log:'fuelPeriod fuelArea fuelOwner fuelNotes',
  gas_log:'fuelPeriod fuelArea fuelBudget physicalTickets fuelCredit fuelOwner fuelNotes',
  internet:'agencyCode serviceKind serviceNumber company itemKind modemQuantity changeReason',
  incident_report:'reportDate preparedBy incidentDepartment subject groupName',
  labor_benefits_request:'requestDate reference notes',
  blank_checks_delivery:'deliveryDate deliveryNumber relatedRequestNumber deliveredBy notes',
  signed_benefits_checks_delivery:'deliveryDate deliveryNumber relatedRequestNumber deliveredBy notes',
} as const;
export type AdministrativeFormKind = keyof typeof formFields;
export type AdministrativeDraft = {values:Row; rows:Row[]; phones:Row[]; number?:string};
export type ImportedForm = AdministrativeDraft & {group:string; sourceRows:number[]};
const extra:Record<string,string> = {ownerRepresentativeId:'Cédula del representante del propietario',tenantRepresentativeId:'Cédula del representante de la empresa',responsible:'Responsable de recepción',carrier:'Compañía telefónica',accountCharge:'Cargo a cuenta',paymentFrequency:'Forma de pago',assetType:'Tipo de solicitud',targetType:'Tipo de destino',requestReason:'Motivo del contrato',renewalReason:'Motivo de renovación',serviceKind:'Tipo de servicio',serviceNumber:'Número de servicio',company:'Empresa / compañía',itemKind:'Equipo o SIM',modemQuantity:'Cantidad de módems',changeReason:'Motivo del cambio',incidentDepartment:'Departamento',groupName:'Grupo del supervisor',generalObservations:'Observación general'};
const lineFields:Partial<Record<AdministrativeFormKind,string>>={sim:'number',marbete:'plate chassis model year amount',payment:'plate chassis model year',fuel_log:'date plate model department amount manager',gas_log:'date plate model department amount manager',incident_report:'agencyId ticketDate ticketCode agentPhone',labor_benefits_request:'agency beneficiaryName exitDate exitReason amount contact paymentMethod',blank_checks_delivery:'checkNumber concept',signed_benefits_checks_delivery:'checkNumber amount beneficiaryName beneficiaryId agencyGroup concept'};
const lineLabels:Record<string,string>={number:'Número de línea',plate:'Placa',chassis:'Chasis',model:'Modelo',year:'Año',amount:'Monto RD$',date:'Fecha de consumo',department:'Departamento',manager:'Gestor de ruta',agencyId:'ID agencia',ticketDate:'Fecha del ticket',ticketCode:'Número de ticket',agentPhone:'Contacto de la agente de venta',agency:'Sucursal / agencia',beneficiaryName:'Nombre del beneficiario',exitDate:'Fecha de salida',exitReason:'Motivo de salida',contact:'Contacto',paymentMethod:'Forma de pago',checkNumber:'Número de cheque',beneficiaryId:'Cédula',agencyGroup:'Grupo o agencia',concept:'Concepto'};
const requiredTemplateFields:Record<AdministrativeFormKind,string[]>={
  sim:['legalRepresentative','authorizedPerson','line.number'],
  marbete:['line.plate','line.chassis'],
  device:['recipient','recipientId','model','imei'],
  fleet:['recipient','model','imei'],
  modem:['agencyName','agencyCode'],
  payment:['payee','bank','accountNumber','amount','concept'],
  general:['targetName','reason'],
  contract_request:['ownerName','ownerId','ownerPhone','agencyCode','agencyAddress'],
  contract_renewal:['ownerName','ownerId','ownerPhone','agencyCode','agencyAddress','currentEndDate','renewalStartDate','renewalEndDate'],
  rental_contract:['ownerName','tenantCompany','monthlyRent','contractStart','contractEnd'],
  rent_receipt:['receivedBy','paidBy','amount','receiptDate'],
  increase_letter:['recipientName','ownerName','increaseYear'],
  fuel_log:['fuelArea','line.date','line.plate','line.amount'],
  gas_log:['line.date','line.plate','line.amount','line.manager'],
  internet:['agencyCode','serviceNumber','changeReason'],
  incident_report:['reportDate','preparedBy','incidentDepartment','subject','groupName','line.agencyId','line.ticketDate','line.ticketCode','line.agentPhone'],
  labor_benefits_request:['requestDate','line.agency','line.beneficiaryName','line.exitDate','line.exitReason','line.amount'],
  blank_checks_delivery:['deliveryDate','deliveredBy','line.checkNumber','line.concept'],
  signed_benefits_checks_delivery:['deliveryDate','deliveredBy','line.checkNumber','line.amount','line.beneficiaryName','line.beneficiaryId','line.agencyGroup','line.concept'],
};
export function administrativeColumns(kind:AdministrativeFormKind){
  return [{key:'_form',label:'Formulario'},...['responsible',...formFields[kind].split(' ')].filter(key=>kind!=='internet'||key!=='responsible').map(key=>({key,label:extra[key]||(labels as Record<string,{label:string}>)[key]?.label||key})),...(lineFields[kind]?.split(' ')||[]).map(key=>({key:`line.${key}`,label:`Detalle: ${lineLabels[key]}`}))];
}
export function administrativeRequiredColumns(kind:AdministrativeFormKind){
  const byKey=new Map(administrativeColumns(kind).map(column=>[column.key,column]));
  return requiredTemplateFields[kind].map(key=>byKey.get(key)!).filter(Boolean);
}
const normalize=(s:string)=>s.normalize('NFD').replace(/[\u0300-\u036f]/g,'').trim().toLowerCase();
export function isAdministrativeFormKind(kind:string):kind is AdministrativeFormKind{return Object.hasOwn(formFields,kind)}
export function newAdministrativeNumber(){return `SOL-ADM-${new Date().toISOString().replace(/[-:TZ.]/g,'').slice(0,14)}-${crypto.randomUUID().slice(0,8).toUpperCase()}`}

export async function administrativeTemplate(kind:AdministrativeFormKind){
  const {Workbook}=await import('exceljs');const book=new Workbook();
  if(kind==='gas_log'){
    const sheet=book.addWorksheet('RELACION GAS');
    sheet.mergeCells('A1:D1');sheet.getCell('A1').value='RELACION DE LLENADO DE VEHICULOS DE GAS';
    sheet.getCell('A1').font={bold:true,size:15,color:{argb:'FFFFFFFF'}};sheet.getCell('A1').alignment={horizontal:'center',vertical:'middle'};sheet.getCell('A1').fill={type:'pattern',pattern:'solid',fgColor:{argb:'FF0B2942'}};sheet.getRow(1).height=34;
    const headers=['FECHA','PLACA','MONTO','GESTOR RUTA'];
    headers.forEach((header,index)=>{const cell=sheet.getCell(3,index+1);cell.value=header;cell.font={bold:true,color:{argb:'FFFFFFFF'}};cell.alignment={horizontal:'center'};cell.fill={type:'pattern',pattern:'solid',fgColor:{argb:'FF176789'}};cell.border={bottom:{style:'thin',color:{argb:'FF75DDF0'}}};});
    sheet.columns=[{width:17},{width:20},{width:18},{width:42}];sheet.views=[{state:'frozen',ySplit:3}];sheet.autoFilter={from:'A3',to:'D3'};
    for(let row=4;row<=103;row++){
      sheet.getCell(row,1).numFmt='dd/mm/yyyy';sheet.getCell(row,3).numFmt='#,##0.00';
      for(let column=1;column<=4;column++){const cell=sheet.getCell(row,column);cell.fill={type:'pattern',pattern:'solid',fgColor:{argb:row%2?'FF102E47':'FF0B2238'}};cell.font={color:{argb:'FFFFFFFF'}};cell.border={bottom:{style:'hair',color:{argb:'FF365D76'}}};}
    }
    return book;
  }
  const sheet=book.addWorksheet('Formularios');const columns=administrativeRequiredColumns(kind);
  sheet.columns=columns.map(column=>({header:column.label,key:column.key,width:Math.max(20,Math.min(38,column.label.length+3))}));
  sheet.getRow(1).font={bold:true,color:{argb:'FFFFFFFF'}};
  sheet.getRow(1).fill={type:'pattern',pattern:'solid',fgColor:{argb:'FF183F55'}};
  sheet.getRow(1).height=32;sheet.views=[{state:'frozen',ySplit:1}];
  sheet.columns.forEach(column=>{column.numFmt='@'});
  sheet.autoFilter={from:{row:1,column:1},to:{row:1,column:columns.length}};
  return book;
}

function rawCellText(cell:Cell){
  const value=cell.value;if(value===null||value===undefined)return '';
  if(value instanceof Date)return value.toISOString().slice(0,10);
  if(typeof value==='object'&&'richText' in value)return value.richText.map(part=>part.text).join('').trim();
  if(typeof value==='object'&&'text' in value)return String(value.text).trim();
  if(typeof value==='object'&&('formula' in value||'sharedFormula' in value))return String(value.result??'').trim();
  return String(value).trim();
}
function gasDate(cell:Cell){
  const value=cell.value;
  if(value instanceof Date)return value.toISOString().slice(0,10);
  if(typeof value==='number'){
    const date=new Date(Date.UTC(1899,11,30)+Math.round(value*86400000));
    if(Number.isFinite(date.getTime()))return date.toISOString().slice(0,10);
  }
  const text=rawCellText(cell).trim();let match=text.match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/);
  if(match){const date=`${match[3]}-${match[2].padStart(2,'0')}-${match[1].padStart(2,'0')}`;const parsed=new Date(date+'T00:00:00Z');if(Number.isFinite(parsed.getTime())&&parsed.toISOString().slice(0,10)===date)return date;}
  match=text.match(/^(\d{4})-(\d{2})-(\d{2})(?:T.*)?$/);if(match){const date=`${match[1]}-${match[2]}-${match[3]}`;const parsed=new Date(date+'T00:00:00Z');if(Number.isFinite(parsed.getTime())&&parsed.toISOString().slice(0,10)===date)return date;}
  return text;
}
function gasAmount(cell:Cell){
  const value=cell.value;const raw=typeof value==='number'?value:Number(rawCellText(cell).replace(/RD\$|\$|\s/g,'').replace(/,/g,''));
  return Number.isFinite(raw)?String(raw):rawCellText(cell);
}
function inferGasPeriod(sheetName:string,rows:Row[]){
  const first=rows.find(row=>/^\d{4}-\d{2}-\d{2}$/.test(row.date||''))?.date;if(first)return first.slice(0,7);
  const months=['ENERO','FEBRERO','MARZO','ABRIL','MAYO','JUNIO','JULIO','AGOSTO','SEPTIEMBRE','OCTUBRE','NOVIEMBRE','DICIEMBRE'];const normalized=normalize(sheetName).toUpperCase();const month=months.findIndex(name=>normalized.includes(name));const year=normalized.match(/20\d{2}/)?.[0];return month>=0&&year?`${year}-${String(month+1).padStart(2,'0')}`:'';
}
function parseGasWorkbook(book:Workbook):ImportedForm[]{
  const forms:ImportedForm[]=[];
  for(const sheet of book.worksheets){
    let headerRow=0;const positions:Record<string,number>={};
    for(let row=1;row<=Math.min(sheet.rowCount,40)&&!headerRow;row++){
      const found:Record<string,number>={};sheet.getRow(row).eachCell((cell,column)=>{const header=normalize(rawCellText(cell));if(header==='fecha')found.date=column;else if(header==='placa')found.plate=column;else if(header==='monto')found.amount=column;else if(header==='gestor ruta'||header==='gestor de ruta')found.manager=column;});
      if(found.date&&found.plate&&found.amount&&found.manager){headerRow=row;Object.assign(positions,found);}
    }
    if(!headerRow)continue;
    const rows:Row[]=[],sourceRows:number[]=[];
    for(let rowNumber=headerRow+1;rowNumber<=sheet.rowCount;rowNumber++){
      const row=sheet.getRow(rowNumber),plate=rawCellText(row.getCell(positions.plate)),dateText=rawCellText(row.getCell(positions.date)),amountText=rawCellText(row.getCell(positions.amount)),manager=rawCellText(row.getCell(positions.manager));
      if(!dateText&&!plate&&!amountText&&!manager)continue;
      if(normalize(plate).includes('total')||normalize(dateText).includes('total'))continue;
      if(!dateText&&!amountText)continue;
      rows.push({date:gasDate(row.getCell(positions.date)),plate,amount:gasAmount(row.getCell(positions.amount)),manager,model:'',department:''});sourceRows.push(rowNumber);
    }
    if(!rows.length)continue;
    const totalParts=Math.ceil(rows.length/100),baseName=sheet.name.trim()||`Hoja ${forms.length+1}`,period=inferGasPeriod(sheet.name,rows);
    for(let part=0;part<totalParts;part++)forms.push({group:totalParts>1?`${baseName} · parte ${part+1} de ${totalParts}`:baseName,sourceRows:sourceRows.slice(part*100,(part+1)*100),values:{fuelPeriod:period,fuelArea:'Administración'},rows:rows.slice(part*100,(part+1)*100),phones:[],number:newAdministrativeNumber()});
  }
  if(!forms.length)throw new Error('No se encontró ninguna hoja con los encabezados FECHA, PLACA, MONTO y GESTOR RUTA.');
  if(forms.length>50000)throw new Error('El archivo genera más de 50,000 formularios. Divide la carga en varios archivos.');return forms;
}

function cellText(cell:Cell,key:string,row:number){
  const value=cell.value;if(value===null||value===undefined)return '';
  if(typeof value==='object'&&('formula' in value||'sharedFormula' in value||'error' in value))throw new Error(`Fila ${row}, ${key}: pega un valor, sin fórmulas ni errores.`);
  if(value instanceof Date)return value.toISOString().slice(0,key==='fuelPeriod'?7:10);
  if(typeof value==='object'&&'richText' in value)return value.richText.map(part=>part.text).join('').trim();
  if(typeof value==='number'&&Math.abs(value)>=1e15)throw new Error(`Fila ${row}: guarda los identificadores largos como texto para evitar pérdida de dígitos.`);
  if(typeof value==='number'&&!Number.isFinite(value))throw new Error(`Fila ${row}: número no válido.`);
  // Respect explicit zero padding in source identifiers.
  if(typeof value==='number'&&/^0+$/.test(cell.numFmt)&&!key.startsWith('line.amount')&&key!=='amount')return String(value).padStart(cell.numFmt.length,'0');
  return String(typeof value==='object'&&'text' in value?value.text:value).trim();
}

export function parseAdministrativeWorkbook(book:Workbook,kind:AdministrativeFormKind):ImportedForm[]{
  if(kind==='gas_log')return parseGasWorkbook(book);
  const sheet=book.getWorksheet('Formularios')||book.worksheets[0];
  if(!sheet)throw new Error('El archivo no contiene hojas.');
  if(sheet.rowCount>50001)throw new Error('El archivo admite hasta 50,000 filas de datos.');
  const columns=administrativeColumns(kind),known=new Map(columns.flatMap(c=>[[normalize(c.label),c.key],[normalize(c.key),c.key]]));
  const headers=new Map<number,string>();const seen=new Set<string>();
  sheet.getRow(1).eachCell((cell,index)=>{const text=cellText(cell,'encabezado',1);const key=known.get(normalize(text));if(!key)throw new Error(`Columna desconocida: ${text}. Descarga la plantilla de este formulario.`);if(seen.has(key))throw new Error(`Columna repetida: ${text}.`);seen.add(key);headers.set(index,key)});
  if(!headers.size)throw new Error('Faltan los encabezados de la plantilla.');
  const groups=new Map<string,ImportedForm>();
  sheet.eachRow((row,rowNumber)=>{
    if(rowNumber===1)return;
    const values:Row={};let hasData=false;
    row.eachCell((cell,index)=>{const text=cellText(cell,headers.get(index)||`columna ${index}`,rowNumber);if(!text)return;const key=headers.get(index);if(!key)throw new Error(`Fila ${rowNumber}: hay datos en una columna sin encabezado.`);if(text.length>4000)throw new Error(`Fila ${rowNumber}: el texto supera 4,000 caracteres.`);values[key]=text;if(key!=='_form')hasData=true});
    if(!hasData)return;
    const group=values._form||`Fila ${rowNumber}`,groupKey=values._form?`id:${values._form}`:`row:${rowNumber}`;
    let item=groups.get(groupKey);if(!item){item={group,sourceRows:[],values:{},rows:[],phones:[],number:newAdministrativeNumber()};groups.set(groupKey,item)}
    item.sourceRows.push(rowNumber);const line:Row={};
    for(const [key,value] of Object.entries(values)){
      if(key==='_form')continue;
      if(key.startsWith('line.')){line[key.slice(5)]=value;continue}
      if(item.values[key]&&item.values[key]!==value)throw new Error(`Fila ${rowNumber}: ${group} contiene valores distintos para ${columns.find(c=>c.key===key)?.label}. Usa otro identificador de formulario.`);
      const type=(labels as Record<string,{type:string}>)[key]?.type;
      if((type==='date'||key==='line.date')&&!/^\d{4}-\d{2}-\d{2}$/.test(value))throw new Error(`Fila ${rowNumber}: usa fecha AAAA-MM-DD en ${key}.`);
      if(type==='month'&&!/^\d{4}-\d{2}$/.test(value))throw new Error(`Fila ${rowNumber}: usa período AAAA-MM.`);
      item.values[key]=value;
    }
    if(Object.keys(line).length){
      for(const key of lineFields[kind]?.split(' ')||[])line[key]??='';
      (kind==='sim'?item.phones:item.rows).push(line);
    }
  });
  if(!groups.size)throw new Error('No hay datos para importar.');
  const result:ImportedForm[]=[];
  for(const item of groups.values()){
    const details=kind==='sim'?item.phones:item.rows,totalParts=Math.max(1,Math.ceil(details.length/100));
    for(let part=0;part<totalParts;part++)result.push({...item,group:totalParts>1?`${item.group} · parte ${part+1} de ${totalParts}`:item.group,sourceRows:item.sourceRows.slice(part*100,(part+1)*100),rows:kind==='sim'?[]:item.rows.slice(part*100,(part+1)*100),phones:kind==='sim'?item.phones.slice(part*100,(part+1)*100):[],number:newAdministrativeNumber()});
  }
  if(result.length>50000)throw new Error('El archivo genera más de 50,000 formularios. Divide la carga en varios archivos.');
  return result;
}

export async function readAdministrativeExcel(file:File,kind:AdministrativeFormKind){
  if(!/\.xlsx$/i.test(file.name)||file.size===0||file.size>50*1024*1024)throw new Error('Selecciona un archivo Excel .xlsx de hasta 50 MB.');
  const {Workbook}=await import('exceljs');const book=new Workbook();
  try{await book.xlsx.load(await file.arrayBuffer() as never)}catch{throw new Error('No se pudo leer el archivo. Guarda una copia como Excel .xlsx sin contraseña.')}
  return parseAdministrativeWorkbook(book,kind);
}
