using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
var checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
var name="Codex_TicketDates_QA_"+Guid.NewGuid().ToString("N");await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");await master.OpenAsync();await Exec(master,$"CREATE DATABASE [{name}]");
try{
 var cs=$"Server=localhost;Database={name};Integrated Security=True;User Id=qa;TrustServerCertificate=True";await using var c=new SqlConnection(cs);await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE agencies(id nvarchar(80),grupo nvarchar(100),expected_latitude float,expected_longitude float);
 CREATE TABLE agency_profiles(id uniqueidentifier,agency_id nvarchar(80),latitude float,longitude float,submitted_at datetime2);
 CREATE TABLE admin_users(id nvarchar(80),role varchar(40),is_active bit);
 CREATE TABLE admin_user_groups(user_id nvarchar(80),group_name nvarchar(100));
 CREATE TABLE support_departments(code varchar(40),is_active bit);
 CREATE TABLE support_tickets(id uniqueidentifier,agency_id nvarchar(80),category varchar(40),assigned_department varchar(40),assigned_team varchar(40),priority varchar(20),status varchar(40),subject nvarchar(180),description nvarchar(max),created_by_user_id nvarchar(80),created_by_name nvarchar(160),created_by_username nvarchar(160),ticket_type varchar(20),assigned_technician_id nvarchar(80),created_at datetime2 DEFAULT '2026-09-01',updated_at datetime2 DEFAULT '2026-09-01');
 CREATE TABLE support_ticket_history(ticket_id uniqueidentifier,action varchar(40),from_department varchar(40),to_department varchar(40),from_team varchar(40),to_team varchar(40),actor_user_id nvarchar(80),actor_name nvarchar(160),comment nvarchar(max),created_at datetime2 DEFAULT '2026-09-01');
 CREATE TABLE support_ticket_notifications(ticket_id uniqueidentifier,event_type varchar(40),target_department varchar(40),recipient_user_id nvarchar(80),message nvarchar(max),actor_user_id nvarchar(80),actor_name nvarchar(160),created_at datetime2 DEFAULT '2026-09-01');
 INSERT agencies VALUES('agency','QA',NULL,NULL);INSERT support_departments VALUES('TECHNOLOGY',1);
 """);
 var asm=Assembly.Load("RegistroAgencias.SqlServer.V448");var type=asm.GetType("Database")!;type.GetField("ticketWorkflowSchemaReady",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,true);
 var db=Activator.CreateInstance(type,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",cs}}).Build())!;
 var source=File.ReadAllText("api/DatabaseSqlServerScoped.cs");var start=source.IndexOf("var id=Guid.NewGuid();const string insert=\"\"\"",source.IndexOf("public async Task<SupportTicketRow?> CreateTicket"));start=source.IndexOf("\"\"\"",start)+3;var sql=source[start..source.IndexOf("\"\"\";",start)];
 var id=Guid.NewGuid();await using(var cmd=new SqlCommand(sql,c)){
 foreach(var (k,v) in new (string,object)[]{("id",id),("agency","agency"),("category","QA"),("department","TECHNOLOGY"),("team","CALL_CENTER"),("priority","MEDIUM"),("subject","Milagros QA"),("description","Date regression"),("userId","milagros"),("name","Milagros Nicole"),("username","milagros.nicole"),("ticketType","INTERNAL"),("technician","milagros")})cmd.Parameters.AddWithValue("@"+k,v);await cmd.ExecuteNonQueryAsync();}
 var created=(DateTime)(await Scalar(c,"SELECT created_at FROM support_tickets"))!;Check(Math.Abs((DateTime.UtcNow-created).TotalSeconds)<10,"Ticket uses current server UTC despite stale September database defaults");
 var route=JsonSerializer.Deserialize("{\"assignedDepartment\":\"TECHNOLOGY\",\"assignedTeam\":\"TECHNICAL_FAILURE\",\"comment\":\"Escalamiento QA\"}",asm.GetType("TicketRoute")!,new JsonSerializerOptions(JsonSerializerDefaults.Web));
 var task=(Task)type.GetMethod("RouteTicket")!.Invoke(db,[id,route,"milagros","Technology","Milagros Nicole","CALL_CENTER",CancellationToken.None])!;await task;
 var result=JsonSerializer.Serialize(task.GetType().GetProperty("Result")!.GetValue(task));Check(result.Contains("\"Success\":true")||result.Contains("\"Ok\":true"),"Call Center escalation succeeds: "+result);
 Check((string)(await Scalar(c,"SELECT assigned_team FROM support_tickets"))! =="TECHNICAL_FAILURE","Escalation arrives in Avería Técnica");
 Check(await Scalar(c,"SELECT assigned_technician_id FROM support_tickets") is DBNull,"Escalation releases previous Call Center assignee");
 Check((DateTime)(await Scalar(c,"SELECT created_at FROM support_tickets"))! ==created,"Escalation preserves original creation date");
 Check((string)(await Scalar(c,"SELECT status FROM support_tickets"))! =="OPEN","Escalated ticket awaits assignment");
 foreach(var table in new[]{"support_ticket_history","support_ticket_notifications"}){var date=(DateTime)(await Scalar(c,$"SELECT TOP(1) created_at FROM {table}"))!;Check(Math.Abs((DateTime.UtcNow-date).TotalSeconds)<10,table+" records current event date, ignoring stale defaults");Check((string)(await Scalar(c,$"SELECT TOP(1) {(table.EndsWith("history")?"action":"event_type")} FROM {table}"))! =="ESCALATED",table+" has correct escalation event");}
 var difference=Convert.ToInt32(await Scalar(c,"SELECT ABS(DATEDIFF(second,t.updated_at,n.created_at)) FROM support_tickets t JOIN support_ticket_notifications n ON n.ticket_id=t.id"));Check(difference<2,"Ticket activity and notification agree within same transaction");
 Console.WriteLine($"SUCCESS {checks} checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}]");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
static async Task<object?> Scalar(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);return await cmd.ExecuteScalarAsync();}
