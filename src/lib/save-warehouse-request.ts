import { apiFetch as fetch } from "@/lib/api-fetch";
type Confirmation = { id: number; warehouseRequest?: { number?: string } };
type ApiPayload = Partial<Confirmation> & {
  code?: unknown;
  message?: unknown;
  error?: unknown;
};
const payloadText = (payload: ApiPayload | null, key: 'message' | 'error') =>
  typeof payload?.[key] === 'string' ? payload[key] : '';
const valid = (value: unknown): value is Confirmation => {
  if (!value || typeof value !== 'object') return false;
  const id = (value as Confirmation).id;
  return Number.isSafeInteger(id) && id > 0;
};

// A missing POST response is not proof that the transaction failed. Recover
// only the authenticated owner's exact request; never automatically POST twice.
export async function saveWarehouseRequest(body: FormData, number: string): Promise<Confirmation> {
  let response: Response | undefined;
  let payload: ApiPayload | null = null;
  try {
    response = await fetch('/api/institutional-documents', {
      method: 'POST', credentials: 'same-origin', body,
      headers: { Accept: 'application/json' },
    });
    payload = await response.json().catch(() => null) as ApiPayload | null;
  } catch { /* The server may already have committed the request. */ }
  if (response?.ok && valid(payload)) return payload;
  const diagnostic = response ? ` (HTTP ${response.status}${typeof payload?.code === 'string' ? ` · ${payload.code}` : ''})` : '';
  if (response && response.status >= 400 && response.status < 500) {
    throw new Error((payloadText(payload, 'message') || payloadText(payload, 'error') ||
      (response.status === 401 ? 'Tu sesión venció. Inicia sesión nuevamente.' : 'No fue posible enviar la solicitud.')) + diagnostic);
  }
  try {
    const lookup = await fetch(`/api/institutional-documents/warehouse-submission/${encodeURIComponent(number)}`, {
      credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' },
    });
    const saved: unknown = lookup.ok ? await lookup.json() : null;
    if (valid(saved) && saved.warehouseRequest?.number === number) return saved;
  } catch { /* Preserve the original operation's uncertainty. */ }
  throw new Error((payloadText(payload, 'message') || payloadText(payload, 'error') ||
    `No se pudo comprobar el envío de ${number}. Conserva este formulario y vuelve a pulsar Enviar; se comprobará el mismo número sin duplicar la solicitud.`) + diagnostic);
}
