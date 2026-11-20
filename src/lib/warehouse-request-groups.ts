import { apiFetch as fetch } from "@/lib/api-fetch";
export function normalizeWarehouseGroups(value: unknown): string[] {
  if (!Array.isArray(value)) throw new Error('El servidor no devolvió el listado de grupos.');
  return Array.from(new Set(value.filter((group): group is string => typeof group === 'string').map(group => group.trim()).filter(Boolean))).sort((a,b)=>a.localeCompare(b,'es'));
}

export async function loadWarehouseGroups(signal?: AbortSignal): Promise<string[]> {
  const response=await fetch('/api/institutional-documents/warehouse-groups',{credentials:'same-origin',cache:'no-store',signal});
  const body=await response.json().catch(()=>null);
  if(!response.ok)throw new Error(body?.message||body?.error||'No se pudieron cargar los grupos. Pulsa Reintentar.');
  return normalizeWarehouseGroups(body?.groups);
}
