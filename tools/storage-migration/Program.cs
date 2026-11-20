using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// Default is inventory. --apply copies; --release replaces verified originals
// with private remote references. Neither mode deletes business records.
var apply = args.Contains("--apply");
var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env.local");
if (File.Exists(envPath))
    foreach (var line in File.ReadLines(envPath))
    {
        var split = line.IndexOf('=');
        if (split <= 0 || line.TrimStart().StartsWith('#')) continue;
        var name = line[..split].Trim().Replace("__",":");
        if (string.IsNullOrEmpty(config[name])) config[name] = line[(split+1)..].Trim().Trim('"', '\'');
    }
StorageFiles.Configure(config);
var raw = config.GetConnectionString("DefaultConnection") ?? config["MSSQL_CONNECTION_STRING"];
if(string.IsNullOrWhiteSpace(raw)){Console.Error.WriteLine("Falta configurar ConnectionStrings__DefaultConnection o MSSQL_CONNECTION_STRING; no se modificó ningún dato.");return 1;}
var connectionString = new SqlConnectionStringBuilder(raw) { ConnectTimeout = 20 };
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_,e) => { e.Cancel = true; stop.Cancel(); };
var ct = stop.Token;
try
{
    if(args.Contains("--verify"))
    {
        var verification=await StorageVerification.Run("public",ct);
        Console.WriteLine(JsonSerializer.Serialize(verification));return verification.Passed?0:2;
    }
    await using var connection = new SqlConnection(connectionString.ConnectionString);
    await connection.OpenAsync(ct);
    if(args.Contains("--audit-storage"))return await StoragePersistenceAudit.Run(connection,ct);
    if(args.Contains("--reclaim-space"))
        return await StorageSpaceReclaimer.Run(connection,ct);
    if(args.Contains("--space-detail"))
    {
        var reports=new List<object>();
        foreach(var (label,sql) in new[]{
            ("database","SELECT DB_NAME() database_name,SERVERPROPERTY('ProductMajorVersion') major_version,IS_MEMBER('db_owner') is_db_owner,recovery_model_desc,log_reuse_wait_desc FROM sys.databases WHERE name=DB_NAME()"),
            ("files","SELECT file_id,name,type_desc,CONVERT(bigint,size)*8192 allocated_bytes,CONVERT(bigint,FILEPROPERTY(name,'SpaceUsed'))*8192 used_bytes FROM sys.database_files"),
            ("businessRows","SELECT s.name+'.'+t.name table_name,SUM(p.rows) row_count FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) GROUP BY s.name,t.name ORDER BY s.name,t.name"),
            ("migration","SELECT COUNT_BIG(*) verified_versions,SUM(CASE WHEN released_at IS NOT NULL THEN CONVERT(bigint,1) ELSE 0 END) released_versions,SUM(COALESCE(released_bytes,0)) released_bytes FROM dbo.storage_migration_files"),
            ("tables","SELECT TOP(12) OBJECT_SCHEMA_NAME(object_id)+'.'+OBJECT_NAME(object_id) table_name,SUM(reserved_page_count)*8192 reserved_bytes,SUM(used_page_count)*8192 used_bytes,SUM(CASE WHEN index_id IN(0,1) THEN row_count ELSE 0 END) row_count FROM sys.dm_db_partition_stats GROUP BY object_id ORDER BY reserved_bytes DESC")})
        {
            var rows=new List<Dictionary<string,object?>>();
            await using var command=new SqlCommand(sql,connection){CommandTimeout=20};
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct)){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}
            reports.Add(new{label,rows});
        }
        Console.WriteLine(JsonSerializer.Serialize(new{checkedAt=DateTime.UtcNow,reports}));return 0;
    }
    if(args.Contains("--space"))
    {
        await using var command=new SqlCommand("SELECT SUM(CONVERT(bigint,size))*8192,SUM(CONVERT(bigint,FILEPROPERTY(name,'SpaceUsed')))*8192 FROM sys.database_files WHERE type=0",connection);
        // Only report aggregate space, never connection details or application records.
        await using var reader=await command.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);
        Console.WriteLine(JsonSerializer.Serialize(new{allocatedBytes=reader.GetInt64(0),usedBytes=reader.GetInt64(1)}));return 0;
    }
    if(args.Contains("--release"))
    {
        var release=await StorageMigrationRunner.ReleaseVerified(connection,ct);
        Console.WriteLine(JsonSerializer.Serialize(release));return release.Message is null?0:2;
    }
    var report=await StorageMigrationRunner.Run(connection,apply,ct,Console.WriteLine);
    Directory.CreateDirectory("outputs");
    var path=Path.GetFullPath($"outputs/storage-migration-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}),ct);
    Console.WriteLine(JsonSerializer.Serialize(new{report.Candidates,report.Copied,report.Existing,report.Skipped,report.Failed,report=path,report.OriginalsPreserved}));
    return report.Failed==0?0:2;
}
catch(OperationCanceledException){Console.Error.WriteLine("Operación interrumpida. Se puede reanudar; consulta el diario para conocer las operaciones confirmadas.");return 3;}
catch(Exception error){Console.Error.WriteLine(error is StorageServiceException storage?$"{storage.Code}: {storage.Message}":error is SqlException sql?$"Operación detenida: SQL {sql.Number}, clase {sql.Class}, estado {sql.State}. Consulta el diario antes de reanudar.":$"Operación detenida: {error.GetType().Name}. Revisa conexión/configuración y consulta el diario.");return 1;}
