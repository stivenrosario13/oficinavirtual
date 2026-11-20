using Microsoft.Data.SqlClient;

sealed record HomeRegionSummaryRow(string Region, int Tickets, int Findings);
sealed record HomeRecentActivityRow(long TicketNumber, string Subject, string Department, string Status, string Agency, DateTime UpdatedAt);
sealed record HomeDashboardSummary(List<HomeRegionSummaryRow> Regions, List<HomeRecentActivityRow> Recent);

sealed partial class Database
{
    public async Task<HomeDashboardSummary> HomeSummary(string userId, string role, bool canAssignTickets, string? supportTeam, CancellationToken ct)
    {
        if (DepartmentAccounts.IsSupportScope(role)) supportTeam = null;
        await using var connection = await Open(ct);
        await EnsureTicketWorkflowSchema(connection, ct);
        await EnsureFindingEvidenceSchema(connection, ct);
        await EnsureAgencyDirectorySchema(connection, ct);
        const string region = """
            CASE
              WHEN UPPER(COALESCE(NULLIF(a.region,''),'')) LIKE '%SANTO DOMINGO%' OR UPPER(COALESCE(a.directory_provincia,'')) IN ('SANTO DOMINGO','DISTRITO NACIONAL') THEN N'Gran Santo Domingo'
              WHEN UPPER(COALESCE(NULLIF(a.region,''),'')) LIKE '%NORTE%' OR UPPER(COALESCE(a.directory_provincia,'')) IN ('SANTIAGO','PUERTO PLATA','ESPAILLAT','LA VEGA','MONSEÑOR NOUEL','SANCHEZ RAMIREZ','DUARTE','HERMANAS MIRABAL','MARIA TRINIDAD SANCHEZ','SAMANA','VALVERDE','MONTE CRISTI','DAJABON','SANTIAGO RODRIGUEZ') THEN N'Norte'
              WHEN UPPER(COALESCE(NULLIF(a.region,''),'')) LIKE '%ESTE%' OR UPPER(COALESCE(a.directory_provincia,'')) IN ('LA ALTAGRACIA','ALTAGRACIA','LA ROMANA','ROMANA','EL SEIBO','SEIBO','HATO MAYOR','SAN PEDRO DE MACORIS','MONTE PLATA') THEN N'Este'
              WHEN UPPER(COALESCE(NULLIF(a.region,''),'')) LIKE '%SUR%' OR UPPER(COALESCE(a.directory_provincia,'')) IN ('SAN CRISTOBAL','PERAVIA','SAN JOSE DE OCOA','AZUA','SAN JUAN','ELIAS PIÑA','BARAHONA','BAHORUCO','BAORUCO','INDEPENDENCIA','PEDERNALES') THEN N'Sur'
              ELSE N'Sin región registrada' END
            """;
        var sql = $"""
            DECLARE @visible TABLE(id uniqueidentifier NOT NULL PRIMARY KEY);
            INSERT INTO @visible(id)
            SELECT t.id FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id
            WHERE @administrator=1
               OR (@groupSupervisor=1 AND (LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)) OR (t.created_by_user_id=@userId AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))))
               OR (@department<>'' AND (LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)) OR (@supportTeam<>'TECHNICIANS' AND @canAssign=1 AND ((@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER') OR (@supportTeam='TECHNICAL_FAILURE' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='TECHNICAL_FAILURE') OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE') AND (t.assigned_department=@department OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@department))))));

            SELECT region,SUM(tickets),SUM(findings) FROM (
              SELECT {region} region,1 tickets,0 findings
              FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id INNER JOIN dbo.agencies a ON a.id=t.agency_id
              UNION ALL
              SELECT {region} region,0 tickets,1 findings
              FROM dbo.automatic_findings af INNER JOIN dbo.agencies a ON a.id=af.agency_id
              WHERE (@administrator=1 OR (@department<>'' AND af.assigned_department=@department) OR (@groupSupervisor=1 AND LTRIM(RTRIM(af.assigned_technician_id))=LTRIM(RTRIM(@userId))))
                AND (@administrator=1 OR @groupSupervisor=1 OR @canAssign=1 OR LTRIM(RTRIM(af.assigned_technician_id))=LTRIM(RTRIM(@userId)))
            ) totals GROUP BY region;

            SELECT TOP(6)t.ticket_number,t.subject,t.assigned_department,t.status,a.terminal,t.updated_at
            FROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id INNER JOIN dbo.agencies a ON a.id=t.agency_id
            ORDER BY t.updated_at DESC,t.ticket_number DESC;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@administrator", role == "Administrator" ? 1 : 0);
        command.Parameters.AddWithValue("@groupSupervisor", role == "GroupAdministrator" ? 1 : 0);
        command.Parameters.AddWithValue("@department", DepartmentForRole(role));
        command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
        command.Parameters.AddWithValue("@supportTeam", supportTeam ?? "");
        command.Parameters.AddWithValue("@userId", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var regions = new List<HomeRegionSummaryRow>();
        while (await reader.ReadAsync(ct)) regions.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2)));
        var recent = new List<HomeRecentActivityRow>();
        if (await reader.NextResultAsync(ct)) while (await reader.ReadAsync(ct)) recent.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDateTime(5)));
        return new(regions, recent);
    }
}
