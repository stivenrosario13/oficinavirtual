using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

internal static class WorkshopStockExitEndpoints
{
	public static void MapWorkshopStockExitEndpoints(this WebApplication app)
	{
		app.MapPost("/api/workshop/available-inventory/{id:guid}/exit", (Func<Guid, WorkshopStockExitRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, WorkshopStockExitRequest body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			WorkshopActor actor = WorkshopActor.From(context);
			if (!actor.CanOperate)
			{
				return Results.Forbid();
			}
			bool flag = body.AgencyId == Guid.Empty;
			bool flag2 = flag;
			if (!flag2)
			{
				string department = body.Department;
				bool flag3 = ((department == "TECHNOLOGY" || department == "GENERAL_SERVICES") ? true : false);
				flag2 = !flag3;
			}
			if (flag2 || string.IsNullOrWhiteSpace(body.Category) || body.Category.Length > 80 || string.IsNullOrWhiteSpace(body.Description) || body.Description.Length > 1500)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona agencia, departamento, falla y motivo de la salida."
				});
			}
			await db.SupportCatalog(ct);
			try
			{
				SupportTicketRow supportTicketRow = await db.CreateTicket(new TicketCreate(body.AgencyId, body.Category, body.Department, "MEDIUM", "Salida de equipo de Taller", body.Description, "INTERNAL", null, (body.Department == "TECHNOLOGY") ? "TECHNICAL_FAILURE" : null, false), actor.Id, actor.Role, actor.Name, actor.Login, actor.Team, canAssignTickets: false, Array.Empty<TicketEvidenceUpload>(), null, ct, id);
				if ((object)supportTicketRow == null)
				{
					return Results.UnprocessableEntity(new
					{
						error = "No se pudo crear el ticket. Revisa la agencia y la falla seleccionadas; el equipo no salió del inventario."
					});
				}
				broker.Publish("ticket-created");
				broker.Publish("maintenance-workshop-updated");
				return Results.Ok(new { supportTicketRow.Id, supportTicketRow.TicketNumber });
			}
			catch (TicketDuplicateException)
			{
				return Results.Conflict(new
				{
					error = "Ya existe un ticket activo de esta falla para la agencia. No se registró la salida del equipo."
				});
			}
			catch (SqlException ex2) when (ex2.Number == 50001)
			{
				return Results.Conflict(new
				{
					error = ex2.Message
				});
			}
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
	}
}
