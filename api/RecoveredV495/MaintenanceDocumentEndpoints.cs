using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class MaintenanceDocumentEndpoints
{
	private static (string Id, string Role, string? Team, string Name, string Login) Actor(HttpContext c)
	{
		return (Id: c.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "", Role: c.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", Team: c.User.FindFirstValue("support_team"), Name: c.User.FindFirstValue("display_name") ?? "Usuario", Login: c.User.Identity?.Name ?? "");
	}

	private static IResult Result(TicketActionResult r)
	{
		if (!r.Ok)
		{
			return Results.Json(new
			{
				error = r.Error
			}, (JsonSerializerOptions?)null, (string?)null, (int?)((r.Code == "FORBIDDEN") ? 403 : 409));
		}
		return Results.Ok(new
		{
			ok = true
		});
	}

	internal static bool ValidSignature(string? value)
	{
		if (value == null || value.Length > 400000 || !value.StartsWith("data:image/png;base64,"))
		{
			return false;
		}
		try
		{
			byte[] array = Convert.FromBase64String(value.Substring(22));
			return array.Length >= 24 && array[0] == 137 && array[1] == 80 && array[2] == 78 && array[3] == 71 && array[12] == 73 && array[13] == 72 && array[14] == 68 && array[15] == 82;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static void MapMaintenanceDocumentEndpoints(this WebApplication app)
	{
		app.MapGet("/api/maintenance/movements/{id:guid}/document", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string, string, string, string) tuple = Actor(context);
			string text = await db.MaintenanceDocument(id, tuple.Item1, tuple.Item2, tuple.Item3, ct);
			context.Response.Headers.CacheControl = "no-store";
			return (text == null) ? Results.Forbid() : Results.Content(text, "application/json");
		})).RequireAuthorization("PortalUser");
		app.MapGet("/api/maintenance/track", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string code, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(code) || code.Length > 400)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Escanea el QR o escribe el número del formulario."
				});
			}
			(string, string, string, string, string) tuple = Actor(context);
			context.Response.Headers.CacheControl = "no-store";
			return Results.Content(await db.MaintenanceTrack(code, tuple.Item1, tuple.Item2, tuple.Item3, ct), "application/json");
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/maintenance/movements/{id:guid}/document", (Func<Guid, MaintenanceDocumentAction, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, MaintenanceDocumentAction body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!OperationalPermissions.Allowed(context.User, (body.Action == "RECEIVE") ? "canReceiveDocuments" : "canSignDocuments"))
			{
				return Results.Forbid();
			}
			bool flag;
			switch (body.Action)
			{
			case "RECEIVE":
			case "MOVE":
			case "SIGN":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag || !double.IsFinite(body.X) || !double.IsFinite(body.Y) || body.X < 4.0 || body.X > 117.0 || body.Y < 4.0 || body.Y > 256.0)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona una posición del sello dentro de la hoja."
				});
			}
			if (body.Action == "SIGN" && !ValidSignature(body.SignatureData))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Dibuja una firma PNG válida antes de guardarla."
				});
			}
			bool flag2 = body.Action == "SIGN";
			bool flag3 = flag2;
			if (flag3)
			{
				string signatureRole = body.SignatureRole;
				flag = ((signatureRole == "DELIVERY" || signatureRole == "RECEIPT") ? true : false);
				flag3 = !flag;
			}
			if (flag3)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona Firma de quien entrega o Firma de quien recibe. Actualiza la página si no aparecen ambas opciones."
				});
			}
			(string Id, string Role, string? Team, string Name, string Login) a = Actor(context);
			TicketActionResult ticketActionResult = await db.MarkMaintenanceDocument(id, body, a.Id, a.Role, a.Team, a.Name, a.Login, ct);
			if (!ticketActionResult.Ok)
			{
				return Result(ticketActionResult);
			}
			broker.Publish("maintenance-document-updated");
			string text = await db.MaintenanceDocument(id, a.Id, a.Role, a.Team, ct);
			return (text == null) ? Results.Forbid() : Results.Content(text, "application/json");
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapGet("/api/account/saved-signature", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			return Results.Ok(new
			{
				signatureData = await db.OwnSavedSignature(Actor(context).Id, ct)
			});
		})).RequireAuthorization("PortalUser");
		app.MapPut("/api/account/saved-signature", (Func<SavedSignatureInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (SavedSignatureInput body, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (!ValidSignature(body.SignatureData))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La firma no es una imagen PNG válida."
				});
			}
			await db.SaveOwnSignature(Actor(context).Id, body.SignatureData, ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapGet("/api/maintenance/request-board", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string, string, string, string) tuple = Actor(context);
			string text = await db.WarehouseRequestBoard(tuple.Item2, tuple.Item3, ct);
			return (text == null) ? Results.Forbid() : Results.Content(text, "application/json");
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/maintenance/department-requests", (Func<WarehouseDepartmentRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WarehouseDepartmentRequest body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag;
			switch (body.Department)
			{
			case "TECHNOLOGY":
			case "GENERAL_SERVICES":
			case "HUMAN_RESOURCES":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag2 = !flag || string.IsNullOrWhiteSpace(body.ProductName) || body.ProductName.Length > 160;
			bool flag3 = flag2;
			if (!flag3)
			{
				int quantity = body.Quantity;
				bool flag4 = ((quantity < 1 || quantity > 100000) ? true : false);
				flag3 = flag4;
			}
			if (flag3 || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa un departamento válido, artículo y cantidad de la solicitud."
				});
			}
			(string, string, string, string, string) tuple = Actor(context);
			TicketActionResult ticketActionResult = await db.SendWarehouseRequest(body, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, ct);
			if (ticketActionResult.Ok)
			{
				broker.Publish("maintenance-request-sent");
			}
			return Result(ticketActionResult);
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapPost("/api/maintenance/requisitions/{id:guid}/decision", (Func<Guid, WarehouseDecision, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, WarehouseDecision body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string status = body.Status;
			bool flag = ((status == "APPROVED" || status == "REJECTED") ? true : false);
			bool flag2 = !flag || string.IsNullOrWhiteSpace(body.Reason);
			bool flag3 = flag2;
			if (!flag3)
			{
				int length = body.Reason.Trim().Length;
				bool flag4 = ((length < 5 || length > 1000) ? true : false);
				flag3 = flag4;
			}
			if (flag3)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Escribe un motivo de 5 a 1,000 caracteres para autorizar o rechazar."
				});
			}
			(string, string, string, string, string) tuple = Actor(context);
			TicketActionResult ticketActionResult = await db.DecideWarehouseRequirement(id, body, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, ct);
			if (ticketActionResult.Ok)
			{
				broker.Publish("maintenance-requirement-decided");
			}
			return Result(ticketActionResult);
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
	}
}
