using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

record WorkshopStockExitRequest(Guid AgencyId,string Department,string Category,string Description);
partial class Database
{
 static async Task EnsureWorkshopStockExitSchema(SqlConnection connection,CancellationToken ct)
 {
  await using var cmd=new SqlCommand("""
   BEGIN TRANSACTION;
   DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='WorkshopStockExitSchema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @lock<0 BEGIN ROLLBACK;THROW 50001,N'No se pudo preparar el registro de salidas. Inténtalo nuevamente.',1;END;
   IF OBJECT_ID(N'dbo.workshop_stock_exits',N'U') IS NULL
   CREATE TABLE dbo.workshop_stock_exits(stock_id uniqueidentifier NOT NULL PRIMARY KEY,ticket_id uniqueidentifier NOT NULL,
    equipment nvarchar(100) NOT NULL,serial nvarchar(120) NOT NULL,origin varchar(20) NOT NULL,
    actor_id nvarchar(80) NOT NULL,actor_name nvarchar(160) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
   COMMIT;
   """,connection);await cmd.ExecuteNonQueryAsync(ct);
 }
 static async Task RecordWorkshopStockExit(SqlConnection c,SqlTransaction tx,Guid stockId,Guid ticketId,string userId,string name,CancellationToken ct)
 {
  await using var cmd=new SqlCommand("""
   DECLARE @resource nvarchar(255)=CONCAT('WorkshopStock:',@stock),@lock int;
   EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @lock<0 THROW 50001,N'El equipo está siendo actualizado. Inténtalo nuevamente.',1;
   IF EXISTS(SELECT 1 FROM dbo.workshop_stock_exits WITH(UPDLOCK,HOLDLOCK) WHERE stock_id=@stock)
    THROW 50001,N'Este equipo ya tiene una salida registrada. Actualiza el inventario.',1;
   INSERT dbo.workshop_stock_exits(stock_id,ticket_id,equipment,serial,origin,actor_id,actor_name)
   SELECT id,@ticket,equipment,serial,'DIRECT',@user,@name FROM dbo.workshop_available_inventory WITH(UPDLOCK,HOLDLOCK) WHERE id=@stock;
   IF @@ROWCOUNT=0
   BEGIN
    INSERT dbo.workshop_stock_exits(stock_id,ticket_id,equipment,serial,origin,actor_id,actor_name)
    SELECT m.id,@ticket,m.equipment_type,m.serial_number,'REPAIRED',@user,@name
    FROM dbo.maintenance_workshop_cases w WITH(UPDLOCK,HOLDLOCK) JOIN dbo.maintenance_movements m ON m.id=w.source_id
    WHERE w.source_id=@stock AND w.status='INVENTORY';
    IF @@ROWCOUNT=0 THROW 50001,N'El equipo ya no está disponible en Taller.',1;
   END;
   SELECT equipment,serial FROM dbo.workshop_stock_exits WHERE stock_id=@stock;
   """,c,tx);
  cmd.Parameters.AddWithValue("@stock",stockId);cmd.Parameters.AddWithValue("@ticket",ticketId);cmd.Parameters.AddWithValue("@user",userId);cmd.Parameters.AddWithValue("@name",name);
  string equipment,serial;await using(var reader=await cmd.ExecuteReaderAsync(ct)){await reader.ReadAsync(ct);equipment=reader.GetString(0);serial=reader.GetString(1);}
  await AddTicketHistory(c,tx,ticketId,"WORKSHOP_STOCK_OUT",null,null,null,null,userId,name,$"Equipo entregado por Taller: {equipment}\nSerial del equipo reemplazado: {serial}",ct);
 }
 public async Task<string> WorkshopTicketReplacementSerial(Guid ticketId,CancellationToken ct)
 {
  await using var c=await Open(ct);await using var cmd=new SqlCommand("IF OBJECT_ID(N'dbo.workshop_stock_exits',N'U') IS NOT NULL SELECT serial FROM dbo.workshop_stock_exits WHERE ticket_id=@ticket",c);
  cmd.Parameters.AddWithValue("@ticket",ticketId);var result=new List<string>();await using var reader=await cmd.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct))result.Add(reader.GetString(0));return string.Join(", ",result.Distinct());
 }
}
