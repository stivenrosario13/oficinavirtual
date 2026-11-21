using System;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class WarehouseFulfillmentEndpoints
{
	private static DocumentActor Actor(HttpContext context)
	{
		return new DocumentActor(context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "", context.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", context.User.FindFirstValue("support_team") ?? "", context.User.FindFirstValue("display_name") ?? "Usuario", context.User.Identity?.Name ?? "", null, OperationalPermissions.Allowed(context.User, "canDeleteWarehouseInventory"), WarehouseRequesterProfile.IsRequester(context.User), Permission(context, "canAuthorizeWarehouseRequests") && OperationalPermissions.Allowed(context.User, "canDecideDocuments"));
	}

	private static bool Permission(HttpContext context, string key)
	{
		return WarehousePermission.Has(context.User, key);
	}

	private static IResult Error(string message)
	{
		return Results.Json(new { message }, (JsonSerializerOptions?)null, (string?)null, (int?)409);
	}

	public static void MapWarehouseFulfillmentEndpoints(this WebApplication app)
	{
		RouteGroupBuilder routeGroupBuilder = app.MapGroup("/api/warehouse/fulfillment").RequireAuthorization("PortalUser");
		routeGroupBuilder.AddEndpointFilter(async (EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
		{
			context.HttpContext.Response.Headers.CacheControl = "private,no-store";
			return await next(context);
		});
		routeGroupBuilder.MapGet("/inventory", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => (!Actor(context).Allowed) ? Results.Forbid() : Results.Ok(await db.WarehouseAvailableInventory(Actor(context), ct))));
		routeGroupBuilder.MapPut("/requests/{id:long}/edit", (Func<long, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			if (!WarehouseRequestEditing.Editor(actor))
			{
				return Results.Forbid();
			}
			if (!context.Request.HasFormContentType || context.Request.ContentLength > 11000000)
			{
				return Error("Adjunta el formulario PDF actualizado.");
			}
			IFormCollection formCollection = await context.Request.ReadFormAsync(ct);
			IFormFile file = formCollection.Files.GetFile("file");
			bool flag = file == null;
			bool flag2 = flag;
			if (!flag2)
			{
				long length = file.Length;
				bool flag3 = ((length < 5 || length > 10485760) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				return Error("PDF no válido.");
			}
			WarehouseRequestEdit input;
			try
			{
				input = JsonSerializer.Deserialize<WarehouseRequestEdit>(formCollection["request"].ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
			}
			catch (JsonException)
			{
				return Error("Datos no válidos.");
			}
			if ((object)input == null)
			{
				return Error("Datos no válidos.");
			}
			using MemoryStream stream = new MemoryStream();
			await file.CopyToAsync(stream, ct);
			byte[] array = stream.ToArray();
			if (!array.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
			{
				return Error("PDF no válido.");
			}
			string text = await db.WarehouseEditRequest(id, actor, input, array, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(await db.InstitutionalDetail(id, actor, ct));
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/requests/{id:long}/loan-report", (Func<long, WeekendLoanReport, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, WeekendLoanReport input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			if (!WeekendLoans.Requester(actor))
			{
				return Results.Forbid();
			}
			string text = await db.WeekendLoanReport(id, actor, input, confirm: false, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(await db.InstitutionalDetail(id, actor, ct));
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/requests/{id:long}/loan-return", (Func<long, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			if (!actor.Manager)
			{
				return Results.Forbid();
			}
			string text = await db.WeekendLoanReport(id, actor, null, confirm: true, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("institutional-document-updated");
			broker.Publish("maintenance-movement-created");
			return Results.Ok(await db.InstitutionalDetail(id, actor, ct));
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapGet("/track", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string code, HttpContext context, Database db, CancellationToken ct) => Results.Ok(await db.WarehouseTrace(code, Actor(context), ct))));
		routeGroupBuilder.MapGet("/notifications", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => Results.Ok(await db.WarehouseNotifications(Actor(context), ct))));
		routeGroupBuilder.MapPost("/inventory/replace", (Func<WarehouseCatalogImport, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WarehouseCatalogImport input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!Permission(context, "canLoadWarehouseInventory"))
			{
				return Results.Forbid();
			}
			string text = await db.WarehouseReplaceCatalog(Actor(context), input, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("maintenance-movement-created");
			return Results.Ok(new
			{
				ok = true,
				count = input.Products.Count,
				version = input.Version
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapGet("/allocations", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => (!Actor(context).Allowed) ? Results.Forbid() : Results.Ok(await db.WarehouseAllocations(Actor(context), ct))));
		routeGroupBuilder.MapGet("/analytics", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => Results.Ok(await db.WarehouseConsumptionAnalytics(Actor(context), ct))));
		routeGroupBuilder.MapGet("/requests/{id:long}/frequency", (Func<long, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, HttpContext context, Database db, CancellationToken ct) => Results.Ok(await db.WarehouseRequestFrequency(id, Actor(context), ct))));
		routeGroupBuilder.MapPost("/inventory/remove", (Func<WarehouseInventoryRemovalInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WarehouseInventoryRemovalInput input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor documentActor = Actor(context);
			if (!documentActor.CanDeleteInventory)
			{
				return Results.Forbid();
			}
			string text = await db.WarehouseRemoveInventory(documentActor, input, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("maintenance-movement-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/inventory/products/remove", (Func<WarehouseProductRemoval, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WarehouseProductRemoval input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor documentActor = Actor(context);
			if (!documentActor.CanDeleteInventory)
			{
				return Results.Forbid();
			}
			string text = await db.WarehouseRemoveProduct(documentActor, input, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("maintenance-movement-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/requests/{id:long}/pending-purchase", (Func<long, WarehousePurchasePendingInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, WarehousePurchasePendingInput input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			bool flag = actor.Team != "WAREHOUSE";
			bool flag2 = flag;
			if (flag2)
			{
				string role = actor.Role;
				bool flag3 = ((role == "Administrator" || role == "Warehouse") ? true : false);
				flag2 = !flag3;
			}
			if (flag2)
			{
				return Results.Forbid();
			}
			string text = await db.WarehouseSavePendingPurchase(id, actor, input, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(new
			{
				ok = true,
				document = await db.InstitutionalDetail(id, actor, ct)
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/requests/{id:long}/prepare", (Func<long, WarehouseAuthorizationInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, WarehouseAuthorizationInput input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			bool flag = actor.Team != "WAREHOUSE";
			bool flag2 = flag;
			if (flag2)
			{
				string role = actor.Role;
				bool flag3 = ((role == "Administrator" || role == "Warehouse") ? true : false);
				flag2 = !flag3;
			}
			if (flag2)
			{
				return Results.Forbid();
			}
			string text = await db.WarehousePrepare(id, actor, input, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("institutional-document-updated");
			broker.Publish("maintenance-movement-created");
			return Results.Ok(new
			{
				ok = true,
				document = await db.InstitutionalDetail(id, actor, ct)
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/requests/{id:long}/approve", (Func<long, WarehouseApprovalInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, WarehouseApprovalInput input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor actor = Actor(context);
			if (!actor.Manager || !Permission(context, "canAuthorizeWarehouseRequests"))
			{
				return Results.Forbid();
			}
			string text = await db.WarehouseApprovePrepared(id, actor, input, ct);
			if (text != null)
			{
				return Error(text);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(new
			{
				ok = true,
				document = await db.InstitutionalDetail(id, actor, ct)
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/withdraw", (Func<WarehouseWithdrawalInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (WarehouseWithdrawalInput input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			DocumentActor documentActor = Actor(context);
			if (!documentActor.Manager)
			{
				return Results.Forbid();
			}
			(string, WarehouseDeliveryInfo, long) tuple = await db.WarehouseWithdraw(documentActor, input, ct);
			if (tuple.Item1 != null)
			{
				return Error(tuple.Item1);
			}
			broker.Publish("institutional-document-updated");
			broker.Publish("maintenance-movement-created");
			return Results.Ok(new
			{
				ok = true,
				documentId = tuple.Item3,
				delivery = tuple.Item2
			});
		})).RequireRateLimiting("sensitive-write");
	}
}
