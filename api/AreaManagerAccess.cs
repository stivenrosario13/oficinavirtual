using System.Security.Claims;
using System.Text.Json;

static class AreaManagerAccess
{
 public const string Permission="canManageDepartment";
 public static void ApplyViews(Dictionary<string,bool> values,IReadOnlyDictionary<string,bool>? custom)
 {
  if(!values.GetValueOrDefault(Permission))return;
  foreach(var key in new[]{"canViewFieldAudits","canViewDashboard","canViewSupportDashboard"})
   values[key]=custom is not null&&custom.TryGetValue(key,out var granted)?granted:true;
 }
 public static bool Default(string role,string? team,string login)=>role=="Administrator"||
  ((role=="Warehouse"||team=="WAREHOUSE")&&login.Equals("bryan.arias@grupotejeda.local",StringComparison.OrdinalIgnoreCase))||
  (team=="CALL_CENTER"&&(login.Equals("yessica.mercedes@grupotejeda.local",StringComparison.OrdinalIgnoreCase)||login.Equals("yessica.mercedes",StringComparison.OrdinalIgnoreCase)))||
  (role=="Technology"&&team=="TECHNICAL_FAILURE"&&(login.Equals("rosmarlin.vittini@grupotejeda.local",StringComparison.OrdinalIgnoreCase)||login.Equals("rosmarlin.vittini",StringComparison.OrdinalIgnoreCase)));
 public static bool Allowed(ClaimsPrincipal user)
 {
  if(user.Identity?.IsAuthenticated!=true)return false;
  if(user.IsInRole("Administrator"))return true;
  try{using var doc=JsonDocument.Parse(user.FindFirstValue("permissions_json")??"{}");if(doc.RootElement.ValueKind!=JsonValueKind.Object)return false;if(doc.RootElement.TryGetProperty(Permission,out var value))return value.ValueKind==JsonValueKind.True;}catch(JsonException){return false;}
  return Default(user.FindFirstValue(ClaimTypes.Role)??"",user.FindFirstValue("support_team"),user.Identity?.Name??"");
 }
 // Scope is derived only from the authenticated account, never from query parameters.
 public static string? Scope(ClaimsPrincipal user)
 {
  if(!Allowed(user))return null;
  if(user.IsInRole("Administrator"))return "ALL";
  if(user.IsInRole("Department"))return DepartmentAccounts.Standalone(user.FindFirstValue("support_team"))?user.FindFirstValue("support_team"):null;
  var team=user.FindFirstValue("support_team");
  if(user.IsInRole("Warehouse")||team=="WAREHOUSE")return "WAREHOUSE";
  if(team=="WORKSHOP")return "WORKSHOP";
  if(user.IsInRole("Technology"))return team=="TECHNICAL_FAILURE"?"TECHNICAL_FAILURE":team=="CALL_CENTER"?"CALL_CENTER":"TECHNOLOGY";
  return user.FindFirstValue(ClaimTypes.Role) switch{"GeneralServices"=>"GENERAL_SERVICES","HumanResources"=>"HUMAN_RESOURCES","Administrative"=>"ADMINISTRATIVE","GroupAdministrator"=>"SUPERVISION",_=>null};
 }
 public static string? RequestedScope(ClaimsPrincipal user,string? requested)
 {
  var own=Scope(user);if(own is null)return null;
  if(string.IsNullOrWhiteSpace(requested)||requested=="ALL")return own;
  if(requested==own)return own;
  if(own=="ALL"&&DepartmentAccounts.Valid(requested))return requested;
  return own=="TECHNOLOGY"&&requested is "CALL_CENTER" or "TECHNICAL_FAILURE"?requested:null;
 }
}
