import { apiFetch as fetch } from "@/lib/api-fetch";
import {useEffect,useMemo,useRef,useState} from 'react';
import {Building2,Router,Send,X} from 'lucide-react';
import type {PortalSession} from '@/lib/session';
import {saveAdministrativeForm} from '@/lib/administrative-form-save';
import {newAdministrativeNumber} from '@/lib/administrative-form-excel';
import type {DocumentDetail} from './DocumentSystemModule';
import AdministrativeExcelImport from './AdministrativeExcelImport';
import './administrativeWorkspace.css';
import './administrativeRequests.css';
import './administrativeForms.css';
import './administrativeFormFrame.css';

type Agency={id:string;codigo:string;terminal:string;grupo:string};
type AgencyResponse={agencies:Agency[]};
export type InternetLinkedTicket={id:string;ticketNumber:number;codigo:string;terminal:string;grupo:string;subject:string};
type Props={linkedTicket?:InternetLinkedTicket;session:PortalSession;onClose:()=>void;onBack:()=>void;onSaved:()=>Promise<void>;editing?:DocumentDetail};
const normalize=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('es');
const agencyCode=(value:string)=>normalize(value).replace(/[^a-z0-9]/g,'').replace(/^0+(?=\d)/,'');

export function linkedTicketAgency(agencies:Agency[],ticket:InternetLinkedTicket):Agency|undefined{
 const sameCode=agencies.filter(item=>agencyCode(item.codigo)===agencyCode(ticket.codigo));
 if(sameCode.length===1)return sameCode[0];
 const sameGroup=sameCode.filter(item=>normalize(item.grupo)===normalize(ticket.grupo));
 if(sameGroup.length===1)return sameGroup[0];
 const ticketTerminal=normalize(ticket.terminal);
 return sameGroup.find(item=>ticketTerminal.includes(normalize(item.terminal))||normalize(item.terminal).includes(ticketTerminal))
  ||sameCode.find(item=>ticketTerminal.includes(normalize(item.terminal))||normalize(item.terminal).includes(ticketTerminal));
}

export default function AdministrativeInternetRequestComposer({session,onClose,onBack,onSaved,editing,linkedTicket}:Props){
 const previous=editing?.administrativeRequest?.internet;
 const [importOpen,setImportOpen]=useState(false);
 const number=useRef(previous?editing!.administrativeRequest!.number:newAdministrativeNumber()),inFlight=useRef(false);
 const[agencies,setAgencies]=useState<Agency[]>([]),[agencyId,setAgencyId]=useState(previous?.agencyId||''),[agencyQuery,setAgencyQuery]=useState(''),[loading,setLoading]=useState(true),[serviceKind,setServiceKind]=useState<'BANDA_ANCHA'|'LINEA_FIJA'>(previous?.serviceKind||'BANDA_ANCHA'),[serviceNumber,setServiceNumber]=useState(previous?.serviceNumber||''),[company,setCompany]=useState<'CLARO'|'ALTICE'>(previous?.company||'CLARO'),[itemKind,setItemKind]=useState<'EQUIPO'|'SIM'>(previous?.itemKind||'EQUIPO'),[modemQuantity,setModemQuantity]=useState(previous?.modemQuantity??1),[changeReason,setChangeReason]=useState(previous?.changeReason||(linkedTicket?`Cambio de módem para el ticket #${linkedTicket.ticketNumber}: ${linkedTicket.subject}`:'')),[error,setError]=useState(''),[busy,setBusy]=useState(false);
 useEffect(()=>{let cancelled=false;fetch('/api/tickets/agencies',{credentials:'same-origin',cache:'no-store'}).then(async response=>{if(!response.ok)throw new Error('No se pudo cargar el catálogo de agencias.');return response.json() as Promise<AgencyResponse>}).then(data=>{if(!cancelled)setAgencies(data.agencies||[])}).catch(reason=>{if(!cancelled)setError((reason as Error).message)}).finally(()=>{if(!cancelled)setLoading(false)});return()=>{cancelled=true}},[]);
 useEffect(()=>{if(linkedTicket&&agencies.length){const match=linkedTicketAgency(agencies,linkedTicket);if(match)setAgencyId(match.id);else setError(`No encontramos la agencia ${linkedTicket.codigo} del ticket #${linkedTicket.ticketNumber} en el catálogo activo.`)}},[agencies,linkedTicket]);
 const agency=agencies.find(item=>item.id===agencyId);
 const visibleAgencies=useMemo(()=>agencies.filter(item=>item.id===agencyId||!agencyQuery.trim()||normalize(`${item.codigo} ${item.terminal} ${item.grupo}`).includes(normalize(agencyQuery.trim()))).slice(0,250),[agencies,agencyQuery,agencyId]);
 async function submit(){
  if(inFlight.current)return;
  if(!agency){setError('Selecciona una agencia.');return;}
  inFlight.current=true;setBusy(true);setError('');
  try{await saveAdministrativeForm('internet',{number:number.current,values:{ticketId:linkedTicket?.id||previous?.ticketId||'',ticketNumber:String(linkedTicket?.ticketNumber||previous?.ticketNumber||''),agencyCode:agency.codigo,serviceKind,serviceNumber,company,itemKind,modemQuantity:String(modemQuantity),changeReason},rows:[],phones:[]},'Tecnología',session,editing);await onSaved();}catch(reason){setError((reason as Error).message)}finally{inFlight.current=false;setBusy(false)}
 }
 if(importOpen)return <AdministrativeExcelImport kind="internet" title="Equipo de internet" session={session} department="Tecnología" onClose={()=>setImportOpen(false)} onSaved={onSaved}/>;
 return <div className="adw-dialog-backdrop" role="presentation" onMouseDown={event=>{if(event.target===event.currentTarget&&!busy)onClose()}}><section className="adw-dialog admin-request-form admin-internet-form" role="dialog" aria-modal="true" aria-label="Solicitud de equipo de internet"><header><div><small>AVERÍA TÉCNICA · TECNOLOGÍA</small><h3>Solicitud de equipo de internet</h3></div><button disabled={busy} onClick={onClose} aria-label="Cerrar"><X/></button></header>{!editing&&!linkedTicket&&<div className="admin-excel-tools"><button disabled={busy} onClick={()=>setImportOpen(true)}>Importar masivo desde Excel</button></div>}{editing&&<p>Al guardar, el formulario vuelve a pendiente para una nueva revisión.</p>}{linkedTicket&&<div className="admin-linked-ticket"><strong>Ticket interno #{linkedTicket.ticketNumber}</strong><span>{linkedTicket.codigo} · {linkedTicket.terminal} · {linkedTicket.grupo}</span></div>}<p>El comprobante tendrá el mismo formato institucional de Almacén y pasará a recepción, autorización y entrega en Administración.</p><div className="admin-form-grid">{!linkedTicket&&<label className="admin-agency-picker"><Building2/> Buscar agencia<input value={agencyQuery} onChange={event=>setAgencyQuery(event.target.value)} placeholder="Código, nombre o grupo"/></label>}<label className={linkedTicket?'admin-linked-agency':''}>{linkedTicket?'Agencia vinculada al ticket':'Agencia donde se instalará'}<select value={agencyId} disabled={loading||!!linkedTicket} onChange={event=>setAgencyId(event.target.value)}><option value="">{loading?'Cargando agencias…':'Selecciona una agencia'}</option>{visibleAgencies.map(item=><option key={item.id} value={item.id}>{item.codigo} · {item.terminal} · {item.grupo}</option>)}</select></label><label>Grupo<input readOnly value={agency?.grupo||''} placeholder="Se completa con la agencia"/></label><label>Tipo de servicio<select value={serviceKind} onChange={event=>setServiceKind(event.target.value as typeof serviceKind)}><option value="BANDA_ANCHA">Internet / banda ancha</option><option value="LINEA_FIJA">Línea fija</option></select></label><label>Número de internet, banda ancha o línea fija<input value={serviceNumber} maxLength={80} onChange={event=>setServiceNumber(event.target.value)} placeholder="Número asociado al servicio"/></label><label>Compañía<select value={company} onChange={event=>setCompany(event.target.value as typeof company)}><option value="CLARO">Claro</option><option value="ALTICE">Altice</option></select></label><label>Solicitar<select value={itemKind} onChange={event=>{const next=event.target.value as typeof itemKind;setItemKind(next);setModemQuantity(next==='SIM'?0:1)}}><option value="EQUIPO">Equipo / módem</option><option value="SIM">SIM</option></select></label><label>Cantidad de módems<input type="number" min={0} max={20} value={modemQuantity} disabled={itemKind==='SIM'} onChange={event=>setModemQuantity(Number(event.target.value))}/></label></div><label>Motivo del cambio<textarea value={changeReason} maxLength={900} onChange={event=>setChangeReason(event.target.value)} placeholder="Describe la avería, el cambio necesario y cualquier detalle de instalación"/></label><div className="admin-requester"><Router/><span><strong>Solicita: {session.displayName}</strong><small>{session.email||session.id} · Avería Técnica, Tecnología</small></span></div>{error&&<p className="mw-error" role="alert">{error}</p>}<footer><button disabled={busy} onClick={onBack}>Volver</button><button className="primary" disabled={busy||loading||!agency} onClick={()=>void submit()}><Send/> {busy?'Guardando…':editing?'Guardar cambios':'Generar comprobante y enviar'}</button></footer></section></div>;
}
