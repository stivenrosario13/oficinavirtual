using Microsoft.Data.SqlClient;

static class WarehouseArticleCreationAccess
{
    static readonly string[] Creators=["diorly.abreu","diorly.abreu@grupotejeda.local","ana.rodriguez","ana.rodriguez@grupotejeda.local"];

    public static bool Named(string? login)=>Creators.Contains((login??"").Trim().ToLowerInvariant());

    public static async Task EnsureGrants(SqlConnection connection,CancellationToken ct)
    {
        await using var command=new SqlCommand("""
            UPDATE dbo.admin_users
            SET permissions_json=JSON_MODIFY(JSON_MODIFY(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,
                '$.canLoadWarehouseInventory',CAST(1 AS bit)),'$.canCreateWarehouseArticles',CAST(1 AS bit)),
                updated_at=SYSUTCDATETIME()
            WHERE LOWER(LTRIM(RTRIM(email))) IN(N'diorly.abreu',N'diorly.abreu@grupotejeda.local',N'ana.rodriguez',N'ana.rodriguez@grupotejeda.local')
              AND (COALESCE(JSON_VALUE(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,'$.canLoadWarehouseInventory'),'false')<>'true'
                OR COALESCE(JSON_VALUE(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,'$.canCreateWarehouseArticles'),'false')<>'true');
            """,connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
