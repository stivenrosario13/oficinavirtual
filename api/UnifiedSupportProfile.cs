using Microsoft.Data.SqlClient;
using System.Text.Json;

static class SupportProfilePolicy
{
    public static bool IsSupport(string role,string? team,Dictionary<string,bool>? permissions)
    {
        if(role is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES" or "DEPARTMENT" or "WAREHOUSE"))return false;
        // Warehouse and workshop capabilities are separate from ticket support,
        // even when an older account has the generic SUPPORT marker.
        if(role=="WAREHOUSE"||team is "WAREHOUSE" or "WORKSHOP")return false;
        if(permissions?.GetValueOrDefault("profile:SUPPORT")==true)return true;
        if(permissions?.Any(p=>p.Key.StartsWith("profile:")&&p.Value)==true)return false;
        if(role=="WAREHOUSE"||team is "WAREHOUSE" or "WORKSHOP" or "TECHNICIANS")return false;
        if(permissions?.GetValueOrDefault("canManageDepartment")==true)return false;
        // Legacy Call Center accounts were created before profile markers existed.
        // Their operational team identifies them as Support even if an old
        // permissions snapshot left canAssignTickets set to false.
        if(role=="TECHNOLOGY"&&team=="CALL_CENTER")return true;
        return permissions?.GetValueOrDefault("canAssignTickets")==true;
    }
    public static Dictionary<string,bool> Standard(Dictionary<string,bool>? original,Dictionary<string,bool> reference)
    {
        var result=new Dictionary<string,bool>(original??[]);
        foreach(var key in result.Keys.Where(k=>k.StartsWith("can")||k.StartsWith("profile:")).ToArray())result.Remove(key);
        foreach(var pair in reference.Where(p=>p.Key.StartsWith("can")))result[pair.Key]=pair.Value;
        result["profile:SUPPORT"]=true;result[ExplicitUserPermissions.Marker]=true;return result;
    }
    // The administrator's selected profile remains usable without a named account.
    // If the shared reference exists, keep the established shared-profile behavior.
    public static Dictionary<string,bool> ForSave(Dictionary<string,bool>? selected,Dictionary<string,bool>? reference)
    {
        var result=new Dictionary<string,bool>(selected??reference??[]);
        result["profile:SUPPORT"]=true;
        result[ExplicitUserPermissions.Marker]=true;
        result["usesCustomizedPermissions"]=true;
        return result;
    }
}

sealed partial class Database
{
    static readonly SemaphoreSlim SupportStandardLock=new(1,1);
    static Dictionary<string,bool>? supportStandard;
    public static Dictionary<string,bool>? SupportStandard=>supportStandard is null?null:new(supportStandard);
    public static void InvalidateSupportStandard()=>supportStandard=null;
    public async Task EnsureUnifiedSupportProfile(Func<AdminUserRow,AdminUserRow> effective,CancellationToken ct)
    {
        if(supportStandard is not null)return;
        await SupportStandardLock.WaitAsync(ct);
        try{
            if(supportStandard is not null)return;
            var users=await AdminUsers(ct);
            var sources=users.Where(u=>u.Role=="GENERAL_SERVICES"&&new[]{"ross.mariel","ross.mariel@grupotejeda.local"}.Contains(u.Email.Trim().ToLowerInvariant())).ToList();
            if(sources.Count!=1){
                await AlignEnedraineWithoutSupportReference(users,effective,ct);
                return; // Do not rewrite other existing accounts without a unique reference.
            }
            var source=sources[0];var reference=effective(source).EffectivePermissions!;
            await using var connection=await Open(ct);await EnsureUserAccessPresetSchema(connection,ct);
            await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            try{
                await using(var guard=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='unified-support-profile',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @result<0 THROW 50001,'No se pudo bloquear la actualización de perfiles.',1;",connection,transaction))await guard.ExecuteNonQueryAsync(ct);
                await using(var readSource=new SqlCommand("SELECT permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",connection,transaction)){
                    readSource.Parameters.AddWithValue("@id",source.Id.ToString());var current=ReadPermissions(await readSource.ExecuteScalarAsync(ct) as string);
                    if(current.Count!=(source.Permissions?.Count??0)||current.Any(p=>source.Permissions?.GetValueOrDefault(p.Key)!=p.Value))throw new InvalidOperationException("Los permisos de Ross Mariel cambiaron; vuelve a intentarlo.");
                }
                const string schema="""
                    IF OBJECT_ID(N'dbo.support_permission_backups',N'U') IS NULL
                    CREATE TABLE dbo.support_permission_backups(id uniqueidentifier PRIMARY KEY,user_id nvarchar(80) NOT NULL,permissions_json nvarchar(max) NULL,reference_user_id nvarchar(80) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                    """;
                await using(var command=new SqlCommand(schema,connection,transaction))await command.ExecuteNonQueryAsync(ct);
                var alignedCallCenterUser=await AlignEnedraineCallCenter(connection,transaction,reference,ct);
                foreach(var user in users.Where(u=>u.Id!=source.Id&&u.Id!=alignedCallCenterUser&&SupportProfilePolicy.IsSupport(u.Role,u.SupportTeam,u.Permissions))){
                    if(user.Permissions?.GetValueOrDefault("usesCustomizedPermissions")==true)continue;
                    var updated=CallCenterSupportProfile.Applies(user.Role,user.SupportTeam,user.Permissions)?CallCenterSupportProfile.Normalize(user.Permissions):SupportProfilePolicy.Standard(user.Permissions,reference);
                    if(user.Permissions is not null&&updated.Count==user.Permissions.Count&&updated.All(p=>user.Permissions.TryGetValue(p.Key,out var value)&&value==p.Value))continue;
                    const string sql="""
                        DECLARE @previous nvarchar(max)=(SELECT permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id);
                        IF ISNULL(@previous,N'')<>ISNULL(@expected,N'') THROW 50001,'Los permisos cambiaron durante la actualización; vuelve a intentarlo.',1;
                        INSERT dbo.support_permission_backups(id,user_id,permissions_json,reference_user_id) VALUES(NEWID(),@id,@previous,@source);
                        UPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE id=@id;
                        """;
                    // ReadPermissions/WritePermissions can reorder keys; compare the current parsed map below instead.
                    await using var read=new SqlCommand("SELECT permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",connection,transaction);read.Parameters.AddWithValue("@id",user.Id.ToString());
                    var raw=await read.ExecuteScalarAsync(ct) as string;
                    var current=ReadPermissions(raw)??[];
                    if(current.Count!=(user.Permissions?.Count??0)||current.Any(p=>user.Permissions?.GetValueOrDefault(p.Key)!=p.Value))throw new InvalidOperationException("Los permisos cambiaron durante la actualización; vuelve a intentarlo.");
                    await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@id",user.Id.ToString());command.Parameters.AddWithValue("@source",source.Id.ToString());command.Parameters.AddWithValue("@expected",(object?)raw??DBNull.Value);command.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(updated));await command.ExecuteNonQueryAsync(ct);
                }
                foreach(var department in DepartmentAccounts.Names.Keys.Where(code=>code is not ("WAREHOUSE" or "WORKSHOP"))){
                    const string sql="""
                        UPDATE dbo.user_access_presets SET permissions_json=@permissions,updated_by=@source,updated_at=SYSUTCDATETIME() WHERE department=@department AND profile='SUPPORT';
                        IF @@ROWCOUNT=0 INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by,updated_at) VALUES(@department,'SUPPORT',@permissions,@source,SYSUTCDATETIME());
                        """;
                    await using var command=new SqlCommand(sql,connection,transaction);command.Parameters.AddWithValue("@department",department);command.Parameters.AddWithValue("@source",source.Id.ToString());command.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(department=="CALL_CENTER"?CallCenterSupportProfile.Permissions():reference.Where(p=>p.Key.StartsWith("can")).ToDictionary(p=>p.Key,p=>p.Value)));await command.ExecuteNonQueryAsync(ct);
                }
                await transaction.CommitAsync(ct);supportStandard=new(reference);
            }catch{await transaction.RollbackAsync(ct);throw;}
        }finally{SupportStandardLock.Release();}
    }
}
