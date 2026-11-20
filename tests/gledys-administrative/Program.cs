using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Text.Json;

int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
var user=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"Administrative"),new Claim(ClaimTypes.Name,"gledys.tejeda@grupotejeda.local"),new Claim("permissions_json","{\"canManageDepartment\":true}")},"QA"));
Check(AreaManagerAccess.Scope(user)=="ADMINISTRATIVE","Administrative audit scope");
Check(AreaManagerAccess.RequestedScope(user,"ALL")=="ADMINISTRATIVE"&&AreaManagerAccess.RequestedScope(user,"TECHNOLOGY") is null,"Cannot widen audit to other departments");
Check(DepartmentAccounts.SupportScopeRole(user,"/api/tickets",AreaManagerAccess.Allowed(user))=="Department:ADMINISTRATIVE","Support queries use administrative scope");
Check(DepartmentAccounts.SupportScopeRole(user,"/api/account/password",true)=="Administrative","Account operations retain original role");
Check(DepartmentAccounts.SupportScopeRole(user,"/api/tickets",false)=="Administrative","Ordinary administrative profile remains unchanged");
foreach(var path in new[]{"/api/tickets","/api/tickets/123/resolve","/api/tickets/123/route"})Check(AdministrativeTicketAccess.ApiAllowed(path,"POST",true),"Manager support route: "+path);
Check(!AdministrativeTicketAccess.ApiAllowed("/api/tickets/123/resolve","POST"),"Ordinary administrative profile cannot resolve tickets");
Check(!AdministrativeTicketAccess.ApiAllowed("/api/admin/users","GET",true)&&!AdministrativeTicketAccess.ApiAllowed("/api/tickets/123","DELETE",true),"Global user management and ticket deletion remain unavailable");
foreach(var form in new[]{"SIM","MARBETE","DEVICE","PAYMENT","FLEET","MODEM","CONTRACT_REQUEST","CONTRACT_RENEWAL","RENTAL_CONTRACT","RENT_RECEIPT","INCREASE_LETTER","FUEL_LOG","GENERAL"})Check(AdministrativeProfiles.CanRead(new("Administrative"," GLEDYS.TEJEDA@grupotejeda.local "),new(form,"OTRO")),"Gledys form: "+form);
Check(!AdministrativeProfiles.CanRead(new("Administrative","sandra.ramirez"),new("SIM","OTRO")),"Sandra's existing restrictions retained");
Check(AdministrativeDeliveryQr.RequestNumber("https://example.invalid/api/institutional-documents/lookup/SOL-ADM-QA-1")=="SOL-ADM-QA-1","Main form QR parsed");
Check(AdministrativeDeliveryQr.RequestNumber("https://example.invalid/?equipment=REAL-MNT%7CSOL-ADM-QA-1")=="SOL-ADM-QA-1","Legacy internet QR parsed");
Check(AdministrativeDeliveryQr.RequestNumber("https://example.invalid/not-a-form/SOL-ADM-QA-1") is null,"Unrelated URL rejected");

var name="Codex_Gledys_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");await master.OpenAsync();
await Exec(master,$"CREATE DATABASE [{name}];");
try{
 await using var c=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE admin_users(id varchar(80),email nvarchar(160),role varchar(40),support_team varchar(40),permissions_json nvarchar(max),updated_at datetime2,password_hash varbinary(32),is_active bit,must_change_password bit);
 INSERT admin_users VALUES('other','sandra.ramirez','ADMINISTRATIVE',NULL,N'{"custom":true}',NULL,0x0102,1,0);
 """);
 await Database.EnsureGledysAdministrativeProfile(c,CancellationToken.None);
 Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM admin_users"))==1,"No account or password fabricated when Gledys is absent");
 await Exec(c,"""
 INSERT admin_users VALUES('gledys','GLEDYS.TEJEDA@grupotejeda.local','VIEWER',NULL,N'{"canManageDepartment":false,"canDecideDocuments":false,"custom":true,"canExportAudits":true}',NULL,0xAABB,0,1);
 """);
 await Database.EnsureGledysAdministrativeProfile(c,CancellationToken.None);
 var permissions=JsonDocument.Parse((string)(await Scalar(c,"SELECT permissions_json FROM admin_users WHERE id='gledys'"))!).RootElement;
 foreach(var key in new[]{"canManageDepartment","canCreateTickets","canAssignTickets","canResolveTickets","canDecideDocuments","canManageAdministrativeAssets","custom","canExportAudits"})Check(permissions.GetProperty(key).GetBoolean(),"Permission retained/granted: "+key);
 Check((string)(await Scalar(c,"SELECT role FROM admin_users WHERE id='gledys'"))! =="ADMINISTRATIVE","Administrative role applied");
 Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM admin_users WHERE id='gledys' AND password_hash=0xAABB AND is_active=0 AND must_change_password=1"))==1,"Password and activation untouched");
 Check((string)(await Scalar(c,"SELECT permissions_json FROM admin_users WHERE id='other'"))! =="{\"custom\":true}","Other account unchanged");
 Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM administrative_profile_updates WHERE previous_role='VIEWER' AND JSON_VALUE(previous_permissions,'$.canManageDepartment')='false'"))==1,"Previous profile backed up");
 await Exec(c,"UPDATE admin_users SET permissions_json=JSON_MODIFY(permissions_json,'$.canManageDepartment',CAST(0 AS bit)) WHERE id='gledys';");
 await Database.EnsureGledysAdministrativeProfile(c,CancellationToken.None);
 Check((string)(await Scalar(c,"SELECT JSON_VALUE(permissions_json,'$.canManageDepartment') FROM admin_users WHERE id='gledys'"))! =="false","Later permission edits respected");
 Console.WriteLine($"SUCCESS {checks} checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}];");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
static async Task<object?> Scalar(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);return await cmd.ExecuteScalarAsync();}
record DocumentActor(string Role,string Login);
record AdministrativeRequestInfo(string? FormType,string AssetType);
