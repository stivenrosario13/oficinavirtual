using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
var api=Assembly.LoadFrom(Directory.GetFiles(AppContext.BaseDirectory,"RegistroAgencias.SqlServer.V*.dll").OrderByDescending(File.GetLastWriteTimeUtc).First());
const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
int checks=0;void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
var gate=api.GetType("TemporaryPasswordGate")!.GetMethod("Invoke",flags)!;
using var services=new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
foreach(var path in new[]{"/api/license/status","/api/version","/api/admin/session","/api/account/password","/api/tickets","/api/admin/users","/api/institutional-documents/123/approve"}){
 var context=new DefaultHttpContext{RequestServices=services,User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name,"inocencio.avila@grupotejeda.local"),new Claim(ClaimTypes.Role,"Technology"),new Claim("support_team","CALL_CENTER"),new Claim("must_change_password","true")],"qa"))};
 context.Request.Path=path;context.Response.Body=new MemoryStream();var next=false;
 await (Task)gate.Invoke(null,[context,(Func<Task>)(()=>{next=true;return Task.CompletedTask;})])!;
 var publicOrAccount=path is "/api/license/status" or "/api/version" or "/api/admin/session" or "/api/account/password";
 Check(next==publicOrAccount,"Temporary-password gate: "+path);
 if(!next){context.Response.Body.Position=0;var payload=await JsonDocument.ParseAsync(context.Response.Body);Check(context.Response.StatusCode==403&&payload.RootElement.GetProperty("code").GetString()=="PASSWORD_CHANGE_REQUIRED","Protected operations still require a password change");}
}
foreach(var scenario in new[]{"pending-received","pending","received","approved","rejected","cancelled","correction_requested","other-destination","other-channel","other-area","delivered"}){
 var type=api.GetType("InstitutionalFile")!;var doc=Activator.CreateInstance(type)!;
 void Set(string key,object value)=>type.GetProperty(key)!.SetValue(doc,value);
 var status=scenario is "pending" or "received" or "approved" or "rejected" or "cancelled" or "correction_requested"?scenario:"pending";
 Set("Status",status);Set("Area",scenario=="other-area"?"files":"communications");Set("Channel",scenario=="other-channel"?"requests":"requirements");Set("RecipientDepartment",scenario=="other-destination"?"Tecnología":"Almacén");
 if(scenario!="pending")Set("ReceivedUtc",DateTime.UtcNow);
 if(scenario=="delivered")Set("WarehouseDelivery",Activator.CreateInstance(api.GetType("WarehouseDeliveryInfo")!,["QA","Operador","qa","Receptor",DateTime.UtcNow])!);
 type.GetMethod("NormalizeWarehouseReceiptState")!.Invoke(doc,[]);
 Check((string)type.GetProperty("Status")!.GetValue(doc)! ==(scenario=="pending-received"?"received":status),"Receipt state normalization preserves scope and later decisions: "+scenario);
}
var name="Codex_WarehouseAuth_QA_"+Guid.NewGuid().ToString("N");
// The API must recompute the capability for each reader, even for legacy PDFs.
foreach(var endpoint in new[]{"InstitutionalDocumentEndpoints","WarehouseFulfillmentEndpoints"})
foreach(var scenario in new[]{"ana","diorly","denied","no-document-decision","requester","other-department","approved","pending","trashed","delivered","other-destination"}){
 var grant=scenario!="denied";
 var permissions=new Dictionary<string,bool>{{"canAuthorizeWarehouseRequests",grant},{"canDecideDocuments",scenario!="no-document-decision"}};
 if(scenario=="requester")permissions["profile:WAREHOUSE_REQUESTER"]=true;
 var role=scenario=="diorly"?"Technology":scenario=="other-department"?"GeneralServices":"Warehouse";
 var team=scenario=="diorly"?"WAREHOUSE":"";
 var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"qa-reader"),new Claim(ClaimTypes.Name,scenario+"@grupotejeda.local"),new Claim(ClaimTypes.Role,role),new Claim("support_team",team),new Claim("permissions_json",JsonSerializer.Serialize(permissions))],"qa"))};
 var actor=api.GetType(endpoint)!.GetMethod("Actor",flags)!.Invoke(null,[context]);
 var type=api.GetType("InstitutionalFile")!;var document=Activator.CreateInstance(type)!;
 void Set(string property,object value)=>type.GetProperty(property)!.SetValue(document,value);
 Set("CanAuthorizeWarehouseRequest",true); // Simulate stale or forged persisted metadata.
 Set("Channel","requirements");Set("RecipientDepartment",scenario=="other-destination"?"Tecnología":"Almacén");
 Set("Status",scenario is "pending" or "approved"?scenario:"received");Set("Trashed",scenario=="trashed");
 if(scenario=="delivered")Set("WarehouseDelivery",Activator.CreateInstance(api.GetType("WarehouseDeliveryInfo")!,["QA","Operador","qa","Receptor",DateTime.UtcNow])!);
 type.GetMethod("For")!.Invoke(document,[actor]);
 Check((bool)type.GetProperty("CanAuthorizeWarehouseRequest")!.GetValue(document)! ==(scenario is "ana" or "diorly"),endpoint+" recomputes authorization for "+scenario);
}
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");await master.OpenAsync();await Exec(master,$"CREATE DATABASE [{name}]");
try{
 await using var connection=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");await connection.OpenAsync();
 await Exec(connection,"CREATE TABLE dbo.admin_users(id char(36) PRIMARY KEY,email nvarchar(320),role varchar(40),support_team varchar(40),permissions_json nvarchar(max),updated_at datetime2,is_active bit,password_token nvarchar(80));");
 var original="{\"profile:SUPPORT\":true,\"usesExplicitPermissions\":true,\"canAuthorizeWarehouseRequests\":false,\"canDecideDocuments\":false,\"canLoadWarehouseInventory\":false,\"canManageUsers\":false,\"canPrepareWarehouseRequests\":true,\"metadata\":true}";
 foreach(var login in new[]{"ana.rodriguez","diorly.abreu","other.operator"}){
  await using var cmd=new SqlCommand("INSERT dbo.admin_users VALUES(CONVERT(char(36),NEWID()),@email,'WAREHOUSE','WAREHOUSE',@permissions,NULL,1,'untouched')",connection);cmd.Parameters.AddWithValue("@email",login+"@grupotejeda.local");cmd.Parameters.AddWithValue("@permissions",original);await cmd.ExecuteNonQueryAsync();
 }
 var db=api.GetType("Database")!;var restore=db.GetMethod("RestoreWarehouseAuthorizers",flags)!;
 await (Task)restore.Invoke(null,[connection,CancellationToken.None])!;
 var accountPermissions=api.GetType("Program")!.GetMethods(flags).Single(m=>m.Name.Contains("g__AccountPermissions|"));
 await using(var read=new SqlCommand("SELECT id,email,permissions_json,is_active,password_token FROM dbo.admin_users ORDER BY email",connection))
 await using(var reader=await read.ExecuteReaderAsync())while(await reader.ReadAsync()){
  var email=reader.GetString(1);var raw=reader.GetString(2);var saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(raw)!;
  if(email.StartsWith("other.")){Check(raw==original,"Other warehouse operator untouched");continue;}
  Check(reader.GetBoolean(3)&&reader.GetString(4)=="untouched","Account state and password preserved: "+email);
  Check(saved["canAuthorizeWarehouseRequests"]&&saved["canDecideDocuments"],"Authorization and document permission restored: "+email);
  var before=JsonSerializer.Deserialize<Dictionary<string,bool>>(original)!;Check(saved.Count==before.Count&&before.All(p=>p.Key is "canAuthorizeWarehouseRequests" or "canDecideDocuments"||saved[p.Key]==p.Value),"Every unrelated permission preserved: "+email);
  var account=Activator.CreateInstance(api.GetType("AdminIdentity")!,[reader.GetString(0),email,"QA","Warehouse",false,null,saved,"WAREHOUSE"]);
  var effective=(Dictionary<string,bool>)accountPermissions.Invoke(null,[account])!;
  Check(effective["canAuthorizeWarehouseRequests"]&&effective["canDecideDocuments"]&&!effective["canManageUsers"],"Real session displays authorization without admin privileges: "+email);
  var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,reader.GetString(0)),new Claim(ClaimTypes.Name,email),new Claim(ClaimTypes.Role,"Warehouse"),new Claim("support_team","WAREHOUSE"),new Claim("permissions_json",JsonSerializer.Serialize(effective))],"qa"));
  Check((bool)api.GetType("WarehousePermission")!.GetMethod("Has",flags)!.Invoke(null,[principal,"canAuthorizeWarehouseRequests"])!,"Endpoint authorization recognizes restored grant");
  Check((bool)api.GetType("OperationalPermissions")!.GetMethod("Allowed",flags)!.Invoke(null,[principal,"canDecideDocuments"])!,"Document permission middleware accepts approval");
 }
 await (Task)restore.Invoke(null,[connection,CancellationToken.None])!;
 await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.warehouse_authorization_recovery WHERE previous_permissions=@previous",connection)){cmd.Parameters.AddWithValue("@previous",original);Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==2,"Exactly two original permission backups; repeat startup is idempotent");}
 Console.WriteLine($"SUCCESS {checks} warehouse authorization and new-account license regressions");
}finally{SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");}
static async Task Exec(SqlConnection connection,string sql){await using var cmd=new SqlCommand(sql,connection);await cmd.ExecuteNonQueryAsync();}
