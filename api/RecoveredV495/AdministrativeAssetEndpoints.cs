using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class AdministrativeAssetEndpoints
{
	private static IResult Error(string message, int code = 422)
	{
		return Results.Json(new { message }, (JsonSerializerOptions?)null, (string?)null, (int?)code);
	}

	private static DocumentActor Actor(HttpContext c)
	{
		return new DocumentActor(c.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "", c.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", c.User.FindFirstValue("support_team") ?? "", c.User.FindFirstValue("display_name") ?? "Administrador", c.User.Identity?.Name ?? "");
	}

	public static void MapAdministrativeAssetEndpoints(this WebApplication app)
	{
		RouteGroupBuilder routeGroupBuilder = app.MapGroup("/api/administrative-assets").RequireAuthorization("PortalUser");
		routeGroupBuilder.AddEndpointFilter(async (EndpointFilterInvocationContext context, EndpointFilterDelegate next) => (!context.HttpContext.User.IsInRole("Administrator") && !context.HttpContext.User.IsInRole("Administrative") && (!context.HttpContext.User.IsInRole("Department") || !AreaManagerAccess.Allowed(context.HttpContext.User))) ? Results.Forbid() : (await next(context)));
		routeGroupBuilder.MapGet("", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) => Results.Ok((await db.AdministrativeAssets(ct, Actor(c).Department)).Where((AdministrativeAsset asset) => CanUseAsset(Actor(c), asset.Kind)))));
		routeGroupBuilder.MapPost("", (Func<AdministrativeAssetInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (AdministrativeAssetInput input, HttpContext c, Database db, CancellationToken ct) =>
		{
			if (!CanUseAsset(Actor(c), input.Kind))
			{
				return Results.Forbid();
			}
			string text = Validate(input);
			if (text != null)
			{
				return Error(text);
			}
			AdministrativeAsset administrativeAsset = await db.SaveAdministrativeAsset(null, input, ct, Actor(c).Department);
			return ((object)administrativeAsset == null) ? Error("El identificador ya está registrado.", 409) : Results.Ok(administrativeAsset);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPut("/{id:guid}", (Func<Guid, AdministrativeAssetInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, AdministrativeAssetInput input, HttpContext c, Database db, CancellationToken ct) =>
		{
			AdministrativeAsset administrativeAsset = (await db.AdministrativeAssets(ct, Actor(c).Department)).FirstOrDefault((AdministrativeAsset a) => a.Id == id);
			if ((object)administrativeAsset == null || !CanUseAsset(Actor(c), administrativeAsset.Kind) || !CanUseAsset(Actor(c), input.Kind))
			{
				return Results.Forbid();
			}
			string text = Validate(input);
			if (text != null)
			{
				return Error(text);
			}
			AdministrativeAsset administrativeAsset2 = await db.SaveAdministrativeAsset(id, input, ct, Actor(c).Department);
			return ((object)administrativeAsset2 == null) ? Error("El activo no existe o su identificador ya está registrado.", 409) : Results.Ok(administrativeAsset2);
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/requests/{id:long}/deliver", (Func<long, AdministrativeDeliveryInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, AdministrativeDeliveryInput input, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag = string.IsNullOrWhiteSpace(input.ReceivedBy);
			bool flag2 = flag;
			if (!flag2)
			{
				int length = input.ReceivedBy.Trim().Length;
				bool flag3 = ((length < 3 || length > 160) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				return Error("Indica quién recibe el activo o documento.");
			}
			if (string.IsNullOrWhiteSpace(input.SealQr) || input.SealQr.Length > 2048)
			{
				return Error("Escanea el QR del sello AUTORIZADO A RETIRAR de esta solicitud.");
			}
			string text = await db.DeliverAdministrativeRequest(id, Actor(context), input, ct);
			if (text != null)
			{
				return Error(text, 409);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireRateLimiting("sensitive-write");
	}

	private static bool CanUseAsset(DocumentActor actor, string kind)
	{
		string[] array = AdministrativeProfiles.Forms(actor.Login);
		if (array == null)
		{
			string role = actor.Role;
			return (role == "Administrator" || role == "Department") ? true : false;
		}
		return Enumerable.Contains(array, (kind == "FLOTA") ? "FLEET" : "MODEM");
	}

	private static string? Validate(AdministrativeAssetInput input)
	{
		string kind = input.Kind;
		bool flag = ((kind == "FLOTA" || kind == "MODEM") ? true : false);
		bool flag2 = !flag || string.IsNullOrWhiteSpace(input.Label) || input.Label.Length > 160 || string.IsNullOrWhiteSpace(input.Identifier) || input.Identifier.Length > 100;
		bool flag3 = flag2;
		if (!flag3)
		{
			bool flag4;
			switch (input.Status)
			{
			case "DISPONIBLE":
			case "ASIGNADO":
			case "MANTENIMIENTO":
			case "BAJA":
				flag4 = true;
				break;
			default:
				flag4 = false;
				break;
			}
			flag3 = !flag4;
		}
		if (flag3)
		{
			return "Completa el tipo, nombre, placa o serial y estado del activo.";
		}
		switch (input.HolderType)
		{
		case null:
		case "AGENCIA":
		case "EMPLEADO":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag || (input.HolderName?.Length ?? 0) > 160 || (input.AgencyCode?.Length ?? 0) > 40 || (input.Notes?.Length ?? 0) > 2000)
		{
			return "Revisa los datos de custodia del activo.";
		}
		if (input.Status == "ASIGNADO" && (string.IsNullOrWhiteSpace(input.HolderType) || string.IsNullOrWhiteSpace(input.HolderName)))
		{
			return "Una asignación requiere agencia o empleado responsable.";
		}
		return null;
	}
}
