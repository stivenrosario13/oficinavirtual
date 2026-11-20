using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

var name="Codex_AdminProfiles_QA_"+Guid.NewGuid().ToString("N");
if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^Codex_AdminProfiles_QA_[a-f0-9]{32}$"))throw new Exception("Unsafe test database name");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();bool created=false;int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
try{
 await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Refusing existing database',1; CREATE DATABASE [{name}];");created=true;
 await using var c=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE admin_users(id char(36) PRIMARY KEY,email nvarchar(160) UNIQUE,display_name nvarchar(160),role varchar(40),password_salt varbinary(16),password_hash varbinary(32),password_iterations int,is_active bit,region nvarchar(100),must_change_password bit,permissions_json nvarchar(max),support_team varchar(30),CONSTRAINT CK_admin_users_role CHECK(role IN('ADMINISTRATOR','VIEWER')));
 INSERT admin_users VALUES(CONVERT(char(36),NEWID()),'emma.mendoza@grupotejeda.local','Emma existente','ADMINISTRATOR',0x01,0x0203,123,1,N'Administración',0,N'{"canManageUsers":true}',NULL);
 """);
 await Database.Seed(c);await Database.Seed(c);
 await using(var count=new SqlCommand("SELECT COUNT(*) FROM admin_users",c))Check(Convert.ToInt32(await count.ExecuteScalarAsync())==6,"Six distinct accounts after repeated provisioning");
 await using(var read=new SqlCommand("SELECT email,role,password_salt,password_hash,password_iterations,must_change_password,permissions_json FROM admin_users",c)){
  await using var r=await read.ExecuteReaderAsync();while(await r.ReadAsync()){
   var login=r.GetString(0);
   if(login.StartsWith("emma."))Check(r.GetString(1)=="ADMINISTRATOR"&&!r.GetBoolean(5)&&((byte[])r[3]).SequenceEqual(new byte[]{2,3})&&r.GetString(6).Contains("canManageUsers"),"Existing Emma role, password and permissions preserved");
   else Check(r.GetString(1)=="ADMINISTRATIVE"&&r.GetBoolean(5)&&Database.Hash("123456",(byte[])r[2],r.GetInt32(4)).SequenceEqual((byte[])r[3]),login+" temporary login and forced password change");
  }
 }
 foreach(var login in new[]{"emma.mendoza","derihan.alisha","whilmaly.sulay","sandra.ramirez","emely.figueroa","diana.ventura"}){
  var actor=new DocumentActor("Administrative",login+"@grupotejeda.local");var allowed=AdministrativeProfiles.Forms(login)!;
  foreach(var form in new[]{"SIM","MARBETE","DEVICE","PAYMENT","FLEET","MODEM","CONTRACT_REQUEST","CONTRACT_RENEWAL","RENTAL_CONTRACT","RENT_RECEIPT","INCREASE_LETTER","FUEL_LOG","GENERAL"})Check(AdministrativeProfiles.CanRead(actor,new(form,"OTRO"))==allowed.Contains(form),login+" / "+form);
 }
 Check(AdministrativeProfiles.CanRead(new("Administrator","admin@local.com"),new("FUEL_LOG","OTRO")),"Principal administrator unrestricted");
 Check(!AdministrativeProfiles.CanRead(new("Administrative","unknown@grupotejeda.local"),new("SIM","OTRO")),"Unconfigured administrative profile fails closed");
 Check(AdministrativeProfiles.CanRead(new("Administrative","emma.mendoza"),new("GENERAL","FLOTA")),"Legacy fleet routing preserved");
 Check(!AdministrativeProfiles.CanRead(new("Administrative","emma.mendoza"),new("GENERAL","MODEM")),"Legacy modem cannot bypass assigned scope");
 foreach(var path in new[]{"/api/tickets","/api/support/catalog","/api/admin/users","/api/audits","/api/maintenance/movements"})Check(!AdministrativeProfiles.ApiAllowed(path),"New administrative role cannot access "+path);
 foreach(var path in new[]{"/api/admin/session","/api/account/password","/api/institutional-documents/123/download","/api/warehouse/fulfillment/inventory"})Check(AdministrativeProfiles.ApiAllowed(path),"Administrative workflow allowed "+path);
 Console.WriteLine($"SUCCESS {checks} checks; live database untouched");
}finally{if(created){SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");}}
static async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
record DocumentActor(string Role,string Login);
record AdministrativeRequestInfo(string? FormType,string AssetType);
partial class Database{
 public static Task Seed(SqlConnection c)=>EnsureAdministrativeProfiles(c,CancellationToken.None);
 public static byte[] Hash(string password,byte[] salt,int count)=>PasswordHash(password,salt,count);
 static byte[] PasswordHash(string password,byte[] salt,int iterations)=>Rfc2898DeriveBytes.Pbkdf2(password,salt,iterations,HashAlgorithmName.SHA256,32);
}
