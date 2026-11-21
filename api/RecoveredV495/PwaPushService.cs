using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebPush;

internal sealed class PwaPushService : BackgroundService
{
	private sealed record VapidKeyFile(string PublicKey, string PrivateKey);

	private readonly Database db;

	private readonly ILogger<PwaPushService> log;

	private readonly string publicKey;

	private readonly string privateKey;

	private readonly string subject;

	public bool UsesPersistedFallbackKeys { get; }

	public string PublicKey => publicKey;

	public PwaPushService(Database db, ILogger<PwaPushService> log, IWebHostEnvironment environment)
	{
		this.db = db;
		this.log = log;
		subject = ValidSubject(Clean(Environment.GetEnvironmentVariable("VAPID_SUBJECT")));
		string text = Clean(Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY"));
		string text2 = Clean(Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY"));
		if (ValidKeys(text, text2))
		{
			publicKey = text;
			privateKey = text2;
			return;
		}
		if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(text2))
		{
			log.LogWarning("Las claves VAPID configuradas en MonsterASP no son válidas; se usará la identidad push persistente de App_Data.");
		}
		UsesPersistedFallbackKeys = true;
		string text3 = Clean(Environment.GetEnvironmentVariable("VAPID_KEYS_PATH"));
		if (string.IsNullOrWhiteSpace(text3))
		{
			text3 = Path.Combine(environment.ContentRootPath, "App_Data", "Push", "vapid-keys.json");
		}
		try
		{
			if (File.Exists(text3))
			{
				VapidKeyFile vapidKeyFile = JsonSerializer.Deserialize<VapidKeyFile>(File.ReadAllText(text3));
				if ((object)vapidKeyFile != null && ValidKeys(vapidKeyFile.PublicKey, vapidKeyFile.PrivateKey))
				{
					publicKey = vapidKeyFile.PublicKey;
					privateKey = vapidKeyFile.PrivateKey;
					return;
				}
			}
		}
		catch (Exception exception)
		{
			log.LogWarning(exception, "No fue posible leer la identidad push persistente.");
		}
		VapidDetails vapidDetails = VapidHelper.GenerateVapidKeys();
		publicKey = vapidDetails.PublicKey;
		privateKey = vapidDetails.PrivateKey;
		try
		{
			string directoryName = Path.GetDirectoryName(text3);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string text4 = $"{text3}.{Guid.NewGuid():N}.tmp";
			File.WriteAllText(text4, JsonSerializer.Serialize(new VapidKeyFile(publicKey, privateKey)));
			File.Move(text4, text3, overwrite: true);
		}
		catch (Exception exception2)
		{
			log.LogError(exception2, "No fue posible guardar la identidad push. Las suscripciones podrían renovarse tras reiniciar el servidor.");
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Dispatch(stoppingToken);
			}
			catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
			{
				log.LogWarning(exception, "No fue posible consultar notificaciones push pendientes");
			}
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(10.0), stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
		}
	}

	private async Task Dispatch(CancellationToken ct)
	{
		WebPushClient client = new WebPushClient();
		VapidDetails vapid = new VapidDetails(subject, publicKey, privateKey);
		List<PushDeliveryRow> ticketDeliveries = await db.PendingPushDeliveries(ct);
		List<PushDeliveryRow> list = ticketDeliveries;
		list.AddRange(await db.PendingFindingPushDeliveries(ct));
		HttpStatusCode statusCode = default;
		foreach (PushDeliveryRow item in ticketDeliveries)
		{
			try
			{
				string stringToEscape = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc).ToString("O");
				bool flag = item.TicketType == "FINDING";
				string url = (flag ? $"/?notification=finding&findingId={item.TicketId:D}&department={Uri.EscapeDataString(item.AssignedDepartment)}&notificationKey={Uri.EscapeDataString($"FINDING_EVENT:{item.NotificationId:D}")}&notificationAt={Uri.EscapeDataString(stringToEscape)}" : $"/?notification=ticket&ticketId={item.TicketId:D}&ticket={item.TicketNumber}&department={Uri.EscapeDataString(item.AssignedDepartment)}&ticketType={Uri.EscapeDataString(item.TicketType)}&ticketStatus={Uri.EscapeDataString(item.TicketStatus)}&notificationKey={Uri.EscapeDataString($"TICKET_EVENT:{item.NotificationId:D}")}&notificationAt={Uri.EscapeDataString(stringToEscape)}");
				string payload = JsonSerializer.Serialize(new
				{
					title = (flag ? "Hallazgo automático asignado" : $"Ticket #{item.TicketNumber}"),
					body = (string.IsNullOrWhiteSpace(item.Message) ? item.Subject : item.Message),
					icon = "/loto-real-logo-transparent.png",
					tag = $"ticket-{item.TicketNumber}-{item.NotificationId:N}",
					url = url,
					requireInteraction = true,
					renotify = false,
					vibrate = new int[3] { 180, 80, 180 }
				});
				await client.SendNotificationAsync(new PushSubscription(item.Endpoint, item.P256dh, item.Auth), payload, vapid);
				await db.MarkPushDelivered(item.NotificationId, item.Endpoint, ct);
			}
			catch (WebPushException ex) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				statusCode = ex.StatusCode;
				return (statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Gone) ? true : false;
			}).Invoke())
			{
				await db.DeletePushSubscription(item.Endpoint, ct);
			}
			catch (Exception exception) when (!ct.IsCancellationRequested)
			{
				log.LogWarning(exception, "No se pudo entregar la notificación push para {Endpoint}", item.Endpoint);
			}
		}
		foreach (ChatPushDeliveryRow item2 in await db.PendingChatPushDeliveries(ct))
		{
			try
			{
				string stringToEscape2 = DateTime.SpecifyKind(item2.CreatedAt, DateTimeKind.Utc).ToString("O");
				string url2 = $"/?notification=chat&conversationId={item2.ConversationId:D}&department={Uri.EscapeDataString(item2.AssignedDepartment)}&notificationKey={Uri.EscapeDataString($"CHAT_MESSAGE:{item2.MessageId:D}")}&notificationAt={Uri.EscapeDataString(stringToEscape2)}";
				string payload2 = JsonSerializer.Serialize(new
				{
					title = "Mensaje de " + item2.SenderName,
					body = item2.Message,
					icon = "/loto-real-logo-transparent.png",
					tag = $"chat-{item2.MessageId:N}",
					url = url2,
					requireInteraction = true,
					renotify = false,
					vibrate = new int[3] { 180, 80, 180 }
				});
				await client.SendNotificationAsync(new PushSubscription(item2.Endpoint, item2.P256dh, item2.Auth), payload2, vapid);
				await db.MarkPushDelivered(item2.MessageId, item2.Endpoint, ct);
			}
			catch (WebPushException ex2) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				statusCode = ex2.StatusCode;
				return (statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Gone) ? true : false;
			}).Invoke())
			{
				await db.DeletePushSubscription(item2.Endpoint, ct);
			}
			catch (Exception exception2) when (!ct.IsCancellationRequested)
			{
				log.LogWarning(exception2, "No se pudo entregar la notificación push del mensaje para {Endpoint}", item2.Endpoint);
			}
		}
	}

	public async Task<PushTestResult> SendTest(string userId, CancellationToken ct)
	{
		List<(string Endpoint, string P256dh, string Auth)> subscriptions = await db.PushSubscriptions(userId, ct);
		WebPushClient client = new WebPushClient();
		VapidDetails vapid = new VapidDetails(subject, publicKey, privateKey);
		int sent = 0;
		string failure = null;
		HttpStatusCode statusCode = default;
		foreach (var subscription in subscriptions)
		{
			try
			{
				string payload = JsonSerializer.Serialize(new
				{
					title = "Prueba de notificación",
					body = "Las notificaciones push de Grupo Tejeda están activas en este dispositivo.",
					icon = "/loto-real-logo-transparent.png",
					tag = $"push-test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
					url = "/",
					requireInteraction = true,
					renotify = true,
					vibrate = new int[3] { 180, 80, 180 }
				});
				await client.SendNotificationAsync(new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth), payload, vapid);
				sent++;
			}
			catch (WebPushException ex) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				statusCode = ex.StatusCode;
				return (statusCode == HttpStatusCode.NotFound || statusCode == HttpStatusCode.Gone) ? true : false;
			}).Invoke())
			{
				await db.DeletePushSubscription(subscription.Endpoint, ct);
				failure = "La suscripción anterior expiró. El dispositivo se registrará nuevamente al abrir la app.";
			}
			catch (WebPushException ex2)
			{
				failure = $"El proveedor push rechazó el envío (HTTP {new int?((int)ex2.StatusCode).GetValueOrDefault()}). Activa nuevamente las notificaciones en este dispositivo.";
				log.LogWarning(ex2, "El proveedor push rechazó la prueba para {Endpoint}", subscription.Endpoint);
			}
			catch (Exception exception) when (!ct.IsCancellationRequested)
			{
				failure = "El servidor no pudo completar el envío push. Revisa la conexión saliente HTTPS de MonsterASP.";
				log.LogWarning(exception, "Falló la prueba push para {Endpoint}", subscription.Endpoint);
			}
		}
		return new PushTestResult(sent, subscriptions.Count, failure);
	}

	private static string? Clean(string? value)
	{
		value = value?.Trim();
		if (value != null && value.Length >= 2)
		{
			if (value[0] == '"')
			{
				string? text = value;
				if (text[text.Length - 1] == '"')
				{
					goto IL_0054;
				}
			}
			if (value[0] == '\'')
			{
				string? text2 = value;
				if (text2[text2.Length - 1] == '\'')
				{
					goto IL_0054;
				}
			}
		}
		goto IL_006e;
		IL_006e:
		return value;
		IL_0054:
		string text3 = value;
		value = text3.Substring(1, text3.Length - 1 - 1).Trim();
		goto IL_006e;
	}

	private static string ValidSubject(string? value)
	{
		value = (string.IsNullOrWhiteSpace(value) ? "mailto:soporte@grupotejeda.com" : value);
		try
		{
			VapidHelper.ValidateSubject(value);
			return value;
		}
		catch
		{
			return "mailto:soporte@grupotejeda.com";
		}
	}

	private static bool ValidKeys(string? publicValue, string? privateValue)
	{
		if (string.IsNullOrWhiteSpace(publicValue) || string.IsNullOrWhiteSpace(privateValue))
		{
			return false;
		}
		try
		{
			VapidHelper.ValidatePublicKey(publicValue);
			VapidHelper.ValidatePrivateKey(privateValue);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
