import { apiFetch as fetch } from "@/lib/api-fetch";
import {useEffect,useRef,useState} from 'react';
import {createPortal} from 'react-dom';
import {ArrowLeft,ArrowRight,Building2,LifeBuoy,Plus,RefreshCw,Send,X} from 'lucide-react';
import type {PortalSession} from '@/lib/session';
import {userDepartments} from '@/lib/user-departments';
import './departmentTicketCreate.css';
import SearchSelect from './SearchSelect';

type Category={code:string;name:string;description?:string;defaultPriority:string;isActive?:boolean;requiresDetail:boolean;detailLabel?:string;options?:string[]};
type Department={code:string;name:string;categories:Category[]};
type Agency={id:string;codigo:string;terminal:string;grupo:string};
const priorities:Record<string,string>={CRITICAL:'Crítica',HIGH:'Alta',MEDIUM:'Media',LOW:'Baja'};
async function request<T>(url:string,init?:RequestInit):Promise<T>{
  const response=await fetch(url,{credentials:'same-origin',cache:'no-store',...init});
  const data=await response.json();
  if(!response.ok)throw new Error(data.message||data.error||'No se pudo completar la operación.');
  return data;
}

export default function InternalTicketRequest({session}:{session:PortalSession}){
  const area=session.role==='Administrative'?'Administración':session.role==='Department'?userDepartments.find(([code])=>code===session.assignedDepartment)?.[1]||'Departamento':'Almacén';
  const [departments,setDepartments]=useState<Department[]>([]),[agencies,setAgencies]=useState<Agency[]>([]);
  const [open,setOpen]=useState(false),[loading,setLoading]=useState(true),[busy,setBusy]=useState(false),[revision,setRevision]=useState(0);
  const [error,setError]=useState(''),[notice,setNotice]=useState(''),[department,setDepartment]=useState('');
  const [selected,setSelected]=useState<string[]>([]),[step,setStep]=useState<'categories'|'details'>('categories');
  const [details,setDetails]=useState<Record<string,string>>({}),[agencyId,setAgencyId]=useState(''),[subject,setSubject]=useState(''),[description,setDescription]=useState('');
  const dialog=useRef<HTMLDialogElement>(null),submitting=useRef(false);
  useEffect(()=>{
    let active=true;setLoading(true);setError('');
    void Promise.all([request<{departments:Department[]}>('/api/support/catalog'),request<{agencies:Agency[]}>('/api/tickets/agencies')]).then(([catalog,locations])=>{
      if(!active)return;
      setDepartments(catalog.departments.filter(item=>['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'].includes(item.code)));
      setAgencies(locations.agencies);
    }).catch(reason=>{if(active)setError((reason as Error).message)}).finally(()=>{if(active)setLoading(false)});
    return()=>{active=false};
  },[revision]);
  useEffect(()=>{if(open&&!dialog.current?.open)dialog.current?.showModal();},[open]);
  const activeDepartment=departments.find(item=>item.code===department);
  const categories=activeDepartment?.categories.filter(item=>item.isActive!==false).map(item=>({...item,defaultPriority:department==='TECHNOLOGY'&&item.code==='CPU_FAILURE'?'CRITICAL':item.defaultPriority||'MEDIUM'}))||[];
  const selectedCategories=categories.filter(item=>selected.includes(item.code));

  const close=()=>{if(!submitting.current)setOpen(false)};
  async function submit(event:React.FormEvent){
    event.preventDefault();if(submitting.current||!selectedCategories.length||!agencyId)return;
    submitting.current=true;setBusy(true);setError('');setNotice('');
    const sent:string[]=[],numbers:number[]=[];
    try{
      for(const category of selectedCategories){
        const created=await request<{ticketNumber:number}>('/api/tickets',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({agencyId,category:category.code,assignedDepartment:department,assignedTeam:department==='TECHNOLOGY'?'TECHNICAL_FAILURE':null,priority:category.defaultPriority||'MEDIUM',subject:subject.trim()||category.name,description:[`Solicitado por: ${session.displayName}.`,description.trim(),details[category.code]?`${category.detailLabel||'Detalle'}: ${details[category.code]}`:''].filter(Boolean).join('\n'),ticketType:'INTERNAL'})});
        sent.push(category.code);numbers.push(created.ticketNumber);
      }
      setSelected([]);setDetails({});setSubject('');setDescription('');setAgencyId('');setDepartment('');setStep('categories');setOpen(false);
    }catch(reason){setError(`${(reason as Error).message}${sent.length?' Los casos enviados se retiraron de la selección; puedes reintentar los restantes.':''}`);setSelected(current=>current.filter(code=>!sent.includes(code)))}
    finally{
      if(numbers.length){setNotice(`Ticket${numbers.length===1?'':'s'} ${numbers.map(number=>`#${number}`).join(', ')} enviado${numbers.length===1?'':'s'} correctamente.`);window.dispatchEvent(new Event('support-live-refresh'))}
      submitting.current=false;setBusy(false);
    }
  }
  if(!session.permissions.canCreateTickets)return <p role="status">Tu perfil no tiene permiso para crear tickets.</p>;
  return <section className="administrative-ticket-create-only internal-ticket-request">
    <header className="internal-support-heading"><h3><LifeBuoy/> Soporte de {area}</h3><p>Selecciona el departamento que atenderá tu solicitud.</p></header>
    <button type="button" className="administrative-create-ticket-card" onClick={()=>{setNotice('');setOpen(true)}}><span className="support-section-icon"><Plus/></span><span className="support-section-copy"><em>NUEVA SOLICITUD INTERNA</em><strong>Crear ticket interno</strong><small>Solicita asistencia a Tecnología, Servicios Generales o Recursos Humanos.</small></span><span className="support-section-count">Seleccionar departamento <ArrowRight/></span></button>
    {notice&&<p className="internal-ticket-notice" role="status">{notice}</p>}
    {!open&&error&&<p role="alert">{error}</p>}
    {open&&createPortal(<dialog ref={dialog} className="ticket-modal support-catalog-modal internal-ticket-dialog" aria-labelledby="internal-ticket-title" onCancel={event=>{event.preventDefault();close()}}>
      <header><div><span>NUEVA SOLICITUD INTERNA</span><h2 id="internal-ticket-title">Crear ticket interno</h2><p>Selecciona el servicio especializado del departamento elegido.</p></div><button type="button" aria-label="Cerrar" disabled={busy} onClick={close}><X/></button></header>
      <form onSubmit={submit}>
        {error&&<p className="alert" role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
        {loading?<p role="status">Cargando departamentos y agencias…</p>:!activeDepartment?<><div className="modal-step-heading"><span>01 · DEPARTAMENTO</span><h3>¿Qué departamento necesitas?</h3></div><div className="internal-department-grid">{departments.map(item=><button type="button" key={item.code} onClick={()=>{setDepartment(item.code);setSelected([]);setDetails({});setStep('categories')}}><Building2/><span><strong>{item.name}</strong><small>{item.categories.filter(category=>category.isActive!==false).length} servicios disponibles</small></span><ArrowRight/></button>)}</div>{!departments.length&&<p>No hay servicios disponibles en este momento.</p>}{error&&<button type="button" onClick={()=>setRevision(current=>current+1)}><RefreshCw/> Volver a cargar</button>}</>:<>
          <div className="selected-support-department"><button type="button" disabled={busy} onClick={()=>{setDepartment('');setStep('categories')}}>← Cambiar departamento</button><span><small>DEPARTAMENTO SELECCIONADO</small><strong>{activeDepartment.name}</strong><em className="ticket-auto">Asignado automáticamente</em></span></div>
          {step==='categories'?<><div className="modal-step-heading"><span>02 · TIPO DE SOPORTE</span><h3>Selecciona el ticket que necesitas</h3></div><div className="support-category-grid">{categories.map(category=><button type="button" key={category.code} className={selected.includes(category.code)?'active':''} aria-pressed={selected.includes(category.code)} onClick={()=>setSelected(current=>current.includes(category.code)?current.filter(code=>code!==category.code):[...current,category.code])}><input type="checkbox" tabIndex={-1} aria-hidden="true" readOnly checked={selected.includes(category.code)}/><span><strong>{category.name}</strong><small>{category.description}</small></span><em className={`ticket-priority priority-${category.defaultPriority.toLowerCase()}`}>{priorities[category.defaultPriority]||category.defaultPriority}</em></button>)}</div>{!categories.length&&<p>No hay categorías activas para este departamento.</p>}<button type="button" className="support-category-continue" disabled={!selected.length} onClick={()=>setStep('details')}>Continuar con {selected.length} caso{selected.length===1?'':'s'} <ArrowRight/></button></>:<>
            <div className="modal-step-heading"><span>03 · DATOS DEL TICKET</span><h3>Completa la solicitud</h3></div>
            <SearchSelect label="Agencia" value={agencyId} options={agencies.map(item=>({value:item.id,label:`${item.codigo} · ${item.terminal}`,description:item.grupo}))} onChange={setAgencyId} disabled={busy} required placeholder="Buscar por código, nombre o grupo"/>
            {selectedCategories.map(category=><fieldset key={category.code}><legend>{category.name}</legend>{category.requiresDetail&&<label>{category.detailLabel||'Detalle de la falla'}{category.options?.length?<select aria-label={category.detailLabel||"Detalle de la falla"} required disabled={busy} value={details[category.code]||''} onChange={event=>setDetails({...details,[category.code]:event.target.value})}><option value="">Selecciona</option>{category.options.map(option=><option key={option}>{option}</option>)}</select>:<input required disabled={busy} maxLength={200} value={details[category.code]||''} onChange={event=>setDetails({...details,[category.code]:event.target.value})}/>}</label>}</fieldset>)}
            <label>Asunto<input required disabled={busy} maxLength={180} value={subject} onChange={event=>setSubject(event.target.value)}/></label><label>Descripción de la falla<textarea required disabled={busy} maxLength={1700} value={description} onChange={event=>setDescription(event.target.value)}/></label>
            <footer><button type="button" disabled={busy} onClick={()=>setStep('categories')}><ArrowLeft/> Cambiar tipo de soporte</button><button type="submit" className="primary" disabled={busy||!selected.length}><Send/>{busy?'Enviando…':'Enviar ticket interno'}</button></footer>
          </>}
        </>}
      </form>
    </dialog>,document.body)}
  </section>;
}
