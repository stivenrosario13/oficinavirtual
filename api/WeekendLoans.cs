using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

record WeekendLoanPeriod(DateOnly From,DateOnly To,DateOnly ReturnBy);
record WeekendLoanUse(int AssignedIndex,int Quantity,Guid AgencyId,string? AgencyLabel=null);
record WeekendLoanReport(List<WeekendLoanUse> Uses,string Note);
record WeekendLoanSettlement(List<WeekendLoanUse> Uses,string Note,string ReportedBy,DateTime ReportedAt,string? ConfirmedBy=null,DateTime? ConfirmedAt=null);
static class WeekendLoans
{
    public static bool Requester(DocumentActor a)=>a.Role=="Technology"&&a.Team=="TECHNICAL_FAILURE"&&AdministrativeProfiles.Login(a.Login)=="rosmarlin.vittini";
    public static bool Valid(WeekendLoanPeriod p)=>p.From.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday&&p.To.DayOfWeek==DayOfWeek.Sunday&&p.To.DayNumber-p.From.DayNumber is >=1 and <=2&&p.ReturnBy>p.To&&p.ReturnBy.DayNumber-p.To.DayNumber<=7;
}
partial class Database
{
    public async Task<string?> WeekendLoanReport(long id,DocumentActor actor,WeekendLoanReport? input,bool confirm,CancellationToken ct)
    {
        if(confirm?!actor.Manager:!WeekendLoans.Requester(actor))return "No tienes acceso a esta operación del préstamo.";
        if(!confirm&&(input is null||input.Note?.Trim().Length is not (>=5 and <=2000)||input.Uses is null||input.Uses.Count>10000||input.Uses.Any(x=>x is null||x.Quantity is <1 or >10000||x.AssignedIndex<0||x.AgencyId==Guid.Empty)))return "Revisa las cantidades utilizadas, las agencias y la nota de devolución.";
        var agencies=confirm?null:await TicketAgencies(actor.Id,actor.Role,ct);
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureMaintenanceSchema(c,ct);await EnsureWarehouseFulfillmentSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id",c,tx);read.Parameters.AddWithValue("@id",id);
        var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "Préstamo no encontrado.";
        var d=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;d.Id=id;
        if(!d.Accessible(actor)||d.Trashed||d.WarehouseRequest?.WeekendLoan is null||d.WarehouseDelivery is null||d.WarehouseAssigned.Count==0)return "Primero debe registrarse la entrega del préstamo.";
        if(!confirm)
        {
            if(d.OwnerUserName!=actor.Id)return "Solo el solicitante puede declarar el uso del préstamo.";
            if(d.WeekendLoanSettlement is not null)return "Este préstamo ya tiene una declaración. Actualiza la pantalla.";
            if(input!.Uses.Any(x=>x.AssignedIndex>=d.WarehouseAssigned.Count||!agencies!.Any(a=>a.Id==x.AgencyId))||input.Uses.GroupBy(x=>x.AssignedIndex).Any(g=>g.Sum(x=>x.Quantity)>d.WarehouseAssigned[g.Key].Quantity))return "El uso no puede superar las unidades entregadas y requiere una agencia válida.";
            var uses=input.Uses.Select(x=>{var a=agencies!.First(a=>a.Id==x.AgencyId);return x with{AgencyLabel=$"{a.Codigo} · {a.Terminal} · {a.Grupo}"};}).ToList();
            d.WeekendLoanSettlement=new(uses,input.Note.Trim(),actor.Name,DateTime.UtcNow);
        }
        else
        {
            var settlement=d.WeekendLoanSettlement;if(settlement is null)return "Vittini debe declarar primero los equipos usados y los sobrantes.";
            if(settlement.ConfirmedAt is not null)return null; // Retry never credits stock twice.
            await using(var stockLock=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)",c,tx))await stockLock.ExecuteScalarAsync(ct);
            for(var i=0;i<d.WarehouseAssigned.Count;i++)
            {
                var line=d.WarehouseAssigned[i];var returned=line.Quantity-settlement.Uses.Where(x=>x.AssignedIndex==i).Sum(x=>x.Quantity);if(returned<=0)continue;
                var number=$"DEV-PRE-{id}-{i}";
                await using var movement=new SqlCommand("""
                  INSERT dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at)
                  VALUES(NEWID(),NULL,NULL,@department,NULL,@owner,'ENTRY',@equipment,@product,N'Devolución de sobrantes de préstamo',@serial,@quantity,@note,@actorId,@actorName,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@owner,@actorName,N'Almacén central',NULL,SYSUTCDATETIME())
                  """,c,tx);
                movement.Parameters.AddWithValue("@department",WarehouseDepartmentCode(d.Department)??"TECHNOLOGY");movement.Parameters.AddWithValue("@owner",d.OwnerDisplayName);movement.Parameters.AddWithValue("@equipment",line.Name);movement.Parameters.AddWithValue("@product",(object?)line.ProductId??DBNull.Value);movement.Parameters.AddWithValue("@serial",line.Serial=="CONTROL POR CANTIDAD"?"":line.Serial);movement.Parameters.AddWithValue("@quantity",returned);movement.Parameters.AddWithValue("@note",$"Préstamo {d.WarehouseRequest.Number}. {settlement.Note}");movement.Parameters.AddWithValue("@actorId",actor.Id);movement.Parameters.AddWithValue("@actorName",actor.Name);movement.Parameters.AddWithValue("@number",number);await movement.ExecuteNonQueryAsync(ct);
            }
            d.WeekendLoanSettlement=settlement with{ConfirmedBy=actor.Name,ConfirmedAt=DateTime.UtcNow};
        }
        d.History.Add(new(confirm?"loan_return_confirmed":"loan_usage_reported",actor.Name,actor.Login,DateTime.UtcNow,d.Status,d.ReceiptCode,d.ReceiverLogin,d.ReceivedUtc,null));
        await using var save=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",c,tx);save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@json",JsonSerializer.Serialize(d,InstitutionalJson));await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
}
