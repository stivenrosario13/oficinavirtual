using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

internal sealed class StorageService
{
	private readonly HttpClient api;

	private readonly HttpClient downloads;

	private readonly Func<TimeSpan, CancellationToken, Task> delay;

	private readonly SemaphoreSlim requestGate = new SemaphoreSlim(1, 1);

	private DateTime nextRequest;

	public StorageService(string key)
		: this(key, new HttpClient(new SocketsHttpHandler
		{
			AllowAutoRedirect = false,
			ConnectTimeout = TimeSpan.FromSeconds(3.0)
		})
		{
			Timeout = TimeSpan.FromSeconds(60.0)
		}, new HttpClient(new SocketsHttpHandler
		{
			AllowAutoRedirect = false,
			ConnectTimeout = TimeSpan.FromSeconds(3.0)
		})
		{
			Timeout = TimeSpan.FromSeconds(60.0)
		})
	{
	}

	internal StorageService(string key, HttpClient apiClient, HttpClient downloadClient, Func<TimeSpan, CancellationToken, Task>? pause = null)
	{
		api = apiClient;
		downloads = downloadClient;
		delay = pause ?? new Func<TimeSpan, CancellationToken, Task>(Task.Delay);
		api.BaseAddress = new Uri("https://storage-api.gptcomer.com/");
		api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
	}

	private async Task<JsonObject> Send(Func<HttpRequestMessage> create, CancellationToken ct, bool deferRetries = false)
	{
		string lastCode = null;
		string lastRequestId = null;
		string lastMessage = null;
		TimeSpan lastWait = TimeSpan.FromSeconds(2.0);
		for (int attempt = 0; attempt < 5; attempt++)
		{
			TimeSpan wait = TimeSpan.FromSeconds(Math.Pow(2.0, attempt));
			try
			{
				using HttpRequestMessage request = create();
				await requestGate.WaitAsync(ct);
				try
				{
					TimeSpan timeSpan = nextRequest - DateTime.UtcNow;
					if (timeSpan > TimeSpan.Zero)
					{
						await delay(timeSpan, ct);
					}
					nextRequest = DateTime.UtcNow.AddMilliseconds(50.0);
				}
				finally
				{
					requestGate.Release();
				}
				using HttpResponseMessage response = await api.SendAsync(request, ct);
				if (response.IsSuccessStatusCode)
				{
					return (await response.Content.ReadFromJsonAsync<JsonObject>(ct)) ?? throw new StorageServiceException("STORAGE_RESPONSE", "Respuesta vacía del servicio de archivos.");
				}
				int statusCode = (int)response.StatusCode;
				bool flag = ((statusCode == 429 || statusCode == 503 || statusCode == 507) ? true : false);
				bool retry = flag;
				JsonObject body = null;
				try
				{
					body = await response.Content.ReadFromJsonAsync<JsonObject>(ct);
				}
				catch (JsonException)
				{
				}
				catch (NotSupportedException)
				{
				}
				string text = body?["error"]?["code"]?.GetValue<string>() ?? "storage_error";
				string text2 = body?["error"]?["request_id"]?.GetValue<string>();
				string text3 = body?["error"]?["message"]?.GetValue<string>();
				lastCode = text;
				lastRequestId = text2;
				lastMessage = ((text3 != null && text3.Length > 500) ? text3.Substring(0, 500) : text3);
				if (!retry)
				{
					string code = text;
					string message = $"El servicio de archivos rechazó la operación ({text}; solicitud {text2 ?? "sin id"}).";
					HttpStatusCode statusCode2 = response.StatusCode;
					flag = ((statusCode2 == HttpStatusCode.Unauthorized || statusCode2 == HttpStatusCode.Forbidden) ? true : false);
					throw new StorageServiceException(code, message, flag ? 503 : 422);
				}
				TimeSpan? timeSpan2 = response.Headers.RetryAfter?.Delta;
				TimeSpan timeSpan3;
				if (!timeSpan2.HasValue)
				{
					DateTimeOffset? dateTimeOffset = response.Headers.RetryAfter?.Date;
					if (dateTimeOffset.HasValue)
					{
						DateTimeOffset valueOrDefault = dateTimeOffset.GetValueOrDefault();
						timeSpan3 = valueOrDefault - DateTimeOffset.UtcNow;
					}
					else
					{
						timeSpan3 = wait;
					}
				}
				else
				{
					timeSpan3 = timeSpan2.GetValueOrDefault();
				}
				wait = timeSpan3;
			}
			catch (HttpRequestException)
			{
			}
			catch (OperationCanceledException) when (!ct.IsCancellationRequested)
			{
			}
			lastWait = wait;
			if (deferRetries || attempt == 4 || wait > TimeSpan.FromSeconds(60.0) || lastCode == "storage_quota_exceeded")
			{
				break;
			}
			await delay(TimeSpan.FromMilliseconds(Math.Max(0.0, wait.TotalMilliseconds) + (double)Random.Shared.Next(100, 400)), ct);
		}
		string text4 = ((lastCode == "storage_quota_exceeded") ? ("El proveedor bloqueó la carga por su cuota de espacio total. " + (string.IsNullOrWhiteSpace(lastMessage) ? "Debe habilitar capacidad para esta aplicación." : ("Respuesta del proveedor: " + lastMessage + ".")) + " Reducir la velocidad no cambia esa cuota.") : "El servicio de archivos no está disponible. Conserva el archivo y vuelve a intentarlo.");
		throw new StorageServiceException(lastCode ?? "STORAGE_UNAVAILABLE", text4 + $" Código: {lastCode ?? "network_error"}; solicitud: {lastRequestId ?? "sin id"}.")
		{
			Retryable = true,
			RetryAfter = lastWait
		};
	}

	public Task<JsonObject> WhoAmI(CancellationToken ct)
	{
		return Send(() => new HttpRequestMessage(HttpMethod.Get, "v1/whoami"), ct);
	}

	public Task<JsonObject> Usage(CancellationToken ct)
	{
		return Send(() => new HttpRequestMessage(HttpMethod.Get, "v1/usage"), ct, deferRetries: true);
	}

	public async Task<StorageReference> Upload(byte[] bytes, string mime, string kind, CancellationToken ct, string? existingId = null, Func<string, Task>? accepted = null, bool deferRetries = false)
	{
		int num = ((mime == "application/pdf") ? 52428800 : 26214400);
		if (bytes.Length > num)
		{
			throw new StorageServiceException("STORAGE_SIZE", "El archivo supera el límite del servicio de almacenamiento.", 413);
		}
		string hash = StorageFiles.Hash(bytes);
		string key = "registro-v1-" + StorageFiles.Hash(Encoding.UTF8.GetBytes(kind + ":" + hash));
		JsonObject jsonObject = ((existingId == null) ? (await Send(() =>
		{
			HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, "v1/files")
			{
				Headers = { { "Idempotency-Key", key } }
			};
			MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();
			ByteArrayContent byteArrayContent = new ByteArrayContent(bytes);
			byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(mime);
			multipartFormDataContent.Add(byteArrayContent, "file", "archivo." + ((mime == "application/pdf") ? "pdf" : mime.Split('/')[1]));
			multipartFormDataContent.Add(new StringContent("private"), "visibility");
			multipartFormDataContent.Add(new StringContent("registro:" + hash), "external_ref");
			multipartFormDataContent.Add(new StringContent(kind), "kind");
			multipartFormDataContent.Add(new StringContent("registro-agencias"), "folder");
			httpRequestMessage.Content = multipartFormDataContent;
			return httpRequestMessage;
		}, ct, deferRetries)) : (await Send(() => new HttpRequestMessage(HttpMethod.Get, "v1/files/" + Uri.EscapeDataString(existingId)), ct, deferRetries)));
		JsonObject file = jsonObject;
		string id = file["id"]?.GetValue<string>() ?? throw new StorageServiceException("STORAGE_RESPONSE", "El servicio no devolvió el identificador del archivo.");
		if (accepted != null)
		{
			await accepted(id);
		}
		int attempt = 0;
		while (file["status"]?.GetValue<string>() == "processing" && attempt < 45)
		{
			await delay(TimeSpan.FromSeconds(2.0), ct);
			file = await Send(() => new HttpRequestMessage(HttpMethod.Get, "v1/files/" + Uri.EscapeDataString(id)), ct, deferRetries);
			attempt++;
		}
		string text = file["status"]?.GetValue<string>();
		if (text == "rejected")
		{
			JsonNode? jsonNode = file["reject_reason"];
			object obj;
			if (jsonNode == null)
			{
				obj = null;
			}
			else
			{
				string value = jsonNode.GetValue<string>();
				obj = ((value != null) ? value.Split(':')[0] : null);
			}
			if (obj == null)
			{
				obj = "unknown";
			}
			throw new StorageServiceException("STORAGE_REJECTED", "Archivo rechazado por el servicio: " + (string?)obj, 422);
		}
		if (text != "ready")
		{
			throw new StorageServiceException("STORAGE_PROCESSING", "El archivo continúa en validación. Reintenta con el mismo archivo para completar el guardado.");
		}
		byte[] array = await DownloadBytes(id, ct, deferRetries);
		string text2 = StorageFiles.DetectMime(array);
		if (text2 != mime || (mime == "application/pdf" && StorageFiles.Hash(array) != hash))
		{
			throw new StorageServiceException("STORAGE_VERIFY", "No se pudo verificar el archivo almacenado.");
		}
		return new StorageReference(id, mime, StorageFiles.Hash(array), array.Length);
	}

	private async Task<byte[]> DownloadBytes(string id, CancellationToken ct, bool deferRetries = false)
	{
		if (!Uri.TryCreate((await Send(() => new HttpRequestMessage(HttpMethod.Post, "v1/files/" + Uri.EscapeDataString(id) + "/url")
		{
			Content = JsonContent.Create(new
			{
				ttl = 600
			})
		}, ct, deferRetries))["url"]?.GetValue<string>(), UriKind.Absolute, out Uri result) || result.Scheme != "https" || result.Host != "files.gptcomer.com" || !result.IsDefaultPort || result.UserInfo.Length != 0)
		{
			throw new StorageServiceException("STORAGE_URL", "El servicio devolvió una dirección de descarga inválida.");
		}
		try
		{
			using HttpResponseMessage response = await downloads.GetAsync(result, HttpCompletionOption.ResponseHeadersRead, ct);
			if (!response.IsSuccessStatusCode)
			{
				StorageServiceException ex = new StorageServiceException("STORAGE_DOWNLOAD_" + (int)response.StatusCode, "El servidor de archivos rechazó la descarga (" + (int)response.StatusCode + ").", 422);
				StorageServiceException ex2 = ex;
				int statusCode = (int)response.StatusCode;
				bool retryable = ((statusCode == 429 || statusCode == 503 || statusCode == 507) ? true : false);
				ex2.Retryable = retryable;
				TimeSpan? timeSpan = response.Headers.RetryAfter?.Delta;
				TimeSpan? retryAfter;
				if (!timeSpan.HasValue)
				{
					DateTimeOffset? dateTimeOffset = response.Headers.RetryAfter?.Date;
					if (dateTimeOffset.HasValue)
					{
						DateTimeOffset valueOrDefault = dateTimeOffset.GetValueOrDefault();
						retryAfter = valueOrDefault - DateTimeOffset.UtcNow;
					}
					else
					{
						retryAfter = null;
					}
				}
				else
				{
					retryAfter = timeSpan;
				}
				ex.RetryAfter = retryAfter;
				throw ex;
			}
			if (response.Content.Headers.ContentLength > 52428800)
			{
				throw new StorageServiceException("STORAGE_SIZE", "Descarga demasiado grande.");
			}
			byte[] result2;
			await using (Stream input = await response.Content.ReadAsStreamAsync(ct))
			{
				using MemoryStream output = new MemoryStream();
				byte[] buffer = new byte[81920];
				int num;
				while ((num = await input.ReadAsync(buffer, ct)) > 0)
				{
					if (output.Length + num > 52428800)
					{
						throw new StorageServiceException("STORAGE_SIZE", "Descarga demasiado grande.");
					}
					output.Write(buffer, 0, num);
				}
				result2 = output.ToArray();
			}
			return result2;
		}
		catch (HttpRequestException)
		{
			throw new StorageServiceException("STORAGE_DOWNLOAD", "No se pudo descargar el archivo. Inténtalo nuevamente.")
			{
				Retryable = true
			};
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			throw new StorageServiceException("STORAGE_TIMEOUT", "La descarga tardó demasiado. Inténtalo nuevamente.")
			{
				Retryable = true
			};
		}
	}

	public async Task<byte[]> Download(StorageReference reference, CancellationToken ct)
	{
		byte[] array = await DownloadBytes(reference.Id, ct);
		if (array.Length != reference.Size || StorageFiles.Hash(array) != reference.Sha256)
		{
			throw new StorageServiceException("STORAGE_VERIFY", "La descarga no coincide con el archivo verificado.");
		}
		return array;
	}
}
