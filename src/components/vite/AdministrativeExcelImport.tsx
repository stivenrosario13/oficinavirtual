import {useEffect,useRef,useState} from 'react';
import {Download,FileSpreadsheet,Pencil,Trash2,Upload,X} from 'lucide-react';
import type {PortalSession} from '@/lib/session';
import {administrativeColumns,administrativeTemplate,readAdministrativeExcel,type AdministrativeDraft,type AdministrativeFormKind,type ImportedForm} from '@/lib/administrative-form-excel';
import {administrativeDefaults} from '@/lib/administrative-form-defaults';
import {prepareAdministrativeForm,validateAdministrativeForm} from '@/lib/administrative-form-data';
import {saveAdministrativeForm,validateInternetDraft} from '@/lib/administrative-form-save';
import AdministrativeRequestComposer from './AdministrativeRequestComposer';
import './administrativeExcelImport.css';

export default function AdministrativeExcelImport({kind,title,session,department,onClose,onSaved}:{kind:AdministrativeFormKind;title:string;session:PortalSession;department:string;onClose:()=>void;onSaved:()=>Promise<void>}){
  const [forms,setForms]=useState<ImportedForm[]>([]),[error,setError]=useState(''),[busy,setBusy]=useState(false),[editing,setEditing]=useState<number|null>(null),[saved,setSaved]=useState(0),[progress,setProgress]=useState(''),[attempted,setAttempted]=useState<string[]>([]);
  useEffect(()=>{const previous=document.body.style.overflow;document.body.style.overflow='hidden';return()=>{document.body.style.overflow=previous}},[]);
  const fileInput=useRef<HTMLInputElement>(null),inFlight=useRef(false),stop=useRef(false);
  const validation=(form:AdministrativeDraft)=>kind==='internet'?validateInternetDraft(form.values):validateAdministrativeForm(kind,form.values,form.rows,form.phones,prepareAdministrativeForm(kind,form.values,form.rows,form.phones,department,session));
  const errors=forms.map(validation);
  async function template(){setBusy(true);setError('');try{const book=await administrativeTemplate(kind);const bytes=await book.xlsx.writeBuffer();const url=URL.createObjectURL(new Blob([new Uint8Array(bytes)],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'}));const link=document.createElement('a');link.href=url;link.download=`Plantilla-${kind}.xlsx`;link.click();setTimeout(()=>URL.revokeObjectURL(url),10000)}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
  async function read(file:File){setBusy(true);setError('');try{const rows=await readAdministrativeExcel(file,kind);setForms(rows.map(row=>({...row,values:{...administrativeDefaults(kind,session).values,...row.values}})));setSaved(0);setAttempted([]);setProgress('')}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
  async function submit(){
    if(inFlight.current||errors.some(Boolean)||!forms.length)return;
    inFlight.current=true;stop.current=false;setBusy(true);setError('');
    try{
      for(let index=0;index<forms.length;index++){
        if(stop.current)break;
        const form=forms[index];setAttempted(current=>[...current,form.number!]);setProgress(`Guardando ${index+1} de ${forms.length}: ${form.group}`);
        try{await saveAdministrativeForm(kind,form,department,session);}catch(error){
          if(!(error as Error&{uncertain?:boolean}).uncertain)setAttempted(current=>current.filter(number=>number!==form.number));
          throw error;
        }
        setSaved(count=>count+1);setForms(current=>current.filter(item=>item.number!==form.number));
        // The API allows 60 writes per minute. Leave room between confirmed documents.
        if(index<forms.length-1&&!stop.current)await new Promise(resolve=>setTimeout(resolve,1100));
      }
      setProgress(stop.current?'Importación detenida. Puedes continuar con los pendientes.':'Importación completada.');
    }catch(e){setError(`${(e as Error).message} Los formularios confirmados ya se guardaron. Puedes continuar con los pendientes usando sus mismos folios.`);setProgress('Importación interrumpida.')}finally{inFlight.current=false;setBusy(false)}
  }
  function updateDraft(draft:AdministrativeDraft){setForms(current=>current.map((form,index)=>index===editing?{...form,...draft}:form));setEditing(null)}
  if(editing!==null&&kind!=='internet')return <AdministrativeRequestComposer session={session} department={department} initialKind={kind} initialDraft={forms[editing]} onClose={()=>setEditing(null)} onSaved={async()=>{}} onDraftSaved={updateDraft}/>;
  return <div className="adw-dialog-backdrop admin-excel-backdrop"><section className="adw-dialog admin-request-form admin-excel-import" role="dialog" aria-modal="true" aria-label={`Importar ${title} desde Excel`}>
    <header><div><small>IMPORTACIÓN MASIVA</small><h3><FileSpreadsheet/> {title}</h3></div><button disabled={busy} onClick={()=>{if(saved)void onSaved();else onClose()}} aria-label="Cerrar"><X/></button></header>
    <p>{kind==='gas_log'?<>La plantilla de GAS contiene únicamente <strong>FECHA · PLACA · MONTO · GESTOR RUTA</strong>. También puedes cargar un listado histórico: cada hoja mensual válida se revisará como un formulario independiente.</>:<>La plantilla contiene únicamente las columnas obligatorias de este formulario. Cada fila completa crea una solicitud que podrás revisar antes de importarla.</>}</p>
    <div className="admin-excel-tools"><button disabled={busy} onClick={()=>void template()}><Download/> Descargar plantilla Excel</button><button disabled={busy||saved>0||attempted.length>0} onClick={()=>fileInput.current?.click()}><Upload/> Cargar Excel .xlsx</button><input ref={fileInput} hidden type="file" accept=".xlsx" onChange={event=>{const file=event.target.files?.[0];event.target.value='';if(file)void read(file)}}/></div>
    <p>Hasta <strong>50,000 filas</strong> y 50 MB por archivo. Las relaciones extensas se dividen automáticamente en formularios de hasta 100 detalles. Los datos se guardan al pulsar <strong>Importar formularios</strong>.</p>
    {error&&<p className="mw-error" role="alert">{error}</p>}
    <p role="status" aria-live="polite">{saved} guardados · {forms.length} pendientes{errors.some(Boolean)?` · ${errors.filter(Boolean).length} por corregir`:''}. {progress}</p>
    {editing!==null&&kind==='internet'?<div className="admin-form-grid">{administrativeColumns(kind).filter(c=>c.key!=='_form').map(column=><label key={column.key}>{column.label}<input value={forms[editing].values[column.key]||''} onChange={event=>setForms(current=>current.map((form,index)=>index===editing?{...form,values:{...form.values,[column.key]:event.target.value}}:form))}/></label>)}<button onClick={()=>setEditing(null)}>Terminar revisión</button></div>:<div className="admin-excel-table"><table><thead><tr><th>Formulario / filas Excel</th><th>Destino</th><th>Detalles</th><th>Revisión</th><th>Acciones</th></tr></thead><tbody>{forms.map((form,index)=><tr key={form.number}><td data-label="Formulario / filas"><strong>{form.group}</strong><small>{form.sourceRows.join(', ')}</small></td><td data-label="Destino">{kind==='internet'?form.values.agencyCode:prepareAdministrativeForm(kind,form.values,form.rows,form.phones,department,session).target||'Sin completar'}</td><td data-label="Detalles">{form.rows.length||form.phones.length||1}</td><td data-label="Revisión" className={errors[index]?'invalid':''}>{errors[index]||'Listo para importar'}</td><td data-label="Acciones"><button disabled={busy||attempted.includes(form.number!)} onClick={()=>setEditing(index)}><Pencil/> Revisar / editar</button><button disabled={busy||attempted.includes(form.number!)} aria-label={`Quitar ${form.group}`} onClick={()=>setForms(current=>current.filter((_,i)=>i!==index))}><Trash2/></button></td></tr>)}</tbody></table></div>}
    {attempted.length>0&&forms.length>0&&<p>Los formularios cuyo envío ya se intentó conservan sus datos para comprobar el guardado al reintentar. Una vez guardados, podrás editarlos desde el listado.</p>}
    <footer><button disabled={busy} onClick={()=>{if(saved)void onSaved();else onClose()}}>{saved?'Volver al listado':'Volver'}</button>{busy&&inFlight.current?<button onClick={()=>{stop.current=true;setProgress('Se detendrá al terminar el formulario en curso.')}}>Detener después de este formulario</button>:<button className="primary" disabled={busy||editing!==null||!forms.length||errors.some(Boolean)} onClick={()=>void submit()}>Importar formularios ({forms.length})</button>}</footer>
  </section></div>;
}
