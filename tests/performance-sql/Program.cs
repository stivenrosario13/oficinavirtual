using System.Collections;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Exercise the compiled production catalog method against an isolated local SQL database.
var name = "Codex_Performance_QA_" + Guid.NewGuid().ToString("N");
const string master = "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True";
await using var root = new SqlConnection(master); await root.OpenAsync();
async Task Exec(SqlConnection connection, string sql) { await using var command = new SqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
await Exec(root, $"CREATE DATABASE [{name}]");
try {
 var connectionString = new SqlConnectionStringBuilder(master) {InitialCatalog=name}.ConnectionString;
 await using (var connection = new SqlConnection(connectionString)) {
  await connection.OpenAsync();
  await Exec(connection,"""
   CREATE TABLE dbo.agencies(codigo nvarchar(40),terminal nvarchar(100),grupo nvarchar(120),status varchar(30));
   CREATE TABLE dbo.admin_user_groups(user_id nvarchar(80),group_name nvarchar(120));
   INSERT INTO agencies VALUES('A','Agencia A','Grupo A','COMPLETED'),('B','Agencia B','Grupo B','PENDING'),('C','Agencia C','Grupo A','REVIEW_REQUIRED');
   INSERT INTO admin_user_groups VALUES('supervisor-a','Grupo A'),('supervisor-b','Grupo B');
  """);
 }
 var assembly = Assembly.Load("RegistroAgencias.SqlServer.V453");
 var type = assembly.GetType("Database",true)!;
 // Windows authentication is test-only. The application constructor validates a SQL login;
 // supply a harmless placeholder and replace its in-memory connection before any call.
 var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"]=connectionString+";User Id=qa-placeholder" }).Build();
 var db = Activator.CreateInstance(type,config)!;
 var connections = (List<(string Value,string Mode)>)type.GetField("connections",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(db)!;
 connections.Clear(); connections.Add((connectionString,"local-qa"));
 var method = type.GetMethod("PanelAgencies")!;
 async Task<string[]> Rows(string? group,string user,string role) {
  var task=(Task)method.Invoke(db,[group,user,role,CancellationToken.None])!;await task;
  var rows=(IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
  return rows.Cast<object>().Select(row=>(string)row.GetType().GetProperty("Codigo")!.GetValue(row)!).Order().ToArray();
 }
 async Task Check(string label,string? group,string user,string role,params string[] expected) {
  if(!(await Rows(group,user,role)).SequenceEqual(expected))throw new Exception(label);
  Console.WriteLine("PASS "+label);
 }
 await Check("Full catalog keeps administrator scope",null,"admin","Administrator","A","B","C");
 await Check("Supervisor catalog contains only assigned group",null,"supervisor-a","GroupAdministrator","A","C");
 await Check("Other supervisor remains isolated",null,"supervisor-b","GroupAdministrator","B");
 await Check("Unassigned supervisor receives no agencies",null,"unknown","GroupAdministrator");
 await Check("Explicit other group cannot bypass scope","Grupo B","supervisor-a","GroupAdministrator");
 await Check("Existing single-group route retains its filter","Grupo B","admin","Administrator","B");
} finally {
 SqlConnection.ClearAllPools();
 await Exec(root,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
}
