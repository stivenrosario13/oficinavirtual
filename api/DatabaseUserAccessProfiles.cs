using Microsoft.Data.SqlClient;
using System.Text.Json;

record UserAccessPreset(string Department,string Profile,Dictionary<string,bool> Permissions);

sealed partial class Database
{
    static readonly SemaphoreSlim UserAccessPresetSchemaLock=new(1,1);
    static bool userAccessPresetSchemaReady;
    static async Task EnsureUserAccessPresetSchema(SqlConnection connection,CancellationToken ct)
    {
        if(userAccessPresetSchemaReady)return;
        await UserAccessPresetSchemaLock.WaitAsync(ct);
        try
        {
            if(userAccessPresetSchemaReady)return;
            const string sql="""
                IF OBJECT_ID(N'dbo.user_access_presets',N'U') IS NULL
                CREATE TABLE dbo.user_access_presets(
                    department nvarchar(40) NOT NULL,
                    profile nvarchar(40) NOT NULL,
                    permissions_json nvarchar(max) NOT NULL,
                    updated_by nvarchar(100) NOT NULL,
                    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_user_access_presets PRIMARY KEY(department,profile));
                """;
            await using var command=new SqlCommand(sql,connection);
            await command.ExecuteNonQueryAsync(ct);userAccessPresetSchemaReady=true;
        }
        finally{UserAccessPresetSchemaLock.Release();}
    }
    public async Task<List<UserAccessPreset>> UserAccessPresets(CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureUserAccessPresetSchema(connection,ct);
        await using var command=new SqlCommand("SELECT department,profile,permissions_json FROM dbo.user_access_presets ORDER BY department,profile",connection);
        await using var reader=await command.ExecuteReaderAsync(ct);var result=new List<UserAccessPreset>();
        while(await reader.ReadAsync(ct))result.Add(new(reader.GetString(0),reader.GetString(1),JsonSerializer.Deserialize<Dictionary<string,bool>>(reader.GetString(2))??[]));
        return result;
    }
    public async Task SaveUserAccessPreset(UserAccessPreset preset,string actor,CancellationToken ct)
    {
        await using var connection=await Open(ct);await EnsureUserAccessPresetSchema(connection,ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        const string sql="""
            UPDATE dbo.user_access_presets WITH(UPDLOCK,HOLDLOCK)
            SET permissions_json=@permissions,updated_by=@actor,updated_at=SYSUTCDATETIME()
            WHERE department=@department AND profile=@profile;
            IF @@ROWCOUNT=0 INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by)
            VALUES(@department,@profile,@permissions,@actor);
            """;
        await using var command=new SqlCommand(sql,connection,transaction);
        command.Parameters.AddWithValue("@department",preset.Department);command.Parameters.AddWithValue("@profile",preset.Profile);
        command.Parameters.AddWithValue("@permissions",JsonSerializer.Serialize(preset.Permissions));command.Parameters.AddWithValue("@actor",actor);
        await command.ExecuteNonQueryAsync(ct);await transaction.CommitAsync(ct);
    }
}
