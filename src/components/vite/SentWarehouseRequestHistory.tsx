import {useCallback,useEffect,useMemo,useState} from "react";
import {Download,FileText,LoaderCircle,Printer,RefreshCw,Send} from "lucide-react";
import InstitutionalDocumentDetail from "./InstitutionalDocumentDetail";
import type {DocumentDetail,DocumentItem} from "./DocumentSystemModule";

const departmentNames:Record<string,string>={TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales"};
const statusNames:Record<string,string>={pending:"Pendiente en Almacén",received:"Recibida por Almacén",approved:"Autorizada",rejected:"Rechazada",correction_requested:"Corrección solicitada"};

export default function SentWarehouseRequestHistory({department,query}:{department:string;query:string}){
  const [items,setItems]=useState<DocumentItem[]>([]),[detail,setDetail]=useState<DocumentDetail|null>(null),[loading,setLoading]=useState(true),[error,setError]=useState("");
  const departmentName=departmentNames[department]||department;
  const load=useCallback(async()=>{setLoading(true);try{const response=await fetch(`/api/institutional-documents?area=communications&channel=requirements&view=sent&department=${encodeURIComponent(departmentName)}`,{credentials:"same-origin",cache:"no-store"});if(!response.ok)throw new Error("No se pudieron cargar las solicitudes PDF enviadas.");setItems(await response.json());setError("");}catch(reason){setError((reason as Error).message)}finally{setLoading(false)}},[departmentName]);
  useEffect(()=>{void load();const refresh=()=>void load();window.addEventListener("support-live-refresh",refresh);return()=>window.removeEventListener("support-live-refresh",refresh)},[load]);
  const visible=useMemo(()=>{const needle=query.trim().toLocaleLowerCase();return needle?items.filter(item=>`${item.title} ${item.description||""} ${item.originalFileName}`.toLocaleLowerCase().includes(needle)):items},[items,query]);
  async function open(item:DocumentItem){const response=await fetch(`/api/institutional-documents/${item.id}`,{credentials:"same-origin",cache:"no-store"});if(!response.ok){setError("No se pudo abrir el formulario.");return}setDetail(await response.json())}
  async function refreshDetail(){if(!detail)return;await load();const response=await fetch(`/api/institutional-documents/${detail.id}`,{credentials:"same-origin",cache:"no-store"});if(response.ok)setDetail(await response.json())}
  if(detail)return <section className="warehouse-sent-document-detail"><InstitutionalDocumentDetail item={detail} onBack={()=>setDetail(null)} onChanged={refreshDetail}/></section>;
  return <section className="warehouse-sent-history" aria-labelledby="warehouse-sent-history-title">
    <header><div><span>FORMULARIOS · SOLICITUDES A ALMACÉN</span><h3 id="warehouse-sent-history-title">Solicitudes PDF enviadas</h3><p>Historial del departamento con el estado real de recepción y autorización en Almacén.</p></div><button type="button" disabled={loading} onClick={()=>void load()}>{loading?<LoaderCircle className="spin"/>:<RefreshCw/>}Actualizar</button></header>
    {error&&<p className="warehouse-sent-error" role="alert">{error}</p>}
    <div>{visible.map(item=><article key={item.id}><span className="warehouse-sent-icon"><Send/></span><div><small>{departmentName}</small><strong>{item.title}</strong><p>{item.description||item.originalFileName}</p><time>{new Date(item.createdUtc).toLocaleString("es-DO",{timeZone:"America/Santo_Domingo"})}</time></div><b className={`warehouse-sent-status ${item.status}`}>{statusNames[item.status]||item.status}</b><footer><button type="button" onClick={()=>void open(item)}><FileText/>Ver formulario</button><a href={`/api/institutional-documents/${item.id}/download`}><Download/>Descargar PDF</a><button type="button" onClick={()=>window.open(`/api/institutional-documents/${item.id}/preview`,"_blank","noopener,noreferrer")}><Printer/>Imprimir</button></footer></article>)}{!loading&&!visible.length&&<div className="warehouse-sent-empty"><FileText/><strong>No hay solicitudes PDF con este criterio</strong><span>Las nuevas solicitudes aparecerán aquí, dentro de Formularios e historial.</span></div>}</div>
  </section>;
}
