using System.Text;
using Microsoft.Data.SqlClient;

record StorageMigrationReport(int Candidates,int Copied,int Existing,int Skipped,int Failed,List<object> Details)
{
    public bool OriginalsPreserved => true; // This scan only copies; release is a separately verified phase.
    public string? StopReason {get;init;}
    public int Queued { get; init; }
    public bool NeedsRescan { get; init; }
}

static class StorageMigrationRunner
{
    public static async Task<StorageMigrationReport> Run(SqlConnection connection,bool apply,CancellationToken ct,Action<string>? log=null)
    {
        if(!apply)return await RunCore(connection,false,ct,log);
        await using var acquire=new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'registro-storage-migration-v1',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @result;",connection);
        if(Convert.ToInt32(await acquire.ExecuteScalarAsync(ct))<0)throw new StorageServiceException("STORAGE_MIGRATION_RUNNING","Ya hay una copia de archivos en curso en otro proceso.",409);
        try{return await RunCore(connection,true,ct,log);}
        finally
        {
            await using var release=new SqlCommand("EXEC sys.sp_releaseapplock @Resource=N'registro-storage-migration-v1',@LockOwner='Session';",connection);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
    static async Task<StorageMigrationReport> RunCore(SqlConnection connection,bool apply,CancellationToken ct,Action<string>? log)
    {
    if (apply)
    {
        if (!StorageFiles.Enabled) throw new InvalidOperationException("Activa STORAGE_ENABLED antes de migrar.");
        if(StorageFiles.Queue is null) { var who = await StorageFiles.Required().WhoAmI(ct); log?.Invoke("Servicio autenticado: " + who["tenant"]); }
        else log?.Invoke("Preparando cola persistente; no se requiere subir durante el inventario.");
        await using var schema = new SqlCommand("""
            IF OBJECT_ID(N'dbo.storage_migration_files',N'U') IS NULL
              CREATE TABLE dbo.storage_migration_files(
                migration_key char(64) NOT NULL PRIMARY KEY,
                source_table nvarchar(260) NOT NULL, source_column nvarchar(128) NOT NULL,
                source_key nvarchar(max) NOT NULL, source_sha256 char(64) NOT NULL,
                reference_json nvarchar(max) NOT NULL,
                verified_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());
            IF OBJECT_ID(N'dbo.storage_migration_pending',N'U') IS NULL
              CREATE TABLE dbo.storage_migration_pending(
                migration_key char(64) NOT NULL PRIMARY KEY,
                source_table nvarchar(260) NOT NULL, source_column nvarchar(128) NOT NULL,
                source_key nvarchar(max) NOT NULL, source_sha256 char(64) NOT NULL,
                reference_json nvarchar(max) NOT NULL,
                queued_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());
            """, connection);
        await schema.ExecuteNonQueryAsync(ct);
    }
    var sources = new List<Source>();
    await using (var discovery = new SqlCommand("""
        SELECT s.name,t.name,c.name,ty.name,
          (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(kc.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
           FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
           JOIN sys.columns kc ON kc.object_id=ic.object_id AND kc.column_id=ic.column_id
           WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.key_ordinal>0) keys
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
        JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
        WHERE t.name<>'storage_migration_files' AND
          ((ty.name IN('varbinary','image') AND c.name IN('file_data','photo_data','avatar_data'))
           OR (ty.name IN('nvarchar','varchar') AND c.name IN('signature_data','metadata','content')))
        ORDER BY s.name,t.name,c.name;
        """, connection))
    await using (var reader = await discovery.ExecuteReaderAsync(ct))
        while (await reader.ReadAsync(ct)) sources.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetString(4)));
    var report = new List<object>(); int copied=0, existing=0, skipped=0, failed=0, candidates=0, queued=0;
    foreach (var source in sources)
    {
        if (source.Keys is null) { report.Add(new { source=source.Table, column=source.Column, error="Tabla sin clave primaria; revisar manualmente" }); failed++; continue; }
        var table = Q(source.Schema)+"."+Q(source.Table); var column = Q(source.Column);
        if(!apply)
        {
            // Inventory needs only magic bytes, never a full download of every
            // production blob. Scan in one streamed query per column.
            var binary=source.Type is "varbinary" or "image";
            var projection=binary?$"CONVERT(varbinary(16),SUBSTRING({column},1,16))":$"CASE WHEN CHARINDEX(N'data:',{column})>0 THEN N'data:' ELSE N'' END";
            await using var inventory=new SqlCommand($"SELECT {projection} FROM {table} WHERE {column} IS NOT NULL AND DATALENGTH({column})>0",connection){CommandTimeout=120};
            await using var rows=await inventory.ExecuteReaderAsync(ct);
            var count=0;
            while(await rows.ReadAsync(ct))
            {
                if(binary?StorageFiles.DetectMime((byte[])rows[0]) is not null:rows.GetString(0)=="data:")count++;
                else skipped++;
            }
            candidates+=count;
            report.Add(new{source=table,column=source.Column,candidates=count});
            log?.Invoke($"{table}.{source.Column}: {count} registros con archivos.");
            continue;
        }
        var keyExpression = $"(SELECT {source.Keys} FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)";
        string cursor = ""; int sourceCount=0;
        while (!ct.IsCancellationRequested)
        {
            object value; string rowKey;
            await using (var select = new SqlCommand($"SELECT TOP(1) {keyExpression} row_key,{column} FROM {table} WHERE {column} IS NOT NULL AND DATALENGTH({column})>0 AND {keyExpression} COLLATE Latin1_General_100_BIN2>@cursor COLLATE Latin1_General_100_BIN2 ORDER BY {keyExpression} COLLATE Latin1_General_100_BIN2",connection))
            {
                select.CommandTimeout=120;select.Parameters.AddWithValue("@cursor",cursor);
                await using var reader = await select.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) break;
                rowKey=reader.GetString(0);value=reader.GetValue(1);
            }
            cursor=rowKey;
            byte[] original = value is byte[] bytes ? bytes : Encoding.UTF8.GetBytes((string)value);
            var text = value as string;
            if (value is byte[] binary && (StorageFiles.IsReference(binary) || StorageFiles.DetectMime(binary) is null)) { skipped++; continue; }
            if (text is not null && !text.Contains("data:",StringComparison.Ordinal)) { skipped++; continue; }
            sourceCount++; candidates++;
            if (!apply) continue;
            var hash=StorageFiles.Hash(original);
            var migrationKey=StorageFiles.Hash(Encoding.UTF8.GetBytes(table+"|"+source.Column+"|"+rowKey+"|"+hash));
            await using(var find=new SqlCommand("SELECT COUNT(*) FROM dbo.storage_migration_files WHERE migration_key=@key",connection))
            {
                find.Parameters.AddWithValue("@key",migrationKey);
                if(Convert.ToInt32(await find.ExecuteScalarAsync(ct))>0){existing++;continue;}
            }
            try
            {
                using var origin=StorageOrigin.Scope("legacy","unknown");
                string reference;
                await using var pending = new SqlCommand("SELECT reference_json FROM dbo.storage_migration_pending WHERE migration_key=@key", connection);
                pending.Parameters.AddWithValue("@key", migrationKey);
                var previouslyQueued = await pending.ExecuteScalarAsync(ct) as string;
                if (previouslyQueued is not null) reference = previouslyQueued;
                else if (text is null) reference=Encoding.UTF8.GetString(await StorageFiles.Store(original,"legacy-"+source.Table,ct));
                else if (text.StartsWith("data:",StringComparison.Ordinal)) reference=(await StorageFiles.StoreDataUrl(text,ct))!;
                else reference=(await StorageFiles.TransformJson(text,true,ct))!;
                if (!reference.Contains(StorageFiles.Prefix,StringComparison.Ordinal)) { skipped++;continue; }
                var resolved = await StorageFiles.ResolveQueuedReferences(reference, ct);
                if (resolved is null)
                {
                    await using var enqueue = new SqlCommand("INSERT dbo.storage_migration_pending(migration_key,source_table,source_column,source_key,source_sha256,reference_json) SELECT @key,@table,@column,@source,@hash,@reference WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_pending WITH(UPDLOCK,HOLDLOCK) WHERE migration_key=@key)", connection);
                    enqueue.Parameters.AddWithValue("@key", migrationKey); enqueue.Parameters.AddWithValue("@table", table); enqueue.Parameters.AddWithValue("@column", source.Column); enqueue.Parameters.AddWithValue("@source", rowKey); enqueue.Parameters.AddWithValue("@hash", hash); enqueue.Parameters.AddWithValue("@reference", reference);
                    await enqueue.ExecuteNonQueryAsync(ct); queued++;
                    if (queued % 50 == 0) log?.Invoke($"En cola: {queued}; verificados: {copied}; existentes: {existing}");
                    continue;
                }
                reference = resolved;
                await using var journal=new SqlCommand("INSERT dbo.storage_migration_files(migration_key,source_table,source_column,source_key,source_sha256,reference_json) SELECT @key,@table,@column,@source,@hash,@reference WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files WITH(UPDLOCK,HOLDLOCK) WHERE migration_key=@key)",connection);
                journal.Parameters.AddWithValue("@key",migrationKey);journal.Parameters.AddWithValue("@table",table);journal.Parameters.AddWithValue("@column",source.Column);journal.Parameters.AddWithValue("@source",rowKey);journal.Parameters.AddWithValue("@hash",hash);journal.Parameters.AddWithValue("@reference",reference);
                await journal.ExecuteNonQueryAsync(ct);copied++;
                if(copied%10==0)log?.Invoke($"Verificados: {copied}; existentes: {existing}; errores: {failed}");
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(StorageServiceException full) when(full.Code is "STORAGE_QUEUE_FULL" or "STORAGE_QUEUE_BUSY")
            {
                return new(candidates,copied,existing,skipped,failed,report){Queued=queued,NeedsRescan=true,StopReason=full.Message};
            }
            catch(Exception disk) when(disk is IOException or UnauthorizedAccessException)
            {
                return new(candidates,copied,existing,skipped,failed+1,report){Queued=queued,StopReason="No se pudo conservar otro temporal. Revisa el espacio y los permisos de la carpeta privada; los originales siguen intactos."};
            }
            catch(Exception error)
            {
                failed++;var code=error is StorageServiceException storage?storage.Code:error.GetType().Name;
                report.Add(new{source=table,column=source.Column,key=rowKey,error=code});
                log?.Invoke($"Pendiente en {table}.{source.Column}: {code}");
                if(error is StorageServiceException { Status:503 } unavailable)
                    return new(candidates,copied,existing,skipped,failed,report){Queued=queued,StopReason=unavailable.Message};
            }
        }
        log?.Invoke($"{table}.{source.Column}: {sourceCount} registros con archivos.");
    }
        return new(candidates,copied,existing,skipped,failed,report){Queued=queued};
    }
    // Finalize metadata only: do not repeatedly download thousands of SQL blobs
    // while the external provider is paused. Originals are never updated.
    public static async Task<(int Pending, int Verified)> FinalizePending(SqlConnection connection, CancellationToken ct)
    {
        var rows = new List<(string Key,string Reference)>();
        await using(var select = new SqlCommand("SELECT p.migration_key,p.reference_json FROM dbo.storage_migration_pending p WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files f WHERE f.migration_key=p.migration_key)", connection))
        await using(var reader = await select.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) rows.Add((reader.GetString(0),reader.GetString(1)));
        foreach(var row in rows)
        {
            var resolved = await StorageFiles.ResolveQueuedReferences(row.Reference, ct);
            if(resolved is null) continue;
            await using var complete = new SqlCommand("INSERT dbo.storage_migration_files(migration_key,source_table,source_column,source_key,source_sha256,reference_json) SELECT migration_key,source_table,source_column,source_key,source_sha256,@reference FROM dbo.storage_migration_pending p WHERE migration_key=@key AND NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files WITH(UPDLOCK,HOLDLOCK) WHERE migration_key=@key)",connection);
            complete.Parameters.AddWithValue("@key",row.Key); complete.Parameters.AddWithValue("@reference",resolved);
            await complete.ExecuteNonQueryAsync(ct);
        }
        await using var count = new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.storage_migration_pending p WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files f WHERE f.migration_key=p.migration_key)),(SELECT COUNT(*) FROM dbo.storage_migration_files)",connection);
        await using var counts = await count.ExecuteReaderAsync(ct); await counts.ReadAsync(ct); return (counts.GetInt32(0),counts.GetInt32(1));
    }
    static string Q(string name)=>"["+name.Replace("]","]]")+"]";
    public record ReleaseStatus(int Released,long BytesFreed,int Pending,int Changed,string? Message=null,DateTime? NextAttempt=null);
    // Re-download every remote reference before removing the corresponding SQL bytes.
    // A transaction locks the current source value and atomically records its replacement.
    public static async Task<ReleaseStatus> ReleaseVerified(SqlConnection connection,CancellationToken ct)
    {
        await using var acquire=new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'registro-storage-release-v2',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;",connection);
        if(Convert.ToInt32(await acquire.ExecuteScalarAsync(ct))<0)throw new StorageServiceException("STORAGE_MIGRATION_RUNNING","La liberación ya está en curso en otro proceso.");
        try{return await ReleaseCore(connection,ct);}
        finally{await using var release=new SqlCommand("EXEC sys.sp_releaseapplock @Resource=N'registro-storage-release-v2',@LockOwner='Session';",connection);await release.ExecuteNonQueryAsync(CancellationToken.None);}
    }
    static async Task<ReleaseStatus> ReleaseCore(SqlConnection connection,CancellationToken ct)
    {
        await using(var schema=new SqlCommand("""
            IF OBJECT_ID(N'dbo.storage_migration_files',N'U') IS NOT NULL
            AND COL_LENGTH('dbo.storage_migration_files','release_state') IS NULL
            BEGIN
              ALTER TABLE dbo.storage_migration_files ADD release_state varchar(20) NULL,
                released_at datetime2(3) NULL, released_bytes bigint NULL;
            END
            """,connection)) await schema.ExecuteNonQueryAsync(ct);
        await using(var exists=new SqlCommand("SELECT OBJECT_ID(N'dbo.storage_migration_files',N'U')",connection))
            if(await exists.ExecuteScalarAsync(ct) is DBNull)return new(0,0,0,0);
        var rows=new List<(string Key,string Table,string Column,string Source,string Hash,string Reference)>();
        await using(var select=new SqlCommand("SELECT TOP(10) migration_key,source_table,source_column,source_key,source_sha256,reference_json FROM dbo.storage_migration_files WHERE release_state IS NULL ORDER BY verified_at,migration_key",connection))
        await using(var reader=await select.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) rows.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5)));
        string? message=null;DateTime? nextAttempt=null;
        foreach(var row in rows)
        {
            // Resolve identifiers exclusively through the database catalog.
            string table,column,keys,type;
            await using(var catalog=new SqlCommand("""
                SELECT QUOTENAME(s.name)+'.'+QUOTENAME(t.name),QUOTENAME(c.name),ty.name,
                  (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(kc.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
                   FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                   JOIN sys.columns kc ON kc.object_id=ic.object_id AND kc.column_id=ic.column_id
                   WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.key_ordinal>0)
                FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
                JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
                WHERE t.object_id=OBJECT_ID(@table) AND c.name=@column
                AND ((ty.name IN('varbinary','image') AND c.name IN('file_data','photo_data','avatar_data'))
                OR (ty.name IN('nvarchar','varchar') AND c.name IN('signature_data','metadata','content')))
                """,connection))
            {
                catalog.Parameters.AddWithValue("@table",row.Table);catalog.Parameters.AddWithValue("@column",row.Column);
                await using var reader=await catalog.ExecuteReaderAsync(ct);
                if(!await reader.ReadAsync(ct)||reader.IsDBNull(3))throw new InvalidOperationException("Origen de migración no reconocido");
                table=reader.GetString(0);column=reader.GetString(1);type=reader.GetString(2);keys=reader.GetString(3);
            }
            try
            {
                if(!row.Reference.Contains(StorageFiles.Prefix,StringComparison.Ordinal)||row.Reference.Contains("queue:",StringComparison.Ordinal))
                    throw new StorageServiceException("STORAGE_VERIFY","La copia todavía no tiene una referencia remota definitiva.");
                if(row.Reference.StartsWith(StorageFiles.Prefix,StringComparison.Ordinal))await StorageFiles.Read(Encoding.UTF8.GetBytes(row.Reference),ct);
                else await StorageFiles.TransformJson(row.Reference,false,ct);
            }
            catch(StorageServiceException e){message=e.Message;nextAttempt=DateTime.UtcNow.Add(e.RetryAfter>TimeSpan.FromMinutes(1)?e.RetryAfter.Value:TimeSpan.FromMinutes(1));break;}
            var binary=type is "varbinary" or "image";
            var expression=$"(SELECT {keys} FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2=@source COLLATE Latin1_General_100_BIN2";
            await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(ct);
            object? original;long oldSize=0;
            await using(var select=new SqlCommand($"SELECT {column},CONVERT(bigint,DATALENGTH({column})) FROM {table} WITH(UPDLOCK,HOLDLOCK) WHERE {expression}",connection,tx))
            {
                select.Parameters.AddWithValue("@source",row.Source);
                await using var reader=await select.ExecuteReaderAsync(ct);
                if(await reader.ReadAsync(ct)){original=reader.GetValue(0);oldSize=reader.IsDBNull(1)?0:reader.GetInt64(1);}else original=null;
            }
            var bytes=original is byte[] b?b:original is string s?Encoding.UTF8.GetBytes(s):Array.Empty<byte>();
            var matches=StorageFiles.Hash(bytes)==row.Hash;
            var same=Encoding.UTF8.GetString(bytes)==row.Reference;
            long freed=0;
            if(matches)
            {
                await using var update=new SqlCommand($"UPDATE {table} SET {column}=@reference WHERE {expression}",connection,tx);
                update.Parameters.AddWithValue("@reference",binary?Encoding.UTF8.GetBytes(row.Reference):(object)row.Reference);
                update.Parameters.AddWithValue("@source",row.Source);
                if(await update.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Origen no único");
                freed=Math.Max(0,oldSize-(binary?Encoding.UTF8.GetByteCount(row.Reference):type=="nvarchar"?row.Reference.Length*2L:Encoding.UTF8.GetByteCount(row.Reference)));
            }
            await using(var mark=new SqlCommand("UPDATE dbo.storage_migration_files SET release_state=@state,released_bytes=@bytes,released_at=SYSUTCDATETIME() WHERE migration_key=@key",connection,tx))
            {
                mark.Parameters.AddWithValue("@state",matches||same?"released":"changed");mark.Parameters.AddWithValue("@bytes",freed);mark.Parameters.AddWithValue("@key",row.Key);
                await mark.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }
        await using var totals=new SqlCommand("SELECT COUNT(CASE WHEN release_state='released' THEN 1 END),COALESCE(SUM(released_bytes),0),COUNT(CASE WHEN release_state IS NULL THEN 1 END),COUNT(CASE WHEN release_state='changed' THEN 1 END) FROM dbo.storage_migration_files",connection);
        await using var result=await totals.ExecuteReaderAsync(ct);await result.ReadAsync(ct);
        return new(result.GetInt32(0),result.GetInt64(1),result.GetInt32(2),result.GetInt32(3),message,nextAttempt);
    }
    record Source(string Schema,string Table,string Column,string Type,string? Keys);
}
