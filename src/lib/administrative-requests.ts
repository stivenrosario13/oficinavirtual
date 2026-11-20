import { apiFetch as fetch } from "@/lib/api-fetch";
const departmentNames: Record<string, string> = {
  WAREHOUSE: 'Almacén',
  ADMINISTRATIVE: 'Administración',
  TECHNOLOGY: 'Tecnología',
  GENERAL_SERVICES: 'Servicios Generales',
  HUMAN_RESOURCES: 'Recursos Humanos',
  SUPERVISION: 'Supervisión',WORKSHOP:"Taller",COMMUNICATION:"Comunicación",COMMERCIALIZATION:"Comercialización",TREASURY:"Tesorería",ACCOUNTING:"Contabilidad",GENERAL_AUDIT:"Auditoría General",VAULT:"Bóveda",CASHIER:"Caja",CALL_CENTER:"Call Center",TECHNICAL_FAILURE:"Avería Técnica",
};

export function administrativeDepartmentName(department?: string): string {
  return department ? departmentNames[department] || department : '';
}

export function belongsToAdministrativeRequests(
  row: { recipientDepartment?: string | null; department: string; isOwner?: boolean },
  department?: string,
): boolean {
  if(!department)return row.recipientDepartment === 'Administración';
  const name=administrativeDepartmentName(department);
  return row.recipientDepartment===name||(row.department===name&&row.isOwner===true);
}

export async function saveAdministrativeRequest(url: string, body: FormData, method: 'POST' | 'PUT' = 'POST'): Promise<void> {
  let response:Response;
  try{response = await fetch(url, { method, credentials: 'same-origin', body });}
  catch(error){throw Object.assign(error instanceof Error?error:new Error('No se pudo confirmar el guardado.'),{uncertain:true})}
  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    throw Object.assign(new Error(payload?.message || payload?.error || 'No fue posible enviar la solicitud.'), {status: response.status,uncertain:response.status>=500||response.status===409});
  }
  if (!payload || !Number.isSafeInteger(payload.id) || payload.id <= 0) {
    throw Object.assign(new Error('El servidor no confirmó el guardado. Actualiza el listado antes de volver a enviar la solicitud.'),{uncertain:true});
  }
}
