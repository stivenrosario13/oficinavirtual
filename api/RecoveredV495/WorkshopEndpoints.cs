using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

internal static class WorkshopEndpoints
{
	public static void MapWorkshopEndpoints(this WebApplication app)
	{
		app.MapGet("/api/workshop/available-inventory/fiscalized", (Func<HttpContext, IResult>)((HttpContext context) => (!WorkshopActor.From(context).CanOperate) ? Results.Forbid() : Results.Ok(Database.FiscalizedStockSource()))).RequireAuthorization("PortalUser");
		app.MapPost("/api/workshop/available-inventory/fiscalized", (Func<FiscalizedImportRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (FiscalizedImportRequest body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			WorkshopActor workshopActor = WorkshopActor.From(context);
			if (!workshopActor.CanOperate)
			{
				return Results.Forbid();
			}
			FiscalizedImportResult fiscalizedImportResult = await db.ImportFiscalizedWorkshopStock(body.IncludeWithoutSerial, workshopActor, ct);
			if (fiscalizedImportResult.Error != null)
			{
				return Results.Conflict(new
				{
					error = fiscalizedImportResult.Error
				});
			}
			broker.Publish("maintenance-workshop-updated");
			return Results.Ok(fiscalizedImportResult);
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapGet("/api/warehouse/repaired-inventory", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => (!WorkshopActor.From(context).CanReceiveWarehouse) ? Results.Forbid() : WorkshopJson(await db.WarehouseRepairedInventory(ct)))).RequireAuthorization("PortalUser");
		app.MapGet("/api/workshop/available-inventory", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			WorkshopActor workshopActor = WorkshopActor.From(context);
			return (!workshopActor.CanOperate) ? Results.Forbid() : WorkshopJson(await db.WorkshopAvailableInventory(ct));
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/workshop/available-inventory", (Func<WorkshopAvailableIntake, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WorkshopAvailableIntake body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			WorkshopActor workshopActor = WorkshopActor.From(context);
			if (!workshopActor.CanOperate)
			{
				return Results.Forbid();
			}
			string text = await db.CreateWorkshopAvailableInventory(body, workshopActor, ct);
			if (text != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text
				});
			}
			broker.Publish("maintenance-workshop-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapGet("/api/workshop/warehouse", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			WorkshopActor workshopActor = WorkshopActor.From(context);
			return (!workshopActor.CanReceiveWarehouse) ? Results.Forbid() : WorkshopJson(await db.WorkshopSnapshot(workshopActor, null, ct));
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/workshop/{id:guid}/warehouse-receipt", (Func<Guid, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			TicketActionResult ticketActionResult = await db.ReceiveWorkshopInWarehouse(id, WorkshopActor.From(context), ct);
			if (ticketActionResult.Ok)
			{
				broker.Publish("maintenance-workshop-updated");
				broker.Publish("institutional-document-created");
			}
			return ticketActionResult.Ok ? Results.Ok(new
			{
				ok = true
			}) : Results.Json(new
			{
				error = ticketActionResult.Error
			}, (JsonSerializerOptions?)null, (string?)null, (int?)((ticketActionResult.Code == "FORBIDDEN") ? 403 : 409));
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapGet("/api/workshop/manage", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			WorkshopActor actor = WorkshopActor.From(context);
			if (!actor.CanOperate)
			{
				return Results.Forbid();
			}
			await db.PrepareWorkshopRenewal(actor, ct);
			return WorkshopJson(await db.WorkshopSnapshot(actor, null, ct));
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/workshop/intake", (Func<WorkshopIntake, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WorkshopIntake body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			WorkshopActor workshopActor = WorkshopActor.From(context);
			if (!workshopActor.CanOperate)
			{
				return Results.Forbid();
			}
			string text = await db.CreateWorkshopIntake(body, workshopActor, ct);
			if (text != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text
				});
			}
			broker.Publish("maintenance-workshop-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapPut("/api/workshop/{id:guid}", (Func<Guid, WorkshopIntakeUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, WorkshopIntakeUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			WorkshopActor actor = WorkshopActor.From(context);
			if (!actor.CanDeleteWorkshop)
			{
				return Results.Forbid();
			}
			string error = await db.UpdateWorkshopIntake(id, body, actor, ct);
			if (error != null)
			{
				return Results.UnprocessableEntity(new { error });
			}
			broker.Publish("maintenance-workshop-updated");
			return Results.Ok(new { ok = true });
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapDelete("/api/workshop/{id:guid}", (Func<Guid, WorkshopIntakeDeletion, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] WorkshopIntakeDeletion body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			WorkshopActor actor = WorkshopActor.From(context);
			if (!actor.CanDeleteWorkshop)
			{
				return Results.Forbid();
			}
			if (string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Trim().Length < 5 || body.Reason.Trim().Length > 500)
			{
				return Results.UnprocessableEntity(new { error = "Indica el motivo de eliminación (entre 5 y 500 caracteres)." });
			}
			string error = await db.DeleteWorkshopIntake(id, body.Reason.Trim(), actor, ct);
			if (error != null)
			{
				return Results.Conflict(new { error });
			}
			broker.Publish("maintenance-workshop-updated");
			return Results.Ok(new { ok = true });
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		app.MapGet("/api/workshop", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => WorkshopJson(await db.WorkshopSnapshot(WorkshopActor.From(context), null, ct)))).RequireAuthorization("PortalUser");
		app.MapGet("/api/workshop/track", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string code, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(code) || code.Length > 400)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Escanea el QR o escribe el número del formulario."
				});
			}
			context.Response.Headers.CacheControl = "no-store";
			return WorkshopJson(await db.WorkshopSnapshot(WorkshopActor.From(context), WorkshopCode.Normalize(code), ct));
		})).RequireAuthorization("PortalUser");
		app.MapPost("/api/workshop/{id:guid}/actions", (Func<Guid, WorkshopAction, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, WorkshopAction body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag;
			switch (body.Action)
			{
			case "RECEIVE":
			case "DELIVER":
			case "REPLACE":
			case "PART":
			case "WORK":
			case "REPAIR":
			case "DISCHARGE":
			case "STOCK":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag || (body.Note?.Length ?? 0) > 2000 || (body.ReplacementSerial?.Length ?? 0) > 120)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La acción o el detalle del trabajo no es válido."
				});
			}
			WorkshopActor actor = WorkshopActor.From(context);
			TicketActionResult r = await db.WorkshopTransition(id, body, actor, ct);
			if (r.Ok)
			{
				broker.Publish("maintenance-workshop-updated");
				bool flag2 = body.Action == "DELIVER";
				bool flag3 = flag2;
				if (flag3)
				{
					flag3 = await db.NotifyWorkshopDeliveryReady(id, actor, ct);
				}
				if (flag3)
				{
					broker.Publish("chat-message");
					broker.Publish("institutional-document-created");
				}
			}
			return r.Ok ? Results.Ok(new
			{
				ok = true
			}) : Results.Json(new
			{
				error = r.Error
			}, (JsonSerializerOptions?)null, (string?)null, (int?)((r.Code == "FORBIDDEN") ? 403 : ((r.Code == "VALIDATION") ? 422 : 409)));
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
	}

	private static IResult WorkshopJson(string json)
	{
		try
		{
			return Results.Json(JsonSerializer.Deserialize<JsonElement>(json));
		}
		catch (JsonException)
		{
			return Results.Json(new
			{
				error = "No se pudo cargar la lista completa de Taller. Los registros no se han eliminado; actualiza e inténtalo de nuevo."
			}, (JsonSerializerOptions?)null, (string?)null, (int?)500);
		}
	}
}
