import {useId,useRef,useState} from 'react';
import {Search,X} from 'lucide-react';
import './searchSelect.css';

export type SearchOption={value:string;label:string;description?:string};
const normalize=(value:string)=>value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').toLocaleLowerCase('es');
/** One searchable, keyboard-accessible field for large catalogs. Values must be selected. */
export default function SearchSelect({label,value,options,onChange,placeholder='Escribe para buscar',disabled=false,required=false,loading=false}:{label:string;value:string;options:SearchOption[];onChange:(value:string)=>void;placeholder?:string;disabled?:boolean;required?:boolean;loading?:boolean}){
 const id=useId(),input=useRef<HTMLInputElement>(null);
 const [open,setOpen]=useState(false),[query,setQuery]=useState(''),[active,setActive]=useState(0);
 const selected=options.find(option=>option.value===value);
 const matches=options.filter(option=>normalize(`${option.label} ${option.description||''}`).includes(normalize(query)));
 const visible=matches.slice(0,60);
 const choose=(option:SearchOption)=>{onChange(option.value);setOpen(false);setQuery('');setActive(0);input.current?.focus()};
 return <div className="search-select" onBlur={event=>{if(!event.currentTarget.contains(event.relatedTarget as Node))setOpen(false)}}>
  <label htmlFor={id}>{label}</label>
  <div className="search-select-control"><Search aria-hidden="true"/><input ref={input} id={id} role="combobox" aria-autocomplete="list" aria-expanded={open} aria-controls={`${id}-options`} aria-activedescendant={open&&visible[active]?`${id}-option-${active}`:undefined} autoComplete="off" required={required} disabled={disabled||loading} placeholder={loading?'Cargando…':placeholder} value={open?query:selected?.label||''} onClick={()=>{if(!open){setOpen(true);setQuery('');setActive(0)}}} onChange={event=>{setQuery(event.target.value);setOpen(true);setActive(0);if(value)onChange('')}} onKeyDown={event=>{
   if(event.key==='ArrowDown'||event.key==='ArrowUp'){event.preventDefault();setOpen(true);setActive(current=>open?Math.max(0,Math.min(visible.length-1,current+(event.key==='ArrowDown'?1:-1))):0)}
   else if(event.key==='Enter'&&open){event.preventDefault();if(visible[active])choose(visible[active])}
   else if(event.key==='Escape'){event.preventDefault();event.stopPropagation();setOpen(false)}
  }}/>{value&&!disabled&&<button type="button" aria-label={`Limpiar ${label.toLocaleLowerCase('es')}`} onClick={()=>{onChange('');setQuery('');setOpen(true);input.current?.focus()}}><X/></button>}</div>
  {open&&!disabled&&<div className="search-select-dropdown"><div role="listbox" id={`${id}-options`} aria-label={label}>{visible.map((option,index)=><div role="option" id={`${id}-option-${index}`} aria-selected={option.value===value} className={index===active?'highlighted':''} key={option.value} onMouseDown={event=>event.preventDefault()} onMouseEnter={()=>setActive(index)} onClick={()=>choose(option)}><Search aria-hidden="true"/><span><strong>{option.label}</strong>{option.description&&<small>{option.description}</small>}</span></div>)}</div><small role="status">{matches.length?matches.length>60?'Mostrando 60 resultados. Escribe más para precisar la búsqueda.':`${matches.length} resultado${matches.length===1?'':'s'}`:'Sin coincidencias'}</small></div>}
 </div>;
}
