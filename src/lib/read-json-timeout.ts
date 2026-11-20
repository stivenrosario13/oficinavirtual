import { apiFetch as fetch } from "@/lib/api-fetch";
// Keep the timer active until the response BODY has finished, not just headers.
export async function readJsonWithTimeout<T>(url: string, timeoutMs = 4000): Promise<T> {
  const { response, data } = await fetchJsonWithTimeout<T>(url, { cache: "no-store" }, timeoutMs);
  if (!response.ok) throw new Error("No se pudo consultar la dirección.");
  return data;
}

export async function fetchJsonWithTimeout<T>(url: string, init: RequestInit, timeoutMs: number): Promise<{ response: Response; data: T }> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(url, { ...init, signal: controller.signal });
    const data = await response.json() as T;
    return { response, data };
  } finally { clearTimeout(timer); }
}
