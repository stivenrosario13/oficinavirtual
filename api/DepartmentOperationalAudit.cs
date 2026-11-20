using Microsoft.Data.SqlClient;
using System.Text;

partial class Database
{
 public async Task<string> DepartmentOperationalAudit(string scope,DateTime from,DateTime to,int offset,CancellationToken ct)
 {
  await using var c=await Open(ct);
  var queries=new List<string>();
  async Task<bool> Exists(string table){await using var check=new SqlCommand("SELECT CASE WHEN OBJECT_ID(@table,N'U') IS NULL THEN 0 ELSE 1 END",c);check.Parameters.AddWithValue("@table","dbo."+table);return Convert.ToInt32(await check.ExecuteScalarAsync(ct))==1;}
  if(await Exists("maintenance_movements"))queries.Add("""
   SELECT CONCAT('movement:',m.id) id,m.created_at at,m.created_by_name actor,m.created_by_user_id login,m.operational_area module,m.movement_type action,m.document_number reference,CONCAT(m.equipment_type,N' · ',m.serial_number,N' · ',m.failure_cause,N' · ',m.notes) detail
   FROM dbo.maintenance_movements m WHERE @scope='ALL' OR (@scope='WAREHOUSE' AND m.operational_area='WAREHOUSE') OR (@scope IN('WORKSHOP','TECHNICAL_FAILURE') AND m.operational_area='WORKSHOP') OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND m.department=@scope)
   """);
  if(await Exists("maintenance_workshop_events"))queries.Add("""
   SELECT CONCAT('work:',e.id),e.occurred_at,e.actor_name,e.actor_login,'WORKSHOP',e.action,m.document_number,CONCAT(e.status,N' · ',e.note)
   FROM dbo.maintenance_workshop_events e JOIN dbo.maintenance_movements m ON m.id=e.source_id
   WHERE @scope IN('ALL','WORKSHOP','TECHNICAL_FAILURE') OR (@scope='WAREHOUSE' AND e.action IN('DELIVER','WAREHOUSE_RECEIVE')) OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND m.department=@scope)
   """);
  if(await Exists("workshop_available_inventory"))queries.Add("""
   SELECT CONCAT('stock:',id),created_at,created_by_name,created_by,'WORKSHOP','AVAILABLE_ENTRY',serial,CONCAT(equipment,N' · ',notes) FROM dbo.workshop_available_inventory WHERE @scope IN('ALL','WORKSHOP','TECHNICAL_FAILURE')
   """);
  if(await Exists("support_ticket_history"))queries.Add("""
   SELECT CONCAT('ticket:',h.id),h.created_at,h.actor_name,COALESCE(u.email,h.actor_user_id),'SUPPORT',h.action,CONCAT('#',t.ticket_number),CONCAT(t.subject,N' · ',h.comment,N' · Ruta: ',COALESCE(h.from_team,h.from_department,N'Origen'),N' → ',COALESCE(h.to_team,h.to_department,t.assigned_team,t.assigned_department,N'Sin destino'))
   FROM dbo.support_ticket_history h JOIN dbo.support_tickets t ON t.id=h.ticket_id LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=h.actor_user_id
   WHERE @scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND COALESCE(h.to_department,t.assigned_department)=@scope)
    OR (@scope='CALL_CENTER' AND (
      h.from_team='CALL_CENTER' OR h.to_team='CALL_CENTER' OR t.assigned_team='CALL_CENTER'
      OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team='CALL_CENTER')))
    OR (@scope='TECHNICAL_FAILURE' AND (
      h.from_team IN('TECHNICAL_FAILURE','TECHNICIANS') OR h.to_team IN('TECHNICAL_FAILURE','TECHNICIANS') OR t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS')
      OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team IN('TECHNICAL_FAILURE','TECHNICIANS'))))
    OR (@scope='ADMINISTRATIVE' AND EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND (creator.role='ADMINISTRATIVE' OR creator.email IN('emma.mendoza','emma.mendoza@grupotejeda.local'))))
    OR (@scope='WAREHOUSE' AND EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND (creator.role='WAREHOUSE' OR creator.support_team='WAREHOUSE')))
   """);
  if(await Exists("workshop_stock_exits"))queries.Add("""
   SELECT CONCAT('stock-exit:',stock_id),created_at,actor_name,actor_id,'WORKSHOP','STOCK_EXIT',CONVERT(nvarchar(80),ticket_id),CONCAT(equipment,N' · ',serial)
   FROM dbo.workshop_stock_exits WHERE @scope IN('ALL','WORKSHOP','TECHNICAL_FAILURE','TECHNOLOGY')
   """);
  if(await Exists("automatic_findings"))queries.Add("""
   SELECT CONCAT('finding:',id),resolved_at,resolved_by_name,resolved_by_username,'FINDING',status,title,CONCAT(diagnosis,N' · ',resolution,N' · ',verification)
   FROM dbo.automatic_findings WHERE resolved_at IS NOT NULL AND (@scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND assigned_department=@scope) OR (@scope='TECHNICAL_FAILURE' AND assigned_department='TECHNOLOGY'))
   """);
  if(await Exists("automatic_finding_notifications"))queries.Add("""
   SELECT CONCAT('finding-event:',MIN(CONVERT(varchar(36),n.id))),n.created_at,n.actor_name,'','FINDING',n.event_type,f.title,n.message
   FROM dbo.automatic_finding_notifications n JOIN dbo.automatic_findings f ON f.id=n.finding_id
   WHERE @scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND f.assigned_department=@scope) OR (@scope='TECHNICAL_FAILURE' AND f.assigned_department='TECHNOLOGY')
   GROUP BY n.finding_id,n.created_at,n.actor_name,n.event_type,f.title,n.message
   """);
  if(await Exists("agency_transition_progress"))queries.Add("""
   SELECT CONCAT('transition:',id),created_at,created_by_name,created_by_user_id,'TRANSITION',progress_status,CONVERT(nvarchar(80),project_id),note FROM dbo.agency_transition_progress WHERE @scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND department=@scope)
   """);
  // Field audits are supervisor actions and must remain visible in the
  // consolidated operational history, including records created previously.
  if(await Exists("agency_profiles")&&await Exists("agencies"))queries.Add("""
   SELECT CONCAT('field-audit:',p.id),p.submitted_at,COALESCE(JSON_VALUE(p.submitted_by_context,'$.displayName'),u.display_name),COALESCE(JSON_VALUE(p.submitted_by_context,'$.login'),u.email),'FIELD_AUDIT','PROFILE_CREATED',CONCAT(a.codigo,N' · ',a.terminal),CONCAT(N'Levantamiento de campo completado · ',p.municipio,N', ',p.provincia)
   FROM dbo.agency_profiles p JOIN dbo.agencies a ON a.id=p.agency_id
   LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')
   WHERE @scope IN('ALL','SUPERVISION')
   """);
  if(await Exists("audit_log"))queries.Add("""
   SELECT CONCAT('operation-error:',l.id),l.created_at,COALESCE(u.display_name,l.actor_email),l.actor_email,'OPERATION_ERROR',l.action,JSON_VALUE(l.after_data,'$.reference'),JSON_VALUE(l.after_data,'$.detail')
   FROM dbo.audit_log l LEFT JOIN dbo.admin_users u ON LOWER(u.email)=LOWER(l.actor_email)
   WHERE l.entity_type='operational_error' AND (@scope='ALL' OR JSON_VALUE(l.after_data,'$.department')=@scope)
   """);
  if(await Exists("maintenance_document_signatures"))queries.Add("""
   SELECT CONCAT('signature:',s.movement_id,':',s.signature_role),s.signed_at,s.signer_name,s.signer_login,m.operational_area,CONCAT('SIGN_',s.signature_role),m.document_number,N'Firma registrada'
   FROM dbo.maintenance_document_signatures s JOIN dbo.maintenance_movements m ON m.id=s.movement_id WHERE @scope='ALL' OR (@scope='WAREHOUSE' AND m.operational_area='WAREHOUSE') OR (@scope IN('TECHNICAL_FAILURE','WORKSHOP') AND m.operational_area='WORKSHOP') OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND m.department=@scope)
   """);
  if(await Exists("institutional_files")){
   const string filter="(@scope='ALL' OR (@scope='ADMINISTRATIVE' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Administración' AND JSON_QUERY(f.metadata,'$.administrativeRequest') IS NOT NULL) OR (@scope='WAREHOUSE' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Almacén' AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL) OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','SUPERVISION','ADMINISTRATIVE') AND f.department=@department))";
   queries.Add("SELECT CONCAT('document:',f.id),TRY_CONVERT(datetime2,JSON_VALUE(f.metadata,'$.createdUtc')),JSON_VALUE(f.metadata,'$.ownerDisplayName'),JSON_VALUE(f.metadata,'$.ownerLogin'),'DOCUMENT','CREATED',JSON_VALUE(f.metadata,'$.title'),JSON_VALUE(f.metadata,'$.description') FROM dbo.institutional_files f WHERE "+filter);
   queries.Add("SELECT CONCAT('document-event:',f.id,':',j.[key]),TRY_CONVERT(datetime2,JSON_VALUE(j.value,'$.at')),JSON_VALUE(j.value,'$.actor'),JSON_VALUE(j.value,'$.login'),'DOCUMENT',JSON_VALUE(j.value,'$.action'),JSON_VALUE(f.metadata,'$.title'),CONCAT(N'Estado anterior: ',JSON_VALUE(j.value,'$.previousStatus')) FROM dbo.institutional_files f CROSS APPLY OPENJSON(f.metadata,'$.history') j WHERE "+filter);
  }
  if(queries.Count==0)return "[]";
  var normalized=queries.Select(q=>"SELECT id COLLATE DATABASE_DEFAULT id,at,actor COLLATE DATABASE_DEFAULT actor,login COLLATE DATABASE_DEFAULT login,module COLLATE DATABASE_DEFAULT module,action COLLATE DATABASE_DEFAULT action,reference COLLATE DATABASE_DEFAULT reference,detail COLLATE DATABASE_DEFAULT detail FROM ("+q+") source(id,at,actor,login,module,action,reference,detail)");
  // An administrative request may pass through Almacén. That does not make
  // warehouse employees' movements part of the Administrative staff audit.
  var administrativeActors=await Exists("admin_users")?"EXISTS(SELECT 1 FROM dbo.admin_users au WHERE (REPLACE(LOWER(LTRIM(RTRIM(au.email))),'@grupotejeda.local','')=REPLACE(LOWER(LTRIM(RTRIM(activity.login))),'@grupotejeda.local','') OR CONVERT(nvarchar(80),au.id)=activity.login) AND (au.role IN('ADMINISTRATIVE','ADMINISTRATOR') OR (au.role='DEPARTMENT' AND au.support_team='ADMINISTRATIVE')) AND COALESCE(au.support_team,'') NOT IN('WAREHOUSE','WORKSHOP'))":"1=0";
  var actorDepartment=await Exists("admin_users")?"(SELECT TOP(1) NULLIF(au.support_team,'') FROM dbo.admin_users au WHERE REPLACE(LOWER(LTRIM(RTRIM(au.email))),'@grupotejeda.local','')=REPLACE(LOWER(LTRIM(RTRIM(activity.login))),'@grupotejeda.local','') OR CONVERT(nvarchar(80),au.id)=activity.login)":"CAST(NULL AS nvarchar(80))";
  var sql="WITH activity AS ("+string.Join(" UNION ALL ",normalized)+") SELECT id,at,actor,login,module,action,reference,detail,COALESCE("+actorDepartment+",CASE module WHEN 'WORKSHOP' THEN 'WORKSHOP' WHEN 'WAREHOUSE' THEN 'WAREHOUSE' WHEN 'FIELD_AUDIT' THEN 'SUPERVISION' WHEN 'SUPPORT' THEN 'SUPPORT' WHEN 'FINDING' THEN 'FINDING' WHEN 'DOCUMENT' THEN 'DOCUMENT' WHEN 'TRANSITION' THEN 'TRANSITION' ELSE module END) department FROM activity WHERE at>=@from AND at<@to AND (@scope<>'ADMINISTRATIVE' OR (module NOT IN('WAREHOUSE','WORKSHOP') AND "+administrativeActors+")) ORDER BY at DESC,id DESC OFFSET @offset ROWS FETCH NEXT 201 ROWS ONLY FOR JSON PATH";
  await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@scope",scope);cmd.Parameters.AddWithValue("@department",DepartmentAccounts.Names.GetValueOrDefault(scope,""));cmd.Parameters.AddWithValue("@from",from);cmd.Parameters.AddWithValue("@to",to);cmd.Parameters.AddWithValue("@offset",offset);
  var json=new StringBuilder();await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))json.Append(reader.GetString(0));return json.Length==0?"[]":json.ToString();
 }
}
