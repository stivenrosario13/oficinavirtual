import {usePdfPreview} from "@/lib/use-pdf-preview";
import {useEffect,useLayoutEffect,useRef,useState} from 'react';
import './warehouseFulfillment.css';
import './institutionalDocumentViewer.css';

import {administrativeSealLayout} from './administrativeSealLayout';
export default function InstitutionalPdfCanvas({data,zoom=100,onPlace,mark,showAdministrativeSeal=false}:{data:Uint8Array|null;zoom?:number;onPlace?:(x:number,y:number)=>void;mark?:{x:number;y:number};showAdministrativeSeal?:boolean}){
 const [sealPage,setSealPage]=useState<number|null>(null);
 useEffect(()=>{let cancelled=false;setSealPage(null);if(data&&showAdministrativeSeal)void import('pdf-lib').then(({PDFDocument})=>PDFDocument.load(data)).then(pdf=>{
  const subject=pdf.getSubject()||'';
  if(!/REAL-ADMINISTRATIVE-(?:REQUEST-V372|FORM-V376)/.test(subject))return;
  const layout=administrativeSealLayout(subject,pdf.getPages().map(p=>p.getSize()));
  if(!cancelled)setSealPage(layout?layout.page+1:null);
 }).catch(()=>{});return()=>{cancelled=true}},[data,showAdministrativeSeal]);
 const [page,setPage]=useState(1),[fitWidth,setFitWidth]=useState(0),[fallbackUrl,setFallbackUrl]=useState('');const canvas=useRef<HTMLCanvasElement>(null),scroll=useRef<HTMLDivElement>(null),previousZoom=useRef(zoom);
 useEffect(()=>{if(!data)return;const url=URL.createObjectURL(new Blob([data.slice().buffer],{type:'application/pdf'}));setFallbackUrl(url);return()=>URL.revokeObjectURL(url)},[data]);
 useEffect(()=>{const element=scroll.current;if(!element)return;const update=()=>setFitWidth(Math.max(300,element.clientWidth-48));update();const observer=new ResizeObserver(update);observer.observe(element);return()=>observer.disconnect()},[]);
 const {count,loading,error,renderedWidth,retry}=usePdfPreview(data,page,canvas,1.75);
 useEffect(()=>setPage(1),[data]);
 useEffect(()=>{if(page>count)setPage(count)},[page,count]);
 useLayoutEffect(()=>{const element=scroll.current,prior=previousZoom.current;if(!element||prior===zoom)return;const center=element.scrollLeft+element.clientWidth/2,ratio=zoom/prior;previousZoom.current=zoom;requestAnimationFrame(()=>{element.scrollLeft=Math.max(0,center*ratio-element.clientWidth/2)})},[zoom]);
 const baseWidth=Math.min(renderedWidth||fitWidth,fitWidth||renderedWidth||760),paperWidth=Math.max(300,baseWidth*zoom/100);
 return <div className="id-pdf"><nav>{sealPage&&<button disabled={loading} onClick={()=>setPage(sealPage)}>Ver sello · página {sealPage}</button>}<button disabled={page<=1||loading} onClick={()=>setPage(p=>p-1)}>Anterior</button><span>Página {page} / {count}</span><button disabled={page>=count||loading} onClick={()=>setPage(p=>p+1)}>Siguiente</button></nav>{error&&<p className="id-pdf-error" role="alert">No se puede previsualizar: {error} <button onClick={retry}>Reintentar vista previa</button></p>}{(loading||!data)&&<p className="id-pdf-loading" role="status">{data?"Preparando página…":"Cargando comprobante…"}</p>}<div className="id-pdf-scroll" ref={scroll}><div className="id-pdf-stage" style={{width:`${Math.max(fitWidth,paperWidth)+24}px`}}><div className="id-pdf-paper" style={{width:`${paperWidth}px`}}>{error&&fallbackUrl?<iframe className="id-pdf-fallback" src={fallbackUrl} title="Comprobante PDF (visor alternativo)"/>:<canvas ref={canvas} aria-label={`Página ${page}`}/>}{onPlace&&page===1&&<button className="id-place" aria-label="Seleccionar ubicación del sello en la primera página" onClick={e=>{const r=e.currentTarget.getBoundingClientRect();onPlace((e.clientX-r.left)/r.width*100,(e.clientY-r.top)/r.height*100)}}/>}{mark&&page===1&&<div className="id-stamp-ghost" style={{left:`${Math.min(61,mark.x)}%`,top:`${Math.min(85,mark.y)}%`}}>Ubicación del sello<br/>REAL · Grupo Tejeda</div>}</div></div></div>{onPlace&&<p>Selecciona el lugar del sello en la primera página. También puedes ajustar sus coordenadas.</p>}</div>
}
