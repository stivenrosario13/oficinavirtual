using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

record FiscalizedStockItem(string Equipment,string Serial,string Notes);
record FiscalizedImportRequest(bool IncludeWithoutSerial=false);
record FiscalizedImportResult(int Added,int AlreadyPresent,string? Error=null);
partial class Database
{
 internal static JsonElement FiscalizedStockSource()=>JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Data","workshop-fiscalized-20260930.json")));
 public async Task<FiscalizedImportResult> ImportFiscalizedWorkshopStock(bool includeWithoutSerial,WorkshopActor actor,CancellationToken ct)
 {
  if(!actor.CanOperate)return new(0,0,"No tienes permiso para cargar el inventario de Taller.");
  var source=FiscalizedStockSource();var items=source.GetProperty("items").Deserialize<List<FiscalizedStockItem>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
  if(includeWithoutSerial)items.Add(new("MAQUINA TOUCH 2CONNET MAQUINA","INT-TAL-FISC-20260930-0068","Código interno, no es un serial de fabricante. Original: SIN SERIAL. Inventario de equpos fiscalizados.xlsm · Hoja1, fila 68."));
  if(items.Count is <1 or >1000||items.Any(i=>string.IsNullOrWhiteSpace(i.Serial)||i.Serial.Length>120||string.IsNullOrWhiteSpace(i.Equipment)||i.Equipment.Length>100||i.Notes.Length>2000)||items.Select(i=>i.Serial.Trim().ToUpperInvariant()).Distinct().Count()!=items.Count)throw new InvalidOperationException("El inventario fiscalizado no supera la validación.");
  await using var c=await Open(ct);await EnsureWorkshopSchema(c,ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  await using var cmd=new SqlCommand("""
   DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='WorkshopIntake',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
   IF @lock<0 THROW 50000,'No se pudo bloquear el inventario.',1;
   DECLARE @items TABLE(serial nvarchar(120),equipment nvarchar(100),notes nvarchar(2000));
   INSERT @items SELECT UPPER(LTRIM(RTRIM(serial))),equipment,notes FROM OPENJSON(@json) WITH(serial nvarchar(120),equipment nvarchar(100),notes nvarchar(2000));
   IF EXISTS(SELECT 1 FROM @items i JOIN dbo.maintenance_movements m ON UPPER(LTRIM(RTRIM(m.serial_number)))=i.serial LEFT JOIN dbo.maintenance_workshop_cases w ON w.source_id=m.id WHERE m.operational_area='WORKSHOP' AND m.movement_type IN('ENTRY','TRANSFER_TO_WORKSHOP') AND COALESCE(w.status,'SENT') NOT IN('DELIVERED','DISCHARGED'))
   BEGIN SELECT 0 added,0 existing,N'Un serial del archivo ya está en una orden de Taller. No se agregó ningún equipo; revisa el inventario antes de continuar.' error; RETURN; END;
   DECLARE @existing int=(SELECT COUNT(*) FROM @items i JOIN dbo.workshop_available_inventory s ON UPPER(LTRIM(RTRIM(s.serial)))=i.serial);
   INSERT dbo.workshop_available_inventory(id,batch_id,equipment,serial,notes,created_by,created_by_name)
   SELECT NEWID(),@batch,i.equipment,i.serial,i.notes,@user,@actor FROM @items i WHERE NOT EXISTS(SELECT 1 FROM dbo.workshop_available_inventory s WHERE UPPER(LTRIM(RTRIM(s.serial)))=i.serial);
   SELECT @@ROWCOUNT added,@existing existing,CAST(NULL AS nvarchar(2000)) error;
   """,c,tx);
  cmd.Parameters.AddWithValue("@json",JsonSerializer.Serialize(items,new JsonSerializerOptions(JsonSerializerDefaults.Web)));cmd.Parameters.AddWithValue("@batch",Guid.Parse("b7e7a306-0930-4418-a68f-3cbfd30f3ae2"));cmd.Parameters.AddWithValue("@user",actor.Id);cmd.Parameters.AddWithValue("@actor",actor.Name);
  FiscalizedImportResult result;await using(var reader=await cmd.ExecuteReaderAsync(ct)){await reader.ReadAsync(ct);result=new(reader.GetInt32(0),reader.GetInt32(1),reader.IsDBNull(2)?null:reader.GetString(2));}
  if(result.Error is null)await tx.CommitAsync(ct);else await tx.RollbackAsync(ct);return result;
 }
}
