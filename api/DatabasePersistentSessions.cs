using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

sealed partial class Database
{
    static readonly SemaphoreSlim PersistentSessionSchemaLock = new(1,1);
    static bool persistentSessionSchemaReady;

    static byte[] PersistentSessionHash(string token) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));

    async Task EnsurePersistentSessionSchema(SqlConnection connection,CancellationToken ct)
    {
        if(persistentSessionSchemaReady)return;
        await PersistentSessionSchemaLock.WaitAsync(ct);
        try
        {
            if(persistentSessionSchemaReady)return;
            const string sql="""
                IF OBJECT_ID(N'dbo.persistent_login_tokens',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.persistent_login_tokens(
                        id uniqueidentifier NOT NULL CONSTRAINT DF_persistent_login_tokens_id DEFAULT NEWID(),
                        user_id uniqueidentifier NOT NULL,
                        token_hash varbinary(32) NOT NULL,
                        created_at datetime2(3) NOT NULL CONSTRAINT DF_persistent_login_tokens_created DEFAULT SYSUTCDATETIME(),
                        last_used_at datetime2(3) NOT NULL CONSTRAINT DF_persistent_login_tokens_used DEFAULT SYSUTCDATETIME(),
                        revoked_at datetime2(3) NULL,
                        CONSTRAINT PK_persistent_login_tokens PRIMARY KEY(id),
                        CONSTRAINT UQ_persistent_login_tokens_hash UNIQUE(token_hash),
                        CONSTRAINT FK_persistent_login_tokens_user FOREIGN KEY(user_id) REFERENCES dbo.admin_users(id) ON DELETE CASCADE
                    );
                    CREATE INDEX IX_persistent_login_tokens_user ON dbo.persistent_login_tokens(user_id,revoked_at);
                END
                """;
            await using var command=new SqlCommand(sql,connection);
            await command.ExecuteNonQueryAsync(ct);
            persistentSessionSchemaReady=true;
        }
        finally{PersistentSessionSchemaLock.Release();}
    }

    public async Task<string?> CreatePersistentLoginToken(string userId,CancellationToken ct)
    {
        if(!Guid.TryParse(userId,out var id))return null;
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var connection=await Open(ct);
        await EnsurePersistentSessionSchema(connection,ct);
        await using var command=new SqlCommand("INSERT INTO dbo.persistent_login_tokens(user_id,token_hash) VALUES(@userId,@hash);",connection);
        command.Parameters.AddWithValue("@userId",id);
        command.Parameters.Add("@hash",System.Data.SqlDbType.VarBinary,32).Value=PersistentSessionHash(token);
        await command.ExecuteNonQueryAsync(ct);
        return token;
    }

    public async Task<AdminIdentity?> RestorePersistentLogin(string? token,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(token)||token.Length!=64||!token.All(Uri.IsHexDigit))return null;
        await using var connection=await Open(ct);
        await EnsurePersistentSessionSchema(connection,ct);
        Guid? userId=null;
        await using(var command=new SqlCommand("UPDATE dbo.persistent_login_tokens SET last_used_at=SYSUTCDATETIME() OUTPUT inserted.user_id WHERE token_hash=@hash AND revoked_at IS NULL;",connection))
        {
            command.Parameters.Add("@hash",System.Data.SqlDbType.VarBinary,32).Value=PersistentSessionHash(token);
            var value=await command.ExecuteScalarAsync(ct);
            if(value is Guid id)userId=id;
        }
        return userId is Guid restoredId?await Account(restoredId,ct):null;
    }

    public async Task RevokePersistentLogin(string? token,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(token)||token.Length!=64||!token.All(Uri.IsHexDigit))return;
        await using var connection=await Open(ct);
        await EnsurePersistentSessionSchema(connection,ct);
        await using var command=new SqlCommand("UPDATE dbo.persistent_login_tokens SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()) WHERE token_hash=@hash;",connection);
        command.Parameters.Add("@hash",System.Data.SqlDbType.VarBinary,32).Value=PersistentSessionHash(token);
        await command.ExecuteNonQueryAsync(ct);
    }
}
