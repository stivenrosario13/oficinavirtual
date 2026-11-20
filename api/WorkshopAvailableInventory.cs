using System.Data;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
record WorkshopAvailableIntake(Guid BatchId,string Equipment,string[] Serials,string? Notes=null);
record WorkshopAvailableInventoryDeletion(string Reason);
partial class Database
{
 public async Task<string> WorkshopAvailableInventory(CancellationToken ct)
 {
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);await EnsureWorkshopStockExitSchema(c,ct);
  await using var cmd=new SqlCommand("""
   SELECT * FROM (
    SELECT id,equipment,serial,notes,created_at createdAt,created_by_name createdBy,CAST('DIRECT' AS varchar(20)) origin FROM dbo.workshop_available_inventory
    UNION ALL
    SELECT m.id,m.equipment_type,m.serial_number,m.notes,w.updated_at,m.created_by_name,'REPAIRED' FROM dbo.maintenance_workshop_cases w JOIN dbo.maintenance_movements m ON m.id=w.source_id WHERE w.status='INVENTORY' AND NOT EXISTS(SELECT 1 FROM dbo.workshop_intake_deletions d WHERE d.source_id=w.source_id)
   ) inventory WHERE NOT EXISTS(SELECT 1 FROM dbo.workshop_stock_exits x WHERE x.stock_id=inventory.id) ORDER BY createdAt DESC FOR JSON PATH
   """,c);
  var json=new StringBuilder();await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))json.Append(reader.GetString(0));return json.Length==0?"[]":json.ToString();
 }
 public async Task<string?> CreateWorkshopAvailableInventory(WorkshopAvailableIntake body,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanOperate)return "No tienes permiso para registrar inventario de Taller.";
  if(body.BatchId==Guid.Empty||string.IsNullOrWhiteSpace(body.Equipment)||body.Equipment.Length>100||(body.Notes?.Length??0)>2000||body.Serials is null||body.Serials.Length is <1 or >100||body.Serials.Any(s=>string.IsNullOrWhiteSpace(s)||s.Trim().Length>120))return "Completa equipo y un serial por unidad (máximo 100).";
  var serials=body.Serials.Select(s=>s.Trim().ToUpperInvariant()).ToArray();if(serials.Distinct().Count()!=serials.Length)return "Los seriales no pueden repetirse.";
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='WorkshopIntake',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @lock<0 THROW 50000,'No se pudo bloquear el registro.',1;
   IF EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch)
   BEGIN SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch AND payload_hash=@hash) THEN N'' ELSE N'Este registro ya existe con otros datos.' END; RETURN; END;
   IF EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.workshop_available_inventory i ON UPPER(i.serial)=s.value)
    OR EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.maintenance_movements m ON UPPER(LTRIM(RTRIM(m.serial_number)))=s.value LEFT JOIN dbo.maintenance_workshop_cases w ON w.source_id=m.id WHERE m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' AND COALESCE(w.status,'SENT') NOT IN('DELIVERED','DISCHARGED'))
   BEGIN SELECT N'Un serial ya pertenece al inventario o a una orden de Taller.'; RETURN; END;
   INSERT dbo.workshop_intake_batches(batch_id,payload_hash) VALUES(@batch,@hash);
   INSERT dbo.workshop_available_inventory(id,batch_id,equipment,serial,notes,created_by,created_by_name)
   SELECT NEWID(),@batch,@equipment,value,@notes,@user,@name FROM OPENJSON(@serials);
   SELECT N'';
   """,c,tx);
  cmd.Parameters.AddWithValue("@batch",body.BatchId);cmd.Parameters.AddWithValue("@hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("STOCK:"+JsonSerializer.Serialize(body with{Serials=serials})))));cmd.Parameters.AddWithValue("@serials",JsonSerializer.Serialize(serials));cmd.Parameters.AddWithValue("@equipment",body.Equipment.Trim());cmd.Parameters.AddWithValue("@notes",Db(body.Notes));cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@name",actor.Name);
  var error=(string?)await cmd.ExecuteScalarAsync(ct);if(!string.IsNullOrEmpty(error)){await tx.RollbackAsync(ct);return error;}await tx.CommitAsync(ct);return null;
 }
 public async Task<string?> DeleteWorkshopAvailableInventory(Guid id,string reason,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanDeleteWorkshop)return "Solo el administrador principal y Vittini pueden eliminar inventario de Taller.";
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);await EnsureWorkshopStockExitSchema(c,ct);
  await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   IF EXISTS(SELECT 1 FROM dbo.workshop_stock_exits WITH(UPDLOCK,HOLDLOCK) WHERE stock_id=@id)
   BEGIN SELECT N'Este equipo ya tiene una salida registrada y no se puede eliminar.'; RETURN; END;
   IF NOT EXISTS(SELECT 1 FROM dbo.workshop_available_inventory WITH(UPDLOCK,HOLDLOCK) WHERE id=@id)
   BEGIN SELECT N'Solo se pueden eliminar equipos agregados directamente. Los equipos reparados conservan su historial.'; RETURN; END;
   INSERT dbo.workshop_available_inventory_deletions(id,stock_id,batch_id,equipment,serial,notes,original_created_at,original_created_by,original_created_by_name,reason,deleted_at,deleted_by,deleted_by_name,deleted_by_login)
   SELECT NEWID(),id,batch_id,equipment,serial,notes,created_at,created_by,created_by_name,@reason,SYSUTCDATETIME(),@user,@name,@login
   FROM dbo.workshop_available_inventory WHERE id=@id;
   DELETE dbo.workshop_available_inventory WHERE id=@id;
   SELECT N'';
   """,c,tx);
  cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@reason",reason);cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@name",actor.Name);cmd.Parameters.AddWithValue("@login",actor.Login);
  var error=(string?)await cmd.ExecuteScalarAsync(ct);if(!string.IsNullOrEmpty(error)){await tx.RollbackAsync(ct);return error;}await tx.CommitAsync(ct);return null;
 }
}
