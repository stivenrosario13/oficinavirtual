import type {PortalSession} from './session';
import {saveAdministrativeRequest} from './administrative-requests';
import {prepareAdministrativeForm,validateAdministrativeForm} from './administrative-form-data';
import {newAdministrativeNumber,type AdministrativeDraft} from './administrative-form-excel';
import {buildAdministrativeFormPdf} from '../components/vite/administrativeFormPdf';
import {realLogoData} from '../components/vite/institutionalDocumentAssets';
import type {DocumentDetail} from '../components/vite/DocumentSystemModule';
import {apiFetch} from './api-fetch';
import {buildWarehouseRequestPdf} from '../components/vite/warehouseRequestPdf';

export async function saveAdministrativeForm(kind:string,draft:AdministrativeDraft,department:string,session:PortalSession,editing?:DocumentDetail){
  if(kind==='internet')return saveInternetForm(draft,session,editing);
  const prepared=prepareAdministrativeForm(kind,draft.values,draft.rows,draft.phones,department,session);
  const error=validateAdministrativeForm(kind,draft.values,draft.rows,draft.phones,prepared);
  if(error)throw new Error(error);
  const number=editing?.administrativeRequest?.number||draft.number||newAdministrativeNumber();
  draft.number=number;
  const payload:Record<string,unknown>={...draft.values,rows:draft.rows,phones:draft.phones};
  if(kind==='fuel_log'){delete payload.fuelBudget;delete payload.physicalTickets;delete payload.fuelCredit;}
  const request={number,assetType:prepared.assetType,targetType:prepared.targetType,targetName:prepared.target.trim(),agencyCode:prepared.agencyCode,reason:prepared.reason.trim(),formType:editing?(editing.administrativeRequest?.formType??null):kind.toUpperCase(),formData:payload};
  if(JSON.stringify(request).length>30000)throw new Error('El formulario supera el tamaño permitido. Divide sus detalles en varios formularios.');
  const bytes=await buildAdministrativeFormPdf({registrationOnly:kind==='fuel_log',detailsOnlyAfterFirstPage:kind==='incident_report',hideCustodySpaces:kind==='incident_report',number,title:prepared.title,subtitle:prepared.subtitle,department,createdBy:editing?.ownerDisplayName||session.displayName,createdByLogin:editing?.ownerLogin||session.email||session.id,responsible:draft.values.responsible,target:prepared.target,fields:prepared.fields,tables:prepared.tables,bodySections:prepared.bodySections,notes:prepared.reason,logoData:await realLogoData(),origin:window.location.origin,createdAt:editing?new Date(editing.createdUtc):new Date()});
  const form=new FormData();
  form.append('file',new Blob([bytes],{type:'application/pdf'}),`${number}.pdf`);
  form.append('area','communications');form.append('channel','requirements');
  form.append('title',`${prepared.title} · ${number}`);
  form.append('description',`${prepared.title} · ${prepared.target} · ${prepared.reason.slice(0,120)}`);
  const recipientDepartment=kind==='labor_benefits_request'||kind==='blank_checks_delivery'?'Contabilidad':kind==='signed_benefits_checks_delivery'?'Caja':'Administración';
  form.append('recipientDepartment',recipientDepartment);
  if(!editing&&['Tecnología','Servicios Generales','Recursos Humanos','Call Center','Supervisión'].includes(department))form.append('sourceDepartment',department);
  form.append('administrativeRequest',JSON.stringify(request));
  if(editing)form.append('revision',String(editing.administrativeRevision||0));
  await saveAdministrativeRequest(editing?`/api/institutional-documents/${editing.id}/administrative-form`:'/api/institutional-documents',form,editing?'PUT':'POST');
}

export function administrativeDraftFromDocument(document:DocumentDetail):AdministrativeDraft{
  const data=document.administrativeRequest?.formData||{};
  const values:Record<string,string>={};
  for(const [key,value] of Object.entries(data))if(typeof value==='string'||typeof value==='number')values[key]=String(value);
  const request=document.administrativeRequest;
  if(request){
    const kind=administrativeDocumentKind(document);
    values.agencyCode??=request.agencyCode||'';
    values[kind==='fleet'||kind==='device'?'recipient':kind==='modem'?'agencyName':'targetName']??=request.targetName;
    values.notes??=request.reason||'';values.reason??=request.reason||'';
    values.targetType??=request.targetType;
  }
  const rows=(value:unknown)=>Array.isArray(value)?value.filter(row=>row&&typeof row==='object').map(row=>Object.fromEntries(Object.entries(row).map(([key,v])=>[key,v==null?'':String(v)]))):[];
  let detailRows=rows(data.rows);
  // Historical fuel imports stored sections and original source cells instead of editable rows.
  if(!detailRows.length&&Array.isArray(data.sections)){
    detailRows=data.sections.flatMap((section:{name?:string;rows?:{cells?:Record<string,unknown>;amount?:number|null}[]})=>(section.rows||[]).map(row=>({date:values.fuelDate||'',plate:String(row.cells?.B??''),model:String(row.cells?.C??''),department:section.name||'',manager:String(row.cells?.D??''),amount:row.amount==null?'':String(row.amount)})));
    values.fuelArea||=document.administrativeRequest?.targetName||document.department;
    values.fuelNotes||=values.notes||document.administrativeRequest?.reason||'';
    if(data.importKey&&document.administrativeRequest?.formType==='FUEL_LOG')values.fuelNotes='Registro diario de gasolina importado del Excel. Se conservan las celdas y los importes registrados.';
  }
  return {values,rows:detailRows,phones:rows(data.phones),number:document.administrativeRequest?.number};
}

export function administrativeDocumentKind(document:Pick<DocumentDetail,'administrativeRequest'>){
  const request=document.administrativeRequest;
  if(request?.assetType==='INTERNET')return 'internet';
  if(!request?.formType||request.formType==='GENERAL')return request?.assetType==='FLOTA'?'fleet':request?.assetType==='MODEM'?'modem':'general';
  return request.formType.toLowerCase();
}

export function validateInternetDraft(values:Record<string,string>){
  if(!values.agencyCode?.trim())return 'Indica el código de agencia.';
  if(!['BANDA_ANCHA','LINEA_FIJA'].includes(values.serviceKind))return 'Tipo de servicio: BANDA_ANCHA o LINEA_FIJA.';
  if(!['CLARO','ALTICE'].includes(values.company))return 'Compañía: CLARO o ALTICE.';
  if(!['EQUIPO','SIM'].includes(values.itemKind))return 'Solicitar: EQUIPO o SIM.';
  if(!values.serviceNumber?.trim()||values.serviceNumber.length>80)return 'Indica un número de servicio de hasta 80 caracteres.';
  const count=Number(values.modemQuantity);
  if(!Number.isInteger(count)||(values.itemKind==='EQUIPO'?(count<1||count>20):count!==0))return 'Cantidad de módems: 1 a 20 para EQUIPO, 0 para SIM.';
  if(!values.changeReason?.trim()||values.changeReason.trim().length<5||values.changeReason.length>2000)return 'Indica el motivo del cambio (5 a 2,000 caracteres).';
  return '';
}

async function saveInternetForm(draft:AdministrativeDraft,session:PortalSession,editing?:DocumentDetail){
  const error=validateInternetDraft(draft.values);if(error)throw new Error(error);
  const response=await apiFetch('/api/tickets/agencies',{credentials:'same-origin'});
  if(!response.ok)throw new Error('No se pudo consultar el catálogo de agencias.');
  const data=await response.json() as {agencies:{id:string;codigo:string;terminal:string;grupo:string}[]};
  const matches=data.agencies.filter(agency=>agency.codigo.trim().toLowerCase()===draft.values.agencyCode.trim().toLowerCase());
  if(matches.length!==1)throw new Error(`El código de agencia ${draft.values.agencyCode} no existe o es ambiguo en tu catálogo.`);
  const agency=matches[0],values=draft.values;
  const number=editing?.administrativeRequest?.number||draft.number||newAdministrativeNumber();draft.number=number;
  const detail={ticketId:values.ticketId||editing?.administrativeRequest?.internet?.ticketId||undefined,ticketNumber:values.ticketNumber?Number(values.ticketNumber):editing?.administrativeRequest?.internet?.ticketNumber,agencyId:agency.id,agencyCode:agency.codigo,agencyName:agency.terminal,agencyGroup:agency.grupo,serviceKind:values.serviceKind as 'BANDA_ANCHA'|'LINEA_FIJA',serviceNumber:values.serviceNumber.trim(),company:values.company as 'CLARO'|'ALTICE',modemQuantity:Number(values.modemQuantity),itemKind:values.itemKind as 'EQUIPO'|'SIM',changeReason:values.changeReason.trim()};
  const bytes=await buildWarehouseRequestPdf({number,department:'Tecnología',destination:'Administración',agency,priority:'MEDIUM',notes:`${detail.ticketNumber?`Ticket interno #${detail.ticketNumber}. `:''}${detail.changeReason}`,lines:[{kind:'EQUIPMENT',name:detail.itemKind==='EQUIPO'?'Módem de internet':'SIM de internet',quantity:detail.itemKind==='EQUIPO'?detail.modemQuantity:1,serial:'Pendiente de asignación',note:`${detail.company} · ${detail.serviceKind} · ${detail.serviceNumber}`}],createdBy:editing?.ownerDisplayName||session.displayName,createdByLogin:session.email||session.id,origin:window.location.origin,internetDetails:{...detail,reason:detail.changeReason}});
  const form=new FormData();form.append('file',new Blob([bytes],{type:'application/pdf'}),`${number}.pdf`);
  form.append('area','communications');form.append('channel','requirements');form.append('recipientDepartment','Administración');form.append('sourceDepartment','Tecnología');
  form.append('title',`Solicitud de equipo de internet ${number}`);form.append('description',`${agency.codigo} · ${agency.terminal} · ${detail.company} · ${detail.serviceNumber}`);
  // Retain null formType on older internet requests when editing them.
  form.append('administrativeRequest',JSON.stringify({number,assetType:'INTERNET',targetType:'AGENCIA',targetName:agency.terminal,agencyCode:agency.codigo,reason:detail.changeReason,internet:detail,...(!editing||editing.administrativeRequest?.formType?{formType:'INTERNET',formData:values}:{})}));
  if(editing)form.append('revision',String(editing.administrativeRevision||0));
  await saveAdministrativeRequest(editing?`/api/institutional-documents/${editing.id}/administrative-form`:'/api/institutional-documents',form,editing?'PUT':'POST');
}
