import {useEffect,useState} from 'react';
import {ArrowLeft,FolderOpen,Inbox,Send,Files} from 'lucide-react';
import type {PortalSession} from '@/lib/session';
import InstitutionalBrand from './InstitutionalBrand';
import WarehouseRequests from './WarehouseRequests';
import MaintenanceTemplates from './MaintenanceTemplates';
import {DocumentSystemModule} from './DocumentSystemModule';
import WarehousePdfRequests from './WarehousePdfRequests';
import {useInstitutionalDialogs} from './useInstitutionalDialogs';
import './maintenanceCommunications.css';

export type MaintenanceChannel='requests'|'requirements'|'templates'|'files';
const channels=[
 {id:'requests',label:'Solicitudes a Almacén',description:'Enviadas por departamentos de soporte',Icon:Send},
 {id:'requirements',label:'Solicitudes PDF',description:'Pendientes de recibir y autorizar',Icon:Inbox},
 {id:'templates',label:'Mis plantillas',description:'Editor y documentos guardados',Icon:Files},
 {id:'files',label:'Archivos',description:'Carpetas, compartidos y papelera',Icon:FolderOpen}
] as const;

export default function MaintenanceCommunications({view,onNavigate,onBack,session,department,onChanged}:{view:MaintenanceChannel;onNavigate:(view:MaintenanceChannel)=>void;onBack:()=>void;session:PortalSession;department:string;onChanged:()=>void}){
 useInstitutionalDialogs();
 const [secondary,setSecondary]=useState(false);
 const warehouseManager=session.supportTeam==='WAREHOUSE'||(session.role==='Administrator'&&!department);
 const visibleChannels=warehouseManager?channels:channels.filter(channel=>channel.id!=='requirements');
 const departmentName=department==='TECHNOLOGY'?'Tecnología':department==='GENERAL_SERVICES'?'Servicios Generales':department==='HUMAN_RESOURCES'?'Recursos Humanos':department;
 useEffect(()=>setSecondary(false),[view]);
 useEffect(()=>{if(!warehouseManager&&view==='requirements')onNavigate('requests')},[warehouseManager,view,onNavigate]);
 return <section className="mc-hub"><header className="mc-masthead"><div><button className="mc-back" onClick={onBack}><ArrowLeft/> Volver al área de trabajo</button><h2>Archivos y comunicaciones</h2><p>{departmentName?`Vista organizada para ${departmentName}. Solo se muestran solicitudes, requerimientos y documentos de este departamento.`:warehouseManager?'Comunicaciones de departamentos, requerimientos y documentos organizados por área.':'Solicitudes a Almacén, archivos y documentos de operación.'}</p></div><InstitutionalBrand/></header><nav className="mc-channels id-channels" role="tablist" aria-label="Canales de operación">{visibleChannels.map(({id,label,description,Icon},index)=><button key={id} id={`mc-tab-${id}`} role="tab" aria-selected={view===id} aria-controls="mc-channel-panel" tabIndex={view===id?0:-1} className={view===id?'active':''} onClick={()=>onNavigate(id)} onKeyDown={event=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;event.preventDefault();const next=event.key==='Home'?0:event.key==='End'?visibleChannels.length-1:(index+(event.key==='ArrowRight'?1:visibleChannels.length-1))%visibleChannels.length;onNavigate(visibleChannels[next].id);document.getElementById(`mc-tab-${visibleChannels[next].id}`)?.focus()}}><Icon/><span><strong>{id==='requests'&&warehouseManager?'Comunicaciones de departamentos':label}</strong><small>{id==='requests'&&warehouseManager?'Mensajes y documentos enviados por cada departamento':id==='requirements'?'Equipos solicitados por este departamento':description}</small></span></button>)}</nav><div id="mc-channel-panel" role="tabpanel" aria-labelledby={`mc-tab-${view}`}>
 {view!=='templates'&&<nav className="id-subnav" aria-label="Tipo de contenido"><button aria-pressed={!secondary} onClick={()=>setSecondary(false)}>{view==='files'?'Mis archivos y carpetas':view==='requirements'?'Solicitudes PDF':'Comunicaciones y documentos'}</button><button aria-pressed={secondary} onClick={()=>setSecondary(true)}>{view==='files'?'Biblioteca de PDF publicados':view==='requirements'?'Gestión de compras':'Gestión de equipos'}</button></nav>}
  {view==='templates'?<DocumentSystemModule key="templates" tab="templates" title="Mis plantillas" description="Redacta, guarda y envía documentos institucionales." session={session} scopeDepartment={department}/>:view==='files'?secondary?<MaintenanceTemplates session={session}/>:<DocumentSystemModule key="files" tab="files" title="Archivos" description="Carpetas departamentales, archivos compartidos y papelera." session={session} scopeDepartment={department}/>:view==='requirements'?secondary?<WarehouseRequests key="requirements" kind="requirements" session={session} department={department} onBack={onBack} onChanged={onChanged}/>:<WarehousePdfRequests session={session} department={department}/>:<DocumentSystemModule key="requests" tab="communications" channel="requests" title={warehouseManager?'Comunicaciones de departamentos':'Solicitudes a Almacén'} description={warehouseManager?'Cada comunicación recibida queda organizada por el departamento remitente y conserva sus documentos de respaldo.':'Envía una solicitud de tu departamento directamente a Almacén, con sus documentos de respaldo.'} session={session} scopeDepartment={department}/>}
 </div></section>
}
