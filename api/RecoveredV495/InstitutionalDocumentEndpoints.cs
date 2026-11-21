using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class InstitutionalDocumentEndpoints
{
	private static DocumentActor Actor(HttpContext c)
	{
		string? id = c.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "";
		string? role = c.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "";
		string? team = c.User.FindFirstValue("support_team") ?? "";
		string? name = c.User.FindFirstValue("display_name") ?? "Usuario";
		string? login = c.User.Identity?.Name ?? "";
		string text = AreaManagerAccess.Scope(c.User);
		string? assignedDepartment = ((text != null && text != "ALL") ? DepartmentAccounts.Names.GetValueOrDefault(text) : null);
		bool warehouseRequestOnly = WarehouseRequesterProfile.IsRequester(c.User);
		bool warehouseAuthorize = Permission(c, "canAuthorizeWarehouseRequests") && OperationalPermissions.Allowed(c.User, "canDecideDocuments");
		return new DocumentActor(id, role, team, name, login, assignedDepartment, null, warehouseRequestOnly, warehouseAuthorize);
	}

	private static bool Permission(HttpContext c, string key)
	{
		return WarehousePermission.Has(c.User, key);
	}

	private static string Area(HttpContext c)
	{
		if (!(c.Request.Query["area"] == "communications"))
		{
			return "files";
		}
		return "communications";
	}

	private static string Channel(HttpContext c)
	{
		string text = c.Request.Query["channel"].ToString();
		if (!(text == "requirements"))
		{
			if (text == "all")
			{
				return "all";
			}
			return "requests";
		}
		return "requirements";
	}

	private static IResult Error(string message, int status = 422)
	{
		return Results.Json(new { message }, (JsonSerializerOptions?)null, (string?)null, (int?)status);
	}

	public static void MapInstitutionalDocumentEndpoints(this WebApplication app)
	{
		app.MapGet("/api/institutional-open/{token}/{fileName}", (Func<string, string, HttpContext, Database, IDataProtectionProvider, CancellationToken, Task<IResult>>)(async (string token, string fileName, HttpContext context, Database db, IDataProtectionProvider protection, CancellationToken ct) =>
		{
			try
			{
				ITimeLimitedDataProtector protector = protection.CreateProtector("InstitutionalDocumentNativeOpenV1").ToTimeLimitedDataProtector();
				string s = protector.Unprotect(token, out var _);
				if (!long.TryParse(s, out var result) || result < 1)
				{
					return Results.NotFound();
				}
				(InstitutionalFile, byte[])? tuple = await db.InstitutionalOpenBytes(result, ct);
				if (!tuple.HasValue)
				{
					return Results.NotFound();
				}
				context.Response.Headers.CacheControl = "private,no-store";
				context.Response.Headers.ContentDisposition = "inline";
				return Results.File(tuple.Value.Item2, tuple.Value.Item1.ContentType, null, enableRangeProcessing: true);
			}
			catch (CryptographicException)
			{
				return Results.NotFound();
			}
		}));
		RouteGroupBuilder routeGroupBuilder = app.MapGroup("/api/institutional-documents").RequireAuthorization("PortalUser");
		routeGroupBuilder.AddEndpointFilter(async (EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
		{
			if (!Actor(context.HttpContext).Allowed)
			{
				return Results.Forbid();
			}
			context.HttpContext.Response.Headers.CacheControl = "private,no-store";
			try
			{
				return await next(context);
			}
			catch (AdministrativeFormConflictException ex)
			{
				return Error(ex.Message, 409);
			}
			catch (WarehouseSubmissionConflictException ex2)
			{
				return Error(ex2.Message, 409);
			}
			catch (WarehouseFailureValidationException ex3)
			{
				return Error(ex3.Message);
			}
		});
		routeGroupBuilder.MapGet("/users", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Content(await db.InstitutionalUsers(ct), "application/json")));
		routeGroupBuilder.MapGet("/warehouse-queue", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) => Results.Ok(await db.InstitutionalWarehouseQueue(Actor(c), ct))));
		routeGroupBuilder.MapGet("/warehouse-groups", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) =>
		{
			DocumentActor documentActor = Actor(c);
			return (documentActor.WarehouseRequestsBlocked || !OperationalPermissions.Allowed(c.User, "canRequestWarehouse")) ? Results.Forbid() : Results.Ok(new
			{
				groups = await db.WarehouseRequestGroups(documentActor, ct)
			});
		}));
		routeGroupBuilder.MapGet("/warehouse-submission/{number}", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string number, HttpContext c, Database db, CancellationToken ct) =>
		{
			if (number.Length > 40 || !number.StartsWith("SOL-ALM-", StringComparison.Ordinal))
			{
				return Results.NotFound();
			}
			InstitutionalFile institutionalFile = await db.InstitutionalWarehouseSubmission(number, Actor(c), ct);
			return (institutionalFile == null) ? Results.NotFound() : Results.Ok(new { institutionalFile.Id, institutionalFile.WarehouseRequest });
		}));
		routeGroupBuilder.MapGet("/overview", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) => Results.Ok(await db.InstitutionalOverview(Actor(c), Area(c), Channel(c), ct))));
		routeGroupBuilder.MapGet("", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) =>
		{
			DocumentActor a = Actor(c);
			IEnumerable<InstitutionalFile> source = await db.InstitutionalList(a, Area(c), Channel(c), ct);
			string view = c.Request.Query["view"].ToString();
			string dept = c.Request.Query["department"].ToString();
			source = source.Where((InstitutionalFile d) =>
			{
				if (!(view == "trash"))
				{
					return !d.Trashed;
				}
				return d.Trashed && (d.IsOwner || d.CanManageFiles);
			});
			source = view switch
			{
				"mine" => source.Where((InstitutionalFile d) => d.IsOwner), 
				"shared" => source.Where((InstitutionalFile d) => !d.IsOwner && d.Shared(a)), 
				"transit" => source.Where((InstitutionalFile d) =>
				{
					string status = d.Status;
					return ((status == "pending" || status == "correction_requested") ? true : false) && !d.AwaitingWarehouseAuthorization();
				}), 
				"received" => source.Where((InstitutionalFile d) =>
				{
					bool flag = d.AwaitingWarehouseAuthorization();
					bool flag2 = flag;
					if (!flag2)
					{
						bool hasValue = d.ReceivedUtc.HasValue;
						bool flag3 = hasValue;
						if (flag3)
						{
							bool flag4;
							switch (d.Status)
							{
							case "received":
							case "approved":
							case "rejected":
								flag4 = true;
								break;
							default:
								flag4 = false;
								break;
							}
							flag3 = flag4;
						}
						flag2 = flag3;
					}
					return flag2;
				}), 
				"rejected" => source.Where((InstitutionalFile d) => d.Status == "rejected"), 
				"approved" => source.Where((InstitutionalFile d) => d.Status == "approved"), 
				"sent" => source.Where((InstitutionalFile d) => d.IsOwner && d.SentUtc.HasValue), 
				_ => source, 
			};
			if (dept != "")
			{
				source = source.Where((InstitutionalFile d) => d.Department == dept);
			}
			return Results.Ok(source);
		}));
		routeGroupBuilder.MapGet("/lookup/{number}", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string number, HttpContext c, Database db, CancellationToken ct) =>
		{
			if (number.Length > 40 || !number.StartsWith("SOL-ADM-", StringComparison.Ordinal))
			{
				return Results.NotFound();
			}
			DocumentActor a = Actor(c);
			InstitutionalFile institutionalFile = (await db.InstitutionalList(a, "communications", "requirements", ct)).FirstOrDefault((InstitutionalFile d) => d.AdministrativeRequest?.Number == number && d.RecipientDepartment == "Administración" && !d.Trashed);
			return (institutionalFile == null) ? Results.NotFound() : Results.Ok(new { institutionalFile.Id, institutionalFile.Title, institutionalFile.Status, institutionalFile.Department, institutionalFile.ReceivedUtc, institutionalFile.DecisionUtc, institutionalFile.DecisionBy, institutionalFile.AdministrativeDelivery, institutionalFile.AdministrativeRequest });
		}));
		routeGroupBuilder.MapGet("/{id:long}", (Func<long, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, HttpContext c, Database db, CancellationToken ct) =>
		{
			InstitutionalFile institutionalFile = await db.InstitutionalDetail(id, Actor(c), ct);
			return (institutionalFile == null) ? Results.NotFound() : Results.Ok(institutionalFile);
		}));
		routeGroupBuilder.MapGet("/{id:long}/warehouse-evidence/{evidenceId:guid}", (Func<long, Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, Guid evidenceId, HttpContext c, Database db, CancellationToken ct) =>
		{
			(byte[], string, string)? tuple = await db.InstitutionalWarehouseEvidence(id, evidenceId, Actor(c), ct);
			return (!tuple.HasValue) ? Results.NotFound() : Results.File(tuple.Value.Item1, tuple.Value.Item2, null, enableRangeProcessing: true);
		}));
		routeGroupBuilder.MapGet("/{id:long}/download", (Func<long, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, HttpContext c, Database db, CancellationToken ct) =>
		{
			(InstitutionalFile, byte[])? tuple = await db.InstitutionalBytes(id, Actor(c), ct);
			return (!tuple.HasValue) ? Results.NotFound() : Results.File(tuple.Value.Item2, tuple.Value.Item1.ContentType, tuple.Value.Item1.OriginalFileName, enableRangeProcessing: true);
		}));
		routeGroupBuilder.MapGet("/{id:long}/preview", (Func<long, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, HttpContext c, Database db, CancellationToken ct) =>
		{
			(InstitutionalFile, byte[])? tuple = await db.InstitutionalBytes(id, Actor(c), ct);
			if (!tuple.HasValue)
			{
				return Results.NotFound();
			}
			c.Response.Headers.ContentDisposition = "inline";
			return Results.File(tuple.Value.Item2, tuple.Value.Item1.ContentType, null, enableRangeProcessing: true);
		}));
		routeGroupBuilder.MapGet("/{id:long}/open-link", (Func<long, HttpContext, Database, IDataProtectionProvider, CancellationToken, Task<IResult>>)(async (long id, HttpContext c, Database db, IDataProtectionProvider protection, CancellationToken ct) =>
		{
			InstitutionalFile institutionalFile = await db.InstitutionalDetail(id, Actor(c), ct);
			if (institutionalFile == null)
			{
				return Results.NotFound();
			}
			ITimeLimitedDataProtector protector = protection.CreateProtector("InstitutionalDocumentNativeOpenV1").ToTimeLimitedDataProtector();
			string stringToEscape = protector.Protect(id.ToString(), TimeSpan.FromMinutes(2.0));
			string value = Uri.EscapeDataString(Path.GetFileName(institutionalFile.OriginalFileName));
			string url = $"{c.Request.Scheme}://{c.Request.Host}{c.Request.PathBase}/api/institutional-open/{Uri.EscapeDataString(stringToEscape)}/{value}";
			return Results.Ok(new
			{
				url = url,
				expiresInSeconds = 120
			});
		}));
		routeGroupBuilder.MapPost("", new Func<HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>(SaveDocument)).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPut("/{id:long}/administrative-form", new Func<HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>(SaveDocument)).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/{id:long}/incident-tickets/decision", (Func<long, IncidentTicketDecisionInput, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, IncidentTicketDecisionInput body, HttpContext c, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string? error = await db.DecideIncidentTicket(id, Actor(c), body, ct);
			if (error != null)
			{
				return Error(error, 409);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(new
			{
				ok = true,
				document = await db.InstitutionalDetail(id, Actor(c), ct)
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapPost("/{id:long}/actions/{action}", (Func<long, string, InstitutionalAction, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (long id, string action, InstitutionalAction body, HttpContext c, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (body.SignatureData != null && !OperationalPermissions.Allowed(c.User, "canSignDocuments"))
			{
				return Results.Forbid();
			}
			bool flag;
			switch (action)
			{
			case "approve":
			case "restore":
			case "receive":
			case "unshare":
			case "sign":
			case "move":
			case "edit":
			case "send":
			case "reject":
			case "cancel":
			case "resend":
			case "share":
			case "trash":
			case "purge":
			case "correction":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				return Error("Acción no válida.");
			}
			bool flag2 = !double.IsFinite(body.SealX) || !double.IsFinite(body.SealY);
			bool flag3 = flag2;
			if (!flag3)
			{
				double sealX = body.SealX;
				flag = ((sealX < 2.0 || sealX > 98.0) ? true : false);
				flag3 = flag;
			}
			bool flag4 = flag3;
			if (!flag4)
			{
				double sealX = body.SealY;
				flag = ((sealX < 2.0 || sealX > 98.0) ? true : false);
				flag4 = flag;
			}
			if (flag4)
			{
				return Error("El sello debe quedar dentro de la página.");
			}
			if (action == "edit" && (string.IsNullOrWhiteSpace(body.Title) || body.Title.Length > 160 || (body.Description?.Length ?? 0) > 2000))
			{
				return Error("Revisa el título y la descripción.");
			}
			switch (action)
			{
			case "approve":
			case "reject":
			case "correction":
			case "cancel":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag5 = flag;
			bool flag6 = flag5;
			if (flag6)
			{
				bool flag7 = string.IsNullOrWhiteSpace(body.Note);
				bool flag8 = flag7;
				if (!flag8)
				{
					int length = body.Note.Trim().Length;
					bool flag9 = ((length < 5 || length > 2000) ? true : false);
					flag8 = flag9;
				}
				flag6 = flag8;
			}
			if (flag6)
			{
				return Error("Indica un motivo de 5 a 2,000 caracteres.");
			}
			if ((action == "sign" || (action == "receive" && body.SignatureData != null)) && !MaintenanceDocumentEndpoints.ValidSignature(body.SignatureData))
			{
				return Error("Dibuja una firma PNG válida.");
			}
			bool flag10 = action == "share";
			bool flag11 = flag10;
			if (flag11)
			{
				flag11 = !(await db.InstitutionalRecipient(body.RecipientUserName, body.RecipientDepartment, ct));
			}
			if (flag11)
			{
				return Error("Selecciona un destinatario válido.");
			}
			bool flag12 = action == "send";
			bool flag13 = flag12;
			if (flag13)
			{
				flag13 = !(await db.InstitutionalRecipient(body.RecipientUserName, body.RecipientDepartment, ct));
			}
			if (flag13)
			{
				return Error("Selecciona un destinatario válido.");
			}
			if (action == "approve")
			{
				DocumentActor actor = Actor(c);
				InstitutionalFile institutionalFile = await db.InstitutionalDetail(id, actor, ct);
				if (institutionalFile == null)
				{
					return Results.NotFound(new
					{
						message = "Documento no disponible."
					});
				}
				if (actor.Department == "Almacén" && !Permission(c, "canAuthorizeWarehouseRequests"))
				{
					return Results.Forbid();
				}
				if ((object)institutionalFile.WarehouseRequest != null && institutionalFile.RecipientDepartment == "Almacén")
				{
					if (!actor.Manager || !Permission(c, "canAuthorizeWarehouseRequests"))
					{
						return Results.Json(new
						{
							message = "No tienes permiso para autorizar solicitudes de Almacén."
						}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
					}
					string text = await db.WarehouseApprovePrepared(id, actor, new WarehouseApprovalInput(body.Note), ct);
					if (text != null)
					{
						return Error(text, 409);
					}
					broker.Publish("institutional-document-updated");
					return Results.Ok(new
					{
						ok = true,
						document = await db.InstitutionalDetail(id, actor, ct)
					});
				}
			}
			string text2 = await db.InstitutionalChange(id, Actor(c), action, body, ct);
			if (text2 != null)
			{
				return Error(text2, 409);
			}
			broker.Publish("institutional-document-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapGet("/verify/{token}", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string token, HttpContext c, Database db, CancellationToken ct) =>
		{
			DocumentActor a = Actor(c);
			IEnumerable<InstitutionalFile> source = (await db.InstitutionalList(a, "communications", "requests", ct)).Concat(await db.InstitutionalList(a, "communications", "requirements", ct));
			InstitutionalFile institutionalFile = source.FirstOrDefault((InstitutionalFile x) => x.VerificationToken == token && x.ReceivedUtc.HasValue && !x.Trashed);
			IResult result;
			if (institutionalFile == null)
			{
				result = Results.NotFound();
			}
			else
			{
				string title = institutionalFile.Title;
				string receiptCode = institutionalFile.ReceiptCode;
				DateTime? receivedUtc = institutionalFile.ReceivedUtc;
				string receiverDisplayName = institutionalFile.ReceiverDisplayName;
				string receiverLogin = institutionalFile.ReceiverLogin;
				string status = institutionalFile.Status;
				string statusText = institutionalFile.Status switch
				{
					"pending" => "En tránsito", 
					"received" => "Recibido", 
					"approved" => "Autorizado", 
					"rejected" => "Rechazado", 
					"correction_requested" => "Corrección solicitada", 
					_ => institutionalFile.Status, 
				};
				var anon = new
				{
					label = "Enviado",
					complete = institutionalFile.SentUtc.HasValue,
					at = institutionalFile.SentUtc,
					actor = institutionalFile.OwnerDisplayName
				};
				var anon2 = new
				{
					label = "Recibido en destino",
					complete = institutionalFile.ReceivedUtc.HasValue,
					at = institutionalFile.ReceivedUtc,
					actor = institutionalFile.ReceiverDisplayName
				};
				string label = ((institutionalFile.Status == "rejected") ? "Rechazado" : "Autorizado");
				string status2 = institutionalFile.Status;
				bool complete = ((status2 == "approved" || status2 == "rejected") ? true : false);
				result = Results.Ok(new
				{
					valid = true,
					institution = "REAL · Grupo Tejeda",
					Title = title,
					ReceiptCode = receiptCode,
					ReceivedUtc = receivedUtc,
					ReceiverDisplayName = receiverDisplayName,
					ReceiverLogin = receiverLogin,
					Status = status,
					statusText = statusText,
					steps = new[]
					{
						anon,
						anon2,
						new
						{
							label = label,
							complete = complete,
							at = institutionalFile.DecisionUtc,
							actor = institutionalFile.DecisionBy
						}
					},
					history = institutionalFile.History.Select((InstitutionalEvent h) => new { h.Action, h.Actor, h.Login, h.At, h.PreviousStatus, h.ReceiptCode, h.ReceiverLogin, h.ReceivedUtc })
				});
			}
			return result;
		}));
		routeGroupBuilder.MapGet("/templates", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext c, Database db, CancellationToken ct) => Results.Ok(await db.InstitutionalTemplates(Actor(c), ct))));
		routeGroupBuilder.MapPost("/templates", (Func<EditableTemplateInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (EditableTemplateInput b, HttpContext c, Database db, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(b.Name) || b.Name.Length > 160 || string.IsNullOrWhiteSpace(b.Content) || b.Content.Length > 100000)
			{
				return Error("Nombre de hasta 160 caracteres y contenido de hasta 100,000.");
			}
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(b.Content);
				if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
				{
					return Error("Plantilla no válida.");
				}
			}
			catch (JsonException)
			{
				return Error("Plantilla no válida.");
			}
			DocumentActor a = Actor(c);
			long? num = await db.InstitutionalSaveTemplate(a, b.Id, b.Name.Trim(), b.Content, ct);
			return (!num.HasValue) ? Results.Forbid() : Results.Ok(new InstitutionalTemplate(num.Value, b.Name.Trim(), b.Content, a.Department, a.Id, IsSystem: false, DateTime.UtcNow));
		})).RequireRateLimiting("sensitive-write");
		routeGroupBuilder.MapDelete("/templates/{id:long}", (Func<long, HttpContext, Database, CancellationToken, Task<IResult>>)(async (long id, HttpContext c, Database db, CancellationToken ct) => (await db.InstitutionalDeleteTemplate(Actor(c), id, ct)) ? Results.NoContent() : Results.NotFound())).RequireRateLimiting("sensitive-write");
		static async Task<IResult> SaveDocument(HttpContext c, Database db, LiveNotificationBroker broker, CancellationToken ct)
		{
			DocumentActor a = Actor(c);
			if (!c.Request.HasFormContentType)
			{
				return Error("Selecciona un archivo.");
			}
			IFormCollection form = await c.Request.ReadFormAsync(ct);
			IFormFile f = form.Files.GetFile("file");
			string area = ((form["area"] == "communications") ? "communications" : "files");
			string channel = ((form["channel"] == "requirements") ? "requirements" : "requests");
			bool draft = string.Equals(form["draft"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
			long? templateId = ((long.TryParse(form["templateId"].ToString(), out var result) && result > 0) ? new long?(result) : ((long?)null));
			string title = form["title"].ToString().Trim();
			string description = form["description"].ToString().Trim();
			string recipient = form["recipientUserName"].ToString().Trim();
			if (recipient == "")
			{
				recipient = null;
			}
			string destination = form["recipientDepartment"].ToString().Trim();
			if (destination == "")
			{
				destination = null;
			}
			if (a.WarehouseRequestOnly && (((area != "communications" || channel != "requirements" || destination != "Almacén") | draft) || string.IsNullOrWhiteSpace(form["warehouseRequest"]) || !string.IsNullOrWhiteSpace(form["administrativeRequest"])))
			{
				return Results.Forbid();
			}
			string key = (string.IsNullOrWhiteSpace(form["warehouseRequest"]) ? "canUploadDocuments" : "canRequestWarehouse");
			if (!OperationalPermissions.Allowed(c.User, key))
			{
				return Results.Forbid();
			}
			string sourceDepartment = a.Department;
			string text = form["sourceDepartment"].ToString().Trim();
			if (!string.IsNullOrWhiteSpace(form["warehouseRequest"]))
			{
				DocumentActor documentActor = WarehouseRequestSource.Resolve(a, text);
				if ((object)documentActor == null || (a.WarehouseRequestOnly && documentActor.Department != a.Department))
				{
					return Error("El departamento solicitante no corresponde a tu cuenta.", 403);
				}
				a = documentActor;
				sourceDepartment = a.Department;
			}
			if (a.Role == "Technology" && a.Team == "TECHNICAL_FAILURE" && text == "Tecnología" && !string.IsNullOrWhiteSpace(form["administrativeRequest"]))
			{
				a = a with
				{
					AssignedDepartment = "Tecnología"
				};
				sourceDepartment = a.Department;
			}
			if (a.WarehouseRequestsBlocked && (!string.IsNullOrWhiteSpace(form["warehouseRequest"]) || (area == "communications" && channel == "requirements" && destination == "Almacén")))
			{
				return Results.Forbid();
			}
			if (text != "")
			{
				bool flag;
				switch (text)
				{
				case "Call Center":
				case "Supervisión":
				case "Administración":
				case "Avería Técnica":
				case "Comercialización":
				case "Recursos Humanos":
				case "Comunicación":
				case "Contabilidad":
				case "Almacén":
				case "Tecnología":
				case "Servicios Generales":
				case "Taller":
				case "Tesorería":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag)
				{
					return Error("El departamento solicitante no es válido.");
				}
				if (!a.Manager && text != a.Department)
				{
					return Results.Forbid();
				}
				sourceDepartment = text;
			}
			Guid? folderId = null;
			string text2 = form["folderId"].ToString().Trim();
			if (text2 != "")
			{
				if (area != "files" || !Guid.TryParse(text2, out var parsedFolder))
				{
					return Error("Selecciona una carpeta válida.");
				}
				string text3 = await db.AdministrativeFolderDepartment(parsedFolder, a, ct);
				if (text3 == null)
				{
					return Error("Selecciona una carpeta propia o compartida contigo.");
				}
				folderId = parsedFolder;
				sourceDepartment = text3;
			}
			bool flag2 = f == null;
			bool flag3 = flag2;
			if (!flag3)
			{
				long length = f.Length;
				bool flag = ((length < 1 || length > 10485760) ? true : false);
				flag3 = flag;
			}
			bool flag4 = flag3 || form.Files.Count != 1;
			bool flag5 = flag4;
			if (!flag5)
			{
				int length2 = title.Length;
				bool flag = ((length2 < 1 || length2 > 160) ? true : false);
				flag5 = flag;
			}
			if (flag5 || description.Length > 2000)
			{
				return Error("Indica título, descripción de hasta 2,000 caracteres y un archivo de hasta 10 MB.");
			}
			string text4;
			if (area == "communications" && !draft)
			{
				bool flag6 = channel == "requirements";
				bool flag7 = flag6;
				bool flag;
				if (flag7)
				{
					text4 = destination;
					flag = ((text4 == "Almacén" || text4 == "Administración" || text4 == "Contabilidad" || text4 == "Caja") ? true : false);
					flag7 = flag;
				}
				bool flag8 = flag7 && DepartmentAccounts.Names.ContainsValue(sourceDepartment);
				text4 = a.Role;
				flag = ((text4 == "Administrator" || text4 == "Administrative") ? true : false);
				bool administrativeSelfRequest = flag && channel == "requirements" && destination == "Administración" && sourceDepartment == "Administración";
				bool warehouseSelfRequest = a.Manager && channel == "requirements" && destination == "Almacén" && sourceDepartment == "Almacén" && !string.IsNullOrWhiteSpace(form["warehouseRequest"]);
				bool flag9 = a.Role == "GroupAdministrator" && channel == "requirements";
				bool flag10 = flag9;
				if (flag10)
				{
					text4 = destination;
					flag = ((text4 == "Administración" || text4 == "Almacén") ? true : false);
					flag10 = flag;
				}
				bool flag11 = flag10 && sourceDepartment == "Supervisión";
				if (channel == "requirements" && !a.Manager && !flag8 && !flag11)
				{
					return Results.Forbid();
				}
				if (!(await db.InstitutionalRecipient(recipient, destination, ct)) || recipient == a.Id || (recipient == null && destination == sourceDepartment && !administrativeSelfRequest && !warehouseSelfRequest))
				{
					return Error("Selecciona un departamento o destinatario válido distinto del remitente.");
				}
			}
			if ((area == "communications") & draft)
			{
				recipient = null;
				destination = null;
				channel = "requests";
			}
			string filename = Path.GetFileName(f.FileName.Replace('\\', '/'));
			if (filename.Length > 180)
			{
				return Error("El nombre del archivo es demasiado largo.");
			}
			string ext = Path.GetExtension(filename).ToLowerInvariant();
			string text5 = ext;
			if (text5 != null)
			{
				int length2 = text5.Length;
				if (length2 != 4)
				{
					if (length2 == 5)
					{
						char c2 = text5[1];
						if (c2 != 'd')
						{
							if (c2 != 'j')
							{
								if (c2 == 'x' && text5 == ".xlsx")
								{
									text4 = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
									goto IL_0ddb;
								}
							}
							else if (text5 == ".jpeg")
							{
								goto IL_0db0;
							}
						}
						else if (text5 == ".docx")
						{
							text4 = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
							goto IL_0ddb;
						}
					}
				}
				else
				{
					char c2 = text5[2];
					if ((uint)c2 <= 110u)
					{
						if (c2 != 'd')
						{
							if (c2 != 'n' || !(text5 == ".png"))
							{
								goto IL_0dd4;
							}
							text4 = "image/png";
						}
						else
						{
							if (!(text5 == ".pdf"))
							{
								goto IL_0dd4;
							}
							text4 = "application/pdf";
						}
						goto IL_0ddb;
					}
					if (c2 != 'p')
					{
						if (c2 == 'x' && text5 == ".txt")
						{
							text4 = "text/plain";
							goto IL_0ddb;
						}
					}
					else if (text5 == ".jpg")
					{
						goto IL_0db0;
					}
				}
			}
			goto IL_0dd4;
			IL_0ddb:
			string mime = text4;
			if (mime == "")
			{
				return Error("Formatos permitidos: PDF, PNG, JPG, DOCX, XLSX y TXT.");
			}
			using (MemoryStream stream = new MemoryStream())
			{
				await f.CopyToAsync(stream, ct);
				byte[] bytes = stream.ToArray();
				if ((ext == ".pdf" && (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))) || (ext == ".png" && (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }))) || ((ext == ".jpg" || ext == ".jpeg") && (bytes.Length < 3 || bytes[0] != byte.MaxValue || bytes[1] != 216 || bytes[2] != byte.MaxValue)) || ((ext == ".docx" || ext == ".xlsx") && (bytes.Length < 4 || bytes[0] != 80 || bytes[1] != 75)))
				{
					return Error("El contenido no corresponde al formato declarado.");
				}
				WarehouseRequestInfo warehouseRequest = null;
				string text6 = form["warehouseRequest"].ToString();
				bool flag;
				if (text6 != "")
				{
					if (!OperationalPermissions.Allowed(c.User, "canRequestWarehouse"))
					{
						return Results.Forbid();
					}
					if (area != "communications" || channel != "requirements" || destination != "Almacén" || mime != "application/pdf" || text6.Length > 20000)
					{
						return Error("Solicitud de Almacén no válida.");
					}
					try
					{
						warehouseRequest = JsonSerializer.Deserialize<WarehouseRequestInfo>(text6, new JsonSerializerOptions(JsonSerializerDefaults.Web));
					}
					catch (JsonException)
					{
						return Error("Datos de equipos no válidos.");
					}
					bool flag12 = (object)warehouseRequest == null || warehouseRequest.Number == null || !warehouseRequest.Number.StartsWith("SOL-ALM-", StringComparison.Ordinal) || warehouseRequest.Number.Length > 40 || warehouseRequest.Lines == null;
					bool flag13 = flag12;
					if (!flag13)
					{
						int length2 = warehouseRequest.Lines.Count;
						flag = ((length2 < 1 || length2 > 30) ? true : false);
						flag13 = flag;
					}
					bool flag14 = flag13 || warehouseRequest.Lines.Any((WarehouseRequestLine x) =>
					{
						bool flag37 = (object)x == null || x.Kind != "EQUIPMENT" || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100;
						bool flag38 = flag37;
						if (!flag38)
						{
							int quantity = x.Quantity;
							bool flag39 = ((quantity < 1 || quantity > 10000) ? true : false);
							flag38 = flag39;
						}
						return flag38;
					}) || (warehouseRequest.InstallerId?.Length ?? 0) > 80 || (warehouseRequest.InstallerName?.Length ?? 0) > 160 || warehouseRequest.Lines.Sum((WarehouseRequestLine x) => x.Quantity) > 10000;
					bool flag15 = flag14;
					if (!flag15)
					{
						switch (warehouseRequest.Priority)
						{
						case "LOW":
						case "MEDIUM":
						case "HIGH":
						case "CRITICAL":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						flag15 = !flag;
					}
					if (flag15)
					{
						return Error("Indica equipos o materiales y cantidades válidas (máximo 10,000 unidades por solicitud).");
					}
					bool flag16 = warehouseRequest.FailureCategory != null;
					bool flag17 = flag16;
					if (flag17)
					{
						bool flag18 = warehouseRequest.AgencyId == Guid.Empty || warehouseRequest.TicketId.HasValue;
						bool flag19 = flag18;
						if (!flag19)
						{
							text4 = warehouseRequest.FailureDepartment;
							flag = ((text4 == "TECHNOLOGY" || text4 == "GENERAL_SERVICES") ? true : false);
							flag19 = !flag;
						}
						bool flag20 = flag19;
						if (!flag20)
						{
							int length2 = warehouseRequest.FailureCategory.Length;
							flag = ((length2 < 1 || length2 > 60) ? true : false);
							flag20 = flag;
						}
						flag17 = flag20 || string.IsNullOrWhiteSpace(warehouseRequest.FailureDescription) || warehouseRequest.FailureDescription.Length > 1200;
					}
					if (flag17)
					{
						return Error("Selecciona una agencia, el área de soporte, la falla y su descripción para crear el ticket.");
					}
					if (warehouseRequest.FailureCategory == null && (warehouseRequest.FailureDepartment != null || warehouseRequest.FailureDescription != null || warehouseRequest.FailureTeam != null))
					{
						return Error("Completa la falla y el área de soporte.");
					}
					if (warehouseRequest.FailureCategory != null)
					{
						warehouseRequest = WarehouseRequestSource.RouteFailure(warehouseRequest, a, sourceDepartment);
						if ((object)warehouseRequest == null)
						{
							return Error("Las fallas deben asignarse a Tecnología o Servicios Generales.");
						}
					}
				}
				if ((object)warehouseRequest != null)
				{
					warehouseRequest = await db.WarehouseCanonicalRequest(warehouseRequest, a, sourceDepartment, ct);
					if ((object)warehouseRequest == null)
					{
						return Error("La agencia, el ticket vinculado o uno de los códigos de artículo no es válido.");
					}
				}
				AdministrativeRequestInfo administrativeRequest = null;
				string text7 = form["administrativeRequest"].ToString();
				if (text7 != "")
				{
					if (area != "communications" || channel != "requirements" || (destination != "Administración" && destination != "Contabilidad" && destination != "Caja") || mime != "application/pdf" || text7.Length > 30000)
					{
						return Error("Solicitud administrativa no válida.");
					}
					try
					{
						administrativeRequest = JsonSerializer.Deserialize<AdministrativeRequestInfo>(text7, new JsonSerializerOptions(JsonSerializerDefaults.Web));
					}
					catch (JsonException)
					{
						return Error("Datos de la solicitud no válidos.");
					}
					bool flag21 = (object)administrativeRequest == null || administrativeRequest.Number == null || !administrativeRequest.Number.StartsWith("SOL-ADM-", StringComparison.Ordinal) || administrativeRequest.Number.Length > 40;
					bool flag22 = flag21;
					if (!flag22)
					{
						switch (administrativeRequest.AssetType)
						{
						case "FLOTA":
						case "MODEM":
						case "OTRO":
						case "INTERNET":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						flag22 = !flag;
					}
					bool flag23 = flag22;
					if (!flag23)
					{
						text4 = administrativeRequest.TargetType;
						flag = ((text4 == "AGENCIA" || text4 == "EMPLEADO") ? true : false);
						flag23 = !flag;
					}
					if (flag23 || string.IsNullOrWhiteSpace(administrativeRequest.TargetName) || administrativeRequest.TargetName.Length > 160 || (administrativeRequest.AgencyCode?.Length ?? 0) > 40 || (administrativeRequest.Reason?.Length ?? 0) > 4000)
					{
						return Error("Completa el tipo de activo, destino y motivo de la solicitud.");
					}
					string[] source = new string[19]
					{
						"FLEET", "MODEM", "GENERAL", "SIM", "MARBETE", "DEVICE", "PAYMENT", "INTERNET", "CONTRACT_REQUEST", "CONTRACT_RENEWAL",
						"RENTAL_CONTRACT", "RENT_RECEIPT", "INCREASE_LETTER", "FUEL_LOG", "GAS_LOG", "INCIDENT_REPORT", "LABOR_BENEFITS_REQUEST", "BLANK_CHECKS_DELIVERY", "SIGNED_BENEFITS_CHECKS_DELIVERY"
					};
					if (administrativeRequest.FormType != null)
					{
						if (Enumerable.Contains(source, administrativeRequest.FormType))
						{
							JsonElement? formData = administrativeRequest.FormData;
							if (formData.HasValue && formData.GetValueOrDefault().ValueKind == JsonValueKind.Object)
							{
								goto IL_17f4;
							}
						}
						return Error("El formulario administrativo no es válido.");
					}
					goto IL_17f4;
				}
				goto IL_2137;
				IL_17f4:
				if (administrativeRequest.FormType == "LABOR_BENEFITS_REQUEST" && (destination != "Contabilidad" || (sourceDepartment != "Recursos Humanos" && sourceDepartment != "Administración")))
				{
					return Error("La solicitud de prestaciones debe ser enviada por Recursos Humanos o Administración a Contabilidad.");
				}
				if (administrativeRequest.FormType == "BLANK_CHECKS_DELIVERY" && (destination != "Contabilidad" || sourceDepartment != "Administración"))
				{
					return Error("La entrega de cheques en blanco debe ser enviada por Administración a Contabilidad.");
				}
				if (administrativeRequest.FormType == "SIGNED_BENEFITS_CHECKS_DELIVERY" && (destination != "Caja" || sourceDepartment != "Administración"))
				{
					return Error("La entrega de cheques firmados debe ser enviada por Administración a Caja.");
				}
				if (administrativeRequest.FormType == "INCIDENT_REPORT")
				{
					JsonElement data = administrativeRequest.FormData!.Value;
					if (!data.TryGetProperty("rows", out JsonElement incidentRows) || incidentRows.ValueKind != JsonValueKind.Array || incidentRows.GetArrayLength() < 1 || incidentRows.GetArrayLength() > 100 || !data.TryGetProperty("groupName", out JsonElement groupName) || groupName.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(groupName.GetString()) || !data.TryGetProperty("incidentAgencyId", out JsonElement agencyId) || agencyId.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(agencyId.GetString()) || incidentRows.EnumerateArray().Any(row => row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("ticketDate", out JsonElement ticketDate) || ticketDate.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(ticketDate.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) || !row.TryGetProperty("ticketCode", out JsonElement code) || code.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(code.GetString()) || code.GetString()!.Length > 180 || !row.TryGetProperty("agentPhone", out JsonElement contact) || contact.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(contact.GetString()) || contact.GetString()!.Length > 180 || row.TryGetProperty("decisionStatus", out _)))
					{
						return Error("Completa la agencia, el grupo y cada ticket con fecha, número y contacto. Las decisiones se registran después de recibir el informe.");
					}
					if (administrativeRequest.AssetType != "OTRO" || administrativeRequest.TargetType != "AGENCIA")
					{
						return Error("El informe debe estar vinculado a una agencia válida.");
					}
					if (!Guid.TryParse(agencyId.GetString(), out Guid incidentAgencyGuid))
					{
						return Error("Selecciona una agencia válida del catálogo.");
					}
					var incidentAgency = (await db.TicketAgencies(a.Id, a.Role, ct)).FirstOrDefault(row => row.Id == incidentAgencyGuid);
					if (incidentAgency == null)
					{
						return Error("La agencia seleccionada no está disponible para tu cuenta.");
					}
					JsonObject normalized = JsonNode.Parse(data.GetRawText())!.AsObject();
					normalized["groupName"] = incidentAgency.Grupo;
					normalized["incidentAgencyName"] = $"{incidentAgency.Codigo} · {incidentAgency.Terminal}";
					foreach (JsonObject row in normalized["rows"]!.AsArray().OfType<JsonObject>()) row["agencyId"] = incidentAgency.Codigo;
					administrativeRequest = administrativeRequest with
					{
						TargetName = incidentAgency.Terminal,
						AgencyCode = incidentAgency.Codigo,
						FormData = JsonSerializer.SerializeToElement(normalized)
					};
				}
				text4 = administrativeRequest.FormType;
				flag = ((text4 == "FUEL_LOG" || text4 == "GAS_LOG") ? true : false);
				if (flag && administrativeRequest.AssetType != "OTRO")
				{
					return Error("El combustible debe registrarse como formulario de control, sin entrega de activos.");
				}
				if (administrativeRequest.FormType == "FLEET" && (administrativeRequest.AssetType != "FLOTA" || administrativeRequest.TargetType != "EMPLEADO"))
				{
					return Error("La flota es un teléfono asignado a un empleado.");
				}
				if (administrativeRequest.FormType == "MODEM" && (administrativeRequest.AssetType != "MODEM" || administrativeRequest.TargetType != "AGENCIA" || string.IsNullOrWhiteSpace(administrativeRequest.AgencyCode)))
				{
					return Error("Indica la agencia destinataria del módem.");
				}
				if (administrativeRequest.FormType == "FUEL_LOG" && AdministrativeFuel.IsSulay(a.Login))
				{
					administrativeRequest = administrativeRequest with
					{
						FormType = "GAS_LOG"
					};
				}
				if (!AdministrativeProfiles.CanRead(a, administrativeRequest))
				{
					return Results.Forbid();
				}
				bool flag24 = a.Role == "GroupAdministrator";
				bool flag25 = flag24;
				if (flag25)
				{
					text4 = administrativeRequest.FormType;
					flag = ((text4 == "CONTRACT_REQUEST" || text4 == "CONTRACT_RENEWAL" || text4 == "INCIDENT_REPORT") ? true : false);
					flag25 = !flag;
				}
				if (flag25)
				{
					return Results.Forbid();
				}
				if (administrativeRequest.AssetType == "INTERNET")
				{
					if ((!(a.Role == "Administrator") && (!(a.Role == "Technology") || !(a.Team == "TECHNICAL_FAILURE"))) || sourceDepartment != "Tecnología")
					{
						return Results.Forbid();
					}
					InternetRequestInfo internet = administrativeRequest.Internet;
					bool flag26 = (object)internet == null || internet.AgencyId == Guid.Empty;
					bool flag27 = flag26;
					if (!flag27)
					{
						text4 = internet.ServiceKind;
						flag = ((text4 == "BANDA_ANCHA" || text4 == "LINEA_FIJA") ? true : false);
						flag27 = !flag;
					}
					bool flag28 = flag27 || string.IsNullOrWhiteSpace(internet.ServiceNumber) || internet.ServiceNumber.Length > 80;
					bool flag29 = flag28;
					if (!flag29)
					{
						text4 = internet.Company;
						flag = ((text4 == "CLARO" || text4 == "ALTICE") ? true : false);
						flag29 = !flag;
					}
					bool flag30 = flag29;
					if (!flag30)
					{
						text4 = internet.ItemKind;
						flag = ((text4 == "EQUIPO" || text4 == "SIM") ? true : false);
						flag30 = !flag;
					}
					bool flag31 = flag30;
					if (!flag31)
					{
						int length2 = internet.ModemQuantity;
						flag = ((length2 < 0 || length2 > 20) ? true : false);
						flag31 = flag;
					}
					bool flag32 = flag31 || (internet.ItemKind == "EQUIPO" && internet.ModemQuantity < 1) || (internet.ItemKind == "SIM" && internet.ModemQuantity != 0) || string.IsNullOrWhiteSpace(internet.ChangeReason);
					bool flag33 = flag32;
					if (!flag33)
					{
						int length2 = internet.ChangeReason.Trim().Length;
						flag = ((length2 < 5 || length2 > 2000) ? true : false);
						flag33 = flag;
					}
					if (flag33)
					{
						return Error("Completa la agencia, línea, compañía, equipo y motivo del cambio.");
					}
					TicketAgencyRow agency = (await db.TicketAgencies(a.Id, a.Role, ct)).FirstOrDefault((TicketAgencyRow row) => row.Id == internet.AgencyId);
					if ((object)agency == null)
					{
						return Error("Selecciona una agencia activa del catálogo.");
					}
					Guid? ticketId = internet.TicketId;
					if (ticketId.HasValue)
					{
						Guid parsedFolder = ticketId.GetValueOrDefault();
						SupportTicketRow linked = (await db.Tickets(a.Id, a.Role, WarehousePermission.Has(c.User, "canAssignTickets") || AreaManagerAccess.Allowed(c.User), a.Team, a.Login, ct, AreaManagerAccess.Allowed(c.User), parsedFolder)).FirstOrDefault();
						bool flag34 = (object)linked == null || linked.TicketType != "INTERNAL" || linked.AssignedDepartment != "TECHNOLOGY";
						bool flag35 = flag34;
						if (!flag35)
						{
							flag35 = !(await db.InternetTicketAgencyMatches(parsedFolder, internet.AgencyId, ct));
						}
						bool flag36 = flag35;
						if (!flag36)
						{
							switch (linked.Status)
							{
							case "RESOLVED":
							case "CLOSED":
							case "CANCELLED":
								flag = true;
								break;
							default:
								flag = false;
								break;
							}
							flag36 = flag;
						}
						if (flag36)
						{
							return Error("El ticket interno debe estar abierto y pertenecer a la agencia seleccionada.");
						}
						internet = internet with
						{
							TicketNumber = linked.TicketNumber
						};
					}
					else
					{
						internet = internet with
						{
							TicketNumber = null
						};
					}
					administrativeRequest = administrativeRequest with
					{
						TargetType = "AGENCIA",
						TargetName = agency.Terminal,
						AgencyCode = agency.Codigo,
						Reason = internet.ChangeReason.Trim(),
						Internet = internet with
						{
							AgencyCode = agency.Codigo,
							AgencyName = agency.Terminal,
							AgencyGroup = agency.Grupo,
							ServiceNumber = internet.ServiceNumber.Trim(),
							ChangeReason = internet.ChangeReason.Trim()
						}
					};
				}
				else if ((object)administrativeRequest.Internet != null)
				{
					return Error("El detalle de internet solo corresponde a una solicitud de Avería Técnica.");
				}
				goto IL_2137;
				IL_2137:
				if (a.Role == "GroupAdministrator" && (object)administrativeRequest == null && (object)warehouseRequest == null)
				{
					return Results.Forbid();
				}
				InstitutionalFile d = new InstitutionalFile
				{
					Area = area,
					Channel = channel,
					Title = title,
					Description = description,
					OriginalFileName = filename,
					ContentType = mime,
					Size = bytes.Length,
					OwnerUserName = a.Id,
					OwnerDisplayName = a.Name,
					OwnerLogin = a.Login,
					Department = sourceDepartment,
					FolderId = folderId,
					RecipientDepartment = ((area == "communications") ? destination : null),
					RecipientUserName = ((area == "communications") ? recipient : null),
					Status = ((!(area == "communications")) ? "active" : (draft ? "draft" : "pending")),
					SentUtc = ((area == "communications" && !draft) ? new DateTime?(DateTime.UtcNow) : ((DateTime?)null)),
					TemplateId = (((area == "communications") & draft) ? templateId : ((long?)null)),
					WarehouseRequest = warehouseRequest,
					AdministrativeRequest = administrativeRequest
				};
				AdministrativeFuel.Normalize(d);
				if (c.Request.RouteValues.TryGetValue("id", out object value))
				{
					if ((object)administrativeRequest == null || (object)warehouseRequest != null || !long.TryParse(value?.ToString(), out var id) || !int.TryParse(form["revision"], out var result2))
					{
						return Error("Edición no válida.");
					}
					string text8 = await db.InstitutionalEditAdministrativeForm(id, result2, a, d, bytes, ct);
					if (text8 != null)
					{
						return Error(text8, 409);
					}
					broker.Publish("institutional-document-updated");
					return Results.Ok(new { id });
				}
				InstitutionalFile institutionalFile = d;
				long id2;
				if (draft && templateId.HasValue)
				{
					long valueOrDefault = templateId.GetValueOrDefault();
					id2 = await db.InstitutionalArchiveTemplatePdf(a, valueOrDefault, d, bytes, ct);
				}
				else
				{
					id2 = await db.InstitutionalUpload(d, bytes, ct);
				}
				institutionalFile.Id = id2;
				broker.Publish("institutional-document-created");
				if (warehouseRequest?.FailureCategory != null)
				{
					broker.Publish("ticket-created");
				}
				return Results.Ok(d.For(a));
			}
			IL_0db0:
			text4 = "image/jpeg";
			goto IL_0ddb;
			IL_0dd4:
			text4 = "";
			goto IL_0ddb;
		}
	}
}
