using Microsoft.Data.SqlClient;

internal record IncidentTicketReportRow(long DocumentId,string DocumentNumber,DateTime TicketDate,string Group,string AgencyCode,string Agency,string TicketCode,string AgentContact,string Status,decimal? CompensationAmount,string Supervisor,string Subject);
internal record ExecutiveFindingDetailRow(Guid Id,string AgencyCode,string Agency,string Group,string Department,string Title,string Detail,string Priority,string Status,string? Diagnosis,string? Solution,string? Verification,string? Recommendations,DateTime CreatedAt,DateTime? ResolvedAt,int? ResolutionMinutes,string? ResolvedBy,string? AssignedTechnician,string Supervisor,int EvidenceCount,string Address,string FindingKey,string SourceQuestion,string SourceAnswer,string TriggerReason);
internal record ExecutiveReportExpandedResult(string Period,DateTime FromUtc,DateTime ToUtc,DateTime GeneratedUtc,List<ExecutiveCaseRow> Tickets,List<ExecutiveCaseRow> Findings,List<ExecutiveFindingPersonRow> FindingPeople,List<ExecutiveFindingDetailRow> FindingDetails,List<ExecutiveWarehouseRow> Warehouse,List<ExecutiveWorkshopRow> Workshop,List<ExecutiveAuditGroup> AuditGroups,List<ExecutiveAuditAgency> Agencies,List<IncidentTicketReportRow> IncidentTickets);

sealed partial class Database
{
    public async Task<ExecutiveReportExpandedResult> ExecutiveReport(DateTime? requestedFrom,DateTime? requestedTo,CancellationToken ct)
    {
        var from=requestedFrom?.Date??new DateTime(2000,1,1,0,0,0,DateTimeKind.Utc);
        var requestedEnd=requestedTo?.Date.AddDays(1).AddTicks(-1)??DateTime.UtcNow;
        var to=requestedEnd>DateTime.UtcNow?DateTime.UtcNow:requestedEnd;
        if(from>to)throw new ArgumentException("La fecha Desde no puede ser posterior a la fecha Hasta.");
        from=DateTime.SpecifyKind(from,DateTimeKind.Utc);to=DateTime.SpecifyKind(to,DateTimeKind.Utc);
        var period=requestedFrom.HasValue||requestedTo.HasValue?"range":"global";
        await using var connection=await Open(ct);
        var tickets=new List<ExecutiveCaseRow>();var findings=new List<ExecutiveCaseRow>();var findingPeople=new List<ExecutiveFindingPersonRow>();var findingDetails=new List<ExecutiveFindingDetailRow>();
        var warehouse=new List<ExecutiveWarehouseRow>();var workshop=new List<ExecutiveWorkshopRow>();var auditGroups=new List<ExecutiveAuditGroup>();var agencies=new List<ExecutiveAuditAgency>();

        const string ticketSql="""
            SELECT ISNULL(NULLIF(assigned_department,''),'SIN_DEPARTAMENTO'),ISNULL(NULLIF(assigned_team,''),'GENERAL'),status,COUNT(*)
            FROM dbo.support_tickets
            WHERE created_at>=@from AND created_at<=@to
            GROUP BY assigned_department,assigned_team,status ORDER BY assigned_department,assigned_team,status;
            """;
        await ReadOptional(connection,ticketSql,from,to,ct,reader=>tickets.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3))));

        const string findingSql="""
            SELECT ISNULL(NULLIF(assigned_department,''),'SIN_DEPARTAMENTO'),'HALLAZGOS',status,COUNT(*)
            FROM dbo.automatic_findings
            WHERE created_at>=@from AND created_at<=@to
            GROUP BY assigned_department,status ORDER BY assigned_department,status;
            """;
        await ReadOptional(connection,findingSql,from,to,ct,reader=>findings.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3))));

        const string findingPeopleSql="""
            SELECT role_name,person_name,department,status,SUM(total) total
            FROM (
              SELECT 'SUPERVISOR' role_name,
                     COALESCE(NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.displayName'),''),NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.login'),''),N'Sin identificar') person_name,
                     af.assigned_department department,af.status,COUNT(*) total
              FROM dbo.automatic_findings af INNER JOIN dbo.agency_profiles p ON p.id=af.profile_id
              WHERE af.created_at>=@from AND af.created_at<=@to
              GROUP BY COALESCE(NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.displayName'),''),NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.login'),''),N'Sin identificar'),af.assigned_department,af.status
              UNION ALL
              SELECT CASE WHEN ISNULL(u.support_team,'')='TECHNICIANS' OR ISNULL(u.role,'') IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') THEN 'TECNICO' ELSE 'SOPORTE' END,
                     COALESCE(NULLIF(af.resolved_by_name,''),NULLIF(u.display_name,''),NULLIF(u.email,''),NULLIF(af.assigned_technician_id,''),N'Sin asignar'),
                     af.assigned_department,af.status,COUNT(*)
              FROM dbo.automatic_findings af LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id
              WHERE af.created_at>=@from AND af.created_at<=@to AND (NULLIF(af.assigned_technician_id,'') IS NOT NULL OR NULLIF(af.resolved_by_name,'') IS NOT NULL)
              GROUP BY CASE WHEN ISNULL(u.support_team,'')='TECHNICIANS' OR ISNULL(u.role,'') IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') THEN 'TECNICO' ELSE 'SOPORTE' END,
                       COALESCE(NULLIF(af.resolved_by_name,''),NULLIF(u.display_name,''),NULLIF(u.email,''),NULLIF(af.assigned_technician_id,''),N'Sin asignar'),af.assigned_department,af.status
            ) detail
            GROUP BY role_name,person_name,department,status
            ORDER BY role_name,person_name,department,status;
            """;
        await ReadOptional(connection,findingPeopleSql,from,to,ct,reader=>findingPeople.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4))));

        const string findingDetailSql="""
            SELECT af.id,ISNULL(a.codigo,N'—'),ISNULL(a.terminal,N'—'),ISNULL(a.grupo,N'Sin grupo'),
                   ISNULL(NULLIF(af.assigned_department,''),N'Sin departamento'),ISNULL(NULLIF(af.title,''),N'Hallazgo automático'),ISNULL(af.detail,N''),
                   ISNULL(NULLIF(af.priority,''),'MEDIUM'),ISNULL(NULLIF(af.status,''),'OPEN'),af.diagnosis,af.resolution,af.verification,af.recommendations,
                   af.created_at,af.resolved_at,af.resolution_minutes,NULLIF(af.resolved_by_name,''),NULLIF(u.display_name,''),
                   COALESCE(NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.displayName'),''),NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.login'),''),N'Sin identificar'),
                   ISNULL(evidence.total,0),
                   LTRIM(RTRIM(CONCAT(ISNULL(NULLIF(p.direccion,''),''),CASE WHEN NULLIF(p.sector,'') IS NULL THEN '' ELSE CONCAT(' · ',p.sector) END,CASE WHEN NULLIF(p.municipio,'') IS NULL THEN '' ELSE CONCAT(' · ',p.municipio) END,CASE WHEN NULLIF(p.provincia,'') IS NULL THEN '' ELSE CONCAT(' · ',p.provincia) END))),af.finding_key
            FROM dbo.automatic_findings af
            INNER JOIN dbo.agency_profiles p ON p.id=af.profile_id
            INNER JOIN dbo.agencies a ON a.id=af.agency_id
            LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id
            OUTER APPLY(SELECT COUNT(*) total FROM dbo.finding_resolution_evidence e WHERE e.finding_id=af.id)evidence
            WHERE (af.created_at BETWEEN @from AND @to OR af.resolved_at BETWEEN @from AND @to)
            ORDER BY a.grupo,a.terminal,af.created_at DESC;
            """;
        var richFindingDetails=await ReadOptional(connection,findingDetailSql,from,to,ct,reader=>findingDetails.Add(new(
            reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),
            reader.IsDBNull(9)?null:reader.GetString(9),reader.IsDBNull(10)?null:reader.GetString(10),reader.IsDBNull(11)?null:reader.GetString(11),reader.IsDBNull(12)?null:reader.GetString(12),
            DateTime.SpecifyKind(Convert.ToDateTime(reader.GetValue(13)),DateTimeKind.Utc),reader.IsDBNull(14)?null:DateTime.SpecifyKind(Convert.ToDateTime(reader.GetValue(14)),DateTimeKind.Utc),reader.IsDBNull(15)?null:Convert.ToInt32(reader.GetValue(15)),
            reader.IsDBNull(16)?null:reader.GetString(16),reader.IsDBNull(17)?null:reader.GetString(17),reader.GetString(18),Convert.ToInt32(reader.GetValue(19)),reader.IsDBNull(20)?"":reader.GetString(20),reader.GetString(21),FindingSourceQuestion(reader.GetString(21)),FindingSourceAnswer(reader.GetString(21)),FindingTriggerReason(reader.GetString(21)))));

        // MonsterASP puede conservar el esquema base de V520 sin las columnas de diagnóstico,
        // técnico o evidencias añadidas posteriormente. En ese caso devolvemos todos los datos
        // disponibles y la tarjeta sigue cargando, en lugar de cancelar el reporte completo.
        if(!richFindingDetails)
        {
            findingDetails.Clear();
            const string basicFindingDetailSql="""
                SELECT af.id,ISNULL(a.codigo,N'—'),ISNULL(a.terminal,N'—'),ISNULL(a.grupo,N'Sin grupo'),
                       ISNULL(NULLIF(af.assigned_department,''),N'Sin departamento'),ISNULL(NULLIF(af.title,''),N'Hallazgo automático'),ISNULL(af.detail,N''),
                       ISNULL(NULLIF(af.priority,''),'MEDIUM'),ISNULL(NULLIF(af.status,''),'OPEN'),
                       af.resolution,af.created_at,af.resolved_at,af.resolution_minutes,NULLIF(af.resolved_by_name,''),
                       COALESCE(NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.displayName'),''),NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.login'),''),N'Sin identificar'),
                       LTRIM(RTRIM(CONCAT(ISNULL(NULLIF(p.direccion,''),''),CASE WHEN NULLIF(p.sector,'') IS NULL THEN '' ELSE CONCAT(' · ',p.sector) END,CASE WHEN NULLIF(p.municipio,'') IS NULL THEN '' ELSE CONCAT(' · ',p.municipio) END,CASE WHEN NULLIF(p.provincia,'') IS NULL THEN '' ELSE CONCAT(' · ',p.provincia) END))),af.finding_key
                FROM dbo.automatic_findings af
                INNER JOIN dbo.agency_profiles p ON p.id=af.profile_id
                INNER JOIN dbo.agencies a ON a.id=af.agency_id
                WHERE (af.created_at BETWEEN @from AND @to OR af.resolved_at BETWEEN @from AND @to)
                ORDER BY a.grupo,a.terminal,af.created_at DESC;
                """;
            await ReadOptional(connection,basicFindingDetailSql,from,to,ct,reader=>findingDetails.Add(new(
                reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),
                null,reader.IsDBNull(9)?null:reader.GetString(9),null,null,
                DateTime.SpecifyKind(Convert.ToDateTime(reader.GetValue(10)),DateTimeKind.Utc),reader.IsDBNull(11)?null:DateTime.SpecifyKind(Convert.ToDateTime(reader.GetValue(11)),DateTimeKind.Utc),reader.IsDBNull(12)?null:Convert.ToInt32(reader.GetValue(12)),
                reader.IsDBNull(13)?null:reader.GetString(13),null,reader.GetString(14),0,reader.IsDBNull(15)?"":reader.GetString(15),reader.GetString(16),FindingSourceQuestion(reader.GetString(16)),FindingSourceAnswer(reader.GetString(16)),FindingTriggerReason(reader.GetString(16)))));
        }

        const string warehouseSql="""
            SELECT ISNULL(NULLIF(department,''),N'Sin departamento'),
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.status'),''),'pending'),COUNT(*)
            FROM dbo.institutional_files
            CROSS APPLY(VALUES(CASE WHEN ISJSON(metadata)=1 THEN metadata ELSE N'{}' END)) safe(metadata)
            WHERE area='communications' AND channel='requirements'
              AND JSON_QUERY(safe.metadata,'$.warehouseRequest') IS NOT NULL
              AND JSON_VALUE(safe.metadata,'$.recipientDepartment')=N'Almacén'
              AND COALESCE(TRY_CONVERT(datetime2,JSON_VALUE(safe.metadata,'$.createdUtc')),TRY_CONVERT(datetime2,JSON_VALUE(safe.metadata,'$.sentUtc')),'2000-01-01') BETWEEN @from AND @to
            GROUP BY department,JSON_VALUE(safe.metadata,'$.status') ORDER BY department,JSON_VALUE(safe.metadata,'$.status');
            """;
        await ReadOptional(connection,warehouseSql,from,to,ct,reader=>warehouse.Add(new(reader.GetString(0),reader.GetString(1),reader.GetInt32(2))));

        const string workshopSql="""
            IF OBJECT_ID(N'dbo.maintenance_movements',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.maintenance_workshop_cases',N'U') IS NOT NULL
            BEGIN
              SELECT m.department,COALESCE(c.status,'SENT'),COUNT(*) equipment,
                     SUM(1+COALESCE(activity.total,0)) movements
              FROM dbo.maintenance_movements m
              LEFT JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id
              OUTER APPLY(SELECT COUNT(*) total FROM dbo.maintenance_workshop_events e WHERE e.source_id=m.id AND e.occurred_at BETWEEN @from AND @to) activity
              WHERE m.created_at BETWEEN @from AND @to AND (m.movement_type='TRANSFER_TO_WORKSHOP' OR
                    (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' AND NOT EXISTS(
                      SELECT 1 FROM dbo.maintenance_workshop_events child WHERE child.movement_id=m.id)))
              GROUP BY m.department,COALESCE(c.status,'SENT')
              ORDER BY m.department,COALESCE(c.status,'SENT');
            END
            """;
        await ReadOptional(connection,workshopSql,from,to,ct,reader=>workshop.Add(new(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),Convert.ToInt32(reader.GetValue(3)))));

        const string groupsSql="""
            SELECT ISNULL(a.grupo,N'Sin grupo'),COUNT(*),
                   SUM(CASE WHEN a.status='COMPLETED' AND p.id IS NOT NULL THEN 1 ELSE 0 END),
                   SUM(CASE WHEN a.status='PENDING' OR (a.status='COMPLETED' AND p.id IS NULL) THEN 1 ELSE 0 END),
                   SUM(CASE WHEN a.status='REVIEW_REQUIRED' THEN 1 ELSE 0 END),
                   SUM(CASE WHEN a.status='COMPLETED' AND p.id IS NOT NULL THEN 1 ELSE 0 END)
            FROM dbo.agencies a OUTER APPLY(
              SELECT TOP(1) p.id,p.submitted_at FROM dbo.agency_profiles p
              WHERE p.agency_id=a.id AND p.submitted_at BETWEEN @from AND @to ORDER BY p.submitted_at DESC
            ) p
            WHERE a.is_active=1 GROUP BY a.grupo ORDER BY a.grupo;
            """;
        await ReadOptional(connection,groupsSql,from,to,ct,reader=>auditGroups.Add(new(reader.GetString(0),reader.GetInt32(1),Convert.ToInt32(reader.GetValue(2)),Convert.ToInt32(reader.GetValue(3)),Convert.ToInt32(reader.GetValue(4)),Convert.ToInt32(reader.GetValue(5)))));

        const string agenciesSql="""
            SELECT ISNULL(a.codigo,N'—'),ISNULL(a.terminal,N'—'),ISNULL(a.grupo,N'Sin grupo'),CASE WHEN a.status='COMPLETED' AND p.id IS NULL THEN 'PENDING' ELSE a.status END,p.submitted_at,
                   COALESCE(NULLIF(JSON_VALUE(CASE WHEN ISJSON(p.submitted_by_context)=1 THEN p.submitted_by_context ELSE N'{}' END,'$.login'),''),N'Sin identificar')
            FROM dbo.agencies a OUTER APPLY(
              SELECT TOP(1) p.submitted_at,p.submitted_by_context FROM dbo.agency_profiles p
              WHERE p.agency_id=a.id AND p.submitted_at BETWEEN @from AND @to ORDER BY p.submitted_at DESC
            ) p
            WHERE a.is_active=1 ORDER BY a.grupo,a.status,a.terminal;
            """;
        await ReadOptional(connection,agenciesSql,from,to,ct,reader=>agencies.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:DateTime.SpecifyKind(reader.GetDateTime(4),DateTimeKind.Utc),reader.IsDBNull(5)?null:reader.GetString(5))));
        var incidentTickets=await IncidentTicketReport(from,to,ct,connection);
        return new(period,from,to,DateTime.UtcNow,tickets,findings,findingPeople,findingDetails,warehouse,workshop,auditGroups,agencies,incidentTickets);
    }

    public async Task<List<IncidentTicketReportRow>> IncidentTicketReport(DateTime? requestedFrom,DateTime? requestedTo,CancellationToken ct,SqlConnection? existingConnection=null)
    {
        var from=requestedFrom?.Date??new DateTime(2000,1,1);
        var to=(requestedTo?.Date??DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);
        if(from>to)throw new ArgumentException("La fecha Desde no puede ser posterior a la fecha Hasta.");
        var ownsConnection=existingConnection is null;
        await using var opened=ownsConnection?await Open(ct):null;
        var connection=existingConnection??opened!;
        const string sql="""
            SELECT f.id,
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.number'),''),CONCAT('INC-',f.id)),
                   dates.ticket_date,
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.formData.groupName'),''),N'Sin grupo'),
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.agencyCode'),''),N'—'),
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.formData.incidentAgencyName'),''),NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.targetName'),''),N'—'),
                   rows.ticket_code,ISNULL(NULLIF(rows.agent_contact,''),N'—'),ISNULL(NULLIF(rows.decision_status,''),'PENDING'),
                   rows.compensation,
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.formData.preparedBy'),''),NULLIF(JSON_VALUE(safe.metadata,'$.ownerDisplayName'),''),N'Sin identificar'),
                   ISNULL(NULLIF(JSON_VALUE(safe.metadata,'$.administrativeRequest.formData.subject'),''),N'Informe de incidentes')
            FROM dbo.institutional_files f
            CROSS APPLY(VALUES(CASE WHEN ISJSON(f.metadata)=1 THEN f.metadata ELSE N'{}' END)) safe(metadata)
            CROSS APPLY OPENJSON(safe.metadata,'$.administrativeRequest.formData.rows') WITH(
              ticket_date_text nvarchar(10) '$.ticketDate',ticket_code nvarchar(180) '$.ticketCode',agent_contact nvarchar(180) '$.agentPhone',
              decision_status nvarchar(20) '$.decisionStatus',compensation decimal(18,2) '$.approvedAmount') rows
            CROSS APPLY(SELECT COALESCE(TRY_CONVERT(datetime2,rows.ticket_date_text),TRY_CONVERT(datetime2,JSON_VALUE(safe.metadata,'$.administrativeRequest.formData.reportDate')),TRY_CONVERT(datetime2,JSON_VALUE(safe.metadata,'$.createdUtc')),TRY_CONVERT(datetime2,JSON_VALUE(safe.metadata,'$.sentUtc'))) ticket_date) dates
            WHERE JSON_VALUE(safe.metadata,'$.administrativeRequest.formType')='INCIDENT_REPORT'
              AND ISNULL(JSON_VALUE(safe.metadata,'$.trashed'),'false')<>'true'
              AND NULLIF(LTRIM(RTRIM(rows.ticket_code)),'') IS NOT NULL
              AND dates.ticket_date BETWEEN @from AND @to
            ORDER BY JSON_VALUE(safe.metadata,'$.administrativeRequest.formData.groupName'),dates.ticket_date DESC,rows.ticket_code;
            """;
        var result=new List<IncidentTicketReportRow>();
        try
        {
            await using var command=new SqlCommand(sql,connection);command.Parameters.AddWithValue("@from",from);command.Parameters.AddWithValue("@to",to);
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))result.Add(new(reader.GetInt64(0),reader.GetString(1),DateTime.SpecifyKind(reader.GetDateTime(2),DateTimeKind.Utc),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.IsDBNull(9)?null:reader.GetDecimal(9),reader.GetString(10),reader.GetString(11)));
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Reporte ejecutivo: se omitió el detalle opcional de incidentes ({ReportErrorCode(exception)}): {exception.Message}");
        }
        return result;
    }

    private static async Task<bool> ReadOptional(SqlConnection connection,string sql,DateTime from,DateTime to,CancellationToken ct,Action<SqlDataReader> add)
    {
        try
        {
            await using var command=new SqlCommand(sql,connection);
            command.Parameters.AddWithValue("@from",from);command.Parameters.AddWithValue("@to",to);
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))add(reader);
            return true;
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            // Las áreas opcionales se incorporaron en versiones distintas. Una tabla o columna
            // ausente o una fila histórica dañada no debe impedir que el administrador consulte
            // las demás áreas disponibles.
            // Sustituye el filtro anterior IsOptionalReportQueryFailure(exception) y también
            // cubre diferencias de tipo devueltas por esquemas históricos de MonsterASP.
            Console.Error.WriteLine($"Reporte ejecutivo: se omitió una fuente opcional ({ReportErrorCode(exception)}): {exception.Message}");
            return false;
        }
    }

    private static string ReportErrorCode(Exception exception)=>exception is SqlException sql?$"SQL {sql.Number}":exception.GetType().Name;

    private static string FindingSourceQuestion(string key)=>key switch
    {
        "NOT_PAINTED"=>"¿La agencia fue pintada?",
        "NO_RAZA_STICKER"=>"¿Fue colocado el sticker de raza?",
        "NO_REAL_STICKER"=>"¿Fue colocado el sticker de Real?",
        "LOTEKA_NOT_REMOVED"=>"¿Fue retirada toda la publicidad de Loteka?",
        "PRINTER_NO_MAINTENANCE"=>"¿Le dieron mantenimiento a la impresora?",
        "NO_INVERTER"=>"¿Tiene inversor?",
        "NO_BATTERY"=>"¿Tiene batería?",
        "DAMAGE_WIRING" or "DAMAGE_SCREENS" or "DAMAGE_CONNECTIVITY" or "DAMAGE_FURNITURE" or
        "DAMAGE_PAINTING" or "DAMAGE_INVERTER" or "DAMAGE_BATTERIES" or "DAMAGE_RESTRUCTURING" or
        "DAMAGE_LIGHTING" or "DAMAGE_METALWORK" or "DAMAGE_GENERATOR_REQUEST" or "DAMAGE_ELECTRICAL" or
        "DAMAGE_SHUTTER" or "DAMAGE_GENERAL_OTHER" or "DAMAGE_REPORTED"=>"¿Hubo alguna avería durante el levantamiento?",
        _=>"Pregunta registrada en Auditoría de campo asociada al hallazgo automático."
    };

    private static string FindingTriggerReason(string key)=>key switch
    {
        "DAMAGE_WIRING"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Fallas de cableado”.",
        "DAMAGE_SCREENS"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Pantallas o monitores”.",
        "DAMAGE_CONNECTIVITY"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Conectividad”.",
        "DAMAGE_FURNITURE" or "DAMAGE_RESTRUCTURING"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Reestructuración física / Sheetrock”.",
        "DAMAGE_PAINTING"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Pintura”.",
        "DAMAGE_INVERTER"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Inversor”.",
        "DAMAGE_BATTERIES"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Baterías”.",
        "DAMAGE_LIGHTING"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Iluminación”.",
        "DAMAGE_METALWORK"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Herrería”.",
        "DAMAGE_GENERATOR_REQUEST"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Requisición planta”.",
        "DAMAGE_ELECTRICAL"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Avería eléctrica”.",
        "DAMAGE_SHUTTER"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Falla en el shutter”.",
        "DAMAGE_GENERAL_OTHER"=>"En Auditoría de campo se respondió “Sí” a la pregunta de averías y se seleccionó “Otro servicio general”.",
        "DAMAGE_REPORTED"=>"En una Auditoría de campo histórica se respondió “Sí” a la pregunta de averías; esa versión no exigía clasificar el tipo.",
        _=>"En Auditoría de campo se respondió “No” a esta pregunta; esa respuesta incumple la condición esperada y creó el hallazgo automáticamente."
    };

    private static string FindingSourceAnswer(string key)=>key switch
    {
        "DAMAGE_WIRING"=>"Sí - Fallas de cableado",
        "DAMAGE_SCREENS"=>"Sí - Pantallas o monitores",
        "DAMAGE_CONNECTIVITY"=>"Sí - Conectividad",
        "DAMAGE_FURNITURE" or "DAMAGE_RESTRUCTURING"=>"Sí - Reestructuración física / Sheetrock",
        "DAMAGE_PAINTING"=>"Sí - Pintura",
        "DAMAGE_INVERTER"=>"Sí - Inversor",
        "DAMAGE_BATTERIES"=>"Sí - Baterías",
        "DAMAGE_LIGHTING"=>"Sí - Iluminación",
        "DAMAGE_METALWORK"=>"Sí - Herrería",
        "DAMAGE_GENERATOR_REQUEST"=>"Sí - Requisición planta",
        "DAMAGE_ELECTRICAL"=>"Sí - Avería eléctrica",
        "DAMAGE_SHUTTER"=>"Sí - Falla en el shutter",
        "DAMAGE_GENERAL_OTHER"=>"Sí - Otro servicio general",
        "DAMAGE_REPORTED"=>"Sí - Tipo de avería no clasificado en el formulario histórico",
        "NOT_PAINTED" or "NO_RAZA_STICKER" or "NO_REAL_STICKER" or "LOTEKA_NOT_REMOVED" or
        "PRINTER_NO_MAINTENANCE" or "NO_INVERTER" or "NO_BATTERY"=>"No",
        _=>"Respuesta no disponible"
    };

    private static bool IsOptionalReportQueryFailure(SqlException exception)
    {
        // Solo se propagan fallos reales de conexión, autenticación o base inaccesible. Los errores
        // de esquema y datos de un módulo histórico no deben cancelar el consolidado completo.
        return exception.Number != 18456 && exception.Number != 4060 && exception.Number != -2 &&
               exception.Number != 20 && exception.Number != 40 && exception.Number != 53 &&
               exception.Number != 64 && exception.Number != 233 && exception.Number != 258 &&
               exception.Number != 10053 && exception.Number != 10054 && exception.Number != 10060 &&
               exception.Number != 11001;
    }
}
