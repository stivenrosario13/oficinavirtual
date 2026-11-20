import { apiFetch as fetch } from "@/lib/api-fetch";
import {useEffect,useState} from 'react';
import {ArrowUpRight,FileText,FolderOpen,Hammer,Inbox,RefreshCw} from 'lucide-react';
import {requestHubCounts} from '@/lib/request-hub-counts';
import type {DocumentItem} from './DocumentSystemModule';
import type {WorkshopCase} from './workshopWorkflow';
import './requestHubCards.css';

type Destination='files'|'administrative'|'warehouse'|'workshop';
const cards=[
 {key:'files',icon:FolderOpen,category:'BIBLIOTECA DEL DEPARTAMENTO',title:'Archivos',description:'Carpetas, documentos compartidos y papelera.',action:'Explorar biblioteca'},
 {key:'administrative',icon:FileText,category:'GESTIÓN ADMINISTRATIVA',title:'Solicitudes a Administrativos',description:'Formularios, autorizaciones y seguimiento administrativo.',action:'Gestionar solicitudes'},
 {key:'warehouse',icon:Inbox,category:'EQUIPOS Y MATERIALES',title:'Solicitudes a Almacén',description:'Equipos y materiales con seguimiento de entrega.',action:'Consultar Almacén'},
 {key:'workshop',icon:Hammer,category:'REPARACIÓN DE EQUIPOS',title:'Solicitudes a Taller',description:'Recepción, reparación y seguimiento de tus equipos.',action:'Consultar Taller'},
] as const;

export default function RequestHubCards({department,name,canWorkshop,onSelect}:{department:string;name:string;canWorkshop:boolean;onSelect:(tab:Destination)=>void}){
 const [counts,setCounts]=useState<{administrative:number|null;warehouse:number|null;workshop:number|null}>({administrative:null,warehouse:null,workshop:null});
 const [loading,setLoading]=useState(true),[error,setError]=useState(''),[revision,setRevision]=useState(0);
 useEffect(()=>{
  let active=true,sequence=0;
  async function read<T>(url:string):Promise<T>{const response=await fetch(url,{credentials:'same-origin',cache:'no-store'});if(!response.ok)throw new Error('No disponible');const data=await response.json();if(!Array.isArray(data))throw new Error('Respuesta inválida');return data as T;}
  async function load(){
   const current=++sequence;setLoading(true);
   const [documents,workshop]=await Promise.allSettled([read<DocumentItem[]>('/api/institutional-documents?area=communications&channel=requirements&view=all'),canWorkshop?read<WorkshopCase[]>('/api/workshop'):Promise.resolve([] as WorkshopCase[])]);
   if(!active||current!==sequence)return;
   const totals=requestHubCounts(documents.status==='fulfilled'?documents.value:[],workshop.status==='fulfilled'?workshop.value:[],department);
   setCounts({administrative:documents.status==='fulfilled'?totals.administrative:null,warehouse:documents.status==='fulfilled'?totals.warehouse:null,workshop:canWorkshop&&workshop.status==='fulfilled'?totals.workshop:null});
   setError(documents.status==='rejected'||canWorkshop&&workshop.status==='rejected'?'Algunos contadores no están disponibles. Puedes abrir cada módulo o volver a actualizar.':'');setLoading(false);
  }
  void load();const refresh=()=>void load();window.addEventListener('support-live-refresh',refresh);
  return()=>{active=false;window.removeEventListener('support-live-refresh',refresh)};
 },[department,canWorkshop,revision]);
 return <div className="request-hub">
  <div className="request-hub-heading"><div><small>TU CENTRO DOCUMENTAL</small><h3>Todo en un solo lugar</h3><p>Pendientes de decisión en Administración y Almacén; casos abiertos en Taller.</p></div><button type="button" disabled={loading} onClick={()=>setRevision(value=>value+1)}><RefreshCw/>{loading?'Actualizando…':'Actualizar'}</button></div>
  {error&&<p className="request-hub-error" role="status">{error}</p>}
  <nav className="adw-entry-cards adw-request-hub" aria-label={`Opciones de archivos y comunicaciones de ${name}`}>
   {cards.map(card=>{const Icon=card.icon,value=card.key==='files'?null:counts[card.key],restricted=card.key==='workshop'&&!canWorkshop;return <button type="button" key={card.key} data-destination={card.key} disabled={restricted} aria-disabled={restricted||undefined} onClick={()=>onSelect(card.key)}>
    <span className="request-hub-icon"><Icon/></span><em>{card.category}</em><strong>{card.title}</strong><small>{card.description}</small>
    <div className="request-hub-metric">{card.key==='files'?<><b>Tu biblioteca</b><span>Documentos organizados y disponibles</span></>:restricted?<><b>—</b><span>Tu perfil no tiene acceso a Taller</span></>:<><b aria-live="polite">{loading?'…':value??'—'}</b><span>{value===null&&!loading?'Contador no disponible':card.key==='workshop'?'solicitudes en proceso':value===1?'solicitud pendiente':'solicitudes pendientes'}</span></>}</div>
    <footer><span>{restricted?'No disponible para este perfil':card.action}</span>{!restricted&&<ArrowUpRight/>}</footer>
   </button>})}
  </nav>
 </div>;
}
