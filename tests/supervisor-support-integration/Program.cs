using Microsoft.Data.SqlClient;

// Execute the actual production SELECTs on an isolated local database, not a copy of their predicates.
var source=File.ReadAllText("api/DatabaseSqlServerScoped.cs");
string Query(string method,Guid? ticketId=null){var start=source.IndexOf(method,StringComparison.Ordinal);if(start<0)throw new Exception(method);start=source.IndexOf("const string sql=\"\"\"",start,StringComparison.Ordinal)+"const string sql=\"\"\"".Length;var end=source.IndexOf("\"\"\";",start,StringComparison.Ordinal);var sql=source[start..end];return sql.Contains("@ticketId")?$"DECLARE @ticketId uniqueidentifier={(ticketId is Guid id?$"'{id}'":"NULL")};\n"+sql:sql;}
var name="Codex_Supervisor_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();var created=false;var checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
try{
 await using(var paths=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath')),CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))",master)){
  string dataPath,logPath;await using(var r=await paths.ExecuteReaderAsync()){await r.ReadAsync();dataPath=Path.Combine(r.GetString(0),name+".mdf").Replace("'","''");logPath=Path.Combine(r.GetString(1),name+"_log.ldf").Replace("'","''");}
  await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Refusing overwrite',1; CREATE DATABASE [{name}] ON PRIMARY (NAME=N'{name}',FILENAME=N'{dataPath}',SIZE=8MB,FILEGROWTH=8MB) LOG ON(NAME=N'{name}_log',FILENAME=N'{logPath}',SIZE=8MB,FILEGROWTH=8MB);");created=true;
 }
 await using var connection=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");await connection.OpenAsync();
 await Exec(connection,"""
 CREATE TABLE agencies(id uniqueidentifier,codigo nvarchar(40),terminal nvarchar(100),grupo nvarchar(100),directory_direccion nvarchar(100),directory_sector nvarchar(100),directory_municipio nvarchar(100),directory_provincia nvarchar(100),expected_latitude float,expected_longitude float);
 CREATE TABLE agency_profiles(agency_id uniqueidentifier,direccion nvarchar(100),sector nvarchar(100),municipio nvarchar(100),provincia nvarchar(100),latitude float,longitude float);
 CREATE TABLE admin_users(id nvarchar(80),email nvarchar(100),display_name nvarchar(100),support_team varchar(40));
 CREATE TABLE admin_user_groups(user_id nvarchar(80),group_name nvarchar(100));
 CREATE TABLE support_tickets(id uniqueidentifier,ticket_number bigint,agency_id uniqueidentifier,category varchar(50),assigned_department varchar(40),priority varchar(20),status varchar(20),subject nvarchar(100),description nvarchar(100),resolution nvarchar(100),created_by_user_id nvarchar(80),created_by_name nvarchar(100),created_by_username nvarchar(100),created_at datetime2,updated_at datetime2,closed_at datetime2,is_automatic bit,ticket_type varchar(20),assigned_technician_id nvarchar(80),assigned_team varchar(40),resolved_by_user_id nvarchar(80),resolved_by_name nvarchar(100));
 CREATE TABLE support_ticket_history(ticket_id uniqueidentifier,id uniqueidentifier,action varchar(40),from_department varchar(40),to_department varchar(40),from_team varchar(40),to_team varchar(40),actor_name nvarchar(100),comment nvarchar(100),created_at datetime2);
 CREATE TABLE support_ticket_shares(ticket_id uniqueidentifier,department_code varchar(40));
 CREATE TABLE support_ticket_evidence(ticket_id uniqueidentifier,id uniqueidentifier,file_name nvarchar(100),content_type varchar(50),captured_at datetime2,latitude float,longitude float,accuracy_meters float,location_captured_at datetime2,photo_data varbinary(max));
 INSERT agencies(id,codigo,terminal,grupo) VALUES('11111111-1111-1111-1111-111111111111','1','QA','OLD GROUP');
 INSERT admin_users VALUES('supervisor','supervisor.qa','Supervisor',''),('technician','technician.qa','Técnico','TECHNICIANS');
 ALTER TABLE admin_users ADD role varchar(40);
 """);
 var ids=new List<Guid>();var evidenceIds=new List<Guid>();
 foreach(var department in new[]{"TECHNOLOGY","GENERAL_SERVICES","HUMAN_RESOURCES"}){
  var id=Guid.NewGuid();var photo=Guid.NewGuid();ids.Add(id);evidenceIds.Add(photo);
  await using var seed=new SqlCommand("""
   INSERT support_tickets VALUES(@id,@number,'11111111-1111-1111-1111-111111111111','QA',@department,'MEDIUM','RESOLVED','Solicitud QA','Descripción','Resuelto por técnico','supervisor','Supervisor','OLD LOGIN',SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),0,'SUPPORT','technician','TECHNICIANS','technician','Técnico');
   INSERT support_ticket_history(ticket_id,id,action,actor_name,comment,created_at) VALUES(@id,NEWID(),'RESOLVED','Técnico','Reparado',SYSUTCDATETIME());
   INSERT support_ticket_evidence(ticket_id,id,file_name,content_type,captured_at,photo_data) VALUES(@id,@photo,'evidence.jpg','image/jpeg',SYSUTCDATETIME(),0x010203);
  """,connection);seed.Parameters.AddWithValue("@id",id);seed.Parameters.AddWithValue("@photo",photo);seed.Parameters.AddWithValue("@department",department);seed.Parameters.AddWithValue("@number",(long)ids.Count);await seed.ExecuteNonQueryAsync();
 }
 await Exec(connection,"INSERT support_tickets SELECT NEWID(),99,agency_id,category,assigned_department,priority,status,subject,description,resolution,'another','Another','another',created_at,updated_at,closed_at,is_automatic,ticket_type,assigned_technician_id,assigned_team,resolved_by_user_id,resolved_by_name FROM support_tickets WHERE ticket_number=1;");
 // The creator can follow all three reports; only the first two are assigned work.
 // An unrelated ticket in the same agency remains private.
 await Exec(connection,"INSERT admin_user_groups VALUES('supervisor','OLD GROUP'); UPDATE support_tickets SET assigned_technician_id='supervisor' WHERE ticket_number IN (1,2);");
 await using(var cmd=new SqlCommand(Query("public async Task<List<SupportTicketRow>> Tickets(string userId,string role,bool canAssignTickets,string? supportTeam,string? userEmail"),connection)){
  foreach(var param in new[]{"@administrator","@administrativeRequester","@areaManager","@canAssign"})cmd.Parameters.AddWithValue(param,0);
  cmd.Parameters.AddWithValue("@groupSupervisor",1);cmd.Parameters.AddWithValue("@userId","supervisor");cmd.Parameters.AddWithValue("@userEmail","supervisor.qa");cmd.Parameters.AddWithValue("@supportDepartment","");cmd.Parameters.AddWithValue("@supportTeam","");
  await using var reader=await cmd.ExecuteReaderAsync();var seen=new List<Guid>();
  while(await reader.ReadAsync()){seen.Add(reader.GetGuid(0));Check(reader.GetBoolean(30),"Creator tracking is identified by user ID");Check(reader.GetBoolean(29)==ids.Take(2).Contains(reader.GetGuid(0)),"Creator access does not pretend assignment");}
  Check(seen.Count==3&&ids.All(seen.Contains),"Creator sees own reports and assignments, never unrelated agency tickets");
  await reader.NextResultAsync();var histories=0;while(await reader.ReadAsync())histories++;Check(histories==3,"Assigned and created resolution history returned");
  await reader.NextResultAsync();await reader.NextResultAsync();var photos=0;while(await reader.ReadAsync())photos++;Check(photos==3,"Evidence references returned for own reports and assigned cases");
 }
 foreach(var actor in new[]{"supervisor","another"}){
  await using var cmd=new SqlCommand(Query("public async Task<List<SupportTicketRow>> Tickets(string userId,string role,bool",ids[0]),connection);
  foreach(var param in new[]{"@administrator","@administrativeRequester","@areaManager","@canAssign"})cmd.Parameters.AddWithValue(param,0);
  cmd.Parameters.AddWithValue("@groupSupervisor",1);cmd.Parameters.AddWithValue("@userId",actor);cmd.Parameters.AddWithValue("@userEmail",actor+".qa");cmd.Parameters.AddWithValue("@supportDepartment","");cmd.Parameters.AddWithValue("@supportTeam","");
  await using var reader=await cmd.ExecuteReaderAsync();var count=0;while(await reader.ReadAsync()){count++;Check(reader.GetGuid(0)==ids[0],"Single-ticket response never includes other tickets");}
  Check(count==(actor=="supervisor"?1:0),"Single-ticket response preserves creator/assignment permissions");
  await reader.NextResultAsync();var histories=0;while(await reader.ReadAsync())histories++;
  Check(histories==count,"Single-ticket response only loads its own history");
  await reader.NextResultAsync();await reader.NextResultAsync();var photos=0;while(await reader.ReadAsync())photos++;
  Check(photos==count,"Single-ticket response only loads authorized evidence references");
 }
 foreach(var owner in new[]{"supervisor","another"})foreach(var photo in evidenceIds){
  await using var cmd=new SqlCommand(Query("public async Task<(byte[] Data,string Mime,string FileName)?> TicketEvidencePhoto"),connection);
  cmd.Parameters.AddWithValue("@evidence",photo);cmd.Parameters.AddWithValue("@administrator",0);cmd.Parameters.AddWithValue("@groupSupervisor",1);cmd.Parameters.AddWithValue("@department","");cmd.Parameters.AddWithValue("@supportTeam","");cmd.Parameters.AddWithValue("@canAssign",0);cmd.Parameters.AddWithValue("@userId",owner);
  var data=await cmd.ExecuteScalarAsync();Check(owner=="supervisor"?data is byte[] bytes&&bytes.Length==3:data is null,"Created/assigned evidence access: "+owner);
 }
 foreach(var id in ids){
  await using var cmd=new SqlCommand(Query("static async Task<bool> CanWorkTicket("),connection);
  cmd.Parameters.AddWithValue("@ticket",id);cmd.Parameters.AddWithValue("@userId","supervisor");
  cmd.Parameters.AddWithValue("@administrator",0);cmd.Parameters.AddWithValue("@groupSupervisor",1);
  cmd.Parameters.AddWithValue("@supportDepartment","");cmd.Parameters.AddWithValue("@supportTeam","");cmd.Parameters.AddWithValue("@canAssign",0);
  Check((Convert.ToInt32(await cmd.ExecuteScalarAsync())>0)==(ids.IndexOf(id)<2),"Resolution authorization is assigned-only");
 }
 // Exercise the production list and action authorization using legacy identifiers.
 foreach(var actor in new[]{("supervisor","supervisor.qa@example.test",true),("technician","technician.qa@example.test",false)}){
  await using(var update=new SqlCommand("UPDATE admin_users SET email=@email WHERE id=@id;",connection)){update.Parameters.AddWithValue("@email",actor.Item2);update.Parameters.AddWithValue("@id",actor.Item1);await update.ExecuteNonQueryAsync();}
  foreach(var assignee in new[]{actor.Item1,actor.Item2,"  "+actor.Item2.Split('@')[0].ToUpperInvariant()+"  ","unrelated.user"}){
   await using(var update=new SqlCommand("UPDATE support_tickets SET assigned_technician_id=@assignee WHERE ticket_number=1;",connection)){update.Parameters.AddWithValue("@assignee",assignee);await update.ExecuteNonQueryAsync();}
   var own=assignee!="unrelated.user";
   await using(var cmd=new SqlCommand(Query("public async Task<List<SupportTicketRow>> Tickets(string userId,string role,bool canAssignTickets,string? supportTeam,string? userEmail"),connection)){
    foreach(var param in new[]{"@administrator","@administrativeRequester","@areaManager","@canAssign"})cmd.Parameters.AddWithValue(param,0);
    cmd.Parameters.AddWithValue("@groupSupervisor",actor.Item3?1:0);cmd.Parameters.AddWithValue("@userId",actor.Item1);cmd.Parameters.AddWithValue("@userEmail",actor.Item2);cmd.Parameters.AddWithValue("@supportDepartment",actor.Item3?"":"TECHNOLOGY");cmd.Parameters.AddWithValue("@supportTeam",actor.Item3?"":"TECHNICIANS");
    await using var reader=await cmd.ExecuteReaderAsync();var found=false;
    while(await reader.ReadAsync())if(reader.GetGuid(0)==ids[0]){found=true;Check(reader.GetBoolean(29)==own,"Legacy assignment flag matches responsibility: "+assignee);}
    Check(found==(own||actor.Item3),"Creator may follow a report without becoming its assignee: "+actor.Item1+" / "+assignee);
   }
   await using(var cmd=new SqlCommand(Query("static async Task<bool> CanWorkTicket("),connection)){
    cmd.Parameters.AddWithValue("@ticket",ids[0]);cmd.Parameters.AddWithValue("@userId",actor.Item1);cmd.Parameters.AddWithValue("@administrator",0);cmd.Parameters.AddWithValue("@groupSupervisor",actor.Item3?1:0);cmd.Parameters.AddWithValue("@supportDepartment",actor.Item3?"":"TECHNOLOGY");cmd.Parameters.AddWithValue("@supportTeam",actor.Item3?"":"TECHNICIANS");cmd.Parameters.AddWithValue("@canAssign",0);
    Check((Convert.ToInt32(await cmd.ExecuteScalarAsync())>0)==own,"Action authorization matches list: "+assignee);
   }
  }
 }
 await Exec(connection,"UPDATE support_tickets SET assigned_technician_id='supervisor' WHERE ticket_number=1;");
 await Exec(connection,"ALTER TABLE admin_users ADD permissions_json nvarchar(max),is_active bit NOT NULL DEFAULT 1;");

 // Support receiving the report can download its mandatory image even after routing to technicians.
 await Exec(connection,"UPDATE support_tickets SET assigned_department='TECHNOLOGY',assigned_team='TECHNICIANS',assigned_technician_id='technician' WHERE ticket_number=1;");
 foreach(var scope in new[]{("TECHNOLOGY","TECHNICAL_FAILURE",1,"dispatcher",true),("TECHNOLOGY","TECHNICIANS",0,"technician",true),("GENERAL_SERVICES","",1,"other-department",false),("TECHNOLOGY","TECHNICIANS",0,"unrelated-technician",false)}){
  await using var cmd=new SqlCommand(Query("public async Task<(byte[] Data,string Mime,string FileName)?> TicketEvidencePhoto"),connection);
  cmd.Parameters.AddWithValue("@evidence",evidenceIds[0]);cmd.Parameters.AddWithValue("@administrator",0);cmd.Parameters.AddWithValue("@groupSupervisor",0);cmd.Parameters.AddWithValue("@department",scope.Item1);cmd.Parameters.AddWithValue("@supportTeam",scope.Item2);cmd.Parameters.AddWithValue("@canAssign",scope.Item3);cmd.Parameters.AddWithValue("@userId",scope.Item4);
  Check((await cmd.ExecuteScalarAsync() is byte[])==scope.Item5,"Report image authorization: "+scope.Item4);
 }
 // Notifications are generated for the creator even after a group change, without duplicates.
 await Exec(connection,"""
 CREATE TABLE support_ticket_notifications(id uniqueidentifier NOT NULL DEFAULT NEWID(),ticket_id uniqueidentifier,event_type varchar(40),recipient_user_id nvarchar(80),target_department varchar(40),message nvarchar(2000),actor_user_id nvarchar(80),actor_name nvarchar(100),created_at datetime2);
 CREATE TABLE support_notification_receipts(user_id nvarchar(80),notification_key nvarchar(120),read_at datetime2,dismissed_at datetime2);
 UPDATE admin_users SET role='GROUP_ADMIN' WHERE id='supervisor';
 DELETE FROM admin_user_groups WHERE user_id='supervisor';
 """);
 foreach(var kind in new[]{"CREATED","ASSIGNED","RESOLVED"}){
  await using var cmd=new SqlCommand(Query("static async Task AddTicketNotifications("),connection);
  cmd.Parameters.AddWithValue("@ticket",ids[0]);cmd.Parameters.AddWithValue("@event",kind);cmd.Parameters.AddWithValue("@recipient",kind=="ASSIGNED"?"technician":"");cmd.Parameters.AddWithValue("@targetDepartment","TECHNOLOGY");cmd.Parameters.AddWithValue("@agencyGroup","OLD GROUP");cmd.Parameters.AddWithValue("@userId","technician");cmd.Parameters.AddWithValue("@actor","Técnico");cmd.Parameters.AddWithValue("@message","Caso actualizado");cmd.Parameters.AddWithValue("@notifySupervisors",1);await cmd.ExecuteNonQueryAsync();
 }
 await using(var cmd=new SqlCommand(Query("public async Task<List<TicketNotificationRow>> ResolvedTicketNotifications(string userId,string role,bool"),connection)){
  cmd.Parameters.AddWithValue("@administrator",0);cmd.Parameters.AddWithValue("@groupSupervisor",1);cmd.Parameters.AddWithValue("@department","");cmd.Parameters.AddWithValue("@canAssign",0);cmd.Parameters.AddWithValue("@supportTeam","");cmd.Parameters.AddWithValue("@userId","supervisor");
  await using var reader=await cmd.ExecuteReaderAsync();var count=0;
  while(await reader.ReadAsync()){count++;Check(reader.GetGuid(0)==ids[0]&&reader.GetInt64(1)==1,"Notification links to exact report ID and number");}
  Check(count==3,"Creator receives creation, assignment and resolution once each without agency membership");
 }
 // A new report with no assignee must still be returned to its creator after commit.
 await Exec(connection,"UPDATE support_tickets SET status='OPEN',assigned_technician_id=NULL WHERE ticket_number=3;");
 foreach(var status in new[]{"OPEN","IN_PROGRESS","RESOLVED"}){
  await Exec(connection,$"UPDATE support_tickets SET status='{status}' WHERE ticket_number=3;");
  await using var cmd=new SqlCommand(Query("public async Task<List<SupportTicketRow>> Tickets(string userId,string role,bool"),connection);
  foreach(var param in new[]{"@administrator","@administrativeRequester","@areaManager","@canAssign"})cmd.Parameters.AddWithValue(param,0);
  cmd.Parameters.AddWithValue("@groupSupervisor",1);cmd.Parameters.AddWithValue("@userId","supervisor");cmd.Parameters.AddWithValue("@userEmail","supervisor.qa@example.test");cmd.Parameters.AddWithValue("@supportDepartment","");cmd.Parameters.AddWithValue("@supportTeam","");
  await using var reader=await cmd.ExecuteReaderAsync();var found=false;while(await reader.ReadAsync())if(reader.GetGuid(0)==ids[2]){found=true;Check(reader.GetString(8)==status&&!reader.GetBoolean(29)&&reader.GetBoolean(30),"Creator follows report state without work permissions: "+status);}
  Check(found,"Created ticket is returned instead of a false forbidden response: "+status);
 }
 await Exec(connection,"""
 INSERT admin_users(id,email,display_name,support_team,role,permissions_json) VALUES('treasury','treasury.qa','Tesorería','TREASURY','DEPARTMENT','{"canAssignTickets":true,"canResolveTickets":true}'),('treasury-tech','treasury.tech','Técnico Tesorería','TREASURY','DEPARTMENT','{"canAssignTickets":false,"canResolveTickets":true}'),('accounting-tech','accounting.tech','Técnico Contabilidad','ACCOUNTING','DEPARTMENT','{"canAssignTickets":false,"canResolveTickets":true}');
 UPDATE support_tickets SET assigned_department='TREASURY',created_by_user_id='another' WHERE ticket_number=1;
 UPDATE support_tickets SET assigned_department='TECHNOLOGY',created_by_user_id='treasury' WHERE ticket_number=2;
 UPDATE support_tickets SET assigned_department='ACCOUNTING',created_by_user_id='another' WHERE ticket_number=3;
 """);
 await using(var cmd=new SqlCommand(Query("public async Task<List<SupportTicketRow>> Tickets(string userId,string role,bool canAssignTickets,string? supportTeam,string? userEmail"),connection)){
  foreach(var param in new[]{"@administrator","@groupSupervisor","@areaManager"})cmd.Parameters.AddWithValue(param,0);
  cmd.Parameters.AddWithValue("@administrativeRequester",1);cmd.Parameters.AddWithValue("@canAssign",1);cmd.Parameters.AddWithValue("@userId","treasury");cmd.Parameters.AddWithValue("@userEmail","treasury.qa");cmd.Parameters.AddWithValue("@supportDepartment","TREASURY");cmd.Parameters.AddWithValue("@supportTeam","");
  await using var reader=await cmd.ExecuteReaderAsync();var seen=new List<Guid>();while(await reader.ReadAsync())seen.Add(reader.GetGuid(0));
  Check(seen.Count==2&&ids.Take(2).All(seen.Contains),"Treasury sees its incoming tickets and own outbound requests, not Accounting or unrelated Technology");
 }
 foreach(var id in ids){
  await using var cmd=new SqlCommand(Query("static async Task<bool> CanWorkTicket("),connection);
  cmd.Parameters.AddWithValue("@ticket",id);cmd.Parameters.AddWithValue("@userId","treasury");cmd.Parameters.AddWithValue("@administrator",0);cmd.Parameters.AddWithValue("@groupSupervisor",0);cmd.Parameters.AddWithValue("@supportDepartment","TREASURY");cmd.Parameters.AddWithValue("@supportTeam","");cmd.Parameters.AddWithValue("@canAssign",1);
  Check((Convert.ToInt32(await cmd.ExecuteScalarAsync())>0)==(ids.IndexOf(id)==0),"Treasury cannot resolve outbound or another department's tickets");
 }
 foreach(var assignee in new[]{"treasury-tech","accounting-tech"}){
  await using var cmd=new SqlCommand(Query("static async Task<bool> ValidTicketAssignee("),connection);
  cmd.Parameters.AddWithValue("@technician",assignee);cmd.Parameters.AddWithValue("@department","TREASURY");cmd.Parameters.AddWithValue("@departmentRole","DEPARTMENT");cmd.Parameters.AddWithValue("@team","");cmd.Parameters.AddWithValue("@ticketType","SUPPORT");cmd.Parameters.AddWithValue("@agencyGroup","OLD GROUP");
  Check((Convert.ToInt32(await cmd.ExecuteScalarAsync())>0)==(assignee=="treasury-tech"),"Assignment respects the employee's department: "+assignee);
 }
 await Exec(connection,"CREATE TABLE support_conversations(id uniqueidentifier,deleted_at datetime2,category varchar(100),assigned_department varchar(40),supervisor_user_id nvarchar(80));");
 foreach(var item in new[]{("TREASURY","another","HELP",true,true),("TECHNOLOGY","treasury","HELP",true,false),("ACCOUNTING","another","HELP",false,false),("ACCOUNTING","treasury","DIRECT:another",true,true),("TREASURY","another","DIRECT:third",false,false)}){
  var conversation=Guid.NewGuid();
  await using(var seed=new SqlCommand("INSERT support_conversations VALUES(@id,NULL,@category,@department,@owner);",connection)){seed.Parameters.AddWithValue("@id",conversation);seed.Parameters.AddWithValue("@category",item.Item3);seed.Parameters.AddWithValue("@department",item.Item1);seed.Parameters.AddWithValue("@owner",item.Item2);await seed.ExecuteNonQueryAsync();}
  string SingleQuery(string method){var start=source.IndexOf(method,StringComparison.Ordinal);start=source.IndexOf("const string sql=\"",start,StringComparison.Ordinal)+"const string sql=\"".Length;return source[start..source.IndexOf("\";",start,StringComparison.Ordinal)];}
  await using var access=new SqlCommand(SingleQuery("static async Task<bool> CanAccessSupportConversation("),connection);
  access.Parameters.AddWithValue("@id",conversation);access.Parameters.AddWithValue("@admin",0);access.Parameters.AddWithValue("@all",0);access.Parameters.AddWithValue("@own",1);access.Parameters.AddWithValue("@userId","treasury");access.Parameters.AddWithValue("@department","TREASURY");
  var readable=Convert.ToInt32(await access.ExecuteScalarAsync())>0;Check(readable==item.Item4,"Chat scope: "+item);
  await using var manage=new SqlCommand(SingleQuery("static async Task<bool> CanManageDepartmentConversation("),connection);manage.Parameters.AddWithValue("@id",conversation);manage.Parameters.AddWithValue("@department","TREASURY");
  Check((readable&&Convert.ToInt32(await manage.ExecuteScalarAsync())>0)==item.Item5,"Chat management cannot take over outgoing or unrelated conversations");
 }
 await Exec(connection,"INSERT admin_users(id,email,display_name,support_team,role,permissions_json) VALUES('department-without-area','incomplete.qa','Cuenta sin departamento',NULL,'DEPARTMENT','{}');");
 await using(var directory=new SqlCommand(Query("public async Task<List<SupportTechnicianRow>> SupportTechnicians("),connection)){
  await using var reader=await directory.ExecuteReaderAsync();var foundTreasury=false;
  while(await reader.ReadAsync()){
   var department=reader.GetString(2); // Same typed read used by the production endpoint.
   if(reader.GetString(0)=="treasury")foundTreasury=department=="TREASURY";
   if(reader.GetString(0)=="department-without-area")Check(department=="","Incomplete accounts never receive a global/default department");
  }
  Check(foundTreasury,"Directory still includes valid Treasury staff");
 }
 Console.WriteLine($"SUCCESS {checks} supervisor and department support SQL checks");
}finally{if(created){SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");Console.WriteLine("Only disposable local QA database removed.");}}
static async Task Exec(SqlConnection connection,string sql){await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync();}
