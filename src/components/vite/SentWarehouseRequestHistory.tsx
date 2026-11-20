import { apiFetch as fetch } from "@/lib/api-fetch";
import {isWarehouseRequest} from "@/lib/warehouse-live-queue";
import {lazy,Suspense,useCallback,useEffect,useMemo,useState} from "react";
import {Download,FileText,LoaderCircle,Printer,QrCode,RefreshCw,Send} from "lucide-react";
import InstitutionalDocumentDetail from "./InstitutionalDocumentDetail";
import type {DocumentDetail,DocumentItem} from "./DocumentSystemModule";
import {buildReceiptCopy} from './institutionalReceiptPdf';
import {downloadDocument,realLogoData} from './institutionalDocumentAssets';
import {warehouseRequestError} from '@/lib/warehouse-request-error';
import {userDepartments} from '@/lib/user-departments';

const departmentNames:Record<string,string>=Object.fromEntries(userDepartments);
const statusNames:Record<string,string>={pending:"Pendiente en Almacén",received:"Recibida por Almacén",approved:"Autorizada",rejected:"Rechazada",cancelled:"Anulada",correction_requested:"Corrección solicitada"};
const EquipmentTracking=lazy(()=>import("./WorkshopBoard").then(module=>({default:module.EquipmentTracking})));

export default function SentWarehouseRequestHistory({department,query,readOnly=false}:{department:string;query:string;readOnly?:boolean}){
  const [items,setItems]=useState<DocumentItem[]>([]),[detail,setDetail]=useState<DocumentDetail|null>(null),[loading,setLoading]=useState(true),[error,setError]=useState("");
  const [traceNumber,setTraceNumber]=useState<string|null>(null);
  const departmentName=departmentNames[department]||department;
  const load=useCallback(async()=>{setLoading(true);try{const params=new URLSearchParams({area:'communications',channel:'requirements',view:readOnly?'mine':'all'});if(!readOnly)params.set('department',departmentName);const response=await fetch(`/api/institutional-documents?${params}`,{credentials:"same-origin",cache:"no-store"});if(!response.ok)throw await warehouseRequestError(response,"No se pudieron cargar las solicitudes de equipo.");setItems((await response.json() as DocumentItem[]).filter(isWarehouseRequest));setError("");}catch(reason){setError((reason as Error).message)}finally{setLoading(false)}},[departmentName,readOnly]);
  useEffect(()=>{void load();const refresh=()=>void load();window.addEventListener("support-live-refresh",refresh);return()=>window.removeEventListener("support-live-refresh",refresh)},[load]);
  const visible=useMemo(()=>{const needle=query.trim().toLocaleLowerCase();return needle?items.filter(item=>`${item.title} ${item.description||""} ${item.originalFileName} ${item.warehouseRequest?.agencyName||""} ${item.warehouseRequest?.agencyCode||""} ${item.warehouseRequest?.ticketNumber||""}`.toLocaleLowerCase().includes(needle)):items},[items,query]);
  async function open(item:DocumentItem){const response=await fetch(`/api/institutional-documents/${item.id}`,{credentials:"same-origin",cache:"no-store"});if(!response.ok){setError("No se pudo abrir el formulario.");return}setDetail(await response.json())}
  async function refreshDetail(){if(!detail)return;await load();const response=await fetch(`/api/institutional-documents/${detail.id}`,{credentials:"same-origin",cache:"no-store"});if(response.ok)setDetail(await response.json())}
  async function exportPdf(item:DocumentItem,print=false){
    const popup=print?window.open('','_blank'):null;if(popup)popup.opener=null;
    try{const response=await fetch(`/api/institutional-documents/${item.id}`,{credentials:'same-origin',cache:'no-store'});if(!response.ok)throw new Error('No se pudo consultar el formulario.');const doc=await response.json() as DocumentDetail;const file=await fetch(doc.fileUrl,{credentials:'same-origin',cache:'no-store'});if(!file.ok)throw new Error('No se pudo cargar el PDF.');const bytes=await buildReceiptCopy(new Uint8Array(await file.arrayBuffer()),doc,await realLogoData(),window.location.origin);if(print&&popup){const url=URL.createObjectURL(new Blob([bytes.slice().buffer],{type:'application/pdf'}));popup.location.href=url;window.setTimeout(()=>URL.revokeObjectURL(url),60000)}else downloadDocument(bytes,doc.originalFileName)}catch(reason){popup?.close();setError((reason as Error).message)}
  }
  if(detail)return <section className="warehouse-sent-document-detail"><InstitutionalDocumentDetail item={detail} onBack={()=>setDetail(null)} onChanged={refreshDetail}/></section>;
  return <section className="warehouse-sent-history" aria-labelledby="warehouse-sent-history-title">
    <header><div><span>FORMULARIOS · SOLICITUDES A ALMACÉN</span><h3 id="warehouse-sent-history-title">Solicitudes de equipo</h3><p>{readOnly?"Tus solicitudes y su estado de recepción y autorización en Almacén.":"Historial del departamento con el estado real de recepción y autorización en Almacén."}</p><span className="warehouse-pdf-legacy-label">Solicitudes PDF enviadas</span></div><button type="button" disabled={loading} onClick={()=>void load()}>{loading?<LoaderCircle className="spin"/>:<RefreshCw/>}Actualizar</button></header>
    {error&&<p className="warehouse-sent-error" role="alert">{error}</p>}
    <div>{visible.map(item=><article key={item.id}><span className="warehouse-sent-icon"><Send/></span><div><small>{departmentName} · {item.warehouseRequest?.agencyName||item.warehouseRequest?.agencyCode||"Uso interno"}</small><strong>{item.title}</strong><p>{item.description||item.originalFileName}{item.warehouseRequest?.ticketNumber?` · Ticket #${item.warehouseRequest.ticketNumber}`:""}</p><time>{new Date(item.createdUtc).toLocaleString("es-DO",{timeZone:"America/Santo_Domingo"})}</time></div><b className={`warehouse-sent-status ${item.status}`}>{statusNames[item.status]||item.status}</b><footer className="warehouse-sent-actions"><button type="button" className="primary" onClick={()=>void (readOnly?exportPdf(item,true):open(item))}><FileText/>{readOnly?"Ver comprobante PDF":"Ver formulario"}</button>{item.warehouseRequest?.number&&<button type="button" onClick={()=>setTraceNumber(item.warehouseRequest!.number)}><QrCode/>Consultar etapa</button>}<button type="button" onClick={()=>void exportPdf(item)}><Download/>Descargar comprobante PDF</button><button type="button" onClick={()=>void exportPdf(item,true)}><Printer/>Imprimir</button></footer></article>)}{!loading&&!visible.length&&<div className="warehouse-sent-empty"><FileText/><strong>No hay solicitudes de equipo con este criterio</strong><span>Las nuevas solicitudes aparecerán aquí, dentro de Formularios e historial.</span></div>}</div>
    {traceNumber&&<Suspense fallback={<p role="status">Cargando trazabilidad…</p>}><EquipmentTracking initialCode={`REAL-MNT|${traceNumber}`} onClose={()=>setTraceNumber(null)}/></Suspense>}
  </section>;
}
