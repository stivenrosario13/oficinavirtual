using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

record StorageQueueItem(string Id, string Mime, string Kind, string Sha256, int Size, DateTime CreatedAt)
{
    public StorageOrigin? Origin {get;set;}
    public string State { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime NextAttempt { get; set; }
    public string? RemoteId { get; set; }
    public StorageReference? Remote { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}
record StorageQueueControl
{
    public DateTime NextAttempt { get; set; }
    public DateTime NextUpload { get; set; }
    public DateTime ProviderRetryUntil {get;set;}
    public DateTime UsageCheckedAt { get; set; }
    public DateTime UtcDay { get; set; }
    public long AttemptedBytes { get; set; }
    public bool Blocked { get; set; }
    public string? Message { get; set; }
    public int MaxPending { get; set; } = 100;
    public int MaxImageBytes { get; set; } = 25 * 1024 * 1024;
    public int MaxPdfBytes { get; set; } = 50 * 1024 * 1024;
    public int MaxPdfPages { get; set; } = 200;
    public System.Text.Json.Nodes.JsonObject? ProviderUsage {get;set;}
}
record StorageQueueStatus(int Pending, int Complete, int Review, long StagedBytes, string State, string Message, DateTime? NextAttempt,
    long DailyAttemptedBytes, long DailyLimitBytes, int UploadsPerMinute, int MaxPdfPages, object[] Issues)
{public DateTime? VerificationBlockedUntil {get;init;}}

// Durable staging is outside wwwroot. SQL receives only versioned references.
// Filesystem leases serialize writers/workers, including overlapping IIS restarts.
sealed class StorageUploadQueue : IStorageUploadQueue
{
    public string Root { get; }
    readonly string jobs, data;
    readonly long maxStagedBytes, dailyBytes;
    readonly int perMinute;
    readonly Func<DateTime> now;
    public StorageUploadQueue(string root, IConfiguration config, Func<DateTime>? clock = null)
    {
        Root = Path.GetFullPath(root); jobs = Path.Combine(Root, "jobs"); data = Path.Combine(Root, "data");
        now = clock ?? (() => DateTime.UtcNow);
        maxStagedBytes = long.TryParse(config["STORAGE_QUEUE_MAX_BYTES"], out var capacity) && capacity > 0 ? capacity : 2L * 1024 * 1024 * 1024;
        dailyBytes = long.TryParse(config["STORAGE_DAILY_BYTES"], out var daily) && daily > 0 ? Math.Min(daily, 10_000_000_000L) : 10_000_000_000L;
        perMinute = int.TryParse(config["STORAGE_UPLOADS_PER_MINUTE"], out var rate) ? Math.Clamp(rate, 1, 60) : 60;
        Directory.CreateDirectory(jobs); Directory.CreateDirectory(data);
    }
    static string CheckedId(string id)
    {
        if (id.Length != 64 || id.Any(c => !char.IsAsciiHexDigit(c))) throw new StorageServiceException("STORAGE_REFERENCE", "Referencia de cola inválida.");
        return id;
    }
    string JobPath(string id) => Path.Combine(jobs, CheckedId(id) + ".json");
    string DataPath(string id) => Path.Combine(data, CheckedId(id) + ".bin");
    internal static T Load<T>(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<T>(input) ?? throw new IOException("Invalid queue state");
    }
    internal static void Save<T>(string path, T value)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { JsonSerializer.Serialize(output, value); output.Flush(true); }
        File.Move(temporary, path, true);
    }
    FileStream? Lease(string name)
    {
        try { return new FileStream(Path.Combine(Root, name + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
    }
    StorageQueueControl Control() => File.Exists(Path.Combine(Root, "control.json")) ? Load<StorageQueueControl>(Path.Combine(Root, "control.json")) : new();
    void SaveControl(StorageQueueControl control) => Save(Path.Combine(Root, "control.json"), control);
    IEnumerable<StorageQueueItem> Items() => Directory.EnumerateFiles(jobs, "*.json").Select(Load<StorageQueueItem>);
    public async Task<StorageReference> Enqueue(byte[] bytes, string mime, string kind, CancellationToken ct)
    {
        var limit = mime == "application/pdf" ? 50 * 1024 * 1024 : 25 * 1024 * 1024;
        if (bytes.Length > limit) throw new StorageServiceException("STORAGE_SIZE", "El archivo supera el límite permitido por el servicio.", 413);
        var hash = StorageFiles.Hash(bytes); var id = StorageFiles.Hash(Encoding.UTF8.GetBytes(kind + ":" + hash));
        FileStream? lease = null;
        for (int attempt = 0; lease is null && attempt < 100; attempt++) { ct.ThrowIfCancellationRequested(); lease = Lease("enqueue"); if (lease is null) await Task.Delay(100, ct); }
        if (lease is null) throw new StorageServiceException("STORAGE_QUEUE_BUSY", "La cola está ocupada. Vuelve a guardar el archivo.");
        using (lease)
        {
            if (File.Exists(JobPath(id)))
            {
                var existing = Load<StorageQueueItem>(JobPath(id));
                return existing.Remote ?? new("queue:" + id, mime, hash, bytes.Length);
            }
            var used = Directory.EnumerateFiles(data).Sum(path => new FileInfo(path).Length);
            if (used + bytes.Length > maxStagedBytes) throw new StorageServiceException("STORAGE_QUEUE_FULL", "La cola privada alcanzó su capacidad. Los archivos existentes siguen intactos; espera a que se transfieran antes de agregar más.");
            if (!File.Exists(DataPath(id)))
            {
                var temporary = DataPath(id) + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                { await output.WriteAsync(bytes, ct); output.Flush(true); }
                File.Move(temporary, DataPath(id));
            }
            Save(JobPath(id), new StorageQueueItem(id, mime, kind, hash, bytes.Length, now()){Origin=StorageOrigin.Current});
            return new("queue:" + id, mime, hash, bytes.Length);
        }
    }
    StorageQueueItem Find(StorageReference reference)
    {
        if (!StorageFiles.IsQueued(reference)) throw new StorageServiceException("STORAGE_REFERENCE", "Referencia de cola inválida.");
        var path = JobPath(reference.Id[6..]);
        if (!File.Exists(path)) throw new StorageServiceException("STORAGE_QUEUE_MISSING", "No se encontró el archivo en la cola privada del servidor.");
        var item = Load<StorageQueueItem>(path);
        if (item.Sha256 != reference.Sha256 || item.Mime != reference.Mime || item.Size != reference.Size) throw new StorageServiceException("STORAGE_VERIFY", "La referencia no coincide con el archivo en cola.");
        return item;
    }
    public Task<StorageReference?> Resolve(StorageReference reference, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult(Find(reference).Remote); }
    public async Task<byte[]> Read(StorageReference reference, CancellationToken ct)
    {
        var item = Find(reference);
        if (item.Remote is not null) return await StorageFiles.Required().Download(item.Remote, ct);
        // Authorization is still enforced by the existing application endpoint.
        if (item.State == "review") throw new StorageServiceException(item.ErrorCode ?? "STORAGE_REVIEW", item.Message ?? "Archivo pendiente de revisión.", 422);
        byte[] bytes;
        try { await using var input = new FileStream(DataPath(item.Id), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous); using var buffer = new MemoryStream(); await input.CopyToAsync(buffer, ct); bytes = buffer.ToArray(); }
        catch (FileNotFoundException) { item = Find(reference); if (item.Remote is not null) return await StorageFiles.Required().Download(item.Remote, ct); throw; }
        if (StorageFiles.Hash(bytes) != item.Sha256) throw new StorageServiceException("STORAGE_VERIFY", "No se pudo verificar el archivo pendiente.");
        return bytes;
    }
    public StorageQueueStatus Status()
    {
        var items = Items().ToArray(); var control = Control(); var pending = items.Count(x => x.Remote is null && x.State != "review");
        var review = items.Count(x => x.State == "review"); var waiting = control.NextAttempt > now();
        return new(pending, items.Count(x => x.Remote is not null), review, Directory.EnumerateFiles(data).Sum(path => new FileInfo(path).Length),
            control.Blocked ? "blocked" : waiting ? "waiting" : pending > 0 ? "running" : review > 0 ? "review" : "idle",
            control.Blocked || waiting ? control.Message ?? "Esperando al proveedor" : pending > 0 ? "Archivos guardados en cola; transferencia automática" : "Sin transferencias pendientes",
            waiting ? control.NextAttempt : null, control.UtcDay == now().Date ? control.AttemptedBytes : 0, dailyBytes, perMinute, control.MaxPdfPages,
            items.Where(x => x.State == "review").Take(20).Select(x => (object)new { x.Id, x.Kind, x.ErrorCode, x.Message }).ToArray()){VerificationBlockedUntil=control.ProviderRetryUntil>now()?control.ProviderRetryUntil:null};
    }
    // A manual check ignores our automatic backoff, but shares the upload lease,
    // provider Retry-After, rate and UTC byte budget with normal transfers.
    public async Task<IDisposable> BeginVerification(CancellationToken ct)
    {
        while(true){ct.ThrowIfCancellationRequested();var lease=Lease("worker");if(lease is not null)return lease;await Task.Delay(250,ct);}
    }
    public async Task ReserveVerificationUpload(int bytes,CancellationToken ct)
    {
        var control=Control();var current=now();
        if(control.ProviderRetryUntil>current)throw new StorageServiceException("STORAGE_RETRY_AFTER","El proveedor exige esperar hasta "+control.ProviderRetryUntil.ToString("u")+" antes de otra subida."){RetryAfter=control.ProviderRetryUntil-current};
        if(control.NextUpload>current)await Task.Delay(control.NextUpload-current,ct);
        current=now();
        if(control.UtcDay!=current.Date){control.UtcDay=current.Date;control.AttemptedBytes=0;}
        if(control.AttemptedBytes+bytes>dailyBytes)throw new StorageServiceException("STORAGE_DAILY_BUDGET","El presupuesto diario de subidas se agotó. Se podrá probar después de medianoche UTC."){RetryAfter=current.Date.AddDays(1)-current};
        control.AttemptedBytes+=bytes;control.NextUpload=now().AddSeconds(60d/perMinute);SaveControl(control);
    }
    public void RecordVerificationError(StorageServiceException error)
    {
        var control=Control();
        if(error.RetryAfter is TimeSpan wait&&wait>TimeSpan.Zero){control.ProviderRetryUntil=now().Add(wait);if(control.NextAttempt<control.ProviderRetryUntil)control.NextAttempt=control.ProviderRetryUntil;}
        control.Message=error.Message;SaveControl(control);
    }
    public void CompleteVerification(bool passed)
    {
        if(!passed)return;
        var control=Control();if(control.ProviderRetryUntil>now())return;
        control.Blocked=false;control.NextAttempt=default;control.Message=null;control.UsageCheckedAt=default;SaveControl(control);
        foreach(var item in Items().Where(i=>i.Remote is null&&i.State!="review")){item.NextAttempt=default;Save(JobPath(item.Id),item);}
    }
    public bool Resume()
    {
        using var lease = Lease("worker"); if (lease is null) return false;
        var control = Control(); control.Blocked = false; SaveControl(control);
        // Preserve Retry-After even when the operator clicks repeatedly.
        return true;
    }
    public object Analytics()
    {
        var items=Items().ToArray();var control=Control();
        object[] Group(Func<StorageQueueItem,string> key)=>items.GroupBy(key).Select(g=>(object)new{key=g.Key,images=g.Count(i=>i.Mime.StartsWith("image/")),pdfs=g.Count(i=>i.Mime=="application/pdf"),bytes=g.Sum(i=>(long)i.Size),pending=g.Count(i=>i.Remote is null&&i.State!="review"),verified=g.Count(i=>i.Remote is not null),review=g.Count(i=>i.State=="review")}).ToArray();
        return new{byOrigin=Group(i=>i.Kind.StartsWith("legacy-",StringComparison.Ordinal)?"legacy":i.Origin?.Source??"unknown"),byGroup=Group(i=>i.Origin?.Group??"unknown"),byModule=Group(i=>i.Kind),provider=control.ProviderUsage,providerCheckedAt=control.UsageCheckedAt==default?(DateTime?)null:control.UsageCheckedAt};
    }
    public StorageQueueItem[] Inventory()=>Items().ToArray();
    public async Task<bool> ProcessOne(CancellationToken ct)
    {
        using var lease = Lease("worker"); if (lease is null) return false;
        var control = Control(); var current = now();
        if (control.Blocked || control.NextAttempt > current || control.NextUpload > current) return false;
        var item = Items().Where(x => x.Remote is null && x.State != "review" && x.NextAttempt <= current).OrderBy(x => x.CreatedAt).FirstOrDefault();
        if (item is null) return false;
        try
        {
            if (!StorageFiles.Enabled) throw new StorageServiceException("STORAGE_DISABLED", "Activa el almacenamiento para procesar la cola.");
            var service = StorageFiles.Required();
            if (control.UsageCheckedAt == default || current - control.UsageCheckedAt > TimeSpan.FromMinutes(5))
            {
                var usage = await service.Usage(ct);
                control.ProviderUsage=usage;
                control.MaxPending = usage["limits"]?["max_pending"]?.GetValue<int>() ?? 100;
                control.MaxImageBytes = Math.Min(25 * 1024 * 1024, usage["limits"]?["max_image_bytes"]?.GetValue<int>() ?? 25 * 1024 * 1024);
                control.MaxPdfBytes = Math.Min(50 * 1024 * 1024, usage["limits"]?["max_pdf_bytes"]?.GetValue<int>() ?? 50 * 1024 * 1024);
                control.MaxPdfPages = usage["limits"]?["max_pdf_pages"]?.GetValue<int>() ?? 200;
                // Always allow polling an already accepted item to leave processing.
                if (item.RemoteId is null && (usage["files"]?["processing"]?.GetValue<int>() ?? 0) >= control.MaxPending)
                { control.NextAttempt = current.AddSeconds(30); control.Message = "Esperando espacio en la validación del proveedor"; SaveControl(control); return false; }
                control.UsageCheckedAt = current;
            }
            if (item.Size > (item.Mime == "application/pdf" ? control.MaxPdfBytes : control.MaxImageBytes)) throw new StorageServiceException("STORAGE_SIZE", "El archivo supera el tamaño permitido para esta aplicación.", 413);
            if (control.UtcDay != current.Date) { control.UtcDay = current.Date; control.AttemptedBytes = 0; }
            if (item.RemoteId is null)
            {
                if (control.AttemptedBytes + item.Size > dailyBytes)
                { control.NextAttempt = current.Date.AddDays(1); control.Message = "Límite diario de transferencia: se reanuda a medianoche UTC"; SaveControl(control); return false; }
                // Count attempted bytes conservatively before sending, even on timeout.
                control.AttemptedBytes += item.Size; control.NextUpload = current.AddSeconds(60d / perMinute);
            }
            SaveControl(control);
            var bytes = await File.ReadAllBytesAsync(DataPath(item.Id), ct);
            if (StorageFiles.Hash(bytes) != item.Sha256) throw new StorageServiceException("STORAGE_VERIFY", "El archivo temporal no supera la comprobación de integridad.", 422);
            item.Attempts++; Save(JobPath(item.Id), item);
            item.Remote = await service.Upload(bytes, item.Mime, item.Kind, ct, item.RemoteId, id => { item.RemoteId = id; Save(JobPath(item.Id), item); return Task.CompletedTask; }, true);
            item.State = "complete"; item.Message = null; item.ErrorCode = null; Save(JobPath(item.Id), item);
            control.Message = null; control.NextAttempt = default; SaveControl(control);
            // This is only the staging copy, after the verified remote reference is
            // durably committed. Original SQL records and remote files are untouched.
            try { File.Delete(DataPath(item.Id)); } catch (IOException) { /* A reader may still hold the verified staging copy. */ }
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (StorageServiceException e)
        {
            item.ErrorCode = e.Code; item.Message = e.Message;
            if (e.Code == "STORAGE_PROCESSING") item.NextAttempt = current.AddSeconds(2);
            else if (e.Retryable)
            {
                var backoff = e.Code == "storage_quota_exceeded" ? TimeSpan.FromMinutes(30) : TimeSpan.FromSeconds(Math.Min(900, Math.Pow(2, Math.Min(item.Attempts + 1, 10)))) + TimeSpan.FromMilliseconds(Random.Shared.Next(100, 1000));
                if (e.RetryAfter > backoff) backoff = e.RetryAfter.Value;
                if(e.RetryAfter is TimeSpan providerWait&&providerWait>TimeSpan.Zero)control.ProviderRetryUntil=now().Add(providerWait);
                control.NextAttempt = now().Add(backoff); item.NextAttempt = control.NextAttempt; control.Message = e.Message + " Los pendientes están conservados y se reintentarán automáticamente.";
            }
            else if (e.Status == 503 && e.Code != "STORAGE_VERIFY") { control.Blocked = true; control.Message = e.Message + " Corrige la configuración y pulsa Reanudar cola."; }
            else item.State = "review";
            Save(JobPath(item.Id), item); SaveControl(control); return false;
        }
        catch (IOException)
        {
            control.Blocked = true; control.Message = "No se pudo leer o guardar la cola privada. Revisa espacio y permisos del servidor; los registros no se marcan como completados."; SaveControl(control); return false;
        }
        catch (Exception)
        {
            control.Blocked = true; control.Message = "La respuesta o el estado de almacenamiento requiere revisión. Los pendientes siguen conservados."; SaveControl(control); return false;
        }
    }
}
