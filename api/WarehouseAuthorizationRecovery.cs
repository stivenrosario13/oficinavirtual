using Microsoft.Data.SqlClient;
using System.Text.Json;

sealed partial class Database
{
    // Restore only the two requested operators, once; retain all other settings.
    internal static async Task RestoreWarehouseAuthorizers(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        try{
            await using(var schema=new SqlCommand("""
                DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='warehouse-authorizers-v441',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                IF @lock<0 THROW 50001,'No se pudo restaurar la autorización de Almacén.',1;
                IF OBJECT_ID(N'dbo.warehouse_authorization_recovery',N'U') IS NULL
                    CREATE TABLE dbo.warehouse_authorization_recovery(user_id nvarchar(80) PRIMARY KEY,previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                """,connection,tx))await schema.ExecuteNonQueryAsync(ct);
            var users=new List<(string Id,string? Raw)>();
            await using(var query=new SqlCommand("""
                SELECT CONVERT(nvarchar(80),id),permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK)
                WHERE (role='WAREHOUSE' OR (role='TECHNOLOGY' AND support_team='WAREHOUSE'))
                  AND LOWER(LTRIM(RTRIM(email))) IN('ana.rodriguez','ana.rodriguez@grupotejeda.local','diorly.abreu','diorly.abreu@grupotejeda.local')
                  AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_authorization_recovery r WHERE r.user_id=CONVERT(nvarchar(80),admin_users.id));
                """,connection,tx))
            await using(var reader=await query.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))users.Add((reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1)));
            foreach(var user in users){
                var saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.Raw??"{}")??[];
                // Request-only accounts must never acquire operational privileges.
                if(saved.GetValueOrDefault("profile:WAREHOUSE_REQUESTER"))continue;
                saved["canAuthorizeWarehouseRequests"]=true;saved["canDecideDocuments"]=true;
                await using var update=new SqlCommand("""
                    INSERT dbo.warehouse_authorization_recovery(user_id,previous_permissions) VALUES(@id,@previous);
                    UPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;
                    """,connection,tx);
                update.Parameters.AddWithValue("@id",user.Id);update.Parameters.AddWithValue("@previous",(object?)user.Raw??DBNull.Value);
                update.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(saved));await update.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }catch{await tx.RollbackAsync(ct);throw;}
    }
}
