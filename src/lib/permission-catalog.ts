import type {PortalSession} from './session';
import {operationalPermissions} from './operational-permissions';
type UserPermissionKey = keyof PortalSession["permissions"];
export const permissionCatalog: Array<{
  key: UserPermissionKey;
  title: string;
  description: string;
}> = [
  {key:"canManageAgencies",title:"Administrar agencias",description:"Edición e importación del directorio, sujeta al alcance autorizado del rol."},
  { key: "canViewFieldAudits", title: "Ver auditoría de campo", description: "Consulta las auditorías y evidencias existentes, sin crearlas, modificarlas ni eliminarlas." },
  { key: "canManageDepartment", title: "Gerente de área", description: "Consulta la actividad registrada de todo el personal de su área. No concede acceso global, eliminación ni administración de usuarios." },
  {
    key: "canViewDashboard",
    title: "Panel y evidencias",
    description: "Consulta estadísticas, grupos, agencias y auditorías.",
  },
  {
    key: "canAudit",
    title: "Realizar auditorías",
    description: "Registra formularios y evidencia georreferenciada.",
  },
  {
    key: "canExportAudits",
    title: "Descargar auditorías PDF",
    description: "Exporta expedientes y reportes de campo.",
  },
  {
    key: "canCreateTickets",
    title: "Crear tickets",
    description: "Reporta incidencias a los departamentos.",
  },
  {
    key: "canAssignTickets",
    title: "Asignar y compartir tickets",
    description:
      "Enruta casos a técnicos, equipos u otros departamentos de soporte.",
  },
  {
    key: "canViewSupportDashboard",
    title: "Tablero de soporte",
    description: "Entra a departamentos y consulta sus tickets.",
  },
  {
    key: "canResolveTickets",
    title: "Trabajar y resolver tickets",
    description: "Actualiza estado, prioridad, diagnóstico y solución.",
  },
  {
    key: "canViewSupportFindings",
    title: "Hallazgos automáticos",
    description: "Consulta y resuelve hallazgos de auditoría.",
  },
  {key:"canCancelFindingWithEvidence",title:"Anular hallazgos con evidencia",description:"Permite anular un hallazgo adjuntando imágenes."},
  {key:"canCancelFindingWithoutEvidence",title:"Anular hallazgos sin evidencia",description:"Permite anular sin imágenes; la acción queda auditada."},
  {key:"canCancelFindingWithReason",title:"Anular hallazgos con motivo",description:"Permite registrar una explicación al anular."},
  {key:"canCancelFindingWithoutReason",title:"Anular hallazgos sin motivo",description:"Permite anular sin explicación; úsalo solo para casos excepcionales."},
  {
    key: "canUseSupportChat",
    title: "Chat de soporte",
    description: "Accede a conversaciones y al asistente interno.",
  },
  {
    key: "canManageSupportChat",
    title: "Atender conversaciones CRM",
    description: "Toma, responde, resuelve y cierra conversaciones.",
  },
  {
    key: "canModerateSupportChat",
    title: "Moderar conversaciones",
    description:
      "Permite bloquear, restringir y silenciar conversaciones del CRM.",
  },
  {
    key: "canDeleteSupportConversations",
    title: "Eliminar conversaciones",
    description:
      "Oculta conversaciones con eliminación segura y trazabilidad administrativa.",
  },
  {
    key: "canShareSupportConversations",
    title: "Compartir conversaciones",
    description: "Comparte o copia un resumen autorizado de la conversación.",
  },
  {
    key: "canViewTicketNotifications",
    title: "Centro de notificaciones",
    description: "Recibe avisos y sonido de novedades de tickets.",
  },
  {
    key: "canConfigureSupport",
    title: "Configuración de soporte",
    description: "Edita departamentos, servicios y prioridades.",
  },
  {
    key: "canViewAllSupportDepartments",
    title: "Todos los departamentos",
    description: "Consulta procesos de todas las áreas de soporte.",
  },
  {
    key: "canDeleteSupportTickets",
    title: "Eliminar tickets",
    description: "Elimina tickets incorrectos con registro de auditoría.",
  },
  {
    key: "canAuthorizeWarehouseRequests",
    title: "Autorizar solicitudes de Almacén",
    description: "Autoriza requerimientos y asigna inventario para retiro.",
  },
  {
    key: "canLoadWarehouseInventory",
    title: "Cargar inventario de Almacén",
    description: "Agrega artículos e importa el inventario desde Excel.",
  },
  {
    key: "canManageUsers",
    title: "Usuarios y permisos",
    description: "Crea cuentas y asigna permisos individuales.",
  },
  {
    key: "canManageAudits",
    title: "Corregir y eliminar auditorías",
    description: "Modifica expedientes y reabre agencias auditadas.",
  },
  {
    key: "canConfigureProfile",
    title: "Perfil y contraseña",
    description: "Administra foto de perfil y credenciales propias.",
  },
];

const sectionFor=(key:string)=>key.includes('Warehouse')?'Almacén':key.includes('Finding')?'Hallazgos':key.includes('Chat')||key.includes('Conversations')?'Chat':key.includes('Ticket')||key.includes('Support')?'Tickets y soporte':key.includes('Audit')||key==='canViewDashboard'||key==='canManageAgencies'?'Auditoría y agencias':'Cuenta y administración';

/** Catálogo único de todos los permisos asignables de todas las secciones. */
export const allPermissionCatalog=[
  ...permissionCatalog.map(item=>({...item,section:sectionFor(item.key)})),
  ...operationalPermissions.map(([key,section,title])=>({key,section,title,description:''})),
];


