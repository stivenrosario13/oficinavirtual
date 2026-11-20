import { apiFetch as fetch } from "@/lib/api-fetch";
import {useEffect,useMemo,useState} from 'react';
import {Copy,MapPin,Share2,RefreshCw} from 'lucide-react';
import {type AgencyLocation,locationUrl,locationText,locationAddress,searchLocation} from '@/lib/agency-locations';
import {pageRows} from '@/lib/warehouse-render-index';
import WarehouseListPager from './WarehouseListPager';
import './agencyLocations.css';

export default function AgencyLocations(){
  const [rows,setRows]=useState<AgencyLocation[]>([]),[query,setQuery]=useState(''),[group,setGroup]=useState(''),[status,setStatus]=useState('all');
  const [page,setPage]=useState(1),[refresh,setRefresh]=useState(0),[loading,setLoading]=useState(true),[error,setError]=useState(''),[notice,setNotice]=useState(''),[manual,setManual]=useState('');
  useEffect(()=>{
    const controller=new AbortController();let active=true;
    const timer=setTimeout(()=>controller.abort(),20000);
    setLoading(true);setError('');
    fetch('/api/agency-locations',{credentials:'same-origin',cache:'no-store',signal:controller.signal}).then(async response=>{
      if(!response.ok)throw new Error(response.status===401?'Tu sesión venció. Vuelve a iniciar sesión.':'No se pudieron consultar las ubicaciones.');
      const data=await response.json();if(active)setRows(data.agencies||[]);
    }).catch(reason=>{if(active)setError(reason.name==='AbortError'?'La consulta tardó demasiado. Pulsa Actualizar para reintentar.':reason.message);}).finally(()=>{clearTimeout(timer);if(active)setLoading(false);});
    return()=>{active=false;controller.abort();clearTimeout(timer);};
  },[refresh]);
  const groups=useMemo(()=>Array.from(new Set(rows.map(row=>row.grupo).filter(Boolean))).sort((a,b)=>a.localeCompare(b,'es')),[rows]);
  const located=useMemo(()=>rows.filter(row=>locationUrl(row)).length,[rows]);
  const filtered=useMemo(()=>rows.filter(row=>(!group||row.grupo===group)&&searchLocation(row,query)&&(status==='all'||(status==='located'?!!locationUrl(row):!locationUrl(row)))),[rows,group,query,status]);
  const current=pageRows(filtered,page,30);
  async function copy(text:string){
    setNotice('');setManual('');
    try{await navigator.clipboard.writeText(text);setNotice('Ubicación copiada al portapapeles.');}
    catch{setManual(text);setNotice('El navegador no permitió copiar automáticamente. Selecciona y copia el texto de abajo.');}
  }
  async function share(row:AgencyLocation){
    const text=locationText(row);setNotice('');setManual('');
    if(!navigator.share){await copy(text);return;}
    try{await navigator.share({title:`Agencia ${row.codigo}`,text});}
    catch(reason){if((reason as Error).name!=='AbortError')await copy(text);}
  }
  return <section className="agency-locations">
    <header><div><span><MapPin size={18}/> DIRECTORIO DE AGENCIAS</span><h1>Ubicaciones de agencias</h1><p>Consulta y comparte las ubicaciones registradas de las agencias a las que tienes acceso.</p></div><button disabled={loading} onClick={()=>setRefresh(value=>value+1)}><RefreshCw size={17}/>{loading?'Cargando…':'Actualizar'}</button></header>
    <div className="location-counts"><strong>{rows.length} agencias</strong><span>{located} con ubicación</span><span>{rows.length-located} sin coordenadas</span></div>
    <div className="location-filters"><label>Buscar agencia<input value={query} onChange={event=>{setQuery(event.target.value);setPage(1);}} placeholder="Código, nombre o dirección"/></label><label>Grupo<select value={group} onChange={event=>{setGroup(event.target.value);setPage(1);}}><option value="">Todos los grupos</option>{groups.map(name=><option key={name}>{name}</option>)}</select></label><label>Ubicación<select value={status} onChange={event=>{setStatus(event.target.value);setPage(1);}}><option value="all">Todas</option><option value="located">Con coordenadas</option><option value="missing">Sin coordenadas</option></select></label><button disabled={!filtered.length||loading} onClick={()=>void copy(filtered.map(locationText).join('\n\n'))}><Copy size={17}/>Copiar lista filtrada ({filtered.length})</button></div>
    {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
    {manual&&<label className="location-manual">Texto para copiar<textarea readOnly value={manual} onFocus={event=>event.target.select()}/></label>}
    {loading&&!rows.length?<p role="status">Consultando las agencias…</p>:!filtered.length?<p>No hay agencias que coincidan con los filtros.</p>:<div className="location-grid">{current.rows.map(row=>{const url=locationUrl(row);return <article key={row.id}><small>{row.grupo||'Sin grupo'}</small><h2>{row.codigo} · {row.terminal}</h2><p>{locationAddress(row)||'Sin dirección registrada'}</p><p className="location-coordinates">{url?`${row.latitude}, ${row.longitude}`:'Sin coordenadas registradas'}</p><div>{url&&<a href={url} target="_blank" rel="noopener noreferrer"><MapPin size={16}/>Abrir mapa</a>}<button onClick={()=>void copy(locationText(row))}><Copy size={16}/>Copiar</button><button onClick={()=>void share(row)}><Share2 size={16}/>Compartir</button></div></article>;})}</div>}
    <WarehouseListPager page={current.page} pages={current.pages} start={current.start} end={current.end} total={current.total} onPage={setPage}/>
  </section>;
}
