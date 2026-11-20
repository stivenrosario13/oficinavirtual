using Microsoft.Data.SqlClient;
using System.Text.Json;

static class EnedraineCallCenterProfile
{
    public const string Email="enedraine.elisabeth@grupotejeda.local";
    public static bool IsTarget(string email)=>email.Trim().Equals(Email,StringComparison.OrdinalIgnoreCase)||email.Trim().Equals("enedraine.elisabeth",StringComparison.OrdinalIgnoreCase);
    public static Dictionary<string,bool> DefaultPermissions()
    {
        var result=new Dictionary<string,bool>();
        foreach(var key in new[]{"canManage","canManageUsers","canManageDepartment","canViewFieldAudits","canManageAudits","canManageAgencies","canViewDashboard","canAudit","canExportAudits","canViewAllSupportDepartments","canConfigureSupport","canModerateSupportChat","canDeleteSupportConversations","canDeleteSupportTickets","canCancelFindingWithoutEvidence","canCancelFindingWithoutReason","canAuthorizeWarehouseRequests","canLoadWarehouseInventory"})result[key]=false;
        foreach(var key in new[]{"canView","canConfigureProfile","canCreateTickets","canAssignTickets","canResolveTickets","canViewSupportDashboard","canViewSupportFindings","canCancelFindingWithEvidence","canCancelFindingWithReason","canUseSupportChat","canManageSupportChat","canShareSupportConversations","canViewTicketNotifications"})result[key]=true;
        result[ExplicitUserPermissions.Marker]=true;result["profile:SUPPORT"]=true;return result;
    }
}

sealed partial class Database
{
    static bool namedCallCenterCorrectionReady;
    async Task AlignEnedraineWithoutSupportReference(List<AdminUserRow> users,Func<AdminUserRow,AdminUserRow> effective,CancellationToken ct)
    {
        if(namedCallCenterCorrectionReady)return;
        var targets=users.Where(u=>EnedraineCallCenterProfile.IsTarget(u.Email)).ToList();
        if(targets.Count!=1)return;
        // Prefer the department's saved Support preset. Otherwise use the same
        // normal Call Center capabilities, explicitly excluding privileged menus.
        var preset=(await UserAccessPresets(ct)).SingleOrDefault(p=>p.Department=="CALL_CENTER"&&p.Profile=="SUPPORT");
        var selected=preset?.Permissions??EnedraineCallCenterProfile.DefaultPermissions();
        var permissions=SupportProfilePolicy.ForSave(selected,null);
        var reference=effective(targets[0] with{Role="TECHNOLOGY",SupportTeam="CALL_CENTER",Permissions=permissions}).EffectivePermissions!;
        await using var connection=await Open(ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        try{
            await using(var guard=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='unified-support-profile',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @result<0 THROW 50001,'No se pudo bloquear la actualización de perfiles.',1;",connection,tx))await guard.ExecuteNonQueryAsync(ct);
            await AlignEnedraineCallCenter(connection,tx,reference,ct);
            await tx.CommitAsync(ct);
            namedCallCenterCorrectionReady=true;
        }catch{await tx.RollbackAsync(ct);throw;}
    }
    // One-time, named-account correction authorized for the Call Center profile.
    // Uses the same validated reference as the other Support accounts.
    internal static async Task<Guid?> AlignEnedraineCallCenter(SqlConnection connection,SqlTransaction transaction,Dictionary<string,bool> reference,CancellationToken ct)
    {
        var matches=new List<(Guid Id,string Role,string? Team,string? Raw)>();
        await using(var query=new SqlCommand("SELECT id,role,support_team,permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(email))) IN(@email,N'enedraine.elisabeth')",connection,transaction)){
            query.Parameters.AddWithValue("@email",EnedraineCallCenterProfile.Email);
            await using var reader=await query.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))matches.Add((Guid.Parse(reader.GetString(0)),reader.GetString(1),reader.IsDBNull(2)?null:reader.GetString(2),reader.IsDBNull(3)?null:reader.GetString(3)));
        }
        if(matches.Count==0)return null;
        if(matches.Count!=1)throw new InvalidOperationException("Hay más de una cuenta de Enedraine Elisabeth; no se modificó su perfil.");
        var user=matches[0];
        await using(var schema=new SqlCommand("""
            IF OBJECT_ID(N'dbo.call_center_profile_corrections',N'U') IS NULL
                CREATE TABLE dbo.call_center_profile_corrections(
                    migration_key varchar(80) NOT NULL PRIMARY KEY,user_id nvarchar(80) NOT NULL,
                    previous_role varchar(40) NOT NULL,previous_team varchar(40) NULL,
                    previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
            """,connection,transaction))await schema.ExecuteNonQueryAsync(ct);
        await using(var applied=new SqlCommand("SELECT COUNT(*) FROM dbo.call_center_profile_corrections WHERE migration_key='V439_ENEDRAINE_CALL_CENTER'",connection,transaction))
            if(Convert.ToInt32(await applied.ExecuteScalarAsync(ct))>0)return null;
        // Do not silently discard malformed permission data.
        var original=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.Raw??"{}")??[];
        var permissions=CallCenterSupportProfile.Normalize(original);
        await using var update=new SqlCommand("""
            INSERT dbo.call_center_profile_corrections(migration_key,user_id,previous_role,previous_team,previous_permissions)
            VALUES('V439_ENEDRAINE_CALL_CENTER',@id,@role,@team,@previous);
            UPDATE dbo.admin_users SET role='TECHNOLOGY',support_team='CALL_CENTER',permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE id=@id;
            """,connection,transaction);
        update.Parameters.AddWithValue("@id",user.Id.ToString());update.Parameters.AddWithValue("@role",user.Role);
        update.Parameters.AddWithValue("@team",(object?)user.Team??DBNull.Value);update.Parameters.AddWithValue("@previous",(object?)user.Raw??DBNull.Value);
        update.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(permissions));await update.ExecuteNonQueryAsync(ct);
        return user.Id;
    }
}
