import {Suspense} from 'react';
import {lazySection as lazy} from './LazySection';
import type {PortalSession} from '@/lib/session';
import {administrativeDepartmentName} from '@/lib/administrative-requests';
const AdministrativeRequests=lazy(()=>import('./AdministrativeRequests'));
const AdministrativeInventory=lazy(async()=>{const module=await import('./AdministrativeRequests');return {default:module.AdministrativeInventory};});
const TicketCenter=lazy(()=>import('./TicketCenter'));
const WarehousePdfRequests=lazy(()=>import('./WarehousePdfRequests'));
const InternalTicketRequest=lazy(()=>import('./InternalTicketRequest'));
const DepartmentOperationalAudit=lazy(()=>import('./DepartmentOperationalAudit'));
const AdministrativeFileManager=lazy(async()=>{const module=await import('./AdministrativeWorkspace');return {default:module.AdministrativeFileManager};});
import './maintenanceCommunications.css';
import './administrativeWorkspace.css';
import './administrativePortal.css';
import './administrativeFormsPolish.css';
import './departmentWorkspacePolish.css';
import type {AdministrativeSection} from './AdministrativeNavigation';
import {isIncidentOperatorAccount} from '@/lib/administrative-profiles';

export default function AdministrativePortal({session,section,departmentCode='ADMINISTRATIVE',onSessionExpired=()=>{}}:{session:PortalSession;departmentCode?:string;onSessionExpired?:()=>void;section?:AdministrativeSection;onSectionChange?:(section:AdministrativeSection)=>void}){
 const tab=section??'forms';
 const departmentName=administrativeDepartmentName(departmentCode);
 const manager=Boolean(session.permissions?.canManageDepartment);
 // Reuse the existing departmental support workspace; server scope still comes from authenticated claims.
 const supportSession:PortalSession={...session,role:'Department',assignedDepartment:departmentCode,supportDepartment:departmentCode,supportTeam:null};
 const supervisor=session.role==='GroupAdministrator';
 const administrativeLogin=session.email.trim().toLowerCase().replace('@grupotejeda.local','');
 const emma=session.role==='Administrative'&&administrativeLogin==='emma.mendoza';
 const incidentOperator=isIncidentOperatorAccount(session.email,session.displayName);
 if(supervisor)return <section className="mc-hub administrative-workspace administrative-portal"><WarehousePdfRequests session={session} department="SUPERVISION" warehouseAccess={false} embedded/></section>;
 const headings={inventory:['Inventario administrativo','Gestiona flotas, módems, disponibilidad y responsables de custodia.'],support:['Soporte','Crea tickets internos y solicita asistencia al departamento correspondiente.'],files:['Archivos y comunicaciones','Organiza documentos, consulta carpetas y comparte archivos con tu equipo.'],forms:['Formularios administrativos','Crea formularios y consulta el estado de cada solicitud.'],warehouse:['Solicitudes a Almacén','Solicita equipos y consulta su recepción, autorización y entrega.'],audit:['Auditoría operativa','Consulta los movimientos y el seguimiento del departamento.']} as const;
 return <section className="mc-hub administrative-workspace administrative-portal"><header className="mc-page-heading"><div><small>DEPARTAMENTO · {departmentName.toUpperCase()}</small><h2>{tab==='forms'&&incidentOperator?'Informes de incidentes':headings[tab][0]}</h2><p>{tab==='forms'&&incidentOperator?'Consulta todos los informes enviados por los supervisores, recíbelos y decide cada ticket.':headings[tab][1]}</p></div></header><div className="department-section-content" key={tab}>{tab==='support'?manager?<Suspense fallback={<p>Cargando soporte administrativo…</p>}><TicketCenter session={supportSession} onSessionExpired={onSessionExpired}/></Suspense>:<InternalTicketRequest session={session}/>:tab==='files'?<AdministrativeFileManager departmentCode={departmentCode} departmentName={departmentName}/>:tab==='audit'?(emma||manager)?<DepartmentOperationalAudit/>:<p role="status">Tu perfil no tiene acceso a la auditoría operativa.</p>:tab==='inventory'?manager&&session.permissions.canManageAdministrativeAssets?<AdministrativeInventory/>:<p role="status">Tu perfil no tiene acceso al inventario administrativo.</p>:tab==='forms'?<AdministrativeRequests session={session} department={departmentCode==='ADMINISTRATIVE'?undefined:departmentCode} incidentOnly={incidentOperator}/>:<WarehousePdfRequests session={session} department={departmentCode} warehouseAccess={false} embedded/>}</div></section>;
}
