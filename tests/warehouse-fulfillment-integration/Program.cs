using Microsoft.Data.SqlClient;

var name="Codex_Warehouse_QA_"+Guid.NewGuid().ToString("N");
if(!name.StartsWith("Codex_Warehouse_QA_",StringComparison.Ordinal)||name.Length!=51)throw new Exception("Invalid disposable database name");
const string masterConnection="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
await using var master=new SqlConnection(masterConnection);
await master.OpenAsync();
bool created=false;
int checks=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
try
{
 await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Refusing to overwrite a database',1; CREATE DATABASE [{name}];");
 created=true;
 Database.Connection=$"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
 await using var connection=new SqlConnection(Database.Connection);
 await connection.OpenAsync();
 await Exec(connection,"""
   CREATE TABLE agencies(id char(36) PRIMARY KEY,codigo nvarchar(120),terminal nvarchar(120),grupo nvarchar(120));
   CREATE TABLE admin_users(id nvarchar(80) PRIMARY KEY,display_name nvarchar(160),role varchar(40),is_active bit);
   CREATE TABLE maintenance_movements(id uniqueidentifier PRIMARY KEY,agency_id char(36),ticket_id uniqueidentifier,department varchar(40),technician_user_id nvarchar(80),technician_name nvarchar(160),movement_type varchar(40),equipment_type nvarchar(100),component_type nvarchar(100),failure_cause nvarchar(200),serial_number nvarchar(120),quantity int,notes nvarchar(2000),created_by_user_id nvarchar(80),created_by_name nvarchar(160),created_at datetime2 DEFAULT SYSUTCDATETIME(),operational_area varchar(20),document_number varchar(40) UNIQUE,qr_token nvarchar(200) UNIQUE,delivered_by_name nvarchar(160),received_by_name nvarchar(160),destination_name nvarchar(200),signature_data nvarchar(max),occurred_at datetime2);
   INSERT INTO agencies VALUES('11111111-1111-1111-1111-111111111111','6010183','Agencia QA','Grupo QA');
   INSERT INTO admin_users VALUES('technician.sg','Tecnico SG','GENERAL_SERVICES',1);
   INSERT INTO maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at)
   VALUES(NEWID(),'GENERAL_SERVICES','ENTRY','Inversor','INV-1',1,'WAREHOUSE','ENTRY-1','REAL-MNT|ENTRY-1',SYSUTCDATETIME()),(NEWID(),'GENERAL_SERVICES','ENTRY','Inversor','INV-2',1,'WAREHOUSE','ENTRY-2','REAL-MNT|ENTRY-2',SYSUTCDATETIME()),(NEWID(),'GENERAL_SERVICES','ENTRY','Inversor','INV-3',1,'WAREHOUSE','ENTRY-3','REAL-MNT|ENTRY-3',SYSUTCDATETIME());
   """);
 await Exec(connection,"ALTER TABLE maintenance_movements ADD CONSTRAINT CK_maintenance_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));");
 var db=new Database();var ct=CancellationToken.None;
 var warehouse=new DocumentActor("warehouse","Technology","WAREHOUSE","Encargado Almacen","almacen.qa");
 var support=new DocumentActor("support.sg","GeneralServices","SUPPORT","Soporte SG","soporte.sg");
 var otherDepartment=new DocumentActor("support.tech","Technology","CALL_CENTER","Soporte Tech","soporte.tech");
 var technician=new DocumentActor("technician.sg","GeneralServices","TECHNICIANS","Tecnico SG","tecnico.sg");
 await db.WarehouseAvailableInventory(warehouse,ct);
 if(args.Contains("--failure-ticket")){
  await Exec(connection,"""
    CREATE TABLE support_departments(code varchar(40) PRIMARY KEY,is_active bit);
    CREATE TABLE support_categories(department_code varchar(40),code varchar(60),name nvarchar(160),is_active bit);
    CREATE TABLE support_tickets(id uniqueidentifier PRIMARY KEY,ticket_number bigint IDENTITY(1,1),agency_id char(36),category varchar(60),assigned_department varchar(40),assigned_team varchar(40) NULL,priority varchar(20),status varchar(40),subject nvarchar(180),description nvarchar(2000),created_by_user_id nvarchar(80),created_by_name nvarchar(160),created_by_username nvarchar(320),ticket_type varchar(20),assigned_technician_id nvarchar(80) NULL);
    INSERT support_departments VALUES('GENERAL_SERVICES',1),('TECHNOLOGY',1);
    INSERT support_categories VALUES('GENERAL_SERVICES','EQUIPMENT_FAILURE',N'Falla de equipo',1);
    INSERT support_categories VALUES('TECHNOLOGY','TECH_FAILURE',N'Falla tecnológica',1);
    """);
  var request=new WarehouseRequestInfo("SOL-ALM-20261014-170000-ABCD",Guid.Parse("11111111-1111-1111-1111-111111111111"),"6010183","Agencia QA","Grupo QA","","","MEDIUM",[new("EQUIPMENT","Inversor",1,null,null)],FailureDepartment:"GENERAL_SERVICES",FailureCategory:"EQUIPMENT_FAILURE",FailureDescription:"El equipo no enciende");
  var canonical=await db.WarehouseCanonicalRequest(request,support,support.Department,ct);
  Check(canonical is not null,"Agency-specific failure request is canonical");
  InstitutionalFile Form()=>new(){Area="communications",Channel="requirements",Title="Falla de equipo",Description="Solicitud con falla",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,OwnerLogin=support.Login,RecipientDepartment="Almacén",WarehouseRequest=canonical,OriginalFileName=request.Number+".pdf",ContentType="application/pdf"};
  var form=Form();var documentId=await db.InstitutionalUpload(form,"%PDF-QA"u8.ToArray(),ct);
  Check(form.WarehouseRequest?.TicketId is not null&&form.WarehouseRequest.TicketNumber is >0,"Ticket and request are linked in one commit");
  await using(var type=new SqlCommand("SELECT ticket_type FROM support_tickets WHERE id=@id",connection)){type.Parameters.AddWithValue("@id",form.WarehouseRequest!.TicketId!.Value);Check((string?)await type.ExecuteScalarAsync(ct)=="INTERNAL","Warehouse failure appears under internal tickets, not supervisor tickets");}
   Check((await db.InstitutionalDetail(documentId,warehouse,ct))?.WarehouseRequest?.TicketId==form.WarehouseRequest!.TicketId,"Warehouse sees the linked ticket");
  var trace=System.Text.Json.JsonSerializer.Serialize(await db.WarehouseTrace(request.Number,support,ct));
  Check(trace.Contains("ticketNumber",StringComparison.Ordinal)&&trace.Contains("Ticket #",StringComparison.Ordinal),"QR trace reports the linked ticket and status");
  var repeated=Form();Check(await db.InstitutionalUpload(repeated,"%PDF-QA"u8.ToArray(),ct)==documentId&&repeated.WarehouseRequest?.TicketId==form.WarehouseRequest.TicketId,"Retry reuses the same ticket and request");
  await using(var count=new SqlCommand("SELECT COUNT(*) FROM support_tickets",connection))Check(Convert.ToInt32(await count.ExecuteScalarAsync(ct))==1,"Retry creates no duplicate ticket");
  await Exec(connection,"INSERT agencies VALUES('22222222-2222-2222-2222-222222222222','6010184','Segunda agencia QA','Grupo QA');");
  var secondRequest=request with{Number="SOL-ALM-20261014-170002-ABCD",AgencyId=Guid.Parse("22222222-2222-2222-2222-222222222222")};
  var secondCanonical=await db.WarehouseCanonicalRequest(secondRequest,support,support.Department,ct);
  var second=Form();second.WarehouseRequest=secondCanonical;second.OriginalFileName=secondRequest.Number+".pdf";
  var secondDocumentId=await db.InstitutionalUpload(second,"%PDF-QA"u8.ToArray(),ct);
  Check(secondDocumentId!=documentId&&second.WarehouseRequest?.TicketId!=form.WarehouseRequest?.TicketId,"Second agency receives its own document and ticket");
  await using(var count=new SqlCommand("SELECT COUNT(*) FROM support_tickets",connection))Check(Convert.ToInt32(await count.ExecuteScalarAsync(ct))==2,"Two agencies keep separate ticket histories");
   var invalid=Form();invalid.WarehouseRequest=canonical! with{Number="SOL-ALM-20261014-170001-ABCD",FailureCategory="UNKNOWN"};
  try{await db.InstitutionalUpload(invalid,"%PDF-QA"u8.ToArray(),ct);throw new Exception("Invalid failure category accepted");}catch(WarehouseFailureValidationException){Check(true,"Invalid failure rejects the whole transaction");}
  await using(var count=new SqlCommand("SELECT COUNT(*) FROM support_tickets",connection))Check(Convert.ToInt32(await count.ExecuteScalarAsync(ct))==2,"Invalid failure leaves no ticket");
  foreach(var team in new[]{"CALL_CENTER","TECHNICAL_FAILURE"}){
   var actor=new DocumentActor("qa."+team,"Technology",team,"Soporte "+team,"qa."+team);
   var source=team=="CALL_CENTER"?"Call Center":"Avería Técnica";
   var routeRequest=request with{Number="SOL-ALM-QA-"+team,FailureDepartment="TECHNOLOGY",FailureCategory="TECH_FAILURE",FailureTeam="CALL_CENTER"};
   Check(WarehouseRequestSource.RouteFailure(routeRequest with{FailureDepartment="HUMAN_RESOURCES"},actor,source) is null,"Non-maintenance department is rejected: "+team);
   var routed=WarehouseRequestSource.RouteFailure(routeRequest,actor,source);
   Check(routed?.FailureTeam==team,"Authenticated team determines ticket inbox: "+team);
   var routeCanonical=await db.WarehouseCanonicalRequest(routed!,actor,source,ct);
   Check(routeCanonical is not null,"Team request keeps canonical agency: "+team);
   var routeForm=new InstitutionalFile{Area="communications",Channel="requirements",Title="Falla de equipo",Description="Solicitud con falla",Department=source,OwnerUserName=actor.Id,OwnerDisplayName=actor.Name,OwnerLogin=actor.Login,RecipientDepartment="Almacén",WarehouseRequest=routeCanonical,OriginalFileName=routeRequest.Number+".pdf",ContentType="application/pdf"};
   await db.InstitutionalUpload(routeForm,"%PDF-QA"u8.ToArray(),ct);
   await using var inbox=new SqlCommand("SELECT assigned_department,assigned_team,ticket_type FROM support_tickets WHERE id=@id",connection);inbox.Parameters.AddWithValue("@id",routeForm.WarehouseRequest!.TicketId!.Value);
   await using var inboxRow=await inbox.ExecuteReaderAsync(ct);Check(await inboxRow.ReadAsync(ct)&&inboxRow.GetString(0)=="TECHNOLOGY"&&inboxRow.GetString(1)==team&&inboxRow.GetString(2)=="INTERNAL","Internal ticket lands in correct team inbox: "+team);
  }
  var groupRequest=secondRequest with{Number="SOL-ALM-20261014-170003-ABCD",DestinationType="GROUP",FailureCategory="EQUIPMENT_FAILURE",FailureDescription="Equipo con falla en agencia del grupo"};
  var groupCanonical=await db.WarehouseCanonicalRequest(groupRequest,support,support.Department,ct);
  Check(groupCanonical?.AgencyId==groupRequest.AgencyId&&groupCanonical.AgencyGroup=="Grupo QA","Group request retains a real agency for its ticket");
  var groupForm=Form();groupForm.WarehouseRequest=groupCanonical;groupForm.OriginalFileName=groupRequest.Number+".pdf";
  await db.InstitutionalUpload(groupForm,"%PDF-QA"u8.ToArray(),ct);
  Check(groupForm.WarehouseRequest?.TicketId is not null&&groupForm.WarehouseRequest.TicketNumber is >0,"Group agency receives its own internal ticket");
  var wrongGroup=groupRequest with{Number="SOL-ALM-20261014-170004-ABCD",AgencyGroup="Grupo Ajeno"};
  Check(await db.WarehouseCanonicalRequest(wrongGroup,support,support.Department,ct) is null,"Group request cannot select an agency from another group");
  foreach(var code in new[]{"COMMUNICATION","COMMERCIALIZATION","TREASURY","ACCOUNTING","ADMINISTRATIVE"}){
   var actor=new DocumentActor("qa."+code,code=="ADMINISTRATIVE"?"Administrative":"Department",code,"QA "+code,"qa."+code,AssignedDepartment:DepartmentAccounts.Names[code]);
   var bulk=request with{Number="SOL-ALM-GROUP-"+code,AgencyId=Guid.Empty,DestinationType="GROUP",FailureDepartment=null,FailureCategory=null,FailureDescription=null};
   var bulkCanonical=await db.WarehouseCanonicalRequest(bulk,actor,actor.Department,ct);
   Check(bulkCanonical?.DestinationType=="GROUP"&&bulkCanonical.AgencyId==Guid.Empty&&bulkCanonical.AgencyGroup=="Grupo QA","Group without agency for "+code);
   Check(await db.WarehouseCanonicalRequest(bulk with{AgencyGroup="Missing"},actor,actor.Department,ct) is null,"Reject unknown group for "+code);
   var routed=WarehouseRequestSource.RouteFailure(request,actor,actor.Department);
   Check(routed?.FailureDepartment=="GENERAL_SERVICES"&&routed.FailureTeam is null,"Cross-department general services failure for "+code);
   var technology=WarehouseRequestSource.RouteFailure(request with{FailureDepartment="TECHNOLOGY",FailureCategory="TECH_FAILURE"},actor,actor.Department);
   Check(technology?.FailureTeam=="TECHNICAL_FAILURE","Cross-department technology failure for "+code);
   var bulkForm=new InstitutionalFile{Area="communications",Channel="requirements",Title="Grupo QA",Description="Solicitud de grupo",Department=actor.Department,OwnerUserName=actor.Id,OwnerDisplayName=actor.Name,OwnerLogin=actor.Login,RecipientDepartment="Almacén",WarehouseRequest=bulkCanonical,OriginalFileName=bulk.Number+".pdf",ContentType="application/pdf"};
   await db.InstitutionalUpload(bulkForm,"%PDF-QA"u8.ToArray(),ct);
   Check(bulkForm.Id>0&&bulkForm.WarehouseRequest?.TicketId is null,"Group document saved without synthetic ticket for "+code);
  }
  Console.WriteLine($"SUCCESS {checks} warehouse failure-ticket checks");return;
 }
 if(args.Contains("--all-departments")){
  var actors=new Dictionary<string,DocumentActor>{
   ["TECHNOLOGY"]=new("qa.tech","Technology","","QA","qa.tech"),
   ["GENERAL_SERVICES"]=support,
   ["HUMAN_RESOURCES"]=new("qa.hr","HumanResources","","QA","qa.hr"),
   ["WAREHOUSE"]=warehouse,
   ["WORKSHOP"]=new("qa.workshop","Technology","WORKSHOP","QA","qa.workshop"),
   ["CALL_CENTER"]=new("qa.call","Technology","CALL_CENTER","QA","qa.call"),
   ["TECHNICAL_FAILURE"]=new("qa.failure","Technology","TECHNICAL_FAILURE","QA","qa.failure")
  };
  foreach(var code in DepartmentAccounts.Names.Keys.Where(code=>!actors.ContainsKey(code)))actors[code]=new("qa."+code,"Department",code,"QA","qa."+code);
  var documents=new List<(string Code,long Id,DocumentActor Owner)>();
  foreach(var (code,actor) in actors){
   Check(actor.Allowed,"Document access: "+code);
   var input=new WarehouseRequestInfo("SOL-ALM-QA-"+code,Guid.Empty,"","","Grupo QA","","","MEDIUM",[new("EQUIPMENT","Material QA",1,null,null)],DestinationType:"DEPARTMENT");
   var canonical=await db.WarehouseCanonicalRequest(input,actor,actor.Department,ct);
   Check(canonical is not null,"Canonical request: "+code);
   var document=new InstitutionalFile{Area="communications",Channel="requirements",Title="QA "+code,Department=actor.Department,OwnerUserName=actor.Id,RecipientDepartment="Almacén",WarehouseRequest=canonical,OriginalFileName="QA.pdf",ContentType="application/pdf"};
   var docId=await db.InstitutionalUpload(document,"%PDF-QA"u8.ToArray(),ct);
   Check((await db.InstitutionalDetail(docId,actor,ct))?.WarehouseRequest?.Number==input.Number,"Owner reads own request: "+code);
   Check((await db.InstitutionalDetail(docId,warehouse,ct))?.Status=="pending","Warehouse receives request: "+code);
   documents.Add((code,docId,actor));
  }
  foreach(var document in documents)foreach(var (code,actor) in actors){
   if(actor.Manager||actor.Id==document.Owner.Id||actor.Department==document.Owner.Department)continue;
   Check(await db.InstitutionalDetail(document.Id,actor,ct) is null,"Cross-department document denied: "+code+" -> "+document.Code);
  }
  Console.WriteLine($"SUCCESS {checks} document checks across {actors.Count} departments; database layer only");return;
 }
 if(args.Contains("--request-source")){
  foreach(var team in new[]{"TECHNICAL_FAILURE","CALL_CENTER","WORKSHOP","TECHNICIANS",""}){
   var scope=team switch{"TECHNICAL_FAILURE"=>"Avería Técnica","CALL_CENTER"=>"Call Center","WORKSHOP"=>"Taller",_=>"Tecnología"};
   foreach(var assigned in new string?[]{null,scope}){
    var actor=new DocumentActor("qa.tech","Technology",team,"QA","qa.tech",assigned);
    var resolved=WarehouseRequestSource.Resolve(actor,"Tecnología");
    Check(resolved?.Department=="Tecnología"&&!resolved.Manager,"Technology ticket source accepted without granting manager: "+team+"/"+assigned);
    Check(WarehouseRequestSource.Resolve(actor,scope)?.Department==scope,"Own team request accepted: "+team);
    Check(WarehouseRequestSource.Resolve(actor,"Servicios Generales") is null,"Other department source denied: "+team);
    Check(WarehouseRequestSource.Resolve(actor,"Almacén") is null,"Cannot impersonate warehouse: "+team);
    Check(WarehouseRequestSource.Resolve(actor,"Tesorería") is null,"Cannot impersonate treasury: "+team);
   }
  }
  foreach(var actor in new[]{support,technician,new DocumentActor("qa.supervisor","GroupAdministrator","","QA","qa"),new DocumentActor("qa.account","Department","ACCOUNTING","QA","qa"),new DocumentActor("qa.admin","Administrative","","QA","qa")}){
   Check(WarehouseRequestSource.Resolve(actor,actor.Department)?.Department==actor.Department,"Own source retained: "+actor.Role);
   Check(WarehouseRequestSource.Resolve(actor,"Tecnología") is null,"Unrelated source denied: "+actor.Role);
  }
  Check(!technician.WarehouseRequestsBlocked,"SG technicians can request supplies");
  await Exec(connection,"CREATE TABLE support_tickets(id uniqueidentifier PRIMARY KEY,ticket_number bigint,agency_id char(36),assigned_department varchar(40),status varchar(40)); INSERT support_tickets VALUES('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',2091,'11111111-1111-1111-1111-111111111111','TECHNOLOGY','OPEN');");
  var manager=new DocumentActor("qa.manager","Technology","TECHNICAL_FAILURE","QA","qa.manager","Avería Técnica");
  Check(manager.Department!="Tecnología"&&!manager.Manager,"Reproduced previous source rejection for technical-failure manager");
  var ticketActor=WarehouseRequestSource.Resolve(manager,"Tecnología")!;
  var input=new WarehouseRequestInfo("SOL-ALM-SOURCE-QA",Guid.Parse("11111111-1111-1111-1111-111111111111"),"6010183","QA","Grupo QA","","","MEDIUM",[new("EQUIPMENT","Escáner",1,null,null)],Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),2091);
  var canonical=await db.WarehouseCanonicalRequest(input,ticketActor,ticketActor.Department,ct);
  Check(canonical?.TicketNumber==2091,"Technical-failure manager can request for a Technology ticket");
  var doc=new InstitutionalFile{Area="communications",Channel="requirements",Title="QA",Department=ticketActor.Department,OwnerUserName=manager.Id,RecipientDepartment="Almacén",WarehouseRequest=canonical,OriginalFileName="QA.pdf",ContentType="application/pdf"};
  var sourceId=await db.InstitutionalUpload(doc,"%PDF-QA"u8.ToArray(),ct);
  Check((await db.InstitutionalWarehouseSubmission(input.Number,manager,ct))?.Id==sourceId,"Manager recovers confirmation with original document scope");
  Check((await db.InstitutionalDetail(sourceId,warehouse,ct))?.WarehouseRequest?.TicketNumber==2091,"Warehouse receives linked ticket request");
  Console.WriteLine($"SUCCESS {checks} request source checks");return;
 }
 if(args.Contains("--ticket-request")){
  await Exec(connection,"""
   CREATE TABLE support_tickets(id uniqueidentifier PRIMARY KEY,ticket_number bigint,agency_id char(36),assigned_department varchar(40),status varchar(40));
   INSERT agencies VALUES('legacy-without-guid','2','Legacy','Grupo QA'),('22222222-2222-2222-2222-222222222222','6010183','Another agency','Other group');
   UPDATE agencies SET grupo=NULL WHERE id='11111111-1111-1111-1111-111111111111';
   INSERT support_tickets VALUES('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',42,'11111111-1111-1111-1111-111111111111','GENERAL_SERVICES','OPEN');
   """);
  var linked=new WarehouseRequestInfo("SOL-ALM-TICKET-QA",Guid.Parse("11111111-1111-1111-1111-111111111111"),"6010183","Agencia QA","","","","MEDIUM",[new("EQUIPMENT","Equipo manual",1500,null,null)],Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),42);
  var canonical=await db.WarehouseCanonicalRequest(linked,support,support.Department,ct);
  Check(canonical is {TicketNumber:42,AgencyGroup:""},"Ticket request accepts legacy null group without server error");
  Check(await db.WarehouseCanonicalRequest(linked with{AgencyId=Guid.Parse("22222222-2222-2222-2222-222222222222")},support,support.Department,ct) is null,"Duplicate agency code cannot link ticket to the wrong group");
  var document=new InstitutionalFile{Area="communications",Channel="requirements",Title="Ticket request",Department=support.Department,OwnerUserName=support.Id,RecipientDepartment="Almacén",WarehouseRequest=canonical,OriginalFileName="QA.pdf",ContentType="application/pdf"};
  var ticketDocumentId=await db.InstitutionalUpload(document,"%PDF-QA"u8.ToArray(),ct);
  Check((await db.InstitutionalDetail(ticketDocumentId,warehouse,ct))?.WarehouseRequest?.TicketId==linked.TicketId,"Warehouse receives request with its ticket link");
  await Exec(connection,"ALTER TABLE maintenance_movements ADD CONSTRAINT CK_maintenance_quantity CHECK(quantity BETWEEN 1 AND 1000);");
  try{await Exec(connection,"UPDATE maintenance_movements SET quantity=1500 WHERE serial_number='INV-1';");throw new Exception("Expected old quantity rejection");}catch(SqlException e) when(e.Number==547){Check(true,"Reproduced server-side rejection above 1000 units");}
  await db.TestRequestQuantitySchema(ct);
  await Exec(connection,"UPDATE maintenance_movements SET quantity=10000 WHERE serial_number='INV-1';");
  Check(true,"Updated schema accepts the request limit of 10000 units");
  try{await Exec(connection,"UPDATE maintenance_movements SET quantity=10001 WHERE serial_number='INV-1';");throw new Exception("Expected quantity rejection");}catch(SqlException e) when(e.Number==547){Check(true,"Quantity above request limit remains rejected");}
  Console.WriteLine($"SUCCESS {checks} ticket request checks");return;
 }
 if(args.Contains("--group-request")){
  await Exec(connection,"INSERT agencies VALUES('legacy-without-guid','2','Legacy',' Grupo legacy '),('33333333-3333-3333-3333-333333333333','3','Empty',NULL);");
  var groups=await db.WarehouseRequestGroups(warehouse,ct);
  Check(groups.Contains("Grupo QA")&&groups.Contains("Grupo legacy")&&groups.Count==2,"Group catalogue works with legacy IDs and empty group records");
  Check((await db.WarehouseRequestGroups(new("supervisor","GroupAdministrator","","Supervisor","qa"),ct)).Count==0,"Supervisors remain agency-only");
  Check((await db.WarehouseRequestGroups(new("administrative","Administrative","","Admin","qa"),ct)).Count==0,"Administrative users remain department-only");
  var groupInput=new WarehouseRequestInfo("SOL-ALM-GROUP-QA",Guid.Empty,"",""," Grupo QA ","","","MEDIUM",[new("EQUIPMENT","Artículo manual",1,null,null)],DestinationType:"GROUP");
  foreach(var actor in new[]{warehouse,support}){
   var canonical=await db.WarehouseCanonicalRequest(groupInput,actor,actor.Department,ct);
   Check(canonical is {AgencyCode:"GRUPO",AgencyGroup:"Grupo QA",DestinationType:"GROUP"}&&canonical.AgencyId==Guid.Empty,"Valid group needs no agency: "+actor.Department);
   var groupDocument=new InstitutionalFile{Area="communications",Channel="requirements",Title="Solicitud por grupo",Department=actor.Department,OwnerUserName=actor.Id,RecipientDepartment="Almacén",WarehouseRequest=canonical,OriginalFileName="GROUP.pdf",ContentType="application/pdf"};
   var groupDocumentId=await db.InstitutionalUpload(groupDocument,"%PDF-GROUP"u8.ToArray(),ct);
   Check((await db.InstitutionalDetail(groupDocumentId,warehouse,ct))?.WarehouseRequest?.AgencyGroup=="Grupo QA","Group request persisted and visible to warehouse");
  }
  Check(await db.WarehouseCanonicalRequest(groupInput with{AgencyGroup="Unknown"},warehouse,"Almacén",ct) is null,"Unknown group rejected");
  Check(await db.WarehouseCanonicalRequest(groupInput with{AgencyGroup="Grupo legacy"},warehouse,"Almacén",ct) is not null,"Group validation independent of agency ID conversion");
  Console.WriteLine($"SUCCESS {checks} group request checks");return;
 }
 if(args.Contains("--submission-all-roles")){
  var actors=new DocumentActor[]{
   new("qa.admin","Administrator","","Admin QA","qa.admin"),
   new("qa.supervisor","GroupAdministrator","","Supervisor QA","qa.supervisor"),
   new("qa.technology","Technology","TECHNICIANS","Tecnología QA","qa.technology"),
   new("qa.services","GeneralServices","","Servicios QA","qa.services"),
   new("qa.services.technician","GeneralServices","TECHNICIANS","Técnico SG QA","qa.services.technician"),
   new("qa.hr","HumanResources","","RRHH QA","qa.hr"),
   new("qa.administrative","Administrative","","Administrativo QA","qa.administrative"),
   new("qa.warehouse","Warehouse","","Almacén QA","qa.warehouse"),
   new("qa.workshop","Technology","WORKSHOP","Taller QA","qa.workshop"),
   new("qa.treasury","Department","TREASURY","Tesorería QA","qa.treasury"),
   new("qa.accounting","Department","ACCOUNTING","Contabilidad QA","qa.accounting")};
  foreach(var actor in actors){
   var request=new WarehouseRequestInfo("SOL-ALM-ALL-ROLES-QA",Guid.Empty,"USO INTERNO",actor.Department,"Grupo Tejeda","","","MEDIUM",[new("EQUIPMENT","Equipo QA",1,null,null)]);
   InstitutionalFile Submission()=>new(){Area="communications",Channel="requirements",Title="Solicitud QA",Department=actor.Department,OwnerUserName=actor.Id,OwnerDisplayName=actor.Name,OwnerLogin=actor.Login,RecipientDepartment="Almacén",WarehouseRequest=request,OriginalFileName="QA.pdf",ContentType="application/pdf"};
   var ids=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>db.InstitutionalUpload(Submission(),"%PDF-QA"u8.ToArray(),ct)));
   Check(ids[0]>0&&ids.Distinct().Count()==1,"Concurrent storage retries deduplicated: "+actor.Role+"/"+actor.Team);
   Check((await db.InstitutionalWarehouseSubmission(request.Number,actor,ct))?.Id==ids[0],"Owner can recover confirmation: "+actor.Id);
   Check(await db.InstitutionalWarehouseSubmission(request.Number,actor with{Id="unrelated-user"},ct) is null,"Another user cannot recover confirmation: "+actor.Id);
   await using var count=new SqlCommand("SELECT COUNT(*) FROM institutional_files WHERE owner_id=@owner",connection);
   count.Parameters.AddWithValue("@owner",actor.Id);
   Check(Convert.ToInt32(await count.ExecuteScalarAsync(ct))==1,"One stored request for owner: "+actor.Id);
  }
  Console.WriteLine($"SUCCESS {checks} storage and recovery checks across 10 role/team combinations; HTTP permissions and canonical validation are tested separately.");return;
 }
 if(args.Contains("--submission-confirmation")){
  var request=new WarehouseRequestInfo("SOL-ALM-CONFIRM-QA",Guid.Parse("11111111-1111-1111-1111-111111111111"),"6010183","Agencia QA","Grupo QA","","","MEDIUM",[new("EQUIPMENT","Equipo QA",1,null,null)],DestinationType:"AGENCY");
  InstitutionalFile Submission()=>new(){Area="communications",Channel="requirements",Title="Solicitud QA",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,OwnerLogin=support.Login,RecipientDepartment="Almacén",WarehouseRequest=request,OriginalFileName="QA.pdf",ContentType="application/pdf"};
  var initial=Submission();var confirmedId=await db.InstitutionalUpload(initial,"%PDF-QA"u8.ToArray(),ct);
  Check(confirmedId>0&&initial.Id==confirmedId,"Upload sets confirmed positive ID before response");
  var json=System.Text.Json.JsonSerializer.Serialize(initial.For(support),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
  Check(System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("id").GetInt64()==confirmedId,"HTTP response serializes camel-case id");
  var recoveredSubmission=await db.InstitutionalWarehouseSubmission(request.Number,support,ct);
  Check(recoveredSubmission?.Id==confirmedId&&recoveredSubmission.WarehouseRequest?.Number==request.Number,"Lost response recovered by exact request number");
  Check(await db.InstitutionalWarehouseSubmission(request.Number,otherDepartment,ct) is null,"Other user cannot recover owner's submission");
  var retryIds=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>db.InstitutionalUpload(Submission(),"%PDF-RETRY"u8.ToArray(),ct)));
  Check(retryIds.All(value=>value==confirmedId),"Concurrent retries return the original ID");
  await using(var count=new SqlCommand("SELECT COUNT(*) FROM institutional_files WHERE owner_id=@owner AND JSON_VALUE(metadata,'$.warehouseRequest.number')=@number",connection)){
   count.Parameters.AddWithValue("@owner",support.Id);count.Parameters.AddWithValue("@number",request.Number);Check(Convert.ToInt32(await count.ExecuteScalarAsync(ct))==1,"Exactly one request is persisted");
  }
  var changed=Submission();changed.Description="Different request";var conflict=false;
  try{await db.InstitutionalUpload(changed,"%PDF-CHANGED"u8.ToArray(),ct);}catch(WarehouseSubmissionConflictException){conflict=true;}
  Check(conflict,"Retry with different content does not overwrite existing request");
  var bytes=await db.InstitutionalBytes(confirmedId,support,ct);Check(bytes is not null&&System.Text.Encoding.UTF8.GetString(bytes.Value.Data)=="%PDF-QA","Original PDF preserved");
  Check((await db.InstitutionalList(warehouse,"communications","requirements",ct)).Any(item=>item.Id==confirmedId&&item.Status=="pending"),"Warehouse sees request pending reception");
  var freshIds=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>{var fresh=Submission();fresh.WarehouseRequest=request with{Number="SOL-ALM-CONCURRENT-QA"};return db.InstitutionalUpload(fresh,"%PDF-NEW"u8.ToArray(),ct);}));
  Check(freshIds.Distinct().Count()==1&&freshIds[0]!=confirmedId,"Simultaneous first submissions create exactly one new request");
  Console.WriteLine($"SUCCESS {checks} submission confirmation checks");return;
 }
 if(args.Contains("--new-departments-only")){
  await Exec(connection,"CREATE TABLE support_departments(code varchar(40) PRIMARY KEY,name nvarchar(160),description nvarchar(300),accent_color varchar(20),display_order int,is_active bit); CREATE TABLE support_categories(department_code varchar(40),code varchar(60),name nvarchar(160),description nvarchar(300),default_priority varchar(20),requires_detail bit,detail_label nvarchar(200),display_order int,is_active bit); INSERT support_departments VALUES('TECHNOLOGY',N'Tecnología original','QA','#000000',1,1);");
  var source=await File.ReadAllTextAsync("api/DatabaseSqlServerScoped.cs");
  var catalog=System.Text.RegularExpressions.Regex.Match(source,"const string administrativeCatalogSql=\"\"\"([\\s\\S]*?)\"\"\";").Groups[1].Value;
  Check(catalog.Length>0,"Catalog migration found");
  await Exec(connection,catalog);await Exec(connection,catalog);
  await using(var query=new SqlCommand("SELECT COUNT(*) FROM support_departments WHERE code IN('TREASURY','ACCOUNTING')",connection))Check(Convert.ToInt32(await query.ExecuteScalarAsync())==2,"New departments inserted once after repeated initialization");
  await using(var query=new SqlCommand("SELECT name FROM support_departments WHERE code='TECHNOLOGY'",connection))Check((string?)await query.ExecuteScalarAsync()=="Tecnología original","Existing department unchanged");
  await Exec(connection,"INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token) VALUES(NEWID(),'TREASURY','ENTRY','QA','QA-T',1,'WAREHOUSE','QA-T','QA-T'),(NEWID(),'ACCOUNTING','ENTRY','QA','QA-C',1,'WAREHOUSE','QA-C','QA-C');");
  await using(var query=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements",connection))Check(Convert.ToInt32(await query.ExecuteScalarAsync())==5,"Both departments accepted and original movements preserved");
  Console.WriteLine($"SUCCESS {checks} new department migration checks");return;
 }
 if(args.Contains("--maintenance-schema-only")){
  var source=await File.ReadAllTextAsync("api/DatabaseSqlServerScoped.cs");
  string Sql(string label)=>System.Text.RegularExpressions.Regex.Match(source,"const string "+label+"=\"\"\"([\\s\\S]*?)\"\"\";").Groups[1].Value;
  await Exec(connection,"UPDATE maintenance_movements SET created_by_name='QA'; INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,created_by_name,operational_area,document_number,qr_token,occurred_at) VALUES(NEWID(),'WAREHOUSE','NEW_DELIVERY','Monitor','WAREHOUSE-SERIAL',1,'QA','WAREHOUSE','WAREHOUSE-QA','WAREHOUSE-QA',SYSUTCDATETIME()),(NEWID(),'ADMINISTRATIVE','NEW_DELIVERY','Impresora','ADMIN-SERIAL',1,'QA','WAREHOUSE','ADMIN-QA','ADMIN-QA',SYSUTCDATETIME());");
  await Exec(connection,"CREATE TABLE maintenance_products(id uniqueidentifier DEFAULT NEWID() PRIMARY KEY,department varchar(40) NOT NULL,scan_code nvarchar(120) NOT NULL,product_name nvarchar(160) NOT NULL,component_type nvarchar(100),created_by_name nvarchar(160) NOT NULL,created_at datetime2 DEFAULT SYSUTCDATETIME(),updated_at datetime2 DEFAULT SYSUTCDATETIME(),CONSTRAINT UQ_maintenance_products_department_code UNIQUE(department,scan_code),CONSTRAINT CK_maintenance_products_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')));");
  try{await Exec(connection,Sql("seedCatalogSql"));throw new Exception("Expected old catalog constraint to fail");}
  catch(SqlException ex) when(ex.Number==547){Check(true,"Reproduced shared inventory/movements/procurement initialization failure: SQL 547 on old catalog constraint");}
  for(var run=0;run<2;run++)foreach(var label in new[]{"columnsSql","dependenciesSql","catalogSql","seedCatalogSql","procurementSql"})await Exec(connection,Sql(label));
  await using(var query=new SqlCommand("SELECT COUNT(*) FROM maintenance_products WHERE department IN('WAREHOUSE','ADMINISTRATIVE')",connection))Check(Convert.ToInt32(await query.ExecuteScalarAsync())==2,"Real maintenance migration preserves and loads warehouse and administrative serials after repeated initialization");
  await using(var query=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements",connection))Check(Convert.ToInt32(await query.ExecuteScalarAsync())==5,"Existing movements preserved");
  Console.WriteLine($"SUCCESS {checks} maintenance migration checks");return;
 }
 await Exec(connection,"""INSERT INTO warehouse_products(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,last_purchase_date,tracks_serial,catalog_version,imported_at,is_active) VALUES(N'QA-INVERSOR',N'Inversor',1,N'QA',0,N'UND',N'QA',N'Equipos',NULL,1,N'QA',DATEADD(day,-1,SYSUTCDATETIME()),1);""");
 var requestSequence=0;
 WarehouseRequestInfo Request(int quantity)=>new($"SOL-ALM-QA-{++requestSequence}",Guid.Parse("11111111-1111-1111-1111-111111111111"),"fake","fake","fake","technician.sg","fake","MEDIUM",[new("EQUIPMENT","Inversor",quantity,null,null)]);
 async Task<long> Upload(int quantity)
 {
  var request=await db.WarehouseCanonicalRequest(Request(quantity),support,"Servicios Generales",ct);
  Check(request?.AgencyCode=="6010183"&&request.InstallerName=="","Canonical agency comes from SQL and the obsolete installer field stays empty");
  return await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="QA",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,RecipientDepartment="Almacén",Status="pending",SentUtc=DateTime.UtcNow,WarehouseRequest=request,ContentType="application/pdf",OriginalFileName="QA.pdf"},"%PDF-QA"u8.ToArray(),ct);
 }
 if(args.Contains("--form-qr-only")){
  if(args.Contains("--legacy-withdraw-schema")){
   await Exec(connection,"ALTER TABLE maintenance_movements ADD CONSTRAINT CK_maintenance_quantity CHECK(quantity BETWEEN 1 AND 1000);");
   try{await Exec(connection,"UPDATE maintenance_movements SET quantity=1500 WHERE serial_number='INV-1';");throw new Exception("Expected quantity rejection");}catch(SqlException e) when(e.Number==547){Check(true,"Reproduced SQL 547 with old quantity constraint");}
  }
  var number="SOL-ALM-20261005101530-A1B2";
  var request=await db.WarehouseCanonicalRequest(Request(1) with{Number=number},support,support.Department,ct);
  var formQrId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="QR QA",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,RecipientDepartment="Almacén",Status="pending",WarehouseRequest=request,ContentType="application/pdf",OriginalFileName="QR.pdf"},"%PDF-QA"u8.ToArray(),ct);
  var formScanQr="https://realagencias.runasp.net/?equipment="+Uri.EscapeDataString("REAL-MNT|"+number);
  Check((await db.WarehouseWithdraw(warehouse,new(formScanQr,"Receptor QA",formQrId),ct)).Error is not null,"Form QR cannot bypass authorization");
  await db.InstitutionalChange(formQrId,warehouse,"receive",new(),ct);await db.WarehouseApprovePrepared(formQrId,warehouse,new("Autorización QA"),ct);
  await db.WarehousePrepare(formQrId,warehouse,new("Asignación QA",[new(0,"Inversor","INV-1",1)]),ct);
  if(args.Contains("--legacy-withdraw-schema"))await Exec(connection,$"UPDATE warehouse_equipment_allocations SET equipment_name=REPLICATE(N'X',140),quantity=1500 WHERE document_id={formQrId};");
  Check((await db.WarehouseWithdraw(warehouse,new(formScanQr,"Receptor QA",formQrId+1),ct)).Error!.Contains("esta solicitud"),"Wrong card rejected");
  var roleOnlyWarehouse=warehouse with{Role="Warehouse",Team=""};
  Check((await db.WarehouseWithdraw(support,new(formScanQr,"Receptor QA",formQrId),ct)).Error is not null,"Other departments cannot register withdrawal");
  Check((await db.WarehouseWithdraw(roleOnlyWarehouse,new(formScanQr,"Receptor QA",formQrId),ct)).Error is null,"Warehouse role without legacy team registers main PDF QR withdrawal");
  Check((await db.InstitutionalDetail(formQrId,warehouse,ct))!.WarehouseDelivery is not null,"Delivery and receiver seal data preserved");
  if(args.Contains("--legacy-withdraw-schema")){
   await using var persisted=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements WHERE movement_type='NEW_DELIVERY' AND LEN(equipment_type)=140 AND quantity=1500",connection);
   Check(Convert.ToInt32(await persisted.ExecuteScalarAsync())==1,"Withdrawal preserves full product name and quantity after migration");
  }
  Check((await db.WarehouseWithdraw(warehouse,new(formScanQr,"Receptor QA",formQrId),ct)).Error is not null,"Duplicate withdrawal blocked");
  var token=(await db.InstitutionalDetail(formQrId,warehouse,ct))!.VerificationToken!;
  Check(Database.TestQrToken(Uri.EscapeDataString("https://example.invalid/api/institutional-documents/verify/"+token))==token,"Encoded seal URL supported");
  Check(Database.TestQrToken("httpsÑ--example.invalid-api-institutional'documents-verify-"+token)==token,"USB keyboard seal format preserved");
  Console.WriteLine($"SUCCESS {checks} QR withdrawal checks");return;
 }
 if(args.Contains("--manual-catalog-only")){
  var occurred=DateTime.UtcNow;
  var method=typeof(Database).GetMethod("EnsureWarehouseEntryProduct",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
  await using(var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)){
   await (Task)method.Invoke(null,new object[]{connection,tx,"Nuevo equipo QA",1,occurred,ct,true})!;
   await (Task)method.Invoke(null,new object[]{connection,tx,"Nuevo equipo QA",1,occurred,ct,true})!;
   await (Task)method.Invoke(null,new object[]{connection,tx,"Material sin serial QA",1,occurred,ct,false})!;
   await using var quantityEntry=new SqlCommand("INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(NEWID(),'WAREHOUSE','ENTRY',N'Material sin serial QA',NULL,5,'WAREHOUSE','NO-SERIAL-QA','NO-SERIAL-QA',@at)",connection,tx);
   quantityEntry.Parameters.AddWithValue("@at",occurred);await quantityEntry.ExecuteNonQueryAsync();
   await using var insert=new SqlCommand("INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(NEWID(),'TECHNOLOGY','ENTRY',N'Nuevo equipo QA','MANUAL-QA-1',1,'WAREHOUSE','MANUAL-QA-1','MANUAL-QA-1',@at)",connection,tx);
   insert.Parameters.AddWithValue("@at",occurred);await insert.ExecuteNonQueryAsync();await tx.CommitAsync();
  }
  var inventory=await db.WarehouseAvailableInventory(warehouse,ct);
  Check(inventory.Single(x=>x.Name=="Material sin serial QA").Available==5,"Central warehouse entry without serial keeps actual quantity");
  Check(!inventory.Single(x=>x.Name=="Material sin serial QA").TracksSerial,"New unserialized product is controlled by quantity");
  Check(inventory.Count(x=>x.Name=="Nuevo equipo QA")==1,"Manual product is searchable exactly once");
  Check(inventory.Single(x=>x.Name=="Nuevo equipo QA").Available==1,"Stock comes from actual entry without agency or destination");
  Check(inventory.Single(x=>x.Name=="Nuevo equipo QA").Serials.Contains("MANUAL-QA-1"),"Serial available for dispatch");
  Console.WriteLine($"SUCCESS {checks} manual inventory checks");return;
 }
 if(args.Contains("--pending-purchase-only")){
  var purchaseId=await Upload(1);
  var draft=new WarehousePurchasePendingInput("Debe comprarse antes de entregar",[0],[new(0,"Silla de agencia","",1)]);
  Check(await db.WarehouseSavePendingPurchase(purchaseId,warehouse,draft,ct) is not null,"Purchase preparation requires authorization");
  await db.InstitutionalChange(purchaseId,warehouse,"receive",new(),ct);
  await db.WarehouseApprovePrepared(purchaseId,warehouse,new("Autorizar solicitud"),ct);
  var before=(await db.WarehouseAvailableInventory(warehouse,ct)).Single(x=>x.ProductId=="QA-INVERSOR").Available;
  Check(await db.WarehouseSavePendingPurchase(purchaseId,support,draft,ct) is not null,"Requester cannot manage purchasing");
  Check(await db.WarehouseSavePendingPurchase(purchaseId,warehouse,draft with{LineIndexes=[99]},ct) is not null,"Unknown lines rejected");
  Check(await db.WarehouseSavePendingPurchase(purchaseId,warehouse,draft,ct) is null,"Article absent from catalog can be saved pending purchase");
  Check(await db.WarehouseSavePendingPurchase(purchaseId,warehouse,draft,ct) is null,"Repeated saving does not duplicate allocations");
  var saved=await db.InstitutionalDetail(purchaseId,warehouse,ct);
  Check(saved!.WarehousePendingPurchaseLines.SequenceEqual(new[]{0})&&saved.WarehouseAssignmentDraft[0].Name=="Silla de agencia","Pending purchase and draft persisted");
  Check(saved.WarehouseAssigned.Count==0&&saved.WarehouseDelivery is null&&saved.Status=="approved","Purchase draft never represents a dispatch or delivery");
  Check((await db.WarehouseAvailableInventory(warehouse,ct)).Single(x=>x.ProductId=="QA-INVERSOR").Available==before,"No stock fabricated or reserved");
  Check(await db.WarehousePrepare(purchaseId,warehouse,new("Intento sin inventario",draft.Equipment),ct) is not null,"Absent inventory still cannot be dispatched");
  Check(await db.WarehousePrepare(purchaseId,warehouse,new("Equipo real disponible",[new(0,"Inversor","INV-1",1)]),ct) is null,"Real stock can complete preparation later");
  Check((await db.InstitutionalDetail(purchaseId,warehouse,ct))!.WarehousePendingPurchaseLines.Count==0,"Successful assignment clears pending purchase");
  Check(await db.WarehouseSavePendingPurchase(purchaseId,warehouse,draft,ct) is not null,"Dispatched request cannot be overwritten by purchase draft");
  Console.WriteLine($"SUCCESS {checks} purchasing checks");return;
 }
 if(args.Contains("--repaired-inventory-only")){
  var newStockBefore=(await db.WarehouseAvailableInventory(warehouse,ct)).Single(i=>i.Name=="Inversor").Available;
  await Exec(connection,"""
   DECLARE @repair uniqueidentifier=NEWID();
   INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(@repair,'GENERAL_SERVICES','ENTRY','Inversor','REPAIRED-QA',1,'WAREHOUSE','REPAIRED-QA','REPAIRED-QA',SYSUTCDATETIME());
   INSERT warehouse_repaired_receipts(movement_id,source_id) VALUES(@repair,NEWID());
   """);
  var newStockAfter=(await db.WarehouseAvailableInventory(warehouse,ct)).Single(i=>i.Name=="Inversor");
  Check(newStockAfter.Available==newStockBefore&&!newStockAfter.Serials.Contains("REPAIRED-QA"),"Repaired receipt does not increase new stock or serials");
  Check(System.Text.Json.JsonDocument.Parse(await db.WarehouseRepairedInventory(ct)).RootElement[0].GetProperty("available").GetInt32()==1,"Repaired unit has its own inventory");
  var repairedRequest=await Upload(1);await db.InstitutionalChange(repairedRequest,warehouse,"receive",new(),ct);await db.WarehouseApprovePrepared(repairedRequest,warehouse,new("Autorizado para prueba"),ct);
  Check(await db.WarehousePrepare(repairedRequest,warehouse,new("No mezclar inventarios",[new(0,"Inversor","REPAIRED-QA",1)]),ct) is not null,"Repaired serial cannot be assigned as new equipment");
  await Exec(connection,"INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(NEWID(),'GENERAL_SERVICES','EXIT','Inversor','REPAIRED-QA',1,'WAREHOUSE','REPAIRED-OUT','REPAIRED-OUT',SYSUTCDATETIME());");
  Check((await db.WarehouseAvailableInventory(warehouse,ct)).Single(i=>i.Name=="Inversor").Available==newStockBefore,"Repaired exit does not consume new stock");
  Check(System.Text.Json.JsonDocument.Parse(await db.WarehouseRepairedInventory(ct)).RootElement[0].GetProperty("available").GetInt32()==0,"Repaired exit removes only repaired balance");
  Console.WriteLine($"SUCCESS {checks} repaired inventory checks");return;
 }
 if(args.Contains("--cancellation-only")){
  var colleague=new DocumentActor("colleague.sg","GeneralServices","CALL_CENTER","Compañero SG","colleague.sg");
  var cancelledId=await Upload(1);
  Check(await db.InstitutionalDetail(cancelledId,technician,ct) is null,"SG technician cannot read department request");
  Check((await db.InstitutionalList(technician,"communications","requirements",ct)).Count==0,"SG technician has no warehouse requests");
  Check(await db.InstitutionalBytes(cancelledId,technician,ct) is null,"SG technician cannot download request");
  var ownTechnician=technician with {Id=support.Id};
  Check(await db.InstitutionalDetail(cancelledId,ownTechnician,ct) is not null,"SG technicians can consult their own requests");
  Check(await db.InstitutionalDetail(cancelledId,colleague,ct) is not null,"Department colleague can read request");
  Check((await db.InstitutionalList(colleague,"communications","requirements",ct)).Any(d=>d.Id==cancelledId),"Department list includes colleagues requests");
  Check(await db.InstitutionalBytes(cancelledId,colleague,ct) is not null,"Department colleague can download PDF");
  Check(await db.InstitutionalDetail(cancelledId,otherDepartment,ct) is null,"Other department cannot read request");
  Check(await db.InstitutionalChange(cancelledId,colleague,"cancel",new(Note:"Cancelar solicitud ajena"),ct) is not null,"Read access does not allow cancellation");
  Check(await db.InstitutionalChange(cancelledId,colleague,"receive",new(),ct) is not null,"Read access does not allow receiving");
  var hidden=new InstitutionalFile{Area="communications",Channel="requirements",Department=support.Department,OwnerUserName=support.Id,RecipientDepartment="Almacén",WarehouseRequest=Request(1),Status="draft"};
  Check(!hidden.Accessible(colleague),"Colleague cannot read draft");
  hidden.Status="pending";hidden.SentUtc=DateTime.UtcNow;hidden.Trashed=true;
  Check(!hidden.Accessible(colleague),"Colleague cannot read trash");
  Check(await db.InstitutionalChange(cancelledId,otherDepartment,"cancel",new(Note:"No se necesita equipo"),ct) is not null,"Unrelated department cannot cancel");
  Check(await db.InstitutionalChange(cancelledId,support,"cancel",new(Note:""),ct) is not null,"Cancellation requires reason");
  Check(await db.InstitutionalChange(cancelledId,warehouse,"receive",new(),ct) is null,"Receive cancellation fixture");
  Check(await db.WarehouseApprovePrepared(cancelledId,warehouse,new("Autorizar solicitud"),ct) is null,"Approve cancellation fixture");
  Check(await db.WarehousePrepare(cancelledId,warehouse,new("Asignar equipo disponible",[new(0,"Inversor","INV-1",1)]),ct) is null,"Reserve stock before cancellation");
  var before=await db.InstitutionalDetail(cancelledId,warehouse,ct);
  Check(await db.InstitutionalChange(cancelledId,support,"cancel",new(Note:"Equipo ya no requerido"),ct) is null,"Owner cancels approved request before delivery");
  var cancelled=await db.InstitutionalDetail(cancelledId,warehouse,ct);
  Check(cancelled?.Status=="cancelled"&&cancelled.CorrectionNote=="Equipo ya no requerido"&&!cancelled.Trashed&&cancelled.History.Last().Action=="cancel","Cancellation preserves document and audit reason");
  await using(var check=new SqlCommand("SELECT COUNT(*) FROM warehouse_equipment_allocations WHERE document_id=@id AND status='CANCELLED'",connection)){check.Parameters.AddWithValue("@id",cancelledId);Check(Convert.ToInt32(await check.ExecuteScalarAsync())==1,"Reservation released without deleting allocation history");}
  Check((await db.WarehouseWithdraw(warehouse,new(before!.VerificationToken!,"Persona QA",cancelledId),ct)).Error is not null,"Cancelled QR cannot withdraw stock");
  Check(System.Text.Json.JsonSerializer.Serialize(await db.WarehouseTrace(before.VerificationToken!,warehouse,ct)).Contains("CANCELLED"),"QR trace shows cancellation");
  Check(await db.InstitutionalChange(cancelledId,warehouse,"cancel",new(Note:"Anular de nuevo"),ct) is not null,"Duplicate cancellation rejected");
  var deliveredId=await Upload(1);
  await Exec(connection,$"UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.warehouseDelivery',JSON_QUERY(N'{{\"number\":\"DEL-QA\",\"deliveredBy\":\"QA\",\"deliveredLogin\":\"qa\",\"receivedBy\":\"QA\",\"at\":\"2026-09-29T12:00:00Z\"}}')) WHERE id={deliveredId};");
  Check(await db.InstitutionalChange(deliveredId,warehouse,"cancel",new(Note:"No debe permitir"),ct) is not null,"Delivered requests cannot be cancelled");
  Console.WriteLine($"SUCCESS {checks} cancellation SQL checks");return;
 }
 if(args.Contains("--removal-only")){
  await Exec(connection,"ALTER TABLE admin_users ADD email nvarchar(160) NULL,support_team varchar(40) NULL;");
  await Exec(connection,"""
    INSERT admin_users(id,display_name,role,is_active,email,support_team) VALUES('qa-bryan','Bryan QA','WAREHOUSE',1,'bryan.arias@grupotejeda.local','WAREHOUSE'),('qa-diorly','Diorly QA','WAREHOUSE',1,'diorly.abreu@grupotejeda.local','WAREHOUSE');
    INSERT warehouse_products(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,tracks_serial,catalog_version,imported_at,is_active) VALUES('QA-DELETE','Artículo de prueba',1,'QA',5,'UND','QA','Materiales',0,'QA',SYSUTCDATETIME(),1),('QA-KEEP','No modificar',1,'QA',12,'UND','QA','Materiales',0,'QA',SYSUTCDATETIME(),1);
    """);
  var bryan=new DocumentActor("qa-bryan","Warehouse","WAREHOUSE","Bryan QA","bryan.arias@grupotejeda.local");
  var diorly=new DocumentActor("qa-diorly","Warehouse","WAREHOUSE","Diorly QA","diorly.abreu@grupotejeda.local");
  var removal=new WarehouseProductRemoval("QA-DELETE",5,"Prueba de eliminación aislada");
  Check(await db.WarehouseRemoveProduct(warehouse,removal,ct) is not null,"Other warehouse account denied");
  Check(await db.WarehouseRemoveProduct(new("admin","Administrator","","Admin","admin@local.com"),removal,ct) is not null,"Administrator not implicitly authorized");
  Check(await db.WarehouseRemoveProduct(bryan,removal with{ExpectedQuantity=4},ct) is not null,"Stale stock denied");
  var reservedDocument=await Upload(1);
  await Exec(connection,$"INSERT warehouse_equipment_allocations(document_id,line_index,department,equipment_name,serial_number,quantity,status,product_id) VALUES({reservedDocument},0,'TECHNOLOGY','Artículo de prueba','QA-RESERVA',1,'PENDING_PICKUP','QA-DELETE');");
  Check(await db.WarehouseRemoveProduct(bryan,removal,ct) is not null,"Reservations protected");
  await Exec(connection,"UPDATE warehouse_equipment_allocations SET status='WITHDRAWN' WHERE product_id='QA-DELETE';");
  Check(await db.WarehouseRemoveProduct(bryan,removal,ct) is null,"Bryan removes only selected active product");
  Check(await db.WarehouseRemoveProduct(diorly,removal,ct) is not null,"Duplicate removal denied");
  var remaining=await db.WarehouseAvailableInventory(bryan,ct);
  Check(!remaining.Any(p=>p.ProductId=="QA-DELETE")&&remaining.Any(p=>p.ProductId=="QA-KEEP"&&p.Available==12),"Only selected product hidden; other stock unchanged");
  await using(var audit=new SqlCommand("SELECT COUNT(*) FROM warehouse_product_removals WHERE product_id='QA-DELETE' AND actor_login='bryan.arias@grupotejeda.local'",connection))Check(Convert.ToInt32(await audit.ExecuteScalarAsync())==1,"Audit retained exactly once");
  await using(var historical=new SqlCommand("SELECT official_stock FROM warehouse_products WHERE product_id='QA-DELETE' AND is_active=0",connection))Check(Convert.ToInt32(await historical.ExecuteScalarAsync())==5,"Original product and stock retained");
  Check(await db.WarehouseRemoveProduct(diorly,new("QA-KEEP",12,"Prueba de encargada autorizada"),ct) is null,"Diorly authorized");
  Console.WriteLine($"{checks} comprobaciones SQL aprobadas en base temporal.");return;
 }
 if(args.Contains("--cleanup-only")){
  var pendingId=await Upload(1);var receivedId=await Upload(1);var correctionId=await Upload(1);
  var approvedId=await Upload(1);var rejectedId=await Upload(1);var deliveredId=await Upload(1);
  Check(await db.InstitutionalChange(receivedId,warehouse,"receive",new(),ct) is null,"Prepare pending-authorization fixture");
  await Exec(connection,$"UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.status','correction_requested') WHERE id={correctionId}; UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.status','approved') WHERE id IN({approvedId},{deliveredId}); UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.status','rejected') WHERE id={rejectedId};");
  var administrativeId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Administrativa",Department="Tecnología",OwnerUserName=support.Id,RecipientDepartment="Administración",Status="approved",ContentType="application/pdf",OriginalFileName="ADMIN.pdf"},"%PDF-ADMIN"u8.ToArray(),ct);
  await Exec(connection,$"INSERT warehouse_equipment_allocations(document_id,line_index,department,equipment_name,serial_number,quantity,status,product_id) VALUES({approvedId},0,'GENERAL_SERVICES','Inversor','INV-1',1,'PENDING_PICKUP','QA-INVERSOR'),({deliveredId},0,'GENERAL_SERVICES','Inversor','INV-2',1,'WITHDRAWN','QA-INVERSOR'); DELETE application_one_time_actions WHERE action_key='{Database.WarehouseCleanupKey}';");
  await Database.CleanupCompletedWarehouseRequests(connection,ct);
  foreach(var kept in new[]{pendingId,receivedId,correctionId})Check((await db.InstitutionalDetail(kept,warehouse,ct))?.Trashed==false,"Pending request retained");
  await using(var backup=new SqlCommand("SELECT COUNT(*) FROM warehouse_request_cleanup_backup",connection))Check(Convert.ToInt32(await backup.ExecuteScalarAsync())==3,"Only approved, delivered and rejected backed up");
  foreach(var removed in new[]{approvedId,rejectedId,deliveredId})Check((await db.InstitutionalDetail(removed,support,ct))?.Trashed==true,"Completed or approved request removed from active inbox");
  Check((await db.InstitutionalDetail(administrativeId,support,ct))?.Trashed==false,"Administrative requests untouched");
  await using(var reserved=new SqlCommand($"SELECT status FROM warehouse_equipment_allocations WHERE document_id={approvedId}",connection))Check((string?)await reserved.ExecuteScalarAsync()=="CANCELLED","Pending pickup reservation released without stock movement");
  await using(var withdrawnCheck=new SqlCommand($"SELECT status FROM warehouse_equipment_allocations WHERE document_id={deliveredId}",connection))Check((string?)await withdrawnCheck.ExecuteScalarAsync()=="WITHDRAWN","Historical withdrawal unchanged");
  await using(var history=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements",connection))Check(Convert.ToInt32(await history.ExecuteScalarAsync())==3,"Inventory history unchanged");
  await using(var backup=new SqlCommand($"SELECT COUNT(*) FROM warehouse_request_cleanup_backup WHERE document_id={approvedId} AND allocations_json LIKE '%PENDING_PICKUP%' AND DATALENGTH(file_data)>0",connection))Check(Convert.ToInt32(await backup.ExecuteScalarAsync())==1,"Original PDF, metadata and reservation recoverable");
  await Exec(connection,$"UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.status','approved') WHERE id={pendingId};");
  await Database.CleanupCompletedWarehouseRequests(connection,ct);
  Check((await db.InstitutionalDetail(pendingId,warehouse,ct))?.Trashed==false,"Cleanup never repeats on restart or removes future approvals");
  Check((await db.WarehouseNotifications(warehouse,ct)).Count==3,"Notifications contain only surviving warehouse requests");
  Console.WriteLine($"SUCCESS {checks} cleanup SQL checks");return;
 }
 if(args.Contains("--warehouse-notifications-only")){
  var requestedId=await Upload(1);
  Check((await db.WarehouseNotifications(warehouse,ct)).Count==1,"New request appears in warehouse notification center");
  Check((await db.WarehouseNotifications(support,ct)).Count==0,"Other departments cannot read warehouse alerts");
  var admin=new DocumentActor("admin","Administrator","","Admin","admin.qa");
  Check((await db.WarehouseNotifications(admin,ct)).Count==1,"General administrator sees warehouse requests in global notification center");
  var secondWarehouse=new DocumentActor("warehouse.second","Warehouse","","Segundo Almacén","segundo.qa");
  Check((await db.WarehouseNotifications(secondWarehouse,ct)).Count==1,"Warehouse role receives warehouse alerts");
  Check((await db.WarehouseNotifications(admin with{Team="WAREHOUSE"},ct)).Count==1,"Administrator assigned to warehouse receives warehouse alerts");
  var json=System.Text.Json.JsonSerializer.Serialize(await db.WarehouseNotifications(warehouse,ct));
  Check(json.Contains("WAREHOUSE")&&json.Contains("Servicios Generales")&&json.Contains("\"isRead\":false"),"Alert contains source department and unread state");
  await Exec(connection,$"INSERT support_notification_receipts(user_id,notification_key,read_at) VALUES('warehouse','WAREHOUSE_REQUEST:{requestedId}',SYSUTCDATETIME());");
  Check(System.Text.Json.JsonSerializer.Serialize(await db.WarehouseNotifications(warehouse,ct)).Contains("\"isRead\":true"),"Read state persists per user");
  Check(System.Text.Json.JsonSerializer.Serialize(await db.WarehouseNotifications(secondWarehouse,ct)).Contains("\"isRead\":false"),"Reading does not mark another user's alert");
  Check(System.Text.Json.JsonSerializer.Serialize(await db.WarehouseNotifications(admin,ct)).Contains("\"isRead\":false"),"Warehouse read state does not mark administrator notification read");
  await Exec(connection,$"UPDATE support_notification_receipts SET dismissed_at=SYSUTCDATETIME() WHERE user_id='warehouse';");
  Check((await db.WarehouseNotifications(warehouse,ct)).Count==0,"Dismissed alert stays hidden");
  await Exec(connection,$"UPDATE institutional_files SET metadata=JSON_MODIFY(metadata,'$.trashed',CAST(1 AS bit)) WHERE id={requestedId};");
  Check((await db.WarehouseNotifications(secondWarehouse,ct)).Count==0,"Removed requests do not leave ghost notifications");
  Console.WriteLine($"SUCCESS {checks} notification SQL checks");return;
 }
 if(args.Contains("--administrative-visibility")){
  var sandra=new DocumentActor("sandra","Administrative","","Sandra","sandra.ramirez@grupotejeda.local");
  var emely=new DocumentActor("emely","Administrative","","Emely","emely.figueroa@grupotejeda.local");
  var admin=new DocumentActor("admin","Administrator","","Principal","admin@local.com");
  var form=new InstitutionalFile{Area="communications",Channel="requirements",Department="Servicios Generales",OwnerUserName=support.Id,RecipientDepartment="Administración",Status="pending",SentUtc=DateTime.UtcNow,Title="Combustible QA",OriginalFileName="QA.pdf",ContentType="application/pdf",AdministrativeRequest=new("SOL-ADM-QA","OTRO","EMPLEADO","QA",null,"Combustible de prueba",FormType:"FUEL_LOG")};
  var administrativeId=await db.InstitutionalUpload(form,"%PDF-QA"u8.ToArray(),ct);
  Check(await db.InstitutionalDetail(administrativeId,sandra,ct) is not null,"Sandra reads fuel from another department");
  Check(await db.InstitutionalBytes(administrativeId,sandra,ct) is not null,"Sandra downloads her assigned form");
  Check(await db.InstitutionalDetail(administrativeId,emely,ct) is null,"Reception cannot open fuel by direct administrativeId");
  Check(await db.InstitutionalBytes(administrativeId,emely,ct) is null,"Reception cannot download fuel by direct administrativeId");
  Check((await db.InstitutionalList(emely,"communications","requirements",ct)).Count==0,"Unauthorized form absent from list");
  Check(await db.InstitutionalChange(administrativeId,emely,"receive",new(),ct) is not null,"Unauthorized action rejected");
  Check(await db.InstitutionalChange(administrativeId,sandra,"receive",new(),ct) is null,"Assigned profile receives form");
  Check(await db.InstitutionalChange(administrativeId,sandra,"approve",new(Note:"Aprobación QA"),ct) is null,"Assigned profile decides its form");
  Check(await db.InstitutionalDetail(administrativeId,admin,ct) is not null,"Principal keeps all document access");
  Console.WriteLine($"SUCCESS {checks} administrative visibility checks");return;
 }
 if(args.Contains("--destination-scope")){
  await Exec(connection,"CREATE TABLE admin_user_groups(user_id nvarchar(80),group_name nvarchar(120)); INSERT admin_user_groups VALUES('supervisor',N'Grupo QA'); INSERT agencies VALUES('22222222-2222-2222-2222-222222222222','999','Otra','Otro grupo');");
  var supervisor=new DocumentActor("supervisor","GroupAdministrator","","Supervisor","supervisor.qa");
  var request=new WarehouseRequestInfo("SOL-ALM-QA",Guid.Empty,"","","Grupo QA","","","MEDIUM",[new("EQUIPMENT","Equipo manual",1,null,null)],DestinationType:"GROUP");
  var grouped=await db.WarehouseCanonicalRequest(request,supervisor,"Supervisión",ct);
  Check(grouped is null,"Supervisor cannot request an entire group");
  Check((await db.WarehouseRequestGroups(supervisor,ct)).Count==0,"Supervisor group picker is disabled server-side");
  Check((await db.WarehouseRequestGroups(supervisor with{Id="unassigned"},ct)).Count==0,"Supervisor without assignments cannot select any group");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyGroup=""},supervisor,"Supervisión",ct) is null,"Empty group rejected rather than converted to internal destination");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyId=Guid.Parse("11111111-1111-1111-1111-111111111111")},supervisor,"Supervisión",ct) is null,"Group destination cannot conceal a specific agency");
  Check(await db.WarehouseCanonicalRequest(request with{TicketId=Guid.NewGuid()},supervisor,"Supervisión",ct) is null,"Group destination cannot attach an agency-specific ticket");
  Check(await db.WarehouseCanonicalRequest(request,supervisor,"Tecnología",ct) is null,"Supervisor cannot impersonate another source department");
  await Exec(connection,"INSERT admin_user_groups VALUES('supervisor',N'Segundo grupo'); INSERT agencies VALUES('33333333-3333-3333-3333-333333333333','333','Segunda asignada','Segundo grupo');");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyId=Guid.Parse("33333333-3333-3333-3333-333333333333"),DestinationType="AGENCY"},supervisor,"Supervisión",ct) is not null,"Multi-agency requests may include a second assigned group");
  Check(await db.WarehouseCanonicalRequest(request with{DestinationType="GROUP"},supervisor,"Supervisión",ct) is null,"Group-only request never reaches warehouse");
  Check(await db.WarehouseCanonicalRequest(request with{DestinationType="DEPARTMENT"},supervisor,"Supervisión",ct) is null,"Supervisor cannot request own department");
  Check(await db.WarehouseCanonicalRequest(request with{DestinationType="AGENCY"},supervisor,"Supervisión",ct) is null,"Supervisor cannot omit agency");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyGroup="Otro grupo"},supervisor,"Supervisión",ct) is null,"Supervisor cannot request another group");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyId=Guid.Parse("11111111-1111-1111-1111-111111111111"),DestinationType="AGENCY"},supervisor,"Supervisión",ct) is not null,"Supervisor requests assigned agency");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyId=Guid.Parse("22222222-2222-2222-2222-222222222222"),DestinationType="AGENCY"},supervisor,"Supervisión",ct) is null,"Supervisor cannot request unassigned agency");
  var administrative=new DocumentActor("sandra","Administrative","","Sandra","sandra.ramirez");
  Check(await db.WarehouseCanonicalRequest(request,administrative,"Administración",ct) is not null,"Administration retains group requests");
  Check(await db.WarehouseCanonicalRequest(request with{AgencyId=Guid.Parse("11111111-1111-1111-1111-111111111111"),DestinationType="AGENCY"},administrative,"Administración",ct) is not null,"Administration retains agency requests");
  Check(await db.WarehouseCanonicalRequest(request with{DestinationType="DEPARTMENT"},administrative,"Administración",ct) is not null,"Administration requests own department");
  Console.WriteLine($"SUCCESS {checks} destination scope checks");return;
 }
 if(args.Contains("--department-substitution-only")){
  var request=await db.WarehouseCanonicalRequest(Request(1),support,"Servicios Generales",ct);
  Check(request?.AgencyCode=="6010183","Department request requires canonical agency");
  var ownRequest=await db.WarehouseCanonicalRequest(Request(1) with{AgencyId=Guid.Empty},support,"Servicios Generales",ct);
  Check(ownRequest?.AgencyName=="Departamento de Servicios Generales"&&ownRequest.AgencyCode=="USO INTERNO","Own department destination canonicalized");
  if(args.Contains("--own-department"))request=ownRequest;
  Check(await db.WarehouseCanonicalRequest(Request(1) with{AgencyId=Guid.Empty},support,"Tecnología",ct) is null,"Requester cannot impersonate another department");
  Check(await db.WarehouseCanonicalRequest(Request(1) with{AgencyId=Guid.Empty,TicketId=Guid.NewGuid()},support,"Servicios Generales",ct) is null,"Ticket request still requires actual agency");
  var administrator=new DocumentActor("admin","Administrator","","Administrador","admin.qa");
  foreach(var departmentName in new[]{"Tecnología","Servicios Generales","Recursos Humanos","Administración","Taller","Comunicación","Comercialización","Tesorería","Contabilidad","Supervisión","Avería Técnica","Call Center"})Check((await db.WarehouseCanonicalRequest(Request(1) with{AgencyId=Guid.Empty},administrator,departmentName,ct))?.AgencyName=="Departamento de "+departmentName,"Administrator internal request for "+departmentName);
  Check((await db.WarehouseCanonicalRequest(Request(1) with{AgencyId=Guid.Empty},administrator,"Almacén",ct))?.AgencyName=="Departamento de Almacén","Administrator may create internal warehouse request");
  var internalId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Solicitud del departamento",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,RecipientDepartment="Almacén",Status="pending",WarehouseRequest=request,ContentType="application/pdf",OriginalFileName="SOL-ALM-DEPT-QA.pdf"},"%PDF-QA"u8.ToArray(),ct);
  Check((await db.InstitutionalList(support,"communications","requirements",ct)).Any(row=>row.Id==internalId),"Department request appears in sender list");
  Check(await db.InstitutionalChange(internalId,warehouse,"receive",new(),ct) is null,"Receive department request");
  Check(await db.WarehouseApprovePrepared(internalId,warehouse,new("Autorizar departamento"),ct) is null,"Authorize department request");
  await Exec(connection,"INSERT warehouse_products(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,tracks_serial,catalog_version,imported_at,is_active) VALUES('QA-DELL','Monitor Dell',1,'QA',3,'UND','QA','Equipos',0,'QA',SYSUTCDATETIME(),1);");
  var actual=new WarehouseAuthorizationInput("Sustituir por equipo disponible",[new(0,"Monitor Dell","",1,"QA-DELL")]);
  Check(await db.WarehousePrepare(internalId,support,actual,ct) is not null,"Only warehouse may select substitute");
  Check(await db.WarehousePrepare(internalId,warehouse,actual with{Equipment=[new(0,"Monitor Dell","",2,"QA-DELL")]},ct) is not null,"Substitution cannot increase requested quantity");
  Check(await db.WarehousePrepare(internalId,warehouse,actual with{Equipment=[new(0,"Monitor Dell","",1,"FAKE")]},ct) is not null,"Unknown substitute rejected");
  Check(await db.WarehousePrepare(internalId,warehouse,actual,ct) is null,"Warehouse selects available substitute");
  var prepared=await db.InstitutionalDetail(internalId,warehouse,ct);
  Check(prepared?.WarehouseRequest?.Lines[0].Name=="Inversor"&&prepared.WarehouseAssigned[0].Name=="Monitor Dell","Original and actual equipment both retained");
  Check((await db.WarehouseAvailableInventory(warehouse,ct)).Single(row=>row.ProductId=="QA-DELL").Available==2,"Reservation applies to actual substitute");
  var withdrawal=await db.WarehouseWithdraw(warehouse,new(prepared!.VerificationToken!,"Receptor del departamento",internalId),ct);
  Check(withdrawal.Error is null,"Withdraw substitute with department agency");
  Check((await db.InstitutionalDetail(internalId,support,ct))?.WarehouseDelivery is not null,"Delivery seals retain their data");
  var destinationSql=args.Contains("--own-department")?"agency_id IS NULL":"agency_id='11111111-1111-1111-1111-111111111111'";
  await using(var movement=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements WHERE movement_type='NEW_DELIVERY' AND component_type='QA-DELL' AND "+destinationSql,connection))Check(Convert.ToInt32(await movement.ExecuteScalarAsync())==1,"Stock deduction uses substitute and selected destination");
  Console.WriteLine($"SUCCESS {checks} department and substitution SQL checks");return;
 }
 if(args.Contains("--snapshot-only")){
  var snapshot=System.Text.Json.JsonSerializer.Deserialize<WarehouseCatalogImport>(await File.ReadAllTextAsync("api/Data/warehouse-products-2026-09-28.json"),new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
  var historicalId=await Upload(1);
  await Database.ApplyWarehouseCatalogSnapshot(connection,snapshot,ct);
  var stock=await db.WarehouseAvailableInventory(warehouse,ct);
  Check(stock.Count==172&&stock.Sum(row=>row.Available)==45004,"Real workbook loaded exactly: 172 products and 45004 units");
  Check(!stock.Any(row=>row.ProductId=="QA-INVERSOR"),"Products absent from workbook inactive");
  await using(var preserved=new SqlCommand("SELECT COUNT(*) FROM warehouse_products WHERE product_id='QA-INVERSOR' AND is_active=0",connection))Check(Convert.ToInt32(await preserved.ExecuteScalarAsync())==1,"Historical product retained");
  Check((await db.InstitutionalDetail(historicalId,warehouse,ct))?.WarehouseRequest?.Lines[0].Name=="Inversor","Existing requests retained");
  await using(var history=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements",connection))Check(Convert.ToInt32(await history.ExecuteScalarAsync())==3,"Inventory movements retained");
  await using(var audit=new SqlCommand("SELECT COUNT(*) FROM warehouse_catalog_snapshots WHERE previous_catalog LIKE '%QA-INVERSOR%'",connection))Check(Convert.ToInt32(await audit.ExecuteScalarAsync())==1,"Recoverable prior inventory snapshot saved");
  await Exec(connection,"UPDATE warehouse_products SET official_stock=official_stock-1 WHERE product_id='00200800';");
  await Database.ApplyWarehouseCatalogSnapshot(connection,snapshot,ct);
  Check((await db.WarehouseAvailableInventory(warehouse,ct)).Single(row=>row.ProductId=="00200800").Available==5,"Restart never resets later stock changes");
  await using(var audit=new SqlCommand("SELECT COUNT(*) FROM warehouse_catalog_snapshots",connection))Check(Convert.ToInt32(await audit.ExecuteScalarAsync())==1,"Snapshot applied exactly once");
  Console.WriteLine($"SUCCESS {checks} inventory snapshot SQL checks");return;
 }
 if(args.Contains("--internal-trace-only")){
  const string number="SOL-ALM-20260928120000-ABCD";
  var requested=Request(1) with{Number=number,AgencyId=Guid.Empty};
  Check(await db.WarehouseCanonicalRequest(requested,support,"Almacén",ct) is null,"Other departments cannot impersonate internal warehouse requester");
  var internalRequest=await db.WarehouseCanonicalRequest(requested,warehouse,"Almacén",ct);
  Check(internalRequest?.AgencyId==Guid.Empty&&internalRequest.AgencyName=="Departamento de Almacén","Internal destination canonical without creating an agency");
  var internalId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Solicitud interna",Department="Almacén",OwnerUserName=warehouse.Id,OwnerDisplayName=warehouse.Name,RecipientDepartment="Almacén",Status="pending",WarehouseRequest=internalRequest,ContentType="application/pdf",OriginalFileName=number+".pdf"},"%PDF-QA"u8.ToArray(),ct);
  string Trace(List<object> rows)=>System.Text.Json.JsonSerializer.Serialize(rows);
  Check(Trace(await db.WarehouseTrace(number,warehouse,ct)).Contains("SENT"),"Trace available immediately without duplicate maintenance movement");
  Check((await db.WarehouseTrace(number,otherDepartment,ct)).Count==0,"Tracking respects document access");
  Check(await db.InstitutionalChange(internalId,warehouse,"receive",new(),ct) is null,"Internal request received normally");
  var received=await db.InstitutionalDetail(internalId,warehouse,ct);
  Check((await db.WarehouseTrace("https://realagencias.runasp.net/api/institutional-documents/verify/"+received!.VerificationToken,warehouse,ct)).Count==1,"Seal QR resolves to request stages");
  Check((await db.WarehouseTrace("https://realagencias.runasp.net/?equipment="+Uri.EscapeDataString("REAL-MNT|"+number),warehouse,ct)).Count==1,"Original form QR resolves to request stages");
  Check(await db.WarehouseApprovePrepared(internalId,warehouse,new("Autorizar uso interno"),ct) is null,"Normal approval required and supported");
  Check(await db.WarehousePrepare(internalId,warehouse,new("Preparar uso interno",[new(0,"Inversor","INV-1",1)]),ct) is null,"Internal request reserves real inventory");
  Check(Trace(await db.WarehouseTrace(number,warehouse,ct)).Contains("DISPATCH"),"Assigned stage shown after authorization");
  var internalWithdrawal=await db.WarehouseWithdraw(warehouse,new(received.VerificationToken!,"Departamento de Almacén",internalId),ct);
  Check(internalWithdrawal.Error is null,"Internal withdrawal supported without agency foreign key");
  Check(Trace(await db.WarehouseTrace(number,warehouse,ct)).Contains("WITHDRAWN"),"Withdrawal appears in trace");
  Check((await db.WarehouseTrace("SOL-ALM-NO-EXISTE",warehouse,ct)).Count==0,"Unknown code returns no records");
  Console.WriteLine($"SUCCESS {checks} internal request and QR SQL checks");return;
 }
 var id=await Upload(2);
 var pendingDetail=await db.InstitutionalDetail(id,warehouse,ct);
 Check(pendingDetail?.Status=="pending"&&pendingDetail.ReceivedUtc is null,"New warehouse request is persisted as pending receipt");
 var pendingInbox=await db.InstitutionalList(warehouse,"communications","requirements",ct);
 Check(pendingInbox.Any(item=>item.Id==id&&item.Status=="pending"&&item.WarehouseRequest is not null),"Linked warehouse request appears in the warehouse pending inbox");
 var linkedTicketId=Guid.NewGuid();
 var linkedRequest=(await db.WarehouseCanonicalRequest(Request(1),support,"Servicios Generales",ct))! with { TicketId=linkedTicketId,TicketNumber=1515 };
 var linkedDocumentId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Solicitud desde ticket",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,RecipientDepartment="Almacén",Status="active",WarehouseRequest=linkedRequest,ContentType="application/pdf",OriginalFileName="SOL-ALM-TICKET-QA.pdf"},"%PDF-QA"u8.ToArray(),ct);
 var linkedTicketInbox=await db.InstitutionalList(warehouse,"communications","requirements",ct);
 Check(linkedTicketInbox.Any(item=>item.Id==linkedDocumentId&&item.Status=="pending"&&item.SentUtc is not null&&item.WarehouseRequest?.TicketNumber==1515),"Ticket request is normalized and registered in Pendientes de recibir");
 var assignment=new WarehouseAuthorizationInput("Autorizado para Agencia QA",[new(0,"Inversor","INV-1",1),new(0,"Inversor","INV-2",1)]);
 Check(await db.WarehousePrepare(id,warehouse,assignment,ct) is not null,"Cannot prepare before receipt");
 Check(await db.InstitutionalChange(id,warehouse,"receive",new(),ct) is null,"Warehouse receives request");
 if(args.Contains("--approval-only"))
 {
  var staleId=await Upload(1);
  Check(await db.InstitutionalChange(staleId,warehouse,"receive",new(),ct) is null,"Register real receipt before reproducing stale pending status");
  var originalReceipt=(await db.InstitutionalDetail(staleId,warehouse,ct))!;
  await using(var stale=new SqlCommand("UPDATE dbo.institutional_files SET metadata=JSON_MODIFY(metadata,'$.status','pending') WHERE id=@id",connection)){stale.Parameters.AddWithValue("@id",staleId);await stale.ExecuteNonQueryAsync(ct);}
  var staleAuthorizer=warehouse with{WarehouseAuthorize=true};
  var corrected=(await db.InstitutionalDetail(staleId,staleAuthorizer,ct))!;
  Check(corrected.Status=="received"&&corrected.CanAuthorizeWarehouseRequest,"Stamped pending PDF opens as received and allows authorized operator");
  Check((await db.InstitutionalList(staleAuthorizer,"communications","requirements",ct)).Single(row=>row.Id==staleId).Status=="received","Inbox and detail agree on pending authorization");
  Check(corrected.ReceivedUtc==originalReceipt.ReceivedUtc&&corrected.ReceiptCode==originalReceipt.ReceiptCode&&corrected.ReceiverLogin==originalReceipt.ReceiverLogin,"Original receipt date, seal code and receiving user preserved");
  Check(await db.InstitutionalChange(staleId,warehouse,"receive",new(),ct) is not null,"Stamped pending document cannot be received a second time");
  Check(await db.WarehouseApprovePrepared(staleId,staleAuthorizer,new("Autorizar recepción histórica"),ct) is null,"Approval accepts persisted pending with a real receipt");
  var repaired=(await db.InstitutionalDetail(staleId,staleAuthorizer,ct))!;
  Check(repaired.Status=="approved"&&repaired.ReceivedUtc==originalReceipt.ReceivedUtc&&repaired.ReceiptCode==originalReceipt.ReceiptCode&&repaired.WarehouseAssigned.Count==0,"Explicit approval keeps original receipt and does not reserve inventory");
  foreach(var resend in new[]{false,true}){
   var correctionId=await Upload(1);
   Check(await db.InstitutionalChange(correctionId,warehouse,"receive",new(),ct) is null,"Receive before requesting correction");
   var receipt=(await db.InstitutionalDetail(correctionId,warehouse,ct))!;
   Check(await db.InstitutionalChange(correctionId,warehouse,"correction",new(Note:"Revisar cantidad solicitada"),ct) is null,"Record correction without losing receipt");
   var correction=(await db.InstitutionalDetail(correctionId,staleAuthorizer,ct))!;
   Check(correction.Status=="correction_requested"&&correction.CanAuthorizeWarehouseRequest&&correction.CorrectionNote=="Revisar cantidad solicitada","Correction stays recorded and received request awaits authorization");
   if(resend)Check(await db.InstitutionalChange(correctionId,support,"resend",new(),ct) is null,"Owner resubmits corrected request");
   var beforeApproval=(await db.InstitutionalDetail(correctionId,staleAuthorizer,ct))!;
   Check(beforeApproval.ReceivedUtc==receipt.ReceivedUtc&&beforeApproval.ReceiptCode==receipt.ReceiptCode,"Correction and resubmission preserve original receipt");
   Check(await db.WarehouseApprovePrepared(correctionId,staleAuthorizer,new("Corrección revisada y autorizada"),ct) is null,"Authorize received correction without repeating receipt");
  }
  var administrator=new DocumentActor("admin","Administrator","","Administrador principal","admin.qa");
  Check(await db.WarehouseApprovePrepared(id,administrator,new("autorizar"),ct) is null,"Administrator can authorize received warehouse request without prior serial assignment");
  var approved=await db.InstitutionalDetail(id,administrator,ct);
  Check(approved?.Status=="approved"&&approved.DecisionBy=="Administrador principal · admin.qa"&&approved.WarehouseAssigned.Count==0,"Approval persists actor and status without reserving stock");
  Check(await db.WarehouseApprovePrepared(id,administrator,new("autorizar"),ct) is not null,"Repeated approval rejected");
  foreach(var authorizedActor in new[]{warehouse,new DocumentActor("warehouse.role","Warehouse","","Almacen","almacen.role")})
  {
   var requestId=await Upload(1);
   Check(await db.WarehouseApprovePrepared(requestId,authorizedActor,new("autorizar"),ct) is not null,"Receipt required before approval");
   Check(await db.InstitutionalChange(requestId,warehouse,"receive",new(),ct) is null,"Receive test request");
   Check(await db.WarehouseApprovePrepared(requestId,support,new("autorizar"),ct) is not null,"Unprivileged department cannot authorize");
   Check(await db.WarehouseApprovePrepared(requestId,authorizedActor,new("no"),ct) is not null,"Short note rejected");
   Check(await db.WarehouseApprovePrepared(requestId,authorizedActor,new("autorizar"),ct) is null,"Both warehouse account formats can authorize");
  }
  foreach(var authorizer in new[]{new DocumentActor("ana-id","Warehouse","","Ana Rodríguez","ana.rodriguez"),new DocumentActor("diorly-id","Technology","WAREHOUSE","Diorly Abreu","diorly.abreu")})foreach(var source in new[]{"Tecnología","Comunicación","Supervisión"}){
   var request=await db.WarehouseCanonicalRequest(Request(1),support,support.Department,ct);
   var receivedId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Solicitud recibida",Department=source,OwnerUserName="sender",OwnerDisplayName="Remitente",RecipientDepartment="Almacén",WarehouseRequest=request,ContentType="application/pdf",OriginalFileName="QA.pdf"},"%PDF-QA"u8.ToArray(),ct);
   Check(await db.InstitutionalChange(receivedId,warehouse,"receive",new(),ct) is null,"Another operator receives "+source+" request");
   Check((await db.InstitutionalList(authorizer,"communications","requirements",ct)).Any(row=>row.Id==receivedId&&row.Status=="received"),authorizer.Login+" can list received "+source+" request");
   Check(await db.WarehouseApprovePrepared(receivedId,authorizer,new("Autorización de prueba"),ct) is null,authorizer.Login+" can authorize after another operator receives the request");
  }
  Check(await db.WarehousePrepare(id,warehouse,assignment,ct) is null,"Authorized request can assign actual equipment and serials");
  Check((await db.InstitutionalDetail(id,administrator,ct))?.WarehouseAssigned.Count==2,"Serials persist after approval");
  Console.WriteLine($"SUCCESS {checks} approval SQL checks");
  return;
 }
 Check(await db.WarehousePrepare(id,technician,assignment,ct) is not null,"Non-warehouse technician cannot prepare inventory");
 Check(await db.InstitutionalChange(id,warehouse,"approve",new(Note:"Autorizado"),ct) is not null,"Generic approval cannot bypass stock assignment");
 Check(await db.WarehousePrepare(id,warehouse,new("Equipo distinto",[new(0,"Router","INV-1",1),new(0,"Router","INV-2",1)]),ct) is not null,"Cannot substitute another equipment type for the requested one");
 Check(await db.WarehousePrepare(id,warehouse,new("Serial no registrado",[new(0,"Inversor","UNKNOWN-1",1),new(0,"Inversor","UNKNOWN-2",1)]),ct) is not null,"Cannot prepare serials absent from actual inventory");
 Check(await db.WarehousePrepare(id,warehouse,assignment,ct) is null,"Preparation reserves two actual units");
 var stillReceived=await db.InstitutionalDetail(id,support,ct);
 Check(stillReceived?.Status=="received"&&stillReceived.WarehouseAssigned.Count==0,"Requester stays received and cannot see internal assignment before approval");
 Check(await db.WarehouseApprovePrepared(id,warehouse,new("Autorizado por la encargada"),ct) is null,"Authorized warehouse user places the final seal");
 Check(await db.InstitutionalChange(id,support,"trash",new(),ct) is not null,"Pending equipment cannot lose its authorized proof through trash");
 var available=await db.WarehouseAvailableInventory(warehouse,ct);
 Check(available.Single().Available==1&&available.Single().Serials.SequenceEqual(new[]{"INV-3"}),"Available quantity and serial list exclude pending units");
 var pending=await db.WarehouseAllocations(support,ct);
 Check(pending.Count==2&&pending.All(row=>row.Status=="PENDING_PICKUP"),"Requester sees pending pickup table");
 Check((await db.WarehouseAllocations(otherDepartment,ct)).Count==0,"Other department cannot see allocations");
 var authorized=await db.InstitutionalDetail(id,support,ct);
 Check(authorized?.WarehouseAssigned.Count==2&&authorized.Status=="approved","Requester receives assigned serials on approved document");
 Check(await db.WarehousePrepare(id,warehouse,assignment,ct) is not null,"Repeated preparation cannot reserve twice");
 var competing=await Upload(2);
 await db.InstitutionalChange(competing,warehouse,"receive",new(),ct);
 Check(await db.WarehousePrepare(competing,warehouse,new("Preparado",[new(0,"Inversor","INV-3",1),new(0,"Inversor","INV-4",1)]),ct) is not null,"Insufficient stock rejects entire reservation");
 Check((await db.WarehouseAllocations(warehouse,ct)).Count==2,"Failed reservation leaves no partial rows");
 Check(await db.TestReservedGuard("GENERAL_SERVICES","Inversor","INV-1",1,ct) is not null,"Manual exit cannot consume reserved serial");
 Check(await db.TestReservedGuard("GENERAL_SERVICES","Inversor","OTHER",2,ct) is not null,"Manual exit cannot consume reserved quantity");
 Check(await db.TestReservedGuard("GENERAL_SERVICES","Inversor","INV-3",1,ct) is null,"Unreserved unit remains usable");
 Check(await db.WarehouseRemoveInventory(warehouse,new("GENERAL_SERVICES","Inversor",1),ct) is not null,"Inventory removal protects pending pickup units");
 var qr=$"https://example.invalid/api/institutional-documents/verify/{authorized!.VerificationToken}";
 Check(Database.TestQrToken(qr)==authorized.VerificationToken,"Normal authorized seal URL remains readable");
 var keyboardQr=$"httpsÑ--example.invalid-api-institutional'documents-verify-{authorized.VerificationToken}";
 Check(Database.TestQrToken(keyboardQr)==authorized.VerificationToken,"USB scanner keyboard punctuation is normalized without changing the token");
 Check(Database.TestQrToken($"https://example.invalid/api/institutional-documents/verify/{authorized.VerificationToken!.ToUpperInvariant()}")==authorized.VerificationToken,"Uppercase scanner token remains valid");
 Check(Database.TestQrToken(authorized.VerificationToken)==authorized.VerificationToken,"Direct verification tokens remain supported");
 Check(Database.TestQrToken(new string('x',2049)+keyboardQr) is null,"Oversized scanner input is rejected");
 Check(Database.TestQrToken($"https://example.invalid/wrong-route/{authorized.VerificationToken}") is null,"Unknown QR route is not accepted");
 Check(Database.TestQrToken(keyboardQr+"a") is null,"QR token must contain exactly 48 hexadecimal characters");
 Check((await db.WarehouseWithdraw(warehouse,new(qr,"Persona receptora",competing),ct)).Error is not null,"QR from a different request card cannot withdraw equipment");
 Check((await db.WarehouseWithdraw(technician,new(qr,"Persona receptora"),ct)).Error is not null,"Technician cannot register warehouse withdrawal");
 Check((await db.WarehouseWithdraw(warehouse,new("invalid","Persona receptora"),ct)).Error is not null,"Unrecognized seal cannot withdraw stock");
 var result=await db.WarehouseWithdraw(warehouse,new(keyboardQr,"Persona receptora"),ct);
 Check(result.Error is null&&result.Delivery?.ReceivedBy=="Persona receptora","Authorized QR registers physical withdrawal");
 var withdrawn=await db.WarehouseAllocations(support,ct);
 Check(withdrawn.Count==2&&withdrawn.All(row=>row.Status=="WITHDRAWN"&&row.DeliveredBy==warehouse.Name&&row.ReceivedBy=="Persona receptora"&&row.WithdrawnUtc is not null&&row.AgencyCode=="6010183"&&row.DeliveryNumber==result.Delivery!.Number),"Withdrawn table retains delivery, recipient, time, agency and proof");
 Check((await db.WarehouseAvailableInventory(warehouse,ct)).Single().Available==1,"Withdrawal does not subtract stock twice");
 var repeat=await db.WarehouseWithdraw(warehouse,new(qr,"Otra persona"),ct);
 Check(repeat.Error is not null,"Second scan is rejected without duplicate delivery");
 await using(var count=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements WHERE movement_type='NEW_DELIVERY'",connection))Check(Convert.ToInt32(await count.ExecuteScalarAsync())==2,"Exactly one outbound movement per assigned unit");
 Check((await db.InstitutionalDetail(id,support,ct))?.WarehouseDelivery?.DeliveredLogin=="almacen.qa","Delivery proof stores authenticated warehouse login");
 var raceA=await Upload(1);var raceB=await Upload(1);
 await db.InstitutionalChange(raceA,warehouse,"receive",new(),ct);
 await db.InstitutionalChange(raceB,warehouse,"receive",new(),ct);
 var race=await Task.WhenAll(db.WarehousePrepare(raceA,warehouse,new("Preparacion concurrente",[new(0,"Inversor","INV-3",1)]),ct),db.WarehousePrepare(raceB,warehouse,new("Preparacion concurrente",[new(0,"Inversor","INV-4",1)]),ct));
 Check(race.Count(error=>error is null)==1,"Concurrent preparation cannot oversell the last unit");
 var raceWinner=race[0] is null?raceA:raceB;
 Check(await db.WarehouseApprovePrepared(raceWinner,warehouse,new("Autorizado para retiro"),ct) is null,"Race winner receives final authorization");
 var raceDocument=(await db.InstitutionalDetail(raceWinner,support,ct))!;
 var raceQr=$"https://example.invalid/api/institutional-documents/verify/{raceDocument.VerificationToken}";
 var simultaneous=await Task.WhenAll(db.WarehouseWithdraw(warehouse,new(raceQr,"Primera persona"),ct),db.WarehouseWithdraw(warehouse,new(raceQr,"Segunda persona"),ct));
 Check(simultaneous.Count(result=>result.Error is null)==1,"Simultaneous QR scans create only one delivery");
 Check((await db.WarehouseAvailableInventory(warehouse,ct)).All(row=>row.Available==0),"All withdrawn units leave zero available stock");
 await Exec(connection,"""
   INSERT INTO maintenance_movements(id,agency_id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at)
   VALUES(NEWID(),NULL,'GENERAL_SERVICES','ENTRY','Inversor','INV-LEGACY',1,'WAREHOUSE','ENTRY-LEGACY','REAL-MNT|ENTRY-LEGACY',SYSUTCDATETIME());
   INSERT INTO maintenance_movements(id,agency_id,department,technician_user_id,technician_name,movement_type,equipment_type,quantity,notes,operational_area,document_number,qr_token,occurred_at)
   VALUES(NEWID(),'11111111-1111-1111-1111-111111111111','GENERAL_SERVICES','technician.sg','Tecnico SG','REQUEST','Inversor',1,N'Renglones del comprobante:'+CHAR(10)+N'1. Equipo: Inversor · 1','WAREHOUSE','SOL-ALM-LEGACY','REAL-MNT|SOL-ALM-LEGACY',SYSUTCDATETIME());
   """);
 var legacy=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Solicitud historica",Department=support.Department,OwnerUserName=support.Id,OwnerDisplayName=support.Name,RecipientDepartment="Almacén",Status="approved",ReceivedUtc=DateTime.UtcNow,DecisionUtc=DateTime.UtcNow,ReceiptCode="LEGACY-REC",VerificationToken=new string('c',48),OriginalFileName="SOL-ALM-LEGACY.pdf",ContentType="application/pdf"},"%PDF-QA"u8.ToArray(),ct);
 var recovered=await db.InstitutionalDetail(legacy,warehouse,ct);
 Check(recovered?.WarehouseRequest?.Lines.Count==1&&recovered.WarehouseRequest.AgencyCode=="6010183","Historic request recovers its existing ledger lines without modifying PDF");
 Check(await db.WarehousePrepare(legacy,warehouse,new("Asignacion de inventario historica",[new(0,"Inversor","INV-LEGACY",1)]),ct) is null,"Previously approved request can assign stock exactly once");
 Check(await db.WarehousePrepare(legacy,warehouse,new("Segundo intento",[new(0,"Inversor","INV-LEGACY",1)]),ct) is not null,"Historic stock assignment cannot duplicate reservation");
 Check((await db.WarehouseWithdraw(warehouse,new(new string('c',48),"Receptor historico",legacy),ct)).Error is null,"Historic request completes its card-bound QR pickup");
 await Exec(connection,"INSERT INTO maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(NEWID(),'GENERAL_SERVICES','ENTRY','Lámpara','DEL-1',2,'WAREHOUSE','QA-DEL-1','QA-DEL-1',SYSUTCDATETIME()),(NEWID(),'GENERAL_SERVICES','ENTRY','Lámpara','DEL-2',3,'WAREHOUSE','QA-DEL-2','QA-DEL-2',SYSUTCDATETIME());");
 Check(await db.WarehouseRemoveInventory(technician,new("GENERAL_SERVICES","Lámpara",5),ct) is not null,"Technician cannot delete warehouse inventory");
 Check(await db.WarehouseRemoveInventory(warehouse,new("GENERAL_SERVICES","Lámpara",4),ct) is not null,"Stale inventory quantity cannot be deleted");
 Check(await db.WarehouseRemoveInventory(warehouse,new("GENERAL_SERVICES","Lámpara",5),ct) is null,"Inventory removal clears all available lots atomically");
 Check((await db.WarehouseAvailableInventory(warehouse,ct)).All(row=>row.Name!="Lámpara"),"Removed inventory leaves no available stock");
 Check(await db.WarehouseRemoveInventory(warehouse,new("GENERAL_SERVICES","Lámpara",5),ct) is not null,"Repeated inventory deletion cannot subtract twice");
 await using(var audit=new SqlCommand("SELECT COUNT(*) FROM maintenance_movements WHERE equipment_type=N'Lámpara'",connection))Check(Convert.ToInt32(await audit.ExecuteScalarAsync())==4,"Original entries and removal audit remain in history");
 var beforeRepair=(await db.WarehouseAvailableInventory(warehouse,ct)).Single(i=>i.Name=="Inversor").Available;
 await Exec(connection,"""
 DECLARE @repair uniqueidentifier=NEWID();
 INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,operational_area,document_number,qr_token,occurred_at) VALUES(@repair,'GENERAL_SERVICES','ENTRY','Inversor','REPAIRED-QA',1,'WAREHOUSE','REPAIRED-QA','REPAIRED-QA',SYSUTCDATETIME());
 INSERT warehouse_repaired_receipts(movement_id,source_id) VALUES(@repair,NEWID());
 """);
 var afterRepair=(await db.WarehouseAvailableInventory(warehouse,ct)).Single(i=>i.Name=="Inversor");
 Check(afterRepair.Available==beforeRepair&&!afterRepair.Serials.Contains("REPAIRED-QA"),"Repaired receipt does not increase new equipment stock or serials");
 Console.WriteLine($"SUCCESS {checks} SQL integration checks");
}
finally
{
 if(created)
 {
  SqlConnection.ClearAllPools();
  await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");
  Console.WriteLine("Disposable QA database removed; real databases were not modified.");
 }
}
static async Task Exec(SqlConnection connection,string sql){await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync();}
partial class Database
{
 public static string Connection="";
 public static string? TestQrToken(string raw)=>WarehouseQrToken(raw);
 public async Task TestRequestQuantitySchema(CancellationToken ct){await using var c=await Open(ct);await EnsureWarehouseWithdrawalSchema(c,ct);}
 static object Db(object? value)=>value??DBNull.Value;
 static async Task<SqlConnection> Open(CancellationToken ct){var connection=new SqlConnection(Connection);await connection.OpenAsync(ct);return connection;}
 static Task EnsureMaintenanceSchema(SqlConnection connection,CancellationToken ct)=>Task.CompletedTask;
 static Task EnsureTicketWorkflowSchema(SqlConnection connection,CancellationToken ct)=>Task.CompletedTask;
 static Task AddTicketHistory(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string action,string? fromDepartment,string? toDepartment,string? fromTeam,string? toTeam,string userId,string actorName,string? comment,CancellationToken ct)=>Task.CompletedTask;
 static Task AddTicketNotifications(SqlConnection connection,SqlTransaction transaction,Guid ticketId,string eventType,string? recipientUserId,string? targetDepartment,string agencyGroup,string userId,string actorName,string message,bool notifySupervisors,CancellationToken ct)=>Task.CompletedTask;
 static async Task EnsureNotificationReceiptSchema(SqlConnection connection,CancellationToken ct){await using var command=new SqlCommand("IF OBJECT_ID('support_notification_receipts','U') IS NULL CREATE TABLE support_notification_receipts(user_id nvarchar(80),notification_key nvarchar(100),read_at datetime2 NULL,dismissed_at datetime2 NULL,PRIMARY KEY(user_id,notification_key));",connection);await command.ExecuteNonQueryAsync(ct);}
 public async Task<string?> TestReservedGuard(string department,string equipment,string serial,int quantity,CancellationToken ct){await using var connection=await Open(ct);await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);return await WarehouseProtectReservedStock(connection,tx,department,equipment,serial,quantity,ct);}
}
