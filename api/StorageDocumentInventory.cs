using Microsoft.Data.SqlClient;
using System.Text;

record StorageDocumentRow(string Category,string Profile,int Images,int Pdfs,long Bytes,int Pending,int Verified,int Review,int InSql);
static class StorageDocumentInventory
{
    // Only small storage references and classification fields leave SQL here.
    // Historical documents are classified from their saved structured metadata.
    public static async Task<List<StorageDocumentRow>> Read(SqlConnection connection,StorageUploadQueue queue,CancellationToken ct)
    {
        var items=queue.Inventory();
        var local=items.ToDictionary(x=>"queue:"+x.Id);
        var remote=items.Where(x=>x.Remote is not null).GroupBy(x=>x.Remote!.Id).ToDictionary(g=>g.Key,g=>g.First());
        var files=new List<(string category,string profile,string key,string mime,long size,string state)>();
        await using var cmd=new SqlCommand("""
            IF OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL RETURN;
            SELECT id,
              CASE WHEN JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL THEN 'warehouse-request'
                   WHEN JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL THEN 'administrative-form'
                   WHEN JSON_VALUE(metadata,'$.templateId') IS NOT NULL THEN 'template-form'
                   ELSE 'document-form' END category,
              CASE WHEN SUBSTRING(file_data,1,@length)=@prefix AND DATALENGTH(file_data)<4096 THEN file_data ELSE NULL END reference,
              JSON_VALUE(metadata,'$.contentType') mime,
              COALESCE(TRY_CONVERT(bigint,JSON_VALUE(metadata,'$.size')),DATALENGTH(file_data)) original_size
            FROM dbo.institutional_files
            WHERE JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL
               OR JSON_QUERY(metadata,'$.administrativeRequest') IS NOT NULL
               OR JSON_VALUE(metadata,'$.templateId') IS NOT NULL
               OR (area='communications' AND channel='requests');
            """,connection){CommandTimeout=20};
        var prefix=Encoding.UTF8.GetBytes(StorageFiles.Prefix);cmd.Parameters.AddWithValue("@length",prefix.Length);cmd.Parameters.AddWithValue("@prefix",prefix);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
        {
            var category=reader.GetString(1);var mime=reader.IsDBNull(3)?"":reader.GetString(3);
            if(mime!="application/pdf"&&!mime.StartsWith("image/",StringComparison.Ordinal))continue;
            var profile="unknown";var key="sql:"+reader.GetInt64(0);var state="sql";var size=reader.GetInt64(4);
            if(!reader.IsDBNull(2))
            {
                var reference=StorageFiles.Decode((byte[])reader[2]);key=reference.Id;mime=reference.Mime;size=reference.Size;
                var item=StorageFiles.IsQueued(reference)?local.GetValueOrDefault(reference.Id):remote.GetValueOrDefault(reference.Id);
                if(item is not null)
                {
                    profile=item.Origin?.Group??"unknown";
                    state=item.Remote is not null?"verified":item.State=="review"?"review":"pending";
                }
                else state=StorageFiles.IsQueued(reference)?"pending":"verified";
            }
            files.Add((category,profile,key,mime,size,state));
        }
        // A document can be referenced more than once. Count each stored file once per category/profile.
        return files.DistinctBy(f=>(f.category,f.profile,f.key)).GroupBy(f=>(f.category,f.profile))
            .Select(g=>new StorageDocumentRow(g.Key.category,g.Key.profile,g.Count(f=>f.mime.StartsWith("image/")),g.Count(f=>f.mime=="application/pdf"),g.Sum(f=>f.size),g.Count(f=>f.state=="pending"),g.Count(f=>f.state=="verified"),g.Count(f=>f.state=="review"),g.Count(f=>f.state=="sql")))
            .OrderBy(r=>r.Category).ThenBy(r=>r.Profile).ToList();
    }
}
