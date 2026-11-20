using System.Text.Json;
using Microsoft.Data.SqlClient;

static class CallCenterSupportProfile
{
    public static Dictionary<string,bool> Permissions(){using var stream=typeof(CallCenterSupportProfile).Assembly.GetManifestResourceStream("CallCenterSupportProfile.json")!;return JsonSerializer.Deserialize<Dictionary<string,bool>>(stream)!;}
    public static bool Applies(string role,string? team,IReadOnlyDictionary<string,bool>? saved)
    {
        if(!role.Equals("TECHNOLOGY",StringComparison.OrdinalIgnoreCase)||team!="CALL_CENTER")return false;
        if(saved?.GetValueOrDefault("usesCustomizedPermissions")==true)return false;
        if(saved?.GetValueOrDefault("profile:SUPPORT")==true)return true;
        return saved?.Any(p=>p.Key.StartsWith("profile:")&&p.Value)!=true&&saved?.GetValueOrDefault("canManageDepartment")!=true;
    }
    public static Dictionary<string,bool> Normalize(Dictionary<string,bool>? original)=>SupportProfilePolicy.Standard(original,Permissions());
    public static void Apply(Dictionary<string,bool> effective,IReadOnlyDictionary<string,bool>? saved,string role,string? team)
    {
        if(!Applies(role,team,saved))return;
        foreach(var pair in Permissions())effective[pair.Key]=pair.Value;
    }
}

sealed partial class Database
{
    internal static async Task EnsureCallCenterSupportProfile(SqlConnection connection,CancellationToken ct)
    {
        await EnsureUserAccessPresetSchema(connection,ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        try{
            await using(var schema=new SqlCommand("""
                DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='call-center-support-v442',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                IF @lock<0 THROW 50001,'No se pudo preparar el perfil Call Center.',1;
                IF OBJECT_ID(N'dbo.call_center_support_backups',N'U') IS NULL CREATE TABLE dbo.call_center_support_backups(id bigint IDENTITY PRIMARY KEY,user_id nvarchar(80) NOT NULL,permissions_json nvarchar(max) NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                IF OBJECT_ID(N'dbo.call_center_preset_backups',N'U') IS NULL CREATE TABLE dbo.call_center_preset_backups(id bigint IDENTITY PRIMARY KEY,permissions_json nvarchar(max) NOT NULL,updated_by nvarchar(100) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
                """,connection,tx))await schema.ExecuteNonQueryAsync(ct);
            var users=new List<(string Id,string? Raw)>();
            await using(var query=new SqlCommand("SELECT CONVERT(nvarchar(80),id),permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE role='TECHNOLOGY' AND support_team='CALL_CENTER'",connection,tx))
            await using(var reader=await query.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))users.Add((reader.GetString(0),reader.IsDBNull(1)?null:reader.GetString(1)));
            foreach(var user in users){
                var original=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.Raw??"{}")??[];
                if(original.GetValueOrDefault("usesCustomizedPermissions"))continue;
                if(!CallCenterSupportProfile.Applies("TECHNOLOGY","CALL_CENTER",original))continue;
                var updated=CallCenterSupportProfile.Normalize(original);
                if(original.Count==updated.Count&&updated.All(p=>original.TryGetValue(p.Key,out var value)&&value==p.Value))continue;
                await using var save=new SqlCommand("INSERT dbo.call_center_support_backups(user_id,permissions_json) VALUES(@id,@previous); UPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id",connection,tx);
                save.Parameters.AddWithValue("@id",user.Id);save.Parameters.AddWithValue("@previous",(object?)user.Raw??DBNull.Value);save.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(updated));await save.ExecuteNonQueryAsync(ct);
            }
            await using(var preset=new SqlCommand("""
                DECLARE @previous nvarchar(max)=(SELECT permissions_json FROM dbo.user_access_presets WITH(UPDLOCK,HOLDLOCK) WHERE department='CALL_CENTER' AND profile='SUPPORT');
                IF @previous IS NOT NULL AND @previous<>@permissions
                    INSERT dbo.call_center_preset_backups(permissions_json,updated_by) SELECT permissions_json,updated_by FROM dbo.user_access_presets WHERE department='CALL_CENTER' AND profile='SUPPORT';
                IF @previous IS NULL INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by) VALUES('CALL_CENTER','SUPPORT',@permissions,'system:call-center-v442');
                ELSE IF @previous<>@permissions UPDATE dbo.user_access_presets SET permissions_json=@permissions,updated_by='system:call-center-v442',updated_at=SYSUTCDATETIME() WHERE department='CALL_CENTER' AND profile='SUPPORT';
                """,connection,tx)){preset.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(CallCenterSupportProfile.Permissions()));await preset.ExecuteNonQueryAsync(ct);}
            await tx.CommitAsync(ct);
        }catch{await tx.RollbackAsync(ct);throw;}
    }
}
