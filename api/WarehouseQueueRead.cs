using System.Text.Json;
using Microsoft.Data.SqlClient;

record WarehouseQueueItem(long Id,string Title,string OriginalFileName,string Department,string RecipientDepartment,string OwnerUserName,string OwnerDisplayName,string Status,DateTime CreatedUtc,DateTime? DecisionUtc,string? DecisionBy);

partial class Database
{
    public async Task<List<WarehouseQueueItem>> InstitutionalWarehouseQueue(DocumentActor actor,CancellationToken ct)
    {
        if(!actor.Allowed)return [];
        await using var connection=await Open(ct);
        await EnsureInstitutionalSchema(connection,ct);
        // The queue needs routing/status metadata, never PDFs, signatures, or form images.
        // Strip legacy inline assets in SQL too, before they enter application memory.
        await using var command=new SqlCommand("""
            SELECT id,JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(metadata,'$.receiptSignature',NULL),'$.history',NULL),'$.administrativeRequest.formData',NULL)
            FROM dbo.institutional_files
            WHERE area='communications' AND channel='requirements'
              AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'
              AND COALESCE(JSON_VALUE(metadata,'$.trashed'),'false')<>'true'
            ORDER BY id DESC
            """,connection);
        var result=new List<WarehouseQueueItem>();
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
        {
            var document=JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1),InstitutionalJson);
            if(document is null||document.Trashed||!document.Accessible(actor))continue;
            document.Id=reader.GetInt64(0);document.NormalizeWarehouseReceiptState();
            result.Add(new(document.Id,document.Title,document.OriginalFileName,document.Department,"Almacén",document.OwnerUserName,document.OwnerDisplayName,document.Status,document.CreatedUtc,document.DecisionUtc,document.DecisionBy));
        }
        return result;
    }
}
