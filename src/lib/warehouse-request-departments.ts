// These are search choices, not permissions. Unknown historical departments
// remain selectable rather than disappearing from the request inbox.
export const warehouseRequestDepartments=[
 'Tecnología','Servicios Generales','Recursos Humanos','Administración',
 'Almacén','Taller','Comunicación','Comercialización','Tesorería','Contabilidad','Supervisión',
 'Avería Técnica','Call Center','Auditoría General','Bóveda','Caja',
];
export function requestDepartmentChoices(items:ReadonlyArray<{department?:string|null}>){
 return [...new Set([...warehouseRequestDepartments,...items.map(item=>typeof item.department==='string'?item.department.trim():'').filter(Boolean)])];
}
