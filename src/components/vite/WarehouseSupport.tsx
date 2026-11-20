import { apiFetch as fetch } from "@/lib/api-fetch";
import {lazy,Suspense,useCallback,useEffect,useState} from 'react';
import type {PortalSession} from '@/lib/session';
import {userDepartments} from '@/lib/user-departments';
const TicketCenter=lazy(()=>import('./TicketCenter'));
import {LifeBuoy,Plus,RefreshCw,Send} from 'lucide-react';
import './warehouseSupport.css';
import './departmentTicketCreate.css';
import InternalTicketRequest from './InternalTicketRequest';
import './maintenanceCommunications.css';
import './administrativeWorkspace.css';
import './administrativePortal.css';
import './departmentWorkspacePolish.css';

type Category={code:string;name:string;defaultPriority:string;requiresDetail:boolean;detailLabel?:string;options?:string[]};
type Department={code:string;name:string;categories:Category[]};
type Agency={id:string;codigo:string;terminal:string;grupo:string};
type Ticket={id:string;ticketNumber:number;subject:string;description:string;assignedDepartment:string;status:string;resolution?:string;history:{id:string;action:string;actorName:string;comment?:string}[]};
async function api<T>(url:string,init?:RequestInit):Promise<T>{
 const response=await fetch(url,{credentials:'same-origin',cache:'no-store',...init});
 const data=await response.json();if(!response.ok)throw new Error(data.error||data.message||'No se pudo completar la operación.');return data;
}
const states:Record<string,string>={OPEN:'Abierto',IN_PROGRESS:'En proceso',PENDING:'Pendiente',RESOLVED:'Resuelto',CLOSED:'Cerrado',CANCELLED:'Anulado'};
export default function WarehouseSupport({session,createOnly=false}:{session:PortalSession;createOnly?:boolean}){
 if(createOnly&&session.role==='Department'){
  const name=userDepartments.find(([code])=>code===(session.assignedDepartment||session.supportDepartment))?.[1]||'Departamento';
  return <section className="mc-hub administrative-workspace administrative-portal department-document-workspace"><header className="mc-page-heading"><div><small>DEPARTAMENTO DE {name.toUpperCase()}</small><h2>Soporte</h2><p>Crea tickets internos y solicita asistencia al departamento correspondiente.</p></div></header><div className="department-section-content"><InternalTicketRequest session={session}/></div></section>;
 }
 return createOnly?<InternalTicketRequest session={session}/>:<WarehouseSupportHistory session={session}/>;
}
function WarehouseSupportHistory({session,createOnly=false}:{session:PortalSession;createOnly?:boolean}){
 const administrative=session.role==='Administrative';
 const area=administrative?'Administración':session.role==='Department'?userDepartments.find(([code])=>code===session.assignedDepartment)?.[1]||'Departamento':'Almacén';
 const destinations=administrative||createOnly?'Tecnología, Servicios Generales o Recursos Humanos':'Tecnología o Servicios Generales';
 const [reportAgency,setReportAgency]=useState<Agency|null>(null);
 const [departments,setDepartments]=useState<Department[]>([]),[agencies,setAgencies]=useState<Agency[]>([]),[tickets,setTickets]=useState<Ticket[]>([]);
 const [open,setOpen]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState(''),[notice,setNotice]=useState('');
 const [department,setDepartment]=useState('TECHNOLOGY'),[category,setCategory]=useState(''),[agencyId,setAgency]=useState(''),[query,setQuery]=useState(''),[subject,setSubject]=useState(''),[description,setDescription]=useState(''),[detail,setDetail]=useState('');
 const load=useCallback(async()=>{try{const data=await api<{tickets:Ticket[]}>('/api/tickets');setTickets(data.tickets);setError('');}catch(e){setError((e as Error).message)}},[]);
 useEffect(()=>{if(!createOnly)void load();void Promise.all([api<{departments:Department[]}>('/api/support/catalog'),api<{agencies:Agency[]}>('/api/tickets/agencies')]).then(([catalog,locations])=>{setDepartments(catalog.departments.filter(d=>(administrative||createOnly?['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES']:['TECHNOLOGY','GENERAL_SERVICES']).includes(d.code)));setAgencies(locations.agencies)}).catch(e=>setError(e.message));if(createOnly)return;const refresh=()=>void load();window.addEventListener('support-live-refresh',refresh);return()=>window.removeEventListener('support-live-refresh',refresh)},[load,administrative,createOnly]);
 const categories=departments.find(d=>d.code===department)?.categories||[],selected=categories.find(c=>c.code===category);
 const locations=agencies.filter(a=>a.id===agencyId||(a.codigo+' '+a.terminal).toLocaleLowerCase().includes(query.toLocaleLowerCase())).slice(0,100);
 async function submit(event:React.FormEvent){event.preventDefault();if(busy)return;setBusy(true);setError('');setNotice('');try{
  const created=await api<Ticket>('/api/tickets',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({agencyId,category,assignedDepartment:department,assignedTeam:department==='TECHNOLOGY'?'TECHNICAL_FAILURE':null,priority:selected?.defaultPriority||'MEDIUM',subject:subject.trim(),description:`Origen: Departamento de ${area}.\n${description.trim()}${detail?'\n'+detail:''}`,ticketType:'INTERNAL'})});
  setNotice(`Ticket interno #${created.ticketNumber} enviado a ${departments.find(d=>d.code===department)?.name||department}.`);setOpen(false);setSubject('');setDescription('');setDetail('');if(!createOnly)await load();window.dispatchEvent(new Event('support-live-refresh'));
 }catch(e){setError((e as Error).message)}finally{setBusy(false)}}
 if(reportAgency)return <Suspense fallback={<p>Cargando formulario de incidencia…</p>}><TicketCenter session={session} reportAgency={reportAgency} onCloseReport={()=>{setReportAgency(null);setOpen(false);void load();}} onSessionExpired={()=>window.location.reload()}/></Suspense>;
 if(open&&!administrative&&!createOnly)return <section className="warehouse-support"><header><div><small>ALMACÉN · REPORTAR INCIDENCIA</small><h1>Selecciona la agencia</h1><p>Después selecciona Tecnología o Servicios Generales, como en el formulario de los supervisores.</p></div><button onClick={()=>setOpen(false)}>Volver a soporte</button></header>{error&&<p role="alert">{error}</p>}<label>Buscar agencia<input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Código o nombre de la agencia"/></label><div className="choice-grid">{locations.map(a=><button className="choice" key={a.id} onClick={()=>setReportAgency(a)}><span><strong>{a.terminal}</strong><small>{a.codigo} · {a.grupo}</small></span></button>)}</div></section>;
 return <section className={`warehouse-support${createOnly?' administrative-ticket-create-only':''}`}><header><div><small>DEPARTAMENTO DE {area.toUpperCase()}</small><h1><LifeBuoy/>Soporte de {area}</h1><p>{createOnly?`Solicita asistencia a ${destinations}.`:`Solicita asistencia a ${destinations} y consulta tus tickets internos.`}</p></div>{!createOnly&&<><button onClick={()=>setOpen(!open)}><Plus/>Reportar incidencia</button><button disabled={busy} onClick={()=>void load()}><RefreshCw/>Actualizar</button></>}</header>
 {createOnly&&!open&&<button type="button" className="administrative-create-ticket-card" onClick={()=>setOpen(true)}><span className="support-section-icon"><Plus/></span><span className="support-section-copy"><em>REPORTAR INCIDENCIA</em><strong>Crear ticket</strong><small>Solicita soporte a Tecnología, Servicios Generales o Recursos Humanos.</small></span><span className="support-section-count">Seleccionar departamento →</span></button>}
 {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
 {open&&<form onSubmit={submit}><div className="administrative-ticket-form-heading"><h2>Reportar una falla</h2>{createOnly&&<button type="button" onClick={()=>setOpen(false)}>Volver</button>}</div><div className="warehouse-support-grid"><label>Departamento<select value={department} onChange={e=>{setDepartment(e.target.value);setCategory('');setDetail('')}}>{departments.map(d=><option key={d.code} value={d.code}>{d.name}</option>)}</select></label><label>Falla / categoría<select required value={category} onChange={e=>{setCategory(e.target.value);setDetail('')}}><option value="">Selecciona la falla</option>{categories.map(c=><option key={c.code} value={c.code}>{c.name}</option>)}</select></label><label>Buscar agencia<input value={query} onChange={e=>setQuery(e.target.value)} placeholder="Código o nombre de la agencia"/></label><label>Agencia del ticket interno<select required value={agencyId} onChange={e=>setAgency(e.target.value)}><option value="">Selecciona una agencia</option>{locations.map(a=><option key={a.id} value={a.id}>{a.codigo} · {a.terminal}</option>)}</select></label></div>
 {selected?.requiresDetail&&<label>{selected.detailLabel||'Detalle de la falla'}{selected.options?.length?<select required value={detail} onChange={e=>setDetail(e.target.value)}><option value="">Selecciona</option>{selected.options.map(option=><option key={option}>{option}</option>)}</select>:<input required maxLength={200} value={detail} onChange={e=>setDetail(e.target.value)}/>}</label>}
 <label>Asunto<input required maxLength={180} value={subject} onChange={e=>setSubject(e.target.value)}/></label><label>Descripción de la falla<textarea required maxLength={1700} value={description} onChange={e=>setDescription(e.target.value)}/></label><button disabled={busy||!selected} type="submit"><Send/>{busy?'Enviando…':'Enviar ticket interno'}</button></form>}
 {!createOnly&&<><h2>Mis tickets · {tickets.length}</h2><div className="warehouse-support-grid">{tickets.map(t=><article key={t.id}><small>#{t.ticketNumber} · {departments.find(d=>d.code===t.assignedDepartment)?.name||t.assignedDepartment}</small><h3>{t.subject}</h3><strong>{states[t.status]||t.status}</strong><p>{t.description}</p>{t.resolution&&<p>Solución: {t.resolution}</p>}<details><summary>Seguimiento</summary>{t.history?.map(h=><p key={h.id}><b>{h.actorName}</b> · {h.comment||h.action}</p>)}</details></article>)}</div>{!tickets.length&&<p>No tienes tickets registrados.</p>}</>}</section>;
}
