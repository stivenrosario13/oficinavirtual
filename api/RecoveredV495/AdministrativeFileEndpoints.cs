using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class AdministrativeFileEndpoints
{
	private static DocumentActor Actor(HttpContext context)
	{
		string? id = context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "";
		string? role = context.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "";
		string? team = context.User.FindFirstValue("support_team") ?? "";
		string? name = context.User.FindFirstValue("display_name") ?? "Usuario";
		string? login = context.User.Identity?.Name ?? "";
		string text = AreaManagerAccess.Scope(context.User);
		return new DocumentActor(id, role, team, name, login, (text != null && text != "ALL") ? DepartmentAccounts.Names.GetValueOrDefault(text) : null);
	}

	private static string Department(HttpContext context, DocumentActor actor)
	{
		return actor.Department;
	}

	private static IResult Error(string message, int status = 422)
	{
		return Results.Json(new { message }, (JsonSerializerOptions?)null, (string?)null, (int?)status);
	}

	public static void MapAdministrativeFileEndpoints(this WebApplication app)
	{
		RouteGroupBuilder routeGroupBuilder = app.MapGroup("/api/administrative-files").RequireAuthorization("PortalUser");
		routeGroupBuilder.AddEndpointFilter(async (EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
		{
			context.HttpContext.Response.Headers.CacheControl = "private, no-store";
			return (!Actor(context.HttpContext).Allowed) ? Results.Forbid() : (await next(context));
		});
		routeGroupBuilder.MapGet("/folders", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			return Results.Ok(await db.AdministrativeFolders(actor, ct));
		}));
		routeGroupBuilder.MapGet("/users", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => Results.Ok(await db.AdministrativeFolderUsers(Actor(context), ct))));
		routeGroupBuilder.MapGet("", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor documentActor = Actor(context);
			string department = Department(context, documentActor);
			return Results.Ok(from file in await db.InstitutionalList(documentActor, "files", "requests", ct)
				where file.Department == department || file.FolderId.HasValue || file.RecipientDepartment == department || file.SharedWith.Any((DocumentAccess access) => access.RecipientDepartment == department)
				group file by file.Id into grouping
				select grouping.First() into file
				orderby file.Id descending
				select file);
		}));
		routeGroupBuilder.MapPost("/folders", (Func<AdministrativeFolderInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (AdministrativeFolderInput input, HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			Department(context, actor);
			string text = input.Name?.Trim() ?? "";
			int length = text.Length;
			bool flag = ((length < 1 || length > 120) ? true : false);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (!flag3)
			{
				bool flag4 = ((text == "." || text == "..") ? true : false);
				flag3 = flag4;
			}
			if (flag3)
			{
				return Error("Escribe un nombre de carpeta de hasta 120 caracteres.");
			}
			AdministrativeFolder administrativeFolder = await db.CreateAdministrativeFolder(text, input.ParentId, actor, input.UserIds, ct);
			return ((object)administrativeFolder == null) ? Error("Selecciona una carpeta válida y hasta 20 usuarios activos.") : Results.Ok(administrativeFolder);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPut("/folders/{id:guid}", (Func<Guid, AdministrativeFolderInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, AdministrativeFolderInput input, HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			Department(context, actor);
			string text = input.Name?.Trim() ?? "";
			int length = text.Length;
			bool flag = ((length < 1 || length > 120) ? true : false);
			bool flag2 = flag;
			bool flag3 = flag2;
			if (!flag3)
			{
				bool flag4 = ((text == "." || text == "..") ? true : false);
				flag3 = flag4;
			}
			IResult result = (flag3 ? Error("Escribe un nombre de carpeta de hasta 120 caracteres.") : ((await db.RenameAdministrativeFolder(id, text, actor, ct)) ? Results.Ok(new
			{
				ok = true
			}) : Results.NotFound()));
			return result;
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPut("/folders/{id:guid}/move", (Func<Guid, AdministrativeFolderMove, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, AdministrativeFolderMove input, HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			string text = await db.MoveAdministrativeFolder(id, input.ParentId, actor, ct);
			return (text == null) ? Results.Ok(new
			{
				ok = true
			}) : Error(text, 409);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPut("/folders/{id:guid}/shared", (Func<Guid, AdministrativeFolderShare, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, AdministrativeFolderShare input, HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			if (input.UserIds == null)
			{
				return Error("Selecciona hasta 20 usuarios activos; una lista vacía deja la carpeta privada.");
			}
			string text = await db.SetAdministrativeFolderRecipients(id, input.UserIds, actor, ct);
			return (text == null) ? Results.Ok(new
			{
				ok = true
			}) : Error(text, 409);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapDelete("/folders/{id:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			return (await db.DeleteAdministrativeFolder(id, actor, ct)) ? Results.Ok(new
			{
				ok = true
			}) : Error("Solo puedes eliminar tus carpetas vacías. Mueve primero sus archivos o subcarpetas.", 409);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPut("/{id:long}/folder", (Func<long, AdministrativeFileMove, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, AdministrativeFileMove input, HttpContext context, Database db, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			string text = await db.MoveAdministrativeFile(id, input.FolderId, Department(context, actor), actor, ct);
			return (text == null) ? Results.Ok(new
			{
				ok = true
			}) : Error(text, 409);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/{id:long}/archive", (Func<long, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			string department = Department(context, actor);
			if (!string.Equals(department, "Administración", StringComparison.OrdinalIgnoreCase))
			{
				return Results.Forbid();
			}
			(InstitutionalFile Document, byte[] Data)? source = await db.InstitutionalBytes(id, actor, ct);
			if (!source.HasValue || source.Value.Document.Area != "communications" || source.Value.Document.RecipientDepartment != "Administración" || (object)source.Value.Document.AdministrativeRequest == null)
			{
				return Error("Solo se pueden guardar en Archivos las solicitudes administrativas visibles para este departamento.", 409);
			}
			InstitutionalFile institutionalFile = (await db.InstitutionalList(actor, "files", "requests", ct)).FirstOrDefault((InstitutionalFile file) => file.Department == department && file.ArchivedFromDocumentId == id && !file.Trashed);
			if (institutionalFile != null)
			{
				return Results.Ok(institutionalFile);
			}
			InstitutionalFile item = source.Value.Document;
			InstitutionalFile archived = new InstitutionalFile
			{
				Area = "files",
				Channel = "requests",
				Title = item.Title,
				Description = "Guardado desde la solicitud administrativa " + item.AdministrativeRequest.Number + ".",
				OriginalFileName = item.OriginalFileName,
				ContentType = item.ContentType,
				Size = source.Value.Data.Length,
				OwnerUserName = actor.Id,
				OwnerDisplayName = actor.Name,
				OwnerLogin = actor.Login,
				Department = department,
				Status = "active",
				ArchivedFromDocumentId = id
			};
			InstitutionalFile institutionalFile2 = archived;
			institutionalFile2.Id = await db.InstitutionalUpload(archived, source.Value.Data, ct);
			broker.Publish("institutional-document-created");
			return Results.Ok(archived.For(actor));
		})).RequireRateLimiting("sensitive-write");
	}
}
