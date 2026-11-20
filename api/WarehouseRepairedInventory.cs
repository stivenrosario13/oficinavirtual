using System.Text;
using Microsoft.Data.SqlClient;
partial class Database
{
 static readonly SemaphoreSlim RepairedInventorySchemaLock=new(1,1);
 static async Task EnsureRepairedInventorySchema(SqlConnection c,CancellationToken ct)
 {
  await RepairedInventorySchemaLock.WaitAsync(ct);try{
   await using var cmd=new SqlCommand("""
    IF OBJECT_ID(N'dbo.warehouse_repaired_receipts',N'U') IS NULL CREATE TABLE dbo.warehouse_repaired_receipts(movement_id uniqueidentifier PRIMARY KEY,source_id uniqueidentifier NOT NULL);
    IF OBJECT_ID(N'dbo.maintenance_workshop_events',N'U') IS NOT NULL
     EXEC(N'INSERT dbo.warehouse_repaired_receipts(movement_id,source_id) SELECT e.movement_id,MIN(CONVERT(varchar(36),e.source_id)) FROM dbo.maintenance_workshop_events e JOIN dbo.maintenance_movements m ON m.id=e.movement_id WHERE e.action IN(''DELIVER'',''WAREHOUSE_RECEIVE'') AND m.operational_area=''WAREHOUSE'' AND m.movement_type=''ENTRY'' AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts r WHERE r.movement_id=e.movement_id) GROUP BY e.movement_id');
    """,c);await cmd.ExecuteNonQueryAsync(ct);
  }finally{RepairedInventorySchemaLock.Release();}
 }
 public async Task<string> WarehouseRepairedInventory(CancellationToken ct)
 {
  await using var c=await Open(ct);await EnsureMaintenanceSchema(c,ct);await EnsureRepairedInventorySchema(c,ct);
  await using var cmd=new SqlCommand("""
   WITH received AS(SELECT m.*,r.source_id,ROW_NUMBER() OVER(PARTITION BY m.department,UPPER(LTRIM(RTRIM(m.serial_number))) ORDER BY m.occurred_at DESC,m.created_at DESC,m.id DESC) rn FROM dbo.warehouse_repaired_receipts r JOIN dbo.maintenance_movements m ON m.id=r.movement_id)
   SELECT m.id,m.source_id sourceId,m.department,m.equipment_type equipment,m.serial_number serial,m.document_number documentNumber,m.qr_token qrToken,m.received_by_name receivedBy,m.occurred_at receivedAt,
    CASE WHEN m.quantity-COALESCE(x.used,0)>0 THEN m.quantity-COALESCE(x.used,0) ELSE 0 END available
   FROM received m OUTER APPLY(SELECT SUM(e.quantity) used FROM dbo.maintenance_movements e WHERE e.operational_area='WAREHOUSE' AND e.department=m.department AND UPPER(LTRIM(RTRIM(e.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND e.movement_type IN('EXIT','NEW_DELIVERY','TRANSFER_TO_WORKSHOP','DISCHARGE') AND e.occurred_at>=m.occurred_at) x
   WHERE m.rn=1 ORDER BY m.occurred_at DESC FOR JSON PATH;
   """,c);
  var json=new StringBuilder();await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))json.Append(reader.GetString(0));return json.Length==0?"[]":json.ToString();
 }
}
