using Microsoft.Data.SqlClient;
using System.Text.Json;

// Explicit, one-off maintenance after the verified migration; never a web job.
static class StorageSpaceReclaimer
{
    public static async Task<int> Run(SqlConnection connection,CancellationToken ct)
    {
        await using(var guard=new SqlCommand("""
            IF DB_NAME()<>N'db60703' THROW 51000,'Unexpected database',1;
            IF CONVERT(int,SERVERPROPERTY('ProductMajorVersion'))<16 THROW 51000,'Low priority shrink unavailable',1;
            IF IS_MEMBER('db_owner')<>1 THROW 51000,'Database owner required',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files WHERE release_state='released') THROW 51000,'No verified release',1;
            IF EXISTS(SELECT 1 FROM dbo.storage_migration_files WHERE release_state IS NULL) THROW 51000,'Release is not finished',1;
            DECLARE @result int;
            EXEC @result=sys.sp_getapplock @Resource=N'registro-storage-migration-v1',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0;
            IF @result<0 THROW 51000,'Migration is active',1;
            """,connection)){await guard.ExecuteNonQueryAsync(ct);}
        var events=new List<object>();
        var reportPath=Path.GetFullPath($"outputs/sql-space-reclaimed-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        async Task Save()=>await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(events,new JsonSerializerOptions{WriteIndented=true}),CancellationToken.None);
        async Task<List<Dictionary<string,object?>>> Read(string sql)
        {
            var rows=new List<Dictionary<string,object?>>();
            await using var command=new SqlCommand(sql,connection){CommandTimeout=30};
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct)){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}
            return rows;
        }
        const string filesSql="SELECT file_id,name,type_desc,CONVERT(bigint,size)*8192 allocated_bytes,CONVERT(bigint,FILEPROPERTY(name,'SpaceUsed'))*8192 used_bytes FROM sys.database_files";
        const string rowsSql="SELECT s.name+'.'+t.name table_name,SUM(p.rows) row_count FROM sys.tables t JOIN sys.schemas s ON t.schema_id=s.schema_id JOIN sys.partitions p ON t.object_id=p.object_id AND p.index_id IN(0,1) GROUP BY s.name,t.name ORDER BY s.name,t.name";
        try
        {
            var before=await Read(filesSql);
            events.Add(new{phase="before",checkedAt=DateTime.UtcNow,files=before,rows=await Read(rowsSql)});await Save();
            var files=before.Where(r=>Equals(r["type_desc"],"ROWS")).ToArray();
            if(files.Length!=1||Convert.ToInt32(files[0]["file_id"])!=1)throw new InvalidOperationException("Unexpected file layout");
            var used=Convert.ToInt64(files[0]["used_bytes"]);
            var allocated=Convert.ToInt64(files[0]["allocated_bytes"]);
            // Retain at least 128 MiB and 25% of current used space as headroom.
            var targetMiB=(int)(Math.Ceiling((used+Math.Max(128L*1024*1024,used/4))/(64d*1024*1024))*64);
            if(allocated-targetMiB*1024L*1024<128L*1024*1024)throw new InvalidOperationException("Insufficient unused space for this maintenance");
            Console.WriteLine(JsonSerializer.Serialize(new{phase="plan",targetMiB,allocatedBytes=allocated,usedBytes=used,report=reportPath}));
            foreach(var sql in new[]{"DBCC SHRINKFILE (1, TRUNCATEONLY) WITH WAIT_AT_LOW_PRIORITY (ABORT_AFTER_WAIT=SELF), NO_INFOMSGS;",$"DBCC SHRINKFILE (1, {targetMiB}) WITH WAIT_AT_LOW_PRIORITY (ABORT_AFTER_WAIT=SELF), NO_INFOMSGS;"})
            {
                var current=await Read(filesSql);
                if(Convert.ToInt64(current.Single(r=>Convert.ToInt32(r["file_id"])==1)["allocated_bytes"])<=targetMiB*1024L*1024)break;
                Console.WriteLine("Reclaiming unused data-file space; user sessions are not terminated.");
                await using var command=new SqlCommand(sql,connection){CommandTimeout=180};
                await command.ExecuteNonQueryAsync(ct);
                events.Add(new{phase="shrink",checkedAt=DateTime.UtcNow,command=sql,files=await Read(filesSql)});await Save();
            }
            string physicalCheck="completed";
            try
            {
                await using var check=new SqlCommand("DBCC CHECKDB WITH PHYSICAL_ONLY, NO_INFOMSGS;",connection){CommandTimeout=120};
                await check.ExecuteNonQueryAsync(ct);
            }
            catch(SqlException error) when(error.Number==5030)
            { physicalCheck="not completed: SQL 5030; host could not acquire the required exclusive lock"; }
            var after=await Read(filesSql);
            var allocatedAfter=Convert.ToInt64(after.Single(r=>Convert.ToInt32(r["file_id"])==1)["allocated_bytes"]);
            events.Add(new{phase="after",checkedAt=DateTime.UtcNow,files=after,rows=await Read(rowsSql),physicalCheck,reclaimedBytes=allocated-allocatedAfter});await Save();
            Console.WriteLine(JsonSerializer.Serialize(new{allocatedBefore=allocated,allocatedAfter,reclaimedBytes=allocated-allocatedAfter,physicalCheck,report=reportPath}));
            return physicalCheck=="completed"?0:2;
        }
        catch(Exception error)
        {
            events.Add(new{phase="interrupted",checkedAt=DateTime.UtcNow,error=error is SqlException sql?$"SQL {sql.Number}":error.GetType().Name});await Save();throw;
        }
        finally
        {
            await using var release=new SqlCommand("EXEC sys.sp_releaseapplock @Resource=N'registro-storage-migration-v1',@LockOwner='Session'",connection);
            await release.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
