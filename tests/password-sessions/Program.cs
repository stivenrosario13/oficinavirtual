using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var name="Codex_Password_QA_"+Guid.NewGuid().ToString("N");
const string master="Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True";
await using var root=new SqlConnection(master);await root.OpenAsync();
async Task Exec(SqlConnection c,string sql){await using var command=new SqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
await Exec(root,$"CREATE DATABASE [{name}]");
var previousEmail=Environment.GetEnvironmentVariable("ADMIN_EMAIL");var previousPassword=Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
try{
 var connectionString=new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString;
 await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
 await Exec(connection,"""
 CREATE TABLE dbo.admin_users(id char(36) NOT NULL PRIMARY KEY,email nvarchar(320) UNIQUE NOT NULL,display_name nvarchar(160) NOT NULL,role varchar(40) NOT NULL,password_salt binary(16) NOT NULL,password_hash binary(32) NOT NULL,password_iterations int NOT NULL,is_active bit NOT NULL,must_change_password bit NOT NULL DEFAULT 0,avatar_data varbinary(max) NULL,permissions_json nvarchar(max) NULL,support_team varchar(30) NULL,last_login_at datetime2 NULL,updated_at datetime2 NULL);
 """);
 var assembly=Assembly.LoadFrom(Path.GetFullPath("api/bin/Debug/net8.0/"+System.Xml.Linq.XDocument.Load("api/RegistroAgencias.Api.csproj").Descendants("AssemblyName").Single().Value+".dll"));
 var type=assembly.GetType("Database",true)!;
 var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=connectionString+";User Id=qa-placeholder"}).Build();
 var db=Activator.CreateInstance(type,config)!;
 var connections=(List<(string Value,string Mode)>)type.GetField("connections",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(db)!;
 connections.Clear();connections.Add((connectionString,"local-qa"));
 // Unrelated profile seeding is excluded; password/recovery/token methods and schemas are production code.
 type.GetField("userPermissionSchemaReady",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,true);
 async Task<object?> Call(string method,params object?[] args){var task=(Task)type.GetMethod(method)!.Invoke(db,args)!;await task;return task.GetType().GetProperty("Result")?.GetValue(task);}
 string Field(object row,string field)=>(string)row.GetType().GetProperty(field)!.GetValue(row)!;
 void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
 var ct=CancellationToken.None;
 Environment.SetEnvironmentVariable("ADMIN_EMAIL","principal@qa.test");Environment.SetEnvironmentVariable("ADMIN_PASSWORD","Original-bootstrap-123!");
 Check(await Call("ChangeLegacyPassword","principal@qa.test","wrong","Nueva-clave-123!",ct) is null,"Wrong bootstrap password rejected without creating account");
 Check((bool)(await Call("LegacyPasswordAccessEnabled",ct))!,"Failed change leaves bootstrap access unchanged");
 var account=(await Call("ChangeLegacyPassword","principal@qa.test","Original-bootstrap-123!","Nueva-clave-123!",ct))!;
 var id=Guid.Parse(Field(account,"Id"));var version=Field(account,"RecoveryVersion");
 Check(version.Length>0,"Bootstrap account becomes permanent with versioned session");
 Check(!(bool)(await Call("LegacyPasswordAccessEnabled",ct))!,"Old bootstrap cookies and environment fallback disabled");
 Check(await Call("ChangeLegacyPassword","principal@qa.test","Original-bootstrap-123!","Attack-reset-123!",ct) is null,"Old environment password cannot overwrite new password");
 Check(await Call("AuthenticateUser","principal@qa.test","Original-bootstrap-123!",ct) is null,"Old password no longer authenticates");
 Check(await Call("AuthenticateUser","principal@qa.test","Nueva-clave-123!",ct) is not null,"New password authenticates permanent account");
 var tokenA=(string)(await Call("CreatePersistentLoginToken",id.ToString(),ct,version))!;
 var tokenB=(string)(await Call("CreatePersistentLoginToken",id.ToString(),ct,version))!;
 Check(await Call("RestorePersistentLogin",tokenA,ct) is not null && await Call("RestorePersistentLogin",tokenB,ct) is not null,"Two devices can restore before password change");
 var recoveryCode=(string)(await Call("GenerateRecoveryCode",id.ToString(),"Nueva-clave-123!",ct))!;
 Check(await Call("ChangeOwnPassword",id,"wrong","Segunda-clave-123!",ct) is null,"Wrong current password leaves password and sessions untouched");
 Check(await Call("RestorePersistentLogin",tokenB,ct) is not null,"Failed change does not log out other device");
 var updated=(await Call("ChangeOwnPassword",id,"Nueva-clave-123!","Segunda-clave-123!",ct))!;
 var nextVersion=Field(updated,"RecoveryVersion");
 Check(nextVersion!=version,"Old cookie version no longer matches database");
 Check(await Call("RestorePersistentLogin",tokenA,ct) is null && await Call("RestorePersistentLogin",tokenB,ct) is null,"All old device tokens revoked");
 Check(await Call("CreatePersistentLoginToken",id.ToString(),ct,version) is null,"Stale login cannot mint a replacement persistent token");
 var currentToken=(string)(await Call("CreatePersistentLoginToken",id.ToString(),ct,nextVersion))!;
 Check(await Call("RestorePersistentLogin",currentToken,ct) is not null,"Changing device receives a valid replacement token");
 Check(!(bool)(await Call("RecoverPassword","principal@qa.test",recoveryCode,"Reused-code-123!",ct))!,"Previous recovery code invalidated with password change");
 var changes=await Task.WhenAll(Call("ChangeOwnPassword",id,"Segunda-clave-123!","Concurrent-A-123!",ct),Call("ChangeOwnPassword",id,"Segunda-clave-123!","Concurrent-B-123!",ct));
 Check(changes.Count(value=>value is not null)==1,"Concurrent password changes cannot both accept old password");
 // Existing principal is reused without changing identity/permissions or adding duplicate accounts.
 await Exec(connection,"DELETE FROM dbo.account_recovery WHERE user_id=N'legacy-admin'; UPDATE dbo.admin_users SET display_name=N'Nombre conservado',permissions_json=N'{\"canManageUsers\":true}'");
 var reused=(await Call("ChangeLegacyPassword","principal@qa.test","Original-bootstrap-123!","Final-clave-123!",ct))!;
 Check(Field(reused,"Id")==id.ToString() && Field(reused,"DisplayName")=="Nombre conservado","Existing principal identity retained during bootstrap conversion");
 await using var count=new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users",connection);
 Check(Convert.ToInt32(await count.ExecuteScalarAsync())==1,"No duplicate principal account");
 var finalVersion=Field(reused,"RecoveryVersion");
 var recoveryToken=(string)(await Call("CreatePersistentLoginToken",id.ToString(),ct,finalVersion))!;
 Check(await Call("GenerateRecoveryCodeFromLogin","principal@qa.test","incorrecta",ct) is null,"Login code generation rejects wrong password");
 Check(await Call("GenerateRecoveryCodeFromLogin","unknown@qa.test","Final-clave-123!",ct) is null,"Login code generation rejects unknown user");
 await Exec(connection,"UPDATE dbo.admin_users SET must_change_password=1");
 Check(await Call("GenerateRecoveryCodeFromLogin","principal@qa.test","Final-clave-123!",ct) is null,"Temporary-password account cannot generate recovery code");
 await Exec(connection,"UPDATE dbo.admin_users SET must_change_password=0,is_active=0");
 Check(await Call("GenerateRecoveryCodeFromLogin","principal@qa.test","Final-clave-123!",ct) is null,"Inactive account cannot generate recovery code");
 await Exec(connection,"UPDATE dbo.admin_users SET is_active=1");
 var firstCode=(string)(await Call("GenerateRecoveryCodeFromLogin","principal@qa.test","Final-clave-123!",ct))!;
 var secondCode=(string)(await Call("GenerateRecoveryCodeFromLogin","principal@qa.test","Final-clave-123!",ct))!;
 Check(firstCode.Length==39 && firstCode!=secondCode,"Login generates and replaces a private recovery code");
 Check(await Call("RestorePersistentLogin",recoveryToken,ct) is not null,"Code generation does not change password or revoke sessions");
 Check(!(bool)(await Call("RecoverPassword","principal@qa.test",firstCode,"Recuperada-123!",ct))!,"Replaced recovery code cannot reset password");
 Check((bool)(await Call("RecoverPassword","principal@qa.test",secondCode,"Recuperada-123!",ct))!,"Code generated at login recovers the account");
 Check(await Call("RestorePersistentLogin",recoveryToken,ct) is null,"Password recovery still revokes other-device tokens");
 Check(!(bool)(await Call("RecoverPassword","principal@qa.test",secondCode,"Reutilizada-123!",ct))!,"Recovery code is single-use");
 Console.WriteLine("PASS password/session integration; production data untouched");
}finally{
 Environment.SetEnvironmentVariable("ADMIN_EMAIL",previousEmail);Environment.SetEnvironmentVariable("ADMIN_PASSWORD",previousPassword);
 SqlConnection.ClearAllPools();await Exec(root,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
}
