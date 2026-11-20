using System.Collections.Concurrent;
using System.Text;
using Microsoft.Data.SqlClient;

internal static class OperationalActivityThrottle
{
    private static readonly ConcurrentDictionary<string,long> LastWrites=new();
    public static bool ShouldWrite(string sessionId)
    {
        if(string.IsNullOrWhiteSpace(sessionId))return false;
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while(true)
        {
            if(!LastWrites.TryGetValue(sessionId,out var previous))return LastWrites.TryAdd(sessionId,now);
            if(now-previous<60)return false;
            if(LastWrites.TryUpdate(sessionId,now,previous))return true;
        }
    }
}

internal sealed partial class Database
{
    private static readonly SemaphoreSlim OperationalSessionSchemaLock=new(1,1);
    private static bool operationalSessionSchemaReady;
    private static async Task EnsureOperationalSessionSchema(SqlConnection connection,CancellationToken ct)
    {
        if(operationalSessionSchemaReady)return;
        await OperationalSessionSchemaLock.WaitAsync(ct);
        try
        {
            if(operationalSessionSchemaReady)return;
            await using var command=new SqlCommand("""
IF OBJECT_ID(N'dbo.operational_user_sessions',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.operational_user_sessions(id uniqueidentifier NOT NULL PRIMARY KEY,user_id nvarchar(80) NOT NULL,user_email nvarchar(256) NOT NULL,display_name nvarchar(160) NOT NULL,role varchar(40) NOT NULL,department varchar(40) NULL,login_at datetime2(3) NOT NULL,last_activity_at datetime2(3) NOT NULL,logout_at datetime2(3) NULL,last_path nvarchar(240) NULL);
 CREATE INDEX IX_operational_sessions_user ON dbo.operational_user_sessions(user_id,login_at DESC) INCLUDE(last_activity_at,logout_at,last_path);
 CREATE INDEX IX_operational_sessions_activity ON dbo.operational_user_sessions(last_activity_at DESC);
END;
IF COL_LENGTH(N'dbo.operational_user_sessions',N'last_heartbeat_at') IS NULL ALTER TABLE dbo.operational_user_sessions ADD last_heartbeat_at datetime2(3) NULL;
IF COL_LENGTH(N'dbo.operational_user_sessions',N'last_interaction_at') IS NULL ALTER TABLE dbo.operational_user_sessions ADD last_interaction_at datetime2(3) NULL;
IF OBJECT_ID(N'dbo.operational_user_connections',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.operational_user_connections(id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,session_id uniqueidentifier NOT NULL,user_id nvarchar(80) NOT NULL,connected_at datetime2(3) NOT NULL,last_seen_at datetime2(3) NOT NULL);
 CREATE INDEX IX_operational_connections_user ON dbo.operational_user_connections(user_id,connected_at DESC) INCLUDE(last_seen_at,session_id);
 CREATE INDEX IX_operational_connections_session ON dbo.operational_user_connections(session_id,last_seen_at DESC);
END;
""",connection);
            await command.ExecuteNonQueryAsync(ct); operationalSessionSchemaReady=true;
        }
        finally{OperationalSessionSchemaLock.Release();}
    }

    public async Task<string> BeginOperationalSession(AdminIdentity account,CancellationToken ct)
    {
        var id=Guid.NewGuid(); await using var connection=await Open(ct); await EnsureOperationalSessionSchema(connection,ct);
        await using var command=new SqlCommand("""
INSERT dbo.operational_user_sessions(id,user_id,user_email,display_name,role,department,login_at,last_activity_at,last_heartbeat_at,last_interaction_at,last_path)
VALUES(@id,@user,@email,@name,@role,@department,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),N'/inicio-sesion');
INSERT dbo.operational_user_connections(session_id,user_id,connected_at,last_seen_at) VALUES(@id,@user,SYSUTCDATETIME(),SYSUTCDATETIME());
""",connection);
        command.Parameters.AddWithValue("@id",id); command.Parameters.AddWithValue("@user",account.Id); command.Parameters.AddWithValue("@email",account.Email); command.Parameters.AddWithValue("@name",account.DisplayName); command.Parameters.AddWithValue("@role",account.Role); command.Parameters.AddWithValue("@department",(object?)OperationalDepartment(account.Role,account.SupportTeam)??DBNull.Value);
        await command.ExecuteNonQueryAsync(ct); return id.ToString("D");
    }

    public async Task TouchOperationalSession(string traceId,string path,CancellationToken ct)
    {
        if(!Guid.TryParse(traceId,out var id))return; await using var connection=await Open(ct); await EnsureOperationalSessionSchema(connection,ct);
        await using var command=new SqlCommand("UPDATE dbo.operational_user_sessions SET last_activity_at=SYSUTCDATETIME(),last_path=LEFT(@path,240) WHERE id=@id AND logout_at IS NULL;",connection);
        command.Parameters.AddWithValue("@id",id); command.Parameters.AddWithValue("@path",path??""); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task EndOperationalSession(string? traceId,CancellationToken ct)
    {
        if(!Guid.TryParse(traceId,out var id))return; await using var connection=await Open(ct); await EnsureOperationalSessionSchema(connection,ct);
        await using var command=new SqlCommand("""
UPDATE dbo.operational_user_sessions SET last_activity_at=SYSUTCDATETIME(),logout_at=SYSUTCDATETIME(),last_path=N'/cerrar-sesion' WHERE id=@id AND logout_at IS NULL;
UPDATE dbo.operational_user_connections SET last_seen_at=SYSUTCDATETIME() WHERE id=(SELECT TOP(1) id FROM dbo.operational_user_connections WHERE session_id=@id ORDER BY last_seen_at DESC,id DESC);
""",connection); command.Parameters.AddWithValue("@id",id); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task PulseOperationalSession(string? traceId,bool active,CancellationToken ct)
    {
        if(!Guid.TryParse(traceId,out var id))return; await using var connection=await Open(ct); await EnsureOperationalSessionSchema(connection,ct);
        await using var command=new SqlCommand("""
UPDATE dbo.operational_user_sessions SET last_heartbeat_at=SYSUTCDATETIME(),last_interaction_at=CASE WHEN @active=1 THEN SYSUTCDATETIME() ELSE last_interaction_at END,last_path=CASE WHEN @active=1 THEN N'/presencia/activo' ELSE N'/presencia/inactivo' END WHERE id=@id AND logout_at IS NULL;
DECLARE @user nvarchar(80)=(SELECT user_id FROM dbo.operational_user_sessions WHERE id=@id AND logout_at IS NULL);
DECLARE @connection bigint=(SELECT TOP(1) id FROM dbo.operational_user_connections WHERE session_id=@id AND last_seen_at>=DATEADD(second,-65,SYSUTCDATETIME()) ORDER BY last_seen_at DESC,id DESC);
IF @user IS NOT NULL BEGIN IF @connection IS NULL INSERT dbo.operational_user_connections(session_id,user_id,connected_at,last_seen_at) VALUES(@id,@user,SYSUTCDATETIME(),SYSUTCDATETIME()); ELSE UPDATE dbo.operational_user_connections SET last_seen_at=SYSUTCDATETIME() WHERE id=@connection; END;
""",connection); command.Parameters.AddWithValue("@id",id); command.Parameters.AddWithValue("@active",active); await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<string> OperationalDirectoryMembers(string scope,CancellationToken ct)
    {
        await using var connection=await Open(ct); await EnsureOperationalSessionSchema(connection,ct);
        await using var command=new SqlCommand("""
DECLARE @now datetime2(3)=SYSUTCDATETIME(),@dayStart datetime2(3)=DATEADD(hour,4,CONVERT(datetime2(3),CONVERT(date,DATEADD(hour,-4,SYSUTCDATETIME()))));
DECLARE @scheduleStart datetime2(3)=DATEADD(minute,450,@dayStart),@scheduleEnd datetime2(3)=DATEADD(hour,10,@dayStart);
DECLARE @window bigint=CASE WHEN @now<=@scheduleStart THEN 0 WHEN @now>=@scheduleEnd THEN DATEDIFF_BIG(second,@scheduleStart,@scheduleEnd) ELSE DATEDIFF_BIG(second,@scheduleStart,@now) END;
WITH users AS (
 SELECT CONVERT(nvarchar(80),u.id) id,u.display_name displayName,u.email,u.role,u.support_team supportTeam,
 CASE WHEN UPPER(u.role) IN('ADMINISTRATOR','ADMINISTRATIVE') THEN 'ADMINISTRATIVE' WHEN UPPER(u.role) IN('GROUP_ADMIN','GROUPADMINISTRATOR') THEN 'SUPERVISION' WHEN UPPER(u.role)='DEPARTMENT' THEN UPPER(u.support_team) WHEN UPPER(u.role)='TECHNOLOGY' THEN COALESCE(NULLIF(UPPER(u.support_team),''),'TECHNOLOGY') WHEN UPPER(u.role)='GENERAL_SERVICES' THEN COALESCE(NULLIF(UPPER(u.support_team),''),'GENERAL_SERVICES') WHEN UPPER(u.role)='HUMAN_RESOURCES' THEN 'HUMAN_RESOURCES' WHEN UPPER(u.role)='WAREHOUSE' THEN 'WAREHOUSE' ELSE UPPER(u.role) END department
 FROM dbo.admin_users u WHERE u.is_active=1
), latest AS (
 SELECT s.user_id,s.login_at,s.last_activity_at,s.last_heartbeat_at,s.last_interaction_at,s.logout_at,s.last_path,ROW_NUMBER() OVER(PARTITION BY s.user_id ORDER BY s.login_at DESC,s.id DESC) rn FROM dbo.operational_user_sessions s
), totals AS (
 SELECT c.user_id,MIN(c.connected_at) firstLoginAt,COUNT_BIG(*) sessionCount,COALESCE(SUM(DATEDIFF_BIG(second,c.connected_at,c.last_seen_at)),0) totalConnectedSeconds,COALESCE(SUM(CASE WHEN c.connected_at>=@dayStart AND c.connected_at<DATEADD(day,1,@dayStart) THEN 1 ELSE 0 END),0) todaySessionCount,COALESCE(SUM(CASE WHEN c.connected_at<CASE WHEN @now<@scheduleEnd THEN @now ELSE @scheduleEnd END AND c.last_seen_at>@scheduleStart THEN DATEDIFF_BIG(second,CASE WHEN c.connected_at>@scheduleStart THEN c.connected_at ELSE @scheduleStart END,CASE WHEN c.last_seen_at<CASE WHEN @now<@scheduleEnd THEN @now ELSE @scheduleEnd END THEN c.last_seen_at ELSE CASE WHEN @now<@scheduleEnd THEN @now ELSE @scheduleEnd END END) ELSE 0 END),0) connectedWindowSeconds
 FROM dbo.operational_user_connections c GROUP BY c.user_id
)
SELECT u.id,u.displayName,u.email,u.role,u.supportTeam,u.department,l.login_at lastLoginAt,COALESCE(l.last_interaction_at,l.last_activity_at) lastActivityAt,l.logout_at lastLogoutAt,l.last_path lastPath,
 CAST(CASE WHEN l.logout_at IS NULL AND l.last_heartbeat_at>=DATEADD(second,-65,@now) THEN 1 ELSE 0 END AS bit) hasOpenSession,CAST(CASE WHEN l.logout_at IS NULL AND l.last_heartbeat_at>=DATEADD(second,-65,@now) AND l.last_path=N'/presencia/activo' THEN 1 ELSE 0 END AS bit) isOnline,
 CASE WHEN COALESCE(l.last_interaction_at,l.last_activity_at) IS NULL THEN NULL ELSE DATEDIFF_BIG(second,COALESCE(l.last_interaction_at,l.last_activity_at),@now) END inactiveSeconds,CASE WHEN l.login_at IS NULL THEN NULL ELSE DATEDIFF_BIG(second,l.login_at,COALESCE(l.logout_at,l.last_heartbeat_at,l.last_activity_at,@now)) END lastSessionSeconds,
 t.firstLoginAt,COALESCE(t.sessionCount,0) sessionCount,COALESCE(t.totalConnectedSeconds,0) totalConnectedSeconds,COALESCE(t.todaySessionCount,0) todaySessionCount,CASE WHEN @window-COALESCE(t.connectedWindowSeconds,0)<0 THEN 0 ELSE @window-COALESCE(t.connectedWindowSeconds,0) END scheduledOfflineSeconds,@window scheduledWindowSeconds
FROM users u LEFT JOIN latest l ON l.user_id=u.id AND l.rn=1 LEFT JOIN totals t ON t.user_id=u.id WHERE @scope='ALL' OR u.department=@scope ORDER BY u.department,u.displayName,u.email FOR JSON PATH;
""",connection);
        command.Parameters.AddWithValue("@scope",scope); var json=new StringBuilder(); await using var reader=await command.ExecuteReaderAsync(ct); while(await reader.ReadAsync(ct))json.Append(reader.GetString(0)); return json.Length==0?"[]":json.ToString();
    }

    private static string? OperationalDepartment(string role,string? team)
    {
        var normalizedRole=(role??"").Replace("_","",StringComparison.Ordinal).ToUpperInvariant(); var normalizedTeam=team?.Trim().ToUpperInvariant();
        return normalizedRole switch {"ADMINISTRATOR" or "ADMINISTRATIVE"=>"ADMINISTRATIVE","GROUPADMIN" or "GROUPADMINISTRATOR"=>"SUPERVISION","DEPARTMENT"=>normalizedTeam,"TECHNOLOGY"=>string.IsNullOrWhiteSpace(normalizedTeam)?"TECHNOLOGY":normalizedTeam,"GENERALSERVICES"=>string.IsNullOrWhiteSpace(normalizedTeam)?"GENERAL_SERVICES":normalizedTeam,"HUMANRESOURCES"=>"HUMAN_RESOURCES","WAREHOUSE"=>"WAREHOUSE",_=>normalizedTeam??role?.ToUpperInvariant()};
    }
}
