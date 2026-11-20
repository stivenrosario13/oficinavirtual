using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

var name="Codex_LoginRecovery_QA_"+Guid.NewGuid().ToString("N");
if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^Codex_LoginRecovery_QA_[a-f0-9]{32}$"))throw new Exception("Unsafe database name");
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();
bool created=false;
try
{
    await Exec(master,$"IF DB_ID('{name}') IS NOT NULL THROW 50000,'Database exists',1; CREATE DATABASE [{name}];");
    created=true;
    await using var connection=new SqlConnection($"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True");
    await connection.OpenAsync();
    await Exec(connection,"""
        CREATE TABLE dbo.admin_users
        (
          id char(36) PRIMARY KEY,email nvarchar(320) NOT NULL UNIQUE,display_name nvarchar(160) NOT NULL,
          role varchar(40) NOT NULL,password_salt binary(16) NOT NULL,password_hash binary(32) NOT NULL,
          password_iterations int NOT NULL,is_active bit NOT NULL,must_change_password bit NOT NULL,
          last_login_at datetime2 NULL,updated_at datetime2 NULL,permissions_json nvarchar(max) NULL,support_team varchar(30) NULL
        );
        """);
    var originalSalt=RandomNumberGenerator.GetBytes(16);
    var originalHash=Database.Hash("historical-only",originalSalt,210000);
    foreach(var (email,lastLogin) in new[]{("waskar.sepulveda@grupotejeda.local",false),("old.supervisor@grupotejeda.local",false),("active.supervisor@grupotejeda.local",true)})
    {
        await using var insert=new SqlCommand("INSERT dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password,last_login_at) VALUES(CONVERT(char(36),NEWID()),@email,@email,'GROUP_ADMIN',@salt,@hash,210000,1,0,@lastLogin)",connection);
        insert.Parameters.AddWithValue("@email",email);insert.Parameters.AddWithValue("@salt",originalSalt);insert.Parameters.AddWithValue("@hash",originalHash);
        insert.Parameters.AddWithValue("@lastLogin",lastLogin?DateTime.UtcNow:DBNull.Value);
        await insert.ExecuteNonQueryAsync();
    }
    await using(var existingAdmin=new SqlCommand("INSERT dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password,last_login_at,permissions_json) VALUES(CONVERT(char(36),NEWID()),'admin@local.com','Administrador principal','ADMINISTRATOR',@salt,@hash,210000,1,0,SYSUTCDATETIME(),N'{\"canManageUsers\":true}')",connection))
    {
        existingAdmin.Parameters.AddWithValue("@salt",originalSalt);
        existingAdmin.Parameters.AddWithValue("@hash",originalHash);
        await existingAdmin.ExecuteNonQueryAsync();
    }
    await Database.Recover(connection);
    var rows=await Read(connection);
    Check(rows.Count==4,"Existing principal account is reused");
    var admin=rows["admin@local.com"];
    Check(admin.Role=="ADMINISTRATOR"&&admin.Active&&admin.MustChange,"Principal account retains role and requires password change");
    Check(Database.Hash("123456",admin.Salt,admin.Iterations).SequenceEqual(admin.Hash),"Principal account uses requested temporary password");
    Check(admin.Permissions=="{\"canManageUsers\":true}"&&admin.Name=="Administrador principal","Principal identity and permissions are preserved");
    var waskar=rows["waskar.sepulveda@grupotejeda.local"];
    Check(waskar.MustChange&&Database.Hash("123456",waskar.Salt,waskar.Iterations).SequenceEqual(waskar.Hash),"Waskar can use temporary password");
    Check(rows["old.supervisor@grupotejeda.local"].MustChange&&rows["old.supervisor@grupotejeda.local"].Hash.SequenceEqual(originalHash),"Never-logged supervisor keeps stored password");
    Check(!rows["active.supervisor@grupotejeda.local"].MustChange&&rows["active.supervisor@grupotejeda.local"].Hash.SequenceEqual(originalHash),"Previously logged supervisor untouched");
    await using(var change=new SqlCommand("UPDATE dbo.admin_users SET password_hash=@newHash,must_change_password=0 WHERE email IN ('waskar.sepulveda@grupotejeda.local','admin@local.com')",connection))
    {
        change.Parameters.AddWithValue("@newHash",originalHash);
        await change.ExecuteNonQueryAsync();
    }
    await Database.Recover(connection);
    rows=await Read(connection);
    Check(rows.Count==4,"Repeated startup does not duplicate the administrator");
    Check(rows["waskar.sepulveda@grupotejeda.local"].Hash.SequenceEqual(originalHash)&&!rows["waskar.sepulveda@grupotejeda.local"].MustChange,"Repeated startup preserves Waskar's new password");
    Check(rows["admin@local.com"].Hash.SequenceEqual(originalHash)&&!rows["admin@local.com"].MustChange,"Repeated startup preserves principal administrator's new password");
    Console.WriteLine("PASS 10 recovery checks; live database untouched");
}
finally
{
    if(created){SqlConnection.ClearAllPools();await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");}
}

static void Check(bool result,string description){if(!result)throw new Exception(description);Console.WriteLine("PASS "+description);}
static async Task Exec(SqlConnection connection,string sql){await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync();}
static async Task<Dictionary<string,(string Role,bool Active,bool MustChange,byte[] Salt,byte[] Hash,int Iterations,string Name,string? Permissions)>> Read(SqlConnection connection)
{
    await using var command=new SqlCommand("SELECT email,role,is_active,must_change_password,password_salt,password_hash,password_iterations,display_name,permissions_json FROM dbo.admin_users",connection);
    await using var reader=await command.ExecuteReaderAsync();
    var rows=new Dictionary<string,(string,bool,bool,byte[],byte[],int,string,string?)>();
    while(await reader.ReadAsync())rows[reader.GetString(0)]=(reader.GetString(1),reader.GetBoolean(2),reader.GetBoolean(3),(byte[])reader[4],(byte[])reader[5],reader.GetInt32(6),reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8));
    return rows;
}

partial class Database
{
    public static Task Recover(SqlConnection connection)=>EnsureLegacyAccountRecovery(connection,CancellationToken.None);
    public static byte[] Hash(string password,byte[] salt,int iterations)=>PasswordHash(password,salt,iterations);
    static byte[] PasswordHash(string password,byte[] salt,int iterations)=>Rfc2898DeriveBytes.Pbkdf2(password,salt,iterations,HashAlgorithmName.SHA256,32);
}
