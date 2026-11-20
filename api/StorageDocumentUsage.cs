partial class Database
{
    public async Task<List<StorageDocumentRow>> StorageDocuments(StorageUploadQueue queue,CancellationToken ct)
    {await using var connection=await Open(ct);return await StorageDocumentInventory.Read(connection,queue,ct);}
}

sealed class StorageDocumentUsage(Database database,StorageUploadQueue queue)
{
    readonly SemaphoreSlim gate=new(1,1);
    List<StorageDocumentRow>? rows;
    DateTime? checkedAt;
    DateTime nextRead;
    string? error;
    public async Task<object> Read(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if(DateTime.UtcNow>=nextRead)
            {
                nextRead=DateTime.UtcNow.AddSeconds(30);
                try{rows=await database.StorageDocuments(queue,ct);checkedAt=DateTime.UtcNow;error=null;}
                catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
                catch(Exception){error="No se pudo actualizar el detalle de solicitudes y formularios. Se conserva la última lectura disponible.";}
            }
            return new{rows,checkedAt,error};
        }
        finally{gate.Release();}
    }
}
