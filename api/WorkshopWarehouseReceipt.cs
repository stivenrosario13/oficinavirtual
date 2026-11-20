using System.Data;
using Microsoft.Data.SqlClient;

partial class Database
{
 public async Task<TicketActionResult> ReceiveWorkshopInWarehouse(Guid id,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanReceiveWarehouse)return new(false,"FORBIDDEN","Solo Almacén o el administrador principal puede confirmar esta recepción.");
  await using var connection=await Open(ct);await EnsureWorkshopSchema(connection,ct);await EnsureRepairedInventorySchema(connection,ct);await EnsureWarehouseWithdrawalSchema(connection,ct);
  await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   DECLARE @status varchar(20);
   SELECT @status=status FROM dbo.maintenance_workshop_cases WITH(UPDLOCK,HOLDLOCK) WHERE source_id=@id;
   IF @status IS NULL OR @status<>'DELIVERED' OR NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events WHERE source_id=@id AND action='DELIVER' AND movement_id IS NULL)
    OR EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events WHERE source_id=@id AND action='WAREHOUSE_RECEIVE')
   BEGIN SELECT 0; RETURN; END;
   DECLARE @now datetime2(3)=SYSUTCDATETIME(),@entry uniqueidentifier=NEWID(),@document varchar(40)=CONCAT('ALM-',REPLACE(CONVERT(varchar(36),NEWID()),'-',''));
   INSERT dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,occurred_at)
   SELECT @entry,m.agency_id,m.ticket_id,m.department,@name,'ENTRY',m.equipment_type,m.component_type,N'Recibido en Almacén desde Taller',COALESCE(c.replacement_serial,m.serial_number),m.quantity,N'Recepción confirmada mediante QR de Taller',@user,@name,'WAREHOUSE',@document,CONCAT('REAL-MNT|',@document),d.actor_name,@name,N'Almacén',@now
   FROM dbo.maintenance_movements m JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id
   CROSS APPLY(SELECT TOP(1) actor_name FROM dbo.maintenance_workshop_events WHERE source_id=m.id AND action='DELIVER' ORDER BY occurred_at DESC) d WHERE m.id=@id;
   INSERT dbo.maintenance_movements(id,department,technician_name,movement_type,equipment_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,occurred_at)
   SELECT NEWID(),m.department,@name,'EXIT',m.equipment_type,N'Recepción en Almacén confirmada',m.serial_number,m.quantity,N'Salida de Taller confirmada al recibir Almacén',@user,@name,'WORKSHOP',CONCAT(@document,'-SAL'),CONCAT('REAL-MNT|',@document,'-SAL'),d.actor_name,@name,N'Almacén',@now
   FROM dbo.maintenance_movements m CROSS APPLY(SELECT TOP(1) actor_name FROM dbo.maintenance_workshop_events WHERE source_id=m.id AND action='DELIVER' ORDER BY occurred_at DESC) d WHERE m.id=@id;
   INSERT dbo.maintenance_workshop_events(id,source_id,action,status,note,actor_user_id,actor_name,actor_login,occurred_at,movement_id)
   VALUES(NEWID(),@id,'WAREHOUSE_RECEIVE','DELIVERED',N'Equipo recibido e incorporado al inventario de Almacén',@user,@name,@login,@now,@entry);
   INSERT dbo.warehouse_repaired_receipts(movement_id,source_id) VALUES(@entry,@id);
   UPDATE dbo.maintenance_workshop_cases SET updated_at=@now WHERE source_id=@id;
   SELECT 1;
   """,connection,tx);
  cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@name",actor.Name);cmd.Parameters.AddWithValue("@login",actor.Login);
  var ok=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==1;
  if(!ok){await tx.RollbackAsync(ct);return new(false,"CONFLICT","Este equipo ya fue recibido o no está pendiente de recibir en Almacén.");}
  await tx.CommitAsync(ct);return new(true,"OK",null);
 }
}
