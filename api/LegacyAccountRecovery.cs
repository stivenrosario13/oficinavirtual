using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

partial class Database
{
    static async Task EnsureLegacyAccountRecovery(SqlConnection connection, CancellationToken ct)
    {
        var adminSalt = RandomNumberGenerator.GetBytes(16);
        var adminHash = PasswordHash("123456", adminSalt, 210000);
        var waskarSalt = RandomNumberGenerator.GetBytes(16);
        var waskarHash = PasswordHash("123456", waskarSalt, 210000);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = new SqlCommand("""
                DECLARE @lock int;
                EXEC @lock=sys.sp_getapplock @Resource='V429_ACCOUNT_ACCESS_RECOVERY',@LockMode='Exclusive',@LockOwner='Transaction';
                IF @lock<0 THROW 51028,'No se pudo preparar la recuperación de acceso.',1;

                IF OBJECT_ID(N'dbo.account_access_recovery_migrations',N'U') IS NULL
                    CREATE TABLE dbo.account_access_recovery_migrations
                    (
                        migration_key varchar(80) NOT NULL PRIMARY KEY,
                        account_email nvarchar(320) NOT NULL,
                        applied_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
                    );

                -- Preserve the principal account if it exists; create it only when absent.
                IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE LOWER(email)=N'admin@local.com')
                    INSERT INTO dbo.admin_users
                      (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password,permissions_json,support_team)
                    VALUES(CONVERT(char(36),NEWID()),N'admin@local.com',N'Administrador de recuperación','ADMINISTRATOR',
                           @adminSalt,@adminHash,210000,1,1,NULL,NULL);

                -- Reset only this named account once. Its identity, role, permissions,
                -- groups, and profile stay intact; later password changes are preserved.
                IF NOT EXISTS(SELECT 1 FROM dbo.account_access_recovery_migrations WHERE migration_key='V429_ADMIN_LOCAL_TEMPORARY_PASSWORD')
                BEGIN
                    UPDATE dbo.admin_users
                       SET password_salt=@adminSalt,password_hash=@adminHash,password_iterations=210000,
                           must_change_password=1,updated_at=SYSUTCDATETIME()
                     WHERE LOWER(email)=N'admin@local.com' AND is_active=1;
                    IF @@ROWCOUNT=1
                        INSERT INTO dbo.account_access_recovery_migrations(migration_key,account_email)
                        VALUES('V429_ADMIN_LOCAL_TEMPORARY_PASSWORD',N'admin@local.com');
                END;

                -- Older supervisors who never logged in retain their stored passwords,
                -- while their first-access 123456 path is restored.
                IF NOT EXISTS(SELECT 1 FROM dbo.account_access_recovery_migrations WHERE migration_key='V428_LEGACY_FIRST_LOGIN_FLAGS')
                BEGIN
                    UPDATE dbo.admin_users
                       SET must_change_password=1,updated_at=SYSUTCDATETIME()
                     WHERE role='GROUP_ADMIN' AND is_active=1 AND last_login_at IS NULL AND must_change_password=0;
                    INSERT INTO dbo.account_access_recovery_migrations(migration_key,account_email)
                    VALUES('V428_LEGACY_FIRST_LOGIN_FLAGS',N'legacy-group-supervisors');
                END;

                -- The named active account is recovered exactly once. No role, groups,
                -- permissions, or other users' passwords are changed.
                IF NOT EXISTS(SELECT 1 FROM dbo.account_access_recovery_migrations WHERE migration_key='V428_WASKAR_TEMPORARY_PASSWORD')
                BEGIN
                    UPDATE dbo.admin_users
                       SET password_salt=@waskarSalt,password_hash=@waskarHash,password_iterations=210000,
                           must_change_password=1,updated_at=SYSUTCDATETIME()
                     WHERE LOWER(email)=N'waskar.sepulveda@grupotejeda.local' AND is_active=1;
                    IF @@ROWCOUNT=1
                        INSERT INTO dbo.account_access_recovery_migrations(migration_key,account_email)
                        VALUES('V428_WASKAR_TEMPORARY_PASSWORD',N'waskar.sepulveda@grupotejeda.local');
                END;
                """, connection, transaction);
            command.Parameters.AddWithValue("@adminSalt", adminSalt);
            command.Parameters.AddWithValue("@adminHash", adminHash);
            command.Parameters.AddWithValue("@waskarSalt", waskarSalt);
            command.Parameters.AddWithValue("@waskarHash", waskarHash);
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
