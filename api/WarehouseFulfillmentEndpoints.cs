using System.Security.Claims;
using System.Text.Json;

static class WarehouseFulfillmentEndpoints
{
    static DocumentActor Actor(HttpContext context)=>new(context.User.FindFirstValue(ClaimTypes.NameIdentifier)??"",context.User.FindFirstValue(ClaimTypes.Role)??"",context.User.FindFirstValue("support_team")??"",context.User.FindFirstValue("display_name")??"Usuario",context.User.Identity?.Name??"",InventoryDelete:OperationalPermissions.Allowed(context.User,"canDeleteWarehouseInventory"),WarehouseRequestOnly:WarehouseRequesterProfile.IsRequester(context.User),WarehouseAuthorize:Permission(context,"canAuthorizeWarehouseRequests")&&OperationalPermissions.Allowed(context.User,"canDecideDocuments"));
    static bool Permission(HttpContext context,string key)=>WarehousePermission.Has(context.User,key);
    static IResult Error(string message)=>Results.Json(new{message},statusCode:409);
    public static void MapWarehouseFulfillmentEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/api/warehouse/fulfillment").RequireAuthorization("PortalUser");
        group.AddEndpointFilter(async(context,next)=>{context.HttpContext.Response.Headers.CacheControl="private,no-store";return await next(context);});
        group.MapGet("/inventory",async(HttpContext context,Database db,CancellationToken ct)=>Actor(context).Allowed?Results.Ok(await db.WarehouseAvailableInventory(Actor(context),ct)):Results.Forbid());
        group.MapPut("/requests/{id:long}/edit",async(long id,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!WarehouseRequestEditing.Editor(actor))return Results.Forbid();
            if(!context.Request.HasFormContentType||context.Request.ContentLength>11000000)return Error("Adjunta el formulario PDF actualizado.");
            var form=await context.Request.ReadFormAsync(ct);var file=form.Files.GetFile("file");
            if(file is null||file.Length is <5 or >10485760)return Error("PDF no válido.");
            WarehouseRequestEdit? input;try{input=JsonSerializer.Deserialize<WarehouseRequestEdit>(form["request"].ToString(),new JsonSerializerOptions(JsonSerializerDefaults.Web));}catch(JsonException){return Error("Datos no válidos.");}
            if(input is null)return Error("Datos no válidos.");using var stream=new MemoryStream();await file.CopyToAsync(stream,ct);var pdf=stream.ToArray();if(!pdf.AsSpan(0,5).SequenceEqual("%PDF-"u8))return Error("PDF no válido.");
            var error=await db.WarehouseEditRequest(id,actor,input,pdf,ct);if(error is not null)return Error(error);broker.Publish("institutional-document-updated");return Results.Ok(await db.InstitutionalDetail(id,actor,ct));
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/requests/{id:long}/loan-report",async(long id,WeekendLoanReport input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!WeekendLoans.Requester(actor))return Results.Forbid();var error=await db.WeekendLoanReport(id,actor,input,false,ct);if(error is not null)return Error(error);broker.Publish("institutional-document-updated");return Results.Ok(await db.InstitutionalDetail(id,actor,ct));
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/requests/{id:long}/loan-return",async(long id,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!actor.Manager)return Results.Forbid();var error=await db.WeekendLoanReport(id,actor,null,true,ct);if(error is not null)return Error(error);broker.Publish("institutional-document-updated");broker.Publish("maintenance-movement-created");return Results.Ok(await db.InstitutionalDetail(id,actor,ct));
        }).RequireRateLimiting("sensitive-write");
        group.MapGet("/track",async(string code,HttpContext context,Database db,CancellationToken ct)=>Results.Ok(await db.WarehouseTrace(code,Actor(context),ct)));
        group.MapGet("/notifications",async(HttpContext context,Database db,CancellationToken ct)=>Results.Ok(await db.WarehouseNotifications(Actor(context),ct)));
        group.MapPost("/inventory/replace",async(WarehouseCatalogImport input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            if(!Permission(context,"canLoadWarehouseInventory"))return Results.Forbid();
            var error=await db.WarehouseReplaceCatalog(Actor(context),input,ct);if(error is not null)return Error(error);
            broker.Publish("maintenance-movement-created");return Results.Ok(new{ok=true,count=input.Products.Count,version=input.Version});
        }).RequireRateLimiting("sensitive-write");
        group.MapGet("/allocations",async(HttpContext context,Database db,CancellationToken ct)=>Actor(context).Allowed?Results.Ok(await db.WarehouseAllocations(Actor(context),ct)):Results.Forbid());
        group.MapGet("/analytics",async(HttpContext context,Database db,CancellationToken ct)=>Results.Ok(await db.WarehouseConsumptionAnalytics(Actor(context),ct)));
        group.MapGet("/requests/{id:long}/frequency",async(long id,HttpContext context,Database db,CancellationToken ct)=>Results.Ok(await db.WarehouseRequestFrequency(id,Actor(context),ct)));
        group.MapPost("/inventory/remove",async(WarehouseInventoryRemovalInput input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!actor.CanDeleteInventory)return Results.Forbid();
            var error=await db.WarehouseRemoveInventory(actor,input,ct);if(error is not null)return Error(error);
            broker.Publish("maintenance-movement-created");return Results.Ok(new{ok=true});
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/inventory/products/remove",async(WarehouseProductRemoval input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!actor.CanDeleteInventory)return Results.Forbid();
            var error=await db.WarehouseRemoveProduct(actor,input,ct);if(error is not null)return Error(error);
            broker.Publish("maintenance-movement-created");return Results.Ok(new{ok=true});
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/requests/{id:long}/pending-purchase",async(long id,WarehousePurchasePendingInput input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(actor.Team!="WAREHOUSE"&&actor.Role is not ("Administrator" or "Warehouse"))return Results.Forbid();
            var error=await db.WarehouseSavePendingPurchase(id,actor,input,ct);
            if(error is not null)return Error(error);
            broker.Publish("institutional-document-updated");
            return Results.Ok(new{ok=true,document=await db.InstitutionalDetail(id,actor,ct)});
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/requests/{id:long}/prepare",async(long id,WarehouseAuthorizationInput input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(actor.Team!="WAREHOUSE"&&actor.Role is not ("Administrator" or "Warehouse"))return Results.Forbid();
            var error=await db.WarehousePrepare(id,actor,input,ct);
            if(error is not null)return Error(error);
            broker.Publish("institutional-document-updated");broker.Publish("maintenance-movement-created");
            return Results.Ok(new{ok=true,document=await db.InstitutionalDetail(id,actor,ct)});
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/requests/{id:long}/approve",async(long id,WarehouseApprovalInput input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!actor.Manager||!Permission(context,"canAuthorizeWarehouseRequests"))return Results.Forbid();
            var error=await db.WarehouseApprovePrepared(id,actor,input,ct);
            if(error is not null)return Error(error);
            broker.Publish("institutional-document-updated");
            return Results.Ok(new{ok=true,document=await db.InstitutionalDetail(id,actor,ct)});
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/withdraw",async(WarehouseWithdrawalInput input,HttpContext context,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var actor=Actor(context);if(!actor.Manager)return Results.Forbid();
            var result=await db.WarehouseWithdraw(actor,input,ct);
            if(result.Error is not null)return Error(result.Error);
            broker.Publish("institutional-document-updated");broker.Publish("maintenance-movement-created");
            return Results.Ok(new{ok=true,documentId=result.DocumentId,delivery=result.Delivery});
        }).RequireRateLimiting("sensitive-write");
    }
}
