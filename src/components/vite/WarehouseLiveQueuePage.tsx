import { apiFetch as fetch } from "@/lib/api-fetch";
import {useState} from "react";
import {ArrowLeft,LoaderCircle,RefreshCw,Warehouse} from "lucide-react";
import type {PortalSession} from "@/lib/session";
import type {DocumentDetail} from "./DocumentSystemModule";
import type {WarehouseQueueItem} from "@/lib/warehouse-live-queue";
import {useWarehouseQueue} from "@/lib/use-warehouse-queue";
import InstitutionalDocumentDetail from "./InstitutionalDocumentDetail";
import WarehouseLiveQueue from "./WarehouseLiveQueue";
import "./warehouseLiveQueuePage.css";

type QueueRequest=WarehouseQueueItem;

export default function WarehouseLiveQueuePage({session}:{session:PortalSession}){
  const[detail,setDetail]=useState<DocumentDetail|null>(null),[detailError,setError]=useState("");
  const {items,loading,error:queueError,refresh:load}=useWarehouseQueue();const error=detailError||queueError;
  async function open(item:QueueRequest){setError("");try{const response=await fetch(`/api/institutional-documents/${item.id}`,{credentials:"same-origin",cache:"no-store"});if(!response.ok)throw new Error("No se pudo abrir la solicitud.");setDetail(await response.json() as DocumentDetail)}catch(reason){setError((reason as Error).message)}}
  async function refreshDetail(updated?:DocumentDetail){if(!detail)return;if(updated){setDetail(updated);return}await load();const response=await fetch(`/api/institutional-documents/${detail.id}`,{credentials:"same-origin",cache:"no-store"});if(!response.ok||!response.headers.get("content-type")?.includes("json"))throw new Error("No se pudo actualizar la solicitud. Vuelve a abrirla.");setDetail(await response.json() as DocumentDetail)}
  if(detail)return <section className="warehouse-live-queue-page"><button type="button" className="wlqp-back" onClick={()=>setDetail(null)}><ArrowLeft/>Volver a Cola en vivo</button><InstitutionalDocumentDetail item={detail} onBack={()=>setDetail(null)} onChanged={refreshDetail} warehouseAccess warehouseAuthorizeAccess={session.permissions.canAuthorizeWarehouseRequests}/></section>;
  return <section className="warehouse-live-queue-page" aria-labelledby="warehouse-queue-page-title">
    <header><div><span><Warehouse/></span><div><small>DEPARTAMENTO DE ALMACÉN</small><h1 id="warehouse-queue-page-title">Cola en vivo</h1><p>Solicitudes pendientes, autorizadas y rechazadas con control diario de 7:30 a. m. a 10:00 p. m.</p></div></div><button type="button" disabled={loading} onClick={()=>void load()}>{loading?<LoaderCircle className="spin"/>:<RefreshCw/>}Actualizar</button></header>
    {error&&<p className="wlqp-error" role="alert">{error}</p>}
    <WarehouseLiveQueue items={items} loading={loading} onOpen={item=>void open(item)}/>
  </section>;
}
