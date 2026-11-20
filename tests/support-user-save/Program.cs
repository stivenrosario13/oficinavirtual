using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;

var api=Assembly.LoadFrom(Directory.GetFiles(AppContext.BaseDirectory,"RegistroAgencias.SqlServer.V*.dll").OrderByDescending(File.GetLastWriteTimeUtc).First());
const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
var dbType=api.GetType("Database")!;var createType=api.GetType("AdminUserCreate")!;var updateType=api.GetType("AdminUserUpdate")!;
var createHandler=api.GetTypes().SelectMany(t=>t.GetMethods(all)).Single(m=>m.Name.Contains("b__")&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{createType,dbType,typeof(CancellationToken)}));
var updateHandler=api.GetTypes().SelectMany(t=>t.GetMethods(all)).Single(m=>m.Name.Contains("b__")&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(Guid),updateType,typeof(HttpContext),dbType,typeof(CancellationToken)}));
int checks=0;void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
async Task<object> Invoke(MethodInfo handler,params object?[] args){var target=handler.IsStatic?null:Activator.CreateInstance(handler.DeclaringType!,true);var task=(Task)handler.Invoke(target,args)!;await task;return task.GetType().GetProperty("Result")!.GetValue(task)!;}
var name="Codex_SupportSave_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");await master.OpenAsync();await Exec(master,$"CREATE DATABASE [{name}]");
try{
 var connectionString=$"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True";
 await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
 await Exec(connection,"""
 CREATE TABLE dbo.admin_users(id char(36) PRIMARY KEY,email nvarchar(320) NOT NULL UNIQUE,display_name nvarchar(160) NOT NULL,role varchar(40) NOT NULL,
 password_salt binary(16),password_hash binary(32),password_iterations int,is_active bit NOT NULL DEFAULT 1,must_change_password bit NOT NULL DEFAULT 1,
 last_login_at datetime2 NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NULL,permissions_json nvarchar(max),support_team varchar(30),region nvarchar(80),contact nvarchar(80),avatar_data varbinary(max));
 CREATE TABLE dbo.admin_user_groups(user_id char(36),group_name nvarchar(120),region nvarchar(80));
 CREATE TABLE dbo.agencies(id uniqueidentifier,grupo nvarchar(120),is_active bit);
 """);
 // Execute the production route handlers and persistence methods against an
 // isolated schema; bypass only the unrelated historical startup migrations.
 var db=RuntimeHelpers.GetUninitializedObject(dbType);
 dbType.GetField("connections",all)!.SetValue(db,new List<(string Value,string Mode)>{(connectionString,"isolated-test")});
 dbType.GetField("userPermissionSchemaReady",all)!.SetValue(null,true);
 dbType.GetMethod("InvalidateSupportStandard",all)!.Invoke(null,null);
 Check(dbType.GetProperty("SupportStandard",all)!.GetValue(null) is null,"Reference account unavailable in this scenario");
 var selected=new Dictionary<string,bool>{{"usesExplicitPermissions",true},{"profile:SUPPORT",true},{"canViewSupportDashboard",true},{"canCreateTickets",true},{"canAssignTickets",true},{"canResolveTickets",true},{"canManageUsers",false},{"canManageDepartment",false},{"canManageSupportChat",false},{"metadata",true}};
 object Create(string email,string team)=>Activator.CreateInstance(createType,[email,"Inocencio","TECHNOLOGY","Temporary-test-8642",null,null,new List<string>(),selected,team])!;
 var result=await Invoke(createHandler,Create("inocencio.avila@grupotejeda.local","CALL_CENTER"),db,CancellationToken.None);
 Check(((IStatusCodeHttpResult)result).StatusCode==201,"Create Call Center support succeeds without Ross Mariel");
 var user=((IValueHttpResult)result).Value!;var id=(Guid)user.GetType().GetProperty("Id")!.GetValue(user)!;
 var effective=(Dictionary<string,bool>)user.GetType().GetProperty("EffectivePermissions")!.GetValue(user)!;
 Check((string)user.GetType().GetProperty("SupportTeam")!.GetValue(user)! == "CALL_CENTER"&&effective["canViewSupportDashboard"]&&effective["canAssignTickets"],"Created account opens Call Center support");
 Check(!effective["canManageUsers"]&&!effective["canManageDepartment"]&&!effective["canViewWorkshop"]&&!effective["canViewMaintenance"]&&!effective["canViewAgencyTransitions"]&&!effective["canViewAllSupportDepartments"],"Call Center excludes unrelated sections and global administration");
 byte[] hash;await using(var cmd=new SqlCommand("SELECT password_hash FROM dbo.admin_users WHERE id=@id",connection)){cmd.Parameters.AddWithValue("@id",id.ToString());hash=(byte[])(await cmd.ExecuteScalarAsync())!;}
 var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Administrator")],"qa"))};
 var body=Activator.CreateInstance(updateType,["inocencio.avila@grupotejeda.local","Inocencio Ávila","TECHNOLOGY",true,null,null,null,new List<string>(),selected,"CALL_CENTER"]);
 result=await Invoke(updateHandler,id,body,context,db,CancellationToken.None);
 Check(((IStatusCodeHttpResult)result).StatusCode==200,"Edit Call Center support succeeds without Ross Mariel");
 await using(var cmd=new SqlCommand("SELECT password_hash,permissions_json FROM dbo.admin_users WHERE id=@id",connection)){cmd.Parameters.AddWithValue("@id",id.ToString());await using var reader=await cmd.ExecuteReaderAsync();await reader.ReadAsync();Check(hash.SequenceEqual((byte[])reader[0]),"Editing permissions preserves password");var saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(reader.GetString(1))!;var canonical=(Dictionary<string,bool>)api.GetType("CallCenterSupportProfile")!.GetMethod("Permissions",all)!.Invoke(null,null)!;Check(canonical.All(p=>saved[p.Key]==p.Value)&&saved["metadata"],"Dedicated Call Center profile persisted with metadata");}
 result=await Invoke(createHandler,Create("inocencio.avila@grupotejeda.local","CALL_CENTER"),db,CancellationToken.None);
 Check(((IStatusCodeHttpResult)result).StatusCode==409,"Existing account conflict remains protected");
 result=await Invoke(createHandler,Create("invalid.area@grupotejeda.local","NOT_A_TEAM"),db,CancellationToken.None);
 Check(((IStatusCodeHttpResult)result).StatusCode==422,"Invalid department/team is still rejected");
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==1,"No reference account or duplicate user fabricated");
 dbType.GetField("supportStandard",all)!.SetValue(null,new Dictionary<string,bool>(selected){{"canUseSupportChat",true},{"canViewWorkshop",true},{"canViewAgencyTransitions",true}});
 result=await Invoke(createHandler,Create("second.support@grupotejeda.local","CALL_CENTER"),db,CancellationToken.None);
 Check(((IStatusCodeHttpResult)result).StatusCode==201,"Creation still succeeds with an available shared reference");
 var second=((IValueHttpResult)result).Value!;var permissions=(Dictionary<string,bool>)second.GetType().GetProperty("Permissions")!.GetValue(second)!;
 Check(permissions["canUseSupportChat"]&&!permissions["canViewWorkshop"]&&!permissions["canViewAgencyTransitions"],"Generic support reference cannot broaden Call Center access");
 Console.WriteLine($"SUCCESS {checks} production create/edit handler checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");}
static async Task Exec(SqlConnection connection,string sql){await using var cmd=new SqlCommand(sql,connection);await cmd.ExecuteNonQueryAsync();}
