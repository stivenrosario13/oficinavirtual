using Microsoft.Data.SqlClient;
using System.Text.Json;

sealed partial class Database
{
    internal static async Task EnsureIncidentOperatorPermissions(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        try
        {
            await using(var schema=new SqlCommand("""
                DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='incident-operators-v513',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                IF @lock<0 THROW 50001,'No se pudieron restaurar los permisos de incidentes.',1;
                IF OBJECT_ID(N'dbo.incident_operator_permission_recovery',N'U') IS NULL
                    CREATE TABLE dbo.incident_operator_permission_recovery(user_id nvarchar(80) PRIMARY KEY,previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                """,connection,tx))await schema.ExecuteNonQueryAsync(ct);
            var users=new List<(string Id,string? Raw)>();
            await using(var query=new SqlCommand("""
                SELECT CONVERT(nvarchar(80),id),permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK)
                WHERE LOWER(LEFT(LTRIM(RTRIM(email)),CHARINDEX('@',LTRIM(RTRIM(email))+'@')-1)) IN('yesica.mercedes','yessica.mercedes','lucianny.gonzalez','lucianny.gonzales')
                   OR LOWER(LTRIM(RTRIM(display_name))) IN(N'yesica mercedes',N'yessica mercedes',N'lucianny gonzález',N'lucianny gonzalez',N'lucianny gonzales');
                """,connection,tx))
            await using(var reader=await query.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))users.Add((reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1)));
            foreach(var user in users)
            {
                Dictionary<string,bool> saved;
                try{saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.Raw??"{}")??[];}catch(JsonException){saved=[];}
                foreach(var key in new[]{"canViewDocuments","canReceiveDocuments","canSignDocuments","canDecideDocuments"})saved[key]=true;
                await using var update=new SqlCommand("""
                    IF NOT EXISTS(SELECT 1 FROM dbo.incident_operator_permission_recovery WHERE user_id=@id)
                        INSERT dbo.incident_operator_permission_recovery(user_id,previous_permissions) VALUES(@id,@previous);
                    UPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;
                    """,connection,tx);
                update.Parameters.AddWithValue("@id",user.Id);update.Parameters.AddWithValue("@previous",(object?)user.Raw??DBNull.Value);update.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(saved));await update.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        catch{await tx.RollbackAsync(ct);throw;}
    }
}
