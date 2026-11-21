using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

internal sealed class StorageUploadQueue : IStorageUploadQueue
{
	private readonly string jobs;

	private readonly string data;

	private readonly long maxStagedBytes;

	private readonly long dailyBytes;

	private readonly int perMinute;

	private readonly Func<DateTime> now;

	public string Root { get; }

	public StorageUploadQueue(string root, IConfiguration config, Func<DateTime>? clock = null)
	{
		Root = Path.GetFullPath(root);
		jobs = Path.Combine(Root, "jobs");
		data = Path.Combine(Root, "data");
		now = clock ?? ((Func<DateTime>)(() => DateTime.UtcNow));
		maxStagedBytes = ((long.TryParse(config["STORAGE_QUEUE_MAX_BYTES"], out var result) && result > 0) ? result : 2147483648u);
		dailyBytes = ((long.TryParse(config["STORAGE_DAILY_BYTES"], out var result2) && result2 > 0) ? Math.Min(result2, 10000000000L) : 10000000000L);
		perMinute = (int.TryParse(config["STORAGE_UPLOADS_PER_MINUTE"], out var result3) ? Math.Clamp(result3, 1, 60) : 60);
		Directory.CreateDirectory(jobs);
		Directory.CreateDirectory(data);
	}

	private static string CheckedId(string id)
	{
		if (id.Length != 64 || id.Any((char c) => !char.IsAsciiHexDigit(c)))
		{
			throw new StorageServiceException("STORAGE_REFERENCE", "Referencia de cola inválida.");
		}
		return id;
	}

	private string JobPath(string id)
	{
		return Path.Combine(jobs, CheckedId(id) + ".json");
	}

	private string DataPath(string id)
	{
		return Path.Combine(data, CheckedId(id) + ".bin");
	}

	internal static T Load<T>(string path)
	{
		using FileStream utf8Json = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		T val = JsonSerializer.Deserialize<T>(utf8Json);
		if (val == null)
		{
			throw new IOException("Invalid queue state");
		}
		return val;
	}

	internal static void Save<T>(string path, T value)
	{
		string text = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
		{
			JsonSerializer.Serialize(fileStream, value);
			fileStream.Flush(flushToDisk: true);
		}
		File.Move(text, path, overwrite: true);
	}

	private FileStream? Lease(string name)
	{
		try
		{
			return new FileStream(Path.Combine(Root, name + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		}
		catch (IOException)
		{
			return null;
		}
	}

	private StorageQueueControl Control()
	{
		if (!File.Exists(Path.Combine(Root, "control.json")))
		{
			return new StorageQueueControl();
		}
		return Load<StorageQueueControl>(Path.Combine(Root, "control.json"));
	}

	private void SaveControl(StorageQueueControl control)
	{
		Save(Path.Combine(Root, "control.json"), control);
	}

	private IEnumerable<StorageQueueItem> Items()
	{
		return Directory.EnumerateFiles(jobs, "*.json").Select(Load<StorageQueueItem>);
	}

	public async Task<StorageReference> Enqueue(byte[] bytes, string mime, string kind, CancellationToken ct)
	{
		int num = ((mime == "application/pdf") ? 52428800 : 26214400);
		if (bytes.Length > num)
		{
			throw new StorageServiceException("STORAGE_SIZE", "El archivo supera el límite permitido por el servicio.", 413);
		}
		string hash = StorageFiles.Hash(bytes);
		string id = StorageFiles.Hash(Encoding.UTF8.GetBytes(kind + ":" + hash));
		FileStream lease = null;
		int attempt = 0;
		while (lease == null && attempt < 100)
		{
			ct.ThrowIfCancellationRequested();
			lease = Lease("enqueue");
			if (lease == null)
			{
				await Task.Delay(100, ct);
			}
			attempt++;
		}
		if (lease == null)
		{
			throw new StorageServiceException("STORAGE_QUEUE_BUSY", "La cola está ocupada. Vuelve a guardar el archivo.");
		}
		using (lease)
		{
			if (File.Exists(JobPath(id)))
			{
				StorageQueueItem storageQueueItem = Load<StorageQueueItem>(JobPath(id));
				return storageQueueItem.Remote ?? new StorageReference("queue:" + id, mime, hash, bytes.Length);
			}
			long num2 = Directory.EnumerateFiles(data).Sum((string path) => new FileInfo(path).Length);
			if (num2 + bytes.Length > maxStagedBytes)
			{
				throw new StorageServiceException("STORAGE_QUEUE_FULL", "La cola privada alcanzó su capacidad. Los archivos existentes siguen intactos; espera a que se transfieran antes de agregar más.");
			}
			if (!File.Exists(DataPath(id)))
			{
				string temporary = DataPath(id) + "." + Guid.NewGuid().ToString("N") + ".tmp";
				await using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
				{
					await output.WriteAsync(bytes, ct);
					output.Flush(flushToDisk: true);
				}
				File.Move(temporary, DataPath(id));
			}
			Save(JobPath(id), new StorageQueueItem(id, mime, kind, hash, bytes.Length, now())
			{
				Origin = StorageOrigin.Current
			});
			return new StorageReference("queue:" + id, mime, hash, bytes.Length);
		}
	}

	private StorageQueueItem Find(StorageReference reference)
	{
		if (!StorageFiles.IsQueued(reference))
		{
			throw new StorageServiceException("STORAGE_REFERENCE", "Referencia de cola inválida.");
		}
		string path = JobPath(reference.Id.Substring(6));
		if (!File.Exists(path))
		{
			throw new StorageServiceException("STORAGE_QUEUE_MISSING", "No se encontró el archivo en la cola privada del servidor.");
		}
		StorageQueueItem storageQueueItem = Load<StorageQueueItem>(path);
		if (storageQueueItem.Sha256 != reference.Sha256 || storageQueueItem.Mime != reference.Mime || storageQueueItem.Size != reference.Size)
		{
			throw new StorageServiceException("STORAGE_VERIFY", "La referencia no coincide con el archivo en cola.");
		}
		return storageQueueItem;
	}

	public Task<StorageReference?> Resolve(StorageReference reference, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		return Task.FromResult(Find(reference).Remote);
	}

	public async Task<byte[]> Read(StorageReference reference, CancellationToken ct)
	{
		StorageQueueItem item = Find(reference);
		if ((object)item.Remote != null)
		{
			return await StorageFiles.Required().Download(item.Remote, ct);
		}
		if (item.State == "review")
		{
			throw new StorageServiceException(item.ErrorCode ?? "STORAGE_REVIEW", item.Message ?? "Archivo pendiente de revisión.", 422);
		}
		byte[] bytes;
		try
		{
			await using FileStream input = new FileStream(DataPath(item.Id), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous);
			using MemoryStream buffer = new MemoryStream();
			await input.CopyToAsync(buffer, ct);
			bytes = buffer.ToArray();
		}
		catch (FileNotFoundException)
		{
			item = Find(reference);
			if ((object)item.Remote != null)
			{
				return await StorageFiles.Required().Download(item.Remote, ct);
			}
			throw;
		}
		if (StorageFiles.Hash(bytes) != item.Sha256)
		{
			throw new StorageServiceException("STORAGE_VERIFY", "No se pudo verificar el archivo pendiente.");
		}
		return bytes;
	}

	public StorageQueueStatus Status()
	{
		StorageQueueItem[] source = Items().ToArray();
		StorageQueueControl storageQueueControl = Control();
		int num = source.Count((StorageQueueItem x) => (object)x.Remote == null && x.State != "review");
		int num2 = source.Count((StorageQueueItem x) => x.State == "review");
		bool flag = storageQueueControl.NextAttempt > now();
		return new StorageQueueStatus(num, source.Count((StorageQueueItem x) => (object)x.Remote != null), num2, Directory.EnumerateFiles(data).Sum((string path) => new FileInfo(path).Length), storageQueueControl.Blocked ? "blocked" : (flag ? "waiting" : ((num > 0) ? "running" : ((num2 > 0) ? "review" : "idle"))), (storageQueueControl.Blocked | flag) ? (storageQueueControl.Message ?? "Esperando al proveedor") : ((num > 0) ? "Archivos guardados en cola; transferencia automática" : "Sin transferencias pendientes"), flag ? new DateTime?(storageQueueControl.NextAttempt) : ((DateTime?)null), (storageQueueControl.UtcDay == now().Date) ? storageQueueControl.AttemptedBytes : 0, dailyBytes, perMinute, storageQueueControl.MaxPdfPages, source.Where((StorageQueueItem x) => x.State == "review").Take(20).Select((Func<StorageQueueItem, object>)((StorageQueueItem x) => new { x.Id, x.Kind, x.ErrorCode, x.Message }))
			.ToArray())
		{
			VerificationBlockedUntil = ((storageQueueControl.ProviderRetryUntil > now()) ? new DateTime?(storageQueueControl.ProviderRetryUntil) : ((DateTime?)null))
		};
	}

	public async Task<IDisposable> BeginVerification(CancellationToken ct)
	{
		FileStream fileStream;
		while (true)
		{
			ct.ThrowIfCancellationRequested();
			fileStream = Lease("worker");
			if (fileStream != null)
			{
				break;
			}
			await Task.Delay(250, ct);
		}
		return fileStream;
	}

	public async Task ReserveVerificationUpload(int bytes, CancellationToken ct)
	{
		StorageQueueControl control = Control();
		DateTime dateTime = now();
		if (control.ProviderRetryUntil > dateTime)
		{
			throw new StorageServiceException("STORAGE_RETRY_AFTER", "El proveedor exige esperar hasta " + control.ProviderRetryUntil.ToString("u") + " antes de otra subida.")
			{
				RetryAfter = control.ProviderRetryUntil - dateTime
			};
		}
		if (control.NextUpload > dateTime)
		{
			await Task.Delay(control.NextUpload - dateTime, ct);
		}
		dateTime = now();
		if (control.UtcDay != dateTime.Date)
		{
			control.UtcDay = dateTime.Date;
			control.AttemptedBytes = 0L;
		}
		if (control.AttemptedBytes + bytes > dailyBytes)
		{
			throw new StorageServiceException("STORAGE_DAILY_BUDGET", "El presupuesto diario de subidas se agotó. Se podrá probar después de medianoche UTC.")
			{
				RetryAfter = dateTime.Date.AddDays(1.0) - dateTime
			};
		}
		control.AttemptedBytes += bytes;
		control.NextUpload = now().AddSeconds(60.0 / (double)perMinute);
		SaveControl(control);
	}

	public void RecordVerificationError(StorageServiceException error)
	{
		StorageQueueControl storageQueueControl = Control();
		TimeSpan? retryAfter = error.RetryAfter;
		if (retryAfter.HasValue)
		{
			TimeSpan valueOrDefault = retryAfter.GetValueOrDefault();
			if (valueOrDefault > TimeSpan.Zero)
			{
				storageQueueControl.ProviderRetryUntil = now().Add(valueOrDefault);
				if (storageQueueControl.NextAttempt < storageQueueControl.ProviderRetryUntil)
				{
					storageQueueControl.NextAttempt = storageQueueControl.ProviderRetryUntil;
				}
			}
		}
		storageQueueControl.Message = error.Message;
		SaveControl(storageQueueControl);
	}

	public void CompleteVerification(bool passed)
	{
		if (!passed)
		{
			return;
		}
		StorageQueueControl storageQueueControl = Control();
		if (storageQueueControl.ProviderRetryUntil > now())
		{
			return;
		}
		storageQueueControl.Blocked = false;
		storageQueueControl.NextAttempt = default;
		storageQueueControl.Message = null;
		storageQueueControl.UsageCheckedAt = default;
		SaveControl(storageQueueControl);
		foreach (StorageQueueItem item in from i in Items()
			where (object)i.Remote == null && i.State != "review"
			select i)
		{
			item.NextAttempt = default;
			Save(JobPath(item.Id), item);
		}
	}

	public bool Resume()
	{
		using FileStream fileStream = Lease("worker");
		if (fileStream == null)
		{
			return false;
		}
		StorageQueueControl storageQueueControl = Control();
		storageQueueControl.Blocked = false;
		SaveControl(storageQueueControl);
		return true;
	}

	public object Analytics()
	{
		StorageQueueItem[] items = Items().ToArray();
		StorageQueueControl storageQueueControl = Control();
		return new
		{
			byOrigin = Group((StorageQueueItem i) =>
			{
				string text;
				if (!i.Kind.StartsWith("legacy-", StringComparison.Ordinal))
				{
					text = i.Origin?.Source;
					if (text == null)
					{
						return "unknown";
					}
				}
				else
				{
					text = "legacy";
				}
				return text;
			}),
			byGroup = Group((StorageQueueItem i) => i.Origin?.Group ?? "unknown"),
			byModule = Group((StorageQueueItem i) => i.Kind),
			provider = storageQueueControl.ProviderUsage,
			providerCheckedAt = ((storageQueueControl.UsageCheckedAt == default(DateTime)) ? ((DateTime?)null) : new DateTime?(storageQueueControl.UsageCheckedAt))
		};
		object[] Group(Func<StorageQueueItem, string> key)
		{
			return items.GroupBy(key).Select((Func<IGrouping<string, StorageQueueItem>, object>)((IGrouping<string, StorageQueueItem> g) => new
			{
				key = g.Key,
				images = g.Count((StorageQueueItem i) => i.Mime.StartsWith("image/")),
				pdfs = g.Count((StorageQueueItem i) => i.Mime == "application/pdf"),
				bytes = ((IEnumerable<StorageQueueItem>)g).Sum((Func<StorageQueueItem, long>)((StorageQueueItem i) => i.Size)),
				pending = g.Count((StorageQueueItem i) => (object)i.Remote == null && i.State != "review"),
				verified = g.Count((StorageQueueItem i) => (object)i.Remote != null),
				review = g.Count((StorageQueueItem i) => i.State == "review")
			})).ToArray();
		}
	}

	public StorageQueueItem[] Inventory()
	{
		return Items().ToArray();
	}

	public async Task<bool> ProcessOne(CancellationToken ct)
	{
		using FileStream lease = Lease("worker");
		if (lease == null)
		{
			return false;
		}
		StorageQueueControl control = Control();
		DateTime current = now();
		if (control.Blocked || control.NextAttempt > current || control.NextUpload > current)
		{
			return false;
		}
		StorageQueueItem item = (from x in Items()
			where (object)x.Remote == null && x.State != "review" && x.NextAttempt <= current
			orderby x.CreatedAt
			select x).FirstOrDefault();
		if ((object)item == null)
		{
			return false;
		}
		try
		{
			if (!StorageFiles.Enabled)
			{
				throw new StorageServiceException("STORAGE_DISABLED", "Activa el almacenamiento para procesar la cola.");
			}
			StorageService service = StorageFiles.Required();
			if (control.UsageCheckedAt == default(DateTime) || current - control.UsageCheckedAt > TimeSpan.FromMinutes(5.0))
			{
				JsonObject jsonObject = (control.ProviderUsage = await service.Usage(ct));
				control.MaxPending = jsonObject["limits"]?["max_pending"]?.GetValue<int>() ?? 100;
				control.MaxImageBytes = Math.Min(26214400, jsonObject["limits"]?["max_image_bytes"]?.GetValue<int>() ?? 26214400);
				control.MaxPdfBytes = Math.Min(52428800, jsonObject["limits"]?["max_pdf_bytes"]?.GetValue<int>() ?? 52428800);
				control.MaxPdfPages = jsonObject["limits"]?["max_pdf_pages"]?.GetValue<int>() ?? 200;
				if (item.RemoteId == null && (jsonObject["files"]?["processing"]?.GetValue<int>()).GetValueOrDefault() >= control.MaxPending)
				{
					control.NextAttempt = current.AddSeconds(30.0);
					control.Message = "Esperando espacio en la validación del proveedor";
					SaveControl(control);
					return false;
				}
				control.UsageCheckedAt = current;
			}
			if (item.Size > ((item.Mime == "application/pdf") ? control.MaxPdfBytes : control.MaxImageBytes))
			{
				throw new StorageServiceException("STORAGE_SIZE", "El archivo supera el tamaño permitido para esta aplicación.", 413);
			}
			if (control.UtcDay != current.Date)
			{
				control.UtcDay = current.Date;
				control.AttemptedBytes = 0L;
			}
			if (item.RemoteId == null)
			{
				if (control.AttemptedBytes + item.Size > dailyBytes)
				{
					control.NextAttempt = current.Date.AddDays(1.0);
					control.Message = "Límite diario de transferencia: se reanuda a medianoche UTC";
					SaveControl(control);
					return false;
				}
				control.AttemptedBytes += item.Size;
				control.NextUpload = current.AddSeconds(60.0 / (double)perMinute);
			}
			SaveControl(control);
			byte[] bytes = await File.ReadAllBytesAsync(DataPath(item.Id), ct);
			if (StorageFiles.Hash(bytes) != item.Sha256)
			{
				throw new StorageServiceException("STORAGE_VERIFY", "El archivo temporal no supera la comprobación de integridad.", 422);
			}
			item.Attempts++;
			Save(JobPath(item.Id), item);
			StorageQueueItem storageQueueItem = item;
			storageQueueItem.Remote = await service.Upload(bytes, item.Mime, item.Kind, ct, item.RemoteId, (string id) =>
			{
				item.RemoteId = id;
				Save(JobPath(item.Id), item);
				return Task.CompletedTask;
			}, deferRetries: true);
			item.State = "complete";
			item.Message = null;
			item.ErrorCode = null;
			Save(JobPath(item.Id), item);
			control.Message = null;
			control.NextAttempt = default;
			SaveControl(control);
			try
			{
				File.Delete(DataPath(item.Id));
			}
			catch (IOException)
			{
			}
			return true;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (StorageServiceException ex3)
		{
			item.ErrorCode = ex3.Code;
			item.Message = ex3.Message;
			if (ex3.Code == "STORAGE_PROCESSING")
			{
				item.NextAttempt = current.AddSeconds(2.0);
			}
			else if (ex3.Retryable)
			{
				TimeSpan value = ((ex3.Code == "storage_quota_exceeded") ? TimeSpan.FromMinutes(30.0) : (TimeSpan.FromSeconds(Math.Min(900.0, Math.Pow(2.0, Math.Min(item.Attempts + 1, 10)))) + TimeSpan.FromMilliseconds(Random.Shared.Next(100, 1000))));
				if (ex3.RetryAfter > value)
				{
					value = ex3.RetryAfter.Value;
				}
				TimeSpan? retryAfter = ex3.RetryAfter;
				if (retryAfter.HasValue)
				{
					TimeSpan valueOrDefault = retryAfter.GetValueOrDefault();
					if (valueOrDefault > TimeSpan.Zero)
					{
						control.ProviderRetryUntil = now().Add(valueOrDefault);
					}
				}
				control.NextAttempt = now().Add(value);
				item.NextAttempt = control.NextAttempt;
				control.Message = ex3.Message + " Los pendientes están conservados y se reintentarán automáticamente.";
			}
			else if (ex3.Status == 503 && ex3.Code != "STORAGE_VERIFY")
			{
				control.Blocked = true;
				control.Message = ex3.Message + " Corrige la configuración y pulsa Reanudar cola.";
			}
			else
			{
				item.State = "review";
			}
			Save(JobPath(item.Id), item);
			SaveControl(control);
			return false;
		}
		catch (IOException)
		{
			control.Blocked = true;
			control.Message = "No se pudo leer o guardar la cola privada. Revisa espacio y permisos del servidor; los registros no se marcan como completados.";
			SaveControl(control);
			return false;
		}
		catch (Exception)
		{
			control.Blocked = true;
			control.Message = "La respuesta o el estado de almacenamiento requiere revisión. Los pendientes siguen conservados.";
			SaveControl(control);
			return false;
		}
	}
}
