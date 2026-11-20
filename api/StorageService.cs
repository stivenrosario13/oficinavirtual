using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

sealed class StorageServiceException(string code, string message, int status = 503) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public bool Retryable { get; init; }
    public TimeSpan? RetryAfter { get; init; }
}

interface IStorageUploadQueue
{
    Task<StorageReference> Enqueue(byte[] bytes, string mime, string kind, CancellationToken ct);
    Task<byte[]> Read(StorageReference reference, CancellationToken ct);
    Task<StorageReference?> Resolve(StorageReference reference, CancellationToken ct);
}

// A versioned reference fits the existing binary columns. It contains no file bytes,
// credentials or expiring URL. Legacy values remain readable without configuration.
record StorageReference(string Id, string Mime, string Sha256, int Size);

record StorageOrigin(string Source,string Group)
{
    static readonly AsyncLocal<StorageOrigin?> current=new();
    public static StorageOrigin Current=>current.Value??new("unknown","unknown");
    public static IDisposable Scope(string source,string group){var previous=current.Value;current.Value=new(source,group);return new Restore(()=>current.Value=previous);}
    sealed class Restore(Action restore):IDisposable{public void Dispose()=>restore();}
}

static class StorageFiles
{
    internal const string Prefix = "REGISTRO-STORAGE-V1:";
    internal static StorageService? Service { get; private set; }
    internal static IStorageUploadQueue? Queue { get; set; }
    public static bool Enabled { get; private set; }
    public static void Configure(IConfiguration config, StorageService? service = null)
    {
        Queue = null;
        Enabled = !string.Equals(config["STORAGE_ENABLED"], "false", StringComparison.OrdinalIgnoreCase);
        var key = config["STORAGE_KEY"];
        Service = service ?? (string.IsNullOrWhiteSpace(key) ? null : new StorageService(key));
    }
    public static StorageService Required() => Service ?? throw new StorageServiceException("STORAGE_CONFIG", "El servidor no tiene configurada la clave de almacenamiento (STORAGE_KEY). No se puede consultar ni transferir archivos a la API hasta restaurarla.");
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    public static string? DetectMime(byte[] b)
    {
        if (b.AsSpan().StartsWith("%PDF-"u8)) return "application/pdf";
        if (b.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255) return "image/jpeg";
        if (b.AsSpan().StartsWith("GIF87a"u8) || b.AsSpan().StartsWith("GIF89a"u8)) return "image/gif";
        if (b.Length >= 12 && b.AsSpan(0,4).SequenceEqual("RIFF"u8) && b.AsSpan(8,4).SequenceEqual("WEBP"u8)) return "image/webp";
        if (b.Length >= 16 && b.AsSpan(4,4).SequenceEqual("ftyp"u8) && (b.AsSpan(8,4).SequenceEqual("avif"u8) || b.AsSpan(8,4).SequenceEqual("avis"u8))) return "image/avif";
        return null;
    }
    public static bool IsReference(byte[] bytes) => bytes.AsSpan().StartsWith(Encoding.UTF8.GetBytes(Prefix));
    public static byte[] Encode(StorageReference reference) => Encoding.UTF8.GetBytes(Prefix + JsonSerializer.Serialize(reference));
    public static StorageReference Decode(byte[] bytes) => JsonSerializer.Deserialize<StorageReference>(Encoding.UTF8.GetString(bytes)[Prefix.Length..]) ?? throw new StorageServiceException("STORAGE_REFERENCE", "Referencia de archivo inválida.");
    public static async Task<byte[]> Store(byte[] bytes, string kind, CancellationToken ct)
    {
        if (IsReference(bytes)) return bytes;
        var mime = DetectMime(bytes);
        // Office documents and audio are outside the service's supported formats.
        if (mime is null)
        {
            if (bytes.Length>=12 && bytes.AsSpan(4,4).SequenceEqual("ftyp"u8) && new[]{"heic","heix","hevc","hevx","heim","heis","mif1","msf1"}.Contains(Encoding.ASCII.GetString(bytes,8,4))
                || bytes.AsSpan().StartsWith("BM"u8) || bytes.AsSpan().StartsWith(new byte[]{73,73,42,0}) || bytes.AsSpan().StartsWith(new byte[]{77,77,0,42}))
                throw new StorageServiceException("STORAGE_FORMAT","Convierte esta imagen a JPG, PNG o WebP para guardarla en el almacenamiento.",422);
            return bytes;
        }
        if(!Enabled)throw new StorageServiceException("STORAGE_DISABLED","El almacenamiento está desactivado. Actívalo para guardar imágenes y PDF; no se guardaron sus bytes en SQL.");
        // Diagnostics must still exercise the provider, never succeed on staging alone.
        return Encode(Queue is not null && kind != "integration-verification"
            ? await Queue.Enqueue(bytes, mime, kind, ct)
            : await Required().Upload(bytes, mime, kind, ct,deferRetries:kind=="integration-verification"));
    }
    public static bool IsQueued(StorageReference reference) => reference.Id.StartsWith("queue:", StringComparison.Ordinal);
    public static async Task<byte[]> Read(byte[] bytes, CancellationToken ct)
    {
        if (!IsReference(bytes)) return bytes;
        var reference = Decode(bytes);
        return IsQueued(reference) ? await (Queue ?? throw new StorageServiceException("STORAGE_QUEUE_CONFIG", "La cola privada de archivos no está disponible.")).Read(reference, ct) : await Required().Download(reference, ct);
    }
    public static async Task<string?> StoreDataUrl(string? value, CancellationToken ct)
    {
        if (value is null || !value.StartsWith("data:", StringComparison.Ordinal)) return value;
        var comma = value.IndexOf(',');
        if (comma < 0 || !value[..comma].EndsWith(";base64", StringComparison.Ordinal))
        {
            if(value.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase)||value.StartsWith("data:application/pdf",StringComparison.OrdinalIgnoreCase))
                throw new StorageServiceException("STORAGE_FORMAT","La imagen o el PDF incrustado debe usar base64 válido.",422);
            return value;
        }
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value[(comma+1)..]); }
        catch (FormatException) { throw new StorageServiceException("STORAGE_FORMAT", "Imagen incrustada inválida.", 400); }
        if (DetectMime(bytes) is null)
        {
            if(value.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase)||value.StartsWith("data:application/pdf",StringComparison.OrdinalIgnoreCase))
                throw new StorageServiceException("STORAGE_FORMAT","El archivo incrustado debe ser una imagen compatible o un PDF válido.",422);
            return value;
        }
        return Encoding.UTF8.GetString(await Store(bytes, "embedded-image", ct));
    }
    public static async Task<string?> ReadDataUrl(string? value, CancellationToken ct)
    {
        if (value is null || !value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        var reference = Decode(Encoding.UTF8.GetBytes(value));
        return $"data:{reference.Mime};base64,{Convert.ToBase64String(await Read(Encoding.UTF8.GetBytes(value), ct))}";
    }
    public static async Task<string?> ResolveQueuedReferences(string value, CancellationToken ct)
    {
        bool pending = false;
        async Task<string> Resolve(string text)
        {
            if (!text.StartsWith(Prefix, StringComparison.Ordinal)) return text;
            var reference = Decode(Encoding.UTF8.GetBytes(text));
            if (!IsQueued(reference)) return text;
            var remote = Queue is null ? null : await Queue.Resolve(reference, ct);
            if (remote is null) { pending = true; return text; }
            return Encoding.UTF8.GetString(Encode(remote));
        }
        if (value.StartsWith(Prefix, StringComparison.Ordinal)) { var result = await Resolve(value); return pending ? null : result; }
        var node = JsonNode.Parse(value);
        async Task Visit(JsonNode? current)
        {
            if (current is JsonObject obj)
                foreach (var item in obj.ToArray())
                    if (item.Value is JsonValue v && v.TryGetValue<string>(out var text)) obj[item.Key] = await Resolve(text); else await Visit(item.Value);
            if (current is JsonArray arr)
                for (var i = 0; i < arr.Count; i++)
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var text)) arr[i] = await Resolve(text); else await Visit(arr[i]);
        }
        await Visit(node); return pending ? null : node?.ToJsonString();
    }
    public static async Task<string?> TransformJson(string? json, bool upload, CancellationToken ct)
    {
        if (json is null || (upload ? !json.Contains("data:", StringComparison.Ordinal) : !json.Contains(Prefix, StringComparison.Ordinal))) return json;
        var node = JsonNode.Parse(json);
        async Task Visit(JsonNode? current)
        {
            if (current is JsonObject obj)
                foreach (var item in obj.ToArray())
                    if (item.Value is JsonValue v && v.TryGetValue<string>(out var text)) obj[item.Key] = upload ? await StoreDataUrl(text, ct) : await ReadDataUrl(text, ct);
                    else await Visit(item.Value);
            if (current is JsonArray arr)
                for (var i = 0; i < arr.Count; i++)
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var text)) arr[i] = upload ? await StoreDataUrl(text, ct) : await ReadDataUrl(text, ct);
                    else await Visit(arr[i]);
        }
        await Visit(node);
        return node?.ToJsonString() ?? json;
    }
}

sealed class StorageService
{
    readonly HttpClient api;
    readonly HttpClient downloads;
    readonly Func<TimeSpan, CancellationToken, Task> delay;
    readonly SemaphoreSlim requestGate = new(1, 1);
    DateTime nextRequest;
    public StorageService(string key) : this(key,
        new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(3) }) { Timeout = TimeSpan.FromSeconds(60) },
        new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(3) }) { Timeout = TimeSpan.FromSeconds(60) }) { }
    internal StorageService(string key, HttpClient apiClient, HttpClient downloadClient, Func<TimeSpan,CancellationToken,Task>? pause = null)
    {
        api = apiClient; downloads = downloadClient; delay = pause ?? Task.Delay;
        api.BaseAddress = new Uri("https://storage-api.gptcomer.com/");
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }
    async Task<JsonObject> Send(Func<HttpRequestMessage> create, CancellationToken ct, bool deferRetries = false)
    {
        string? lastCode=null,lastRequestId=null,lastMessage=null;
        TimeSpan lastWait = TimeSpan.FromSeconds(2);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            TimeSpan wait = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            try
            {
                using var request = create();
                // At most 20 API requests/second from this application client.
                await requestGate.WaitAsync(ct);
                try { var pause = nextRequest - DateTime.UtcNow; if (pause > TimeSpan.Zero) await delay(pause, ct); nextRequest = DateTime.UtcNow.AddMilliseconds(50); }
                finally { requestGate.Release(); }
                using var response = await api.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct) ?? throw new StorageServiceException("STORAGE_RESPONSE", "Respuesta vacía del servicio de archivos.");
                var retry = (int)response.StatusCode is 429 or 503 or 507;
                JsonObject? body=null;
                try { body=await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken:ct); }
                catch(JsonException) { }
                catch(NotSupportedException) { }
                var code=body?["error"]?["code"]?.GetValue<string>() ?? "storage_error";
                var requestId=body?["error"]?["request_id"]?.GetValue<string>();
                var providerMessage=body?["error"]?["message"]?.GetValue<string>();
                lastCode=code;lastRequestId=requestId;lastMessage=providerMessage is {Length:>500}?providerMessage[..500]:providerMessage;
                if (!retry)
                {
                    throw new StorageServiceException(code, $"El servicio de archivos rechazó la operación ({code}; solicitud {requestId ?? "sin id"}).", response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? 503 : 422);
                }
                wait = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is DateTimeOffset date ? date - DateTimeOffset.UtcNow : wait);
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            lastWait = wait;
            if (deferRetries || attempt == 4 || wait > TimeSpan.FromSeconds(60) || lastCode == "storage_quota_exceeded") break;
            await delay(TimeSpan.FromMilliseconds(Math.Max(0, wait.TotalMilliseconds) + Random.Shared.Next(100, 400)), ct);
        }
        var explanation=lastCode=="storage_quota_exceeded"?"El proveedor bloqueó la carga por su cuota de espacio total. "+(string.IsNullOrWhiteSpace(lastMessage)?"Debe habilitar capacidad para esta aplicación.":"Respuesta del proveedor: "+lastMessage+".")+" Reducir la velocidad no cambia esa cuota.":"El servicio de archivos no está disponible. Conserva el archivo y vuelve a intentarlo.";
        throw new StorageServiceException(lastCode??"STORAGE_UNAVAILABLE", explanation+$" Código: {lastCode??"network_error"}; solicitud: {lastRequestId??"sin id"}.") { Retryable = true, RetryAfter = lastWait };
    }
    public Task<JsonObject> WhoAmI(CancellationToken ct) => Send(() => new(HttpMethod.Get, "v1/whoami"), ct);
    public Task<JsonObject> Usage(CancellationToken ct) => Send(() => new(HttpMethod.Get, "v1/usage"), ct, true);
    public async Task<StorageReference> Upload(byte[] bytes, string mime, string kind, CancellationToken ct, string? existingId = null, Func<string,Task>? accepted = null, bool deferRetries = false)
    {
        var limit = mime == "application/pdf" ? 50 * 1024 * 1024 : 25 * 1024 * 1024;
        if (bytes.Length > limit) throw new StorageServiceException("STORAGE_SIZE", "El archivo supera el límite del servicio de almacenamiento.", 413);
        var hash = StorageFiles.Hash(bytes);
        var key = "registro-v1-" + StorageFiles.Hash(Encoding.UTF8.GetBytes(kind + ":" + hash));
        var file = existingId is not null ? await Send(() => new(HttpMethod.Get, "v1/files/" + Uri.EscapeDataString(existingId)), ct, deferRetries) : await Send(() => {
            var request = new HttpRequestMessage(HttpMethod.Post, "v1/files");
            request.Headers.Add("Idempotency-Key", key);
            var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(bytes); content.Headers.ContentType = new(mime);
            form.Add(content, "file", "archivo." + (mime == "application/pdf" ? "pdf" : mime.Split('/')[1]));
            form.Add(new StringContent("private"), "visibility");
            form.Add(new StringContent("registro:" + hash), "external_ref");
            form.Add(new StringContent(kind), "kind");
            form.Add(new StringContent("registro-agencias"), "folder");
            request.Content = form; return request;
        }, ct, deferRetries);
        var id = file["id"]?.GetValue<string>() ?? throw new StorageServiceException("STORAGE_RESPONSE", "El servicio no devolvió el identificador del archivo.");
        if (accepted is not null) await accepted(id);
        for (var attempt = 0; file["status"]?.GetValue<string>() == "processing" && attempt < 45; attempt++)
        {
            await delay(TimeSpan.FromSeconds(2), ct);
            file = await Send(() => new(HttpMethod.Get, "v1/files/" + Uri.EscapeDataString(id)), ct, deferRetries);
        }
        var status = file["status"]?.GetValue<string>();
        if (status == "rejected") throw new StorageServiceException("STORAGE_REJECTED", "Archivo rechazado por el servicio: " + (file["reject_reason"]?.GetValue<string>()?.Split(':')[0] ?? "unknown"), 422);
        if (status != "ready") throw new StorageServiceException("STORAGE_PROCESSING", "El archivo continúa en validación. Reintenta con el mismo archivo para completar el guardado.");
        // Verify a usable private download before committing the reference. Images
        // are re-encoded by the provider, so hash the delivered representation.
        var delivered = await DownloadBytes(id, ct, deferRetries);
        var deliveredMime = StorageFiles.DetectMime(delivered);
        if (deliveredMime != mime || (mime == "application/pdf" && StorageFiles.Hash(delivered) != hash))
            throw new StorageServiceException("STORAGE_VERIFY", "No se pudo verificar el archivo almacenado.");
        return new(id, mime, StorageFiles.Hash(delivered), delivered.Length);
    }
    async Task<byte[]> DownloadBytes(string id, CancellationToken ct, bool deferRetries = false)
    {
        var result = await Send(() => new(HttpMethod.Post, "v1/files/" + Uri.EscapeDataString(id) + "/url") { Content = JsonContent.Create(new { ttl = 600 }) }, ct, deferRetries);
        if (!Uri.TryCreate(result["url"]?.GetValue<string>(), UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "files.gptcomer.com" || !url.IsDefaultPort || url.UserInfo.Length != 0)
            throw new StorageServiceException("STORAGE_URL", "El servicio devolvió una dirección de descarga inválida.");
        // Separate client: never forward the API credential to the download host.
        try
        {
            using var response = await downloads.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new StorageServiceException("STORAGE_DOWNLOAD_" + (int)response.StatusCode, "El servidor de archivos rechazó la descarga (" + (int)response.StatusCode + ").", 422) { Retryable = (int)response.StatusCode is 429 or 503 or 507, RetryAfter = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is DateTimeOffset retryDate ? retryDate - DateTimeOffset.UtcNow : null) };
            if (response.Content.Headers.ContentLength > 50L * 1024 * 1024) throw new StorageServiceException("STORAGE_SIZE", "Descarga demasiado grande.");
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            using var output = new MemoryStream(); var buffer = new byte[81920]; int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + count > 50L * 1024 * 1024) throw new StorageServiceException("STORAGE_SIZE", "Descarga demasiado grande.");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        catch (HttpRequestException) { throw new StorageServiceException("STORAGE_DOWNLOAD", "No se pudo descargar el archivo. Inténtalo nuevamente.") { Retryable = true }; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new StorageServiceException("STORAGE_TIMEOUT", "La descarga tardó demasiado. Inténtalo nuevamente.") { Retryable = true }; }
    }
    public async Task<byte[]> Download(StorageReference reference, CancellationToken ct)
    {
        var bytes = await DownloadBytes(reference.Id, ct);
        if (bytes.Length != reference.Size || StorageFiles.Hash(bytes) != reference.Sha256) throw new StorageServiceException("STORAGE_VERIFY", "La descarga no coincide con el archivo verificado.");
        return bytes;
    }
}
