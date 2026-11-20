using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Security.Claims;

int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
var reference=new Dictionary<string,bool>{{"canAssignTickets",true},{"canResolveTickets",true},{"canManageUsers",false},{"canRequestWarehouse",true},{"canConfigureProfile",true}};
foreach(var role in new[]{"TECHNOLOGY","GENERAL_SERVICES","HUMAN_RESOURCES","DEPARTMENT"}){
 Check(SupportProfilePolicy.IsSupport(role,null,new(){{"profile:SUPPORT",true}}),"Marked support: "+role);
 Check(!SupportProfilePolicy.IsSupport(role,null,new(){{"profile:TECHNICIAN",true}}),"Preserve technician: "+role);
 Check(!SupportProfilePolicy.IsSupport(role,null,new(){{"canManageDepartment",true}}),"Preserve manager: "+role);
}
Check(!SupportProfilePolicy.IsSupport("WAREHOUSE","WAREHOUSE",new(){{"profile:SUPPORT",true}}),"Warehouse support keeps warehouse permissions");
Check(!SupportProfilePolicy.IsSupport("TECHNOLOGY","WORKSHOP",new(){{"profile:SUPPORT",true}}),"Workshop support keeps workshop permissions");
Check(SupportProfilePolicy.IsSupport("TECHNOLOGY","CALL_CENTER",new(){{"canAssignTickets",false}}),"Legacy Call Center support is recognized without a profile marker");
Check(!SupportProfilePolicy.IsSupport("TECHNOLOGY","CALL_CENTER",new(){{"profile:TECHNICIAN",true}}),"Explicit Call Center technician is preserved");
Check(!SupportProfilePolicy.IsSupport("TECHNOLOGY","CALL_CENTER",new(){{"canManageDepartment",true}}),"Call Center manager is preserved");
Check(!SupportProfilePolicy.IsSupport("ADMINISTRATOR",null,new(){{"profile:SUPPORT",true}}),"Never change administrators");
var restricted=new Dictionary<string,bool>{{"canAssignTickets",true},{"canManageUsers",true},{"canViewDocuments",true},{"canPrepareWarehouseRequests",true}};
WarehouseRequesterProfile.Apply(restricted,new(){{WarehouseRequesterProfile.Marker,true}});
Check(!restricted["canAssignTickets"]&&!restricted["canManageUsers"]&&!restricted["canPrepareWarehouseRequests"]&&restricted["canRequestWarehouse"],"Requester denies other privileges");
foreach(var path in new[]{"/api/admin/users","/api/tickets","/api/workshop","/api/warehouse/fulfillment/withdraw","/api/institutional-documents/1/approve","/api/institutional-documents/1/share"})foreach(var method in new[]{"GET","POST","PUT","DELETE"})Check(!WarehouseRequesterProfile.ApiAllowed(path,method),"Requester blocks "+method+" "+path);
Check(WarehouseRequesterProfile.ApiAllowed("/api/institutional-documents","POST"),"Requester may submit");
Check(WarehouseRequesterProfile.ApiAllowed("/api/support/catalog","GET"),"Requester may load maintenance failure choices");
Check(!WarehouseRequesterProfile.ApiAllowed("/api/support/catalog","POST"),"Requester cannot change the failure catalog");
Check(WarehouseRequesterProfile.ApiAllowed("/api/institutional-documents/1/preview","GET"),"Requester may preview own PDF through ownership check");
var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("permissions_json",JsonSerializer.Serialize(restricted))},"test"));Check(WarehouseRequesterProfile.IsRequester(principal),"Requester marker survives claims");

var dbName="Codex_SupportStandard_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");await master.OpenAsync();
await Exec(master,$"CREATE DATABASE [{dbName}]");
try{
 Database.Connection=$"Server=localhost;Database={dbName};Integrated Security=True;TrustServerCertificate=True";
 await using var connection=new SqlConnection(Database.Connection);await connection.OpenAsync();
 await Exec(connection,"CREATE TABLE admin_users(id nvarchar(80) PRIMARY KEY,email nvarchar(120),role varchar(40),support_team varchar(40),permissions_json nvarchar(max),updated_at datetime2);");
 async Task<Guid> Seed(string email,string role,Dictionary<string,bool> permissions,string? team=null){var id=Guid.NewGuid();await using var cmd=new SqlCommand("INSERT admin_users(id,email,role,support_team,permissions_json,updated_at) VALUES(@id,@email,@role,@team,@permissions,SYSUTCDATETIME())",connection);cmd.Parameters.AddWithValue("@id",id.ToString());cmd.Parameters.AddWithValue("@email",email);cmd.Parameters.AddWithValue("@role",role);cmd.Parameters.AddWithValue("@team",(object?)team??DBNull.Value);cmd.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(permissions));await cmd.ExecuteNonQueryAsync();return id;}
 var source=await Seed("ross.mariel@grupotejeda.local","GENERAL_SERVICES",reference);
 var support=await Seed("support@local","TECHNOLOGY",new(){{"profile:SUPPORT",true},{"canManageUsers",true},{"preserveMetadata",true}});
 var callCenter=await Seed("callcenter@local","TECHNOLOGY",new(){{"canAssignTickets",false},{"canResolveTickets",true},{"preserveMetadata",true}},"CALL_CENTER");
 var technician=await Seed("technician@local","TECHNOLOGY",new(){{"profile:TECHNICIAN",true},{"canResolveTickets",true}});
 var supervisor=await Seed("supervisor@local","GROUP_ADMIN",new(){{"canAudit",true}});
 var warehouse=await Seed("diorly.abreu@grupotejeda.local","WAREHOUSE",new(){{"profile:SUPPORT",true},{"canAuthorizeWarehouseRequests",true},{"canDecideDocuments",true},{"canLoadWarehouseInventory",false}},"WAREHOUSE");
 var enedraine=await Seed("enedraine.elisabeth@grupotejeda.local","GENERAL_SERVICES",new(){{"profile:WAREHOUSE_REQUESTER",true},{"canAssignTickets",false},{"canViewSupportDashboard",false},{"preserveMetadata",true}});
 await Exec(connection,"ALTER TABLE dbo.admin_users ADD is_active bit NOT NULL DEFAULT 1,password_token nvarchar(80) NOT NULL DEFAULT N'unchanged-password',display_name nvarchar(120) NOT NULL DEFAULT N'Unchanged name';");
 var before=await new Database().AdminUsers(default);
 await new Database().EnsureUnifiedSupportProfile(user=>user with{EffectivePermissions=user.Permissions},default);
 var after=await new Database().AdminUsers(default);var changed=after.Single(u=>u.Id==support);
 Check(changed.Permissions!["canAssignTickets"]&&!changed.Permissions["canManageUsers"]&&changed.Permissions["preserveMetadata"],"Support matches reference and retains metadata");
 var callCenterChanged=after.Single(u=>u.Id==callCenter);
 Check(callCenterChanged.Permissions!["canAssignTickets"]&&!callCenterChanged.Permissions["canManageUsers"]&&callCenterChanged.Permissions["profile:SUPPORT"]&&callCenterChanged.Permissions["preserveMetadata"],"Legacy Call Center uses dedicated support profile");
 var aligned=after.Single(u=>u.Id==enedraine);
 Check(aligned.Role=="TECHNOLOGY"&&aligned.SupportTeam=="CALL_CENTER","Enedraine is in the same department and team as Call Center colleagues");
 Check(aligned.Permissions!.Count==callCenterChanged.Permissions!.Count&&aligned.Permissions.All(p=>callCenterChanged.Permissions.TryGetValue(p.Key,out var value)&&value==p.Value),"Enedraine permissions exactly match normalized Call Center support");
 Check(!aligned.Permissions.ContainsKey("profile:WAREHOUSE_REQUESTER")&&!aligned.Permissions.GetValueOrDefault("profile:TECHNICIAN"),"Wrong workspace markers removed");
 await using(var cmd=new SqlCommand("SELECT is_active,password_token,display_name FROM dbo.admin_users WHERE id=@id",connection)){cmd.Parameters.AddWithValue("@id",enedraine.ToString());await using var reader=await cmd.ExecuteReaderAsync();await reader.ReadAsync();Check(reader.GetBoolean(0)&&reader.GetString(1)=="unchanged-password"&&reader.GetString(2)=="Unchanged name","Account activation, password and identity preserved");}
 await using(var cmd=new SqlCommand("SELECT previous_role,previous_team,previous_permissions FROM dbo.call_center_profile_corrections",connection)){await using var reader=await cmd.ExecuteReaderAsync();Check(await reader.ReadAsync(),"Named correction has a backup");Check(reader.GetString(0)=="GENERAL_SERVICES"&&reader.IsDBNull(1)&&reader.GetString(2)==JsonSerializer.Serialize(before.Single(u=>u.Id==enedraine).Permissions),"Original role, team and permissions backed up exactly");}
 foreach(var id in new[]{source,technician,supervisor,warehouse})Check(JsonSerializer.Serialize(before.Single(u=>u.Id==id).Permissions)==JsonSerializer.Serialize(after.Single(u=>u.Id==id).Permissions),"Non-target and reference unchanged");
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM support_permission_backups",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==2,"Previous support permissions backed up once per account");
 var presets=await new Database().UserAccessPresets(default);Check(presets.Count==11&&presets.All(p=>p.Profile=="SUPPORT"&&p.Permissions["canAssignTickets"]&&!p.Permissions["canManageUsers"]),"11 department presets include dedicated Call Center; warehouse and workshop remain separate");
 Database.InvalidateSupportStandard();
 await new Database().EnsureUnifiedSupportProfile(user=>user with{EffectivePermissions=user.Permissions},default);
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM support_permission_backups",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==2,"Repeated calls do not rewrite accounts");
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.call_center_profile_corrections",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==1,"Named correction runs once across restarts");
 // Inspect guard cases inside a rolled-back transaction in this disposable database.
 await using(var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)){
  await using(var reset=new SqlCommand("DELETE dbo.call_center_profile_corrections; UPDATE dbo.admin_users SET email='unrelated.enedraine@local' WHERE id=@id",connection,tx)){reset.Parameters.AddWithValue("@id",enedraine.ToString());await reset.ExecuteNonQueryAsync();}
  Check(await Database.AlignEnedraineCallCenter(connection,tx,reference,default) is null,"Missing exact account never changes similar names");
  await using(var duplicates=new SqlCommand("UPDATE dbo.admin_users SET email='enedraine.elisabeth@grupotejeda.local' WHERE id=@id; INSERT dbo.admin_users(id,email,role,permissions_json) VALUES(CONVERT(nvarchar(80),NEWID()),'enedraine.elisabeth','VIEWER','{}')",connection,tx)){duplicates.Parameters.AddWithValue("@id",enedraine.ToString());await duplicates.ExecuteNonQueryAsync();}
  var rejected=false;try{await Database.AlignEnedraineCallCenter(connection,tx,reference,default);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Ambiguous duplicate login is rejected without choosing a user");
  await tx.RollbackAsync();
 }
 // Reproduce the deployed installation where the optional Ross account is absent.
 await using(var remove=new SqlCommand("DELETE dbo.admin_users WHERE id=@source; DELETE dbo.call_center_profile_corrections; UPDATE dbo.admin_users SET role='GENERAL_SERVICES',support_team=NULL,permissions_json=N'{\"profile:WAREHOUSE_REQUESTER\":true,\"canViewSupportDashboard\":false}' WHERE id=@target",connection)){
  remove.Parameters.AddWithValue("@source",source.ToString());remove.Parameters.AddWithValue("@target",enedraine.ToString());await remove.ExecuteNonQueryAsync();
 }
 Database.InvalidateSupportStandard();
 await new Database().EnsureUnifiedSupportProfile(user=>user with{EffectivePermissions=user.Permissions},default);
 var withoutSource=(await new Database().AdminUsers(default)).Single(u=>u.Id==enedraine);
 Check(withoutSource.Role=="TECHNOLOGY"&&withoutSource.SupportTeam=="CALL_CENTER"&&withoutSource.Permissions!["canAssignTickets"],"Enedraine correction works without Ross using saved Call Center preset");
 Check(Database.SupportStandard is null,"Missing source does not fabricate a shared reference");
 Check((await new Database().AdminUsers(default)).Count==before.Count-1,"No replacement reference account created");
 await Exec(connection,"DELETE dbo.call_center_profile_corrections; DELETE dbo.user_access_presets WHERE department='CALL_CENTER' AND profile='SUPPORT'; UPDATE dbo.admin_users SET role='GENERAL_SERVICES',support_team=NULL,permissions_json=N'{\"profile:TECHNICIAN\":true,\"canViewSupportDashboard\":false}' WHERE email='enedraine.elisabeth@grupotejeda.local';");
 Database.ResetNamedCorrectionForTest();
 await new Database().EnsureUnifiedSupportProfile(user=>user with{EffectivePermissions=user.Permissions},default);
 withoutSource=(await new Database().AdminUsers(default)).Single(u=>u.Id==enedraine);
 Check(withoutSource.Role=="TECHNOLOGY"&&withoutSource.SupportTeam=="CALL_CENTER"&&withoutSource.Permissions!["canViewSupportDashboard"]&&withoutSource.Permissions["canAssignTickets"],"Built-in Call Center profile restores sections without a reference or preset");
 Check(!withoutSource.Permissions!["canManageUsers"]&&!withoutSource.Permissions["canManageDepartment"]&&!withoutSource.Permissions["canViewAllSupportDepartments"],"Built-in profile does not grant global administration");
 var stable=JsonSerializer.Serialize(withoutSource.Permissions);
 await new Database().EnsureUnifiedSupportProfile(user=>user with{EffectivePermissions=user.Permissions},default);
 Check(JsonSerializer.Serialize((await new Database().AdminUsers(default)).Single(u=>u.Id==enedraine).Permissions)==stable,"No-source correction remains idempotent");
 // Upgrade existing broad Call Center accounts without changing special profiles or credentials.
 var broad=new Dictionary<string,bool>{{"profile:SUPPORT",true},{"canViewAllSupportDepartments",true},{"canViewWorkshop",true},{"canManageDepartment",true},{"metadata",true}};
 var newcomer=await Seed("new.callcenter@local","TECHNOLOGY",broad,"CALL_CENTER");
 var legacy=await Seed("old.callcenter@local","TECHNOLOGY",new(){{"canViewMaintenance",true}},"CALL_CENTER");
 var special=new List<Guid>();
 foreach(var marker in new[]{"DEPARTMENT_MANAGER","TECHNICIAN","WAREHOUSE_REQUESTER"})special.Add(await Seed(marker+"@local","TECHNOLOGY",new(){{"profile:"+marker,true},{"canManageDepartment",marker=="DEPARTMENT_MANAGER"}},"CALL_CENTER"));
 var originalSpecial=(await new Database().AdminUsers(default)).Where(u=>special.Contains(u.Id)||u.Id==warehouse).ToDictionary(u=>u.Id,u=>JsonSerializer.Serialize(u.Permissions));
 await Exec(connection,"INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by) VALUES('CALL_CENTER','SUPPORT',N'{\"canViewWorkshop\":true}','qa');");
 await Database.EnsureCallCenterSupportProfile(connection,default);
 var migrated=await new Database().AdminUsers(default);var expected=CallCenterSupportProfile.Permissions();
 foreach(var id in new[]{newcomer,legacy,enedraine}){
  var current=migrated.Single(u=>u.Id==id);Check(expected.All(p=>current.Permissions!.GetValueOrDefault(p.Key)==p.Value),"Existing and new support accounts share canonical sections");
 }
 foreach(var pair in originalSpecial)Check(JsonSerializer.Serialize(migrated.Single(u=>u.Id==pair.Key).Permissions)==pair.Value,"Special Call Center profiles and warehouse authorizers remain unchanged");
 Check(migrated.Single(u=>u.Id==newcomer).Permissions!["metadata"],"Migration preserves unrelated metadata");
 await using(var cmd=new SqlCommand("SELECT permissions_json FROM dbo.call_center_support_backups WHERE user_id=@id",connection)){cmd.Parameters.AddWithValue("@id",newcomer.ToString());Check((string?)await cmd.ExecuteScalarAsync()==JsonSerializer.Serialize(broad),"Migration backs up exact original permissions");}
 var migratedPreset=(await new Database().UserAccessPresets(default)).Single(p=>p.Department=="CALL_CENTER");Check(expected.Count==migratedPreset.Permissions.Count&&expected.All(p=>migratedPreset.Permissions[p.Key]==p.Value),"Dedicated default preset is stored in database");
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.call_center_preset_backups",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==1,"Previous default preset backed up");
 int backupCount;await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.call_center_support_backups",connection))backupCount=Convert.ToInt32(await cmd.ExecuteScalarAsync());
 await Database.EnsureCallCenterSupportProfile(connection,default);
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.call_center_support_backups",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==backupCount,"Repeated startup does not rewrite canonical accounts");
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE password_token<>'unchanged-password' OR is_active<>1 OR display_name<>'Unchanged name'",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==0,"Migration leaves passwords, account activation and display names intact");
 Console.WriteLine($"SUCCESS {checks} support and requester checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
record AdminUserRow(Guid Id,string Email,string Role,string? SupportTeam,Dictionary<string,bool>? Permissions,Dictionary<string,bool>? EffectivePermissions=null);
sealed partial class Database{
 public static void ResetNamedCorrectionForTest()=>namedCallCenterCorrectionReady=false;
 public static string Connection="";
 static Dictionary<string,bool> ReadPermissions(string? raw)=>JsonSerializer.Deserialize<Dictionary<string,bool>>(raw??"{}")??[];
 static async Task<SqlConnection> Open(CancellationToken ct){var c=new SqlConnection(Connection);await c.OpenAsync(ct);return c;}
 public async Task<List<AdminUserRow>> AdminUsers(CancellationToken ct){await using var c=await Open(ct);await using var cmd=new SqlCommand("SELECT id,email,role,support_team,permissions_json FROM admin_users",c);await using var r=await cmd.ExecuteReaderAsync(ct);var rows=new List<AdminUserRow>();while(await r.ReadAsync(ct))rows.Add(new(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),ReadPermissions(r.GetString(4))));return rows;}
}
