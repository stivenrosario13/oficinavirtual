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
 CREATE TABLE agencies(id char(36) PRIMARY KEY,codigo nvarchar(30),terminal nvarchar(160),grupo nvarchar(160));
 CREATE TABLE maintenance_movements(id uniqueidentifier PRIMARY KEY,agency_id char(36),ticket_id uniqueidentifier,department varchar(40),technician_user_id nvarchar(80),technician_name nvarchar(160),movement_type varchar(30),equipment_type nvarchar(100),component_type nvarchar(100),failure_cause nvarchar(200),serial_number nvarchar(120),quantity int,notes nvarchar(2000),created_by_user_id nvarchar(80),created_by_name nvarchar(160),created_at datetime2 DEFAULT SYSUTCDATETIME(),operational_area varchar(20),document_number varchar(40) UNIQUE,qr_token nvarchar(200) UNIQUE,delivered_by_name nvarchar(160),received_by_name nvarchar(160),destination_name nvarchar(200),occurred_at datetime2);
 INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,failure_cause)
 VALUES('11111111-1111-1111-1111-111111111111','TECHNOLOGY','TRANSFER_TO_WORKSHOP','Printer','SERIAL-1',1,'sender','Sender login name','WAREHOUSE','ORIGINAL-1','REAL-MNT|ORIGINAL-1','Original deliverer','Failure'),
 ('22222222-2222-2222-2222-222222222222','TECHNOLOGY','TRANSFER_TO_WORKSHOP','Monitor','SERIAL-2',10000,'sender','Sender login name','WAREHOUSE','ALM-20260903-195201-970AB','REAL-MNT|ALM-20260903-195201-970AB','Original deliverer','Failure');
 """);
 var db=new Database();var ct=CancellationToken.None;var id=Guid.Parse("11111111-1111-1111-1111-111111111111");var id2=Guid.Parse("22222222-2222-2222-2222-222222222222");
 var actor=new WorkshopActor("worker","Technology","TECHNICAL_FAILURE","Workshop user","rosmarlin.vittini@grupotejeda.local");
 var warehouse=new WorkshopActor("diorly","Warehouse","WAREHOUSE","Diorly Abreu","diorly.abreu@grupotejeda.local");
 var ana=new WorkshopActor("ana","Warehouse","WAREHOUSE","Ana Rodríguez","ana.rodriguez@grupotejeda.local");
 var bryan=new WorkshopActor("bryan","Warehouse",null,"Bryan Arias","bryan.arias@grupotejeda.local");
 Check(warehouse.CanReceiveWarehouse,"Diorly can receive workshop deliveries");
 Check(ana.CanReceiveWarehouse,"Ana can receive workshop deliveries");
 Check(bryan.CanReceiveWarehouse,"Bryan can receive even without a support-team value");
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
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE' AND movement_type='ENTRY' AND serial_number='SERIAL-1'")==0,"Sending does not add warehouse inventory");
 Check((await Get(warehouse))[0].GetProperty("pendingWarehouse").GetBoolean(),"Warehouse pending receipt sees original seal QR");
 Check(!(await db.ReceiveWorkshopInWarehouse(id,actor,ct)).Ok,"Workshop cannot receive for warehouse");
 var receipts=await Task.WhenAll(db.ReceiveWorkshopInWarehouse(id,warehouse,ct),db.ReceiveWorkshopInWarehouse(id,warehouse,ct));
 Check(receipts.Count(r=>r.Ok)==1,"Concurrent warehouse reception creates exactly one entry");
 Check(!(await Get(warehouse))[0].GetProperty("pendingWarehouse").GetBoolean(),"Received equipment leaves pending list");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WORKSHOP' AND movement_type='EXIT' AND destination_name=N'Almacén' AND serial_number='SERIAL-1'")==1,"Repaired equipment leaves workshop inventory");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE' AND movement_type='ENTRY' AND serial_number='SERIAL-1'")==1,"Repaired equipment enters warehouse separately");
 var trace=(await Get(sender))[0];Check(trace.GetProperty("status").GetString()=="DELIVERED"&&trace.GetProperty("events").GetArrayLength()==5,"Original QR shows final status and complete chronology");
 Check(trace.GetProperty("events")[2].GetProperty("actorLogin").GetString()=="rosmarlin.vittini@grupotejeda.local","Work log records authenticated login");
 Check((await db.WorkshopTransition(id2,new("RECEIVE","SENT"),actor,ct)).Ok,"Second transfer receives independently");
 Check((await db.WorkshopTransition(id2,new("REPLACE","RECEIVED"),actor,ct)).Ok,"Replacement selects separate table");
 Check(!(await db.WorkshopTransition(id2,new("WORK","REPLACEMENT","Replaced faulty monitor","SERIAL-2"),actor,ct)).Ok,"Replacement cannot reuse original serial");
 Check((await db.WorkshopTransition(id2,new("WORK","REPLACEMENT","Faulty panel replaced by tested monitor","NEW-SERIAL-2"),actor,ct)).Ok,"Replacement stores new serial");
 Check((await db.WorkshopTransition(id2,new("DELIVER","REPLACEMENT"),actor,ct)).Ok,"Replacement can return to original deliverer");
 Check((await db.ReceiveWorkshopInWarehouse(id2,bryan,ct)).Ok,"Bryan receives replacement in warehouse");
 Check(await Scalar(conn,"SELECT quantity FROM maintenance_movements WHERE operational_area='WAREHOUSE' AND movement_type='ENTRY' AND serial_number='NEW-SERIAL-2'")==10000,"Warehouse receipt preserves all 10,000 transferred units");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE' AND movement_type='ENTRY' AND serial_number='NEW-SERIAL-2'")==1,"Warehouse entry references replacement equipment");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WORKSHOP' AND movement_type='EXIT' AND serial_number='SERIAL-2'")==1,"Workshop no longer counts original replacement serial as active");
 Check((await Get(sender,"REAL'MNTÇALM'20260903'195201'970AB"))[0].GetProperty("replacementSerial").GetString()=="NEW-SERIAL-2","Malformed original QR preserves replacement trace");
 await using(var command=new SqlCommand("SELECT TOP 1 qr_token FROM maintenance_movements WHERE movement_type='EXIT'",conn)){var token=(string)(await command.ExecuteScalarAsync())!;Check((await Get(sender,token)).GetArrayLength()==1,"Derived movement QR resolves original case");}
 var manualId=Guid.NewGuid();
 await Exec(conn,$"INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,created_by_user_id,created_by_name,operational_area,document_number,qr_token,failure_cause) VALUES('{manualId}','TECHNOLOGY','ENTRY','Monitor manual','MANUAL-1',1,'worker','Workshop user','WORKSHOP','TAL-MANUAL-QA','REAL-MNT|TAL-MANUAL-QA','Pantalla dañada');");
 Check((await Get(actor,"TAL-MANUAL-QA")).GetArrayLength()==1,"Manual workshop entry joins repair workflow without custody fields");
 Check((await db.WorkshopTransition(manualId,new("RECEIVE","SENT"),actor,ct)).Ok,"Manual entry can be received without original custodian");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE serial_number='MANUAL-1' AND operational_area='WORKSHOP' AND movement_type='ENTRY'")==1,"Receiving a manual entry does not duplicate workshop stock");
 Check((await db.WorkshopTransition(manualId,new("REPAIR","RECEIVED"),actor,ct)).Ok,"Manual entry can be repaired");
 Check((await db.WorkshopTransition(manualId,new("WORK","REPAIR","Reparación y prueba completada"),actor,ct)).Ok,"Manual entry keeps work history");
 Check((await db.WorkshopTransition(manualId,new("DELIVER","REPAIR"),actor,ct)).Ok,"Manual entry transfers to warehouse after repair");
 Check((await Get(ana,"TAL-MANUAL-QA"))[0].GetProperty("canReceiveWarehouse").GetBoolean(),"Ana sees pending warehouse receipt when scanning workshop QR");
 Check((await db.ReceiveWorkshopInWarehouse(manualId,ana,ct)).Ok,"Ana confirms workshop equipment reception in warehouse");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE serial_number='MANUAL-1' AND operational_area='WAREHOUSE' AND movement_type='ENTRY'")==1,"Manual repair adds one warehouse entry");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_workshop_events WHERE source_id='"+manualId+"' AND action='WAREHOUSE_RECEIVE' AND actor_login='ana.rodriguez@grupotejeda.local'")==1,"Ana's reception is recorded in the audit trail");
 Check((await Get(actor,"TAL-MANUAL-QA"))[0].GetProperty("status").GetString()=="DELIVERED","Manual repair QR retains final stage");

 foreach(var route in new[]{"PART","DISCHARGE"}){
  var caseId=Guid.NewGuid();var serialCode="QA-"+route;
  await Exec(conn,$"INSERT maintenance_movements(id,department,movement_type,equipment_type,serial_number,quantity,created_by_user_id,created_by_name,operational_area,document_number,qr_token,failure_cause) VALUES('{caseId}','TECHNOLOGY','ENTRY','Equipo QA','{serialCode}',1,'worker','Workshop user','WORKSHOP','TAL-{route}','REAL-MNT|TAL-{route}','Equipo averiado');");
  Check((await db.WorkshopTransition(caseId,new("RECEIVE","SENT"),actor,ct)).Ok,"Receive "+route+" fixture");
  if(route=="PART"){
   Check((await db.WorkshopTransition(caseId,new("PART","RECEIVED"),actor,ct)).Ok,"Select part replacement");
   Check(!(await db.WorkshopTransition(caseId,new("DELIVER","PART_REPLACEMENT"),actor,ct)).Ok,"Part replacement requires work record");
   Check((await db.WorkshopTransition(caseId,new("WORK","PART_REPLACEMENT","Pieza: fuente. Fuente reemplazada y probada."),actor,ct)).Ok,"Record replaced part without replacement equipment serial");
   Check((await db.WorkshopTransition(caseId,new("DELIVER","PART_REPLACEMENT"),actor,ct)).Ok,"Send repaired original equipment");
   Check((await db.ReceiveWorkshopInWarehouse(caseId,warehouse,ct)).Ok,"Receive part replacement in warehouse");
   Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE serial_number='QA-PART' AND operational_area='WAREHOUSE' AND movement_type='ENTRY'")==1,"Part replacement keeps original serial");
  }else{
   Check(!(await db.WorkshopTransition(caseId,new("DISCHARGE","RECEIVED",""),actor,ct)).Ok,"Discharge requires reason");
   Check(!(await db.WorkshopTransition(caseId,new("DISCHARGE","RECEIVED","Equipo irreparable"),other,ct)).Ok,"Other department cannot discharge");
   Check((await db.WorkshopTransition(caseId,new("DISCHARGE","RECEIVED","Placa dañada sin reparación"),actor,ct)).Ok,"Discharge records reason");
   Check(!(await db.WorkshopTransition(caseId,new("DISCHARGE","RECEIVED","Descarga repetida"),actor,ct)).Ok,"Discharge cannot repeat");
   Check(!(await db.WorkshopTransition(caseId,new("DELIVER","DISCHARGED"),actor,ct)).Ok,"Discharged equipment cannot be delivered");
   Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE serial_number='QA-DISCHARGE' AND operational_area='WAREHOUSE'")==0,"Discharge does not enter warehouse");
   Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE serial_number='QA-DISCHARGE' AND operational_area='WORKSHOP' AND movement_type='DISCHARGE'")==1,"Discharge creates audited inventory movement");
   Check((await Get(actor,"TAL-DISCHARGE"))[0].GetProperty("status").GetString()=="DISCHARGED","QR shows discharge stage");
  }
 }

 Check(!new WorkshopActor("old","Technology","WORKSHOP","Old","old.login").CanOperate,"Former workshop role no longer grants management");
 Check(!new WorkshopActor("manager","Technology",null,"Manager","manager").CanOperate,"Other technology manager cannot manage workshop");
 Check(new WorkshopActor("michael","Technology","TECHNICAL_FAILURE","Michael","michael.miguel@grupotejeda.local").CanOperate,"Michael authorized");
 var warehouseBefore=await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'");
 await db.PrepareWorkshopRenewal(actor,ct);
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WORKSHOP'")==0,"Permanent reset removes workshop movements");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_workshop_events")==0,"Permanent reset removes workshop history");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'")==warehouseBefore,"Permanent reset preserves warehouse movements");
 Check(JsonDocument.Parse(await db.WorkshopSnapshot(actor,null,ct)).RootElement.GetArrayLength()==0,"Historic warehouse transfers do not reappear as new workshop entries");

 var intake=new WorkshopIntake(Guid.NewGuid(),"GENERAL_SERVICES","Monitores","No encienden",["BATCH-1","BATCH-2"]);
 Check(await db.CreateWorkshopIntake(intake,actor,ct) is null,"Atomic multiple serial intake");
 Check(await db.CreateWorkshopIntake(intake,actor,ct) is null,"Retry with same batch does not duplicate");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WORKSHOP'")==2,"Two serials create exactly two pending cases");
 Check(await db.CreateWorkshopIntake(intake with {BatchId=Guid.NewGuid(),Serials=["BATCH-3","BATCH-1"]},actor,ct) is not null,"Duplicate existing serial rejects entire batch");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE serial_number='BATCH-3'")==0,"No partial intake on duplicate");
 Check(await db.CreateWorkshopIntake(intake with {BatchId=Guid.NewGuid(),Serials=["BAD","BAD"]},actor,ct) is not null,"Repeated serials rejected");
 Check(await db.CreateWorkshopIntake(intake with {BatchId=Guid.NewGuid(),Serials=["OTHER"]},other,ct) is not null,"Unauthorized user cannot create workshop intake");
 await db.PrepareWorkshopRenewal(actor,ct);
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WORKSHOP'")==2,"Reset runs only once and preserves new entries");
 var fresh=JsonDocument.Parse(await db.WorkshopSnapshot(actor,null,ct)).RootElement[0].GetProperty("id").GetGuid();
 Check((await db.WorkshopTransition(fresh,new("RECEIVE","SENT"),actor,ct)).Ok,"Manager can receive another department equipment");
 Check((await db.WorkshopTransition(fresh,new("PART","RECEIVED"),actor,ct)).Ok,"Route new entry to part replacement");
 Check(!(await db.WorkshopTransition(fresh,new("STOCK","PART_REPLACEMENT"),actor,ct)).Ok,"Inventory requires documented work");
 Check((await db.WorkshopTransition(fresh,new("WORK","PART_REPLACEMENT","Se cambió la fuente. Prueba correcta."),actor,ct)).Ok,"Register component change");
 Check((await db.WorkshopTransition(fresh,new("STOCK","PART_REPLACEMENT"),actor,ct)).Ok,"Repaired equipment goes to workshop inventory");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'")==warehouseBefore,"Workshop inventory does not alter warehouse");
 Check(!(await db.WorkshopTransition(fresh,new("STOCK","PART_REPLACEMENT"),actor,ct)).Ok,"Cannot duplicate inventory transition");
 Check((await db.WorkshopTransition(fresh,new("DELIVER","INVENTORY"),actor,ct)).Ok,"Workshop inventory can subsequently go to warehouse");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'")==warehouseBefore,"Sending stock stays pending receipt");
 Check((await db.ReceiveWorkshopInWarehouse(fresh,warehouse,ct)).Ok,"Receive stock in warehouse");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'")==warehouseBefore+1,"Warehouse receives exactly one item");
 var largeBatch=intake with {BatchId=Guid.NewGuid(),Serials=Enumerable.Range(1,20).Select(i=>"TABLE-LONG-"+i).ToArray(),Notes=new string('x',1500)};
 Check(await db.CreateWorkshopIntake(largeBatch,actor,ct) is null,"Large table fixture registers every equipment");
 var completeSnapshot=JsonDocument.Parse(await db.WorkshopSnapshot(actor,null,ct)).RootElement;
 Check(completeSnapshot.GetArrayLength()==22,"Large JSON table keeps every registered equipment");
 Check(completeSnapshot.EnumerateArray().Count(i=>i.GetProperty("status").GetString()=="SENT")==21,"New equipment visible in pending receipt table");
 var ordersBefore=completeSnapshot.GetArrayLength();var warehouseCount=await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'");
 var availableBody=new WorkshopAvailableIntake(Guid.NewGuid(),"Equipo disponible",["AVAILABLE-1","AVAILABLE-2"],"Fuera de reparación");
 Check(await db.CreateWorkshopAvailableInventory(availableBody,actor,ct) is null,"Direct available inventory registers independently");
 Check(await db.CreateWorkshopAvailableInventory(availableBody,actor,ct) is null,"Available inventory retry does not duplicate");
 Check(JsonDocument.Parse(await db.WorkshopAvailableInventory(ct)).RootElement.GetArrayLength()==2,"Available inventory lists two direct units");
 Check(JsonDocument.Parse(await db.WorkshopSnapshot(actor,null,ct)).RootElement.GetArrayLength()==ordersBefore,"Direct inventory does not create repair orders");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'")==warehouseCount,"Direct workshop stock does not alter warehouse");
 Check(await db.CreateWorkshopAvailableInventory(availableBody with {BatchId=Guid.NewGuid()},actor,ct) is not null,"Duplicate available serial rejected");
 Check(await db.CreateWorkshopAvailableInventory(availableBody with {BatchId=Guid.NewGuid(),Serials=["FORBIDDEN"]},other,ct) is not null,"Unauthorized available inventory rejected");
 Check(await db.CreateWorkshopIntake(intake with {BatchId=Guid.NewGuid(),Serials=["AVAILABLE-1"]},actor,ct) is not null,"Available serial cannot be duplicated as repair order");
 var repaired=JsonDocument.Parse(await db.WarehouseRepairedInventory(ct)).RootElement;
 Check(repaired.EnumerateArray().Any(i=>i.GetProperty("sourceId").GetGuid()==fresh&&i.GetProperty("available").GetInt32()==1),"Warehouse receives repaired equipment in separate inventory");
 var fiscalized=await db.ImportFiscalizedWorkshopStock(false,actor,ct);
 Check(fiscalized.Error is null&&fiscalized.Added==82,"Fiscalized file imports exactly 82 unique known serials");
 var repeated=await db.ImportFiscalizedWorkshopStock(false,actor,ct);
 Check(repeated.Added==0&&repeated.AlreadyPresent==82,"Import retry is idempotent");
 var includeMissing=await db.ImportFiscalizedWorkshopStock(true,actor,ct);
 Check(includeMissing.Added==1&&includeMissing.AlreadyPresent==82,"Optional missing serial gets one explicit internal identifier");
 Check(JsonDocument.Parse(await db.WorkshopSnapshot(actor,null,ct)).RootElement.GetArrayLength()==ordersBefore,"Fiscalized import creates no repair orders");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM maintenance_movements WHERE operational_area='WAREHOUSE'")==warehouseCount,"Fiscalized import does not alter warehouse");
 Check((await db.ImportFiscalizedWorkshopStock(false,other,ct)).Error is not null,"Unauthorized fiscalized import refused");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM workshop_available_inventory WHERE serial='AVAILABLE-1'")==1,"Existing available stock preserved");
 Console.WriteLine(count+" workshop integration checks passed");
 await Exec(conn,"CREATE TABLE qa_stock_tickets(id uniqueidentifier PRIMARY KEY);CREATE TABLE qa_stock_history(ticket_id uniqueidentifier,action varchar(40),comment nvarchar(max));");
 var stock=JsonDocument.Parse(await db.WorkshopAvailableInventory(ct)).RootElement.EnumerateArray().First(x=>x.GetProperty("serial").GetString()=="AVAILABLE-1").GetProperty("id").GetGuid();
 try{await db.TestStockExit(stock,true,ct);throw new Exception("Expected rollback");}catch(InvalidOperationException){}
 Check(await Scalar(conn,"SELECT COUNT(*) FROM qa_stock_tickets")==0&&await Scalar(conn,"SELECT COUNT(*) FROM workshop_stock_exits")==0,"Ticket and stock exit rollback together");
 var stockTicket=await db.TestStockExit(stock,false,ct);
 Check(!JsonDocument.Parse(await db.WorkshopAvailableInventory(ct)).RootElement.EnumerateArray().Any(x=>x.GetProperty("id").GetGuid()==stock),"Outgoing equipment leaves available list");
 Check(await db.WorkshopTicketReplacementSerial(stockTicket,ct)=="AVAILABLE-1","Ticket replacement serial comes from stock ledger");
 try{await db.TestStockExit(stock,false,ct);throw new Exception("Expected duplicate rejection");}catch(SqlException ex) when(ex.Number==50001){}
 Check(await Scalar(conn,"SELECT COUNT(*) FROM qa_stock_tickets")==1&&await Scalar(conn,"SELECT COUNT(*) FROM workshop_stock_exits")==1,"Duplicate exit cannot create a second ticket");
 Check(await Scalar(conn,"SELECT COUNT(*) FROM workshop_available_inventory WHERE serial='AVAILABLE-1'")==1,"Exit preserves original stock history");
 Console.WriteLine(count+" total workshop and stock-exit checks passed");
 var repairedBatch=Guid.NewGuid();Check(await db.CreateWorkshopIntake(new(repairedBatch,"TECHNOLOGY","Monitor QA","Falla",["EXIT-REPAIRED"]),actor,ct) is null,"Create repair fixture");
 var repairedId=JsonDocument.Parse(await db.WorkshopSnapshot(actor,null,ct)).RootElement.EnumerateArray().First(x=>x.GetProperty("serialNumber").GetString()=="EXIT-REPAIRED").GetProperty("id").GetGuid();
 Check((await db.WorkshopTransition(repairedId,new("RECEIVE","SENT"),actor,ct)).Ok,"Receive repair fixture");
 Check((await db.WorkshopTransition(repairedId,new("REPAIR","RECEIVED"),actor,ct)).Ok,"Repair fixture");
 Check((await db.WorkshopTransition(repairedId,new("WORK","REPAIR","Trabajo realizado y comprobado"),actor,ct)).Ok,"Record repair work");
 Check((await db.WorkshopTransition(repairedId,new("STOCK","REPAIR"),actor,ct)).Ok,"Repair fixture available");
 await db.TestStockExit(repairedId,false,ct);
 Check(!JsonDocument.Parse(await db.WorkshopAvailableInventory(ct)).RootElement.EnumerateArray().Any(x=>x.GetProperty("id").GetGuid()==repairedId),"Repaired equipment can exit through a ticket");
 Check(!(await db.WorkshopTransition(repairedId,new("DELIVER","INVENTORY"),actor,ct)).Ok,"Exited equipment cannot also be sent to warehouse");
 Console.WriteLine(count+" final workshop checks passed");
 await Exec(conn,"CREATE TABLE support_departments(code varchar(40) PRIMARY KEY);INSERT support_departments VALUES('TECHNOLOGY'),('GENERAL_SERVICES'),('HUMAN_RESOURCES');CREATE TABLE support_categories(department_code varchar(40),code varchar(80),name nvarchar(160),description nvarchar(500),default_priority varchar(20),requires_detail bit,detail_label nvarchar(160),display_order int,is_active bit,UNIQUE(department_code,code));");
 var catalogSource=System.IO.File.ReadAllText("api/DatabaseSqlServerScoped.cs");var seed=System.Text.RegularExpressions.Regex.Match(catalogSource,"const string newAgencyCategorySql=\"\"\"([\\s\\S]*?)\"\"\";").Groups[1].Value;
 await Exec(conn,seed);await Exec(conn,seed);Check(await Scalar(conn,"SELECT COUNT(*) FROM support_categories WHERE code='NEW_AGENCY'")==2,"Agency-new category seeded once in Technology and General Services only");
 await Exec(conn,"UPDATE support_categories SET is_active=0 WHERE department_code='TECHNOLOGY'");await Exec(conn,seed);Check(await Scalar(conn,"SELECT COUNT(*) FROM support_categories WHERE department_code='TECHNOLOGY' AND is_active=0")==1,"Repeated catalog loading preserves administrator settings");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
static async Task<int> Scalar(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
partial class Database{
 public async Task<Guid> TestStockExit(Guid stock,bool fail,CancellationToken ct){await using var c=await Open(ct);await EnsureWorkshopStockExitSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var ticket=Guid.NewGuid();try{await using(var insert=new SqlCommand("INSERT qa_stock_tickets VALUES(@id)",c,tx)){insert.Parameters.AddWithValue("@id",ticket);await insert.ExecuteNonQueryAsync(ct);}await RecordWorkshopStockExit(c,tx,stock,ticket,"worker","Workshop QA",ct);if(fail)throw new InvalidOperationException("Test rollback");await tx.CommitAsync(ct);return ticket;}catch{await tx.RollbackAsync(ct);throw;}}
 static async Task AddTicketHistory(SqlConnection c,SqlTransaction tx,Guid ticket,string action,string? from,string? to,string? fromTeam,string? toTeam,string user,string name,string? comment,CancellationToken ct){await using var cmd=new SqlCommand("INSERT qa_stock_history VALUES(@ticket,@action,@comment)",c,tx);cmd.Parameters.AddWithValue("@ticket",ticket);cmd.Parameters.AddWithValue("@action",action);cmd.Parameters.AddWithValue("@comment",(object?)comment??DBNull.Value);await cmd.ExecuteNonQueryAsync(ct);}
 public const string Connection="Server=localhost;Database=Codex_Workshop_Flow_QA;Integrated Security=True;TrustServerCertificate=True";
 static Task EnsureMaintenanceSchema(SqlConnection c,CancellationToken ct)=>Task.CompletedTask;
 static object Db(object? v)=>v??DBNull.Value;
 async Task<SqlConnection> Open(CancellationToken ct){var c=new SqlConnection(Connection);await c.OpenAsync(ct);return c;}
}
record TicketActionResult(bool Ok,string Code,string? Error);
class LiveNotificationBroker{public void Publish(string name){}}
