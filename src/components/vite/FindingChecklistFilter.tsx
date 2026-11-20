import {useEffect,useRef,useState,type ReactNode} from "react";
import {Check,ChevronDown} from "lucide-react";
import "./findingChecklistFilter.css";

export type FindingFilterOption={value:string;label:string};

export default function FindingChecklistFilter({title,icon,options,selected,onChange}:{title:string;icon:ReactNode;options:FindingFilterOption[];selected:string[];onChange:(values:string[])=>void}){
  const [open,setOpen]=useState(false);
  const root=useRef<HTMLDivElement>(null);
  useEffect(()=>{
    if(!open)return;
    const close=(event:PointerEvent)=>{if(root.current&&!root.current.contains(event.target as Node))setOpen(false)};
    const escape=(event:KeyboardEvent)=>{if(event.key==="Escape")setOpen(false)};
    document.addEventListener("pointerdown",close);
    document.addEventListener("keydown",escape);
    return()=>{document.removeEventListener("pointerdown",close);document.removeEventListener("keydown",escape)};
  },[open]);
  const active=selected.filter(value=>options.some(option=>option.value===value));
  const summary=active.length?`${active.length} seleccionad${active.length===1?"o":"os"}`:"Todos";
  const toggle=(value:string)=>onChange(active.includes(value)?active.filter(item=>item!==value):[...active,value]);
  return <div className="finding-checklist-filter" ref={root}>
    <button type="button" className="finding-checklist-trigger" aria-expanded={open} aria-haspopup="true" onClick={()=>setOpen(value=>!value)}>{icon}<span><strong>Filtrar {title}</strong><small>{summary}</small></span><ChevronDown className={open?"open":""}/></button>
    {open&&<div className="finding-checklist-menu" role="group" aria-label={`Filtrar ${title}`}>
      <div className="finding-checklist-menu-head"><strong>{title[0].toUpperCase()+title.slice(1)}</strong><button type="button" onClick={()=>onChange([])} disabled={!active.length}>Mostrar todos</button></div>
      <div className="finding-checklist-options">{options.map(option=><label key={option.value}><input type="checkbox" checked={active.includes(option.value)} onChange={()=>toggle(option.value)}/><span>{option.label}</span>{active.includes(option.value)&&<Check/>}</label>)}{!options.length&&<p>No hay opciones disponibles.</p>}</div>
    </div>}
  </div>;
}
