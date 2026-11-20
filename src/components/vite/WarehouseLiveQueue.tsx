import {useEffect,useMemo,useState} from "react";
import {CalendarDays,CheckCircle2,Clock3,Eye,Filter,Inbox,Search,XCircle} from "lucide-react";
import {deviceDate,deviceTimestamp} from "@/lib/display";
import {isWarehouseRequest} from "@/lib/warehouse-live-queue";
import {warehouseQueueCode,warehouseQueueStatus,warehouseRequestResultForOperationalDate,warehouseRequestsInOperationalQueue,type WarehouseQueueStatus} from "@/lib/warehouse-live-queue";
import type {WarehouseQueueItem} from "@/lib/warehouse-live-queue";
import "./warehouseLiveQueue.css";

type QueueRequest=WarehouseQueueItem;
type QueueFilter="ALL"|WarehouseQueueStatus;

const statusLabels:Record<WarehouseQueueStatus,string>={PENDING:"Pendiente",AUTHORIZED:"Autorizada",REJECTED:"Rechazada"};
const statusIcons={PENDING:Clock3,AUTHORIZED:CheckCircle2,REJECTED:XCircle};
const localDateValue=(date:Date)=>`${date.getFullYear()}-${String(date.getMonth()+1).padStart(2,"0")}-${String(date.getDate()).padStart(2,"0")}`;
const formatTime=(value:string|number)=>new Date(typeof value==="number"?value:deviceTimestamp(value)).toLocaleString("es-DO",{dateStyle:"short",timeStyle:"short"});

export default function WarehouseLiveQueue({items,loading,onOpen}:{items:QueueRequest[];loading:boolean;onOpen:(item:QueueRequest)=>void}){
  const [now,setNow]=useState(()=>Date.now());
  const [queueDate,setQueueDate]=useState(()=>localDateValue(new Date()));
  const [filter,setFilter]=useState<QueueFilter>("ALL");
  const [query,setQuery]=useState("");
  useEffect(()=>{const timer=window.setInterval(()=>setNow(Date.now()),30000);return()=>window.clearInterval(timer)},[]);

  const warehouseItems=useMemo(()=>items.filter(isWarehouseRequest),[items]);
  const live=useMemo(()=>warehouseRequestsInOperationalQueue(warehouseItems,new Date(now)),[warehouseItems,now]);
  const liveGroups=useMemo(()=>({
    PENDING:live.filter(item=>warehouseQueueStatus(item)==="PENDING"),
    AUTHORIZED:live.filter(item=>warehouseQueueStatus(item)==="AUTHORIZED"),
    REJECTED:live.filter(item=>warehouseQueueStatus(item)==="REJECTED"),
  }),[live]);
  const ledger=useMemo(()=>warehouseItems.flatMap(item=>{
    const result=warehouseRequestResultForOperationalDate(item,queueDate);
    return result?[{item,...result}]:[];
  }).filter(entry=>{
    const text=`${entry.item.title} ${entry.item.originalFileName} ${entry.item.department} ${entry.item.ownerDisplayName||entry.item.ownerUserName}`.toLocaleLowerCase();
    return(filter==="ALL"||entry.result===filter)&&text.includes(query.trim().toLocaleLowerCase());
  }).sort((left,right)=>deviceTimestamp(right.item.createdUtc)-deviceTimestamp(left.item.createdUtc)),[filter,warehouseItems,query,queueDate]);
  const current=new Date(now),hour=current.getHours()+current.getMinutes()/60;
  const queueOpen=hour>=7.5&&hour<22;

  return <section className="warehouse-live-queue" aria-labelledby="warehouse-live-title">
    <header className="wlq-heading"><div><small>COLA EN VIVO · 07:30 A. M. — 10:00 P. M.</small><h4 id="warehouse-live-title">Control diario de solicitudes</h4><p>Las solicitudes que entren hoy aparecen en vivo. Al cerrar la jornada, las que no tengan decisión quedan registradas como pendientes en su fecha de entrada.</p></div><span className={queueOpen?"open":"closed"}><i/>{queueOpen?"Jornada activa":"Jornada cerrada"}</span></header>
    <div className="wlq-columns">
      {(["PENDING","AUTHORIZED","REJECTED"] as WarehouseQueueStatus[]).map(group=>{const Icon=statusIcons[group],requests=liveGroups[group];return <section className={`wlq-column ${group.toLowerCase()}`} key={group}><header><Icon/><strong>{statusLabels[group]}s</strong><b>{requests.length}</b></header><div>{requests.map(item=><button type="button" key={item.id} onClick={()=>onOpen(item)}><span><b>{warehouseQueueCode(item)}</b><small>{formatTime(item.createdUtc)}</small></span><strong>{item.title}</strong><small>{item.department} · {item.ownerDisplayName||item.ownerUserName}</small><em><Eye/>Abrir solicitud</em></button>)}{!loading&&!requests.length&&<p>{queueOpen?`No hay solicitudes ${statusLabels[group].toLocaleLowerCase()}s en la jornada actual.`:"La cola en vivo abre a las 7:30 a. m."}</p>}</div></section>})}
    </div>
    <section className="wlq-ledger">
      <header><div><small>HISTORIAL POR FECHA DE ENTRADA</small><h4>Tabla diaria de pendientes, autorizadas y rechazadas</h4></div><div><label><CalendarDays/><input aria-label="Fecha de la cola de Almacén" type="date" value={queueDate} onChange={event=>setQueueDate(event.target.value)}/></label><label><Filter/><select aria-label="Resultado de la cola de Almacén" value={filter} onChange={event=>setFilter(event.target.value as QueueFilter)}><option value="ALL">Todos los resultados</option><option value="PENDING">Solo pendientes</option><option value="AUTHORIZED">Solo autorizadas</option><option value="REJECTED">Solo rechazadas</option></select></label><label className="wlq-search"><Search/><input aria-label="Buscar en la cola de Almacén" value={query} onChange={event=>setQuery(event.target.value)} placeholder="Buscar solicitud…"/></label></div></header>
      <div className="wlq-table-wrap"><table><thead><tr><th>Turno</th><th>Solicitud</th><th>Departamento</th><th>Entrada</th><th>Resultado</th><th>Decisión</th><th/></tr></thead><tbody>{ledger.map(entry=><tr key={entry.item.id}><td><b>{warehouseQueueCode(entry.item)}</b></td><td><strong>{entry.item.title}</strong><small>{entry.item.ownerDisplayName||entry.item.ownerUserName}</small></td><td>{entry.item.department}</td><td>{formatTime(entry.item.createdUtc)}</td><td><span className={entry.result.toLowerCase()}>{statusLabels[entry.result]}</span></td><td>{entry.decisionAt?formatTime(entry.decisionAt):"Sin decisión durante la jornada"}</td><td><button type="button" onClick={()=>onOpen(entry.item)}><Eye/>Ver</button></td></tr>)}</tbody></table>{!loading&&!ledger.length&&<div className="wlq-empty"><Inbox/><strong>Sin solicitudes para esta fecha</strong><span>No hay entradas entre 7:30 a. m. y 10:00 p. m. con estos filtros.</span></div>}</div>
      <footer><Clock3/><span>Una solicitud pendiente al llegar las 10:00 p. m. conserva su fecha original y permanece en esta tabla aunque continúe abierta otro día.</span><time>{deviceDate(now).toLocaleTimeString("es-DO",{hour:"2-digit",minute:"2-digit"})}</time></footer>
    </section>
  </section>;
}
