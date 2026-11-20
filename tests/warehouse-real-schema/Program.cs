using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

var name="Codex_WarehouseSchema_"+Guid.NewGuid().ToString("N");
if(name.Length!=54||!name.StartsWith("Codex_WarehouseSchema_"))throw new Exception("Invalid disposable database");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");
await master.OpenAsync();bool created=false;
try{
 await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Already exists',1; CREATE DATABASE [{name}];");created=true;
 var connectionString=$"Server=localhost;Database={name};Integrated Security=True;User Id=QA;TrustServerCertificate=True";
 await using var c=new SqlConnection(connectionString);await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE agencies(id char(36) PRIMARY KEY,codigo nvarchar(120),terminal nvarchar(120),grupo nvarchar(120));
 CREATE TABLE maintenance_movements(id uniqueidentifier PRIMARY KEY,agency_id char(36) NULL,department varchar(40),movement_type varchar(40),equipment_type nvarchar(240),component_type nvarchar(100),serial_number nvarchar(120),quantity int,created_by_name nvarchar(160),created_at datetime2 DEFAULT SYSUTCDATETIME());
 INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,created_by_name)
 VALUES(NEWID(),'WAREHOUSE','ENTRY',REPLICATE(N'A',200),'LEGACY-SERIAL-1',1,N'QA');
 """);
 if(args.Contains("--existing-catalog"))await Exec(c,"""
 CREATE TABLE maintenance_products(id uniqueidentifier PRIMARY KEY DEFAULT NEWID(),department varchar(40) NOT NULL,scan_code nvarchar(120) NOT NULL,product_name nvarchar(160) NOT NULL,component_type nvarchar(100),created_by_name nvarchar(160) NOT NULL,created_at datetime2 DEFAULT SYSUTCDATETIME(),updated_at datetime2 DEFAULT SYSUTCDATETIME(),CONSTRAINT UQ_maintenance_products_department_code UNIQUE(department,scan_code));
 CREATE INDEX IX_maintenance_products_name ON maintenance_products(department,product_name);
 INSERT maintenance_products(department,scan_code,product_name,created_by_name) VALUES('TECHNOLOGY','EXISTING','Producto existente','QA');
 """);
 var assembly=Assembly.Load("RegistroAgencias.SqlServer.V418");var type=assembly.GetType("Database")!;
 var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",connectionString}}).Build();
 var db=Activator.CreateInstance(type,config)!;
 var actor=Activator.CreateInstance(assembly.GetType("DocumentActor")!,["qa","Administrator","","QA","qa",null,null])!;
 var result=(Task)type.GetMethod("WarehouseAvailableInventory")!.Invoke(db,[actor,CancellationToken.None])!;
 await result;
 await using(var check=new SqlCommand("SELECT product_name FROM maintenance_products WHERE scan_code='LEGACY-SERIAL-1'",c)){
  if((string?)await check.ExecuteScalarAsync()!=new string('A',200))throw new Exception("Equipment name was lost");
 }
 if(args.Contains("--existing-catalog"))await using(var check=new SqlCommand("SELECT COUNT(*) FROM maintenance_products WHERE scan_code='EXISTING' AND product_name=N'Producto existente'",c)){
  if((int)(await check.ExecuteScalarAsync())! !=1)throw new Exception("Existing catalog altered");
 }
 Console.WriteLine("PASS real API inventory initialization with long legacy equipment names");
 var jsonOptions=new JsonSerializerOptions(JsonSerializerDefaults.Web);
 var departments=new Dictionary<string,string>{{"TECHNOLOGY","Tecnología"},{"GENERAL_SERVICES","Servicios Generales"},{"HUMAN_RESOURCES","Recursos Humanos"},{"WAREHOUSE","Almacén"},{"WORKSHOP","Taller"},{"CALL_CENTER","Call Center"},{"TECHNICAL_FAILURE","Avería Técnica"},{"ADMINISTRATIVE","Administración"},{"COMMUNICATION","Comunicación"},{"COMMERCIALIZATION","Comercialización"},{"TREASURY","Tesorería"},{"ACCOUNTING","Contabilidad"},{"SUPERVISION","Supervisión"}};
 foreach(var (code,label) in departments){
  var owner="qa."+code;
  var role=code switch{"TECHNOLOGY" or "WORKSHOP" or "CALL_CENTER" or "TECHNICAL_FAILURE"=>"Technology","GENERAL_SERVICES"=>"GeneralServices","HUMAN_RESOURCES"=>"HumanResources","WAREHOUSE"=>"Warehouse",_=>"Department"};
  var account=Activator.CreateInstance(assembly.GetType("DocumentActor")!,[owner,role,code,"QA",owner,label,null])!;
  var requestJson=JsonSerializer.Serialize(new {number="SOL-ALM-QA-"+code,agencyId=Guid.Empty,agencyCode="",agencyName="",agencyGroup="",installerId="",installerName="",priority="MEDIUM",destinationType="DEPARTMENT",lines=new[]{new{kind="EQUIPMENT",name="Solicitud QA",quantity=1}}});
  var request=JsonSerializer.Deserialize(requestJson,assembly.GetType("WarehouseRequestInfo")!,jsonOptions)!;
  var canonical=await Call("WarehouseCanonicalRequest",request,account,label,CancellationToken.None);
  if(canonical is null)throw new Exception("Request rejected for "+code);
  var document=JsonSerializer.Deserialize(JsonSerializer.Serialize(new{ownerUserName=owner,ownerDisplayName="QA",ownerLogin=owner,department=label,area="communications",channel="requirements",recipientDepartment="Almacén",title="QA",contentType="application/pdf",originalFileName="qa.pdf",warehouseRequest=canonical}),assembly.GetType("InstitutionalFile")!,jsonOptions)!;
  var id=(long)(await Call("InstitutionalUpload",document,"%PDF-1.7 QA"u8.ToArray(),CancellationToken.None))!;
  if(id<=0)throw new Exception("Request was not saved for "+code);
  Console.WriteLine("PASS actual API SQL request save: "+code);
 }
 async Task<object?> Call(string method,params object[] arguments){var task=(Task)type.GetMethod(method)!.Invoke(db,arguments)!;await task;return task.GetType().GetProperty("Result")?.GetValue(task);}
}finally{SqlConnection.ClearAllPools();if(created)await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");}
static async Task Exec(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
