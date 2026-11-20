using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

static class StoragePersistenceAudit
{
    public static async Task<int> Run(SqlConnection connection,CancellationToken ct)
    {
        var results=new List<object>();
        async Task Read(string label,string sql)
        {
            await using var command=new SqlCommand(sql,connection){CommandTimeout=30};
            var prefix=Encoding.UTF8.GetBytes(StorageFiles.Prefix);command.Parameters.AddWithValue("@prefix",prefix);command.Parameters.AddWithValue("@length",prefix.Length);
            await using var reader=await command.ExecuteReaderAsync(ct);var rows=new List<Dictionary<string,object?>>();
            while(await reader.ReadAsync(ct)){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}
            results.Add(new{label,rows});
        }
        var columns=new List<(string table,string column)>();
        await using(var cmd=new SqlCommand("SELECT QUOTENAME(s.name)+'.'+QUOTENAME(t.name),QUOTENAME(c.name) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE ty.name IN('varbinary','image') AND c.name IN('photo_data','file_data','avatar_data')",connection))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))while(await reader.ReadAsync(ct))columns.Add((reader.GetString(0),reader.GetString(1)));
        foreach(var (table,column) in columns)
            await Read(table+"."+column,$"SELECT COUNT_BIG(*) records,SUM(CASE WHEN SUBSTRING({column},1,@length)=@prefix THEN CONVERT(bigint,1) ELSE 0 END) storage_references,SUM(CASE WHEN SUBSTRING({column},1,5)=0x255044462D OR SUBSTRING({column},1,8)=0x89504E470D0A1A0A OR SUBSTRING({column},1,3)=0xFFD8FF OR SUBSTRING({column},1,4)=0x52494646 OR SUBSTRING({column},1,3)=0x474946 OR SUBSTRING({column},5,4)=0x66747970 THEN CONVERT(bigint,1) ELSE 0 END) raw_images_or_pdf,COALESCE(SUM(CONVERT(bigint,DATALENGTH({column}))),0) stored_bytes FROM {table}");
        await Read("documents_since_20261024_utc","""
            SELECT CASE WHEN JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL THEN 'warehouse-request' WHEN JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL THEN 'administrative-form' ELSE 'other-document' END category,
              COUNT_BIG(*) records,
              SUM(CASE WHEN SUBSTRING(file_data,1,@length)=@prefix THEN CONVERT(bigint,1) ELSE 0 END) storage_references,
              SUM(CONVERT(bigint,DATALENGTH(file_data))) sql_bytes,
              SUM(TRY_CONVERT(bigint,JSON_VALUE(metadata,'$.size'))) original_bytes,
              SUM(CASE WHEN metadata LIKE '%data:image/%base64,%' OR metadata LIKE '%data:application/pdf%base64,%' THEN 1 ELSE 0 END) embedded_file_payloads
            FROM dbo.institutional_files
            WHERE TRY_CONVERT(datetime2,JSON_VALUE(metadata,'$.createdUtc'))>='2026-10-24T00:00:00'
              AND (JSON_VALUE(metadata,'$.contentType')='application/pdf' OR JSON_VALUE(metadata,'$.contentType') LIKE 'image/%')
            GROUP BY CASE WHEN JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL THEN 'warehouse-request' WHEN JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL THEN 'administrative-form' ELSE 'other-document' END
            """);
        var path=Path.GetFullPath($"outputs/storage-persistence-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        var report=new{checkedAt=DateTime.UtcNow,results};await File.WriteAllTextAsync(path,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}),ct);
        Console.WriteLine(JsonSerializer.Serialize(new{report,path}));return 0;
    }
}
