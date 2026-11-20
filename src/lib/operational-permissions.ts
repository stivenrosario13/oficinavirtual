export const operationalPermissions = [
 ['canViewDocuments','Archivos y documentos','Consultar documentos'],
 ['canUploadDocuments','Archivos y documentos','Subir documentos y enviar comunicaciones'],
 ['canManageDocumentFolders','Archivos y documentos','Crear y organizar carpetas'],
 ['canShareDocuments','Archivos y documentos','Compartir documentos y carpetas'],
 ['canDeleteDocuments','Archivos y documentos','Eliminar documentos propios'],
 ['canReceiveDocuments','Archivos y documentos','Recibir documentos'],
 ['canSignDocuments','Archivos y documentos','Firmar documentos'],
 ['canDecideDocuments','Archivos y documentos','Aprobar o rechazar comunicaciones'],
 ['canManageDocumentTemplates','Archivos y documentos','Crear y editar plantillas'],
 ['canRequestWarehouse','Almacén','Solicitar equipos y materiales'],
 ['canViewWarehouseInventory','Almacén','Consultar inventario'],
 ['canCreateWarehouseArticles','Almacén','Crear artículos nuevos desde una entrada de inventario'],
 ['canDeleteWarehouseInventory','Almacén','Eliminar productos y existencias'],
 ['canPrepareWarehouseRequests','Almacén','Preparar y asignar equipos'],
 ['canWithdrawWarehouseRequests','Almacén','Registrar retiros por QR'],
 ['canCancelWarehouseRequests','Almacén','Cancelar solicitudes propias'],
 ['canViewWarehouseReports','Almacén','Consultar consumo y trazabilidad'],
 ['canViewWorkshop','Taller','Consultar equipos y reparaciones'],
 ['canOperateWorkshop','Taller','Recibir, reparar y entregar equipos'],
 ['canImportWorkshopInventory','Taller','Importar inventario fiscalizado'],
 ['canViewMaintenance','Mantenimiento','Consultar movimientos y comprobantes'],
 ['canCreateMaintenanceMovements','Mantenimiento','Registrar movimientos'],
 ['canManageProcurement','Mantenimiento','Gestionar compras y proveedores'],
 ['canViewAgencyTransitions','Procesos de agencias','Consultar procesos'],
 ['canManageAgencyTransitions','Procesos de agencias','Gestionar etapas y evidencias'],
 ['canManageAdministrativeAssets','Administración','Gestionar activos administrativos'],
] as const;
export const operationalPermissionKeys=operationalPermissions.map(([key])=>key);
export type OperationalPermissionKey=typeof operationalPermissions[number][0];

export function operationalDefaults(role:string,team?:string|null){
 const admin=role==='ADMINISTRATOR',warehouse=role==='WAREHOUSE'||team==='WAREHOUSE';
 const support=['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','DEPARTMENT'].includes(role);
 const documents=admin||warehouse||support||['ADMINISTRATIVE','GROUP_ADMIN'].includes(role);
 const maintenance=admin||warehouse||['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'].includes(role);
 const workshop=admin||role==='TECHNOLOGY'&&['WORKSHOP','TECHNICAL_FAILURE'].includes(team||'');
 return Object.fromEntries(operationalPermissions.map(([key])=>[key,
  key==='canRequestWarehouse'?documents:
  key==='canViewWarehouseInventory'||key==='canCancelWarehouseRequests'||key==='canViewWarehouseReports'?documents:
  key==='canCreateWarehouseArticles'?admin:
  key==='canDeleteWarehouseInventory'?false:
  key==='canPrepareWarehouseRequests'||key==='canWithdrawWarehouseRequests'?admin||warehouse:
  key==='canViewWorkshop'?maintenance:
  key==='canOperateWorkshop'||key==='canImportWorkshopInventory'?workshop:
  key==='canViewMaintenance'||key==='canCreateMaintenanceMovements'||key==='canManageProcurement'?maintenance:
  key==='canViewAgencyTransitions'||key==='canManageAgencyTransitions'?admin||['TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'].includes(role):
  key==='canManageAdministrativeAssets'?admin||role==='ADMINISTRATIVE'||role==='DEPARTMENT':documents
 ])) as Record<OperationalPermissionKey,boolean>;
}
