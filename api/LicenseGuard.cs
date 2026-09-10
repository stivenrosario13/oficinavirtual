using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

sealed class LicenseGuard
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    readonly IHttpClientFactory clients;
    readonly SemaphoreSlim refreshLock = new(1, 1);
    readonly string serverUrl;
    readonly string licenseKey;
    readonly string installationId;
    readonly string domain;
    readonly string publicKeyPem;
    readonly string publicKeyBase64;
    readonly string publicKeyXBase64;
    readonly string publicKeyYBase64;
    readonly string cachePath;
    readonly TimeSpan refreshInterval;
    readonly TimeSpan gracePeriod;
    volatile LicenseDecision? current;
    SignedEnvelope? latestEnvelope;

    public bool Enabled { get; }
    public string? LastDiagnostic { get; private set; }

    public LicenseGuard(IHttpClientFactory clients, IWebHostEnvironment environment)
    {
        this.clients = clients;
        Enabled = Bool("LICENSE_ENFORCEMENT_ENABLED");
        serverUrl = Env("LICENSE_SERVER_URL").TrimEnd('/');
        licenseKey = Env("LICENSE_KEY");
        installationId = Env("LICENSE_INSTALLATION_ID");
        domain = Env("LICENSE_DOMAIN");
        publicKeyPem = Env("LICENSE_PUBLIC_KEY_PEM").Replace("\\n", "\n");
        publicKeyBase64 = Env("LICENSE_PUBLIC_KEY_BASE64");
        publicKeyXBase64 = Env("LICENSE_PUBLIC_X_BASE64");
        publicKeyYBase64 = Env("LICENSE_PUBLIC_Y_BASE64");
        refreshInterval = TimeSpan.FromMinutes(Number("LICENSE_REFRESH_MINUTES", 30, 5, 1440));
        gracePeriod = TimeSpan.FromHours(Number("LICENSE_GRACE_HOURS", 48, 1, 168));
        cachePath = string.IsNullOrWhiteSpace(Env("LICENSE_CACHE_PATH"))
            ? Path.Combine(environment.ContentRootPath, "App_Data", "license", "license.cache.json")
            : Path.GetFullPath(Env("LICENSE_CACHE_PATH"), environment.ContentRootPath);
        if (Enabled && (new[] { serverUrl, licenseKey, installationId, domain }.Any(string.IsNullOrWhiteSpace) || (string.IsNullOrWhiteSpace(publicKeyPem) && string.IsNullOrWhiteSpace(publicKeyBase64) && (string.IsNullOrWhiteSpace(publicKeyXBase64) || string.IsNullOrWhiteSpace(publicKeyYBase64)))))
            current = LicenseDecision.ConfigurationError("La licencia del sistema no está configurada completamente.");
    }

    public async Task<LicenseDecision> Check(CancellationToken ct)
    {
        if (!Enabled) return LicenseDecision.Development();
        if (current is { Code: "LICENSE_CONFIG" }) return current;
        var snapshot = current ?? await ReadCache(ct);
        if (snapshot is not null && DateTimeOffset.UtcNow - snapshot.CheckedAt < refreshInterval)
            return current = snapshot;

        await refreshLock.WaitAsync(ct);
        try
        {
            snapshot = current ?? snapshot ?? await ReadCache(ct);
            if (snapshot is not null && DateTimeOffset.UtcNow - snapshot.CheckedAt < refreshInterval) return snapshot;
            try
            {
                var remote = await Refresh(ct);
                LastDiagnostic = null;
                current = remote;
                await WriteCache(remote, ct);
                return remote;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                Console.Error.WriteLine($"License server: {error.Message}");
                LastDiagnostic = error.GetType().Name + ": " + error.Message;
                if (snapshot is { AllowsUse: true } && DateTimeOffset.UtcNow - snapshot.CheckedAt <= gracePeriod)
                    return current = snapshot with { UsingGracePeriod = true, Message = "Validación temporal mediante la última licencia firmada." };
                return current = new(false, "UNAVAILABLE", "LICENSE_UNAVAILABLE", "No fue posible validar la licencia del sistema. Contacte al soporte técnico.", null, DateTimeOffset.UtcNow, false, false);
            }
        }
        finally { refreshLock.Release(); }
    }

    async Task<LicenseDecision> Refresh(CancellationToken ct)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var server) || (server.Scheme != Uri.UriSchemeHttps && !server.IsLoopback))
            throw new InvalidOperationException("LICENSE_SERVER_URL debe utilizar HTTPS.");
        var client = clients.CreateClient("license-server");
        using var response = await client.PostAsJsonAsync($"{serverUrl}/api/v1/license/check", new
        {
            license_key = licenseKey,
            installation_id = installationId,
            domain
        }, JsonOptions, ct);
        if (response.StatusCode is HttpStatusCode.TooManyRequests) throw new HttpRequestException("El servidor de licencias limitó temporalmente las solicitudes.");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<SignedEnvelope>(JsonOptions, ct)
            ?? throw new CryptographicException("Respuesta de licencia vacía.");
        latestEnvelope = envelope;
        return Verify(envelope);
    }

    LicenseDecision Verify(SignedEnvelope envelope)
    {
        var payloadBytes = Convert.FromBase64String(envelope.Payload);
        var signature = Convert.FromBase64String(envelope.Signature);
        using var key = !string.IsNullOrWhiteSpace(publicKeyXBase64) && !string.IsNullOrWhiteSpace(publicKeyYBase64)
            ? ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = Convert.FromBase64String(publicKeyXBase64), Y = Convert.FromBase64String(publicKeyYBase64) } })
            : ECDsa.Create();
        if (!string.IsNullOrWhiteSpace(publicKeyXBase64) && !string.IsNullOrWhiteSpace(publicKeyYBase64)) { }
        else if (!string.IsNullOrWhiteSpace(publicKeyBase64))
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
        else
            key.ImportFromPem(publicKeyPem);
        if (!key.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256))
            throw new CryptographicException("La firma de la licencia no es válida.");
        var payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, JsonOptions)
            ?? throw new CryptographicException("El contenido firmado no es válido.");
        if (!Fixed(payload.LicenseKey, licenseKey) || !Fixed(payload.InstallationId, installationId) || !Fixed(payload.Domain, domain))
            throw new CryptographicException("La respuesta firmada no corresponde a esta instalación.");
        if (payload.CheckedAt > DateTimeOffset.UtcNow.AddMinutes(5) || DateTimeOffset.UtcNow - payload.CheckedAt > TimeSpan.FromHours(24))
            throw new CryptographicException("La fecha de la respuesta firmada no es aceptable.");
        var status = payload.Status.Trim().ToUpperInvariant();
        if (payload.ExpiresAt is { } expires && expires <= DateTimeOffset.UtcNow) status = "EXPIRED";
        var allowed = status == "ACTIVE";
        var message = string.IsNullOrWhiteSpace(payload.Message) ? DefaultMessage(status) : payload.Message.Trim();
        return new(allowed, status, allowed ? "OK" : $"LICENSE_{status}", message, payload.ExpiresAt, payload.CheckedAt, false, true);
    }

    async Task<LicenseDecision?> ReadCache(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(cachePath)) return null;
            var envelope = JsonSerializer.Deserialize<SignedEnvelope>(await File.ReadAllTextAsync(cachePath, ct), JsonOptions);
            if (envelope is null) return null;
            latestEnvelope = envelope;
            return Verify(envelope);
        }
        catch (Exception error) when (error is not OperationCanceledException) { Console.Error.WriteLine($"License cache: {error.Message}"); return null; }
    }

    async Task WriteCache(LicenseDecision decision, CancellationToken ct)
    {
        if (latestEnvelope is null || !decision.SignatureVerified) return;
        var directory = Path.GetDirectoryName(cachePath)!;
        Directory.CreateDirectory(directory);
        var temporary = cachePath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(latestEnvelope, JsonOptions), ct);
        File.Move(temporary, cachePath, true);
    }

    static string DefaultMessage(string status) => status switch
    {
        "MAINTENANCE" => "El sistema se encuentra temporalmente en mantenimiento.",
        "SUSPENDED" => "El servicio está suspendido. Contacte al soporte técnico.",
        "EXPIRED" => "La licencia del sistema ha vencido.",
        "REVOKED" => "Esta instalación ya no está autorizada.",
        _ => "La licencia del sistema no permite continuar."
    };
    static bool Fixed(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left.Trim().ToLowerInvariant()); var b = Encoding.UTF8.GetBytes(right.Trim().ToLowerInvariant());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    static string Env(string key) => Environment.GetEnvironmentVariable(key) ?? "";
    static bool Bool(string key) => bool.TryParse(Env(key), out var value) && value;
    static double Number(string key, double fallback, double min, double max) => double.TryParse(Env(key), out var value) ? Math.Clamp(value, min, max) : fallback;
}

sealed record SignedEnvelope(string Payload, string Signature);
sealed record LicensePayload(string LicenseKey, string InstallationId, string Domain, string Status, string? Message, DateTimeOffset? ExpiresAt, DateTimeOffset CheckedAt);
sealed record LicenseDecision(bool AllowsUse, string Status, string Code, string Message, DateTimeOffset? ExpiresAt, DateTimeOffset CheckedAt, bool UsingGracePeriod, bool SignatureVerified)
{
    public static LicenseDecision Development() => new(true, "DISABLED", "LICENSE_DISABLED", "Control de licencia desactivado en esta instalación.", null, DateTimeOffset.UtcNow, false, false);
    public static LicenseDecision ConfigurationError(string message) => new(false, "CONFIGURATION_ERROR", "LICENSE_CONFIG", message, null, DateTimeOffset.UtcNow, false, false);
}
