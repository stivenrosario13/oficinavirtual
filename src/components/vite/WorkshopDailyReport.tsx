import {useMemo,useState} from 'react';
import {ArrowDownUp,Ban,CalendarDays,Clock3,Download,FileClock} from 'lucide-react';
import type {WorkshopCase} from './workshopWorkflow';
import {buildWorkshopReportRows,todayInSantoDomingo,workshopReportRangeLabel,type WorkshopReportKind} from './workshopReportData';
import {buildWorkshopReportPdf} from './workshopDailyReportPdf';
import {downloadDocument,realLogoData} from './institutionalDocumentAssets';

const configurations:Record<WorkshopReportKind,{title:string;description:string;file:string;empty:string}>={
 MOVEMENTS:{title:'Reporte de movimientos',description:'Bitácora completa de toda la actividad realizada en Taller durante el rango seleccionado.',file:'Movimientos-Taller',empty:'No se registraron movimientos de Taller en el rango seleccionado.'},
 ENTRIES_EXITS:{title:'Reporte de entradas y salidas',description:'Equipos recibidos, ingresados, enviados a Almacén o despachados sin reparación.',file:'Entradas-Salidas-Taller',empty:'No se registraron entradas ni salidas en el rango seleccionado.'},
 UNREPAIRED:{title:'Equipos sin reparación',description:'Equipos que salieron de Taller sin reparación dentro del rango seleccionado.',file:'Equipos-Sin-Reparacion',empty:'No se registraron equipos sin reparación en el rango seleccionado.'},
};
const entryActions=new Set(['Registro / envío a Taller','Recepción en Taller','Ingreso a inventario','Recepción en Almacén']);
const exitActions=new Set(['Salida sin reparación','Envío a Almacén']);
function firstDayOfMonth(date:string){return `${date.slice(0,8)}01`;}
function shortDate(value:string){return new Intl.DateTimeFormat('es-DO',{timeZone:'America/Santo_Domingo',day:'2-digit',month:'2-digit',year:'numeric'}).format(new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value)?value:value+'Z'));}

export default function WorkshopDailyReport({items,refreshing,onRefresh}:{items:WorkshopCase[];refreshing:boolean;onRefresh:()=>Promise<WorkshopCase[]>}){
 const today=todayInSantoDomingo(),[kind,setKind]=useState<WorkshopReportKind>('MOVEMENTS'),[from,setFrom]=useState(()=>firstDayOfMonth(today)),[to,setTo]=useState(today),[fromTime,setFromTime]=useState('00:00'),[toTime,setToTime]=useState('23:59'),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const valid=!!from&&!!to&&!!fromTime&&!!toTime&&`${from}T${fromTime}`<=`${to}T${toTime}`;
 const rows=useMemo(()=>valid?buildWorkshopReportRows(items,from,to,kind,fromTime,toTime):[],[items,from,to,kind,fromTime,toTime,valid]);
 const equipment=useMemo(()=>new Set(rows.map(row=>`${row.documentNumber}|${row.serial}`)).size,[rows]);
 const entries=rows.filter(row=>entryActions.has(row.action)).length,exits=rows.filter(row=>exitActions.has(row.action)).length,configuration=configurations[kind];
 async function download(){if(!valid)return;setBusy(true);setError('');try{const current=await onRefresh();const freshRows=buildWorkshopReportRows(current,from,to,kind,fromTime,toTime);const pdf=buildWorkshopReportPdf({from,to,fromTime,toTime,title:configuration.title,emptyMessage:configuration.empty,rows:freshRows,logo:await realLogoData()});downloadDocument(pdf,`${configuration.file}-${from}-${fromTime.replace(':','')}-al-${to}-${toTime.replace(':','')}.pdf`);}catch(e){setError(`No se pudo descargar el reporte: ${(e as Error).message}`);}finally{setBusy(false)}}
 return <section className="workshop-daily-report" aria-label="Reportes de Taller">
  <header><div><small>REPORTES INSTITUCIONALES · TALLER</small><h2><FileClock/>{configuration.title}</h2><p>{configuration.description}</p></div></header>
  <nav className="workshop-report-types" aria-label="Tipos de reporte de Taller"><button aria-pressed={kind==='ENTRIES_EXITS'} onClick={()=>setKind('ENTRIES_EXITS')}><ArrowDownUp/>Entradas y salidas</button><button aria-pressed={kind==='UNREPAIRED'} onClick={()=>setKind('UNREPAIRED')}><Ban/>Equipos sin reparación</button><button aria-pressed={kind==='MOVEMENTS'} onClick={()=>setKind('MOVEMENTS')}><FileClock/>Movimientos</button></nav>
  <div className="workshop-report-range"><div><strong>Rango para imprimir</strong><span>Selecciona las fechas y las horas exactas que debe contener la descarga.</span></div><div className="workshop-report-controls"><label><CalendarDays/>Desde<input aria-label="Fecha desde" type="date" value={from} max={to||today} onChange={event=>setFrom(event.target.value)}/></label><label><Clock3/>Hora desde<input aria-label="Hora desde" type="time" value={fromTime} onChange={event=>setFromTime(event.target.value)}/></label><label><CalendarDays/>Hasta<input aria-label="Fecha hasta" type="date" value={to} min={from} max={today} onChange={event=>setTo(event.target.value)}/></label><label><Clock3/>Hora hasta<input aria-label="Hora hasta" type="time" value={toTime} onChange={event=>setToTime(event.target.value)}/></label><button disabled={busy||refreshing||!valid} onClick={()=>void download()}><Download/>{busy?'Preparando…':'Descargar e imprimir PDF'}</button></div></div>
  {!valid&&<p role="alert" className="wt-error">La fecha y hora “Desde” no pueden ser posteriores a “Hasta”.</p>}{error&&<p role="alert" className="wt-error">{error}</p>}
  <div className="workshop-report-summary"><article><span>Periodo seleccionado</span><strong>{valid?`${workshopReportRangeLabel(from,to)} · ${fromTime} a ${toTime}`:'Rango no válido'}</strong></article><article><span>{kind==='ENTRIES_EXITS'?'Entradas':'Registros'}</span><strong>{kind==='ENTRIES_EXITS'?entries:rows.length}</strong></article><article><span>{kind==='ENTRIES_EXITS'?'Salidas':'Equipos involucrados'}</span><strong>{kind==='ENTRIES_EXITS'?exits:equipment}</strong></article></div>
  <div className="workshop-report-table"><table><thead><tr>{['Fecha / hora','Movimiento','Formulario','Equipo / serial','Departamento / agencia','Responsable','Detalle'].map(label=><th key={label}>{label}</th>)}</tr></thead><tbody>{rows.map(row=><tr key={row.id}><td>{shortDate(row.occurredAt)}<small>{row.time}</small></td><td>{row.action}</td><td>{row.documentNumber}</td><td><strong>{row.equipment}</strong><small>{row.serial}</small></td><td>{row.department}<small>{row.agency}</small></td><td>{row.actor}<small>{row.login}</small></td><td>{row.detail}</td></tr>)}</tbody></table>{!rows.length&&<p>{configuration.empty} Puedes descargar el PDF como constancia.</p>}</div>
 </section>;
}
