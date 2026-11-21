using Microsoft.Data.SqlClient;
using System.Text.Json;

internal sealed partial class Database
{
    public async Task<List<SupportDepartmentRow>> SupportCatalogReadOnly(CancellationToken ct)
    {
        await using SqlConnection connection = await Open(ct);
        const string sql = """
            SELECT d.code,d.name,d.description,d.accent_color,d.display_order,d.is_active,
                   c.id,c.code,c.name,c.description,c.default_priority,c.requires_detail,c.detail_label,c.options_json,c.display_order,c.is_active
            FROM dbo.support_departments d
            LEFT JOIN dbo.support_categories c ON c.department_code=d.code
            WHERE d.is_active=1
            ORDER BY d.display_order,c.display_order,c.name;
            """;
        await using SqlCommand command = new SqlCommand(sql, connection) { CommandTimeout = 8 };
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        Dictionary<string, SupportDepartmentRow> map = new(StringComparer.OrdinalIgnoreCase);
        List<SupportDepartmentRow> departments = new();
        while (await reader.ReadAsync(ct))
        {
            string code = reader.GetString(0);
            if (!map.TryGetValue(code, out SupportDepartmentRow? department))
            {
                department = new SupportDepartmentRow(code, reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5), new List<SupportCategoryRow>());
                map[code] = department;
                departments.Add(department);
            }
            if (!reader.IsDBNull(6))
            {
                department.Categories.Add(new SupportCategoryRow(reader.GetGuid(6), reader.GetString(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetBoolean(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : JsonSerializer.Deserialize<List<string>>(reader.GetString(13)), reader.GetInt32(14), reader.GetBoolean(15)));
            }
        }
        return departments;
    }

    public async Task<List<SupportDepartmentMetricRow>> GlobalSupportDepartmentMetrics(CancellationToken ct)
    {
        await using SqlConnection connection = await Open(ct);
        const string sql = """
            SELECT assigned_department,
                   SUM(CASE WHEN status IN('OPEN','PENDING','IN_PROGRESS') THEN 1 ELSE 0 END),
                   SUM(CASE WHEN status IN('RESOLVED','CLOSED') THEN 1 ELSE 0 END)
            FROM dbo.support_tickets WITH (NOLOCK)
            GROUP BY assigned_department;

            SELECT assigned_department,
                   SUM(CASE WHEN status IN('OPEN','PENDING','IN_PROGRESS') THEN 1 ELSE 0 END),
                   SUM(CASE WHEN status IN('RESOLVED','CLOSED') THEN 1 ELSE 0 END)
            FROM dbo.automatic_findings WITH (NOLOCK)
            GROUP BY assigned_department;
            """;
        await using SqlCommand command = new SqlCommand(sql, connection) { CommandTimeout = 6 };
        Dictionary<string, SupportDepartmentMetricRow> metrics = new(StringComparer.OrdinalIgnoreCase);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            string department = reader.GetString(0);
            metrics[department] = new SupportDepartmentMetricRow(department, reader.GetInt32(1), 0, reader.GetInt32(2), 0, 0, 0);
        }
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                string department = reader.GetString(0);
                SupportDepartmentMetricRow current = metrics.GetValueOrDefault(department) ?? new SupportDepartmentMetricRow(department, 0, 0, 0, 0, 0, 0);
                metrics[department] = current with { Findings = reader.GetInt32(1), Solutions = current.Solutions + reader.GetInt32(2) };
            }
        }
        return metrics.Values.OrderBy(item => item.Department).ToList();
    }
}
