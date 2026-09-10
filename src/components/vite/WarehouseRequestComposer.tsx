import {useMemo,useState} from "react";
import {Boxes,FileText,PackagePlus,Plus,RotateCcw,Send,Trash2,X} from "lucide-react";
import type {PortalSession} from "@/lib/session";
import "./warehouseRequestComposer.css";

type Agency={id:string;codigo:string;terminal:string;grupo:string};
type Recipient={id:string;name:string;department:string;isTechnician:boolean;isAgencySupervisor?:boolean;role?:string|null};
type LineKind="EQUIPMENT"|"COMPONENT"|"RETURN";
type RequestLine={id:string;kind:LineKind;name:string;quantity:number;serial:string;note:string};

const departmentNames:Record<string,string>={TECHNOLOGY:"Tecnología",GENERAL_SERVICES:"Servicios Generales"};
const kindNames:Record<LineKind,string>={EQUIPMENT:"Equipo",COMPONENT:"Componente",RETURN:"Devolución a Almacén"};
const newLine=(kind:LineKind="EQUIPMENT"):RequestLine=>({id:crypto.randomUUID(),kind,name:"",quantity:1,serial:"",note:""});

function requestNumber(){
  const now=new Date();
  const stamp=[now.getFullYear(),String(now.getMonth()+1).padStart(2,"0"),String(now.getDate()).padStart(2,"0"),String(now.getHours()).padStart(2,"0"),String(now.getMinutes()).padStart(2,"0"),String(now.getSeconds()).padStart(2,"0")].join("");
  return `SOL-ALM-${stamp}-${crypto.randomUUID().slice(0,4).toUpperCase()}`;
}

async function createRequestPdf(input:{number:string;department:string;agency:Agency;installer:string;priority:string;notes:string;lines:RequestLine[];createdBy:string}){
  const {jsPDF}=await import("jspdf");
  const pdf=new jsPDF({unit:"mm",format:"a4",compress:true});
  const navy:[number,number,number]=[9,31,53],cyan:[number,number,number]=[56,189,225],ink:[number,number,number]=[26,47,64],muted:[number,number,number]=[92,111,126];
  const pageHeader=()=>{pdf.setFillColor(...navy);pdf.rect(0,0,210,34,"F");pdf.setTextColor(...cyan);pdf.setFont("helvetica","bold");pdf.setFontSize(7);pdf.text("REAL · GRUPO TEJEDA · OFICINA VIRTUAL",14,10);pdf.setTextColor(255,255,255);pdf.setFontSize(16);pdf.text("SOLICITUD A ALMACÉN",14,21);pdf.setFontSize(8);pdf.text(input.number,14,28);};
  pageHeader();
  pdf.setTextColor(...ink);pdf.setFontSize(8);pdf.setFont("helvetica","bold");
  pdf.text("DEPARTAMENTO SOLICITANTE",14,44);pdf.text("AGENCIA QUE REQUIERE",75,44);pdf.text("PRIORIDAD",166,44);
  pdf.setFont("helvetica","normal");pdf.text(departmentNames[input.department]||input.department,14,50);pdf.text(`${input.agency.codigo} · ${input.agency.terminal}`,75,50);pdf.text(input.priority,166,50);
  pdf.setFont("helvetica","bold");pdf.text("GRUPO",14,60);pdf.text("TÉCNICO INSTALADOR",75,60);pdf.text("SOLICITADO POR",145,60);
  pdf.setFont("helvetica","normal");pdf.text(input.agency.grupo,14,66);pdf.text(input.installer,75,66);pdf.text(input.createdBy,145,66);
  pdf.setDrawColor(190,207,218);pdf.line(14,72,196,72);
  let y=81;
  const tableHeader=()=>{pdf.setFillColor(...navy);pdf.rect(14,y-6,182,8,"F");pdf.setTextColor(255,255,255);pdf.setFont("helvetica","bold");pdf.setFontSize(7);pdf.text("TIPO",17,y-1);pdf.text("EQUIPO / COMPONENTE / DEVOLUCIÓN",48,y-1);pdf.text("CANT.",139,y-1);pdf.text("SERIAL / DETALLE",157,y-1);y+=7;};
  tableHeader();
  for(const line of input.lines){
    const description=pdf.splitTextToSize(`${line.name}${line.note?` · ${line.note}`:""}`,85) as string[];
    const serial=pdf.splitTextToSize(line.serial||"Pendiente / no aplica",38) as string[];
    const height=Math.max(12,Math.max(description.length,serial.length)*4+5);
    if(y+height>270){pdf.addPage();pageHeader();y=48;tableHeader();}
    pdf.setTextColor(...ink);pdf.setFont("helvetica","normal");pdf.setFontSize(7.5);pdf.text(kindNames[line.kind],17,y+4);pdf.text(description,48,y+4);pdf.text(String(line.quantity),144,y+4,{align:"center"});pdf.text(serial,157,y+4);pdf.setDrawColor(215,225,233);pdf.line(14,y+height,196,y+height);y+=height;
  }
  if(y>235){pdf.addPage();pageHeader();y=48;}
  pdf.setFont("helvetica","bold");pdf.setTextColor(...muted);pdf.text("MOTIVO Y OBSERVACIONES",14,y+8);pdf.setFont("helvetica","normal");pdf.setTextColor(...ink);pdf.text(pdf.splitTextToSize(input.notes||"Sin observaciones adicionales.",182),14,y+15);
  pdf.setFontSize(6);pdf.setTextColor(...muted);pdf.text("Almacén debe marcar el documento como recibido antes de autorizarlo o rechazarlo. La entrega física se documenta por separado.",14,288);
  for(let page=1;page<=pdf.getNumberOfPages();page++){pdf.setPage(page);pdf.setFontSize(6);pdf.setTextColor(...muted);pdf.text(`${page} / ${pdf.getNumberOfPages()}`,196,294,{align:"right"});}
  return pdf.output("arraybuffer");
}

export default function WarehouseRequestComposer({session,department,agencies,recipients,equipmentOptions,componentOptions,onClose,onSaved}:{session:PortalSession;department:string;agencies:Agency[];recipients:Recipient[];equipmentOptions:string[];componentOptions:string[];onClose:()=>void;onSaved:(number:string)=>void}){
  const[number]=useState(requestNumber),[agencyId,setAgencyId]=useState(""),[installerId,setInstallerId]=useState(""),[priority,setPriority]=useState("MEDIUM"),[notes,setNotes]=useState(""),[lines,setLines]=useState<RequestLine[]>([newLine()]),[busy,setBusy]=useState(false),[error,setError]=useState("");
  const agency=agencies.find(item=>item.id===agencyId),installer=recipients.find(item=>item.id===installerId);
  const eligibleRecipients=useMemo(()=>recipients.filter(item=>(item.isTechnician&&item.department===department)||item.isAgencySupervisor||item.role==="GroupAdministrator"),[recipients,department]);
  const setLine=(id:string,patch:Partial<RequestLine>)=>setLines(current=>current.map(line=>line.id===id?{...line,...patch}:line));
  const submit=async()=>{
    setError("");
    if(!agency){setError("Selecciona la agencia que requiere los equipos.");return;}
    if(!installer){setError("Selecciona el técnico que instalará o recibirá los equipos.");return;}
    if(!lines.length||lines.some(line=>!line.name.trim()||line.quantity<1||(line.kind==="RETURN"&&!line.serial.trim()))){setError("Completa todos los renglones. Las devoluciones requieren el serial del equipo devuelto.");return;}
    setBusy(true);
    try{
      const bytes=await createRequestPdf({number,department,agency,installer:installer.name,priority,notes,lines,createdBy:session.displayName});
      const form=new FormData();
      form.append("file",new Blob([bytes],{type:"application/pdf"}),`${number}.pdf`);
      form.append("area","communications");form.append("channel","requirements");form.append("title",`Solicitud a Almacén ${number}`);
      form.append("description",`${lines.length} renglón(es) · Agencia ${agency.codigo} · Técnico ${installer.name} · ${lines.some(line=>line.kind==="RETURN")?"Incluye devolución":"Sin devolución"}`);
      form.append("recipientDepartment","Almacén");form.append("sourceDepartment",departmentNames[department]||department);
      const response=await fetch("/api/institutional-documents",{method:"POST",credentials:"same-origin",body:form});
      if(!response.ok){const body=await response.json().catch(()=>({}));throw new Error(body.message||body.error||"No fue posible enviar la solicitud a Almacén.");}
      onSaved(number);
    }catch(reason){setError(reason instanceof Error?reason.message:"No fue posible crear el comprobante PDF.");}finally{setBusy(false);}
  };
  return <div className="warehouse-request-backdrop" role="presentation"><section className="warehouse-request-composer" role="dialog" aria-modal="true" aria-labelledby="warehouse-request-title"><header><div><small>SOLICITUD FORMAL · PDF · TRAZABILIDAD</small><h2 id="warehouse-request-title">Solicitar equipos a Almacén</h2><p>{number} · El departamento solicitante se toma automáticamente de tu sesión.</p></div><button type="button" disabled={busy} onClick={onClose} aria-label="Cerrar"><X/></button></header>
    {error&&<p className="warehouse-request-error" role="alert">{error}</p>}
    <div className="warehouse-request-route"><article><span>Departamento solicitante</span><strong>{departmentNames[department]||department}</strong></article><label>Agencia que requiere la solicitud<select autoFocus required value={agencyId} onChange={event=>setAgencyId(event.target.value)}><option value="">Seleccionar agencia</option>{agencies.map(item=><option key={item.id} value={item.id}>{item.codigo} · {item.terminal} · {item.grupo}</option>)}</select></label><label>Técnico que instalará o recibirá<select required value={installerId} onChange={event=>setInstallerId(event.target.value)}><option value="">Seleccionar técnico</option>{eligibleRecipients.map(item=><option key={item.id} value={item.id}>{item.name}{item.isAgencySupervisor?" · Supervisor":""}</option>)}</select></label><label>Prioridad<select value={priority} onChange={event=>setPriority(event.target.value)}><option value="LOW">Baja</option><option value="MEDIUM">Media</option><option value="HIGH">Alta</option><option value="CRITICAL">Crítica</option></select></label></div>
    <section className="warehouse-request-lines"><header><div><Boxes/><span><strong>Equipos, componentes y devoluciones</strong><small>Agrega todos los renglones necesarios dentro del mismo comprobante.</small></span></div><button type="button" onClick={()=>setLines(current=>[...current,newLine()])}><Plus/>Agregar renglón</button></header>{lines.map((line,index)=><article key={line.id}><b>{index+1}</b><label>Tipo<select value={line.kind} onChange={event=>setLine(line.id,{kind:event.target.value as LineKind,serial:""})}><option value="EQUIPMENT">Equipo</option><option value="COMPONENT">Componente</option><option value="RETURN">Devolución a Almacén</option></select></label><label>Equipo o componente<input required list={line.kind==="COMPONENT"?"warehouse-component-options":"warehouse-equipment-options"} value={line.name} onChange={event=>setLine(line.id,{name:event.target.value})} placeholder={line.kind==="RETURN"?"Equipo nuevo que será devuelto":line.kind==="COMPONENT"?"Componente requerido":"Equipo requerido"}/></label><label>Cantidad<input required type="number" min="1" max="1000" value={line.quantity} onChange={event=>setLine(line.id,{quantity:Number(event.target.value)})}/></label><label>{line.kind==="RETURN"?"Serial devuelto (obligatorio)":"Referencia o detalle"}<input required={line.kind==="RETURN"} value={line.serial} onChange={event=>setLine(line.id,{serial:event.target.value})} placeholder={line.kind==="RETURN"?"Serial del equipo a devolver":"Opcional"}/></label><label className="line-note">Observación<input value={line.note} onChange={event=>setLine(line.id,{note:event.target.value})} placeholder={line.kind==="RETURN"?"Ej. Solo se utilizó el cable":"Detalle opcional"}/></label><button type="button" className="remove" disabled={lines.length===1} onClick={()=>setLines(current=>current.filter(item=>item.id!==line.id))} aria-label={`Eliminar renglón ${index+1}`}><Trash2/></button></article>)}<datalist id="warehouse-equipment-options">{equipmentOptions.map(value=><option key={value} value={value}/>)}</datalist><datalist id="warehouse-component-options">{componentOptions.map(value=><option key={value} value={value}/>)}</datalist></section>
    <label className="warehouse-request-notes">Motivo general y observaciones<textarea maxLength={2000} value={notes} onChange={event=>setNotes(event.target.value)} placeholder="Explica qué necesita la agencia y cualquier instrucción para Almacén."/></label>
    <div className="warehouse-request-flow"><PackagePlus/><span><strong>Flujo del comprobante</strong><small>Almacén lo verá primero en Pendientes. Después de marcarlo Recibido podrá autorizarlo o rechazarlo.</small></span><RotateCcw/><span><strong>Devoluciones incluidas</strong><small>Puedes devolver el equipo nuevo completo cuando solo fue necesario utilizar un cable o componente.</small></span></div>
    <footer><button type="button" disabled={busy} onClick={onClose}>Cancelar</button><span><FileText/>Se generará un PDF con todos los renglones</span><button type="button" className="primary" disabled={busy} onClick={()=>void submit()}><Send/>{busy?"Generando y enviando…":"Enviar a Requerimientos"}</button></footer>
  </section></div>;
}
