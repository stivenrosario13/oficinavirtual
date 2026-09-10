using System.Security.Claims;

static class MaintenanceDocumentEndpoints
{
    static (string Id,string Role,string? Team,string Name,string Login) Actor(HttpContext c)=>(c.User.FindFirstValue(ClaimTypes.NameIdentifier)??"",c.User.FindFirstValue(ClaimTypes.Role)??"",c.User.FindFirstValue("support_team"),c.User.FindFirstValue("display_name")??"Usuario",c.User.Identity?.Name??"");
    static IResult Result(TicketActionResult r)=>r.Ok?Results.Ok(new{ok=true}):Results.Json(new{error=r.Error},statusCode:r.Code=="FORBIDDEN"?403:409);
    internal static bool ValidSignature(string? value)
    {
        if(value is null||value.Length>400000||!value.StartsWith("data:image/png;base64,"))return false;
        try{var data=Convert.FromBase64String(value[22..]);return data.Length>=24&&data[0]==137&&data[1]==80&&data[2]==78&&data[3]==71&&data[12]==73&&data[13]==72&&data[14]==68&&data[15]==82;}catch(FormatException){return false;}
    }
    public static void MapMaintenanceDocumentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/maintenance/movements/{id:guid}/document",async(Guid id,HttpContext context,Database db,CancellationToken ct)=>{var a=Actor(context);var json=await db.MaintenanceDocument(id,a.Id,a.Role,a.Team,ct);context.Response.Headers.CacheControl="no-store";return json is null?Results.Forbid():Results.Content(json,"application/json");}).RequireAuthorization("PortalUser");
        app.MapGet("/api/maintenance/track",async(string code,HttpContext context,Database db,CancellationToken ct)=>{
            if(string.IsNullOrWhiteSpace(code)||code.Length>400)return Results.UnprocessableEntity(new{error="Escanea el QR o escribe el número del formulario."});
            var a=Actor(context);context.Response.Headers.CacheControl="no-store";return Results.Content(await db.MaintenanceTrack(code,a.Id,a.Role,a.Team,ct),"application/json");
        }).RequireAuthorization("PortalUser");
        app.MapPost("/api/maintenance/movements/{id:guid}/document",async(Guid id,MaintenanceDocumentAction body,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(body.Action is not ("RECEIVE" or "MOVE" or "SIGN")||!double.IsFinite(body.X)||!double.IsFinite(body.Y)||body.X<4||body.X>117||body.Y<4||body.Y>256)return Results.UnprocessableEntity(new{error="Selecciona una posición del sello dentro de la hoja."});
            if(body.Action=="SIGN"&&!ValidSignature(body.SignatureData))return Results.UnprocessableEntity(new{error="Dibuja una firma PNG válida antes de guardarla."});
            if(body.Action=="SIGN"&&body.SignatureRole is not ("DELIVERY" or "RECEIPT"))return Results.UnprocessableEntity(new{error="Selecciona Firma de quien entrega o Firma de quien recibe. Actualiza la página si no aparecen ambas opciones."});
            var a=Actor(context);var result=await db.MarkMaintenanceDocument(id,body,a.Id,a.Role,a.Team,a.Name,a.Login,ct);if(!result.Ok)return Result(result);
            broker.Publish("maintenance-document-updated");
            var json=await db.MaintenanceDocument(id,a.Id,a.Role,a.Team,ct);
            return json is null?Results.Forbid():Results.Content(json,"application/json");
        }).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
        app.MapGet("/api/account/saved-signature",async(HttpContext context,Database db,CancellationToken ct)=>{context.Response.Headers.CacheControl="no-store";return Results.Ok(new{signatureData=await db.OwnSavedSignature(Actor(context).Id,ct)});}).RequireAuthorization("PortalUser");
        app.MapPut("/api/account/saved-signature",async(SavedSignatureInput body,HttpContext context,Database db,CancellationToken ct)=>{if(!ValidSignature(body.SignatureData))return Results.UnprocessableEntity(new{error="La firma no es una imagen PNG válida."});await db.SaveOwnSignature(Actor(context).Id,body.SignatureData,ct);return Results.Ok(new{ok=true});}).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
        app.MapGet("/api/maintenance/request-board",async(HttpContext context,Database db,CancellationToken ct)=>{var a=Actor(context);var json=await db.WarehouseRequestBoard(a.Role,a.Team,ct);return json is null?Results.Forbid():Results.Content(json,"application/json");}).RequireAuthorization("PortalUser");
        app.MapPost("/api/maintenance/department-requests",async(WarehouseDepartmentRequest body,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(body.Department is not ("TECHNOLOGY" or "GENERAL_SERVICES")||string.IsNullOrWhiteSpace(body.ProductName)||body.ProductName.Length>160||body.Quantity is <1 or >100000||(body.Notes?.Length??0)>2000)return Results.UnprocessableEntity(new{error="Completa un departamento válido (Tecnología o Servicios Generales), equipo y cantidad de la solicitud."});
            var a=Actor(context);var r=await db.SendWarehouseRequest(body,a.Role,a.Team,a.Name,a.Login,ct);if(r.Ok)broker.Publish("maintenance-request-sent");return Result(r);
        }).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
        app.MapPost("/api/maintenance/requisitions/{id:guid}/decision",async(Guid id,WarehouseDecision body,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(body.Status is not ("APPROVED" or "REJECTED")||string.IsNullOrWhiteSpace(body.Reason)||body.Reason.Trim().Length is <5 or >1000)return Results.UnprocessableEntity(new{error="Escribe un motivo de 5 a 1,000 caracteres para autorizar o rechazar."});
            var a=Actor(context);var r=await db.DecideWarehouseRequirement(id,body,a.Role,a.Team,a.Name,a.Login,ct);if(r.Ok)broker.Publish("maintenance-requirement-decided");return Result(r);
        }).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
    }
}
