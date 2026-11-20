using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

record WorkshopIntake(Guid BatchId,string Department,string Equipment,string Failure,string[] Serials,Guid? AgencyId=null,string? Notes=null);
record WorkshopIntakeDeletion(string Reason);
partial class Database
{
 public async Task<string?> CreateWorkshopIntake(WorkshopIntake body,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanOperate)return "Solo Vittini, Michael y el administrador principal pueden registrar equipos en Taller.";
  if(body.BatchId==Guid.Empty||body.Department is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES")||string.IsNullOrWhiteSpace(body.Equipment)||body.Equipment.Length>100||string.IsNullOrWhiteSpace(body.Failure)||body.Failure.Length>200||(body.Notes?.Length??0)>2000||body.Serials is null||body.Serials.Length is <1 or >100||body.Serials.Any(s=>string.IsNullOrWhiteSpace(s)||s.Trim().Length>120))return "Completa equipo, motivo y un serial por unidad (máximo 100).";
  var serials=body.Serials.Select(s=>s.Trim().ToUpperInvariant()).ToArray();
  if(serials.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=serials.Length)return "Los seriales no pueden repetirse.";
  await PrepareWorkshopRenewal(actor,ct);
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);
  await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   DECLARE @lock int;
   EXEC @lock=sys.sp_getapplock @Resource='WorkshopIntake',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @lock<0 THROW 50000,'No se pudo bloquear la entrada de Taller.',1;
   IF EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch)
   BEGIN
    SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch AND payload_hash=@hash) THEN N'' ELSE N'La entrada ya existe con datos diferentes.' END; RETURN;
   END;
   IF @agency IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.agencies WHERE id=@agency) BEGIN SELECT N'Agencia no disponible.'; RETURN; END;
   IF EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.workshop_available_inventory i ON UPPER(i.serial)=s.value)
     BEGIN SELECT N'Un serial está registrado en el inventario disponible de Taller.'; RETURN; END;
   IF EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.maintenance_movements m ON UPPER(LTRIM(RTRIM(m.serial_number)))=s.value
    LEFT JOIN dbo.maintenance_workshop_cases wc ON wc.source_id=m.id
     WHERE (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY')) AND COALESCE(wc.status,'SENT') NOT IN('DELIVERED','DISCHARGED')
       AND NOT EXISTS(SELECT 1 FROM dbo.workshop_intake_deletions deletedIntake WHERE deletedIntake.source_id=m.id)
       AND NOT EXISTS(SELECT 1 FROM dbo.workshop_reset_log r WHERE r.reset_key='V418_20260929_WORKSHOP_PERMANENT_RESET' AND m.operational_area='WAREHOUSE' AND m.created_at<=r.completed_at)
      AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events e WHERE e.movement_id=m.id))
   BEGIN SELECT N'Uno de los seriales ya está activo en Taller.'; RETURN; END;
   INSERT dbo.workshop_intake_batches(batch_id,payload_hash) VALUES(@batch,@hash);
   INSERT dbo.maintenance_movements(id,agency_id,department,technician_name,movement_type,equipment_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,destination_name,occurred_at)
   SELECT NEWID(),@agency,@department,@name,'ENTRY',@equipment,@failure,s.value,1,@notes,@user,@name,'WORKSHOP',
    CONCAT('TAL-',REPLACE(CONVERT(varchar(36),@batch),'-',''),'-',s.[key]),CONCAT('REAL-MNT|TAL-',REPLACE(CONVERT(varchar(36),@batch),'-',''),'-',s.[key]),N'Taller',SYSUTCDATETIME()
   FROM OPENJSON(@serials) s;
   SELECT N'';
   """,c,tx);
  cmd.Parameters.AddWithValue("@batch",body.BatchId);cmd.Parameters.AddWithValue("@hash",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body with {Serials=serials})))));
  cmd.Parameters.AddWithValue("@serials",JsonSerializer.Serialize(serials));cmd.Parameters.AddWithValue("@agency",body.AgencyId is null?DBNull.Value:body.AgencyId.Value.ToString());cmd.Parameters.AddWithValue("@department",body.Department);cmd.Parameters.AddWithValue("@equipment",body.Equipment.Trim());cmd.Parameters.AddWithValue("@failure",body.Failure.Trim());cmd.Parameters.AddWithValue("@notes",Db(body.Notes));cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@name",actor.Name);
  var error=(string?)await cmd.ExecuteScalarAsync(ct);if(!string.IsNullOrEmpty(error)){await tx.RollbackAsync(ct);return error;}
  await tx.CommitAsync(ct);return null;
 }

 public async Task<string?> DeleteWorkshopIntake(Guid id,string reason,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanDeleteWorkshop)return "Solo el administrador principal y Vittini pueden eliminar equipos de Taller.";
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);
  await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   IF EXISTS(SELECT 1 FROM dbo.workshop_intake_deletions WITH(UPDLOCK,HOLDLOCK) WHERE source_id=@id)
   BEGIN SELECT N'Este equipo ya fue eliminado de Taller.'; RETURN; END;
   IF NOT EXISTS(
    SELECT 1 FROM dbo.maintenance_movements m WITH(UPDLOCK,HOLDLOCK)
    WHERE m.id=@id AND (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY')))
   BEGIN SELECT N'El equipo no pertenece a un proceso válido de Taller.'; RETURN; END;
   INSERT dbo.workshop_intake_deletions(source_id,document_number,equipment,serial,department,original_created_at,original_created_by,reason,deleted_at,deleted_by,deleted_by_name,deleted_by_login)
   SELECT id,document_number,equipment_type,serial_number,department,created_at,created_by_name,@reason,SYSUTCDATETIME(),@user,@name,@login
   FROM dbo.maintenance_movements WHERE id=@id;
   SELECT N'';
   """,c,tx);
  cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@reason",reason);cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@name",actor.Name);cmd.Parameters.AddWithValue("@login",actor.Login);
  var error=(string?)await cmd.ExecuteScalarAsync(ct);if(!string.IsNullOrEmpty(error)){await tx.RollbackAsync(ct);return error;}await tx.CommitAsync(ct);return null;
 }

 // One-time, explicitly requested permanent reset. Never deletes a warehouse movement or a ticket.
 public async Task PrepareWorkshopRenewal(WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanOperate)return;
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);
  await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   DECLARE @lock int;
   EXEC @lock=sys.sp_getapplock @Resource='WorkshopPermanentResetV418',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @lock<0 THROW 50000,'No se pudo preparar la limpieza de Taller.',1;
   IF NOT EXISTS(SELECT 1 FROM dbo.workshop_reset_log WHERE reset_key='V418_20260929_WORKSHOP_PERMANENT_RESET')
   BEGIN
    SELECT id INTO #workshop_delete FROM dbo.maintenance_movements WITH(UPDLOCK,HOLDLOCK) WHERE operational_area='WORKSHOP';
    DECLARE @deleted int=(SELECT COUNT(*) FROM #workshop_delete),@cutoff datetime2(3)=SYSUTCDATETIME();
    DELETE FROM dbo.maintenance_workshop_events;
    DELETE FROM dbo.maintenance_workshop_cases;
    IF OBJECT_ID('dbo.institutional_files','U') IS NOT NULL
     DELETE f FROM dbo.institutional_files f WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(f.metadata,'$.workshopMovementId')) IN(SELECT id FROM #workshop_delete)
      OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(f.metadata,'$.maintenanceMovementId')) IN(SELECT id FROM #workshop_delete)
      OR (f.area='communications' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Taller');
    IF OBJECT_ID('dbo.maintenance_document_signatures','U') IS NOT NULL DELETE s FROM dbo.maintenance_document_signatures s JOIN #workshop_delete t ON t.id=s.movement_id;
    IF OBJECT_ID('dbo.maintenance_document_marks','U') IS NOT NULL DELETE s FROM dbo.maintenance_document_marks s JOIN #workshop_delete t ON t.id=s.movement_id;
    DELETE m FROM dbo.maintenance_movements m JOIN #workshop_delete t ON t.id=m.id WHERE m.operational_area='WORKSHOP';
    INSERT dbo.workshop_reset_log(reset_key,completed_at,deleted_movements,actor_login) VALUES('V418_20260929_WORKSHOP_PERMANENT_RESET',@cutoff,@deleted,@actor);
   END;
   """,c,tx);
  cmd.Parameters.AddWithValue("@actor",actor.Login);await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
 }
}
