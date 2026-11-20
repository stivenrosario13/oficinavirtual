using System.Security.Claims;

record AdministrativeFolderInput(string? Name, Guid? ParentId,List<string>? UserIds=null);
record AdministrativeFileMove(Guid? FolderId);
record AdministrativeFolderMove(Guid? ParentId);
record AdministrativeFolderShare(List<string>? UserIds);

static class AdministrativeFileEndpoints
{
    static DocumentActor Actor(HttpContext context) => new(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", context.User.FindFirstValue(ClaimTypes.Role) ?? "", context.User.FindFirstValue("support_team") ?? "", context.User.FindFirstValue("display_name") ?? "Usuario", context.User.Identity?.Name ?? "",AreaManagerAccess.Scope(context.User) is string scope&&scope!="ALL"?DepartmentAccounts.Names.GetValueOrDefault(scope):null);
    static string Department(HttpContext context, DocumentActor actor)=>actor.Department;
    static IResult Error(string message, int status = 422) => Results.Json(new { message }, statusCode: status);

    public static void MapAdministrativeFileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/administrative-files").RequireAuthorization("PortalUser");
        group.AddEndpointFilter(async (context, next) => {context.HttpContext.Response.Headers.CacheControl="private, no-store";return Actor(context.HttpContext).Allowed ? await next(context) : Results.Forbid();});
        group.MapGet("/folders", async (HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context);
            return Results.Ok(await db.AdministrativeFolders(actor, ct));
        });
        group.MapGet("/users",async(HttpContext context,Database db,CancellationToken ct)=>Results.Ok(await db.AdministrativeFolderUsers(Actor(context),ct)));
        group.MapGet("", async (HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context); var department = Department(context, actor);
            var files = await db.InstitutionalList(actor, "files", "requests", ct);
            return Results.Ok(files
                .Where(file => file.Department == department || file.FolderId is not null || file.RecipientDepartment == department || file.SharedWith.Any(access => access.RecipientDepartment == department))
                .GroupBy(file => file.Id).Select(grouping => grouping.First()).OrderByDescending(file => file.Id));
        });
        group.MapPost("/folders", async (AdministrativeFolderInput input, HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context); var department = Department(context, actor);
            var name = input.Name?.Trim() ?? "";
            if (name.Length is < 1 or > 120 || name is "." or "..") return Error("Escribe un nombre de carpeta de hasta 120 caracteres.");
            var folder = await db.CreateAdministrativeFolder(name, input.ParentId, actor, input.UserIds, ct);
            return folder is null ? Error("Selecciona una carpeta válida y hasta 20 usuarios activos.") : Results.Ok(folder);
        }).RequireRateLimiting("sensitive-write");
        group.MapPut("/folders/{id:guid}", async (Guid id, AdministrativeFolderInput input, HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context); var department = Department(context, actor);
            var name = input.Name?.Trim() ?? "";
            if (name.Length is < 1 or > 120 || name is "." or "..") return Error("Escribe un nombre de carpeta de hasta 120 caracteres.");
            return await db.RenameAdministrativeFolder(id, name, actor, ct) ? Results.Ok(new { ok = true }) : Results.NotFound();
        }).RequireRateLimiting("sensitive-write");
        group.MapPut("/folders/{id:guid}/move", async (Guid id, AdministrativeFolderMove input, HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context);
            var error = await db.MoveAdministrativeFolder(id, input.ParentId, actor, ct);
            return error is null ? Results.Ok(new { ok = true }) : Error(error, 409);
        }).RequireRateLimiting("sensitive-write");
        group.MapPut("/folders/{id:guid}/shared", async (Guid id, AdministrativeFolderShare input, HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context);
            if(input.UserIds is null)return Error("Selecciona hasta 20 usuarios activos; una lista vacía deja la carpeta privada.");
            var error=await db.SetAdministrativeFolderRecipients(id,input.UserIds,actor,ct);
            return error is null?Results.Ok(new{ok=true}):Error(error,409);
        }).RequireRateLimiting("sensitive-write");
        group.MapDelete("/folders/{id:guid}", async (Guid id, HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context);
            return await db.DeleteAdministrativeFolder(id, actor, ct) ? Results.Ok(new { ok = true }) : Error("Solo puedes eliminar tus carpetas vacías. Mueve primero sus archivos o subcarpetas.", 409);
        }).RequireRateLimiting("sensitive-write");
        group.MapPut("/{id:long}/folder", async (long id, AdministrativeFileMove input, HttpContext context, Database db, CancellationToken ct) =>
        {
            var actor = Actor(context);
            var error = await db.MoveAdministrativeFile(id, input.FolderId, Department(context, actor), actor, ct);
            return error is null ? Results.Ok(new { ok = true }) : Error(error, 409);
        }).RequireRateLimiting("sensitive-write");
        group.MapPost("/{id:long}/archive", async (long id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
        {
            var actor = Actor(context);
            var department = Department(context, actor);
            if (!string.Equals(department, "Administración", StringComparison.OrdinalIgnoreCase)) return Results.Forbid();
            var source = await db.InstitutionalBytes(id, actor, ct);
            if (source is null || source.Value.Document.Area != "communications" || source.Value.Document.RecipientDepartment != "Administración" || source.Value.Document.AdministrativeRequest is null)
                return Error("Solo se pueden guardar en Archivos las solicitudes administrativas visibles para este departamento.", 409);
            var existing = (await db.InstitutionalList(actor, "files", "requests", ct))
                .FirstOrDefault(file => file.Department == department && file.ArchivedFromDocumentId == id && !file.Trashed);
            if (existing is not null) return Results.Ok(existing);
            var document = source.Value.Document;
            var archived = new InstitutionalFile
            {
                Area = "files", Channel = "requests", Title = document.Title,
                Description = $"Guardado desde la solicitud administrativa {document.AdministrativeRequest.Number}.",
                OriginalFileName = document.OriginalFileName, ContentType = document.ContentType, Size = source.Value.Data.Length,
                OwnerUserName = actor.Id, OwnerDisplayName = actor.Name, OwnerLogin = actor.Login,
                Department = department, Status = "active", ArchivedFromDocumentId = id
            };
            archived.Id = await db.InstitutionalUpload(archived, source.Value.Data, ct);
            broker.Publish("institutional-document-created");
            return Results.Ok(archived.For(actor));
        }).RequireRateLimiting("sensitive-write");
    }
}
