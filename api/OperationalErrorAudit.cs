using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Text.Json;

public sealed record OperationalErrorReport(string? Action,string? Detail,string? Reference);

static class OperationalErrorScope
{
 public static string Department(ClaimsPrincipal user)
 {
  var role=user.FindFirstValue(ClaimTypes.Role)??"";
  var team=user.FindFirstValue("support_team");
  return role switch {
   "GroupAdministrator"=>"SUPERVISION",
   "Department" when DepartmentAccounts.Valid(team)=>team!,
   "Warehouse"=>"WAREHOUSE",
   "GeneralServices" when team=="WORKSHOP"=>"WORKSHOP",
   "GeneralServices"=>"GENERAL_SERVICES",
   "HumanResources"=>"HUMAN_RESOURCES",
   "Technology" when team is "CALL_CENTER" or "TECHNICAL_FAILURE"=>team!,
   "Technology"=>"TECHNOLOGY",
   "Administrative"=>"ADMINISTRATIVE",
   _=>"ADMINISTRATIVE"
  };
 }
}

partial class Database
{
 public async Task RecordOperationalError(OperationalErrorReport report,ClaimsPrincipal user,CancellationToken ct)
 {
  var action=(report.Action??"OPERATION_ERROR").Trim();
  var detail=(report.Detail??"").Trim();
  if(action.Length==0||action.Length>80||detail.Length==0||detail.Length>1600)return;
  await using var connection=await Open(ct);
  await using var exists=new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.audit_log',N'U') IS NULL THEN 0 ELSE 1 END",connection);
  if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct))==0)return;
  var reference=(report.Reference??"").Trim();
  var metadata=JsonSerializer.Serialize(new { department=OperationalErrorScope.Department(user),reference=reference[..Math.Min(reference.Length,240)],detail });
  await using var command=new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email) VALUES('operational_error',CONVERT(nvarchar(36),NEWID()),@action,@data,@actor);",connection);
  command.Parameters.AddWithValue("@action",action);
  command.Parameters.AddWithValue("@data",metadata);
  command.Parameters.AddWithValue("@actor",user.Identity?.Name??"");
  await command.ExecuteNonQueryAsync(ct);
 }
}
