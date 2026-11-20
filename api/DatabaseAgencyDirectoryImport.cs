using System.Data;
using Microsoft.Data.SqlClient;

sealed partial class Database
{
    public async Task<AgencyDirectoryImportResult> ImportAgencyDirectory(IReadOnlyList<AgencyDirectoryImportRow> rows,string? fileName,string actor,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await EnsureAgencyDirectorySchema(connection,ct);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var batchId=Guid.NewGuid();
        try
        {
            const string stagingSql="""
                CREATE TABLE #agency_import(
                    row_number int NOT NULL,
                    codigo nvarchar(100) NOT NULL,
                    terminal nvarchar(300) NOT NULL,
                    grupo nvarchar(150) NOT NULL,
                    region nvarchar(120) NULL,
                    direccion nvarchar(300) NULL,
                    sector nvarchar(120) NULL,
                    municipio nvarchar(120) NULL,
                    provincia nvarchar(120) NULL,
                    latitude float NULL,
                    longitude float NULL,
                    group_conflict nvarchar(300) NULL
                );
                """;
            await using(var staging=new SqlCommand(stagingSql,connection,transaction))await staging.ExecuteNonQueryAsync(ct);
            var existingGroups=new List<AgencyImportGroup>();
            await using(var groups=new SqlCommand("SELECT grupo,COUNT(*) FROM dbo.agencies WITH(UPDLOCK,HOLDLOCK) GROUP BY grupo;",connection,transaction))
            await using(var reader=await groups.ExecuteReaderAsync(ct))
                while(await reader.ReadAsync(ct))existingGroups.Add(new(reader.GetString(0),reader.GetInt32(1)));
            var groupResolver=new AgencyImportGroups(existingGroups,rows.Select(row=>row.Grupo!));
            var table=new DataTable();
            table.Columns.Add("row_number",typeof(int));table.Columns.Add("codigo",typeof(string));table.Columns.Add("terminal",typeof(string));table.Columns.Add("grupo",typeof(string));
            table.Columns.Add("region",typeof(string));table.Columns.Add("direccion",typeof(string));table.Columns.Add("sector",typeof(string));table.Columns.Add("municipio",typeof(string));table.Columns.Add("provincia",typeof(string));
            table.Columns.Add("latitude",typeof(double));table.Columns.Add("longitude",typeof(double));
            table.Columns.Add("group_conflict",typeof(string));
            foreach(var row in rows)
            {
                var codigo=row.Codigo!.Trim();
                var terminal=row.Terminal!.Trim();
                var group=groupResolver.Resolve(row.Grupo!);
                var grupo=group.Name;
                table.Rows.Add(row.RowNumber,codigo,terminal,grupo,
                    Db(string.IsNullOrWhiteSpace(row.Region)?null:row.Region.Trim()),Db(string.IsNullOrWhiteSpace(row.Direccion)?null:row.Direccion.Trim()),
                    Db(string.IsNullOrWhiteSpace(row.Sector)?null:row.Sector.Trim()),Db(string.IsNullOrWhiteSpace(row.Municipio)?null:row.Municipio.Trim()),Db(string.IsNullOrWhiteSpace(row.Provincia)?null:row.Provincia.Trim()),
                    Db(row.Latitude),Db(row.Longitude),Db(group.Error));
            }

            using(var bulk=new SqlBulkCopy(connection,SqlBulkCopyOptions.TableLock,transaction)){bulk.DestinationTableName="#agency_import";bulk.BatchSize=1000;bulk.BulkCopyTimeout=120;await bulk.WriteToServerAsync(table,ct);}

            // Resolve identities before writes; a group change must not create another agency.
            // Report ambiguous identities instead of choosing an arbitrary audited record.
            const string resolveSql="""
                SELECT i.*,existing.target_id,
                    CASE WHEN i.group_conflict IS NOT NULL THEN i.group_conflict
                         WHEN batch_code.matches>1 THEN N'El código aparece varias veces en el archivo'
                         WHEN existing.matches>1 THEN N'El código ya pertenece a varias agencias; requiere revisión'
                         WHEN existing.matches=0 AND terminal_match.matches>0 THEN N'La terminal ya existe con otro código, incluso en otro grupo'
                         WHEN existing.matches=0 AND batch_match.matches>1 THEN N'La terminal aparece con varios códigos en el archivo'
                         ELSE NULL END AS conflict_reason
                INTO #agency_import_resolution
                FROM #agency_import i
                CROSS APPLY(SELECT COUNT(*) matches,MIN(a.id) target_id FROM dbo.agencies a WITH(UPDLOCK,HOLDLOCK)
                    WHERE UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(i.codigo)) existing
                CROSS APPLY(SELECT COUNT(*) matches FROM dbo.agencies a WITH(UPDLOCK,HOLDLOCK)
                    WHERE UPPER(LTRIM(RTRIM(a.terminal)))=UPPER(i.terminal)) terminal_match
                CROSS APPLY(SELECT COUNT(*) matches FROM #agency_import other
                    WHERE UPPER(other.terminal)=UPPER(i.terminal)) batch_match
                CROSS APPLY(SELECT COUNT(*) matches FROM #agency_import other
                    WHERE UPPER(other.codigo)=UPPER(i.codigo)) batch_code;
                """;
            await using(var resolve=new SqlCommand(resolveSql,connection,transaction)){resolve.CommandTimeout=120;await resolve.ExecuteNonQueryAsync(ct);}
            var conflicts=new List<string>();
            const string conflictSql="""
                SELECT row_number,CONCAT(codigo,N': ',conflict_reason)
                FROM #agency_import_resolution WHERE conflict_reason IS NOT NULL ORDER BY row_number;
                """;
            await using(var conflictCommand=new SqlCommand(conflictSql,connection,transaction))
            await using(var reader=await conflictCommand.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))conflicts.Add($"Fila {reader.GetInt32(0)}: {reader.GetString(1)}");

            const string updateSql="""
                UPDATE a SET terminal=m.terminal,grupo=m.grupo,region=COALESCE(m.region,a.region),
                    directory_direccion=COALESCE(m.direccion,a.directory_direccion),directory_sector=COALESCE(m.sector,a.directory_sector),
                    directory_municipio=COALESCE(m.municipio,a.directory_municipio),directory_provincia=COALESCE(m.provincia,a.directory_provincia),
                    expected_latitude=COALESCE(m.latitude,a.expected_latitude),expected_longitude=COALESCE(m.longitude,a.expected_longitude),
                    is_active=1,updated_at=SYSUTCDATETIME()
                FROM dbo.agencies a INNER JOIN #agency_import_resolution m ON m.target_id=a.id
                WHERE m.conflict_reason IS NULL;
                """;
            int updated;
            await using(var update=new SqlCommand(updateSql,connection,transaction)){update.CommandTimeout=120;updated=await update.ExecuteNonQueryAsync(ct);}

            // Directory imports must not rewrite historical field audit addresses or GPS evidence.
            const string insertSql="""
                INSERT INTO dbo.agencies
                    (id,source_key,codigo,terminal,grupo,region,directory_direccion,directory_sector,directory_municipio,directory_provincia,expected_latitude,expected_longitude,is_active,status,created_at,updated_at)
                SELECT CONVERT(char(36),NEWID()),CONCAT(N'EXCEL:',CONVERT(nvarchar(36),NEWID())),i.codigo,i.terminal,i.grupo,i.region,i.direccion,i.sector,i.municipio,i.provincia,i.latitude,i.longitude,1,'PENDING',SYSUTCDATETIME(),SYSUTCDATETIME()
                FROM #agency_import_resolution i
                WHERE i.target_id IS NULL AND i.conflict_reason IS NULL;
                """;
            int created;
            await using(var insert=new SqlCommand(insertSql,connection,transaction)){insert.CommandTimeout=120;created=await insert.ExecuteNonQueryAsync(ct);}

            const string auditSql="""
                INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
                VALUES('agency_import',@id,'AGENCY_DIRECTORY_EXCEL_IMPORTED',@after,@actor);
                """;
            await using(var audit=new SqlCommand(auditSql,connection,transaction))
            {
                audit.Parameters.AddWithValue("@id",batchId.ToString());
                audit.Parameters.AddWithValue("@after",System.Text.Json.JsonSerializer.Serialize(new{fileName,total=rows.Count,created,updated,skipped=conflicts.Count,conflicts=conflicts.Take(100)}));
                audit.Parameters.AddWithValue("@actor",actor);
                await audit.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return new(rows.Count,created,updated,conflicts.Count,conflicts.Take(100).ToList());
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

}
