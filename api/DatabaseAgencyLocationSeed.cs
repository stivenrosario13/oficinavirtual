using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

sealed partial class Database
{
    const string AgencyLocationSeedId = "SONADORA-2026-09-07";

    static async Task EnsureBundledAgencyLocations(SqlConnection connection,CancellationToken ct)
    {
        var sourcePath=Path.Combine(AppContext.BaseDirectory,"Data","agency-locations-sonadora-20260907.json");
        if(!File.Exists(sourcePath))return;
        var rows=JsonSerializer.Deserialize<List<BundledAgencyLocation>>(await File.ReadAllTextAsync(sourcePath,ct),StorageJson)??[];
        if(rows.Count==0)return;

        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            const string schema="""
                IF OBJECT_ID(N'dbo.agency_location_imports',N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.agency_location_imports(
                        source_id varchar(80) NOT NULL CONSTRAINT PK_agency_location_imports PRIMARY KEY,
                        source_rows int NOT NULL,
                        matched_agencies int NOT NULL,
                        applied_at datetime2(3) NOT NULL CONSTRAINT DF_agency_location_imports_applied DEFAULT SYSUTCDATETIME()
                    );
                END;
                """;
            await using(var schemaCommand=new SqlCommand(schema,connection,transaction))await schemaCommand.ExecuteNonQueryAsync(ct);
            await using(var applied=new SqlCommand("SELECT COUNT(*) FROM dbo.agency_location_imports WITH(UPDLOCK,HOLDLOCK) WHERE source_id=@source;",connection,transaction))
            {
                applied.Parameters.AddWithValue("@source",AgencyLocationSeedId);
                if(Convert.ToInt32(await applied.ExecuteScalarAsync(ct))>0){await transaction.CommitAsync(ct);return;}
            }

            const string temp="""
                CREATE TABLE #agency_locations(
                    codigo nvarchar(100) NOT NULL PRIMARY KEY,
                    direccion nvarchar(300) NULL,
                    sector nvarchar(120) NULL,
                    municipio nvarchar(120) NULL,
                    provincia nvarchar(120) NULL,
                    latitude float NOT NULL,
                    longitude float NOT NULL
                );
                """;
            await using(var tempCommand=new SqlCommand(temp,connection,transaction))await tempCommand.ExecuteNonQueryAsync(ct);
            var table=new DataTable();
            table.Columns.Add("codigo",typeof(string));table.Columns.Add("direccion",typeof(string));table.Columns.Add("sector",typeof(string));table.Columns.Add("municipio",typeof(string));table.Columns.Add("provincia",typeof(string));table.Columns.Add("latitude",typeof(double));table.Columns.Add("longitude",typeof(double));
            foreach(var row in rows)table.Rows.Add(row.Codigo,Db(row.Direccion),Db(row.Sector),Db(row.Municipio),Db(row.Provincia),row.Latitude,row.Longitude);
            using(var bulk=new SqlBulkCopy(connection,SqlBulkCopyOptions.CheckConstraints,transaction)){bulk.DestinationTableName="#agency_locations";bulk.BatchSize=500;await bulk.WriteToServerAsync(table,ct);}

            const string sync="""
                DECLARE @matched int=(SELECT COUNT(*) FROM dbo.agencies a INNER JOIN #agency_locations s ON UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(LTRIM(RTRIM(s.codigo))));
                UPDATE a SET
                    directory_direccion=COALESCE(s.direccion,a.directory_direccion),
                    directory_sector=COALESCE(s.sector,a.directory_sector),
                    directory_municipio=COALESCE(s.municipio,a.directory_municipio),
                    directory_provincia=COALESCE(s.provincia,a.directory_provincia),
                    expected_latitude=s.latitude,expected_longitude=s.longitude,updated_at=SYSUTCDATETIME()
                FROM dbo.agencies a INNER JOIN #agency_locations s ON UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(LTRIM(RTRIM(s.codigo)));
                UPDATE p SET
                    direccion=COALESCE(s.direccion,p.direccion),sector=COALESCE(s.sector,p.sector),
                    municipio=COALESCE(s.municipio,p.municipio),provincia=COALESCE(s.provincia,p.provincia),
                    latitude=s.latitude,longitude=s.longitude,updated_at=SYSUTCDATETIME()
                FROM dbo.agency_profiles p INNER JOIN dbo.agencies a ON a.id=p.agency_id INNER JOIN #agency_locations s ON UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(LTRIM(RTRIM(s.codigo)));
                INSERT INTO dbo.agency_location_imports(source_id,source_rows,matched_agencies) VALUES(@source,@rows,@matched);
                INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
                VALUES('agency_import',@source,'AGENCY_LOCATIONS_SYNCHRONIZED',CONCAT(N'{"sourceRows":',@rows,N',"matchedAgencies":',@matched,N'}'),N'system@grupotejeda.local');
                SELECT @matched;
                """;
            await using var syncCommand=new SqlCommand(sync,connection,transaction);syncCommand.Parameters.AddWithValue("@source",AgencyLocationSeedId);syncCommand.Parameters.AddWithValue("@rows",rows.Count);await syncCommand.ExecuteScalarAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch{await transaction.RollbackAsync(ct);throw;}
    }

    sealed record BundledAgencyLocation(string Codigo,string? Direccion,string? Sector,string? Municipio,string? Provincia,double Latitude,double Longitude);
}
