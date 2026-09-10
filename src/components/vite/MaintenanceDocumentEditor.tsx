import {useCallback,useEffect,useRef,useState} from "react";
import type {PointerEvent} from "react";
import {Download,Move,PenLine,Stamp,X,RefreshCw} from "lucide-react";
import {buildMaintenancePdf,formatDocumentTime} from "./maintenanceDocumentPdf";
import type {DocumentMarks,MaintenanceDocumentMovement,SignatureRole} from "./maintenanceDocumentPdf";
import {maintenanceApi,registerDocumentSignature} from "./maintenanceApi";
import "./maintenanceWorkflow.css";

export default function MaintenanceDocumentEditor({item,onClose}:{item:MaintenanceDocumentMovement;onClose:()=>void}){
  const [marks,setMarks]=useState<DocumentMarks|null>(null),[error,setError]=useState(""),[notice,setNotice]=useState(""),[busy,setBusy]=useState(false),[rendering,setRendering]=useState(true);
  const [mode,setMode]=useState<"RECEIVE"|"MOVE"|null>(null),[position,setPosition]=useState({x:109,y:191}),[signatureOpen,setSignatureOpen]=useState(false),[savedSignature,setSavedSignature]=useState<string|null>(null),[dirty,setDirty]=useState(false);
  const [signatureRole,setSignatureRole]=useState<SignatureRole>("DELIVERY");
  const [pageNumber,setPageNumber]=useState(1),[pageCount,setPageCount]=useState(1);
  const [zoom,setZoom]=useState(100);
  const canvas=useRef<HTMLCanvasElement>(null),sheet=useRef<HTMLDivElement>(null),signature=useRef<HTMLCanvasElement>(null),dialog=useRef<HTMLElement>(null),drawing=useRef(false),dragging=useRef(false),offset=useRef({x:0,y:0});
  const endpoint=`/api/maintenance/movements/${item.id}/document`;
  const reload=useCallback(async()=>{const data=await maintenanceApi<DocumentMarks>(endpoint);setMarks(data);setPosition({x:data.stampX,y:data.stampY});},[endpoint]);
  useEffect(()=>{let active=true;maintenanceApi<DocumentMarks>(endpoint).then(data=>{if(active){setMarks(data);setPosition({x:data.stampX,y:data.stampY});}}).catch(e=>{if(active){setError(e.message);setRendering(false);}});maintenanceApi<{signatureData:string|null}>("/api/account/saved-signature").then(data=>{if(active)setSavedSignature(data.signatureData);}).catch(()=>{});return()=>{active=false;};},[endpoint]);
  useEffect(()=>{const previous=document.activeElement as HTMLElement|null;const overflow=document.body.style.overflow;document.body.style.overflow="hidden";dialog.current?.focus();return()=>{document.body.style.overflow=overflow;previous?.focus();};},[]);
  useEffect(()=>{
    if(!marks)return;let cancelled=false;let cleanup:(()=>void)|undefined;
    setRendering(true);
    (async()=>{
      const pdf=await buildMaintenancePdf(item,marks);if(cancelled)return;setPageCount(pdf.getNumberOfPages());
      const modulePath="/vendor/pdfjs/pdf.mjs";const pdfjs=await import(/* @vite-ignore */ modulePath);pdfjs.GlobalWorkerOptions.workerSrc="/vendor/pdfjs/pdf.worker.mjs";pdfjs.setVerbosityLevel?.(pdfjs.VerbosityLevel?.ERRORS??0);
      const task=pdfjs.getDocument({data:new Uint8Array(pdf.output("arraybuffer")),isEvalSupported:false});cleanup=()=>{void task.destroy();};
      const document=await task.promise;if(cancelled){cleanup();return;}
      const page=await document.getPage(Math.min(pageNumber,pdf.getNumberOfPages())),viewport=page.getViewport({scale:1.4});
      if(!canvas.current||cancelled)return;const target=canvas.current;target.width=viewport.width;target.height=viewport.height;
      await page.render({canvas:target,canvasContext:target.getContext("2d"),viewport}).promise;if(!cancelled)setRendering(false);
    })().catch(e=>{if(!cancelled){setError(`No se pudo mostrar el PDF: ${e.message}`);setRendering(false);}});
    return()=>{cancelled=true;cleanup?.();};
  },[item,marks,pageNumber]);
  async function commit(action:"RECEIVE"|"MOVE"|"SIGN",signatureData?:string){
    await maintenanceApi(endpoint,"POST",{action,x:position.x,y:position.y,signatureData,signatureRole:action==="SIGN"?signatureRole:undefined});await reload();
  }
  async function savePosition(){setBusy(true);setError("");try{await commit(mode!);setMode(null);setNotice(mode==="RECEIVE"?(marks?.stampLabel==="ENTREGADO"?"Entrega confirmada con tu cuenta y la hora del servidor.":"Recepción confirmada con tu cuenta y la hora del servidor."):"Ubicación del sello guardada.");}catch(e){setError((e as Error).message);}finally{setBusy(false);}}
  const clamp=(x:number,y:number)=>({x:Math.round(Math.max(4,Math.min(117,x))*10)/10,y:Math.round(Math.max(4,Math.min(256,y))*10)/10});
  const point=(e:PointerEvent<HTMLElement>)=>{const rect=sheet.current!.getBoundingClientRect();return {x:(e.clientX-rect.left)/rect.width*210,y:(e.clientY-rect.top)/rect.height*297};};
  function place(e:PointerEvent<HTMLDivElement>){if(!mode||busy||pageNumber!==1)return;const p=point(e);setPosition(clamp(p.x-44.5,p.y-18.5));}
  function startMode(next:"RECEIVE"|"MOVE"){setPageNumber(1);setPosition({x:marks?.stampX??109,y:marks?.stampY??191});setMode(next);setNotice("");}
  function draw(e:PointerEvent<HTMLCanvasElement>,start=false){if(busy)return;const target=signature.current!,ctx=target.getContext("2d")!,rect=target.getBoundingClientRect(),x=(e.clientX-rect.left)*target.width/rect.width,y=(e.clientY-rect.top)*target.height/rect.height;
    if(start){drawing.current=true;target.setPointerCapture(e.pointerId);ctx.strokeStyle="#102e46";ctx.fillStyle="#102e46";ctx.lineWidth=3;ctx.lineCap="round";ctx.lineJoin="round";ctx.beginPath();ctx.arc(x,y,1.5,0,Math.PI*2);ctx.fill();ctx.beginPath();ctx.moveTo(x,y);setDirty(true);}else if(drawing.current){ctx.lineTo(x,y);ctx.stroke();}}
  async function applySignature(save:boolean,existing?:string){
    if(busy)return;
    if(!existing&&!dirty){setError("Dibuja tu firma antes de continuar.");return;}
    setBusy(true);setError("");
    try{
      const data=existing||signature.current!.toDataURL("image/png");
      const registered=await registerDocumentSignature(endpoint,signatureRole,data);
      setMarks(registered);setSignatureOpen(false);setPageNumber(1);
      setNotice("Firma registrada y verificada en este formulario.");
      if(save){try{await maintenanceApi("/api/account/saved-signature","PUT",{signatureData:data});setSavedSignature(data);setNotice("Firma registrada en el formulario y guardada para reutilizar con tu cuenta.");}catch{setNotice("La firma quedó registrada en este formulario. No se pudo guardar la copia reutilizable para tu cuenta.");}}
    }catch(e){setError((e as Error).message);}finally{setBusy(false);}
  }
  async function download(){if(!marks)return;setBusy(true);setError("");try{const current=await maintenanceApi<DocumentMarks>(endpoint);setMarks(current);(await buildMaintenancePdf(item,current)).save(`${item.documentNumber.replace(/[^a-z0-9_-]/gi,"_")}.pdf`);}catch(e){setError((e as Error).message);}finally{setBusy(false);}}
  return <div className="mw-overlay"><section ref={dialog} className="mw-editor" role="dialog" aria-modal="true" aria-labelledby="document-editor-title" tabIndex={-1} onKeyDown={e=>{
    if(e.key==="Escape"&&!busy){if(signatureOpen)setSignatureOpen(false);else if(mode)setMode(null);else onClose();}
    if(e.key==="Tab"){const scope=signatureOpen?dialog.current?.querySelector(".mw-sign-panel"):dialog.current;const nodes=scope?.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),[tabindex="0"]');if(nodes?.length){const first=nodes[0],last=nodes[nodes.length-1];if(e.shiftKey&&(document.activeElement===first||document.activeElement===dialog.current)){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}}}
  }}>
    <header className="mw-header"><div><small>FORMULARIO INSTITUCIONAL · PDF + QR</small><h2 id="document-editor-title">{item.documentNumber}</h2><p>{marks?.receivedAt?`${marks.stampLabel==="ENTREGADO"?"Entregado":"Recibido"} por ${marks.receiverName} · ${formatDocumentTime(marks.receivedAt)}`:"Pendiente de firmas y confirmación"}</p></div><button disabled={busy} onClick={onClose} aria-label="Cerrar formulario"><X/></button></header>
    <div className="mw-toolbar" inert={signatureOpen}>{marks?.canReceive&&<button disabled={busy||!!mode} onClick={()=>startMode("RECEIVE")}><Stamp/> {marks.stampLabel==="ENTREGADO"?"Entregado":"Recibido"}</button>}{marks?.receivedAt&&marks.canMove&&<button disabled={busy||!!mode} onClick={()=>startMode("MOVE")}><Move/> Mover sello</button>}{marks?.canSignDelivery&&<button disabled={busy||!!mode} onClick={()=>{setSignatureRole("DELIVERY");setDirty(false);setSignatureOpen(true);setError("");}}><PenLine/> Firma de quien entrega</button>}{marks?.canSignReceipt&&<button disabled={busy||!!mode} onClick={()=>{setSignatureRole("RECEIPT");setDirty(false);setSignatureOpen(true);setError("");}}><PenLine/> Firma de quien recibe</button>}<button disabled={!marks||busy||!!mode} className="primary" onClick={()=>void download()}><Download/> Descargar PDF</button><button disabled={busy||!!mode} onClick={()=>void reload().catch(e=>setError(e.message))} aria-label="Actualizar formulario"><RefreshCw/></button></div>
    {marks&&<div className="mw-signature-summary" inert={signatureOpen}>{([["DELIVERY","Quien entrega",marks.deliverySignature],["RECEIPT","Quien recibe",marks.receiptSignature]] as const).map(([role,label,signature])=><article key={role}><strong>{label}</strong>{signature?<><img src={signature.data} alt={`Firma de ${signature.name}`}/><span>{signature.name}</span><small>{signature.login} · {formatDocumentTime(signature.signedAt)}</small></>:<span>Firma pendiente</span>}</article>)}</div>}
    {(marks?.signatureData||item.signatureData)&&<p className="mw-legacy-note">La firma anterior está visible en la primera página y se conserva en el anexo. Su registro original no especificaba entrega o recepción.</p>}
    {error&&<div className="mw-error" role="alert">{error}</div>}{notice&&<p className="mw-notice" role="status">{notice}</p>}
    {mode&&<div className="mw-placement"><strong>{mode==="RECEIVE"?`Elige dónde confirmar ${marks?.stampLabel==="ENTREGADO"?"la entrega":"la recepción"}`:"Mover sello"}</strong><p>Pulsa en la hoja o arrastra el recuadro. La ubicación se guarda al confirmar. Evita cubrir datos o el QR.</p><label>Horizontal (mm)<input type="number" min={4} max={117} step="0.1" value={position.x} disabled={busy} onChange={e=>setPosition(clamp(Number(e.target.value),position.y))}/></label><label>Vertical (mm)<input type="number" min={4} max={256} step="0.1" value={position.y} disabled={busy} onChange={e=>setPosition(clamp(position.x,Number(e.target.value)))}/></label><button className="primary" disabled={busy} onClick={()=>void savePosition()}>{busy?"Guardando…":mode==="RECEIVE"?`Confirmar ${marks?.stampLabel==="ENTREGADO"?"entregado":"recibido"} con mi cuenta`:"Guardar ubicación"}</button><button disabled={busy} onClick={()=>setMode(null)}>Cancelar</button></div>}
    <div className="mw-preview" inert={signatureOpen}>
      <div className="mw-preview-controls"><strong>Vista del formulario · A4</strong><label>Zoom <select aria-label="Zoom del formulario" value={zoom} onChange={e=>setZoom(Number(e.target.value))}><option value={100}>Ajustar a pantalla</option><option value={125}>125 %</option><option value={150}>150 %</option></select></label><span>Página {pageNumber} de {pageCount}</span></div>
      {rendering&&<p role="status">Preparando vista previa del PDF…</p>}
      <div className="mw-preview-scroll"><div className={`mw-sheet ${mode?"placing":""}`} style={{width:zoom===100?undefined:`${zoom}%`,maxWidth:zoom===100?undefined:"none"}} ref={sheet} onPointerDown={place}>
        <canvas ref={canvas} aria-label="Vista previa del formulario PDF"/>
        {mode&&pageNumber===1&&<div className="mw-stamp-ghost" style={{left:`${position.x/210*100}%`,top:`${position.y/297*100}%`,width:`${89/210*100}%`,height:`${37/297*100}%`}} onPointerDown={e=>{e.stopPropagation();if(busy)return;const p=point(e);offset.current={x:p.x-position.x,y:p.y-position.y};dragging.current=true;e.currentTarget.setPointerCapture(e.pointerId);}} onPointerMove={e=>{if(!dragging.current)return;const p=point(e);setPosition(clamp(p.x-offset.current.x,p.y-offset.current.y));}} onPointerUp={()=>{dragging.current=false;}} onPointerCancel={()=>{dragging.current=false;}}><Move/><strong>Ubicación del sello</strong><small>Pendiente de guardar</small></div>}
      </div></div>
      {pageCount>1&&<div className="mw-pagination"><button disabled={pageNumber===1||!!mode} onClick={()=>setPageNumber(p=>p-1)}>Anterior</button><span>Página {pageNumber} de {pageCount}</span><button disabled={pageNumber===pageCount||!!mode} onClick={()=>setPageNumber(p=>p+1)}>Siguiente</button></div>}
    </div>
    {signatureOpen&&<div className="mw-sign-overlay"><section className="mw-sign-panel" role="dialog" aria-modal="true" aria-labelledby="signature-title"><header><h3 id="signature-title">{signatureRole==="DELIVERY"?"Firma de quien entrega":"Firma de quien recibe"}</h3><button autoFocus disabled={busy} onClick={()=>setSignatureOpen(false)} aria-label="Cerrar firma"><X/></button></header><p>Dibuja con el mouse, lápiz o dedo. Se completará únicamente el campo de {signatureRole==="DELIVERY"?"quien entrega":"quien recibe"}, con tu cuenta y la hora de firma.</p><canvas ref={signature} width={800} height={240} aria-label="Dibujar firma" onPointerDown={e=>draw(e,true)} onPointerMove={e=>draw(e)} onPointerUp={()=>{drawing.current=false;}} onPointerCancel={()=>{drawing.current=false;}}/>
      {error&&<p className="mw-error" role="alert">{error}</p>}<div className="mw-toolbar"><button disabled={busy} onClick={()=>{signature.current?.getContext("2d")?.clearRect(0,0,800,240);setDirty(false);}}>Limpiar</button><button disabled={busy||!dirty} onClick={()=>void applySignature(false)}>Usar una vez</button><button className="primary" disabled={busy||!dirty} onClick={()=>void applySignature(true)}>Guardar firma y usar</button></div><small>“Usar una vez” no guarda una plantilla reutilizable. La firma aplicada permanece en este documento. “Guardar firma” la deja disponible solo para tu cuenta.</small>{savedSignature&&<div className="mw-saved-signature"><img src={savedSignature} alt="Tu firma guardada"/><button disabled={busy} onClick={()=>void applySignature(false,savedSignature)}>Usar firma guardada</button></div>}</section></div>}
  </section></div>;
}
