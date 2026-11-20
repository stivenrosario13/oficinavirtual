using System.Security.Claims;
int checks=0;
void Check(bool value,string text){if(!value)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
Check(AdministrativeProfiles.ApiAllowed("/api/license/status"),"License status is available without expanding administrative permissions");
var original=new AdminIdentity("emma-id","emma.mendoza@grupotejeda.local","Emma","Administrator",false,"avatar",new(){["canManageUsers"]=true},"CALL_CENTER");
var scoped=EmmaAdministrativeAccess.Scope(original);
Check(scoped.Role=="Administrative"&&scoped.SupportTeam is null,"Emma effective department-only role");
Check(original.Role=="Administrator"&&original.Permissions["canManageUsers"],"No stored account mutation");
Check(scoped.Id==original.Id&&scoped.Email==original.Email&&scoped.AvatarUrl==original.AvatarUrl&&scoped.MustChangePassword==original.MustChangePassword,"Identity and password-change state preserved");
Check(scoped.Permissions["canCreateTickets"]&&!scoped.Permissions.ContainsKey("canManageUsers"),"Requester permission, no administration grant");
foreach(var login in new[]{"bryan.arias","whilmaly.sulay","sandra.ramirez","admin@local.com","emma.other"}){
 var other=original with{Email=login};Check(ReferenceEquals(other,EmmaAdministrativeAccess.Scope(other)),login+" unchanged");
}
ClaimsPrincipal User(string login,string role="Administrative")=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,login),new Claim(ClaimTypes.Role,role)},"QA"));
var emma=User(original.Email);
foreach(var path in new[]{"/api/support/catalog","/api/tickets","/api/tickets/agencies","/api/department-audit"})Check(EmmaAdministrativeAccess.ApiAllowed(emma,path,"GET"),"Read "+path);
Check(EmmaAdministrativeAccess.ApiAllowed(emma,"/api/tickets","POST"),"Create internal ticket route");
foreach(var path in new[]{"/api/admin/users","/api/tickets/123/resolve","/api/department-audit","/api/tickets/agencies"})Check(!EmmaAdministrativeAccess.ApiAllowed(emma,path,"POST"),"Reject write "+path);
Check(!EmmaAdministrativeAccess.ApiAllowed(User("sandra.ramirez"),"/api/tickets","POST"),"Other administrative profiles not expanded");
Check(!EmmaAdministrativeAccess.ApiAllowed(User(original.Email,"Viewer"),"/api/tickets","POST"),"Role required");
Check(AdministrativeProfiles.Forms(original.Email)!.SequenceEqual(new[]{"SIM","MARBETE","DEVICE","PAYMENT","FLEET"}),"Assigned forms unchanged");
Console.WriteLine($"SUCCESS {checks} access checks");
record AdminIdentity(string Id,string Email,string DisplayName,string Role,bool MustChangePassword,string? AvatarUrl,Dictionary<string,bool> Permissions,string? SupportTeam);
record DocumentActor(string Role,string Login);
record AdministrativeRequestInfo(string? FormType,string AssetType);
