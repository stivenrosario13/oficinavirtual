using Microsoft.Data.SqlClient;

var name="Codex_UserPresets_QA_"+Guid.NewGuid().ToString("N");
if(!name.StartsWith("Codex_UserPresets_QA_",StringComparison.Ordinal)||name.Length!=53)throw new Exception("Invalid test database name");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();bool created=false;int checks=0;
void Check(bool value){if(!value)throw new Exception("Preset persistence check failed");checks++;}
try
{
 await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Existing test database',1; CREATE DATABASE [{name}];");created=true;
 Database.Connection=$"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
 var db=new Database();var ct=CancellationToken.None;
 Check((await db.UserAccessPresets(ct)).Count==0);
 var departments=new[]{"TECHNOLOGY","GENERAL_SERVICES","HUMAN_RESOURCES","ADMINISTRATIVE","WAREHOUSE","WORKSHOP","COMMUNICATION","COMMERCIALIZATION","TREASURY","ACCOUNTING","CALL_CENTER","TECHNICAL_FAILURE","SUPERVISION"};
 var profiles=new[]{"ADMINISTRATOR","DEPARTMENT_MANAGER","SUPPORT","TECHNICIAN","SUPERVISOR","ADMINISTRATIVE","FISCALIZADOR","VIEWER"};
 foreach(var department in departments)foreach(var profile in profiles){
  await db.SaveUserAccessPreset(new(department,profile,new(){{"canRequestWarehouse",true},{"canManageUsers",false}}),"qa",ct);
 }
 var saved=await new Database().UserAccessPresets(ct);Check(saved.Count==104);
 foreach(var department in departments)foreach(var profile in profiles){
  var row=saved.Single(p=>p.Department==department&&p.Profile==profile);
  Check(row.Permissions["canRequestWarehouse"]);Check(!row.Permissions["canManageUsers"]);
 }
 await db.SaveUserAccessPreset(new("TECHNOLOGY","SUPPORT",new(){{"canRequestWarehouse",false}}),"qa",ct);
 saved=await new Database().UserAccessPresets(ct);
 Check(saved.Count==104);Check(!saved.Single(p=>p.Department=="TECHNOLOGY"&&p.Profile=="SUPPORT").Permissions["canRequestWarehouse"]);
 Check(saved.Single(p=>p.Department=="GENERAL_SERVICES"&&p.Profile=="SUPPORT").Permissions["canRequestWarehouse"]);
 Console.WriteLine($"PASS {checks} SQL persistence checks across 13 departments and 8 profiles");
}
finally
{
 SqlConnection.ClearAllPools();
 if(created)await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");
}
static async Task Exec(SqlConnection connection,string sql){await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync();}
sealed partial class Database
{
 public static string Connection="";
 static async Task<SqlConnection> Open(CancellationToken ct){var connection=new SqlConnection(Connection);await connection.OpenAsync(ct);return connection;}
}
