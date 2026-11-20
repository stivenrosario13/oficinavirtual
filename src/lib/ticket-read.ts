import { apiFetch as fetch } from './api-fetch';
// Reads may time out safely. Writes keep their existing confirmation semantics.
export async function readTicketResource<T>(path: string, fetcher: typeof fetch = fetch, timeoutMs = 20000, signal?: AbortSignal): Promise<T> {
  const controller = new AbortController();
  const cancel = () => controller.abort(signal?.reason);
  signal?.addEventListener('abort', cancel, {once:true});
  if(signal?.aborted)cancel();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetcher(path, {credentials: 'same-origin', cache: 'no-store', signal: controller.signal});
    const data = await response.json().catch(() => null);
    if (!response.ok) throw Object.assign(new Error(data?.error || data?.message || `El servidor respondió HTTP ${response.status}.`), {status: response.status});
    if(data === null)throw new Error('El servidor devolvió una respuesta no válida.');
    return data as T;
  } catch (error) {
    if (signal?.aborted) throw signal.reason;
    if (controller.signal.aborted) throw new Error('La consulta tardó demasiado. Pulsa Actualizar ahora para volver a consultar.');
    throw error;
  } finally { clearTimeout(timer); signal?.removeEventListener('abort',cancel); }
}

// Publish each successful directory independently; a slow/failed directory must
// never discard tickets or another directory that has already loaded.
export async function loadTicketResources(jobs: Array<() => Promise<unknown>>, failed: (error: unknown) => void) {
  await Promise.all(jobs.map(async job => { try { await job(); } catch (error) { failed(error); } }));
}
