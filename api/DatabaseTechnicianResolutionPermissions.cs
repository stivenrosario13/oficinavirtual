using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

sealed partial class Database
{
    // Runs during the existing account-schema initialization. It changes only
    // the two capabilities needed to open and resolve assigned cases.
    internal static async Task EnsureTechnicianResolutionPermissions(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try{
            await using(var schema=new SqlCommand("""
                DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='technician-resolution-permissions-v438',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                IF @lock<0 THROW 50001,'No se pudo verificar los permisos técnicos.',1;
                IF OBJECT_ID(N'dbo.technician_resolution_permission_backups',N'U') IS NULL
                CREATE TABLE dbo.technician_resolution_permission_backups(id bigint IDENTITY PRIMARY KEY,user_id nvarchar(80) NOT NULL,permissions_json nvarchar(max) NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                """,connection,tx))await schema.ExecuteNonQueryAsync(ct);
            var users=new List<(string Id,string Role,string? Team,string? Raw)>();
            await using(var query=new SqlCommand("SELECT CONVERT(nvarchar(80),id),role,support_team,permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE role IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','DEPARTMENT')",connection,tx))
            await using(var reader=await query.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))users.Add((reader.GetString(0),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3)));
            foreach(var user in users){
                Dictionary<string,bool> permissions;
                try{permissions=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.Raw??"{}")??[];}
                catch(JsonException){continue;} // Never replace an unrecognized permissions document.
                if(!TechnicianResolutionPermissions.IsTechnician(user.Role,user.Team,permissions)||permissions.GetValueOrDefault("canResolveTickets")&&permissions.GetValueOrDefault("canViewSupportDashboard"))continue;
                permissions["canResolveTickets"]=true;permissions["canViewSupportDashboard"]=true;
                await using var update=new SqlCommand("""
                    INSERT dbo.technician_resolution_permission_backups(user_id,permissions_json) VALUES(@id,@previous);
                    UPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;
                    """,connection,tx);
                update.Parameters.AddWithValue("@id",user.Id);update.Parameters.AddWithValue("@previous",(object?)user.Raw??DBNull.Value);update.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(permissions));await update.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }catch{await tx.RollbackAsync(ct);throw;}
    }
}
