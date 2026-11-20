import { apiFetch as fetch } from "@/lib/api-fetch";
import {useEffect,useMemo,useRef,useState} from "react";
import {Boxes,FileText,PackagePlus,Plus,Send,Trash2,X} from "lucide-react";
import type {PortalSession} from "@/lib/session";
import {saveWarehouseRequest} from "@/lib/save-warehouse-request";
import {warehouseRequestError} from "@/lib/warehouse-request-error";
import {loadWarehouseGroups} from "@/lib/warehouse-request-groups";
import {buildWarehouseRequestPdf} from "./warehouseRequestPdf";
import "./warehouseRequestComposer.css";
import SearchSelect from "./SearchSelect";
import {findVoiceAgency,parseVoiceWarehouseLines,prepareVoiceWarehouseBatch,type VoiceWarehouseBatch} from "@/lib/voice-operation-assistant";

type Agency={id:string;codigo:string;terminal:string;grupo:string};
type RequestLine={id:string;productId:string;name:string;productQuery:string;manual:boolean;quantity:number;note:string;agencyId:string;agencyQuery:string};
type AgencyFailure={department:string;category:string};
type InventoryOption={department:string;productId:string;name:string;available:number;serials:string[];isConsumable?:boolean;warehouse:string;unitId:string;division:string;group:string;lastPurchaseDate?:string|null;tracksSerial:boolean};
type LinkedTicket={id:string;ticketNumber:number;codigo:string;terminal:string;grupo:string;subject:string};
type FailureDepartment={code:string;name:string;categories:{code:string;name:string;isActive:boolean}[]};
type WarehouseDestination='AGENCY'|'MULTI_AGENCY'|'DEPARTMENT'|'GROUP';
type WarehouseDestinationOption={value:WarehouseDestination;label:string};

const departmentNames:Record<string,string>={WAREHOUSE:"Almacén",TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales",HUMAN_RESOURCES:"Recursos Humanos",ADMINISTRATIVE:"Administración",SUPERVISION:"Supervisión",WORKSHOP:"Taller",COMMUNICATION:"Comunicación",COMMERCIALIZATION:"Comercialización",TREASURY:"Tesorería",ACCOUNTING:"Contabilidad",GENERAL_AUDIT:"Auditoría General",VAULT:"Bóveda",CASHIER:"Caja",CALL_CENTER:"Call Center",TECHNICAL_FAILURE:"Avería Técnica"};
const newLine=():RequestLine=>({id:crypto.randomUUID(),productId:"",name:"",productQuery:"",manual:false,quantity:1,note:"",agencyId:"",agencyQuery:""});

export function warehouseDestinationOptions(session:Pick<PortalSession,'role'>,weekendLoan=false,linkedTicket=false):WarehouseDestinationOption[]{
  if(weekendLoan||linkedTicket)return [];
  if(session.role==='GroupAdministrator')return [{value:'AGENCY',label:'Para una agencia'},{value:'MULTI_AGENCY',label:'Para varias agencias'}];
  return [
    {value:'GROUP',label:'Para un grupo'},
    {value:'AGENCY',label:'Para una agencia'},
    {value:'MULTI_AGENCY',label:'Para varias agencias'},
    {value:'DEPARTMENT',label:'Para mi departamento'},
  ];
}

const normalizeProductSearch=(value:string)=>value.trim().normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es");
export function findWarehouseTicketAgency(agencies:Agency[],ticket:LinkedTicket){
  const matches=agencies.filter(item=>item.codigo===ticket.codigo&&
    (item.grupo||'Sin grupo registrado')===(ticket.grupo||'Sin grupo registrado'));
  return matches.length===1?matches[0]:undefined;
}
export function warehouseProductMatches(product:Pick<InventoryOption,"productId"|"name"|"group"|"division"|"warehouse">,query:string){
  const needle=normalizeProductSearch(query);
  return !needle||normalizeProductSearch(`${product.productId} ${product.name} ${product.group} ${product.division} ${product.warehouse}`).includes(needle);
}
export function warehouseAgenciesForArticle(agencies:Agency[],group:string|null,query:string,selectedId:string){
  const needle=normalizeProductSearch(query);
  const matches=agencies.filter(item=>(group===null||!!group&&item.grupo.trim()===group.trim())&&item.id!==selectedId&&(!needle||normalizeProductSearch(`${item.codigo} ${item.terminal} ${item.grupo}`).includes(needle)));
  const selected=agencies.find(item=>item.id===selectedId&&(group===null||item.grupo.trim()===group.trim()));
  if(selected)matches.unshift(selected);
  return matches;
}

function requestNumber(){
  const now=new Date();
  const stamp=[now.getFullYear(),String(now.getMonth()+1).padStart(2,"0"),String(now.getDate()).padStart(2,"0"),String(now.getHours()).padStart(2,"0"),String(now.getMinutes()).padStart(2,"0"),String(now.getSeconds()).padStart(2,"0")].join("");
  return `SOL-ALM-${stamp}-${crypto.randomUUID().slice(0,4).toUpperCase()}`;
}

export function weekendLoanPeriodError(from:string,to:string,returnBy:string){
  if(!from||!to||!returnBy)return 'Completa las tres fechas del préstamo.';
  const start=new Date(`${from}T12:00:00Z`),end=new Date(`${to}T12:00:00Z`),returned=new Date(`${returnBy}T12:00:00Z`);
  if([start,end,returned].some(value=>Number.isNaN(value.getTime())))return 'Las fechas del préstamo no son válidas.';
  const days=(later:Date,earlier:Date)=>(later.getTime()-earlier.getTime())/86_400_000;
  if(![5,6].includes(start.getUTCDay())||end.getUTCDay()!==0||days(end,start)<1||days(end,start)>2)return 'El préstamo debe iniciar viernes o sábado y terminar el domingo.';
  if(days(returned,end)<1||days(returned,end)>7)return 'La devolución de sobrantes debe registrarse entre 1 y 7 días después del domingo.';
  return '';
}

export default function WarehouseRequestComposer({session,department,agencies:providedAgencies,linkedTicket,weekendLoan=false,initialVoiceCommand,initialVoiceBatch,autoSubmitVoice=false,onClose,onSaved}:{session:PortalSession;department:string;agencies:Agency[];linkedTicket?:LinkedTicket|null;departmentOnly?:boolean;weekendLoan?:boolean;initialVoiceCommand?:string;initialVoiceBatch?:VoiceWarehouseBatch;autoSubmitVoice?:boolean;onClose:()=>void;onSaved:(number:string)=>void|Promise<void>}){
  const submitting=useRef(false);
  const [loanFrom,setLoanFrom]=useState(''),[loanTo,setLoanTo]=useState(''),[loanReturn,setLoanReturn]=useState('');
  const destinationOptions=warehouseDestinationOptions(session,weekendLoan,!!linkedTicket);
  const [destination,setDestination]=useState<WarehouseDestination>(initialVoiceBatch?(initialVoiceBatch.targets.length>1?'MULTI_AGENCY':'AGENCY'):initialVoiceCommand?'AGENCY':(weekendLoan||department==='WAREHOUSE')&&!linkedTicket?'DEPARTMENT':'AGENCY');
  const isSupervisor=session.role==='GroupAdministrator';
  const internal=!isSupervisor&&!linkedTicket&&destination==='DEPARTMENT';
  const groupDestination=!linkedTicket&&!internal&&destination==='GROUP';
  const [agencyGroup,setAgencyGroup]=useState(linkedTicket?.grupo||'');
  const multipleAgencies=!internal&&!linkedTicket&&destination==='MULTI_AGENCY';
  const [agencyGroups,setAgencyGroups]=useState<string[]>([]),[loadingGroups,setLoadingGroups]=useState(false),[groupError,setGroupError]=useState(''),[groupReload,setGroupReload]=useState(0);
  useEffect(()=>{
    if(!groupDestination)return;
    const controller=new AbortController();setLoadingGroups(true);setGroupError('');
    loadWarehouseGroups(controller.signal).then(groups=>{if(!controller.signal.aborted)setAgencyGroups(groups);}).catch(reason=>{if(!controller.signal.aborted)setGroupError(reason instanceof Error?reason.message:'No se pudieron cargar los grupos.');}).finally(()=>{if(!controller.signal.aborted)setLoadingGroups(false);});
    return()=>controller.abort();
  },[groupDestination,groupReload]);
  const [loadedAgencies,setLoadedAgencies]=useState<Agency[]>([]),[agencyError,setAgencyError]=useState(''),[loadingAgencies,setLoadingAgencies]=useState(false);
  const agencies=groupDestination?loadedAgencies:providedAgencies.length?providedAgencies:loadedAgencies;
  useEffect(()=>{
    if(internal||groupDestination||providedAgencies.length){setLoadingAgencies(false);return;}
    const controller=new AbortController();setLoadingAgencies(true);setAgencyError('');
    fetch('/api/tickets/agencies',{credentials:'same-origin',cache:'no-store',signal:controller.signal})
      .then(async response=>{if(!response.ok)throw await warehouseRequestError(response,'No se pudieron cargar las agencias.');const data=await response.json();if(!Array.isArray(data.agencies))throw new Error('El servidor no devolvió la lista de agencias.');return data.agencies as Agency[];})
      .then(rows=>{if(!controller.signal.aborted)setLoadedAgencies(rows);})
      .catch(reason=>{if(!controller.signal.aborted)setAgencyError(reason instanceof Error?reason.message:'No se pudieron cargar las agencias.');})
      .finally(()=>{if(!controller.signal.aborted)setLoadingAgencies(false);});
    return()=>controller.abort();
  },[internal,groupDestination,providedAgencies.length]);
  const linkedAgency=linkedTicket?findWarehouseTicketAgency(agencies,linkedTicket):undefined;
  const[number]=useState(requestNumber),[agencyId,setAgencyId]=useState(linkedAgency?.id||""),[priority,setPriority]=useState("MEDIUM"),[notes,setNotes]=useState(linkedTicket?`Equipo requerido para resolver el ticket #${linkedTicket.ticketNumber}: ${linkedTicket.subject}`:""),[lines,setLines]=useState<RequestLine[]>(()=>[newLine()]),[inventory,setInventory]=useState<InventoryOption[]>([]),[loadingInventory,setLoadingInventory]=useState(true),[busy,setBusy]=useState(false),[error,setError]=useState("");
  const [agencyNumbers,setAgencyNumbers]=useState<Record<string,string>>({}),[agencyFailures,setAgencyFailures]=useState<Record<string,AgencyFailure>>({});
  const [failureDepartments,setFailureDepartments]=useState<FailureDepartment[]>([]),[failureCatalogError,setFailureCatalogError]=useState("");
  const agency=groupDestination?{id:'00000000-0000-0000-0000-000000000000',codigo:'GRUPO',terminal:`Grupo ${agencyGroup}`,grupo:agencyGroup}:internal?{id:'00000000-0000-0000-0000-000000000000',codigo:'USO INTERNO',terminal:`Departamento de ${departmentNames[department]||department}`,grupo:'Grupo Tejeda'}:agencies.find(item=>item.id===agencyId);
  useEffect(()=>{if(linkedAgency){setAgencyId(linkedAgency.id);setAgencyGroup(linkedAgency.grupo);}},[linkedAgency]);
  const officialCatalog=useMemo(()=>[...inventory].sort((a,b)=>a.group.localeCompare(b.group,"es")||a.name.localeCompare(b.name,"es")||a.productId.localeCompare(b.productId,"es")),[inventory]);
  const failureOptions=failureDepartments.filter(item=>['TECHNOLOGY','GENERAL_SERVICES'].includes(item.code));
  const agencyOptions=agencies.map(item=>({value:item.id,label:`${item.codigo} · ${item.terminal}`,description:item.grupo}));
  useEffect(()=>{if(internal||groupDestination||linkedTicket)return;const controller=new AbortController();fetch('/api/support/catalog',{credentials:'same-origin',cache:'no-store',signal:controller.signal}).then(async response=>{if(!response.ok)throw await warehouseRequestError(response,'No se pudo cargar el catálogo de fallas.');return await response.json() as {departments:FailureDepartment[]}}).then(data=>{if(!controller.signal.aborted){setFailureDepartments(data.departments);setFailureCatalogError('');}}).catch(reason=>{if(!controller.signal.aborted)setFailureCatalogError(reason instanceof Error?reason.message:'No se pudo cargar el catálogo de fallas.')});return()=>controller.abort()},[internal,groupDestination,linkedTicket]);
  const setLine=(id:string,patch:Partial<RequestLine>)=>setLines(current=>current.map(line=>line.id===id?{...line,...patch}:line));
  const selectLineAgency=(id:string,selectedId:string)=>{setLine(id,{agencyId:selectedId,agencyQuery:""});if(selectedId)setAgencyNumbers(current=>current[selectedId]?current:{...current,[selectedId]:Object.keys(current).length?requestNumber():number});};
  const setAgencyFailure=(selectedId:string,category:string)=>setAgencyFailures(current=>({...current,[selectedId]:{...current[selectedId],category}}));
  const applyVoiceRequest=(transcript:string)=>{
    const voiceAgency=findVoiceAgency(transcript,agencies);
    const parsed=parseVoiceWarehouseLines(transcript,officialCatalog);
    if(voiceAgency&&!internal&&!groupDestination){setDestination('AGENCY');setAgencyId(voiceAgency.id);}
    if(parsed.length)setLines(parsed.map(item=>({...newLine(),productId:item.productId,name:item.name,productQuery:"",manual:item.manual,quantity:item.quantity})));
    setNotes(transcript);
    const missing=[!internal&&!groupDestination&&!voiceAgency?'agencia':'',!parsed.length?'artículos y cantidades':''].filter(Boolean);
    return missing.length?`Preparé lo reconocido. Completa ${missing.join(' y ')} antes de enviar.`:'Solicitud preparada. Revisa los artículos y pulsa Enviar solicitud para crearla.';
  };
  const appliedVoiceCommand=useRef(''),autoSubmittedVoice=useRef(false);const[voiceReady,setVoiceReady]=useState(false);
  useEffect(()=>{if(!initialVoiceCommand||loadingInventory||loadingAgencies||!officialCatalog.length||!agencies.length||appliedVoiceCommand.current===initialVoiceCommand)return;appliedVoiceCommand.current=initialVoiceCommand;const result=applyVoiceRequest(initialVoiceCommand);setVoiceReady(result.startsWith('Solicitud preparada'))},[initialVoiceCommand,loadingInventory,loadingAgencies,officialCatalog.length,agencies.length]);
  const appliedVoiceBatch=useRef(false);
  useEffect(()=>{if(!initialVoiceBatch||appliedVoiceBatch.current||loadingInventory||loadingAgencies||!officialCatalog.length||!agencies.length)return;const prepared=prepareVoiceWarehouseBatch(initialVoiceBatch,agencies.map(item=>item.id),officialCatalog.map(item=>item.productId));if(!prepared.valid){setError('La conversación de voz contiene una agencia, artículo o cantidad que ya no está disponible en el catálogo.');return}appliedVoiceBatch.current=true;setDestination(prepared.multi?'MULTI_AGENCY':'AGENCY');setAgencyId(prepared.multi?'':initialVoiceBatch.targets[0].agencyId);const numbers:Record<string,string>={};initialVoiceBatch.targets.forEach((target,index)=>{numbers[target.agencyId]=index===0?number:requestNumber()});setAgencyNumbers(numbers);setLines(prepared.lines.map(item=>({...newLine(),productId:item.productId,name:item.name,productQuery:'',manual:false,quantity:item.quantity,agencyId:item.agencyId,agencyQuery:''})));setNotes(initialVoiceBatch.transcript);setVoiceReady(true)},[initialVoiceBatch,loadingInventory,loadingAgencies,officialCatalog.length,agencies.length,number]);
  useEffect(()=>{let cancelled=false;setLoadingInventory(true);fetch("/api/warehouse/fulfillment/inventory",{credentials:"same-origin",cache:"no-store"}).then(async response=>{if(!response.ok)throw await warehouseRequestError(response,"No se pudo consultar el inventario disponible de Almacén.");return await response.json() as InventoryOption[]}).then(rows=>{if(!cancelled){setInventory(rows);setError("")}}).catch(reason=>{if(!cancelled)setError((reason as Error).message)}).finally(()=>{if(!cancelled)setLoadingInventory(false)});return()=>{cancelled=true}},[department]);
  const stockProblem=()=>lines.some(line=>line.manual?line.name.trim().length<2:!officialCatalog.some(item=>item.productId===line.productId))?"Selecciona un artículo del catálogo o escribe el artículo que todavía no existe en inventario.":"";
  const submit=async()=>{
    if(submitting.current)return;
    setError("");
    const loanPeriodError=weekendLoan?weekendLoanPeriodError(loanFrom,loanTo,loanReturn):'';if(loanPeriodError){setError(loanPeriodError);return;}
    if(groupDestination&&(loadingGroups||groupError||!agencyGroups.includes(agencyGroup))){setError('Selecciona un grupo válido del listado antes de enviar.');return;}
    const quantityGroups=multipleAgencies?Object.values(lines.reduce<Record<string,RequestLine[]>>((groups,line)=>{if(line.agencyId)(groups[line.agencyId]??=[]).push(line);return groups;},{})):[lines];
    if(!lines.length||lines.length>30||lines.some(line=>!line.name||!Number.isInteger(line.quantity)||line.quantity<1||line.quantity>10000)||quantityGroups.some(group=>group.reduce((total,line)=>total+line.quantity,0)>10000)){setError("Completa los artículos y cantidades: máximo 10,000 unidades por solicitud y agencia.");return;}
    const inventoryError=stockProblem();if(inventoryError){setError(inventoryError);return;}
    const lineGroups=!multipleAgencies?[[...lines]]:Object.values(lines.reduce<Record<string,RequestLine[]>>((groups,line)=>{if(line.agencyId)(groups[line.agencyId]??=[]).push(line);return groups;},{}));
    if(multipleAgencies&&lines.some(line=>!line.agencyId||!agencies.some(item=>item.id===line.agencyId))){setError('Selecciona una agencia válida junto a cada artículo.');return;}
    const targets=lineGroups.map(group=>({agency:!multipleAgencies?agency:agencies.find(item=>item.id===group[0].agencyId),number:!multipleAgencies?number:agencyNumbers[group[0].agencyId],lines:group,failure:!internal&&!groupDestination&&!linkedTicket?agencyFailures[multipleAgencies?group[0].agencyId:agencyId]:undefined}));
    if(!targets.length||targets.some(item=>!item.agency||!item.number)){setError("Selecciona el destino que requiere los artículos.");return;}
    if(targets.some(item=>item.failure&&!failureOptions.some(dept=>dept.code===item.failure?.department&&dept.categories.some(category=>category.isActive&&category.code===item.failure?.category)))){setError("Selecciona Tecnología o Servicios Generales y una falla válida para el reemplazo.");return;}
    submitting.current=true;setBusy(true);
    let saved=0;
    try{
      for(const target of targets){
      const targetAgency=target.agency!;
      const targetNumber=target.number;
      const requestLines=target.lines.map(line=>({kind:"EQUIPMENT" as const,productId:line.productId||undefined,name:line.name.trim(),quantity:line.quantity,serial:"",note:line.note.trim()}));
      const failureName=failureOptions.find(item=>item.code===target.failure?.department)?.categories.find(item=>item.code===target.failure?.category)?.name;
      const requestNotes=linkedTicket?`Ticket vinculado #${linkedTicket.ticketNumber} · ${linkedTicket.subject}${notes.trim()?`\n${notes.trim()}`:""}`:failureName?`${notes.trim()}${notes.trim()?"\n":""}Falla: ${failureName}.`:notes;
      const failureDescription=failureName?`Falla: ${failureName}. Artículos: ${target.lines.map(line=>`${line.name.trim()} x${line.quantity}`).join(", ")}. ${notes.trim()}`.slice(0,1200):null;
      const bytes=await buildWarehouseRequestPdf({number:targetNumber,department:departmentNames[department]||department,agency:targetAgency,priority,notes:weekendLoan?`PRÉSTAMO DE FIN DE SEMANA: ${loanFrom} a ${loanTo}. Devolver sobrantes: ${loanReturn}. ${requestNotes}`:requestNotes,lines:requestLines,createdBy:session.displayName,createdByLogin:session.email||session.id,origin:window.location.origin});
      const form=new FormData();
      form.append("file",new Blob([bytes],{type:"application/pdf"}),`${targetNumber}.pdf`);
      form.append("area","communications");form.append("channel","requirements");form.append("title",`Solicitud a Almacén ${targetNumber}`);
      form.append("description",`${target.lines.length} artículo(s) · ${internal||groupDestination?targetAgency.terminal:`Agencia ${targetAgency.codigo}`}${groupDestination?` · Grupo ${agencyGroup}`:""}${linkedTicket?` · Ticket #${linkedTicket.ticketNumber}`:""} · Catálogo oficial de Almacén`);
      form.append("recipientDepartment","Almacén");form.append("sourceDepartment",departmentNames[department]||department);
      form.append("warehouseRequest",JSON.stringify({weekendLoan:weekendLoan?{from:loanFrom,to:loanTo,returnBy:loanReturn}:undefined,destinationType:internal?'DEPARTMENT':groupDestination?'GROUP':'AGENCY',number:targetNumber,agencyId:targetAgency.id,agencyCode:targetAgency.codigo,agencyName:targetAgency.terminal,agencyGroup:targetAgency.grupo,installerId:"",installerName:"",priority,lines:requestLines,ticketId:linkedTicket?.id||null,ticketNumber:linkedTicket?.ticketNumber||null,failureDepartment:target.failure?.department||null,failureCategory:target.failure?.category||null,failureDescription}));
      await saveWarehouseRequest(form,targetNumber);
      saved++;
      // Standalone requests are tracked by their institutional document. Only
      // ticket-linked requests also need the support maintenance movement.
      if(!linkedTicket)continue;
      const totalQuantity=target.lines.reduce((total,line)=>total+line.quantity,0),firstLine=target.lines[0];
      const lineSummary=target.lines.map((line,index)=>`${index+1}. Equipo: ${line.name} · ${line.quantity}${line.note.trim()?` · ${line.note.trim()}`:""}`).join("\n");
      const equipmentType=`${firstLine.name}${target.lines.length>1?` + ${target.lines.length-1} equipo${target.lines.length>2?"s":""}`:""}`.slice(0,100);
      const movementResponse=await fetch("/api/maintenance/movements",{method:"POST",credentials:"same-origin",headers:{"Content-Type":"application/json"},body:JSON.stringify({agencyId:targetAgency.id,ticketId:linkedTicket?.id||null,technicianUserId:null,technicianName:session.displayName,movementType:"REQUEST",equipmentType,componentType:null,failureCause:`Solicitud de ${departmentNames[department]||department} a Almacén`,serialNumber:null,quantity:totalQuantity,notes:`${requestNotes.trim()?`${requestNotes.trim()}\n\n`:""}Equipos solicitados:\n${lineSummary}`.slice(0,2000),department,operationalArea:"WAREHOUSE",documentNumber:targetNumber,qrToken:`REAL-MNT|${targetNumber}`,deliveredByName:null,receivedByName:null,destinationName:"Almacén",signatureData:null,occurredAt:new Date().toISOString()})});
      if(!movementResponse.ok){const body=await movementResponse.json().catch(()=>({}));if(movementResponse.status!==409)throw new Error(body.error||body.message||"El PDF se creó, pero no pudo registrarse en Solicitud de equipo.");}
      }
      await onSaved(targets[0].number);
    }catch(reason){setError(`${saved?`${saved} de ${targets.length} solicitudes guardadas. Puedes reintentar sin duplicarlas. `:""}${reason instanceof Error?reason.message:"No fue posible crear el comprobante PDF."}`);}finally{submitting.current=false;setBusy(false);}
  };
  useEffect(()=>{if(!autoSubmitVoice||!voiceReady||autoSubmittedVoice.current||busy||loadingInventory||loadingAgencies)return;autoSubmittedVoice.current=true;void submit()},[autoSubmitVoice,voiceReady,busy,loadingInventory,loadingAgencies,agencyId,lines]);
  return <div className="warehouse-request-backdrop" role="presentation"><section className="warehouse-request-composer" role="dialog" aria-modal="true" aria-labelledby="warehouse-request-title"><header><div><small>SOLICITUD A ALMACÉN · PDF · TRAZABILIDAD</small><h2 id="warehouse-request-title">{weekendLoan?'Préstamo de equipos para fin de semana':'Solicitud de equipos y materiales'}</h2><p>{number} · Catálogo oficial completo, incluso artículos sin existencia.</p></div><button type="button" disabled={busy} onClick={onClose} aria-label="Cerrar"><X/></button></header>
    {weekendLoan&&<fieldset className="admin-form-grid"><legend>Préstamo de Avería Técnica · Vittini</legend><label>Inicio (viernes o sábado)<input type="date" value={loanFrom} onChange={e=>setLoanFrom(e.target.value)}/></label><label>Fin (domingo)<input type="date" min={loanFrom||undefined} value={loanTo} onChange={e=>setLoanTo(e.target.value)}/></label><label>Devolución de sobrantes<input type="date" min={loanTo||undefined} value={loanReturn} onChange={e=>setLoanReturn(e.target.value)}/></label><p>Solicita los equipos para prepararlos antes del cierre de Almacén. Al terminar registra el uso por agencia y entrega los sobrantes.</p></fieldset>}
    {error&&<p className="warehouse-request-error" role="alert">{error}</p>}
    {!internal&&!groupDestination&&agencyError&&<p className="warehouse-request-error" role="alert">{agencyError}</p>}
    {!internal&&!groupDestination&&loadingAgencies&&<p role="status">Cargando agencias…</p>}
    {groupDestination&&groupError&&<p className="warehouse-request-error" role="alert">{groupError} <button type="button" disabled={loadingGroups} onClick={()=>setGroupReload(value=>value+1)}>Reintentar grupos</button></p>}
    {linkedTicket&&<div className="warehouse-ticket-link"><FileText/><span><strong>Vinculada al ticket #{linkedTicket.ticketNumber}</strong><small>{linkedTicket.codigo} · {linkedTicket.terminal} · {linkedTicket.subject}</small><em>El serial entregado y la evidencia final de instalación quedarán unidos a este formulario.</em></span></div>}
    <div className="warehouse-request-route">
      <article><span>Departamento solicitante</span><strong>{departmentNames[department]||department}</strong></article>
      {isSupervisor&&linkedTicket&&<article><span>Destino de la solicitud</span><strong>Para una agencia asignada</strong></article>}
      {!!destinationOptions.length&&<label>Destino de la solicitud<select disabled={busy} aria-label="Destino de la solicitud" value={destination} onChange={event=>{setDestination(event.target.value as WarehouseDestination);setAgencyGroup('');setAgencyId('');setLines(current=>current.map(line=>({...line,agencyId:'',agencyQuery:''})));setAgencyFailures({});}}>{destinationOptions.map(option=><option key={option.value} value={option.value}>{option.label}</option>)}</select></label>}
      {internal?<article><span>Destino · uso interno</span><strong>Departamento de {departmentNames[department]||department}</strong><small>No requiere agencia ni ticket.</small></article>:<>
        {groupDestination&&<SearchSelect label="Grupo de agencias" value={agencyGroup} options={agencyGroups.map(group=>({value:group,label:group}))} onChange={setAgencyGroup} loading={loadingGroups} disabled={busy} required placeholder="Buscar y seleccionar grupo"/>}
        {!groupDestination&&!multipleAgencies&&!linkedTicket&&<SearchSelect label="Agencia de la solicitud" value={agencyId} options={agencyOptions} onChange={id=>{setAgencyId(id);setAgencyFailures({})}} loading={loadingAgencies} disabled={busy} required placeholder="Nombre, código, terminal o grupo"/>}
        {linkedTicket&&<article><span>Agencia del ticket</span><strong>{linkedTicket.codigo} · {linkedTicket.terminal}</strong></article>}
        {multipleAgencies&&<article><span>Varias agencias</span><strong>Agencia por artículo</strong><small>Una solicitud y un PDF por cada agencia.</small></article>}
      </>}
      <label>Prioridad<select disabled={busy} value={priority} onChange={event=>setPriority(event.target.value)}><option value="LOW">Baja</option><option value="MEDIUM">Media</option><option value="HIGH">Alta</option><option value="CRITICAL">Crítica</option></select></label>
    </div>
    {!internal&&!linkedTicket&&<section className="warehouse-multi-agencies"><div><strong>{groupDestination?'Solicitud para el grupo':multipleAgencies?'Artículos para varias agencias':'Una agencia para todos los artículos'}</strong><small>{groupDestination?'Selecciona el grupo y los artículos que necesitas. No es necesario asignar agencias.':multipleAgencies?'Selecciona la agencia en cada artículo. Los artículos de la misma agencia se agrupan en una solicitud.':'Selecciona la agencia arriba una sola vez. Todos los artículos se enviarán para esa agencia.'}</small></div></section>}
    {!internal&&!groupDestination&&!linkedTicket&&failureCatalogError&&<p className="warehouse-request-error" role="alert">{failureCatalogError}</p>}
    <section className="warehouse-request-lines">
      <header><div><Boxes/><span><strong>Artículos solicitados</strong><small>Selecciona del catálogo o registra una necesidad que todavía no exista en inventario.</small></span></div><button type="button" disabled={busy||lines.length>=30} onClick={()=>setLines(current=>[...current,newLine()])}><Plus/>Agregar artículo</button></header>
      {loadingInventory&&<p>Cargando catálogo oficial…</p>}
      {!loadingInventory&&!officialCatalog.length&&<p>El catálogo no está disponible. Puedes generar el comprobante escribiendo manualmente el artículo solicitado.</p>}
      {lines.map((line,index)=>{
        const selected=officialCatalog.find(item=>item.productId===line.productId);
        const targetAgencyId=multipleAgencies?line.agencyId:agencyId;
        const firstAgencyRow=!!targetAgencyId&&(multipleAgencies?lines.findIndex(item=>item.agencyId===targetAgencyId)===index:index===0);
        const failure=agencyFailures[targetAgencyId];
        const failureCategories=failureOptions.find(item=>item.code===failure?.department)?.categories.filter(item=>item.isActive)||[];
        return <article key={line.id}>
          <div className="warehouse-request-line-heading"><b>{index+1}</b><strong>Artículo {index+1}</strong><button type="button" className="remove" disabled={busy||lines.length===1} onClick={()=>setLines(current=>current.filter(item=>item.id!==line.id))} aria-label={`Eliminar artículo ${index+1}`}><Trash2/></button></div>
          {multipleAgencies&&<SearchSelect label={`Agencia para artículo ${index+1}`} value={line.agencyId} options={agencyOptions} onChange={id=>selectLineAgency(line.id,id)} loading={loadingAgencies} disabled={busy} required placeholder="Nombre, código, terminal o grupo"/>}
          <div className="warehouse-line-product"><div className="warehouse-product-field">
          {line.manual?<label>Artículo sin existencia<input required disabled={busy} minLength={2} maxLength={100} value={line.name} onChange={event=>setLine(line.id,{name:event.target.value})} placeholder="Nombre del equipo o material"/><button type="button" disabled={busy} onClick={()=>setLine(line.id,{manual:false,name:"",productId:""})}>Volver al catálogo</button></label>:<><SearchSelect label={`Artículo ${index+1} del catálogo`} value={line.productId} options={officialCatalog.map(item=>({value:item.productId,label:item.name,description:`${item.productId} · ${item.group} · ${item.available} disponible(s)`}))} onChange={id=>{const product=officialCatalog.find(item=>item.productId===id);setLine(line.id,{productId:id,name:product?.name||"",quantity:1});}} disabled={busy} loading={loadingInventory} required placeholder="Buscar artículo por nombre, código o grupo"/><div className="warehouse-product-meta"><small>{selected?`${selected.available} disponible(s) · puedes solicitar aunque no haya existencia`:'Selecciona un resultado del catálogo'}</small><button type="button" disabled={busy} onClick={()=>setLine(line.id,{manual:true,productId:"",name:""})}>Solicitar artículo sin existencia</button></div></>}
          </div>
          <label>Cantidad<input disabled={busy} required type="number" min="1" max="10000" value={line.quantity} onChange={event=>setLine(line.id,{quantity:Number(event.target.value)})}/><small>Cantidad solicitada</small></label><label className="line-note">Observación<input disabled={busy} value={line.note} onChange={event=>setLine(line.id,{note:event.target.value})} placeholder="Detalle opcional"/></label></div>
          {firstAgencyRow&&!internal&&!groupDestination&&!linkedTicket&&<div className="warehouse-line-failure"><label className="warehouse-ticket-toggle"><input type="checkbox" checked={!!failure} disabled={busy} onChange={event=>{if(event.target.checked)setAgencyFailures(current=>({...current,[targetAgencyId]:{department:'',category:''}}));else setAgencyFailures(current=>{const next={...current};delete next[targetAgencyId];return next})}}/>Reemplazo por falla</label><small>Actívalo si estos artículos reemplazan un equipo averiado. Se creará un ticket interno para esta agencia.</small>{failure&&<div className="warehouse-failure-fields"><SearchSelect label={`Departamento de la falla · artículo ${index+1}`} value={failure.department} options={failureOptions.map(item=>({value:item.code,label:item.name}))} onChange={code=>setAgencyFailures(current=>({...current,[targetAgencyId]:{department:code,category:''}}))} disabled={busy} required placeholder="Tecnología o Servicios Generales"/><SearchSelect label={`Falla · artículo ${index+1}`} value={failure.category} options={failureCategories.map(item=>({value:item.code,label:item.name}))} onChange={category=>setAgencyFailure(targetAgencyId,category)} disabled={busy||!failure.department} required placeholder="Buscar y seleccionar falla"/></div>}</div>}
          {multipleAgencies&&!!line.agencyId&&!firstAgencyRow&&<p className="warehouse-line-ticket-note">Esta agencia comparte la configuración de falla de su primer artículo.</p>}

        </article>;
      })}
    </section>
    <label className="warehouse-request-notes">Motivo general y observaciones<textarea disabled={busy} maxLength={2000} value={notes} onChange={event=>setNotes(event.target.value)} placeholder={internal?'Explica qué necesita tu departamento.':groupDestination?'Explica qué necesita el grupo.':'Explica qué necesita la agencia y cualquier instrucción para Almacén.'}/></label>
    <div className="warehouse-request-flow"><PackagePlus/><span><strong>Solicitud de equipos y materiales</strong><small>Almacén entregará lo disponible y conservará pendiente lo que deba encargarse.</small></span></div>
    <footer><button type="button" disabled={busy} onClick={onClose}>Cancelar</button><span><FileText/>Se generará el PDF aunque el artículo no tenga existencia</span><button type="button" className="primary" disabled={busy||loadingInventory} onClick={()=>void submit()}><Send/>{busy?"Generando y enviando…":"Enviar solicitud"}</button></footer>
  </section></div>;
}
