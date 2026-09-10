using Microsoft.Data.SqlClient;
using System.Text.Json;

// Runs the production document/requisition code against a disposable local database.
const string name="Codex_Maintenance_V271_QA";
const string master="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True";
await using var root=new SqlConnection(master);await root.OpenAsync();
await Exec(root,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'QA database already exists; refusing to overwrite',1; CREATE DATABASE [{name}];");
int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
try{
 await using var conn=new SqlConnection(Database.Connection);await conn.OpenAsync();
 await Exec(conn,"""
 CREATE TABLE agencies(id char(36) PRIMARY KEY,grupo nvarchar(120));
 CREATE TABLE admin_user_groups(user_id nvarchar(80),group_name nvarchar(120));
 CREATE TABLE maintenance_movements(id uniqueidentifier PRIMARY KEY,document_number varchar(40),department varchar(40),operational_area varchar(20),movement_type varchar(40),technician_user_id nvarchar(80),agency_id char(36));
 CREATE TABLE maintenance_requisitions(id uniqueidentifier PRIMARY KEY,requisition_number varchar(40),department varchar(40),product_name nvarchar(160),quantity_requested int,priority varchar(20),status varchar(20),notes nvarchar(2000),requested_by_name nvarchar(160),created_at datetime2 DEFAULT SYSUTCDATETIME(),updated_at datetime2,CONSTRAINT CK_maintenance_requisition_status CHECK(status IN('PENDING','ORDERED','PARTIAL','FULFILLED','CANCELLED')));
 INSERT INTO maintenance_movements VALUES('11111111-1111-1111-1111-111111111111','DEMO-001','TECHNOLOGY','WAREHOUSE','ENTRY','tech',NULL);
 INSERT INTO maintenance_requisitions(id,requisition_number,department,product_name,quantity_requested,priority,status,requested_by_name) VALUES('22222222-2222-2222-2222-222222222222','REQ-DEMO','TECHNOLOGY','Equipo de prueba',1,'MEDIUM','PENDING','Solicitante');
 """);
 var db=new Database();var ct=CancellationToken.None;var id=Guid.Parse("11111111-1111-1111-1111-111111111111");
 var initial=JsonDocument.Parse((await db.MaintenanceDocument(id,"warehouse","Technology","WAREHOUSE",ct))!);
 Check(!initial.RootElement.TryGetProperty("receivedAt",out _),"Opening PDF does not mark receipt");
 Check(await db.MaintenanceDocument(id,"other","GeneralServices",null,ct)==null,"Other department cannot read document");
 Check(!(await db.MarkMaintenanceDocument(id,new("RECEIVE"),"other","GeneralServices",null,"Other","other",ct)).Ok,"Other department cannot receive");
 Check(!(await db.MarkMaintenanceDocument(id,new("RECEIVE",12,200),"warehouse","Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Receipt is blocked until both parties sign");
 await db.SaveOwnSignature("warehouse","PNG-DEMO",ct);Check(await db.OwnSavedSignature("warehouse",ct)=="PNG-DEMO"&&await db.OwnSavedSignature("second",ct)==null,"Saved signature is private to its owner");
 Check(!(await db.MarkMaintenanceDocument(id,new("SIGN",109,191,"PNG-ONCE"),"warehouse","Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Signature requires an explicit role");
 Check((await db.MarkMaintenanceDocument(id,new("SIGN",109,191,"PNG-RECEIPT","RECEIPT"),"warehouse","Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Receipt signature applied");
 Check(await db.OwnSavedSignature("warehouse",ct)=="PNG-DEMO","Use-once does not replace reusable signature");
 Check(!(await db.MarkMaintenanceDocument(id,new("SIGN",109,191,"FORGED","RECEIPT"),"second","Technology","WAREHOUSE","Other","other",ct)).Ok,"Another operator cannot replace receipt signer");
 Check((await db.MarkMaintenanceDocument(id,new("SIGN",109,191,"PNG-DELIVERY","DELIVERY"),"second","Technology","WAREHOUSE","Sender","sender.login",ct)).Ok,"Different person can sign delivery");
 var signed=JsonDocument.Parse((await db.MaintenanceDocument(id,"warehouse","Technology","WAREHOUSE",ct))!);
 Check(signed.RootElement.GetProperty("deliverySignature").GetProperty("data").GetString()=="PNG-DELIVERY"&&signed.RootElement.GetProperty("receiptSignature").GetProperty("data").GetString()=="PNG-RECEIPT","Both signatures persist independently");
 Check(!signed.RootElement.GetProperty("canSignDelivery").GetBoolean()&&signed.RootElement.GetProperty("canSignReceipt").GetBoolean()&&signed.RootElement.GetProperty("canReceive").GetBoolean(),"UI permissions match slot ownership");
 Check((await db.MarkMaintenanceDocument(id,new("RECEIVE",12,200),"warehouse","Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Receive with authenticated identity after both signatures");
 Check(!(await db.MarkMaintenanceDocument(id,new("RECEIVE"),"admin","Administrator",null,"Admin","admin",ct)).Ok,"Duplicate receipt blocked even for admin");
 var receipt=JsonDocument.Parse((await db.MaintenanceDocument(id,"warehouse","Technology","WAREHOUSE",ct))!);
 var time=receipt.RootElement.GetProperty("receivedAt").GetString();Check(receipt.RootElement.GetProperty("receiverLogin").GetString()=="receiver.login","Stored receiver is not creator");
 Check(!(await db.MarkMaintenanceDocument(id,new("MOVE",20,100),"second","Technology","WAREHOUSE","Second","second",ct)).Ok,"Another operator cannot move receipt");
 Check((await db.MarkMaintenanceDocument(id,new("MOVE",20,100),"warehouse","Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Receipt owner can move stamp");
 var moved=JsonDocument.Parse((await db.MaintenanceDocument(id,"warehouse","Technology","WAREHOUSE",ct))!);Check(moved.RootElement.GetProperty("receivedAt").GetString()==time&&moved.RootElement.GetProperty("stampX").GetDouble()==20,"Moving preserves original receipt time");
 await Exec(conn,"UPDATE maintenance_document_marks SET signature_data='LEGACY-UNASSIGNED',signer_login='legacy' WHERE movement_id='11111111-1111-1111-1111-111111111111';");
 var legacy=JsonDocument.Parse((await db.MaintenanceDocument(id,"warehouse","Technology","WAREHOUSE",ct))!);Check(legacy.RootElement.GetProperty("signatureData").GetString()=="LEGACY-UNASSIGNED"&&legacy.RootElement.GetProperty("deliverySignature").GetProperty("login").GetString()=="sender.login","Historical signature is preserved without role reassignment");
 var req=Guid.Parse("22222222-2222-2222-2222-222222222222");
 Check(!(await db.DecideWarehouseRequirement(req,new("APPROVED","Disponible"),"Technology",null,"Dept","dept",ct)).Ok,"Department cannot authorize its own requirement");
 Check((await db.DecideWarehouseRequirement(req,new("APPROVED","Equipo disponible"),"Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Warehouse authorizes pending requirement");
 Check(!(await db.DecideWarehouseRequirement(req,new("REJECTED","Otra decisión"),"Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Repeated decision is blocked");
 Check((await db.SendWarehouseRequest(new("GENERAL_SERVICES","Revisar equipo",2,"Prueba"),"Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Warehouse can send departmental request");
 var board=JsonDocument.Parse((await db.WarehouseRequestBoard("Technology",null,ct))!);Check(board.RootElement.GetProperty("requests").GetArrayLength()==0,"Requests scoped to destination department");
 Check(board.RootElement.GetProperty("requirements")[0].GetProperty("decidedByLogin").GetString()=="receiver.login","Decision keeps authenticated login");
 await Exec(conn,"UPDATE maintenance_requisitions SET status='PENDING' WHERE id='22222222-2222-2222-2222-222222222222';");
 Check((await db.DecideWarehouseRequirement(req,new("REJECTED","No corresponde a este equipo"),"Technology","WAREHOUSE","Receiver","receiver.login",ct)).Ok,"Warehouse rejects pending requirement");
 var rejected=JsonDocument.Parse((await db.WarehouseRequestBoard("Technology",null,ct))!);Check(rejected.RootElement.GetProperty("requirements")[0].GetProperty("status").GetString()=="REJECTED","Rejection persists in department board");
 Check(JsonDocument.Parse((await db.MaintenanceTemplates("Technology",null,ct))!).RootElement.GetArrayLength()==0,"Empty template library is an array");
 await db.AddMaintenanceTemplate("Plantilla de prueba","Descripción","FORMS","TECHNOLOGY","test.pdf","%PDF-test"u8.ToArray(),"Publisher",ct);
 var templates=JsonDocument.Parse((await db.MaintenanceTemplates("Technology",null,ct))!);var templateId=templates.RootElement[0].GetProperty("id").GetGuid();
 Check(await db.MaintenanceTemplateFile(templateId,"GeneralServices",null,ct)==null,"Other department cannot fetch template bytes");
 Check(await db.MaintenanceTemplateFile(templateId,"Technology",null,ct)!=null,"Owner department can fetch template bytes");
 Check(await db.ArchiveMaintenanceTemplate(templateId,true,"Publisher",ct),"Template can be archived");
 Check(await db.MaintenanceTemplateFile(templateId,"Technology",null,ct)==null,"Archived file is hidden from department");
 Check(await db.MaintenanceTemplateFile(templateId,"Technology","WAREHOUSE",ct)!=null,"Warehouse retains access for recovery");
 Check(await db.ArchiveMaintenanceTemplate(templateId,false,"Publisher",ct)&&await db.MaintenanceTemplateFile(templateId,"Technology",null,ct)!=null,"Archive restoration preserves file");
 var docOwner=new DocumentActor("owner","Technology","","Owner","owner.real");var otherDept=new DocumentActor("other","GeneralServices","","Other","other.real");var recipient=new DocumentActor("recipient","Technology","","Recipient","recipient.real");var warehouse=new DocumentActor("warehouse","Technology","WAREHOUSE","Warehouse","warehouse.real");
 var fid=await db.InstitutionalUpload(new(){Title="Archivo privado",OwnerUserName=docOwner.Id,OwnerDisplayName=docOwner.Name,Department=docOwner.Department,OriginalFileName="test.pdf",ContentType="application/pdf",Size=5},"%PDF-"u8.ToArray(),ct);
 Check(await db.InstitutionalDetail(fid,otherDept,ct)==null,"Other department cannot read unshared files");
 Check(await db.InstitutionalBytes(fid,otherDept,ct)==null,"Other department cannot download unshared bytes");
 Check((await db.InstitutionalList(otherDept,"files","requests",ct)).Count==0,"List does not leak another department's files");
 Check(await db.InstitutionalChange(fid,docOwner,"share",new(RecipientDepartment:otherDept.Department,CanEdit:false),ct)==null,"Owner shares with a department");
 Check(await db.InstitutionalBytes(fid,otherDept,ct)!=null,"Shared department can download");
 Check(await db.InstitutionalChange(fid,otherDept,"edit",new(Title:"Forbidden rename"),ct)!=null,"Read-only share cannot edit");
 var sharedFile=(await db.InstitutionalDetail(fid,docOwner,ct))!;var shareId=sharedFile.SharedWith.Single().Id;Check(shareId<9007199254740991,"Share ID is safe in JavaScript");
 Check(await db.InstitutionalChange(fid,docOwner,"unshare",new(ShareId:shareId),ct)==null&&await db.InstitutionalBytes(fid,otherDept,ct)==null,"Revocation immediately removes byte access");
 Check(await db.InstitutionalChange(fid,otherDept,"purge",new(),ct)!=null,"Non-owner cannot purge");
 Check(await db.InstitutionalChange(fid,docOwner,"purge",new(),ct)!=null,"Active file cannot be permanently deleted");
 Check(await db.InstitutionalChange(fid,docOwner,"trash",new(),ct)==null&&await db.InstitutionalDetail(fid,recipient,ct)==null,"Trash hides file from department colleagues");
 Check(await db.InstitutionalChange(fid,docOwner,"restore",new(),ct)==null&&(await db.InstitutionalDetail(fid,docOwner,ct))!.Status=="active","Restore preserves original file status");
 var commId=await db.InstitutionalUpload(new(){Area="communications",Channel="requests",Title="Solicitud",OwnerUserName=warehouse.Id,OwnerDisplayName=warehouse.Name,Department=warehouse.Department,RecipientDepartment=recipient.Department,RecipientUserName=recipient.Id,Status="pending",SentUtc=DateTime.UtcNow},"%PDF-"u8.ToArray(),ct);
 Check(await db.InstitutionalDetail(commId,docOwner,ct)==null,"Named-recipient communication is private from same-department coworkers");
 Check(await db.InstitutionalChange(commId,warehouse,"receive",new(),ct)!=null,"Sender cannot receive their own communication");
 var receipts=await Task.WhenAll(db.InstitutionalChange(commId,recipient,"receive",new(SealX:15,SealY:70),ct),db.InstitutionalChange(commId,recipient,"receive",new(SealX:40,SealY:30),ct));Check(receipts.Count(x=>x is null)==1,"Concurrent receipt requests produce exactly one receipt");
 var received=(await db.InstitutionalDetail(commId,recipient,ct))!;var receiptTime=received.ReceivedUtc;Check(received.ReceiverLogin==recipient.Login&&receiptTime!=null,"Receipt uses authenticated login and server time");
 Check(await db.InstitutionalChange(commId,warehouse,"move",new(),ct)!=null,"Sender cannot move another user's receipt");
 Check(await db.InstitutionalChange(commId,recipient,"move",new(SealX:21,SealY:65),ct)==null&&(await db.InstitutionalDetail(commId,recipient,ct))!.ReceivedUtc==receiptTime,"Moving a receipt preserves its timestamp");
 Check(await db.InstitutionalChange(commId,warehouse,"sign",new(SignatureData:"fake"),ct)!=null,"Sender cannot sign for recipient");
 Check(await db.InstitutionalChange(commId,recipient,"sign",new(SignatureData:"qa-signature"),ct)==null,"Receiver can store own receipt signature");
 Check(await db.InstitutionalChange(commId,warehouse,"trash",new(),ct)==null&&await db.InstitutionalChange(commId,warehouse,"restore",new(),ct)==null&&(await db.InstitutionalDetail(commId,recipient,ct))!.Status=="received","Trash/restore keeps receipt status and signatures");
 var requirementId=await db.InstitutionalUpload(new(){Area="communications",Channel="requirements",Title="Requerimiento",OwnerUserName=docOwner.Id,Department=docOwner.Department,RecipientDepartment="Almacén",Status="pending"},"%PDF-"u8.ToArray(),ct);
 Check(await db.InstitutionalChange(requirementId,docOwner,"approve",new(Note:"Auto aprobación prohibida"),ct)!=null,"Requester cannot authorize own requirement");
 Check(await db.InstitutionalChange(requirementId,warehouse,"approve",new(Note:"Equipo autorizado para entrega"),ct)==null&&(await db.InstitutionalDetail(requirementId,docOwner,ct))!.Status=="approved","Warehouse authorization is visible to requester");
 Check(await db.InstitutionalChange(requirementId,warehouse,"reject",new(Note:"Segundo cambio no válido"),ct)!=null,"Final decision cannot be overwritten");
 Check(await db.InstitutionalChange(requirementId,warehouse,"receive",new(),ct)==null&&(await db.InstitutionalDetail(requirementId,warehouse,ct))!.Status=="approved","Approved requirement can be received without losing approval");
 var tid=await db.InstitutionalSaveTemplate(docOwner,null,"Formato real","{\"subject\":\"Solicitud\"}",ct);Check(tid!=null,"Editable template persists");
 Check((await db.InstitutionalTemplates(otherDept,ct)).Count==0,"Template catalog respects department");
 Check(await db.InstitutionalSaveTemplate(recipient,tid,"Overwrite","{}",ct)==null,"Department coworker must copy another author's template");
 Check(!await db.InstitutionalDeleteTemplate(otherDept,tid!.Value,ct),"Only template author may delete");
 Check(await db.InstitutionalDeleteTemplate(docOwner,tid.Value,ct),"Template owner can delete");
 Console.WriteLine($"{checks} integration checks passed");
}finally{SqlConnection.ClearAllPools();await Exec(root,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");Console.WriteLine("Disposable QA database removed.");}
static async Task Exec(SqlConnection connection,string sql){await using var cmd=new SqlCommand(sql,connection);await cmd.ExecuteNonQueryAsync();}

partial class Database{
 public const string Connection="Server=localhost;Database=Codex_Maintenance_V271_QA;Integrated Security=True;TrustServerCertificate=True";
 static Task EnsureMaintenanceSchema(SqlConnection c,CancellationToken ct)=>Task.CompletedTask;
 static string DepartmentForRole(string role)=>role switch{"Technology"=>"TECHNOLOGY","GeneralServices"=>"GENERAL_SERVICES","HumanResources"=>"HUMAN_RESOURCES",_=>""};
 static object Db(object? value)=>value??DBNull.Value;
 async Task<SqlConnection> Open(CancellationToken ct){var c=new SqlConnection(Connection);await c.OpenAsync(ct);return c;}
}
record TicketActionResult(bool Ok,string Code,string? Error);
