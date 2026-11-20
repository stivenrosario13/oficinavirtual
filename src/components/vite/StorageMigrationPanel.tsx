import { apiFetch as fetch } from "@/lib/api-fetch";
import { useCallback, useEffect, useRef, useState } from 'react';
import './storageDashboard.css';

type StorageStatus = {
  enabled: boolean; configured: boolean;
  state: 'idle' | 'queued' | 'running' | 'waiting' | 'complete' | 'partial' | 'failed' | 'interrupted';
  message: string;
  release?: { released: number; bytesFreed: number; pending: number; changed: number; message?: string };
  report?: { candidates: number; copied: number; existing: number; queued?: number; failed: number; originalsPreserved: boolean; details: unknown[] } | null;
};
type Breakdown = {key: string; images: number; pdfs: number; bytes: number; pending: number; verified: number; review: number};
type Analytics = {byOrigin: Breakdown[]; byGroup: Breakdown[]; byModule: Breakdown[]; providerCheckedAt?: string; provider?: {files?: {ready?: number; processing?: number; rejected?: number}; bytes?: {logical?: number; physical?: number}}};
type ProviderUsage = {provider?: {files?: {ready?: number; processing?: number; rejected?: number; deleted_in_retention?: number}; by_type?: {image?: {count?: number}; pdf?: {count?: number}}; bytes?: {logical?: number}; last_upload_at?: string} | null; checkedAt?: string | null; error?: string | null; nextCheckAt?: string | null};
type DocumentUsage = {rows: (Omit<Breakdown,'key'> & {category:string;profile:string;inSql:number})[] | null; checkedAt:string|null;error:string|null};
const documentNames:Record<string,string>={'warehouse-request':'Solicitudes de almacén','administrative-form':'Formularios administrativos','template-form':'Formularios de plantillas','document-form':'Otros formularios y documentos'};
const names: Record<string,string> = {legacy:'Base de datos anterior',new:'Archivos nuevos',unknown:'Sin clasificar',supervisors:'Supervisores',technicians:'Técnicos',support:'Soportes',administration:'Administración principal',other:'Otros perfiles','embedded-image':'Firmas e imágenes incrustadas',photo:'Fotos y perfiles','ticket-evidence':'Evidencias de soporte','finding-evidence':'Resoluciones de hallazgos','transition-evidence':'Agencias en transición','institutional-document':'Documentos institucionales',template:'Plantillas', 'legacy-audit_photos':'Fotos de auditorías (anteriores)','legacy-admin_users':'Perfiles (anteriores)','legacy-institutional_files':'Documentos institucionales (anteriores)','legacy-support_ticket_evidence':'Evidencias de soporte (anteriores)'};
const size = (bytes: number) => bytes>0&&bytes<1024*1024 ? `${(bytes/1024).toLocaleString('es-DO',{maximumFractionDigits:2})} KB` : `${(bytes / 1024 / 1024).toLocaleString('es-DO',{maximumFractionDigits:2})} MB`;
function BreakdownTable({title, rows}: {title: string; rows: Breakdown[]}) {
  return <section className="storage-breakdown"><h2>{title}</h2><div className="storage-table-scroll"><table><thead><tr><th>Procedencia</th><th>Imágenes</th><th>PDF</th><th>Tamaño original</th><th>Pendientes</th><th>Verificados</th><th>Revisión</th></tr></thead><tbody>{rows.map(row=><tr key={row.key}><th scope="row">{names[row.key]??row.key}</th><td>{row.images}</td><td>{row.pdfs}</td><td>{size(row.bytes)}</td><td>{row.pending}</td><td>{row.verified}</td><td>{row.review}</td></tr>)}</tbody></table></div>{!rows.length&&<p>Todavía no hay archivos registrados en esta cola.</p>}</section>;
}
const labels: Record<StorageStatus['state'], string> = {
  idle: 'Copia pendiente', queued: 'Preparando copia', running: 'Preparando cola', waiting: 'Copia en cola de espera',
  complete: 'Copia completada', partial: 'Copia con archivos pendientes', failed: 'No se completó la copia', interrupted: 'Copia interrumpida',
};
type Verification = {
  state: 'unknown' | 'queued' | 'running' | 'passed' | 'failed'; message: string;
  report?: { checkedAt: string; passed: boolean; checks: { name: string; passed: boolean; message: string; code?: string }[] } | null;
};
type QueueStatus = {
  pending: number; complete: number; review: number; stagedBytes: number;
  state: string; message: string; nextAttempt: string | null;
  verificationBlockedUntil?: string | null;
  dailyAttemptedBytes: number; dailyLimitBytes: number; uploadsPerMinute: number; maxPdfPages: number;
  issues: { id: string; kind: string; errorCode: string; message: string }[];
};

export default function StorageMigrationPanel() {
  const [status, setStatus] = useState<StorageStatus | null>(null);
  const [error, setError] = useState('');
  const [starting, setStarting] = useState(false);
  const [verification, setVerification] = useState<Verification | null>(null);
  const [verifying, setVerifying] = useState(false);
  const [queue, setQueue] = useState<QueueStatus | null>(null);
  const [resuming, setResuming] = useState(false);
  const [analytics, setAnalytics] = useState<Analytics | null>(null);
  const [usage, setUsage] = useState<ProviderUsage | null>(null);
  const [documents, setDocuments] = useState<DocumentUsage | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const refreshInFlight = useRef(false);
  const refresh = useCallback(async (signal?: AbortSignal) => {
    if(refreshInFlight.current)return;
    refreshInFlight.current=true;
    setRefreshing(true);
    try {
      const failures: string[] = [];
      const part = async <T,>(path: string, publish: (data: T) => void) => {
        try {
          const response = await fetch(path, {signal, cache: 'no-store'});
          const data = await response.json();
          if (!response.ok) throw new Error(data.error || 'No se pudo consultar el almacenamiento.');
          if (!signal?.aborted) publish(data);
        } catch (error) { if (!signal?.aborted) failures.push((error as Error).message); }
      };
      await Promise.all([
        part<StorageStatus>('/api/admin/storage/status', setStatus),
        part<Verification>('/api/admin/storage/verification', setVerification),
        part<QueueStatus>('/api/admin/storage/queue', setQueue),
        part<Analytics>('/api/admin/storage/analytics', setAnalytics),
        part<ProviderUsage>('/api/admin/storage/usage', setUsage),
        part<DocumentUsage>('/api/admin/storage/documents', setDocuments),
      ]);
      if (!signal?.aborted) { setError(failures.join(' · ')); setUpdatedAt(new Date().toISOString()); }
    } catch (e) {
      if (!signal?.aborted) setError(e instanceof Error ? e.message : 'No se pudo consultar el almacenamiento.');
    } finally {refreshInFlight.current=false;setRefreshing(false);}
  }, []);
  const busy = starting || status?.state === 'queued' || status?.state === 'running';
  const checking = verifying || verification?.state === 'queued' || verification?.state === 'running';
  const verificationWaiting=!!queue?.verificationBlockedUntil&&new Date(queue.verificationBlockedUntil).getTime()>Date.now();
  const refreshSignal = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    refreshSignal.current = controller;
    void refresh(controller.signal);
    return () => controller.abort();
  }, [refresh]);
  const refreshInterval = busy || checking || status?.state === 'waiting' || (queue?.pending ?? 0) > 0 ? 5000 : 30000;
  useEffect(() => {
    const timer = window.setInterval(() => { if (!document.hidden) void refresh(refreshSignal.current?.signal); }, refreshInterval);
    return () => window.clearInterval(timer);
  }, [refresh, refreshInterval]);
  async function resumeQueue() {
    setResuming(true); setError('');
    try {
      const response = await fetch('/api/admin/storage/queue/resume', { method: 'POST' });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No se pudo reanudar la cola.');
      setQueue(data);
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo reanudar la cola.'); }
    finally { setResuming(false); }
  }
  async function verify() {
    setVerifying(true); setError('');
    try {
      const response = await fetch('/api/admin/storage/verify', { method: 'POST' });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No se pudo iniciar la prueba.');
      setVerification(data);
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo iniciar la prueba.'); }
    finally { setVerifying(false); }
  }
  async function start() {
    setStarting(true); setError('');
    try {
      const response = await fetch('/api/admin/storage/migrate', { method: 'POST' });
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No se pudo iniciar la copia.');
      setStatus(data);
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo iniciar la copia.'); }
    finally { setStarting(false); }
  }
  function downloadReport() {
    const url = URL.createObjectURL(new Blob([JSON.stringify({ migration: status?.report, release: status?.release, queue, analytics, usage, documents }, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'informe-copia-archivos.json'; link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  return <section className="storage-migration-panel" aria-label="Almacenamiento">
    <header className="storage-heading"><div><p>ADMINISTRADOR PRINCIPAL · IMÁGENES Y PDF</p><h1>Almacenamiento</h1></div><span>{checking ? 'Prueba en curso…' : verification?.state === 'passed' ? 'Última prueba correcta' : verification?.state === 'failed' ? 'Requiere atención' : 'Funcionamiento sin verificar'}</span></header>
    <div className="storage-migration-content">
      <p>Las imágenes y los PDF nuevos se guardan en una cola privada y se transfieren automáticamente a la API. Los archivos anteriores se retiran de SQL únicamente después de verificar su descarga privada. Sus accesos siguen funcionando.</p>
      <div className="storage-migration-actions storage-refresh-actions"><button type="button" disabled={refreshing} onClick={()=>void refresh()}>{refreshing?'Actualizando estado…':'Actualizar estado'}</button><span role="status">{updatedAt?`Estado consultado: ${new Date(updatedAt).toLocaleString()}`:'Consultando estado…'}</span></div>
      <section className="storage-queue" aria-label="Archivos recibidos por la API">
        <h2>Archivos recibidos por la API</h2>
        <p>Inventario actual informado por el almacenamiento de esta aplicación; los pendientes locales aún no cuentan aquí.</p>
        <div className="storage-metrics">
          <article><span>Listos en la API</span><strong>{usage?.provider?.files?.ready ?? '—'}</strong><small>Disponibles según el proveedor</small></article>
          <article><span>Imágenes registradas</span><strong>{usage?.provider?.by_type?.image?.count ?? '—'}</strong><small>Conteo informado por la API</small></article>
          <article><span>PDF registrados</span><strong>{usage?.provider?.by_type?.pdf?.count ?? '—'}</strong><small>Conteo informado por la API</small></article>
        </div>
        {usage?.provider&&<><p>{usage.provider.files?.processing ?? '—'} en proceso · {usage.provider.files?.rejected ?? '—'} rechazados · {usage.provider.files?.deleted_in_retention ?? '—'} eliminados en retención.</p>{usage.provider.bytes?.logical!=null&&<p>Contenido almacenado: {size(usage.provider.bytes.logical)}</p>}{usage.provider.last_upload_at&&<p>Última subida recibida: {new Date(usage.provider.last_upload_at).toLocaleString()}</p>}</>}
        <p>Última lectura de la API: {usage?.checkedAt?new Date(usage.checkedAt).toLocaleString():'Sin lectura disponible'}. Las consultas se comparten durante 30 segundos para respetar sus límites. El inventario puede incluir los archivos de prueba.</p>
        {usage?.error&&<p role="alert">{usage.error}{usage.checkedAt?' Los contadores muestran la última lectura correcta.':''}{usage.nextCheckAt?` Próxima consulta: ${new Date(usage.nextCheckAt).toLocaleString()}.`:''}</p>}
      </section>
      <div className="storage-metrics">
        <article><span>Espacio liberado de los registros SQL</span><strong>{size(status?.release?.bytesFreed ?? 0)}</strong><small>{status?.release?.released ?? 0} registros sustituidos por referencias</small></article>
        <article><span>Archivos verificados en la cola</span><strong>{queue?.complete ?? 0}</strong><small>{queue?.pending ?? 0} pendientes · {queue?.review ?? 0} en revisión</small></article>
        <article><span>Temporales privados en el servidor</span><strong>{size(queue?.stagedBytes ?? 0)}</strong><small>Se retiran tras verificar la transferencia</small></article>
      </div>
      <p>El espacio liberado corresponde al contenido de los registros y SQL puede reutilizarlo. Monster puede seguir mostrando el tamaño asignado al archivo de la base. La capacidad de Monster y la cuota de la API son independientes.</p>
      {status?.release?.message&&<p role="alert">{status.release.message}</p>}
      {!!status?.release?.changed&&<p>{status.release.changed} versiones anteriores no sustituidas porque el registro cambió o ya no existe.</p>}
      {analytics&&<><BreakdownTable title="Origen de los archivos" rows={analytics.byOrigin}/><BreakdownTable title="Perfil que incorporó el archivo" rows={analytics.byGroup}/>
        <section className="storage-breakdown" aria-label="Solicitudes y formularios por perfil"><h2>Solicitudes de almacén y formularios por perfil</h2>
          <p>Detalle de los archivos guardados desde solicitudes y formularios. Ya están incluidos en los totales anteriores; no se suman de nuevo.</p>
          {documents?.error&&<p role="alert">{documents.error}</p>}
          <div className="storage-table-scroll"><table><thead><tr><th>Documento</th><th>Perfil que lo incorporó</th><th>Imágenes</th><th>PDF</th><th>Tamaño original</th><th>Pendientes</th><th>Verificados</th><th>Revisión</th><th>Con contenido en SQL</th></tr></thead><tbody>{documents?.rows?.map(row=><tr key={`${row.category}:${row.profile}`}><th scope="row">{documentNames[row.category]??row.category}</th><td>{names[row.profile]??row.profile}</td><td>{row.images}</td><td>{row.pdfs}</td><td>{size(row.bytes)}</td><td>{row.pending}</td><td>{row.verified}</td><td>{row.review}</td><td>{row.inSql}</td></tr>)}</tbody></table></div>
          {documents?.rows?.length===0&&<p>No hay imágenes o PDF archivados de solicitudes o formularios.</p>}
          <p>Perfil según el primer guardado en la cola; los anteriores sin atribución comprobable aparecen como «Sin clasificar». Cada archivo se cuenta una vez por tipo y perfil. Los formularios generados solo para descargar no forman parte de este inventario.</p>
          {documents?.checkedAt&&<p>Detalle consultado: {new Date(documents.checkedAt).toLocaleString()}</p>}
        </section>
        <p>Se cuentan archivos únicos de esta cola, según el perfil que los incorporó por primera vez. Los anteriores sin atribución comprobable figuran como “Sin clasificar”. El inventario aumenta mientras se prepara la migración.</p><details className="storage-module-details"><summary>Ver detalle por módulo</summary><BreakdownTable title="Módulos del sistema" rows={analytics.byModule}/></details>
      </>}
      {status && (!status.configured || !status.enabled) && <p role="alert">Falta activar el servicio de almacenamiento en el servidor.</p>}
      {queue && <section className="storage-queue" aria-label="Cola de transferencias">
        <p><strong>Cola de transferencias</strong></p>
        <p>{queue.pending} pendientes · {queue.complete} archivos verificados · {queue.review} requieren revisión.</p>
        <p role="status">{queue.message}</p>
        {queue.nextAttempt && <p>Próximo intento: <strong>{new Date(queue.nextAttempt).toLocaleString()}</strong>. La espera se conserva aunque reinicies el sitio.</p>}
        <p>Una transferencia a la vez · máximo {queue.uploadsPerMinute} subidas por minuto · presupuesto diario: {(queue.dailyAttemptedBytes / 1e9).toFixed(2)} / {(queue.dailyLimitBytes / 1e9).toFixed(2)} GB (UTC).</p>
        <p>Imágenes hasta 25 MB · PDF hasta 50 MB y {queue.maxPdfPages} páginas. El proveedor valida el contenido.</p>
        {queue.state === 'blocked' && <button type="button" className="storage-verify-button" disabled={resuming} onClick={() => void resumeQueue()}>{resuming ? 'Reanudando…' : 'Reanudar cola tras corregir configuración'}</button>}
        {queue.issues.length > 0 && <ul className="storage-checks">{queue.issues.map(issue => <li key={issue.id}><strong>{issue.errorCode}</strong><span>{issue.message}</span></li>)}</ul>}
      </section>}
      <p>La prueba sube una imagen y un PDF de ejemplo y verifica sus descargas privadas. Los mismos ejemplos se reutilizan al repetirla.</p>
      <p>Puedes probar de nuevo después de que el proveedor corrija la cuota, aunque la cola esté en pausa automática. Si la prueba es correcta, las transferencias se reanudan.</p>
      {verificationWaiting&&<p role="status">Para respetar los límites de subida, la próxima prueba estará disponible a partir de {new Date(queue!.verificationBlockedUntil!).toLocaleString()}.</p>}
      <button className="storage-verify-button" type="button" disabled={checking || !status || verificationWaiting} onClick={() => void verify()}>{checking ? 'Probando almacenamiento…' : 'Probar imagen y PDF'}</button>
      {verification && <p role={verification.state === 'failed' ? 'alert' : 'status'}>{verification.message}</p>}
      {verification?.report && <>
        <p>Última comprobación: {new Date(verification.report.checkedAt).toLocaleString()}</p>
        <ul className="storage-checks">{verification.report.checks.map(check => <li key={check.name}>
          <strong>{check.passed ? 'Correcto' : 'Pendiente'} · {check.name}</strong>
          {!check.passed && <span>{check.message}</span>}
        </li>)}</ul>
      </>}
      {status && <p><strong>{labels[status.state]}</strong></p>}
      {status?.state === 'running' && <p role="status">La copia continúa en segundo plano. Puedes seguir usando el sistema.</p>}
      {(status?.state === 'failed' || status?.state === 'partial') && <p role="alert">{status.message}</p>}
      {status?.state === 'waiting' && <p role="status">{status.message}</p>}
      {status?.report && <p role="status">{status.report.copied} registros copiados · {status.report.existing} ya copiados · {status.report.queued ?? 0} en cola · {status.report.failed} pendientes de revisión.</p>}
      {error && <p role="alert">{error}</p>}
      <div className="storage-migration-actions">
        <button type="button" disabled={busy || status?.state === 'waiting' || !status?.enabled} onClick={() => void start()}>{busy ? 'Preparando cola…' : status?.state === 'waiting' ? 'Copia en cola automática' : status?.state === 'idle' ? 'Encolar archivos existentes' : 'Revisar y continuar copia'}</button>
        <button type="button" disabled={refreshing} onClick={() => void refresh()}>{refreshing?'Actualizando estado…':'Actualizar estado'}</button>
        {status?.report && <button type="button" onClick={downloadReport}>Descargar informe</button>}
      </div>
    </div>
  </section>;
}
