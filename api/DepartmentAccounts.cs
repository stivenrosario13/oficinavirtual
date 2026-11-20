using System.Security.Claims;
static class DepartmentAccounts
{
 public static readonly Dictionary<string,string> Names=new(){["TECHNOLOGY"]="Tecnología",["GENERAL_SERVICES"]="Servicios Generales",["HUMAN_RESOURCES"]="Recursos Humanos",["ADMINISTRATIVE"]="Administración",["WAREHOUSE"]="Almacén",["WORKSHOP"]="Taller",["COMMUNICATION"]="Comunicación",["COMMERCIALIZATION"]="Comercialización",["TREASURY"]="Tesorería",["ACCOUNTING"]="Contabilidad",["GENERAL_AUDIT"]="Auditoría General",["VAULT"]="Bóveda",["CASHIER"]="Caja",["CALL_CENTER"]="Call Center",["TECHNICAL_FAILURE"]="Avería Técnica",["SUPERVISION"]="Supervisión"};
 public static bool Valid(string? code)=>code is not null&&Names.ContainsKey(code);
 public static string AccountDepartment(string role,string? team)=>role.ToUpperInvariant() switch {
  "DEPARTMENT"=>Names.GetValueOrDefault(team??"","Sin departamento"),
  "WAREHOUSE"=>"Almacén",
  "TECHNOLOGY"=>Names.GetValueOrDefault(team??"","Tecnología"),
  "GENERALSERVICES" or "GENERAL_SERVICES"=>team=="WORKSHOP"?"Taller":"Servicios Generales",
  "HUMANRESOURCES" or "HUMAN_RESOURCES"=>"Recursos Humanos",
  "GROUPADMINISTRATOR" or "GROUP_ADMIN"=>"Supervisión",
  _=>"Administración"
 };
 public static bool LegacyStandalone(string? code)=>code is "ADMINISTRATIVE" or "COMMUNICATION" or "COMMERCIALIZATION" or "TREASURY" or "ACCOUNTING" or "GENERAL_AUDIT" or "VAULT" or "CASHIER" or "SUPERVISION";
 public static bool Standalone(string? code)=>Valid(code);
 // Internal query scope, derived exclusively from signed-in claims. This is not an account role.
 public static string SupportScopeRole(ClaimsPrincipal user,string path,bool administrativeManager=false){
  var role=user.FindFirstValue(ClaimTypes.Role)??"Viewer";
  if(role=="Administrative"&&administrativeManager&&(path.StartsWith("/api/tickets",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/api/support/chat",StringComparison.OrdinalIgnoreCase)))return "Department:ADMINISTRATIVE";
  if(role!="Department"||!(path.StartsWith("/api/tickets",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/api/support/chat",StringComparison.OrdinalIgnoreCase)))return role;
  var department=user.FindFirstValue("support_team");
  return "Department:"+(Valid(department)?department:"INVALID");
 }
 public static bool IsSupportScope(string role)=>role.StartsWith("Department:",StringComparison.Ordinal);
 public static string SupportDepartment(string role)=>IsSupportScope(role)&&Valid(role[11..])?role[11..]:"__DENIED__";
 public static bool ApiAllowed(string path,string method){
  if(!path.StartsWith("/api/",StringComparison.OrdinalIgnoreCase))return true;
  if(new[]{"/api/admin/login","/api/admin/logout","/api/admin/session","/api/admin/session/restore","/api/license/status","/api/version","/api/health"}.Contains(path))return true;
  if(method=="GET"&&new[]{"/api/department-support","/api/department-audit","/api/support/catalog","/api/live/stream"}.Contains(path))return true;
  if(method=="GET"&&new[]{"/api/warehouse/fulfillment/inventory","/api/warehouse/fulfillment/track","/api/warehouse/fulfillment/notifications","/api/warehouse/fulfillment/allocations"}.Contains(path))return true;
  if(path=="/api/push/public-key"&&method=="GET"||path=="/api/push/subscriptions"&&method is "POST" or "DELETE")return true;
  // Endpoint policies still enforce each individual permission. Destructive/global routes stay blocked.
  if((path=="/api/tickets"||path.StartsWith("/api/tickets/",StringComparison.OrdinalIgnoreCase))&&method is "GET" or "POST" or "PUT")return true;
  if(path.StartsWith("/api/support/chat/",StringComparison.OrdinalIgnoreCase)&&method is "GET" or "POST" or "PUT")return true;
  // Deleting one's own chat message retains the endpoint's ownership check.
  if(method=="DELETE"&&path.StartsWith("/api/support/chat/messages/",StringComparison.OrdinalIgnoreCase)&&Guid.TryParse(path["/api/support/chat/messages/".Length..],out _))return true;
  return new[]{"/api/account","/api/administrative-files","/api/administrative-assets","/api/institutional-documents"}.Any(p=>path==p||path.StartsWith(p+"/",StringComparison.OrdinalIgnoreCase));
 }
}
