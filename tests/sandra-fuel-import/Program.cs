using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
var checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
var name="Codex_SandraFuel_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");await master.OpenAsync();
await Exec(master,$"CREATE DATABASE [{name}]");
try{
 await using var c=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE admin_users(id nvarchar(80),email nvarchar(160),display_name nvarchar(160));
 CREATE TABLE institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80),department nvarchar(80),area varchar(20),channel varchar(20),metadata nvarchar(max),file_data varbinary(max));
 CREATE TABLE application_one_time_actions(action_key varchar(100) PRIMARY KEY,executed_utc datetime2 DEFAULT SYSUTCDATETIME());
 INSERT institutional_files VALUES('unrelated',N'Tecnología','files','requests','{"title":"Existing record"}',0x0102);
 """);
 var assembly=Assembly.Load("RegistroAgencias.SqlServer.V448");var type=assembly.GetType("Database")!;
 async Task Import()=>await (Task)type.GetMethod("EnsureSandraFuelImport",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[c,CancellationToken.None])!;
 await Import();Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM application_one_time_actions"))==0,"Missing account does not consume import marker");
 await Exec(c,"INSERT admin_users VALUES('sandra',N' SANDRA.RAMIREZ@grupotejeda.local ',N'Sandra Ramírez')");
 await Import();await Import();
 Check(Convert.ToInt32(await Scalar(c,"SELECT COUNT(*) FROM institutional_files"))==9,"Eight forms inserted once with existing record retained");
 Check((string)(await Scalar(c,"SELECT metadata FROM institutional_files WHERE owner_id='unrelated'"))=="{\"title\":\"Existing record\"}","Existing metadata unchanged");
 await using var read=new SqlCommand("SELECT metadata,file_data FROM institutional_files WHERE owner_id='sandra' ORDER BY id",c);await using var r=await read.ExecuteReaderAsync();var rows=0;decimal total=0;var dates=new HashSet<string>();
 while(await r.ReadAsync()){
  using var json=JsonDocument.Parse(r.GetString(0));var d=json.RootElement;var f=d.GetProperty("administrativeRequest").GetProperty("formData");
  var date=f.GetProperty("fuelDate").GetString()!;dates.Add(date);rows+=f.GetProperty("rowCount").GetInt32();total+=f.GetProperty("calculatedTotal").GetDecimal();
  Check(d.GetProperty("status").GetString()=="pending"&&d.GetProperty("receivedUtc").ValueKind==JsonValueKind.Null&&d.GetProperty("administrativeDelivery").ValueKind==JsonValueKind.Null,date+" has no invented receipt or delivery");
  Check(d.GetProperty("history")[0].GetProperty("action").GetString()=="import",date+" records import provenance");
  Check(System.Text.Encoding.ASCII.GetString((byte[])r[1],0,5)=="%PDF-",date+" has valid PDF bytes");
  if(date is "2026-10-03" or "2026-10-05")Check(f.GetProperty("sections").EnumerateArray().All(s=>s.GetProperty("rows").EnumerateArray().All(row=>row.GetProperty("amount").ValueKind==JsonValueKind.Null)),date+" blank amounts preserved as null");
 }
 Check(rows==417&&total==408722m&&dates.Count==8,"All 417 rows, eight dates and RD$408722 reconciled");
 await r.DisposeAsync();await Exec(c,"UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.description','Edited by user') WHERE id=2");await Import();Check((string)(await Scalar(c,"SELECT JSON_VALUE(metadata,'$.description') FROM institutional_files WHERE id=2"))=="Edited by user","Subsequent import preserves user edits");
 Console.WriteLine($"SUCCESS {checks} checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}]");}
static async Task Exec(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
static async Task<object> Scalar(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);return (await command.ExecuteScalarAsync())!;}
