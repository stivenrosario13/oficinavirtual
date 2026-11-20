using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var name="Codex_AdminDelivery_QA_"+Guid.NewGuid().ToString("N");
if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^Codex_AdminDelivery_QA_[a-f0-9]{32}$"))throw new Exception("Invalid temporary database");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();var created=false;var checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
try{
 await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Refusing existing database',1;CREATE DATABASE [{name}];");created=true;
 var connection=$"Server=localhost;Database={name};Integrated Security=True;User Id=qa;TrustServerCertificate=True;Connect Timeout=5";
 await using var c=new SqlConnection(connection);await c.OpenAsync();
 await Exec(c,"CREATE TABLE institutional_files(id bigint PRIMARY KEY,metadata nvarchar(max));");
 var serverAssembly=Directory.GetFiles(AppContext.BaseDirectory,"RegistroAgencias.SqlServer.V*.dll").Single();
 var assembly=Assembly.LoadFrom(serverAssembly);
 var type=assembly.GetType("Database")!;
 var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",connection}}).Build();
 var db=Activator.CreateInstance(type,config)!;
 type.GetField("institutionalSchemaReady",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
 var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
 object Object(string kind,object value)=>JsonSerializer.Deserialize(JsonSerializer.Serialize(value,options),assembly.GetType(kind)!,options)!;
 var actor=Object("DocumentActor",new{id="admin",role="Administrator",team="",name="Administrador QA",login="qa@example.invalid"});
 async Task<string?> Deliver(long id,string? qr,Guid? asset=null,object? who=null){
  var input=Object("AdministrativeDeliveryInput",new{assetId=asset,receivedBy="Persona QA",sealQr=qr});
  return await (Task<string?>)type.GetMethod("DeliverAdministrativeRequest")!.Invoke(db,[id,who??actor,input,CancellationToken.None])!;
 }
 async Task Seed(long id,string state="approved",string assetType="OTRO",string formType="GENERAL"){
  var data=new{area="communications",channel="requirements",status=state,department="Tecnología",recipientDepartment="Administración",ownerUserName="creator",verificationToken=new string('a',48),receivedUtc=DateTime.UtcNow,decisionUtc=DateTime.UtcNow,administrativeRequest=new{number=$"SOL-ADM-QA-{id}",assetType,targetType="AGENCIA",targetName="Agencia QA",formType}};
  await using var cmd=new SqlCommand("INSERT institutional_files VALUES(@id,@json)",c);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@json",JsonSerializer.Serialize(data,options));await cmd.ExecuteNonQueryAsync();
 }
 async Task<JsonDocument> Read(long id){await using var cmd=new SqlCommand("SELECT metadata FROM institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);return JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync())!);}
 var qr="https://example.invalid/api/institutional-documents/verify/"+new string('a',48);
 await Seed(1);
 Check(await Deliver(1,null) is not null,"Missing QR rejected");
 Check(await Deliver(1,"invalid") is not null,"Invalid QR rejected");
 Check(await Deliver(1,new string('b',48)) is not null,"Another request QR rejected");
 Check(!(await Read(1)).RootElement.TryGetProperty("administrativeDelivery",out _),"Rejected scans leave request unchanged");
 await Seed(2,"pending");Check(await Deliver(2,qr) is not null,"Unapproved request rejected");
 var other=Object("DocumentActor",new{id="other",role="Technology",team="",name="Otro",login="other@example.invalid"});
 Check(await Deliver(1,qr,who:other) is not null,"Other department cannot deliver");
 Check(await Deliver(1,qr) is null,"Authorized request delivered using its seal QR");
 var saved=(await Read(1)).RootElement;Check(saved.GetProperty("administrativeDelivery").GetProperty("receivedBy").GetString()=="Persona QA","Receiver saved");
 Check(saved.GetProperty("history").GetArrayLength()==1,"One delivery audit entry");
 Check(await Deliver(1,qr) is not null,"Repeated delivery rejected");
 Check((await Read(1)).RootElement.GetProperty("history").GetArrayLength()==1,"Repeated scan does not duplicate audit");
 var asset=Guid.NewGuid();
 await using(var cmd=new SqlCommand("INSERT administrative_assets(id,kind,label,identifier,status,created_utc,updated_utc) VALUES(@id,'MODEM','QA','MODEM-QA','DISPONIBLE',SYSUTCDATETIME(),SYSUTCDATETIME())",c)){cmd.Parameters.AddWithValue("@id",asset);await cmd.ExecuteNonQueryAsync();}
 await Seed(3,assetType:"MODEM");
 Check(await Deliver(3,new string('b',48),asset) is not null,"Wrong QR cannot assign inventory");
 await using(var cmd=new SqlCommand("SELECT status FROM administrative_assets WHERE identifier='MODEM-QA'",c))Check((string)(await cmd.ExecuteScalarAsync())! =="DISPONIBLE","Inventory unchanged after wrong QR");
 Check(await Deliver(3,qr,asset) is null,"Valid QR assigns selected modem");
 await using(var cmd=new SqlCommand("SELECT status FROM administrative_assets WHERE identifier='MODEM-QA'",c))Check((string)(await cmd.ExecuteScalarAsync())! =="ASIGNADO","Inventory assigned only after validated scan");
 await Seed(4);var gledys=Object("DocumentActor",new{id="gledys",role="Administrative",team="",name="Gledys Tejeda",login="gledys.tejeda"});
 Check(await Deliver(4,"https://example.invalid/api/institutional-documents/lookup/SOL-ADM-QA-99",who:gledys) is not null,"Another form number cannot deliver");
 Check(await Deliver(4,"https://example.invalid/api/institutional-documents/lookup/SOL-ADM-QA-4",who:gledys) is null,"Gledys delivers using main form QR");
 Check((await Read(4)).RootElement.GetProperty("administrativeDelivery").GetProperty("deliveredLogin").GetString()=="gledys.tejeda","Gledys identity retained on delivery");
 await Seed(5);Check(await Deliver(5,qr,who:gledys) is null,"Gledys delivers using authorized seal QR");
 await Seed(6);var sandra=Object("DocumentActor",new{id="sandra",role="Administrative",team="",name="Sandra",login="sandra.ramirez"});
 Check(await Deliver(6,qr,who:sandra) is not null,"Existing administrative form restrictions preserved");
 await Seed(7,"pending");Check(await Deliver(7,"SOL-ADM-QA-7",who:gledys) is not null,"Main form QR cannot bypass authorization");
 long next=20;
 foreach(var (login,form) in new[]{("gledys.tejeda","GENERAL"),("emma.mendoza","SIM"),("derihan.alisha","DEVICE"),("whilmaly.sulay","GENERAL"),("emely.figueroa","CONTRACT_REQUEST"),("diana.ventura","CONTRACT_REQUEST")}){
  var who=Object("DocumentActor",new{id=login,role="Administrative",team="",name=login,login});
  foreach(var scan in new[]{qr,qr.ToUpperInvariant(),Uri.EscapeDataString(qr),"\r\n"+qr+"\t",new string('a',48),"httpsÇ--example.invalid-api-institutional'documents-verify-"+new string('a',48)}){
   var id=next++;await Seed(id,formType:form);Check(await Deliver(id,scan,who:who) is null,login+" reads seal variant "+id);
   Check(await Deliver(id,scan,who:who) is not null,login+" repeated scan rejected "+id);
  }
 }
 foreach(var login in new[]{"yesica.mercedes","yessica.mercedes","lucianny.gonzalez","lucianny.gonzales"}){
  await Seed(next,formType:"INCIDENT_REPORT");var who=Object("DocumentActor",new{id=login,role="Administrative",team="",name=login,login});
  Check(await Deliver(next++,qr,who:who) is not null,login+" does not register asset delivery for incident reports");
 }
 await Seed(next,formType:"FUEL_LOG");Check(await Deliver(next++,qr,who:sandra) is not null,"Registration-only fuel log cannot enter delivery workflow");
 foreach(var role in new[]{"Department","Viewer","GroupAdministrator","Fiscalizador","Technology","GeneralServices","HumanResources","Warehouse"}){
  await Seed(next);var who=Object("DocumentActor",new{id=role,role,team="",name=role,login=role});Check(await Deliver(next++,qr,who:who) is not null,role+" cannot register an administrative delivery");
 }
 await Seed(next);var simultaneous=await Task.WhenAll(Deliver(next,qr),Deliver(next,qr));Check(simultaneous.Count(x=>x is null)==1,"Simultaneous scans register exactly one delivery");Check((await Read(next++)).RootElement.GetProperty("history").GetArrayLength()==1,"Simultaneous scans preserve one audit entry");
 foreach(var state in new[]{"rejected","received","correction_requested","draft"}){await Seed(next,state);Check(await Deliver(next++,qr) is not null,state+" cannot bypass authorization");}
 Console.WriteLine($"SUCCESS {checks} checks");
}finally{if(created){SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}];");}}
static async Task Exec(SqlConnection connection,string sql){await using var cmd=new SqlCommand(sql,connection);await cmd.ExecuteNonQueryAsync();}
