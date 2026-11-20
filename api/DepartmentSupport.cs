using Microsoft.Data.SqlClient;
using System.Text;
partial class Database
{
 public async Task<string> DepartmentSupport(string scope,CancellationToken ct){
  await using var c=await Open(ct);
  const string sql="""
   SELECT t.id,t.ticket_number ticketNumber,t.subject,t.description,t.category,t.priority,t.status,t.ticket_type ticketType,
    t.assigned_department assignedDepartment,t.assigned_team assignedTeam,t.created_by_name createdByName,
    COALESCE(NULLIF(t.resolved_by_name,N''),resolver.display_name) resolvedByName,
    technician.display_name assignedTechnicianName,t.created_at createdAt,t.updated_at updatedAt,
    COALESCE(a.codigo,'') codigo,COALESCE(a.terminal,'') terminal,COALESCE(a.grupo,'') grupo,
    JSON_QUERY((SELECT h.id,h.action,h.actor_name actorName,h.comment,h.from_department fromDepartment,
      h.to_department toDepartment,h.from_team fromTeam,h.to_team toTeam,h.created_at createdAt
      FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id ORDER BY h.created_at,h.id FOR JSON PATH)) history
   FROM dbo.support_tickets t
   LEFT JOIN dbo.agencies a ON a.id=t.agency_id
   LEFT JOIN dbo.admin_users technician ON CONVERT(nvarchar(80),technician.id)=t.assigned_technician_id
   LEFT JOIN dbo.admin_users resolver ON CONVERT(nvarchar(80),resolver.id)=t.resolved_by_user_id
   WHERE (@scope NOT IN('WAREHOUSE','WORKSHOP','CALL_CENTER','TECHNICAL_FAILURE') AND t.assigned_department=@scope)
    OR (@scope='CALL_CENTER' AND (
      t.assigned_team='CALL_CENTER'
      OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team='CALL_CENTER')
      OR EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id AND (h.from_team='CALL_CENTER' OR h.to_team='CALL_CENTER'))))
    OR (@scope='TECHNICAL_FAILURE' AND (
      t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS')
      OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team IN('TECHNICAL_FAILURE','TECHNICIANS'))
      OR EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id AND (h.from_team IN('TECHNICAL_FAILURE','TECHNICIANS') OR h.to_team IN('TECHNICAL_FAILURE','TECHNICIANS')))))
    OR (@scope IN('WAREHOUSE','WORKSHOP') AND (t.assigned_department=@scope OR EXISTS(SELECT 1 FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=t.created_by_user_id AND (u.support_team=@scope OR u.role=@scope))))
   ORDER BY t.updated_at DESC FOR JSON PATH
   """;
  await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@scope",scope);
  var json=new StringBuilder();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))json.Append(r.GetString(0));return json.Length==0?"[]":json.ToString();
 }

 public async Task<string> DepartmentSupportMembers(string scope,CancellationToken ct){
  await using var c=await Open(ct);
  const string sql="""
   SELECT CONVERT(nvarchar(80),u.id) id,u.display_name displayName,u.email,u.role,u.support_team supportTeam
   FROM dbo.admin_users u
   WHERE u.is_active=1 AND (
    @scope='ALL'
    OR
    (@scope='CALL_CENTER' AND u.support_team='CALL_CENTER')
    OR (@scope='TECHNICAL_FAILURE' AND u.support_team IN('TECHNICAL_FAILURE','TECHNICIANS'))
    OR (@scope NOT IN('CALL_CENTER','TECHNICAL_FAILURE') AND (u.support_team=@scope OR u.role=@scope)))
   ORDER BY u.support_team,u.display_name,u.email FOR JSON PATH
   """;
  await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@scope",scope);
  var json=new StringBuilder();await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))json.Append(r.GetString(0));return json.Length==0?"[]":json.ToString();
 }
}
