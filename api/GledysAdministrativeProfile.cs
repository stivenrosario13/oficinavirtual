using Microsoft.Data.SqlClient;
using System.Text.Json;

partial class Database
{
    // Apply once to the existing account. Preserve credentials, activation and
    // every unrelated permission; keep the previous profile for traceability.
    internal static async Task EnsureGledysAdministrativeProfile(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(ct);
        await using(var schema=new SqlCommand("""
            DECLARE @lock int;
            EXEC @lock=sys.sp_getapplock @Resource='GLEDYS_ADMINISTRATIVE_20261020',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
            IF @lock<0 THROW 50001,'No se pudo preparar el perfil administrativo.',1;
            IF OBJECT_ID(N'dbo.administrative_profile_updates',N'U') IS NULL
              CREATE TABLE dbo.administrative_profile_updates(update_key varchar(100) PRIMARY KEY,user_id nvarchar(80) NOT NULL,previous_role varchar(40),previous_team varchar(40),previous_permissions nvarchar(max),applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
            """,connection,tx))await schema.ExecuteNonQueryAsync(ct);
        await using var read=new SqlCommand("""
            SELECT CONVERT(nvarchar(80),id),role,support_team,permissions_json
            FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK)
            WHERE LOWER(LTRIM(RTRIM(email))) IN('gledys.tejeda','gledys.tejeda@grupotejeda.local')
              AND NOT EXISTS(SELECT 1 FROM dbo.administrative_profile_updates WHERE update_key='GLEDYS_ADMINISTRATIVE_20261020');
            """,connection,tx);
        string id,role;string? team,previous;
        await using(var reader=await read.ExecuteReaderAsync(ct)){
            if(!await reader.ReadAsync(ct)){await reader.DisposeAsync();await tx.CommitAsync(ct);return;}
            id=reader.GetString(0);role=reader.GetString(1);team=reader.IsDBNull(2)?null:reader.GetString(2);previous=reader.IsDBNull(3)?null:reader.GetString(3);
            if(await reader.ReadAsync(ct))throw new InvalidOperationException("Hay más de una cuenta de gledys.tejeda; no se modificó ningún perfil.");
        }
        var permissions=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(previous??"{}")??[];
        // Department management must not implicitly add unrelated dashboard or field-audit views.
        foreach(var key in new[]{"canViewDashboard","canViewFieldAudits","canViewAllSupportDepartments"})
            permissions.TryAdd(key,JsonSerializer.SerializeToElement(false));
        foreach(var key in new[]{"canManageDepartment","canCreateTickets","canAssignTickets","canResolveTickets","canViewSupportDashboard","canViewTicketNotifications","canViewSupportFindings","canUseSupportChat","canShareSupportConversations","canViewDocuments","canUploadDocuments","canManageDocumentFolders","canShareDocuments","canDeleteDocuments","canReceiveDocuments","canSignDocuments","canDecideDocuments","canManageDocumentTemplates","canManageAdministrativeAssets","canRequestWarehouse"})
            permissions[key]=JsonSerializer.SerializeToElement(true);
        await using var save=new SqlCommand("""
            INSERT dbo.administrative_profile_updates(update_key,user_id,previous_role,previous_team,previous_permissions)
            VALUES('GLEDYS_ADMINISTRATIVE_20261020',@id,@role,@team,@previous);
            UPDATE dbo.admin_users SET role='ADMINISTRATIVE',support_team=NULL,permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;
            """,connection,tx);
        save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@role",role);save.Parameters.AddWithValue("@team",(object?)team??DBNull.Value);save.Parameters.AddWithValue("@previous",(object?)previous??DBNull.Value);save.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(permissions));
        await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
}
