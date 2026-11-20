import {readFileSync} from 'node:fs';
import {renderToStaticMarkup} from 'react-dom/server';
import {describe,expect,it} from 'vitest';
import IncidentTicketDecisions from '../src/components/vite/IncidentTicketDecisions';
import type {DocumentDetail} from '../src/components/vite/DocumentSystemModule';
import {prepareAdministrativeForm} from '../src/lib/administrative-form-data';

const item=(rows:Record<string,unknown>[],canDecide=true):DocumentDetail=>({id:44,title:'Informe',originalFileName:'informe.pdf',contentType:'application/pdf',size:1200,ownerUserName:'supervisor',ownerDisplayName:'Supervisora',department:'Supervisión',recipientDepartment:'Administración',status:'received',createdUtc:'2026-11-12T12:00:00Z',receivedUtc:'2026-11-12T12:05:00Z',isOwner:false,isRecipient:true,canEdit:false,canMove:false,sharedWith:[],channel:'requirements',fileUrl:'/preview',canDecideIncidentTickets:canDecide,administrativeRequest:{number:'SOL-ADM-QA',assetType:'OTRO',targetType:'AGENCIA',targetName:'Grupo QA',agencyCode:'001',formType:'INCIDENT_REPORT',formData:{rows}}});

describe('decisiones individuales del informe de incidentes',()=>{
 it('muestra Aprobar y Rechazar al lado de cada ticket pendiente',()=>{
  const html=renderToStaticMarkup(<IncidentTicketDecisions item={item([{agencyId:'001',ticketCode:'D488',agentPhone:'Ana'},{agencyId:'001',ticketCode:'OBB8',agentPhone:'Luisa'}])} onChanged={async()=>{}}/>);
  expect(html.match(/Aprobar/g)).toHaveLength(2);
  expect(html.match(/Rechazar/g)).toHaveLength(2);
  expect(html).toContain('D488');expect(html).toContain('OBB8');
 });

 it('muestra los controles a Lucianny aunque el indicador histórico del documento llegue desactivado',()=>{
  const html=renderToStaticMarkup(<IncidentTicketDecisions item={item([{ticketCode:'D488',agentPhone:'Ana'}],false)} operatorAccess onChanged={async()=>{}}/>);
  expect(html).toContain('Aprobar');expect(html).toContain('Rechazar');
 });

 it('muestra resultado, monto y responsable sin opciones a lectores',()=>{
  const html=renderToStaticMarkup(<IncidentTicketDecisions item={item([{agencyId:'001',ticketCode:'D488',agentPhone:'Ana',decisionStatus:'APPROVED',approvedAmount:1250.5,decisionBy:'Yessica Mercedes'},{agencyId:'001',ticketCode:'OBB8',agentPhone:'Luisa',decisionStatus:'REJECTED',decisionNote:'No corresponde',decisionBy:'Yessica Mercedes'}],false)} onChanged={async()=>{}}/>);
  expect(html).toContain('Aprobado');expect(html).toContain('Rechazado');expect(html).toContain('Yessica Mercedes');expect(html).toContain('1,250.50');expect(html).not.toContain('>Aprobar<');
 });

 it('usa el tema oscuro institucional con texto blanco',()=>{
  const css=readFileSync('src/components/vite/institutionalDocuments.css','utf8');
  expect(css).toContain('.incident-ticket-decisions{border-color:#365f78;background:linear-gradient(145deg,#102e46,#0b2033);color:#fff');
  expect(css).toContain('.incident-ticket-list article>div:first-child strong,.incident-ticket-result>b,.incident-ticket-result p{color:#fff}');
  expect(css).toContain('.incident-ticket-options .danger{border-color:#d37787;background:#87475a;color:#fff}');
 });

 it('incluye todas las decisiones en el PDF actualizado',()=>{
  const rows=[{agencyId:'001',ticketDate:'2026-11-11',ticketCode:'D488',agentPhone:'Ana',decisionStatus:'APPROVED',approvedAmount:'1250.50',decisionBy:'Yessica Mercedes',decisionUtc:'2026-11-12T15:00:00Z',decisionNote:'Pago autorizado'},{agencyId:'001',ticketDate:'2026-11-12',ticketCode:'OBB8',agentPhone:'Luisa',decisionStatus:'REJECTED',decisionBy:'Yessica Mercedes',decisionNote:'No corresponde'}];
  const prepared=prepareAdministrativeForm('incident_report',{reportDate:'2026-11-12',preparedBy:'Supervisora',incidentDepartment:'Supervisión',subject:'Incidentes',groupName:'Grupo QA',incidentAgencyName:'001 · Agencia QA'},rows,[],'Supervisión',{displayName:'Supervisora'});
  expect(prepared.tables[0].headers).toEqual(['N.º','Fecha','Ticket','Contacto','Resultado','Monto de compensación RD$','Decidido por / detalle']);
  expect(prepared.tables[0].rows[0]).toContain('APROBADO');expect(prepared.tables[0].rows[0]).toContain('1,250.50');expect(prepared.tables[0].rows[1]).toContain('RECHAZADO');
 });

 it('restringe la decisión en servidor a Yessica, Lucianny y al administrador principal',()=>{
  const store=readFileSync('api/InstitutionalDocumentStore.cs','utf8'),profiles=readFileSync('api/AdministrativeProfiles.cs','utf8'),endpoints=readFileSync('api/InstitutionalDocumentEndpoints.cs','utf8'),permissions=readFileSync('api/OperationalPermissions.cs','utf8'),recovery=readFileSync('api/IncidentOperatorPermissionRecovery.cs','utf8'),database=readFileSync('api/DatabaseSqlServerScoped.cs','utf8'),list=readFileSync('src/components/vite/AdministrativeRequests.tsx','utf8'),detail=readFileSync('src/components/vite/InstitutionalDocumentDetail.tsx','utf8'),compiledProfiles=readFileSync('.codex/decompiled-v495/AdministrativeProfiles.cs','utf8'),compiledStore=readFileSync('.codex/decompiled-v495/Database.cs','utf8'),compiledPermissions=readFileSync('.codex/decompiled-v495/OperationalPermissions.cs','utf8');
  expect(store).toContain('CanDecideIncidentTickets');expect(profiles).toContain('"yesica.mercedes" or "yessica.mercedes"');
  expect(profiles).toContain('"GROUP_ADMINISTRATOR"');expect(profiles).toContain('or "lucianny.gonzalez" or "lucianny.gonzales"');expect(profiles).toContain('CanManageIncidentReports');expect(store).toContain('Los supervisores no pueden autorizar.');
  expect(store).toContain('Indica un monto de compensación mayor que cero.');expect(store).toContain('incident-ticket-approved');expect(store).toContain('incident-ticket-rejected');
  expect(endpoints).toContain('/{id:long}/incident-tickets/decision');expect(endpoints).toContain('"INCIDENT_REPORT"');
  expect(permissions).toContain('IncidentDocumentPermissions=["canViewDocuments","canReceiveDocuments","canSignDocuments","canDecideDocuments"]');
  expect(permissions).toContain('if(IncidentOperator(login))foreach(var key in IncidentDocumentPermissions)effective[key]=true');
  expect(permissions).toContain('if(IncidentOperator(user.Identity.Name??"",user.FindFirstValue("display_name"))&&IncidentDocumentPermissions.Contains(key))return true');
  expect(permissions).toContain('if(path.EndsWith("/incident-tickets/decision"))return "canDecideDocuments"');
  for(const permission of ['canViewDocuments','canReceiveDocuments','canSignDocuments','canDecideDocuments'])expect(recovery).toContain(permission);
  expect(recovery).toContain("IN('yesica.mercedes','yessica.mercedes','lucianny.gonzalez','lucianny.gonzales')");expect(recovery).toContain('display_name');
  expect(database).toContain('await EnsureIncidentOperatorPermissions(connection,ct)');
  expect(list).toContain('incidentOperatorAccess={incidentAccount}');
  expect(detail).toContain("const canOperateIncident=item.administrativeRequest?.formType==='INCIDENT_REPORT'&&(!!item.canDecideIncidentTickets||incidentOperatorAccess)");
  expect(store).toContain('(!d.IsRecipient&&!incidentAdministrator)');
  expect(compiledProfiles).toContain('CanManageIncidentReports');expect(compiledProfiles).toContain('"lucianny.gonzales"');
  expect(compiledStore).toContain('AdministrativeProfiles.CanManageIncidentReports(a)');expect(compiledStore).toContain('Solo Lucianny, Yessica o el administrador principal');
  expect(compiledPermissions).toContain('IncidentDocumentPermissions');expect(compiledPermissions).toContain('incident-tickets/decision');
 });

 it('da al administrador principal una bandeja global con formularios de supervisores',()=>{
  const app=readFileSync('src/App.tsx','utf8'),store=readFileSync('api/InstitutionalDocumentStore.cs','utf8');
  expect(app).toContain('title="Todos los formularios administrativos"');
  expect(app).toContain('incidentOnly={!isAdministrator}');
  expect(store).toContain('cmd.Parameters.AddWithValue("@manager",a.Manager)');
  expect(store).toContain('a.Department=="Administración"||SameUser(OwnerUserName,a.Id)');
 });

 it('da a Lucianny y Yessica la misma bandeja global de informes de supervisores',()=>{
  const portal=readFileSync('src/components/vite/AdministrativePortal.tsx','utf8'),list=readFileSync('src/components/vite/AdministrativeRequests.tsx','utf8'),store=readFileSync('api/InstitutionalDocumentStore.cs','utf8');
  expect(portal).toContain('const incidentOperator=isIncidentOperatorAccount(session.email,session.displayName)');
  expect(portal).not.toContain("incidentOperator=session.role==='Administrative'");
  expect(portal).toContain('incidentOnly={incidentOperator}');
  expect(portal).toContain('Consulta todos los informes enviados por los supervisores');
  expect(list).toContain("const effectiveIncidentOnly=incidentOnly||incidentAccount");
  expect(list).toContain('channel=all&view=all&administrative=1&summary=1');
  expect(list).toContain("effectiveIncidentOnly?row.administrativeRequest?.formType==='INCIDENT_REPORT'");
  expect(store).toContain('(@incidentReader=1 AND JSON_VALUE(metadata');
  expect(store).toContain('cmd.Parameters.AddWithValue("@incidentReader",AdministrativeProfiles.CanReadIncidentReports(a))');
 });

 it('reserva los sellos en la primera hoja y deja las continuaciones solo para detalles',()=>{
  const pdf=readFileSync('src/components/vite/administrativeFormPdf.ts','utf8'),layout=readFileSync('src/components/vite/administrativeSealLayout.ts','utf8'),save=readFileSync('src/lib/administrative-form-save.ts','utf8'),receipt=readFileSync('src/components/vite/institutionalReceiptPdf.ts','utf8'),detail=readFileSync('src/components/vite/InstitutionalDocumentDetail.tsx','utf8');
  expect(pdf).toContain('detailsOnlyAfterFirstPage?:boolean');
  expect(pdf).toContain('if(!input.detailsOnlyAfterFirstPage)');
  expect(layout).toContain('All three spaces are on page one');
  expect(layout).toContain('page!==1');
  expect(save).toContain("detailsOnlyAfterFirstPage:kind==='incident_report'");
  expect(save).toContain("hideCustodySpaces:kind==='incident_report'");
  expect(receipt).toContain('detailsOnlyAfterFirstPage:true,hideCustodySpaces:true');
  expect(detail).toContain('detailsOnlyAfterFirstPage:true,hideCustodySpaces:true');
 });

 it('carga la bandeja administrativa con un resumen rápido y conserva los detalles bajo demanda',()=>{
  const list=readFileSync('src/components/vite/AdministrativeRequests.tsx','utf8'),endpoints=readFileSync('api/InstitutionalDocumentEndpoints.cs','utf8'),store=readFileSync('api/InstitutionalDocumentStore.cs','utf8');
  expect(list).toContain('&summary=1');
  expect(list).toContain('&administrative=1&summary=1');
  expect(list).toContain('administrativeRequestsCache');
  expect(list).toContain("globalAdministrator=session.role==='Administrator'");
  expect(list).toContain('manager?Boolean(row.administrativeRequest)');
  expect(list).toContain("loadingWithoutData?'—'");
  expect(endpoints).toContain('hydrate:!summary');
  expect(endpoints).toContain('d.ForListSummary()');
  expect(store).toContain('ReceiptSignature=null;');
  expect(store).toContain('History=[];');
  expect(store).toContain('ORDER BY id DESC OPTION(RECOMPILE)');
  expect(store).toContain("JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL");
  expect(list).toContain('`/api/institutional-documents/${item.id}`');
 });

 it('mantiene la sección de incidentes del supervisor en el tema institucional y con desplazamiento completo',()=>{
  const list=readFileSync('src/components/vite/AdministrativeRequests.tsx','utf8'),css=readFileSync('src/components/vite/administrativeRequests.css','utf8');
  expect(list).toContain("effectiveIncidentOnly&&session.role==='GroupAdministrator'");
  expect(list).toContain('supervisor-incident-view');
  expect(css).toContain('.supervisor-incident-view{width:100%;max-width:100%;overflow:visible');
  expect(css).toContain('.supervisor-incident-view .warehouse-pdf-list{max-height:none!important;overflow:visible!important');
  expect(css).toContain('.incident-report-form{max-height:calc(100dvh - 24px)!important;overflow-x:hidden!important;overflow-y:auto!important');
  expect(css).toContain('.incident-report-form>header{position:sticky');
  expect(css).toContain('.incident-report-form>footer{position:sticky');
 });

 it('mantiene oscuro el catálogo de formularios desde cualquier sección',()=>{
  const css=readFileSync('src/components/vite/administrativeForms.css','utf8');
  expect(css).toContain('.adw-dialog-backdrop>.adw-dialog.admin-request-form.admin-form-catalog');
  expect(css).toContain('background:var(--ui-navy-900,#081b2d)!important');
  expect(css).toContain('.adw-dialog-backdrop>.adw-dialog.admin-form-catalog .admin-form-cards>button');
  expect(css).toContain('.admin-form-cards strong{color:var(--ui-text,#f3f9fc)!important}');
  expect(css).toContain('.admin-form-cards small{color:var(--ui-muted,#94b4c7)!important}');
 });
});
