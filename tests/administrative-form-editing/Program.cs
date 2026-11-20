using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var apiAssembly=Directory.GetFiles(AppContext.BaseDirectory,"RegistroAgencias.SqlServer.V*.dll").OrderByDescending(Path.GetFileName).FirstOrDefault()
    ?? throw new FileNotFoundException("No se encontró la API compilada para la prueba.");
var assembly=Assembly.LoadFrom(apiAssembly);
var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
object Model(string type,object value)=>JsonSerializer.Deserialize(JsonSerializer.Serialize(value,options),assembly.GetType(type)!,options)!;
var checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
var owner=Model("DocumentActor",new{id="owner",role="Technology",team="TECHNICAL_FAILURE",name="QA",login="owner@example.invalid"});
var admin=Model("DocumentActor",new{id="admin",role="Administrator",team="",name="Admin QA",login="admin@example.invalid"});
var stranger=Model("DocumentActor",new{id="other",role="Technology",team="TECHNICAL_FAILURE",name="Other QA",login="other@example.invalid"});
var restricted=Model("DocumentActor",new{id="sandra",role="Administrative",team="",name="Sandra QA",login="sandra.ramirez@grupotejeda.local"});
object Document(string status="pending",bool delivered=false,string target="Original",int revision=0)=>Model("InstitutionalFile",new{
    area="communications",channel="requirements",department="Tecnología",recipientDepartment="Administración",ownerUserName="owner",ownerLogin="owner@example.invalid",ownerDisplayName="QA",
    title="Formulario QA",description="Descripción QA",originalFileName="SOL-ADM-QA.pdf",contentType="application/pdf",status,administrativeRevision=revision,
    receivedUtc=status=="received"?DateTime.UtcNow:(DateTime?)null,receiptCode=status=="received"?"REC-QA":null,verificationToken=status=="received"?"token":null,
    administrativeRequest=new{number="SOL-ADM-QA-EDIT",assetType="OTRO",targetType="AGENCIA",targetName=target,reason="Prueba de formulario",formType="GENERAL",formData=new{targetName=target,reason="Prueba de formulario"}},
    administrativeDelivery=delivered?new{number="DEL-QA",deliveredBy="QA",deliveredLogin="qa",receivedBy="QA",at=DateTime.UtcNow}:null
});
bool CanEdit(object document,object actor)=>(bool)assembly.GetType("AdministrativeFormEditing")!.GetMethod("CanEdit")!.Invoke(null,[document,actor])!;
foreach(var state in new[]{"pending","received","correction_requested","rejected"})Check(CanEdit(Document(state),owner),"Owner edits "+state);
foreach(var state in new[]{"approved","cancelled","draft"})Check(!CanEdit(Document(state),owner),"Closed state protected: "+state);
Check(!CanEdit(Document(delivered:true),admin),"Delivered forms protected even for administrator");
Check(!CanEdit(Document(),stranger),"Other department user cannot edit someone else's request");
Check(!CanEdit(Document(),restricted),"Assigned form restrictions are enforced");
Check(CanEdit(Document(),admin),"Administrator can review assigned administrative requests");

var name="Codex_AdminForms_QA_"+Guid.NewGuid().ToString("N");
if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^Codex_AdminForms_QA_[a-f0-9]{32}$"))throw new Exception("Invalid temporary database name");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();bool created=false;
async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
try{
    await Exec(master,$"CREATE DATABASE [{name}]");created=true;
    var connection=$"Server=localhost;Database={name};Integrated Security=True;User Id=qa;TrustServerCertificate=True;Connect Timeout=5";
    await using var c=new SqlConnection(connection);await c.OpenAsync();
    await Exec(c,"CREATE TABLE dbo.institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80),department nvarchar(80),area nvarchar(30),channel nvarchar(30),metadata nvarchar(max),file_data varbinary(max)); CREATE TABLE dbo.admin_users(id nvarchar(80) PRIMARY KEY,role varchar(40),support_team varchar(40),is_active bit);");
    var type=assembly.GetType("Database")!;
    var db=Activator.CreateInstance(type,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",connection}}).Build())!;
    type.GetField("institutionalSchemaReady",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
    // Storage bytes are inert markers; PDF generation and format validation are tested separately.
    async Task<long> Upload(object document)=>await (Task<long>)type.GetMethod("InstitutionalUpload")!.Invoke(db,[document,"original bytes"u8.ToArray(),CancellationToken.None])!;
    async Task<string?> Edit(long id,int revision,object actor,object document)=>await (Task<string?>)type.GetMethod("InstitutionalEditAdministrativeForm")!.Invoke(db,[id,revision,actor,document,"edited bytes"u8.ToArray(),CancellationToken.None])!;
    async Task<JsonElement> Read(long id){await using var cmd=new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);using var doc=JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync())!);return doc.RootElement.Clone();}
    var id=await Upload(Document("received"));
    Check(await Upload(Document("received"))==id,"Lost-response retry returns the same document id");
    Check(await Edit(id,0,stranger,Document(target:"Denied")) is not null,"Edit access enforced inside SQL transaction");
    Check(await Edit(id,0,owner,Document(target:"Updated")) is null,"Owner updates existing record");
    var saved=await Read(id);
    Check(saved.GetProperty("administrativeRequest").GetProperty("number").GetString()=="SOL-ADM-QA-EDIT","Folio preserved");
    Check(saved.GetProperty("administrativeRequest").GetProperty("targetName").GetString()=="Updated","Editable data updated");
    Check(saved.GetProperty("status").GetString()=="pending"&&saved.GetProperty("receivedUtc").ValueKind==JsonValueKind.Null,"Changed form requires new reception");
    Check(saved.GetProperty("history")[0].GetProperty("previousAdministrativeRequest").GetProperty("targetName").GetString()=="Original","Previous content retained in audit history");
    Check(await Edit(id,0,owner,Document(target:"Stale")) is not null,"Stale editor cannot overwrite a newer revision");
    var contenders=await Task.WhenAll(Edit(id,1,owner,Document(target:"Concurrent A")),Edit(id,1,owner,Document(target:"Concurrent B")));
    Check(contenders.Count(result=>result is null)==1,"Exactly one concurrent edit wins");
    Check((await Read(id)).GetProperty("administrativeRevision").GetInt32()==2,"Revision increments once per accepted edit");
    // Exercise the real transactional paths against this disposable database only.
    foreach(var field in new[]{"maintenanceSchemaReady","warehouseFulfillmentSchemaReady","ticketWorkflowSchemaReady","agencyDirectorySchemaReady"})type.GetField(field,BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
    await Exec(c,"""
      CREATE TABLE warehouse_products(product_id nvarchar(40) PRIMARY KEY,description nvarchar(240),official_stock int,imported_at datetime2,is_active bit);
      CREATE TABLE warehouse_equipment_allocations(id bigint IDENTITY PRIMARY KEY,document_id bigint,line_index int,department varchar(40),product_id nvarchar(40),equipment_name nvarchar(240),serial_number nvarchar(120),quantity int,status varchar(20));
      CREATE TABLE maintenance_movements(id uniqueidentifier PRIMARY KEY,agency_id char(36),ticket_id uniqueidentifier,department varchar(40),technician_user_id nvarchar(80),technician_name nvarchar(160),movement_type varchar(40),equipment_type nvarchar(240),component_type nvarchar(100),failure_cause nvarchar(200),serial_number nvarchar(120),quantity int,notes nvarchar(4000),created_by_user_id nvarchar(80),created_by_name nvarchar(160),created_at datetime2 DEFAULT SYSUTCDATETIME(),operational_area varchar(20),document_number varchar(40) UNIQUE,qr_token nvarchar(200) UNIQUE,delivered_by_name nvarchar(160),received_by_name nvarchar(160),destination_name nvarchar(200),signature_data nvarchar(max),occurred_at datetime2);
      CREATE TABLE warehouse_repaired_receipts(movement_id uniqueidentifier PRIMARY KEY,source_id uniqueidentifier);
      CREATE TABLE agencies(id char(36) PRIMARY KEY,codigo nvarchar(40),terminal nvarchar(120),grupo nvarchar(120),directory_direccion nvarchar(200),directory_sector nvarchar(100),directory_municipio nvarchar(100),directory_provincia nvarchar(100),expected_latitude float,expected_longitude float,region nvarchar(100),is_active bit);
      CREATE TABLE agency_profiles(agency_id char(36),direccion nvarchar(200),sector nvarchar(100),municipio nvarchar(100),provincia nvarchar(100),latitude float,longitude float);
      CREATE TABLE admin_user_groups(user_id nvarchar(80),group_name nvarchar(120));
      INSERT warehouse_products VALUES('P1',N'Router QA',10,'2020-01-01',1),('P0',N'Agotado QA',0,'2020-01-01',1);
      INSERT agencies(id,codigo,terminal,grupo,is_active) VALUES('11111111-1111-1111-1111-111111111111','001','Agencia QA','Grupo QA',1);
      """);
    object WarehouseActor(string login,string role="Warehouse")=>Model("DocumentActor",new{id=login,role,team="WAREHOUSE",name=login,login=login+"@grupotejeda.local"});
    var diorly=WarehouseActor("diorly.abreu");var ana=WarehouseActor("ana.rodriguez");var bryan=WarehouseActor("bryan.arias");
    var vittini=Model("DocumentActor",new{id="vittini",role="Technology",team="TECHNICAL_FAILURE",name="Vittini",login="rosmarlin.vittini@grupotejeda.local"});
    object Line(string product="P1",int quantity=4)=>new{kind="EQUIPMENT",productId=product,name=product=="P1"?"Router QA":"Agotado QA",quantity};
    object WarehouseDocument(bool loan=false,bool delivered=false,string owner="vittini",string ownerName="Vittini",string ownerLogin="rosmarlin.vittini@grupotejeda.local")=>Model("InstitutionalFile",new{area="communications",channel="requirements",status=delivered?"approved":"received",department="Tecnología",recipientDepartment="Almacén",ownerUserName=owner,ownerDisplayName=ownerName,ownerLogin,originalFileName="SOL-ALM-QA.pdf",contentType="application/pdf",warehouseRequest=new{number="SOL-ALM-QA",destinationType="DEPARTMENT",agencyId=Guid.Empty,agencyCode="USO INTERNO",agencyName="Tecnología",agencyGroup="QA",installerId="",installerName="",priority="MEDIUM",lines=new[]{Line()},weekendLoan=loan?new{from="2026-10-30",to="2026-11-01",returnBy="2026-11-02"}:null},warehouseAssigned=delivered?new[]{new{lineIndex=0,name="Router QA",productId="P1",serial="CONTROL POR CANTIDAD",quantity=10}}:[],warehouseDelivery=delivered?new{number="DEL-QA",deliveredBy="Ana",deliveredLogin="ana.rodriguez",receivedBy="Vittini",at=DateTime.UtcNow}:null});
    async Task<long> Insert(object document,string owner="vittini"){await using var cmd=new SqlCommand("INSERT institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,N'Tecnología','communications','requirements',@json,0x01)",c);cmd.Parameters.AddWithValue("@owner",owner);cmd.Parameters.AddWithValue("@json",JsonSerializer.Serialize(document,options));return Convert.ToInt64(await cmd.ExecuteScalarAsync());}
    await Exec(c,"INSERT dbo.admin_users VALUES('manauri','TECHNOLOGY','TECHNICAL_FAILURE',1),('other-tech','TECHNOLOGY','TECHNICIANS',1);");
    var manauriId=await Insert(WarehouseDocument(owner:"manauri",ownerName:"Manauri",ownerLogin:"manauri@grupotejeda.local"),"manauri");
    async Task<bool> CanList(long expected,object actor){var task=(Task)type.GetMethod("InstitutionalList")!.Invoke(db,[actor,"communications","requirements",CancellationToken.None,false])!;await task;var rows=(System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;return rows.Cast<object>().Any(row=>Convert.ToInt64(row.GetType().GetProperty("Id")!.GetValue(row))==expected);}
    Check(await CanList(manauriId,vittini),"Vittini sees warehouse requests from active Technical Failure users");
    Check(!await CanList(manauriId,stranger),"Other Technical Failure users do not receive Vittini team-wide visibility");
    async Task<string?> WarehouseEdit(long docId,object actor,int revision=0,string product="P1",int quantity=4)=>await (Task<string?>)type.GetMethod("WarehouseEditRequest")!.Invoke(db,[docId,actor,Model("WarehouseRequestEdit",new{revision,note="Ajuste por disponibilidad",lines=new[]{Line(product,quantity)}}),"edited warehouse bytes"u8.ToArray(),CancellationToken.None])!;
    var warehouseId=await Insert(WarehouseDocument());
    Check(await WarehouseEdit(warehouseId,bryan) is not null,"Bryan cannot edit warehouse requests");
    Check(await WarehouseEdit(warehouseId,admin) is not null,"Administrator does not bypass named editor restriction");
    Check(await WarehouseEdit(warehouseId,diorly,product:"P0") is not null,"Out of stock article rejected");
    Check(await WarehouseEdit(warehouseId,diorly,quantity:11) is not null,"Overstock quantity rejected");
    Check(await WarehouseEdit(warehouseId,diorly) is null,"Diorly edits using available inventory");
    var warehouseSaved=await Read(warehouseId);
    Check(warehouseSaved.GetProperty("status").GetString()=="pending","Warehouse edit resets authorization");
    Check(warehouseSaved.GetProperty("history")[0].GetProperty("previousWarehouseRequest").GetProperty("lines")[0].GetProperty("productId").GetString()=="P1","Original warehouse request retained in audit");
    Check(await WarehouseEdit(warehouseId,ana) is not null,"Stale warehouse revision rejected");
    Check(await WarehouseEdit(warehouseId,ana,revision:1,quantity:3) is null,"Ana edits current revision");
    var loanId=await Insert(WarehouseDocument(loan:true,delivered:true));
    Check(await WarehouseEdit(loanId,ana) is not null,"Delivered loan cannot be edited");
    await Exec(c,"INSERT maintenance_movements(id,department,movement_type,equipment_type,component_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(NEWID(),'TECHNOLOGY','EXIT',N'Router QA','P1','',10,'WAREHOUSE','OUT-QA','OUT-QA',SYSUTCDATETIME());");
    object Report(int qty)=>Model("WeekendLoanReport",new{note="Uso y devolución del fin de semana",uses=new[]{new{assignedIndex=0,quantity=qty,agencyId="11111111-1111-1111-1111-111111111111"}}});
    async Task<string?> Loan(object actor,object? report,bool confirm=false)=>await (Task<string?>)type.GetMethod("WeekendLoanReport")!.Invoke(db,[loanId,actor,report,confirm,CancellationToken.None])!;
    Check(await Loan(ana,null,true) is not null,"Warehouse cannot confirm before usage report");
    Check(await Loan(stranger,Report(6)) is not null,"Other technical user cannot declare Vittini loan");
    Check(await Loan(vittini,Report(11)) is not null,"Use above delivered quantity rejected");
    Check(await Loan(vittini,Report(6)) is null,"Vittini declares six used and four remaining");
    Check(await Loan(vittini,Report(5)) is not null,"Repeated declaration cannot change a submitted report");
    Check(await Loan(vittini,null,true) is not null,"Vittini cannot credit warehouse inventory himself");
    var returns=await Task.WhenAll(Loan(ana,null,true),Loan(diorly,null,true));Check(returns.All(x=>x is null),"Concurrent confirmation retries safely");
    await using(var count=new SqlCommand("SELECT COUNT(*),SUM(quantity) FROM maintenance_movements WHERE document_number LIKE 'DEV-PRE-%'",c)){await using var r=await count.ExecuteReaderAsync();await r.ReadAsync();Check(r.GetInt32(0)==1&&r.GetInt32(1)==4,"Exactly four unused units credited once");}
    Check((await Read(loanId)).GetProperty("weekendLoanSettlement").GetProperty("confirmedAt").ValueKind==JsonValueKind.String,"Loan is closed after confirmed return");
    var weekendType=assembly.GetType("WeekendLoans")!;
    bool ValidPeriod(string from,string to,string returnBy)=>(bool)weekendType.GetMethod("Valid")!.Invoke(null,[Model("WeekendLoanPeriod",new{from,to,returnBy})])!;
    Check(ValidPeriod("2026-10-30","2026-11-01","2026-11-02"),"Friday to Sunday loan accepted");Check(!ValidPeriod("2026-10-28","2026-11-01","2026-11-02"),"Non-weekend loan interval rejected");
    foreach(var login in new[]{"sandra.ramirez","whilmaly.sulay"}){
      var fuel=Model("InstitutionalFile",new{ownerLogin=login+"@grupotejeda.local",status="pending",administrativeRequest=new{number="SOL-ADM-FUEL",assetType="OTRO",targetType="AGENCIA",targetName="Combustible",formType="FUEL_LOG"}});
      assembly.GetType("AdministrativeFuel")!.GetMethod("Normalize")!.Invoke(null,[fuel]);
      var fuelJson=JsonSerializer.SerializeToElement(fuel,options);Check(fuelJson.GetProperty("status").GetString()==(login=="sandra.ramirez"?"registered":"pending"),login+" fuel workflow normalized");Check(fuelJson.GetProperty("administrativeRequest").GetProperty("formType").GetString()==(login=="sandra.ramirez"?"FUEL_LOG":"GAS_LOG"),login+" historical form classified");
      foreach(var action in new[]{"receive","approve","sign"})Check((bool)assembly.GetType("AdministrativeFuel")!.GetMethod("BlocksAction")!.Invoke(null,[fuel,action])! == (login=="sandra.ramirez"),login+" workflow action "+action);
    }
    await Exec(c,"CREATE TABLE support_tickets(id uniqueidentifier,agency_id char(36),ticket_type varchar(20),assigned_department varchar(40),status varchar(40)); INSERT support_tickets VALUES('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','INTERNAL','TECHNOLOGY','OPEN');");
    async Task<bool> TicketAgency(Guid agency)=>await (Task<bool>)type.GetMethod("InternetTicketAgencyMatches")!.Invoke(db,[Guid.Parse("22222222-2222-2222-2222-222222222222"),agency,CancellationToken.None])!;
    Check(await TicketAgency(Guid.Parse("11111111-1111-1111-1111-111111111111")),"Modem request accepts matching internal ticket agency");
    Check(!await TicketAgency(Guid.NewGuid()),"Modem request rejects a different agency ID");
    await Exec(c,"UPDATE support_tickets SET status='CLOSED';");Check(!await TicketAgency(Guid.Parse("11111111-1111-1111-1111-111111111111")),"Modem request rejects closed ticket");
    Console.WriteLine($"PASS {checks} administrative and warehouse transactional checks");
}finally{
    SqlConnection.ClearAllPools();
    if(created)await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
}
