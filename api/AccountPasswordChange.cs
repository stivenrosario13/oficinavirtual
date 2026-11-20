using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

sealed partial class Database
{
    static bool PasswordChangeValid(string password) => password.Length is >= 8 and <= 128 && password != "123456";

    static async Task DisableLegacyPasswordAccess(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
    {
        await using var command = new SqlCommand("UPDATE dbo.account_recovery SET session_version=@version WHERE user_id=N'legacy-admin'; IF @@ROWCOUNT=0 INSERT dbo.account_recovery(user_id,session_version) VALUES(N'legacy-admin',@version);",connection,transaction);
        command.Parameters.AddWithValue("@version",Guid.NewGuid().ToString());
        await command.ExecuteNonQueryAsync(ct);
    }

    static async Task<string> RotatePasswordSessions(SqlConnection connection, SqlTransaction transaction, string userId, CancellationToken ct)
    {
        var version = Guid.NewGuid().ToString();
        await using var command = new SqlCommand("""
            UPDATE dbo.account_recovery SET session_version=@version,code_hash=NULL,used_at=SYSUTCDATETIME() WHERE user_id=@id;
            IF @@ROWCOUNT=0 INSERT dbo.account_recovery(user_id,session_version) VALUES(@id,@version);
            IF OBJECT_ID(N'dbo.persistent_login_tokens',N'U') IS NOT NULL
                UPDATE dbo.persistent_login_tokens SET revoked_at=SYSUTCDATETIME() WHERE user_id=@id AND revoked_at IS NULL;
            """, connection, transaction);
        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync(ct);
        return version;
    }

    // The environment password is a bootstrap credential, not a second permanent password.
    public async Task<bool> LegacyPasswordAccessEnabled(CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await EnsureRecoverySchema(connection, ct);
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.account_recovery WHERE user_id=N'legacy-admin' AND session_version<>N''", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0;
    }

    public async Task<AdminIdentity?> ChangeLegacyPassword(string email, string currentPassword, string newPassword, CancellationToken ct)
    {
        var configuredEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim().ToLowerInvariant();
        var configuredPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (!PasswordChangeValid(newPassword) || string.IsNullOrWhiteSpace(configuredEmail) || string.IsNullOrEmpty(configuredPassword) ||
            !string.Equals(email, configuredEmail, StringComparison.OrdinalIgnoreCase) ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(currentPassword)), SHA256.HashData(Encoding.UTF8.GetBytes(configuredPassword)))) return null;
        await using var connection = await Open(ct);
        await EnsureUserPermissionSchema(connection,ct);
        await EnsureRecoverySchema(connection,ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        string id;
        await using (var read = new SqlCommand("SELECT CONVERT(nvarchar(80),id),role,is_active FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE email=@email",connection,transaction))
        {
            read.Parameters.AddWithValue("@email",configuredEmail);
            await using var reader = await read.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(1) != "ADMINISTRATOR" || !reader.GetBoolean(2)) return null;
                id = reader.GetString(0);
            }
            else id = Guid.NewGuid().ToString();
        }
        // Lock the account before its recovery state, matching regular password changes.
        await using (var legacy = new SqlCommand("SELECT session_version FROM dbo.account_recovery WITH(UPDLOCK,HOLDLOCK) WHERE user_id=N'legacy-admin'",connection,transaction))
            if (await legacy.ExecuteScalarAsync(ct) is string version && version.Length > 0) return null;
        var salt = RandomNumberGenerator.GetBytes(16);
        await using (var save = new SqlCommand("""
            UPDATE dbo.admin_users SET password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=0,updated_at=SYSUTCDATETIME() WHERE id=@id;
            IF @@ROWCOUNT=0 INSERT dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password)
                VALUES(@id,@email,N'Administrador principal','ADMINISTRATOR',@salt,@hash,210000,1,0);
            """,connection,transaction))
        {
            save.Parameters.AddWithValue("@id",id); save.Parameters.AddWithValue("@email",configuredEmail);
            save.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;
            save.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=PasswordHash(newPassword,salt,210000);
            await save.ExecuteNonQueryAsync(ct);
        }
        await RotatePasswordSessions(connection,transaction,id,ct);
        // Invalidates every old bootstrap cookie and permanently disables its login fallback.
        await DisableLegacyPasswordAccess(connection,transaction,ct);
        await transaction.CommitAsync(ct);
        return await Account(Guid.Parse(id),ct);
    }
}
