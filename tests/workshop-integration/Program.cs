global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System.Text.Json;
const string name="Codex_Workshop_Flow_QA";
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");
await master.OpenAsync();
await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'QA database already exists',1; CREATE DATABASE [{name}];");
int count=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
try{
 await using var conn=new SqlConnection(Database.Connection);await conn.OpenAsync();
 await Exec(conn,"""
 CREATE TABLE maintenance_movements(id uniqueidentifier PRIMARY KEY,agency_id char(36),ticket_id uniqueidentifier,department varchar(40),technician_user_id nvarchar(80),technician_name nvarchar(160),movement_type varchar(30),equipment_type nvarchar(100),component_type nvarchar(100),failure_cause nvarchar(200),serial_number nvarchar(120),quantity int,notes nvarchar(2000),created_by_user_id nvarchar(80),created_by_name nvarchar(160),created_at datetime2 DEFAULT SYSUTCDATETIME(),operational_area varchar(20),document_number varchar(40) UNIQUE,qr_token nvarchar(200) UNIQUE,delivered_by_name nvarchar(160),received_by_name nvarchar(160),destination_name nvarchar(200),occurred_at datetime2);
 INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,failure_cause)
 VALUES('11111111-1111-1111-1111-111111111111','TECHNOLOGY','TRANSFER_TO_WORKSHOP','Printer','SERIAL-1',1,'sender','Sender login name','WAREHOUSE','ORIGINAL-1','REAL-MNT|ORIGINAL-1','Original deliverer','Failure'),
 ('22222222-2222-2222-2222-222222222222','TECHNOLOGY','TRANSFER_TO_WORKSHOP','Monitor','SERIAL-2',1,'sender','Sender login name','WAREHOUSE','ALM-20260903-195201-970AB','REAL-MNT|ALM-20260903-195201-970AB','Original deliverer','Failure');
 """);
 var db=new Database();var ct=CancellationToken.None;var id=Guid.Parse("11111111-1111-1111-1111-111111111111");var id2=Guid.Parse("22222222-2222-2222-2222-222222222222");
 var actor=new WorkshopActor("worker","Technology","WORKSHOP","Workshop user","worker.login");
 var sender=new WorkshopActor("sender","GroupAdministrator",null,"Sender","sender.login");
 var other=new WorkshopActor("other","GeneralServices","WORKSHOP","Other","other.login");
 async Task<JsonElement> Get(WorkshopActor user,string code="ORIGINAL-1")=>JsonDocument.Parse(await db.WorkshopSnapshot(user,code,ct)).RootElement;
 Check((await Get(sender))[0].GetProperty("status").GetString()=="SENT","Original sender can track pending transfer");
 Check((await Get(other)).GetArrayLength()==0,"Other department cannot inspect QR");
 Check(!(await db.WorkshopTransition(id,new("RECEIVE","SENT"),sender,ct)).Ok,"Sender cannot perform workshop actions");
 Check(!(await db.WorkshopTransition(id,new("RECEIVE","SENT"),other,ct)).Ok,"Other workshop department cannot receive");
 Check(!(await db.WorkshopTransition(id,new("DELIVER","SENT"),actor,ct)).Ok,"Cannot skip reception or work");
 var received=await Task.WhenAll(db.WorkshopTransition(id,new("RECEIVE","SENT"),actor,ct),db.WorkshopTransition(id,new("RECEIVE","SENT"),actor,ct));
 Check(received.Count(r=>r.Ok)==1,"Concurrent double click creates exactly one reception");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE movement_type='ENTRY'")==1,"Reception automatically creates one entry");
 Check((await Get(sender))[0].GetProperty("returnName").GetString()=="Original deliverer","Return recipient is original deliverer, not creator");
 Check((await db.WorkshopTransition(id,new("REPAIR","RECEIVED"),actor,ct)).Ok,"Received equipment moves to repair table");
 Check(!(await db.WorkshopTransition(id,new("REPLACE","RECEIVED"),actor,ct)).Ok,"Stale selection cannot change route");
 Check(!(await db.WorkshopTransition(id,new("DELIVER","REPAIR"),actor,ct)).Ok,"Delivery requires documented work");
 Check((await db.WorkshopTransition(id,new("WORK","REPAIR","Diagnóstico: cable. Trabajo: reemplazo de cable. Prueba: imprime."),actor,ct)).Ok,"Work log persists on original case");
 Check((await db.WorkshopTransition(id,new("DELIVER","REPAIR"),actor,ct)).Ok,"Repair can be delivered after work");
 Check(!(await db.WorkshopTransition(id,new("DELIVER","REPAIR"),actor,ct)).Ok,"Duplicate delivery cannot create second exit");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE movement_type='EXIT' AND received_by_name='Original deliverer' AND serial_number='SERIAL-1'")==1,"Exit uses original recipient and repaired serial");
 var trace=(await Get(sender))[0];Check(trace.GetProperty("status").GetString()=="DELIVERED"&&trace.GetProperty("events").GetArrayLength()==4,"Original QR shows final status and complete chronology");
 Check(trace.GetProperty("events")[2].GetProperty("actorLogin").GetString()=="worker.login","Work log records authenticated login");
 Check((await db.WorkshopTransition(id2,new("RECEIVE","SENT"),actor,ct)).Ok,"Second transfer receives independently");
 Check((await db.WorkshopTransition(id2,new("REPLACE","RECEIVED"),actor,ct)).Ok,"Replacement selects separate table");
 Check(!(await db.WorkshopTransition(id2,new("WORK","REPLACEMENT","Replaced faulty monitor","SERIAL-2"),actor,ct)).Ok,"Replacement cannot reuse original serial");
 Check((await db.WorkshopTransition(id2,new("WORK","REPLACEMENT","Faulty panel replaced by tested monitor","NEW-SERIAL-2"),actor,ct)).Ok,"Replacement stores new serial");
 Check((await db.WorkshopTransition(id2,new("DELIVER","REPLACEMENT"),actor,ct)).Ok,"Replacement can return to original deliverer");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE movement_type='EXIT' AND received_by_name='Original deliverer' AND serial_number='NEW-SERIAL-2'")==1,"Exit references replacement equipment");
 Check((await Get(sender,"REAL'MNTÇALM'20260903'195201'970AB"))[0].GetProperty("replacementSerial").GetString()=="NEW-SERIAL-2","Malformed original QR preserves replacement trace");
 await using(var command=new SqlCommand("SELECT TOP 1 qr_token FROM maintenance_movements WHERE movement_type='EXIT'",conn)){var token=(string)(await command.ExecuteScalarAsync())!;Check((await Get(sender,token)).GetArrayLength()==1,"Derived movement QR resolves original case");}
 Console.WriteLine(count+" workshop integration checks passed");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
static async Task<int> Scalar(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
partial class Database{
 public const string Connection="Server=localhost;Database=Codex_Workshop_Flow_QA;Integrated Security=True;TrustServerCertificate=True";
 static Task EnsureMaintenanceSchema(SqlConnection c,CancellationToken ct)=>Task.CompletedTask;
 static object Db(object? v)=>v??DBNull.Value;
 async Task<SqlConnection> Open(CancellationToken ct){var c=new SqlConnection(Connection);await c.OpenAsync(ct);return c;}
}
record TicketActionResult(bool Ok,string Code,string? Error);
class LiveNotificationBroker{public void Publish(string name){}}
