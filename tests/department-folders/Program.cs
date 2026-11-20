using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
var count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
var name="Codex_Folders_QA_"+Guid.NewGuid().ToString("N");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True");await master.OpenAsync();
await using(var logPathCommand=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))",master)){
 var logPath=Path.Combine((string)(await logPathCommand.ExecuteScalarAsync())!,name+"_log.ldf").Replace("'","''");
 await using var dataPathCommand=new SqlCommand("SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))",master);
 var dataPath=Path.Combine((string)(await dataPathCommand.ExecuteScalarAsync())!,name+".mdf").Replace("'","''");
 await Exec(master,$"CREATE DATABASE [{name}] ON PRIMARY (NAME=N'{name}',FILENAME=N'{dataPath}',SIZE=8MB,FILEGROWTH=8MB) LOG ON (NAME=N'{name}_log',FILENAME=N'{logPath}',SIZE=8MB,FILEGROWTH=8MB)");
}
try{
 var cs=$"Server=localhost;Database={name};Integrated Security=True;User Id=qa;TrustServerCertificate=True";
 await using var c=new SqlConnection(cs);await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE admin_users(id nvarchar(80),display_name nvarchar(160),email nvarchar(160),role varchar(40),support_team varchar(40),is_active bit);
 INSERT admin_users VALUES('owner','Owner','owner','ADMINISTRATIVE',NULL,1),('one','One','one','ADMINISTRATIVE',NULL,1),('two','Two','two','ADMINISTRATIVE',NULL,1),('outsider','Outsider','outsider','ADMINISTRATIVE',NULL,1),('foreign','Foreign','foreign','DEPARTMENT','ACCOUNTING',1),('disabled','Disabled','disabled','ADMINISTRATIVE',NULL,0);
 CREATE TABLE institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80),department nvarchar(80),area varchar(20),channel varchar(20),metadata nvarchar(max),file_data varbinary(max));
 """);
 var asm=Assembly.Load("RegistroAgencias.SqlServer.V500");var type=asm.GetType("Database")!;var flags=BindingFlags.Static|BindingFlags.NonPublic;
 type.GetField("institutionalSchemaReady",flags)!.SetValue(null,true);
 var db=Activator.CreateInstance(type,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:DefaultConnection",cs}}).Build())!;
 var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
 object Obj(string name,object value)=>JsonSerializer.Deserialize(JsonSerializer.Serialize(value),asm.GetType(name)!,options)!;
 object Actor(string id,string dept="Administración")=>Obj("DocumentActor",new{id,role="Administrative",team="",name=id,login=id,assignedDepartment=dept});
 async Task<object?> Call(string name,params object?[] args){var t=(Task)type.GetMethod(name)!.Invoke(db,args)!;await t;return t.GetType().GetProperty("Result")?.GetValue(t);}
 JsonElement J(object? value)=>JsonSerializer.SerializeToElement(value,options);
 var ct=CancellationToken.None;var owner=Actor("owner");var one=Actor("one");var two=Actor("two");var outsider=Actor("outsider");var foreign=Actor("foreign","Contabilidad");
 var users=J(await Call("AdministrativeFolderUsers",owner,ct));Check(users.GetArrayLength()==4&&users.ToString().Contains("foreign")&&!users.ToString().Contains("disabled"),"Directory includes every active profile and department");
 foreach(var bad in new[]{"disabled","missing"})Check(await Call("CreateAdministrativeFolder","Bad",null,owner,new List<string>{bad},ct)==null,"Reject recipient "+bad);
 Check(await Call("CreateAdministrativeFolder","Too many",null,owner,Enumerable.Range(1,21).Select(index=>"user-"+index).ToList(),ct)==null,"Reject more than twenty recipients");
 var root=J(await Call("CreateAdministrativeFolder","Shared",null,owner,new List<string>{"ONE","TWO","FOREIGN"},ct));var id=root.GetProperty("id").GetGuid();
 Check(root.GetProperty("canManage").GetBoolean(),"Creator manages recipients");
 var sub=J(await Call("CreateAdministrativeFolder","Child",id,one,null,ct));var subid=sub.GetProperty("id").GetGuid();
 await Exec(c,"UPDATE admin_users SET is_active=0 WHERE id='two'");
 var nested=J(await Call("CreateAdministrativeFolder","Nested",subid,one,null,ct));
 Check(nested.ValueKind==JsonValueKind.Object,"Active collaborator creates nested folder despite inactive inherited recipient");
 var nestedId=nested.GetProperty("id").GetGuid();
 Check((bool)(await Call("AdministrativeFolderWritable",nestedId,owner,ct))!,"Root owner can write to collaborator's nested folder");
 Check(!(bool)(await Call("AdministrativeFolderWritable",nestedId,outsider,ct))!,"Outsider cannot write to nested folder");
 Check((bool)(await Call("DeleteAdministrativeFolder",nestedId,owner,ct))!,"Owner manages empty nested folder created by collaborator");
 await Exec(c,"UPDATE admin_users SET is_active=1 WHERE id='two'");
 foreach(var a in new[]{one,two,foreign}){var folders=J(await Call("AdministrativeFolders",a,ct));Check(folders.GetArrayLength()==2&&folders[0].GetProperty("canWrite").GetBoolean()&&!folders[0].GetProperty("canManage").GetBoolean(),"Selected colleague in any department writes but cannot manage recipients");}
 Check(J(await Call("AdministrativeFolders",outsider,ct)).GetArrayLength()==0,"Unselected user cannot see shared folder");
 Check(await Call("SetAdministrativeFolderRecipients",id,new List<string>{"outsider"},one,ct)!=null,"Collaborator cannot change recipients");
 Check(await Call("MoveAdministrativeFolder",subid,null,one,ct)!=null,"Collaborator cannot detach subtree and take over access");
 Check(await Call("MoveAdministrativeFolder",id,subid,owner,ct)!=null,"Cycle rejected");
 object File(string uploader,string department="Administración")=>Obj("InstitutionalFile",new{area="files",channel="requests",title="QA",ownerUserName=uploader,ownerLogin=uploader,ownerDisplayName=uploader,department,folderId=subid,contentType="text/plain",originalFileName="qa.txt"});
 var fileId=(long)(await Call("InstitutionalUpload",File("one"),new byte[]{1,2,3},ct))!;
 foreach(var a in new[]{owner,one,two,foreign}){Check(await Call("InstitutionalBytes",fileId,a,ct)!=null,"Creator and cross-role recipients can open/download collaborator upload");Check(J(await Call("InstitutionalDetail",fileId,a,ct)).GetProperty("canEdit").GetBoolean(),"Collaborators may edit");}
 var legacy=File("ONE","Tecnología");var legacyJson=JsonSerializer.Serialize(legacy,legacy.GetType(),options);long legacyId;
 await using(var legacyInsert=new SqlCommand("INSERT institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES('ONE',N'Tecnología','files','requests',@json,0x0102)",c)){legacyInsert.Parameters.AddWithValue("@json",legacyJson);legacyId=(long)(await legacyInsert.ExecuteScalarAsync())!;}
 foreach(var a in new[]{owner,one,two,foreign})Check(await Call("InstitutionalBytes",legacyId,a,ct)!=null,"Authorized collaborator opens legacy file despite identity case or stale department metadata");
 Check(await Call("InstitutionalBytes",fileId,outsider,ct)==null,"Direct download denied to unselected user");Check(await Call("InstitutionalDetail",fileId,outsider,ct)==null,"Direct detail denied to unselected user");
 var action=Obj("InstitutionalAction",new{title="Edited",description="Colleague edit"});
 foreach(var op in new[]{"edit","trash","restore"})Check(await Call("InstitutionalChange",fileId,two,op,action,ct)==null,"Collaborator action "+op);
 Check(await Call("SetAdministrativeFolderRecipients",id,new List<string>{"two"},owner,ct)==null,"Creator revokes first recipient");
 Check(await Call("InstitutionalBytes",fileId,one,ct)==null,"Revoked uploader loses download access to own file");
 Check(await Call("InstitutionalChange",fileId,one,"edit",action,ct)!=null,"Revoked uploader loses edit access");
 Check(await Call("InstitutionalBytes",fileId,two,ct)!=null,"Other selected recipient retains access");
 Check((bool)(await Call("RenameAdministrativeFolder",subid,"Renamed by collaborator",two,ct))!,"Selected user may rename folder");
 Check(await Call("MoveAdministrativeFile",fileId,id,"Administración",two,ct)==null,"Selected user may move a shared file");
 Check(await Call("MoveAdministrativeFile",legacyId,id,"Administración",two,ct)==null,"Selected user may move a legacy shared file");
 Check((bool)(await Call("DeleteAdministrativeFolder",subid,two,ct))!,"Selected user may delete empty subfolder");
 Check(!(bool)(await Call("RenameAdministrativeFolder",id,"Denied",one,ct))!,"Revoked user cannot rename folder");
 var asset=Obj("AdministrativeAssetInput",new{kind="other",label="QA",identifier="QA-ACCOUNTING",status="available"});
 var saved=J(await Call("SaveAdministrativeAsset",null,asset,ct,"Contabilidad"));
 Check(J(await Call("AdministrativeAssets",ct,"Contabilidad")).GetArrayLength()==1&&J(await Call("AdministrativeAssets",ct,"Administración")).GetArrayLength()==0,"Inventories separated by department");
 Check(await Call("SaveAdministrativeAsset",saved.GetProperty("id").GetGuid(),asset,ct,"Administración")==null,"Cross-department asset update denied");
 Console.WriteLine($"SUCCESS {count} checks");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;DROP DATABASE [{name}]");}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
