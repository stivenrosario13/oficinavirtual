import { reportOperationalError } from "./operational-error-report";
type Snapshot = { status: number; statusText: string; headers: Headers; text: string };
type ClientOptions = { readTimeoutMs?: number; writeTimeoutMs?: number; maxReads?: number; retryDelayMs?: number };

export class ApiTimeoutError extends Error {
  readonly code = 'REQUEST_TIMEOUT';
  constructor(write: boolean) {
    super(write
      ? 'El servidor no confirmó la operación a tiempo. Actualiza para comprobar si se guardó antes de volver a enviarla.'
      : 'La consulta tardó demasiado. Puedes volver a intentarlo; los datos anteriores se conservan.');
    this.name = 'TimeoutError';
  }
}

/** Finite API requests. One transient read retry within the original deadline; writes are never replayed. */
export function createApiFetch(nativeFetch: typeof fetch, options: ClientOptions = {}): typeof fetch {
  const pending = new Map<string, Promise<Snapshot | Response>>();
  const waiting: Array<() => void> = [];
  let activeReads = 0;
  let generation = 0;
  const maxReads = options.maxReads ?? 6;

  async function execute(input: RequestInfo | URL, init: RequestInit | undefined, read: boolean): Promise<Snapshot | Response> {
    const requestUrl = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const requestPath = new URL(requestUrl, typeof location === 'undefined' ? 'http://localhost' : location.origin).pathname;
    const requestMethod = (init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
    const controller = new AbortController();
    const callerSignal = init?.signal ?? (input instanceof Request ? input.signal : undefined);
    let release: (() => void) | undefined;
    let enter: (() => void) | undefined;
    let finished = false;
    let streaming = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    let rejectDeadline!: (reason: unknown) => void;
    const deadline = new Promise<never>((_, reject) => { rejectDeadline = reject; });
    // Cancellation can happen before the first race is attached (already-aborted writes).
    void deadline.catch(() => {});
    const cleanup = () => {
      if (finished) return;
      finished = true;
      clearTimeout(timer);
      clearTimeout(retryTimer);
      callerSignal?.removeEventListener('abort', abort);
      if (enter) { const at = waiting.indexOf(enter); if (at !== -1) waiting.splice(at, 1); }
      release?.();
    };
    const fail = (reason: unknown) => { controller.abort(reason); rejectDeadline(reason); cleanup(); };
    const abort = () => fail(callerSignal?.reason ?? new DOMException('Aborted', 'AbortError'));
    const timer = setTimeout(() => fail(new ApiTimeoutError(!read)), read ? options.readTimeoutMs ?? 20000 : options.writeTimeoutMs ?? 120000);
    callerSignal?.addEventListener('abort', abort, { once: true });
    try {
      if (callerSignal?.aborted) abort();
      if (read) {
        await Promise.race([new Promise<void>(resolve => {
          enter = () => {
            if (finished) return;
            activeReads++;
            release = () => { activeReads--; waiting.shift()?.(); };
            resolve();
          };
          if (activeReads < maxReads) enter(); else waiting.push(enter);
        }), deadline]);
      }
      if (controller.signal.aborted) throw controller.signal.reason;
      const pauseBeforeRetry = async () => {
        await Promise.race([new Promise<void>(resolve => { retryTimer = setTimeout(resolve, options.retryDelayMs ?? 500 + Math.random() * 250); }), deadline]);
        if (controller.signal.aborted) throw controller.signal.reason;
      };
      let response!: Response;
      for (let attempt = 0; attempt < 2; attempt++) {
        try { response = await Promise.race([nativeFetch(input, { ...init, signal: controller.signal }), deadline]); }
        catch (error) {
          if (!read || attempt || controller.signal.aborted || !(error instanceof TypeError)) {
            if (!controller.signal.aborted) reportOperationalError({action:"API_ERROR",reference:`${requestMethod} ${requestPath}`,detail:error instanceof Error?error.message:"No se pudo conectar con el servicio."});
            throw error;
          }
          await pauseBeforeRetry(); continue;
        }
        // Do not retry permissions, validation, SQL/application failures, or rate limits.
        // Respect Retry-After instead of immediately resending an unavailable service request.
        if (read && !attempt && [408,502,503,504].includes(response.status) && !response.headers.has('retry-after')) {
          void response.body?.cancel().catch(() => {});
          await pauseBeforeRetry(); continue;
        }
        if (response.status >= 400 && response.status !== 401) reportOperationalError({action:"API_ERROR",reference:`${requestMethod} ${requestPath}`,detail:`El servidor respondió HTTP ${response.status} al realizar la operación.`});
        break;
      }
      if (response.headers?.get('content-type')?.includes('json')) {
        const text = await Promise.race([response.text(), deadline]);
        return { status: response.status, statusText: response.statusText, headers: response.headers, text };
      }
      // Non-JSON downloads retain streaming instead of duplicating large files.
      // Keep the deadline through their body consumption, including non-JSON errors.
      if (response.body) {
        streaming = true;
        const reader = response.body.getReader();
        const stream = new ReadableStream<Uint8Array>({
          async pull(target) {
            try {
              const next = await Promise.race([reader.read(), deadline]);
              if (next.done) { target.close(); cleanup(); }
              else target.enqueue(next.value);
            } catch (error) { target.error(error); void reader.cancel().catch(() => {}); cleanup(); }
          },
          cancel(reason) { cleanup(); return reader.cancel(reason); },
        });
        // Ownership of cleanup moves to the stream; the deadline also releases an unread body.
        return new Response(stream, { status: response.status, statusText: response.statusText, headers: response.headers });
      }
      return response;
    } finally {
      // JSON is complete here. Streaming bodies call cleanup from pull/cancel/deadline.
      if (!streaming) cleanup();
    }
  }

  return async (input, init) => {
    const rawUrl = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const url = new URL(rawUrl, typeof location === 'undefined' ? 'http://localhost' : location.origin);
    const sameOrigin = typeof location === 'undefined' ? rawUrl.startsWith('/') : url.origin === location.origin;
    if (!sameOrigin || !url.pathname.startsWith('/api/') || url.pathname === '/api/live/stream') return nativeFetch(input, init);
    const method = (init?.method ?? (input instanceof Request ? input.method : 'GET')).toUpperCase();
    const read = method === 'GET' || method === 'HEAD';
    if (!read) { generation++; pending.clear(); }
    // Requests with an explicit signal retain their own cancellation lifetime.
    // Binary routes are not shared because their streams belong to one consumer.
    const share = method === 'GET' && !init?.signal && !(input instanceof Request) &&
      !/\/(?:image|images|photo|photos|evidence|file|download|export)(?:\/|$)/i.test(url.pathname);
    const headers = new Headers(init?.headers);
    const key = JSON.stringify([generation, url.href, init?.credentials, init?.cache, Array.from(headers.entries())]);
    let work = share ? pending.get(key) : undefined;
    const joined = !!work;
    if (!work) {
      work = execute(input, init, read);
      if (share) {
        pending.set(key, work);
        const clear = () => { if (pending.get(key) === work) pending.delete(key); };
        void work.then(clear, clear);
      }
    }
    let result = await work;
    // Unexpected binary endpoints must not tee a stream into an unread clone:
    // that would retain the entire download in memory. Give each caller its own stream.
    if (joined && typeof result.text !== 'string') result = await execute(input, init, read);
    if (typeof result.text === 'string') return new Response(result.text, { status: result.status, statusText: result.statusText, headers: result.headers });
    return result as Response;
  };
}

// Resolve native fetch at call time so tests and browser instrumentation still work.
export const apiFetch = createApiFetch((input, init) => globalThis.fetch(input, init));
