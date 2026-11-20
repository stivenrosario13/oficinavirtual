using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.SqlClient;
int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
ClaimsPrincipal User(string role,string? team,string login,string? json=null){var claims=new List<Claim>{new(ClaimTypes.Role,role),new(ClaimTypes.Name,login)};if(team!=null)claims.Add(new("support_team",team));if(json!=null)claims.Add(new("permissions_json",json));return new(new ClaimsIdentity(claims,"QA"));}
Check(AreaManagerAccess.Scope(User("Warehouse","WAREHOUSE","bryan.arias@grupotejeda.local"))=="WAREHOUSE","Bryan warehouse scope");
Check(AreaManagerAccess.Scope(User("Technology","TECHNICAL_FAILURE","rosmarlin.vittini@grupotejeda.local"))=="TECHNICAL_FAILURE","Vittini technical failure and workshop scope");
Check(AreaManagerAccess.Scope(User("Technology","CALL_CENTER","yessica.mercedes@grupotejeda.local"))=="CALL_CENTER","Yessica Call Center manager scope");
Check(AreaManagerAccess.Scope(User("Warehouse","WAREHOUSE","diorly.abreu@grupotejeda.local"))==null,"Diorly not implicitly promoted");
Check(AreaManagerAccess.Scope(User("Technology","TECHNICAL_FAILURE","michael.miguel@grupotejeda.local"))==null,"Michael not implicitly promoted");
Check(AreaManagerAccess.Scope(User("Warehouse","WAREHOUSE","bryan.arias@grupotejeda.local","{\"canManageDepartment\":false}"))==null,"Explicit revocation respected");
Check(AreaManagerAccess.Scope(User("Administrator",null,"admin","{}"))=="ALL","Admin global scope");
var techManager=User("Technology",null,"manager","{\"canManageDepartment\":true}");
foreach(var code in new[]{"ADMINISTRATIVE","COMMUNICATION","COMMERCIALIZATION","TREASURY","ACCOUNTING","SUPERVISION","GENERAL_AUDIT","VAULT","CASHIER"}){
 var manager=User("Department",code,"manager","{\"canManageDepartment\":true}");
 Check(AreaManagerAccess.Scope(manager)==code,"Assigned department scoped: "+code);
 Check(AreaManagerAccess.RequestedScope(manager,"TECHNOLOGY") is null,"Cross-department audit denied: "+code);
 Check(AreaManagerAccess.Scope(User("Department",code,"staff","{}")) is null,"Ordinary staff not promoted: "+code);
}
Check(!DepartmentAccounts.ApiAllowed("/api/admin/users","GET")&&DepartmentAccounts.ApiAllowed("/api/tickets","GET")&&!DepartmentAccounts.ApiAllowed("/api/tickets/123","DELETE"),"Departmental ticket routes enabled but global administration/deletion denied");
Check(DepartmentAccounts.ApiAllowed("/api/department-support","GET")&&!DepartmentAccounts.ApiAllowed("/api/department-support","POST"),"Departmental support is read-only");
foreach(var department in new[]{"TREASURY","ACCOUNTING","COMMUNICATION","COMMERCIALIZATION","ADMINISTRATIVE","SUPERVISION"}){
 var staff=User("Department",department,"staff");
 var scoped=DepartmentAccounts.SupportScopeRole(staff,"/api/tickets");
 Check(DepartmentAccounts.SupportDepartment(scoped)==department,"Ticket scope from authenticated department: "+department);
 Check(DepartmentAccounts.SupportScopeRole(staff,"/api/account/password")=="Department","Account role unchanged outside support");
 Check(DepartmentAccounts.SupportDepartment(DepartmentAccounts.SupportScopeRole(staff,"/api/support/chat/notifications"))==department,"Chat notifications keep same department");
}
Check(DepartmentAccounts.SupportDepartment(DepartmentAccounts.SupportScopeRole(User("Department",null,"invalid"),"/api/tickets"))=="__DENIED__","Missing department fails closed, never global scope");
Check(DepartmentAccounts.SupportScopeRole(User("GeneralServices",null,"staff"),"/api/tickets")=="GeneralServices","Existing support role unchanged");
foreach(var area in new[]{"TECHNOLOGY","CALL_CENTER","TECHNICAL_FAILURE"})Check(AreaManagerAccess.RequestedScope(techManager,area)==area,"Technology manager may view "+area);
Check(AreaManagerAccess.RequestedScope(techManager,"GENERAL_SERVICES") is null,"Technology manager cannot request General Services");
var callManager=User("Technology","CALL_CENTER","manager","{\"canManageDepartment\":true}");
Check(AreaManagerAccess.RequestedScope(callManager,"ALL")=="CALL_CENTER"&&AreaManagerAccess.RequestedScope(callManager,"TECHNOLOGY") is null&&AreaManagerAccess.RequestedScope(callManager,"TECHNICAL_FAILURE") is null,"Call Center manager cannot widen scope");
foreach(var role in new[]{"GeneralServices","HumanResources","Technology"})Check(AreaManagerAccess.Scope(User(role,null,"manager","{\"canManageDepartment\":true}"))!=null,"Configurable manager "+role);
foreach(var json in new[]{"[]","{bad","{\"canManageDepartment\":\"true\"}"})Check(AreaManagerAccess.Scope(User("Warehouse","WAREHOUSE","bryan.arias@grupotejeda.local",json))==null,"Malformed permission denied");
const string name="Codex_Manager_Audit_QA";
var granted=new Dictionary<string,bool>{{"canManageDepartment",true},{"canManageUsers",false},{"canManageAudits",false}};
AreaManagerAccess.ApplyViews(granted,null);
Check(granted["canViewFieldAudits"]&&granted["canViewDashboard"]&&granted["canViewSupportDashboard"]&&!granted["canManageUsers"]&&!granted["canManageAudits"],"Manager gets read views without global administration");
AreaManagerAccess.ApplyViews(granted,new Dictionary<string,bool>{{"canViewFieldAudits",false}});
Check(!granted["canViewFieldAudits"],"Explicit field-audit read denial retained");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");await master.OpenAsync();
await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'QA database exists; refusing to overwrite',1; CREATE DATABASE [{name}];");
try{
 await using var c=await Database.Open(CancellationToken.None);
 await Exec(c,"""
 CREATE TABLE maintenance_movements(id uniqueidentifier,created_at datetime2,created_by_name nvarchar(160),created_by_user_id nvarchar(80),operational_area varchar(20),movement_type varchar(40),document_number nvarchar(80),equipment_type nvarchar(100),serial_number nvarchar(120),failure_cause nvarchar(200),notes nvarchar(2000),department varchar(40));
 CREATE TABLE maintenance_workshop_events(id uniqueidentifier,source_id uniqueidentifier,occurred_at datetime2,actor_name nvarchar(160),actor_login nvarchar(256),action varchar(40),status varchar(40),note nvarchar(2000));
 CREATE TABLE workshop_available_inventory(id uniqueidentifier,created_at datetime2,created_by_name nvarchar(160),created_by nvarchar(80),serial nvarchar(120),equipment nvarchar(100),notes nvarchar(2000));
 CREATE TABLE admin_users(id nvarchar(80),email nvarchar(256),role varchar(40),support_team varchar(40));
 CREATE TABLE support_tickets(id uniqueidentifier,ticket_number bigint,subject nvarchar(180),assigned_department varchar(40),assigned_team varchar(40),created_by_user_id nvarchar(80));
 CREATE TABLE support_ticket_history(id uniqueidentifier,ticket_id uniqueidentifier,created_at datetime2,actor_name nvarchar(160),actor_user_id nvarchar(80),action varchar(40),comment nvarchar(1000),from_department varchar(40),from_team varchar(40),to_department varchar(40),to_team varchar(40));
 CREATE TABLE institutional_files(id bigint,department nvarchar(80),metadata nvarchar(max));
 CREATE TABLE automatic_findings(id uniqueidentifier,resolved_at datetime2,resolved_by_name nvarchar(160),resolved_by_username nvarchar(256),status varchar(40),title nvarchar(180),diagnosis nvarchar(2000),resolution nvarchar(2000),verification nvarchar(2000),assigned_department varchar(40));
 CREATE TABLE automatic_finding_notifications(id uniqueidentifier,finding_id uniqueidentifier,created_at datetime2,actor_name nvarchar(160),event_type varchar(40),message nvarchar(500));
 CREATE TABLE agency_transition_progress(id uniqueidentifier,created_at datetime2,created_by_name nvarchar(160),created_by_user_id nvarchar(80),progress_status varchar(40),project_id uniqueidentifier,note nvarchar(2000),department varchar(40));
 CREATE TABLE maintenance_document_signatures(movement_id uniqueidentifier,signature_role varchar(10),signed_at datetime2,signer_name nvarchar(160),signer_login nvarchar(256));
 DECLARE @w uniqueidentifier=NEWID(),@g uniqueidentifier=NEWID(),@t uniqueidentifier=NEWID();
 INSERT maintenance_movements VALUES(@w,'2026-09-29T20:00:00',N'Diorly','diorly','WAREHOUSE','ENTRY','ALM-1','Monitor','SN1','Entrada','Stock','TECHNOLOGY'),(@g,'2026-09-29T20:00:00','Other','other','WORKSHOP','ENTRY','TAL-G','Motor','SN2','Roto','','GENERAL_SERVICES'),(@t,'2026-09-29T20:00:00','Michael','michael','WORKSHOP','ENTRY','TAL-T','Monitor','SN3','Roto','','TECHNOLOGY');
 INSERT maintenance_workshop_events VALUES(NEWID(),@t,'2026-09-29T20:01:00','Michael','michael','WORK','REPAIR','Cable cambiado');
 INSERT admin_users VALUES('diorly','diorly.abreu@grupotejeda.local','WAREHOUSE','WAREHOUSE'),('gs','gs@test','GENERAL_SERVICES',NULL),('gledys-id','gledys.tejeda@grupotejeda.local','ADMINISTRATIVE',NULL);
 DECLARE @tk uniqueidentifier=NEWID();INSERT support_tickets VALUES(@tk,1,'Falla almacén','GENERAL_SERVICES',NULL,'diorly');INSERT support_ticket_history VALUES(NEWID(),@tk,'2026-09-29T20:00:00','Técnico','gs','RESOLVED','Resuelto',NULL,NULL,'GENERAL_SERVICES',NULL);
 INSERT institutional_files VALUES(1,N'Servicios Generales',N'{"title":"SOL-ALM","createdUtc":"2026-09-29T19:00:00Z","ownerDisplayName":"Solicitante","ownerLogin":"sg","recipientDepartment":"Almacén","warehouseRequest":{},"history":[{"action":"approve","actor":"Diorly","login":"diorly","at":"2026-09-29T20:00:00Z","previousStatus":"received"}]}'),(2,N'Recursos Humanos',N'{"title":"PRIVADO-RH","createdUtc":"2026-09-29T20:00:00Z","history":[]}');
 """);
 await Exec(c,"INSERT institutional_files VALUES(4,N'Tecnología',N'{\"title\":\"INCOMING-ADMIN-QR\",\"createdUtc\":\"2026-09-29T19:00:00Z\",\"recipientDepartment\":\"Administración\",\"administrativeRequest\":{},\"history\":[{\"action\":\"deliver\",\"actor\":\"Gledys\",\"login\":\"gledys.tejeda\",\"at\":\"2026-09-29T20:00:00Z\",\"previousStatus\":\"approved\"}]}' );");
 var incoming=await new Database().DepartmentOperationalAudit("ADMINISTRATIVE",new DateTime(2026,9,29),new DateTime(2026,9,30),0,CancellationToken.None);
 Check(incoming.Contains("INCOMING-ADMIN-QR")&&incoming.Contains("gledys.tejeda")&&!incoming.Contains("PRIVADO-RH"),"Administrative audit includes incoming form delivery, excludes unrelated HR files");
 var db=new Database();var from=new DateTime(2026,9,29);var to=from.AddDays(1);
 await Exec(c,"""
 DECLARE @cc uniqueidentifier=NEWID(),@tf uniqueidentifier=NEWID(),@transfer uniqueidentifier=NEWID();
 INSERT support_tickets VALUES(@cc,21,'QA-CALL-ONLY','TECHNOLOGY','CALL_CENTER','gs'),(@tf,22,'QA-FAILURE-ONLY','TECHNOLOGY','TECHNICAL_FAILURE','gs'),(@transfer,23,'QA-TRANSFERRED-TO-TECHNICIANS','TECHNOLOGY','TECHNICIANS','gs');
 INSERT support_ticket_history VALUES(NEWID(),@cc,'2026-09-29T20:00:00','Call agent','gs','CREATED','QA-CALL-ONLY',NULL,NULL,'TECHNOLOGY','CALL_CENTER'),(NEWID(),@tf,'2026-09-29T20:00:00','Failure agent','gs','CREATED','QA-FAILURE-ONLY',NULL,NULL,'TECHNOLOGY','TECHNICAL_FAILURE'),(NEWID(),@transfer,'2026-09-29T20:02:00','Call supervisor','gs','ROUTED','QA-TRANSFERRED-TO-TECHNICIANS','TECHNOLOGY','CALL_CENTER','TECHNOLOGY','TECHNICIANS');
 """);
 await Exec(c,"ALTER TABLE admin_users ADD permissions_json nvarchar(max),updated_at datetime2;");
 await Exec(c,"INSERT admin_users(id,email,role,support_team,permissions_json) VALUES('bryan','bryan.arias@grupotejeda.local','WAREHOUSE','WAREHOUSE',N'{\"canManageDepartment\":false,\"canAuthorizeWarehouseRequests\":true,\"canLoadWarehouseInventory\":true,\"canManageUsers\":false}');");
 await Database.EnsureBryanManagerProfile(c,CancellationToken.None);
 async Task<string> Permission(string id){await using var cmd=new SqlCommand("SELECT COALESCE(permissions_json,'{}') FROM admin_users WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);return (string)(await cmd.ExecuteScalarAsync())!;}
 var bryan=JsonDocument.Parse(await Permission("bryan")).RootElement;
 Check(bryan.GetProperty("canManageDepartment").GetBoolean()&&bryan.GetProperty("canViewFieldAudits").GetBoolean()&&bryan.GetProperty("canViewDashboard").GetBoolean(),"Bryan assigned manager and read-only field/panel access");
 Check(bryan.GetProperty("canAuthorizeWarehouseRequests").GetBoolean()&&bryan.GetProperty("canLoadWarehouseInventory").GetBoolean()&&!bryan.GetProperty("canManageUsers").GetBoolean(),"Bryan existing warehouse permissions retained");
 Check(await Permission("diorly")=="{}","Other staff permissions unchanged");
 await Exec(c,"UPDATE admin_users SET permissions_json=JSON_MODIFY(permissions_json,'$.canManageDepartment',CAST(0 AS bit)) WHERE id='bryan';");
 await Database.EnsureBryanManagerProfile(c,CancellationToken.None);
 Check(!JsonDocument.Parse(await Permission("bryan")).RootElement.GetProperty("canManageDepartment").GetBoolean(),"One-time assignment respects subsequent revocation");
 async Task<string> Rows(string scope,int offset=0)=>await db.DepartmentOperationalAudit(scope,from,to,offset,CancellationToken.None);
 await Exec(c,"""
 INSERT admin_users(id,email,role) VALUES('emma','emma.mendoza@grupotejeda.local','ADMINISTRATOR');
 DECLARE @adminTicket uniqueidentifier=NEWID();
 INSERT support_tickets VALUES(@adminTicket,35,'ADMIN-REQUEST-ONLY','TECHNOLOGY','TECHNICAL_FAILURE','emma');
 INSERT support_ticket_history VALUES(NEWID(),@adminTicket,'2026-09-29T20:00:00','Emma','emma','CREATED','ADMIN-REQUEST-ONLY',NULL,NULL,'TECHNOLOGY','TECHNICAL_FAILURE');
 INSERT institutional_files VALUES(3,N'Administración',N'{"title":"ADMIN-DOCUMENT-ONLY","createdUtc":"2026-09-29T19:00:00Z","ownerDisplayName":"Emma","ownerLogin":"emma","history":[]}');
 """);
 await Exec(c,"INSERT support_ticket_history SELECT NEWID(),id,'2026-09-29T20:01:00','Warehouse actor','diorly','UPDATED','WAREHOUSE-MUST-NOT-APPEAR',NULL,NULL,'TECHNOLOGY','TECHNICAL_FAILURE' FROM support_tickets WHERE ticket_number=35;");
 var administrative=await Rows("ADMINISTRATIVE");
 Check(!administrative.Contains("WAREHOUSE-MUST-NOT-APPEAR"),"Administrative audit excludes warehouse actors even on administrative requests");
 Check(administrative.Contains("ADMIN-REQUEST-ONLY")&&administrative.Contains("ADMIN-DOCUMENT-ONLY"),"Administrative audit includes own outgoing tickets and documents");
 Check(!administrative.Contains("PRIVADO-RH")&&!administrative.Contains("QA-CALL-ONLY")&&!administrative.Contains("SOL-ALM")&&!administrative.Contains("Falla almac"),"Administrative audit excludes other departments");
 var warehouse=await Rows("WAREHOUSE");Check(warehouse.Contains("Diorly")&&warehouse.Contains("approve")&&warehouse.Contains("Falla almac"),"Warehouse manager sees staff, approvals, support");Check(!warehouse.Contains("TAL-G")&&!warehouse.Contains("PRIVADO-RH"),"Warehouse excludes foreign work and HR docs");
 var vittini=await Rows("TECHNICAL_FAILURE");Check(vittini.Contains("Cable cambiado")&&!vittini.Contains("SOL-ALM"),"Workshop manager work scope");
 Check(vittini.Contains("QA-TRANSFERRED-TO-TECHNICIANS"),"Vittini sees Technical Failure and Technicians transfers");
 Check(vittini.Contains("QA-FAILURE-ONLY")&&!vittini.Contains("QA-CALL-ONLY"),"Technical Failure excludes Call Center history");
 var cc=await Rows("CALL_CENTER");Check(cc.Contains("QA-CALL-ONLY")&&!cc.Contains("QA-FAILURE-ONLY"),"Call Center excludes Technical Failure history");
 Check(cc.Contains("QA-TRANSFERRED-TO-TECHNICIANS"),"Call Center keeps transferred tickets in its history");
 var tech=await Rows("TECHNOLOGY");Check(tech.Contains("QA-CALL-ONLY")&&tech.Contains("QA-FAILURE-ONLY"),"Technology includes both subareas");
 var gs=await Rows("GENERAL_SERVICES");Check(gs.Contains("SOL-ALM")&&gs.Contains("TAL-G")&&!gs.Contains("TAL-T")&&!gs.Contains("PRIVADO-RH"),"Department isolation");
 Check((await Rows("ALL")).Contains("PRIVADO-RH"),"Administrator global visibility");
 Check(await Rows("WAREHOUSE",200)=="[]","Pagination");
 Check(await db.DepartmentOperationalAudit("ALL",to,to.AddDays(1),0,CancellationToken.None)=="[]","Date filter");
 using var json=JsonDocument.Parse(warehouse);Check(json.RootElement.GetArrayLength()>=4,"Valid full JSON");
 Console.WriteLine($"{checks} manager/audit checks passed");
 var source=await File.ReadAllTextAsync("api/DatabaseSqlServerScoped.cs");
 var migrationStart=source.IndexOf("IF COL_LENGTH('dbo.admin_users','permissions_json')");
 var migrationEnd=source.IndexOf("IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='jordy.arias",migrationStart);
 var migration=source[migrationStart..migrationEnd];
 await Exec(c,migration);await Exec(c,migration);
 foreach(var code in new[]{"ADMINISTRATIVE","COMMUNICATION","COMMERCIALIZATION","TREASURY","ACCOUNTING","SUPERVISION","GENERAL_AUDIT","VAULT","CASHIER"})await Exec(c,$"INSERT admin_users(id,email,role,support_team) VALUES('{code}','{code}@example.invalid','DEPARTMENT','{code}');");
 Check(true,"User constraints accept all six additional departments after repeated migration");
 await Exec(c,"CREATE TABLE agencies(id uniqueidentifier,codigo nvarchar(80),terminal nvarchar(120),grupo nvarchar(120)); ALTER TABLE admin_users ADD display_name nvarchar(160),is_active bit NOT NULL DEFAULT(1); ALTER TABLE support_tickets ADD agency_id uniqueidentifier,description nvarchar(max),category nvarchar(80),priority varchar(20),status varchar(20),ticket_type varchar(20),created_by_name nvarchar(160),created_at datetime2,updated_at datetime2,assigned_technician_id nvarchar(80),resolved_by_user_id nvarchar(80),resolved_by_name nvarchar(160);");
 await Exec(c,"INSERT support_tickets(id,ticket_number,subject,assigned_department) VALUES(NEWID(),91,'TESORERIA-ONLY','TREASURY'),(NEWID(),92,'CONTABILIDAD-ONLY','ACCOUNTING');");
 var treasury=await db.DepartmentSupport("TREASURY",CancellationToken.None);
 Check(treasury.Contains("TESORERIA-ONLY")&&!treasury.Contains("CONTABILIDAD-ONLY")&&!treasury.Contains("QA-CALL-ONLY"),"Treasury support excludes all other departments");
 var accounting=await db.DepartmentSupport("ACCOUNTING",CancellationToken.None);
 Check(accounting.Contains("CONTABILIDAD-ONLY")&&!accounting.Contains("TESORERIA-ONLY"),"Accounting support excludes Treasury");
 var callSupport=await db.DepartmentSupport("CALL_CENTER",CancellationToken.None);
 Check(callSupport.Contains("QA-CALL-ONLY")&&callSupport.Contains("QA-TRANSFERRED-TO-TECHNICIANS")&&!callSupport.Contains("QA-FAILURE-ONLY"),"Call Center list preserves transferred tickets and isolation");
 var failureSupport=await db.DepartmentSupport("TECHNICAL_FAILURE",CancellationToken.None);
 Check(failureSupport.Contains("QA-FAILURE-ONLY")&&failureSupport.Contains("QA-TRANSFERRED-TO-TECHNICIANS")&&!failureSupport.Contains("QA-CALL-ONLY"),"Vittini list combines Technical Failure and Technicians");
 var globalMembers=await db.DepartmentSupportMembers("ALL",CancellationToken.None);
 Check(globalMembers.Contains("diorly.abreu@grupotejeda.local")&&globalMembers.Contains("gledys.tejeda@grupotejeda.local"),"Administrator global directory includes users from every department");
 Console.WriteLine($"TOTAL {checks} manager checks passed");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
partial class Database{public static async Task<SqlConnection> Open(CancellationToken ct){var c=new SqlConnection("Server=localhost;Database=Codex_Manager_Audit_QA;Integrated Security=True;TrustServerCertificate=True");await c.OpenAsync(ct);return c;}}
