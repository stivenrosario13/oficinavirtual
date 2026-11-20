using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

partial class Database
{
    static async Task EnsureAdministrativeProfiles(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using(var schema=new SqlCommand("""
          DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='V418_ADMINISTRATIVE_PROFILES_20261002',@LockMode='Exclusive',@LockOwner='Transaction';
          IF @lock<0 THROW 50001,'No se pudo preparar Administración.',1;
          IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.admin_users') AND name='CK_admin_users_role' AND definition NOT LIKE '%ADMINISTRATIVE%')
          BEGIN
            ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;
            ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role CHECK(role IN('ADMINISTRATOR','ADMINISTRATIVE','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE'));
          END;
          """,connection,tx))await schema.ExecuteNonQueryAsync(ct);
        var accounts=new[]{("emma.mendoza","Emma Mercedes Mendoza Reynoso"),("derihan.alisha","Derihan Alisha Echavarria Rivas"),("whilmaly.sulay","Whilmaly Sulay Lora Noesi"),("sandra.ramirez","Sandra Lucia Ramirez Ortiz"),("emely.figueroa","Emely Figueroa"),("diana.ventura","Diana Marte Ventura")};
        foreach(var (login,name) in accounts){
            // Never reset a password, reactivate an account, or replace existing permissions.
            var salt=RandomNumberGenerator.GetBytes(16);
            await using var command=new SqlCommand("""
              IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE LOWER(REPLACE(email,'@grupotejeda.local',''))=@login)
                INSERT dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)
                VALUES(CONVERT(char(36),NEWID()),@email,@name,'ADMINISTRATIVE',@salt,@hash,210000,1,N'Administración',1,N'{}',NULL);
              """,connection,tx);
            command.Parameters.AddWithValue("@login",login);command.Parameters.AddWithValue("@email",login+"@grupotejeda.local");command.Parameters.AddWithValue("@name",name);command.Parameters.AddWithValue("@salt",salt);command.Parameters.AddWithValue("@hash",PasswordHash("123456",salt,210000));await command.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
