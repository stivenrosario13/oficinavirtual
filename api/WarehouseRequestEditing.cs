using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

static class WarehouseRequestEditing
{
    // This capability belongs to these two authenticated accounts, not to a
    // generic manager flag that may be absent from an older saved session.
    public static bool Editor(DocumentActor actor) => !actor.WarehouseRequestOnly && AdministrativeProfiles.Login(actor.Login) is "diorly.abreu" or "ana.rodriguez";
    public static bool CanEdit(InstitutionalFile d,DocumentActor actor) => Editor(actor)&&!d.Trashed&&d.WarehouseRequest is not null&&d.RecipientDepartment=="Almacén"&&d.WarehouseDelivery is null&&d.WarehouseAssigned.Count==0&&d.Status is "pending" or "received" or "correction_requested" or "rejected" or "approved";
}
record WarehouseRequestEdit(int Revision,string Note,List<WarehouseRequestLine> Lines);

partial class Database
{
    public async Task<string?> WarehouseEditRequest(long id,DocumentActor actor,WarehouseRequestEdit input,byte[] pdf,CancellationToken ct)
    {
        if(!WarehouseRequestEditing.Editor(actor))return "Solo Diorly Abreu y Ana Rodríguez pueden editar solicitudes de Almacén.";
        if(input.Note?.Trim().Length is not (>=5 and <=2000)||input.Lines is null||input.Lines.Count is <1 or >30||input.Lines.Any(x=>x is null||string.IsNullOrWhiteSpace(x.ProductId)||x.Quantity is <1 or >10000||x.Note?.Length>500)||input.Lines.Sum(x=>x.Quantity)>10000)return "Selecciona artículos del inventario, cantidades válidas y el motivo del ajuste.";
        await using var c=await Open(ct);await EnsureMaintenanceSchema(c,ct);await EnsureInstitutionalSchema(c,ct);await EnsureWarehouseFulfillmentSchema(c,ct);await EnsureRepairedInventorySchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",c,tx);read.Parameters.AddWithValue("@id",id);
        var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Solicitud no encontrada.";
        var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;
        if(!WarehouseRequestEditing.CanEdit(d,actor))return "La solicitud ya está preparada, entregada o no admite cambios.";
        if(d.WarehouseRevision!=input.Revision)return "La solicitud cambió. Vuelve a abrirla antes de editar.";
        await using(var stockLock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",c,tx))await stockLock.ExecuteScalarAsync(ct);
        var lines=new List<WarehouseRequestLine>();
        foreach(var group in input.Lines.GroupBy(x=>x.ProductId!.Trim(),StringComparer.OrdinalIgnoreCase))
        {
            var stock=await WarehouseProductBalance(c,tx,group.Key,ct);
            if(stock is null||stock.Value.Available<group.Sum(x=>x.Quantity))return "Uno de los artículos no existe o no tiene suficientes unidades disponibles. Actualiza el inventario.";
            // Require canonical labels so the submitted PDF cannot use a different article name.
            if(group.Any(x=>x.Name!=stock.Value.Name))return "El nombre del artículo cambió. Actualiza el inventario.";
        }
        foreach(var line in input.Lines)lines.Add(line with{Kind="EQUIPMENT",ProductId=line.ProductId!.Trim(),Serial=null});
        d.History.Add(new("edit_warehouse",actor.Name,actor.Login,DateTime.UtcNow,d.Status,d.ReceiptCode,d.ReceiverLogin,d.ReceivedUtc,d.ReceiptSignature,PreviousWarehouseRequest:d.WarehouseRequest,Note:input.Note.Trim()));
        d.WarehouseRequest=d.WarehouseRequest! with{Lines=lines};d.WarehouseRevision++;
        d.Status="pending";d.ReceivedUtc=null;d.ReceivedByUserName=null;d.ReceiverDisplayName=null;d.ReceiverLogin=null;d.ReceiptCode=null;d.VerificationToken=null;d.ReceiptSignature=null;d.DecisionBy=null;d.DecisionUtc=null;d.CorrectionNote=null;
        d.WarehouseAssignmentDraft=[];d.WarehousePendingPurchaseLines=[];d.WarehousePurchaseNote=null;d.Size=pdf.Length;
        await using var save=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id",c,tx);
        save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@json",await StorageFiles.TransformJson(JsonSerializer.Serialize(d,InstitutionalJson),true,ct));save.Parameters.AddWithValue("@bytes",await StorageFiles.Store(pdf,"institutional-document",ct));await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
    static async Task<(string Name,int Available)?> WarehouseProductBalance(SqlConnection c,SqlTransaction tx,string product,CancellationToken ct)
    {
        await using var cmd=new SqlCommand("""
          SELECT p.description,p.official_stock+
          COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at)),0)-
          COALESCE((SELECT SUM(a.quantity) FROM dbo.warehouse_equipment_allocations a WITH(UPDLOCK,HOLDLOCK) WHERE (a.product_id=p.product_id OR (a.product_id IS NULL AND LOWER(a.equipment_name)=LOWER(p.description))) AND a.status='PENDING_PICKUP'),0)
          FROM dbo.warehouse_products p WITH(HOLDLOCK) WHERE p.is_active=1 AND p.product_id=@product
          """,c,tx);cmd.Parameters.AddWithValue("@product",product);await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?(r.GetString(0),r.GetInt32(1)):null;
    }
}
