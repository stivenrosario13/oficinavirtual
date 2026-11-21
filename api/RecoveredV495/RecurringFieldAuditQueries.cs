using System.Data;
using Microsoft.Data.SqlClient;

internal sealed partial class Database
{
    public async Task<List<GroupRow>> RecurringAuditGroups(string userId, string role, CancellationToken ct)
    {
        await using SqlConnection connection = await Open(ct);
        string sql = role == "GroupAdministrator"
            ? """
              SELECT ug.group_name,COUNT(DISTINCT a.id)
              FROM dbo.admin_user_groups ug
              LEFT JOIN dbo.agencies a ON a.grupo=ug.group_name
                AND (a.is_active=1 OR EXISTS(SELECT 1 FROM dbo.agency_profiles history WHERE history.agency_id=a.id))
              WHERE ug.user_id=@userId
              GROUP BY ug.group_name ORDER BY ug.group_name;
              """
            : """
              SELECT a.grupo,COUNT(DISTINCT a.id)
              FROM dbo.agencies a
              WHERE a.is_active=1 OR EXISTS(SELECT 1 FROM dbo.agency_profiles history WHERE history.agency_id=a.id)
              GROUP BY a.grupo ORDER BY a.grupo;
              """;
        await using SqlCommand command = new SqlCommand(sql, connection);
        if (role == "GroupAdministrator") command.Parameters.AddWithValue("@userId", userId);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        List<GroupRow> groups = new();
        while (await reader.ReadAsync(ct)) groups.Add(new GroupRow(reader.GetString(0), reader.GetInt32(1)));
        return groups;
    }

    public async Task<List<AgencyRow>> RecurringAuditAgencies(string group, string userId, string role, CancellationToken ct)
    {
        await using SqlConnection connection = await Open(ct);
        const string sql = """
            SELECT a.id,a.codigo,a.terminal,a.expected_latitude,a.expected_longitude,
                   COUNT(p.id),MAX(p.submitted_at),
                   CASE WHEN COUNT(p.id)>0 THEN 'COMPLETED' ELSE a.status END
            FROM dbo.agencies a
            LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
            WHERE a.grupo=@group
              AND (a.is_active=1 OR p.id IS NOT NULL)
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            GROUP BY a.id,a.codigo,a.terminal,a.expected_latitude,a.expected_longitude,a.status
            ORDER BY a.terminal,a.codigo;
            """;
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@group", group);
        AddScope(command, userId, role);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        List<AgencyRow> agencies = new();
        while (await reader.ReadAsync(ct))
        {
            agencies.Add(new AgencyRow(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
                reader.GetString(7)));
        }
        return agencies;
    }

    public async Task<List<DashboardAgency>> RecurringPanelAgencies(string? group, string userId, string role, CancellationToken ct)
    {
        await using SqlConnection connection = await Open(ct);
        const string sql = """
            SELECT a.codigo,a.terminal,a.grupo,
                   CASE WHEN COUNT(p.id)>0 THEN 'COMPLETED' ELSE a.status END,
                   CASE WHEN COUNT(p.id)>0 THEN 100 WHEN a.status='REVIEW_REQUIRED' THEN 75 ELSE 0 END,
                   MAX(p.submitted_at),COUNT(p.id)
            FROM dbo.agencies a
            LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
            WHERE (@group IS NULL OR a.grupo=@group)
              AND (a.is_active=1 OR p.id IS NOT NULL)
              AND (@scoped=0 OR EXISTS(
                    SELECT 1 FROM dbo.admin_user_groups ug
                    WHERE ug.user_id=@userId AND ug.group_name=a.grupo
              ))
            GROUP BY a.id,a.codigo,a.terminal,a.grupo,a.status
            ORDER BY a.grupo,a.terminal,a.codigo;
            """;
        await using SqlCommand command = new SqlCommand(sql, connection);
        command.Parameters.Add("@group", SqlDbType.NVarChar, 120).Value = (object?)group ?? DBNull.Value;
        AddScope(command, userId, role);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        List<DashboardAgency> agencies = new();
        while (await reader.ReadAsync(ct))
        {
            agencies.Add(new DashboardAgency(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4),
                reader.IsDBNull(5) ? null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc), reader.GetInt32(6)));
        }
        return agencies;
    }

    public async Task<DashboardRow> RecurringAuditDashboard(string userId, string role, CancellationToken ct)
    {
        DashboardRow current = await Dashboard(userId, role, ct);
        List<DashboardAgency> agencies = await RecurringPanelAgencies(null, userId, role, ct);
        List<DashboardGroup> groups = agencies
            .GroupBy(item => item.Grupo, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group =>
            {
                int total = group.Count();
                int completed = group.Count(item => item.AuditCount > 0);
                int pending = group.Count(item => item.AuditCount == 0 && item.Status == "PENDING");
                DateTime? lastUpdated = group.Where(item => item.LastUpdated.HasValue).Select(item => item.LastUpdated).Max();
                int auditCount = group.Sum(item => item.AuditCount);
                int completedRounds = total == 0 ? 0 : group.Min(item => item.AuditCount);
                int roundsTotal = Math.Max(1, total == 0 ? 0 : group.Max(item => item.AuditCount));
                return new DashboardGroup(group.Key, total, completed, pending, total == 0 ? 0 : (int)Math.Round(completed * 100d / total), lastUpdated, auditCount, completedRounds, roundsTotal);
            }).ToList();
        int completedTotal = agencies.Count(item => item.AuditCount > 0);
        int reviewTotal = agencies.Count(item => item.AuditCount == 0 && item.Status == "REVIEW_REQUIRED");
        int pendingTotal = agencies.Count - completedTotal - reviewTotal;
        return current with
        {
            Total = agencies.Count,
            Completed = completedTotal,
            Pending = pendingTotal,
            Review = reviewTotal,
            Percent = agencies.Count == 0 ? 0 : (int)Math.Round(completedTotal * 100d / agencies.Count),
            GroupsTotal = groups.Count,
            GroupsReady = groups.Count(group => group.Total > 0 && group.Completed == group.Total),
            Groups = groups,
            Agencies = agencies
        };
    }
}
