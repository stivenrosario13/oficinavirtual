import {isGasolineRecord} from '@/lib/administrative-fuel';
import { apiFetch as fetch } from "@/lib/api-fetch";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { ArrowLeft, CheckCircle2, Download, History, Maximize2, MessageCircleWarning, PackageCheck, PenLine, Printer, Send, XCircle, ZoomIn, ZoomOut } from "lucide-react";
import type { DocumentDetail } from "./DocumentSystemModule";
import InstitutionalPdfCanvas from "./InstitutionalPdfCanvas";
import InstitutionalSignature from "./InstitutionalSignature";
import { downloadDocument, realLogoData } from "./institutionalDocumentAssets";
import { buildReceiptCopy } from "./institutionalReceiptPdf";
import { buildAdministrativeFormPdf } from './administrativeFormPdf';
import { prepareAdministrativeForm, type Row } from '@/lib/administrative-form-data';
import WarehouseRequestEditor from './WarehouseRequestEditor';
import WeekendLoanDetail from './WeekendLoanDetail';
import IncidentTicketDecisions from './IncidentTicketDecisions';
import WarehouseAllocationForm from "./WarehouseAllocationForm";
import { submitDocumentAction } from "@/lib/document-action";
import './institutionalDocuments.css';
import './institutionalBrandOverrides.css';
import './institutionalDocumentViewer.css';

export default function InstitutionalDocumentDetail({ item, onBack, onChanged, onOpenInventory: _onOpenInventory, warehouseAccess = false, warehouseAuthorizeAccess = false, initialWarehouseEdit = false, incidentOperatorAccess = false }: {
  item: DocumentDetail;
  onBack: () => void;
  onChanged: (updated?: DocumentDetail) => Promise<void>;
  onOpenInventory?: (department: string) => void;
  warehouseAccess?: boolean;
  warehouseAuthorizeAccess?: boolean;
  initialWarehouseEdit?: boolean;
  incidentOperatorAccess?: boolean;
}) {
  const [editingWarehouse,setEditingWarehouse]=useState(initialWarehouseEdit&&!!item.canEditWarehouseRequest);
  const [original, setOriginal] = useState<Uint8Array | null>(null);
  const [data, setData] = useState<Uint8Array | null>(null);
  const [imageUrl, setImageUrl] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [signing, setSigning] = useState<false | "receive" | "update">(false);
  const [note, setNote] = useState("");
  const [decision, setDecision] = useState("");
  const [zoom, setZoom] = useState(100);
  const workspace=useRef<HTMLElement>(null);
  const savedViewerPosition=useRef<{top:number;container:HTMLElement|null}|null>(null);
  useLayoutEffect(()=>{
    const saved=savedViewerPosition.current,element=workspace.current;
    if(!saved||!element)return;
    savedViewerPosition.current=null;
    const delta=element.getBoundingClientRect().top-saved.top;
    if(saved.container)saved.container.scrollTop+=delta;
    else window.scrollBy({top:delta,behavior:"instant"});
  },[item]);

  const isPdf = item.contentType === "application/pdf";
  const receiptPoint = { x: 73.1, y: 70.9 };
  const registrationOnly=isGasolineRecord(item);
  const canOperateIncident=item.administrativeRequest?.formType==='INCIDENT_REPORT'&&(!!item.canDecideIncidentTickets||incidentOperatorAccess);
  const effectiveRecipient = !registrationOnly && (item.isRecipient || canOperateIncident || (warehouseAccess && item.recipientDepartment === "Almacén"));
  const warehouseRequest=!!item.warehouseRequest&&item.recipientDepartment==="Almacén";
  const canCancel=warehouseRequest&&(item.isOwner||warehouseAccess||item.isRecipient)&&!item.warehouseDelivery&&["pending","received","correction_requested","approved"].includes(item.status);
  const canReceive = effectiveRecipient && !item.receivedUtc && ["pending", "correction_requested", "approved"].includes(item.status);
  const canDecide = effectiveRecipient && item.administrativeRequest?.formType!=='INCIDENT_REPORT' && !(item.administrativeRequest?.formType==='GAS_LOG'&&item.isOwner) && (!warehouseRequest||warehouseAuthorizeAccess) && (item.channel === "requirements"
    ? item.status === "received"
    : ["pending", "received", "correction_requested"].includes(item.status));
  const canAuthorizeDispatch=item.canAuthorizeWarehouseRequest ?? (warehouseAccess&&warehouseAuthorizeAccess&&effectiveRecipient&&item.channel==="requirements"&&item.recipientDepartment==="Almacén"&&(item.status==="received"||(item.status==="correction_requested"&&!!item.receivedUtc))&&!item.warehouseDelivery);
  const canPrepareDispatch=warehouseAccess&&effectiveRecipient&&item.status==="approved"&&item.warehouseRequest?.lines.some(line=>line.kind!=="RETURN")&&!item.warehouseAssigned?.length&&!item.warehouseDelivery;
  useEffect(() => {
    let cancelled = false;
    let url = "";
    setError("");
    fetch(item.fileUrl, { credentials: "same-origin", cache: "no-store" })
      .then(async response => {
        if (!response.ok) throw new Error("No se pudo cargar el documento.");
        const bytes = new Uint8Array(await response.arrayBuffer());
        if (cancelled) return;
        setOriginal(bytes);
        if(item.contentType==="application/pdf")setData(current=>current??bytes);
        if (item.contentType.startsWith("image/")) {
          url = URL.createObjectURL(new Blob([bytes], { type: item.contentType }));
          setImageUrl(url);
        }
      })
      .catch(reason => { if (!cancelled) setError(reason.message); });
    return () => { cancelled = true; if (url) URL.revokeObjectURL(url); };
  }, [item.id, item.fileUrl, item.contentType,item.warehouseRevision,item.administrativeRevision]);

  useEffect(() => {
    let cancelled = false;
    if (!original || !isPdf) return;
    // Keep the rendered document in place while regenerating its updated seal.
    (async () => {
      const logo=await realLogoData();
      const incidentData=item.administrativeRequest?.formType==='INCIDENT_REPORT'?item.administrativeRequest.formData:undefined;
      const incidentRows=Array.isArray(incidentData?.rows)?incidentData.rows.filter(row=>row&&typeof row==='object'):[];
      const hasTicketDecision=incidentRows.some(row=>{
        const status=(row as Record<string,unknown>).decisionStatus;
        return status==='APPROVED'||status==='REJECTED';
      });
      let source=original;
      if(incidentData&&hasTicketDecision){
        const values=Object.fromEntries(Object.entries(incidentData).filter(([,value])=>typeof value==='string'||typeof value==='number').map(([key,value])=>[key,String(value)])) as Row;
        const rows=incidentRows.map(row=>Object.fromEntries(Object.entries(row as Record<string,unknown>).map(([key,value])=>[key,value==null?'':String(value)]))) as Row[];
        const prepared=prepareAdministrativeForm('incident_report',values,rows,[],item.department,{displayName:item.ownerDisplayName});
        source=new Uint8Array(await buildAdministrativeFormPdf({detailsOnlyAfterFirstPage:true,hideCustodySpaces:true,number:item.administrativeRequest!.number,title:prepared.title,subtitle:prepared.subtitle,department:item.department,createdBy:item.ownerDisplayName,createdByLogin:item.ownerLogin||item.ownerUserName,responsible:values.responsible||'YESICA MERCEDES',target:prepared.target,fields:prepared.fields,tables:prepared.tables,bodySections:prepared.bodySections,notes:prepared.reason,logoData:logo,origin:window.location.origin,createdAt:new Date(item.createdUtc)}));
      }
      const bytes = await buildReceiptCopy(source, item, logo, window.location.origin);
      if (!cancelled) setData(bytes);
    })().catch(reason => {
      if (!cancelled){setData(current=>current??original);setError(`No se pudo preparar la copia sellada: ${reason.message}. Se muestra el PDF original.`);}
    });
    return () => { cancelled = true; };
  }, [original, item, isPdf]);

  async function action(name: string, body: object = {}) {
    const element=workspace.current;
    if(element){let container=element.parentElement;while(container){const overflow=getComputedStyle(container).overflowY;if(["auto","scroll"].includes(overflow)&&container.scrollHeight>container.clientHeight)break;container=container.parentElement;}savedViewerPosition.current={top:element.getBoundingClientRect().top,container};}
    // Una firma dibujada antes de confirmar la recepción la recibe y sella en
    // un solo paso. La firma es opcional: "Recibir y sellar" funciona sola.
    if (item.channel === "requirements" && name === "sign" && !item.receivedUtc) {
      name = "receive";
      body = { ...body, sealX: receiptPoint.x, sealY: receiptPoint.y };
    }
    try {
      const updated=await submitDocumentAction<DocumentDetail>(item.id,name,body);
      await onChanged(updated);
    } catch(reason) {
      savedViewerPosition.current=null;
      throw reason;
    }
  }

  async function run(work: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await work();
      setDecision("");
      setNote("");
      setSigning(false);
    } catch (reason) {
      setError((reason as Error).message);
    } finally {
      setBusy(false);
    }
  }

  async function approvePrepared() {
    await action("approve",{note:note.trim()});
    window.dispatchEvent(new Event("support-live-refresh"));
  }

  function printPdf() {
    if (!data) return;
    const url = URL.createObjectURL(new Blob([data.slice().buffer], { type: "application/pdf" }));
    const popup = window.open(url, "_blank", "noopener,noreferrer");
    if (popup) popup.addEventListener("load", () => popup.print(), { once: true });
    window.setTimeout(() => URL.revokeObjectURL(url), 60000);
  }

  const finalSealLabel = item.status === "cancelled" ? "ANULADO" : item.warehouseDelivery || item.administrativeDelivery ? "ENTREGADO Y RECIBIDO" : item.status === "approved" && (item.warehouseAssigned?.length || item.administrativeRequest&&item.administrativeRequest.formType!=="INCIDENT_REPORT") ? "AUTORIZADO A RETIRAR" : item.status === "approved" ? "AUTORIZADO" : item.status === "rejected" ? "RECHAZADO" : "RECIBIDO";
  const changeZoom = (next: number) => setZoom(Math.max(60, Math.min(180, next)));

  return <div className={`native-doc-detail${item.channel==="requirements"&&item.recipientDepartment==="Almacén"?" warehouse-request-detail":""}`}>
    <div className="native-doc-detail-toolbar">
      <div className="document-heading-row"><button onClick={onBack}><ArrowLeft />Volver</button>
      <div className="document-heading-copy"><small>{item.department}</small><h2>{item.title}</h2><p>De {item.ownerDisplayName} · {item.recipientDepartment || "PDF archivado en Mis archivos"}</p></div></div>
      <div className="document-action-bar" role="group" aria-label="Acciones del formulario">
      {isPdf ? <button disabled={!data || busy} onClick={() => data && downloadDocument(data, item.originalFileName)}><Download />Descargar formulario</button> : <a href={item.fileUrl}><Download />Original</a>}
      {isPdf && <button disabled={!data || busy} onClick={printPdf}><Printer />Imprimir PDF</button>}
      {isPdf && !registrationOnly && item.receivedUtc && <button disabled={!data || busy} onClick={() => data && downloadDocument(data, `${item.status}-${item.originalFileName}`)}><Download />PDF {finalSealLabel.toLocaleLowerCase("es-DO")}</button>}
      {canReceive && <>
        <button className="primary" disabled={busy} onClick={() => void run(() => action("receive", { sealX: receiptPoint.x, sealY: receiptPoint.y }))}><CheckCircle2 />Recibir y sellar</button>
        <button disabled={busy} onClick={() => setSigning("receive")}><PenLine />Recibir con firma</button>
      </>}
      {item.canEditWarehouseRequest&&<button disabled={busy} onClick={()=>setEditingWarehouse(true)}><PenLine/>Editar formulario</button>}{canCancel&&<button disabled={busy} className="warehouse-reject" onClick={()=>{setNote("");setDecision("cancel")}}><XCircle/>Anular solicitud</button>}
      {!registrationOnly && item.canMove && item.status!=="cancelled" && <button disabled={busy} onClick={() => setSigning("update")}><PenLine />{item.receiptSignature ? "Actualizar firma recibida" : "Agregar firma opcional"}</button>}
      {canDecide && <>
        <button disabled={busy} onClick={() => setDecision("correction")}><MessageCircleWarning />Solicitar corrección</button>
        <button disabled={busy} className="warehouse-reject" onClick={() => setDecision("reject")}><XCircle />Rechazar</button>
        {item.channel === "requirements" && item.recipientDepartment !== "Almacén" && <button disabled={busy} className="primary warehouse-authorize" onClick={() => setDecision("approve")}><CheckCircle2 />Autorizar requerimiento</button>}
      </>}
      {canAuthorizeDispatch&&<button disabled={busy} className="primary warehouse-authorize" onClick={()=>setDecision("warehouse-approve")}><CheckCircle2/>Autorizar y colocar sello</button>}
      {canPrepareDispatch&&<button disabled={busy} className="primary" onClick={()=>setDecision("prepare")}><CheckCircle2/>Asignar equipo y serial para despachar</button>}
      {item.isOwner && ["rejected", "correction_requested"].includes(item.status) && <button disabled={busy} onClick={() => void run(() => action("resend"))}><Send />Reenviar para revisión</button>}
      </div>
    </div>
    {editingWarehouse&&<WarehouseRequestEditor item={item} onCancel={()=>setEditingWarehouse(false)} onSaved={async updated=>{await onChanged(updated);setEditingWarehouse(false)}}/>}
    {item.warehouseRequest?.weekendLoan&&<WeekendLoanDetail item={item} warehouse={warehouseAccess} onChanged={onChanged}/>} 
    {item.administrativeRequest?.formType==='INCIDENT_REPORT'&&<IncidentTicketDecisions item={item} operatorAccess={canOperateIncident} onChanged={onChanged}/>} 
    {error && <p className="native-doc-error" role="alert">{error}</p>}
    {item.status==="correction_requested"&&item.receivedUtc&&item.recipientDepartment==="Almacén"&&<section className="id-decision"><h3>Recibida · pendiente de autorizar</h3><p>Corrección solicitada: {item.correctionNote||"Revisa las indicaciones antes de autorizar."}</p><small>{item.decisionBy}</small></section>}
    {!!item.warehousePendingPurchaseLines?.length&&<section className="id-decision"><h3>Pendiente de compra</h3><p>{item.warehousePendingPurchaseLines.map(index=>item.warehouseRequest?.lines[index]).filter(Boolean).map(line=>`${line!.name} · ${line!.quantity} unidad(es)`).join(' / ')}</p><p>{item.warehousePurchaseNote}</p><small>No se han registrado existencias ni entregas. Al recibir la compra, registra el ingreso al inventario y completa la asignación.</small></section>}
    {item.status==="cancelled"&&<section className="id-decision"><h3>Solicitud anulada</h3><p>{item.correctionNote}</p><small>{item.decisionBy} · {item.decisionUtc&&new Date(item.decisionUtc).toLocaleString("es-DO")}</small></section>}
    {decision === "prepare" && item.warehouseRequest ? <WarehouseAllocationForm item={item} onCancel={()=>setDecision("")} onPrepared={async updated=>{await onChanged(updated);setDecision("")}}/> : decision && <section className="id-decision">
      <h3>{decision === "cancel" ? "Anular solicitud de Almacén" : decision === "warehouse-approve" ? "Autorizar solicitud y colocar sello" : decision === "approve" ? "Autorizar requerimiento" : decision === "reject" ? "Rechazar solicitud" : "Solicitar corrección"}</h3>
      <label>Motivo o indicaciones<textarea maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
      <p>{decision==="warehouse-approve"?"Al confirmar, la solicitud quedará autorizada. Después Almacén asignará el equipo y el serial antes de enviarla a Despachar.":"El remitente podrá ver el motivo en su bandeja. La autorización documental no modifica existencias."}</p>
      <button disabled={busy || note.trim().length < 5} onClick={() => void run(() => decision==="warehouse-approve"?approvePrepared():action(decision, { note: note.trim() }))}>Confirmar</button>
      <button disabled={busy} onClick={() => setDecision("")}>Cancelar</button>
    </section>}
    {item.warehouseRequest?.ticketId&&<section className="warehouse-destination-history"><header><History/><div><small>TRAZABILIDAD HASTA EL DESTINO FINAL</small><h3>Ticket #{item.warehouseRequest.ticketNumber} vinculado</h3><p>{item.warehouseRequest.agencyCode} · {item.warehouseRequest.agencyName}</p></div></header><div className="warehouse-destination-steps"><article className="complete"><span>1</span><strong>Solicitud creada</strong><small>{item.warehouseRequest.number}</small></article><article className={item.warehouseAssigned?.length?"complete":""}><span>2</span><strong>Equipo y serial asignados</strong><small>{item.warehouseAssigned?.length?item.warehouseAssigned.map(row=>`${row.name} · ${row.serial}`).join(" / "):"Pendiente de Almacén"}</small></article><article className={item.warehouseDelivery?"complete":""}><span>3</span><strong>Equipo entregado</strong><small>{item.warehouseDelivery?`${item.warehouseDelivery.receivedBy} · ${new Date(item.warehouseDelivery.at).toLocaleString("es-DO")}`:"Pendiente de retiro"}</small></article><article className={item.warehouseInstallation?"complete":""}><span>4</span><strong>Instalado y comprobado</strong><small>{item.warehouseInstallation?`${item.warehouseInstallation.resolvedBy} · ${new Date(item.warehouseInstallation.resolvedAt).toLocaleString("es-DO")}`:"El técnico debe resolver el ticket con evidencia"}</small></article></div>{item.warehouseInstallation&&<div className="warehouse-installation-result"><PackageCheck/><div><strong>Destino final confirmado: {item.warehouseInstallation.agencyCode} · {item.warehouseInstallation.agencyName}</strong><p>{item.warehouseInstallation.resolution}</p><small>Equipo instalado: {item.warehouseAssigned?.map(row=>`${row.name} — serial ${row.serial}`).join(" · ")||"Según despacho autorizado"}</small></div></div>}{!!item.warehouseInstallation?.evidence.length&&<div className="warehouse-installation-evidence"><h4>Evidencias vinculadas desde el ticket</h4><div>{item.warehouseInstallation.evidence.map(photo=><a key={photo.id} href={`/api/institutional-documents/${item.id}/warehouse-evidence/${photo.id}`} target="_blank" rel="noreferrer"><img src={`/api/institutional-documents/${item.id}/warehouse-evidence/${photo.id}`} alt={photo.fileName}/><span>{photo.fileName}</span></a>)}</div></div>}</section>}
<section className="warehouse-edit-history">{item.history?.filter(h=>h.previousWarehouseRequest).map((h,i)=><details key={i}><summary>Ajuste por {h.actor} · {new Date(h.at).toLocaleString("es-DO")}</summary><p>{h.note}</p><p>Artículos anteriores: {h.previousWarehouseRequest?.lines.map(l=>`${l.name} × ${l.quantity}`).join(" / ")}</p></details>)}</section><section ref={workspace} className="id-document-workspace" aria-label="Visor del documento institucional">
      <header className="id-document-viewer-header">
        <div>
          <small>DOCUMENTO INSTITUCIONAL</small>
          <strong>{!registrationOnly && item.receivedUtc ? `COPIA CON SELLO ${finalSealLabel}` : "VISTA PREVIA DEL FORMULARIO"}</strong>
          {!registrationOnly&&<span>El sello se actualiza dentro del PDF al recibir, autorizar o rechazar.</span>}
        </div>
        <div className="id-document-zoom" role="group" aria-label="Controles de zoom del documento">
          <button type="button" disabled={zoom <= 60} onClick={() => changeZoom(zoom - 10)} aria-label="Alejar documento"><ZoomOut /></button>
          <label><span>Zoom</span><input aria-label="Nivel de zoom" type="range" min="60" max="180" step="10" value={zoom} onChange={event => changeZoom(Number(event.target.value))} /><output>{zoom}%</output></label>
          <button type="button" disabled={zoom >= 180} onClick={() => changeZoom(zoom + 10)} aria-label="Acercar documento"><ZoomIn /></button>
          <button type="button" className="fit" disabled={zoom === 100} onClick={() => changeZoom(100)}><Maximize2 />Ajustar</button>
        </div>
      </header>
      <div className="native-doc-detail-body">
        <div>{isPdf
        ? <InstitutionalPdfCanvas data={data} zoom={zoom} showAdministrativeSeal={!registrationOnly&&!!item.receivedUtc&&!!item.administrativeRequest} />
        : imageUrl ? <img className="id-image-preview" src={imageUrl} alt={item.title} />
        : <p>Descarga el original para abrir este formato.</p>}
        </div>
      </div>
    </section>
    {signing && <InstitutionalSignature onClose={() => setSigning(false)} onUse={signature => signing === "receive"
      ? run(() => action("receive", { sealX: receiptPoint.x, sealY: receiptPoint.y, signatureData: signature }))
      : run(() => action("sign", { signatureData: signature }))} />}
  </div>;
}
