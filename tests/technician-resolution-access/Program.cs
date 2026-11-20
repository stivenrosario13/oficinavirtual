using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

var assembly=Assembly.LoadFrom(Directory.GetFiles(AppContext.BaseDirectory,"RegistroAgencias.SqlServer.V*.dll").OrderByDescending(File.GetLastWriteTimeUtc).First());
const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
var policy=assembly.GetType("TechnicianResolutionPermissions")!;
var database=assembly.GetType("Database")!;
var program=assembly.GetType("Program")!;
var accountMethod=program.GetMethods(flags).Single(m=>m.Name.Contains("g__AccountPermissions|"));
var claimMethod=program.GetMethods(flags).Single(m=>m.Name.Contains("g__PermissionClaim|"));
int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
Dictionary<string,bool> Denied()=>new(){{"usesExplicitPermissions",true},{"canResolveTickets",false},{"canViewSupportDashboard",false},{"canAssignTickets",false},{"canManageUsers",false},{"preserveCustomPermission",true}};
bool IsTech(string role,string? team,Dictionary<string,bool> saved)=>(bool)policy.GetMethod("IsTechnician",flags)!.Invoke(null,[role,team,saved])!;
Dictionary<string,bool> Effective(string role,string? team,Dictionary<string,bool> saved,string email="qa.technician@local"){var account=Activator.CreateInstance(assembly.GetType("AdminIdentity")!,[Guid.NewGuid().ToString(),email,"QA",role,false,null,saved,team]);return (Dictionary<string,bool>)accountMethod.Invoke(null,[account])!;}
Dictionary<string,bool> Claim(string role,string? team,Dictionary<string,bool> saved){var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,role),new Claim("support_team",team??""),new Claim("permissions_json",JsonSerializer.Serialize(saved))],"test"));return (Dictionary<string,bool>)claimMethod.Invoke(null,[principal])!;}
var cases=new (string Role,string SessionRole,string? Team,bool Marked)[]{("TECHNOLOGY","Technology","TECHNICIANS",false),("GENERAL_SERVICES","GeneralServices",null,false),("GENERAL_SERVICES","GeneralServices","WORKSHOP",false),("HUMAN_RESOURCES","HumanResources",null,false),("DEPARTMENT","Department","COMMUNICATION",true),("TECHNOLOGY","Technology","TECHNICAL_FAILURE",true),("TECHNOLOGY","Technology","CALL_CENTER",true),("TECHNOLOGY","Technology","WORKSHOP",true)};
foreach(var item in cases){
 var saved=Denied();if(item.Marked)saved["profile:TECHNICIAN"]=true;
 Check(IsTech(item.Role,item.Team,saved),"Recognizes technician "+item.Role+"/"+item.Team);
 var effective=Effective(item.SessionRole,item.Team,saved);
 Check(effective["canResolveTickets"]&&effective["canViewSupportDashboard"],"Session enables resolution and support "+item.Role+"/"+item.Team);
 Check(!effective["canAssignTickets"]&&!effective["canManageUsers"],"No assignment or admin elevation "+item.Role+"/"+item.Team);
 foreach(var source in new[]{saved,effective}){var claim=Claim(item.SessionRole,item.Team,source);Check(claim["canResolveTickets"]&&claim["canViewSupportDashboard"]&&!claim["canManageUsers"],"Old/new claims grant only technician capabilities "+item.Role+"/"+item.Team);}
}
foreach(var marker in new[]{"profile:SUPPORT","profile:DEPARTMENT_MANAGER","profile:WAREHOUSE_REQUESTER"}){var saved=Denied();saved[marker]=true;Check(!IsTech("GENERAL_SERVICES",null,saved),"Excludes "+marker);var effective=Effective("GeneralServices",null,saved);var claim=Claim("GeneralServices",null,effective);Check(!effective["canResolveTickets"]&&!claim["canResolveTickets"],"Non-technician restriction survives session refresh "+marker);}
var manager=Denied();manager["canManageDepartment"]=true;Check(!IsTech("GENERAL_SERVICES",null,manager),"Excludes legacy manager");
Check(!IsTech("TECHNOLOGY","WAREHOUSE",new(){{"profile:TECHNICIAN",true}}),"Preserves warehouse isolation");
foreach(var role in new[]{"ADMINISTRATOR","GROUP_ADMIN","ADMINISTRATIVE","VIEWER","WAREHOUSE"})Check(!IsTech(role,null,new(){{"profile:TECHNICIAN",true}}),"Does not elevate "+role);
var callCenterSupport=new Dictionary<string,bool>{{"profile:SUPPORT",true},{"usesExplicitPermissions",true},{"canAssignTickets",true},{"canCreateTickets",true},{"canResolveTickets",true},{"canViewSupportDashboard",true},{"canManageUsers",false},{"canManageDepartment",false}};
var enedrainePermissions=Effective("Technology","CALL_CENTER",callCenterSupport,"enedraine.elisabeth@grupotejeda.local");
var colleaguePermissions=Effective("Technology","CALL_CENTER",callCenterSupport,"cristina.orosco@grupotejeda.local");
Check(enedrainePermissions.Count==colleaguePermissions.Count&&enedrainePermissions.All(p=>colleaguePermissions[p.Key]==p.Value),"Enedraine and Call Center colleague receive identical effective navigation permissions");
var enedraineClaim=Claim("Technology","CALL_CENTER",enedrainePermissions);
Check(enedraineClaim["canViewSupportDashboard"]&&enedraineClaim["canAssignTickets"]&&!enedraineClaim.GetValueOrDefault("profile:WAREHOUSE_REQUESTER")&&!enedraineClaim.GetValueOrDefault("profile:TECHNICIAN"),"Enedraine session stays in Call Center support across claim refresh");

var dbName="Codex_TechnicianPermissions_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");await master.OpenAsync();
await Exec(master,$"CREATE DATABASE [{dbName}]");
try{
 await using var connection=new SqlConnection($"Server=localhost;Database={dbName};Integrated Security=True;TrustServerCertificate=True");await connection.OpenAsync();
 await Exec(connection,"CREATE TABLE dbo.admin_users(id nvarchar(80) PRIMARY KEY,email nvarchar(120),role varchar(40),support_team varchar(40),permissions_json nvarchar(max),is_active bit,password_hash nvarchar(120),updated_at datetime2); CREATE TABLE dbo.agencies(id uniqueidentifier PRIMARY KEY); CREATE TABLE dbo.support_tickets(id uniqueidentifier PRIMARY KEY,agency_id uniqueidentifier,assigned_technician_id nvarchar(120),assigned_department varchar(40),assigned_team varchar(40)); CREATE TABLE dbo.support_ticket_shares(ticket_id uniqueidentifier,department_code varchar(40));");
 var rows=new List<(string Id,string Raw,bool Target,bool Active)>();
 async Task<string> Seed(string role,string? team,string raw,bool target,bool active=true){var id=Guid.NewGuid().ToString();await using var cmd=new SqlCommand("INSERT dbo.admin_users VALUES(@id,@email,@role,@team,@raw,@active,'unchanged-test-sentinel',SYSUTCDATETIME())",connection);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@email","qa."+id+"@local");cmd.Parameters.AddWithValue("@role",role);cmd.Parameters.AddWithValue("@team",(object?)team??DBNull.Value);cmd.Parameters.AddWithValue("@raw",raw);cmd.Parameters.AddWithValue("@active",active);await cmd.ExecuteNonQueryAsync();rows.Add((id,raw,target,active));return id;}
 foreach(var item in cases){var saved=Denied();if(item.Marked)saved["profile:TECHNICIAN"]=true;await Seed(item.Role,item.Team,JsonSerializer.Serialize(saved),true);}
 await Seed("GENERAL_SERVICES",null,JsonSerializer.Serialize(Denied()),true,false);
 foreach(var marker in new[]{"profile:SUPPORT","profile:DEPARTMENT_MANAGER","profile:WAREHOUSE_REQUESTER"}){var saved=Denied();saved[marker]=true;await Seed("GENERAL_SERVICES",null,JsonSerializer.Serialize(saved),false);}
 await Seed("GROUP_ADMIN",null,JsonSerializer.Serialize(Denied()),false);
 await Seed("GENERAL_SERVICES",null,"malformed-json",false);
 var already=Denied();already["canResolveTickets"]=true;already["canViewSupportDashboard"]=true;await Seed("TECHNOLOGY","TECHNICIANS",JsonSerializer.Serialize(already),false);
 var migrate=database.GetMethod("EnsureTechnicianResolutionPermissions",flags)!;
 await (Task)migrate.Invoke(null,[connection,CancellationToken.None])!;
 foreach(var row in rows){await using var cmd=new SqlCommand("SELECT permissions_json,is_active,password_hash FROM dbo.admin_users WHERE id=@id",connection);cmd.Parameters.AddWithValue("@id",row.Id);await using var reader=await cmd.ExecuteReaderAsync();await reader.ReadAsync();var raw=reader.GetString(0);Check(reader.GetBoolean(1)==row.Active&&reader.GetString(2)=="unchanged-test-sentinel","Preserves credentials and active state");if(row.Target){var permissions=JsonSerializer.Deserialize<Dictionary<string,bool>>(raw)!;var previous=JsonSerializer.Deserialize<Dictionary<string,bool>>(row.Raw)!;Check(permissions["canResolveTickets"]&&permissions["canViewSupportDashboard"],"Migration enables technician capabilities");Check(previous.All(pair=>pair.Key is "canResolveTickets" or "canViewSupportDashboard"||permissions[pair.Key]==pair.Value)&&permissions.Count==previous.Count,"Preserves every other permission");}else Check(raw==row.Raw,"Non-target document unchanged");}
 await using(var cmd=new SqlCommand("SELECT user_id,permissions_json FROM dbo.technician_resolution_permission_backups",connection)){await using var reader=await cmd.ExecuteReaderAsync();var count=0;while(await reader.ReadAsync()){Check(rows.Single(r=>r.Id==reader.GetString(0)).Raw==reader.GetString(1),"Exact backup of original permissions");count++;}Check(count==rows.Count(r=>r.Target),"One backup per changed account");}
 await (Task)migrate.Invoke(null,[connection,CancellationToken.None])!;
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.technician_resolution_permission_backups",connection))Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==rows.Count(r=>r.Target),"Migration is idempotent");
 var canWork=database.GetMethod("CanWorkTicket",flags)!;
 var technicianId=rows[1].Id;var agency=Guid.NewGuid();await Exec(connection,$"INSERT dbo.agencies VALUES('{agency}')");
 async Task<bool> CanWork(string assigned,string department,string role,string? team,bool assign=false){var ticket=Guid.NewGuid();await using(var cmd=new SqlCommand("INSERT dbo.support_tickets VALUES(@ticket,@agency,@assigned,@department,@team)",connection)){cmd.Parameters.AddWithValue("@ticket",ticket);cmd.Parameters.AddWithValue("@agency",agency);cmd.Parameters.AddWithValue("@assigned",assigned);cmd.Parameters.AddWithValue("@department",department);cmd.Parameters.AddWithValue("@team",(object?)team??DBNull.Value);await cmd.ExecuteNonQueryAsync();}await using var tx=(SqlTransaction)await connection.BeginTransactionAsync();var allowed=await (Task<bool>)canWork.Invoke(null,[connection,tx,ticket,technicianId,role,team,assign,CancellationToken.None])!;await tx.RollbackAsync();return allowed;}
 foreach(var scope in new[]{("GENERAL_SERVICES","GeneralServices",(string?)null),("GENERAL_SERVICES","GeneralServices","WORKSHOP"),("TECHNOLOGY","Technology","TECHNICIANS"),("HUMAN_RESOURCES","HumanResources",(string?)null),("COMMUNICATION","Department:COMMUNICATION",(string?)null)}){
  Check(await CanWork(technicianId,scope.Item1,scope.Item2,scope.Item3),"Assigned ticket authorized "+scope.Item1+"/"+scope.Item3);
  Check(!await CanWork(Guid.NewGuid().ToString(),scope.Item1,scope.Item2,scope.Item3),"Other technician ticket denied "+scope.Item1+"/"+scope.Item3);
  Check(!await CanWork(technicianId,"WAREHOUSE",scope.Item2,scope.Item3),"Other department denied "+scope.Item1+"/"+scope.Item3);
 }
 Check(await CanWork("qa."+technicianId+"@local","GENERAL_SERVICES","GeneralServices",null),"Historical email assignment accepted");
 Check(await CanWork("qa."+technicianId,"GENERAL_SERVICES","GeneralServices",null),"Historical login assignment accepted");
 Console.WriteLine($"SUCCESS: {checks} technician permission, migration, session and assignment checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]");}
static async Task Exec(SqlConnection connection,string sql){await using var cmd=new SqlCommand(sql,connection);await cmd.ExecuteNonQueryAsync();}
