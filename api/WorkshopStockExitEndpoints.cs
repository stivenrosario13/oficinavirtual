using Microsoft.Data.SqlClient;
static class WorkshopStockExitEndpoints
{
 public static void MapWorkshopStockExitEndpoints(this WebApplication app)
 {
  app.MapPost("/api/workshop/available-inventory/{id:guid}/exit",async(Guid id,WorkshopStockExitRequest body,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
   var actor=WorkshopActor.From(context);if(!actor.CanOperate)return Results.Forbid();
   if(body.AgencyId==Guid.Empty||body.Department is not ("TECHNOLOGY" or "GENERAL_SERVICES")||string.IsNullOrWhiteSpace(body.Category)||body.Category.Length>80||string.IsNullOrWhiteSpace(body.Description)||body.Description.Length>1500)return Results.UnprocessableEntity(new{error="Selecciona agencia, departamento, falla y motivo de la salida."});
   var catalog=await db.SupportCatalog(ct);
   try{
    var ticket=await db.CreateTicket(new(body.AgencyId,body.Category,body.Department,"MEDIUM","Salida de equipo de Taller",body.Description,"INTERNAL",null,body.Department=="TECHNOLOGY"?"TECHNICAL_FAILURE":null,false),actor.Id,actor.Role,actor.Name,actor.Login,actor.Team,false,Array.Empty<TicketEvidenceUpload>(),null,ct,id);
    if(ticket is null)return Results.UnprocessableEntity(new{error="No se pudo crear el ticket. Revisa la agencia y la falla seleccionadas; el equipo no salió del inventario."});
    broker.Publish("ticket-created");broker.Publish("maintenance-workshop-updated");return Results.Ok(new{ticket.Id,ticket.TicketNumber});
   }catch(TicketDuplicateException){return Results.Conflict(new{error="Ya existe un ticket activo de esta falla para la agencia. No se registró la salida del equipo."});}
   catch(SqlException e) when(e.Number==50001){return Results.Conflict(new{error=e.Message});}
  }).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
 }
}
