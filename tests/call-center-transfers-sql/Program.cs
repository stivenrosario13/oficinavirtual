
using System.Reflection;
using System.Collections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
var name="Codex_CallCenter_QA_"+Guid.NewGuid().ToString("N");
const string master="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True";
await using var root=new SqlConnection(master);await root.OpenAsync();
async Task Exec(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
await Exec(root,$"CREATE DATABASE [{name}]");int checks=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
try{
 var connectionString=new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString;
 await using var c=new SqlConnection(connectionString);await c.OpenAsync();
 await Exec(c,"""
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

 ALTER TABLE admin_users ADD is_active bit NOT NULL DEFAULT 1,permissions_json nvarchar(max);
 UPDATE admin_users SET role=CASE id WHEN 'supervisor' THEN 'GROUP_ADMIN' ELSE 'TECHNOLOGY' END;
 INSERT admin_users(id,email,display_name,support_team,role) VALUES('cc','cc.qa','Call Center','CALL_CENTER','TECHNOLOGY'),('failure','failure.qa','Avería','TECHNICAL_FAILURE','TECHNOLOGY'),('inactive','inactive.qa','Inactivo',NULL,'GROUP_ADMIN');
 UPDATE admin_users SET is_active=0 WHERE id='inactive';
 ALTER TABLE agency_profiles ADD id uniqueidentifier DEFAULT NEWID(),submitted_at datetime2 DEFAULT SYSUTCDATETIME();
 UPDATE agencies SET expected_latitude=18.5,expected_longitude=-69.9;
 CREATE TABLE support_departments(code varchar(40),is_active bit);INSERT support_departments VALUES('TECHNOLOGY',1);
 ALTER TABLE support_ticket_history ADD actor_user_id nvarchar(80);
 ALTER TABLE support_ticket_history ADD DEFAULT NEWID() FOR id;
 ALTER TABLE support_tickets ADD resolved_at datetime2;
 ALTER TABLE support_ticket_evidence ADD uploaded_by_user_id nvarchar(80),uploaded_by_name nvarchar(100),location_source varchar(40);
 ALTER TABLE support_ticket_evidence ADD DEFAULT NEWID() FOR id;
 ALTER TABLE support_ticket_evidence ADD DEFAULT SYSUTCDATETIME() FOR captured_at;
 CREATE TABLE support_ticket_notifications(ticket_id uniqueidentifier,event_type varchar(40),recipient_user_id nvarchar(80),target_department varchar(40),message nvarchar(1000),actor_user_id nvarchar(80),actor_name nvarchar(100),created_at datetime2 DEFAULT SYSUTCDATETIME());
 CREATE TABLE institutional_files(id bigint,metadata nvarchar(max));
 """);
 var assembly=Assembly.LoadFrom(Path.GetFullPath("api/bin/Debug/net8.0/"+System.Xml.Linq.XDocument.Load("api/RegistroAgencias.Api.csproj").Descendants("AssemblyName").Single().Value+".dll"));
 var type=assembly.GetType("Database",true)!;
 var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=connectionString+";User Id=qa-placeholder"}).Build();
 var db=Activator.CreateInstance(type,config)!;
 var connections=(List<(string Value,string Mode)>)type.GetField("connections",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(db)!;connections.Clear();connections.Add((connectionString,"local-qa"));
 type.GetField("ticketWorkflowSchemaReady",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
 async Task<object> Call(string method,params object?[] args){var m=type.GetMethods().Single(m=>m.Name==method&&m.GetParameters().Length==args.Length);var task=(Task)m.Invoke(db,args)!;await task;return task.GetType().GetProperty("Result")!.GetValue(task)!;}
 object? Field(object row,string field)=>row.GetType().GetProperty(field)!.GetValue(row);
 async Task<Guid> Seed(int number,string team="CALL_CENTER",string kind="INTERNAL"){
  var id=Guid.NewGuid();await using var cmd=new SqlCommand("INSERT support_tickets(id,ticket_number,agency_id,category,assigned_department,priority,status,subject,description,created_by_user_id,created_by_name,created_by_username,created_at,updated_at,is_automatic,ticket_type,assigned_team) VALUES(@id,@number,'11111111-1111-1111-1111-111111111111','QA','TECHNOLOGY','MEDIUM','OPEN','QA','Detalle','cc','Call Center','cc.qa',SYSUTCDATETIME(),SYSUTCDATETIME(),0,@kind,@team)",c);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@number",number);cmd.Parameters.AddWithValue("@kind",kind);cmd.Parameters.AddWithValue("@team",team);await cmd.ExecuteNonQueryAsync();return id;
 }
 async Task<object> Route(Guid id,string team,string? assignee)=>await Call("RouteTicket",id,Activator.CreateInstance(assembly.GetType("TicketRoute")!,["TECHNOLOGY",team,assignee,"QA transfer"]),"cc","Technology","Call Center","CALL_CENTER",CancellationToken.None);
 async Task<List<object>> List(string user="cc",string role="Technology",string? team="CALL_CENTER")=>((IEnumerable)await Call("Tickets",user,role,true,team,user+".qa",CancellationToken.None,false,null)).Cast<object>().ToList();
 var supervisorTicket=await Seed(1);var failureTicket=await Seed(2);var unrelated=await Seed(3,"TECHNICAL_FAILURE");var support=await Seed(4,"CALL_CENTER","SUPPORT");
 Check(!(bool)Field(await Route(supervisorTicket,"TECHNICIANS","technician"),"Ok")!,"Call Center cannot use supervisor route to assign a field technician");
 Check(!(bool)Field(await Route(supervisorTicket,"TECHNICIANS","inactive"),"Ok")!,"Inactive supervisor rejected");
 Check(!(bool)Field(await Route(unrelated,"TECHNICIANS","supervisor"),"Ok")!,"Cannot transfer another team's ticket by guessing its ID");
 Check(!(bool)Field(await Route(support,"TECHNICIANS","supervisor"),"Ok")!,"Only internal Call Center cases can be escalated");
 Check((bool)Field(await Route(supervisorTicket,"TECHNICIANS","supervisor"),"Ok")!,"Call Center assigns active supervisor");
 Check((bool)Field(await Route(failureTicket,"TECHNICAL_FAILURE",null),"Ok")!,"Existing escalation to Avería Técnica preserved");
 Check(!(bool)Field(await Route(supervisorTicket,"TECHNICAL_FAILURE",null),"Ok")!,"Call Center cannot reassign a transferred case");
 var own=await List();Check(own.Any(x=>(Guid)Field(x,"Id")! == supervisorTicket)&&own.Any(x=>(Guid)Field(x,"Id")! == failureTicket)&&!own.Any(x=>(Guid)Field(x,"Id")! == unrelated),"Transfer tracking includes both destinations and excludes unrelated cases");
 Check((await List("supervisor","GroupAdministrator",null)).Any(x=>(Guid)Field(x,"Id")! == supervisorTicket),"Supervisor receives the same ticket");
 var evidenceType=assembly.GetType("TicketEvidenceUpload")!;
 object Evidence(){var list=(IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(evidenceType))!;list.Add(Activator.CreateInstance(evidenceType,["qa.jpg","image/jpeg",new byte[]{1,2,3},18.5,-69.9,5.0,DateTime.UtcNow])!);return list;}
 Check(!(bool)Field(await Call("ResolveTicket",supervisorTicket,"RESOLVED","Forbidden QA",Evidence(),"cc","Technology","Call Center","CALL_CENTER",true,true,CancellationToken.None),"Ok")!,"Tracking access does not grant resolution permission after transfer");
 foreach(var item in new[]{(supervisorTicket,"supervisor","GroupAdministrator",(string?)null),(failureTicket,"failure","Technology",(string?)"TECHNICAL_FAILURE")}){
  var result=await Call("ResolveTicket",item.Item1,"RESOLVED","Reparación QA",Evidence(),item.Item2,item.Item3,item.Item2,item.Item4,true,true,CancellationToken.None);
  Check((bool)Field(result,"Ok")!,"Destination can resolve original ticket: "+item.Item2);
  var tracked=(await List()).Single(x=>(Guid)Field(x,"Id")! == item.Item1);
  Check((string)Field(tracked,"Status")! == "RESOLVED"&&(string)Field(tracked,"ResolvedByName")! == item.Item2,"Call Center sees actual resolution and resolver: "+item.Item2);
  var photo=((IEnumerable)Field(tracked,"Evidence")!).Cast<object>().Single();
  Check(await Call("TicketEvidencePhoto",(Guid)Field(photo,"Id")!,"cc","Technology",true,"CALL_CENTER",CancellationToken.None) is not null,"Call Center can read resolution evidence");
 }
 Console.WriteLine($"{checks} checks passed");
}finally{SqlConnection.ClearAllPools();await Exec(root,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");}
