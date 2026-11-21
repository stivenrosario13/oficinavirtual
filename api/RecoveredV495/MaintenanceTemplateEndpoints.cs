using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class MaintenanceTemplateEndpoints
{
	private static (string Role, string? Team, string Name) Actor(HttpContext c)
	{
		return (Role: c.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", Team: c.User.FindFirstValue("support_team"), Name: c.User.FindFirstValue("display_name") ?? "Usuario");
	}

	public static void MapMaintenanceTemplateEndpoints(this WebApplication app)
	{
		app.MapGet("/api/maintenance/templates", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) =>
		{
			(string, string, string) tuple = Actor(c);
			c.Response.Headers.CacheControl = "no-store";
			string text = await db.MaintenanceTemplates(tuple.Item1, tuple.Item2, ct);
			return (text == null) ? Results.Forbid() : Results.Content(text, "application/json");
		})).RequireAuthorization("PortalUser");
		app.MapGet("/api/maintenance/templates/{id:guid}/file", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext c, Database db, CancellationToken ct) =>
		{
			(string, string, string) tuple = Actor(c);
			(byte[], string)? tuple2 = await db.MaintenanceTemplateFile(id, tuple.Item1, tuple.Item2, ct);
			c.Response.Headers.CacheControl = "private,no-store";
			return (!tuple2.HasValue) ? Results.NotFound() : Results.File(tuple2.Value.Item1, "application/pdf", tuple2.Value.Item2);
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/maintenance/templates", (Func<HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string Role, string? Team, string Name) a = Actor(c);
			if (a.Role != "Administrator" && a.Team != "WAREHOUSE")
			{
				return Results.Forbid();
			}
			if (!c.Request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona una plantilla PDF."
				});
			}
			IFormCollection formCollection = await c.Request.ReadFormAsync(ct);
			IFormFile file = formCollection.Files.GetFile("file");
			string title = formCollection["title"].ToString().Trim();
			string description = formCollection["description"].ToString().Trim();
			string category = formCollection["category"].ToString();
			string department = formCollection["department"].ToString();
			int length = title.Length;
			bool flag = ((length < 3 || length > 160) ? true : false);
			bool flag2 = flag || description.Length > 2000;
			bool flag3 = flag2;
			if (!flag3)
			{
				bool flag4;
				switch (category)
				{
				case "FORMS":
				case "POLICIES":
				case "NOTICES":
					flag4 = true;
					break;
				default:
					flag4 = false;
					break;
				}
				flag3 = !flag4;
			}
			bool flag5 = flag3;
			if (!flag5)
			{
				bool flag4;
				switch (department)
				{
				case "ALL":
				case "TECHNOLOGY":
				case "GENERAL_SERVICES":
				case "HUMAN_RESOURCES":
					flag4 = true;
					break;
				default:
					flag4 = false;
					break;
				}
				flag5 = !flag4;
			}
			bool flag6 = flag5 || file == null;
			bool flag7 = flag6;
			if (!flag7)
			{
				long length2 = file.Length;
				bool flag4 = ((length2 < 5 || length2 > 10485760) ? true : false);
				flag7 = flag4;
			}
			if (flag7 || formCollection.Files.Count != 1)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa título, categoría, departamento y un PDF de hasta 10 MB."
				});
			}
			using MemoryStream stream = new MemoryStream();
			await file.CopyToAsync(stream, ct);
			byte[] array = stream.ToArray();
			if (!array.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
			{
				return Results.UnprocessableEntity(new
				{
					error = "El archivo no tiene un formato PDF válido."
				});
			}
			string text = Path.GetFileName(file.FileName.Replace('\\', '/'));
			if (text.Length > 180)
			{
				text = text.Substring(0, 180);
			}
			if (!text.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
			{
				text += ".pdf";
			}
			await db.AddMaintenanceTemplate(title, description, category, department, text, array, a.Name, ct);
			broker.Publish("maintenance-template-published");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapPost("/api/maintenance/templates/{id:guid}/archive", (Func<Guid, TemplateArchive, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, TemplateArchive body, HttpContext c, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string, string, string) tuple = Actor(c);
			if (tuple.Item1 != "Administrator" && tuple.Item2 != "WAREHOUSE")
			{
				return Results.Forbid();
			}
			if (!(await db.ArchiveMaintenanceTemplate(id, body.Archived, tuple.Item3, ct)))
			{
				return Results.NotFound();
			}
			broker.Publish("maintenance-template-archived");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
	}
}
