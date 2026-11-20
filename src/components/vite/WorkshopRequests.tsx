import { apiFetch as fetch } from "@/lib/api-fetch";
import {useCallback,useEffect,useMemo,useRef,useState} from "react";
import {ArrowRight,Building2,FileText,Hammer,PackageCheck,QrCode,RefreshCw,Search,Send,Wrench} from "lucide-react";
import type {PortalSession} from "@/lib/session";
import {workshopLabels,type WorkshopCase} from "./workshopWorkflow";
import {EquipmentTracking} from "./WorkshopBoard";
import "./workshopRequests.css";

type Agency={id:string;codigo:string;terminal:string;grupo:string};
const departmentNames:Record<string,string>={TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales",HUMAN_RESOURCES:"Recursos Humanos"};
const documentNumber=()=>`SOL-TAL-${new Date().toISOString().replace(/\D/g,"").slice(0,14)}-${crypto.randomUUID().slice(0,4).toUpperCase()}`;

export default function WorkshopRequests({session,department}:{session:PortalSession;department:string}){
 const[agencies,setAgencies]=useState<Agency[]>([]),[allItems,setItems]=useState<WorkshopCase[]>([]),[query,setQuery]=useState(""),[agencyId,setAgencyId]=useState(""),[equipment,setEquipment]=useState(""),[serial,setSerial]=useState(""),[failure,setFailure]=useState(""),[busy,setBusy]=useState(false),[error,setError]=useState(""),[notice,setNotice]=useState(""),[traceCode,setTraceCode]=useState<string|null>(null);
 const [agencyError,setAgencyError]=useState("");
 const agencyLoadedAt=useRef(0),agencyFlight=useRef(false),workshopFlight=useRef(false);
 const loadAgencies=useCallback(async()=>{
  if(agencyFlight.current||Date.now()-agencyLoadedAt.current<300000)return;
  agencyFlight.current=true;
  try{const response=await fetch("/api/tickets/agencies",{credentials:"same-origin",cache:"no-store"});const body=await response.json();if(!response.ok||!Array.isArray(body.agencies))throw new Error("No fue posible actualizar el catálogo de agencias. Pulsa Actualizar para reintentar.");setAgencies(body.agencies);agencyLoadedAt.current=Date.now();setAgencyError("");}
  catch(reason){setAgencyError(reason instanceof Error?reason.message:"No fue posible cargar las agencias.");}
  finally{agencyFlight.current=false;}
 },[]);
 const load=useCallback(async()=>{
  void loadAgencies();if(workshopFlight.current)return;workshopFlight.current=true;
  try{const response=await fetch("/api/workshop",{credentials:"same-origin",cache:"no-store"});const body=await response.json();if(!response.ok||!Array.isArray(body))throw new Error(body.error||"No fue posible cargar las solicitudes de Taller.");setItems(body);setError("");}
  catch(reason){setError(reason instanceof Error?reason.message:"No fue posible cargar Taller.");}
  finally{workshopFlight.current=false;}
 },[loadAgencies]);
 useEffect(()=>{void load();},[load]);
 const items=useMemo(()=>allItems.filter(item=>item.department===department),[allItems,department]);
 const normalized=query.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es");
 const visibleAgencies=useMemo(()=>agencies.filter(item=>!normalized||`${item.codigo} ${item.terminal} ${item.grupo}`.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").includes(normalized)).slice(0,150),[agencies,normalized]);
 const submit=async()=>{const agency=agencies.find(item=>item.id===agencyId);if(!agency||!equipment.trim()||!serial.trim()||!failure.trim()){setError("Selecciona la agencia y completa equipo, serial y falla reportada.");return;}setBusy(true);setError("");setNotice("");const number=documentNumber();try{const response=await fetch("/api/maintenance/movements",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json"},body:JSON.stringify({agencyId:agency.id,ticketId:null,technicianUserId:null,technicianName:session.displayName,movementType:"TRANSFER_TO_WORKSHOP",equipmentType:equipment.trim(),componentType:null,failureCause:failure.trim(),serialNumber:serial.trim().toUpperCase(),quantity:1,notes:`Equipo retirado de ${agency.codigo} · ${agency.terminal} para reparación.`,department,operationalArea:"WORKSHOP",documentNumber:number,qrToken:`REAL-MNT|${number}`,deliveredByName:session.displayName,receivedByName:"Taller · pendiente de recibir",destinationName:"Taller",signatureData:null,occurredAt:new Date().toISOString()})});const body=await response.json().catch(()=>({}));if(!response.ok)throw new Error(body.error||body.message||"No fue posible enviar la solicitud a Taller.");setEquipment("");setSerial("");setFailure("");setNotice(`${number} enviada directamente a Taller.`);await load();window.dispatchEvent(new Event("support-live-refresh"));}catch(reason){setError(reason instanceof Error?reason.message:"No fue posible crear la solicitud.");}finally{setBusy(false);}};
 return <section className="workshop-requests">
  <header><div><small>SOLICITUDES A TALLER · {departmentNames[department]||department}</small><h3>Equipos retirados para reparación</h3><p>El equipo sale de la agencia, entra directamente a Taller y, al terminar la reparación, Taller lo envía a Almacén.</p></div><div className="workshop-request-header-actions"><button type="button" onClick={()=>setTraceCode("")}><QrCode/>Escanear QR y consultar etapa</button><button type="button" onClick={()=>void load()} disabled={busy}><RefreshCw/>Actualizar</button></div></header>
  {agencyError&&<p className="workshop-request-message error" role="alert">{agencyError}</p>}
  {error&&<p className="workshop-request-message error" role="alert">{error}</p>}{notice&&<p className="workshop-request-message success" role="status">{notice}</p>}
  <div className="workshop-request-form"><label>Buscar agencia<div><Search/><input value={query} onChange={event=>setQuery(event.target.value)} placeholder="Código, nombre o grupo"/></div><select value={agencyId} onChange={event=>setAgencyId(event.target.value)}><option value="">Seleccionar agencia</option>{visibleAgencies.map(agency=><option key={agency.id} value={agency.id}>{agency.codigo} · {agency.terminal} · {agency.grupo}</option>)}</select></label><label>Equipo retirado<input value={equipment} onChange={event=>setEquipment(event.target.value)} placeholder="Ej. computadora, impresora o inversor"/></label><label>Serial<input value={serial} onChange={event=>setSerial(event.target.value)} placeholder="Serial del equipo"/></label><label>Falla o motivo<textarea value={failure} onChange={event=>setFailure(event.target.value)} placeholder="Describe la falla que debe reparar Taller"/></label><button type="button" className="primary" onClick={()=>void submit()} disabled={busy}><Send/>{busy?"Enviando…":"Enviar a Taller"}</button></div>
  <div className="workshop-request-list"><header><Hammer/><strong>Seguimiento por departamento</strong><span>{items.length} solicitudes</span></header>{items.map(item=><article key={item.id}><span className="workshop-request-icon">{item.status==="DELIVERED"?<PackageCheck/>:<Wrench/>}</span><div><strong>{item.equipmentType}</strong><small>{item.documentNumber} · Serial {item.serialNumber}</small><em>{workshopLabels[item.status]||item.status}</em></div><a href={`/api/maintenance/movements/${item.id}/document`} target="_blank" rel="noreferrer"><FileText/>PDF y QR</a><ArrowRight/></article>)}{!items.length&&<div className="workshop-request-empty"><Building2/><strong>No hay solicitudes de este departamento</strong><small>La primera solicitud aparecerá aquí al retirar un equipo de una agencia.</small></div>}</div>
  {traceCode!==null&&<EquipmentTracking initialCode={traceCode} onClose={()=>setTraceCode(null)}/>}
 </section>;
}
