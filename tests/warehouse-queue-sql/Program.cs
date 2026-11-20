using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var name="Codex_Queue_QA_"+Guid.NewGuid().ToString("N");
const string master="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True";
await using var root=new SqlConnection(master);await root.OpenAsync();
async Task Exec(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
await Exec(root,$"CREATE DATABASE [{name}]");
try {
 var connectionString=new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString;
 await using var c=new SqlConnection(connectionString);await c.OpenAsync();
 var assembly=Assembly.LoadFrom(Path.GetFullPath("api/bin/Debug/net8.0/"+System.Xml.Linq.XDocument.Load("api/RegistroAgencias.Api.csproj").Descendants("AssemblyName").Single().Value+".dll"));
 var type=assembly.GetType("Database",true)!;
 var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=connectionString+";User Id=qa-placeholder"}).Build();
 var db=Activator.CreateInstance(type,config)!;
 var connections=(List<(string Value,string Mode)>)type.GetField("connections",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(db)!;
 connections.Clear();connections.Add((connectionString,"local-qa"));
 await (Task)type.GetMethod("EnsureInstitutionalSchema",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[c,CancellationToken.None])!;
 for(var i=0;i<6;i++) {
  var json=JsonSerializer.Serialize(new {area="communications",channel="requirements",ownerUserName=i==1?"other":"owner",ownerDisplayName="QA",department=i==1?"Tecnología":"Servicios Generales",recipientDepartment=i==2?"Administración":"Almacén",title="Solicitud "+i,originalFileName="qa.pdf",status=i==3?"approved":"pending",trashed=i==4,createdUtc=DateTime.UtcNow,receivedUtc=i==5?DateTime.UtcNow:(DateTime?)null,receiptSignature="data:image/png;base64,"+new string('A',2000000),history=new[]{new {previousReceiptSignature=new string('A',2000000)}},warehouseRequest=new{number="SOL-ALM-QA-"+i,agencyId=Guid.Empty,lines=Array.Empty<object>()}});
  await using var insert=new SqlCommand("INSERT dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES('owner',N'Servicios Generales','communications','requirements',@json,0x00)",c);insert.Parameters.AddWithValue("@json",json);await insert.ExecuteNonQueryAsync();
 }
 async Task<JsonElement> Rows(string id,string role,string team,string? department=null) {
  var actor=Activator.CreateInstance(assembly.GetType("DocumentActor")!,[id,role,team,"QA","qa",department,null,false,false])!;
  var task=(Task)type.GetMethod("InstitutionalWarehouseQueue")!.Invoke(db,[actor,CancellationToken.None])!;await task;
  return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task),new JsonSerializerOptions(JsonSerializerDefaults.Web));
 }
 void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
 var admin=await Rows("admin","Administrator","");
 Check(admin.GetArrayLength()==4,"Queue includes pending and approved, excludes other destinations and trash");
 Check(admin.GetRawText().Length<5000,"Four megabytes of embedded assets per request excluded from queue response");
 Check(!admin.GetRawText().Contains("Signature")&&!admin.GetRawText().Contains("file_data"),"No signatures or PDFs exposed by summary endpoint");
 Check(admin.EnumerateArray().Single(x=>x.GetProperty("title").GetString()=="Solicitud 5").GetProperty("status").GetString()=="received","Legacy receipt status normalized without hydration");
 Check((await Rows("warehouse","Warehouse","WAREHOUSE")).GetArrayLength()==4,"Warehouse retains full destination scope");
 Check((await Rows("owner","GroupAdministrator","")).GetArrayLength()==3,"Supervisor sees only owned requests");
 Check((await Rows("stranger","GroupAdministrator","")).GetArrayLength()==0,"Unrelated supervisor cannot see queue records");
 Check((await Rows("technician","GeneralServices","TECHNICIANS")).GetArrayLength()==0,"Technician does not gain department-wide access");
 Check((await Rows("denied","Unknown","")).GetArrayLength()==0,"Unknown role rejected");
} finally {
 SqlConnection.ClearAllPools();await Exec(root,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
}
