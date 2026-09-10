using System.Security.Claims;
using Microsoft.Data.SqlClient;

static class MaintenanceTemplateEndpoints
{
    static (string Role,string? Team,string Name) Actor(HttpContext c)=>(c.User.FindFirstValue(ClaimTypes.Role)??"",c.User.FindFirstValue("support_team"),c.User.FindFirstValue("display_name")??"Usuario");
    public static void MapMaintenanceTemplateEndpoints(this WebApplication app)
    {
        app.MapGet("/api/maintenance/templates",async(HttpContext c,Database db,CancellationToken ct)=>{var a=Actor(c);c.Response.Headers.CacheControl="no-store";var json=await db.MaintenanceTemplates(a.Role,a.Team,ct);return json is null?Results.Forbid():Results.Content(json,"application/json");}).RequireAuthorization("PortalUser");
        app.MapGet("/api/maintenance/templates/{id:guid}/file",async(Guid id,HttpContext c,Database db,CancellationToken ct)=>{var a=Actor(c);var file=await db.MaintenanceTemplateFile(id,a.Role,a.Team,ct);c.Response.Headers.CacheControl="private,no-store";return file is null?Results.NotFound():Results.File(file.Value.Data,"application/pdf",file.Value.Name);}).RequireAuthorization("PortalUser");
        app.MapPost("/api/maintenance/templates",async(HttpContext c,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{
            var a=Actor(c);if(a.Role!="Administrator"&&a.Team!="WAREHOUSE")return Results.Forbid();
            if(!c.Request.HasFormContentType)return Results.UnprocessableEntity(new{error="Selecciona una plantilla PDF."});
            var form=await c.Request.ReadFormAsync(ct);var file=form.Files.GetFile("file");var title=form["title"].ToString().Trim();var description=form["description"].ToString().Trim();var category=form["category"].ToString();var department=form["department"].ToString();
            if(title.Length is <3 or >160||description.Length>2000||category is not ("FORMS" or "POLICIES" or "NOTICES")||department is not ("ALL" or "TECHNOLOGY" or "GENERAL_SERVICES" or "HUMAN_RESOURCES")||file is null||file.Length is <5 or >10485760||form.Files.Count!=1)return Results.UnprocessableEntity(new{error="Completa título, categoría, departamento y un PDF de hasta 10 MB."});
            using var stream=new MemoryStream();await file.CopyToAsync(stream,ct);var bytes=stream.ToArray();if(!bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8))return Results.UnprocessableEntity(new{error="El archivo no tiene un formato PDF válido."});
            var name=Path.GetFileName(file.FileName.Replace('\\','/'));if(name.Length>180)name=name[..180];if(!name.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase))name+=".pdf";
            await db.AddMaintenanceTemplate(title,description,category,department,name,bytes,a.Name,ct);broker.Publish("maintenance-template-published");return Results.Ok(new{ok=true});
        }).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
        app.MapPost("/api/maintenance/templates/{id:guid}/archive",async(Guid id,TemplateArchive body,HttpContext c,Database db,LiveNotificationBroker broker,CancellationToken ct)=>{var a=Actor(c);if(a.Role!="Administrator"&&a.Team!="WAREHOUSE")return Results.Forbid();var changed=await db.ArchiveMaintenanceTemplate(id,body.Archived,a.Name,ct);if(!changed)return Results.NotFound();broker.Publish("maintenance-template-archived");return Results.Ok(new{ok=true});}).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
    }
}
record TemplateArchive(bool Archived);
