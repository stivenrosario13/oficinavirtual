using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

record RecoveryCodeRequest(string? CurrentPassword);
record RecoveryCodeLoginRequest(string? UserName,string? CurrentPassword);
record PasswordRecoveryRequest(string? UserName,string? Code,string? NewPassword);
sealed partial class Database
{
    static readonly SemaphoreSlim RecoverySchemaLock=new(1,1);
    static bool recoverySchemaReady;
    static byte[] RecoveryHash(string code)=>SHA256.HashData(Encoding.UTF8.GetBytes(code.Replace("-","").Replace(" ","").Trim().ToUpperInvariant()));
    static async Task EnsureRecoverySchema(SqlConnection c,CancellationToken ct)
    {
        if(recoverySchemaReady)return;
        await RecoverySchemaLock.WaitAsync(ct);
        try{
            if(recoverySchemaReady)return;
            await using var cmd=new SqlCommand("""
                IF OBJECT_ID(N'dbo.account_recovery',N'U') IS NULL
                CREATE TABLE dbo.account_recovery(
                    user_id nvarchar(80) NOT NULL PRIMARY KEY,
                    code_hash varbinary(32) NULL,
                    session_version nvarchar(36) NOT NULL DEFAULT N'',
                    generated_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
                    used_at datetime2(3) NULL
                );
                """,c);
            await cmd.ExecuteNonQueryAsync(ct);recoverySchemaReady=true;
        }finally{RecoverySchemaLock.Release();}
    }
    public Task<string?> GenerateRecoveryCode(string userId,string password,CancellationToken ct)
        => GenerateRecoveryCodeCore(userId,password,false,ct);
    public Task<string?> GenerateRecoveryCodeFromLogin(string userName,string password,CancellationToken ct)
        => GenerateRecoveryCodeCore(userName,password,true,ct);
    async Task<string?> GenerateRecoveryCodeCore(string identifier,string password,bool byLogin,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(identifier)||identifier.Length>320||string.IsNullOrEmpty(password)||password.Length>128)return null;
        await using var c=await Open(ct);await EnsureRecoverySchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        string userId;
        // The lookup mode is internal, and the submitted identifier is always a SQL parameter.
        var lookup=byLogin?"email=@identifier":"id=@identifier";
        await using(var read=new SqlCommand($"SELECT password_salt,password_hash,password_iterations,CONVERT(nvarchar(80),id) FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE {lookup} AND is_active=1 AND must_change_password=0",c,tx)){
            read.Parameters.AddWithValue("@identifier",identifier);await using var r=await read.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct)||!CryptographicOperations.FixedTimeEquals(PasswordHash(password,(byte[])r[0],r.GetInt32(2)),(byte[])r[1]))return null;
            userId=r.GetString(3);
        }
        var raw=Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var code=string.Join("-",Enumerable.Range(0,8).Select(i=>raw.Substring(i*4,4)));
        await using(var write=new SqlCommand("""
            UPDATE dbo.account_recovery SET code_hash=@hash,generated_at=SYSUTCDATETIME(),used_at=NULL WHERE user_id=@id;
            IF @@ROWCOUNT=0 INSERT dbo.account_recovery(user_id,code_hash) VALUES(@id,@hash);
            """,c,tx)){
            write.Parameters.AddWithValue("@id",userId);write.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=RecoveryHash(code);await write.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);return code;
    }
    public async Task<bool> RecoverPassword(string userName,string code,string newPassword,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(userName)||userName.Length>160||string.IsNullOrWhiteSpace(code)||code.Length>80||newPassword.Length<8||newPassword.Length>128||newPassword=="123456")return false;
        await using var c=await Open(ct);await EnsureRecoverySchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        string userId;
        await using(var user=new SqlCommand("SELECT CONVERT(nvarchar(80),id) FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE email=@email AND is_active=1",c,tx)){
            user.Parameters.AddWithValue("@email",userName);var value=await user.ExecuteScalarAsync(ct);if(value is not string id)return false;userId=id;
        }
        await using(var read=new SqlCommand("SELECT code_hash FROM dbo.account_recovery WITH(UPDLOCK,HOLDLOCK) WHERE user_id=@id AND code_hash IS NOT NULL",c,tx)){
            read.Parameters.AddWithValue("@id",userId);var hash=await read.ExecuteScalarAsync(ct);
            if(hash is not byte[] expected||!CryptographicOperations.FixedTimeEquals(expected,RecoveryHash(code)))return false;
        }
        var salt=RandomNumberGenerator.GetBytes(16);var hashPassword=PasswordHash(newPassword,salt,210000);
        await using(var write=new SqlCommand("""
            UPDATE dbo.admin_users SET password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=0,updated_at=SYSUTCDATETIME() WHERE id=@id AND is_active=1;
            UPDATE dbo.account_recovery SET code_hash=NULL,used_at=SYSUTCDATETIME(),session_version=@version WHERE user_id=@id;
            IF OBJECT_ID(N'dbo.persistent_login_tokens',N'U') IS NOT NULL
                UPDATE dbo.persistent_login_tokens SET revoked_at=SYSUTCDATETIME() WHERE user_id=@id AND revoked_at IS NULL;
            """,c,tx)){
            write.Parameters.AddWithValue("@id",userId);write.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;write.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hashPassword;write.Parameters.AddWithValue("@version",Guid.NewGuid().ToString());await write.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);return true;
    }
}
