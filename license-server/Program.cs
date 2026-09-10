using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using System.Threading.RateLimiting;

LoadEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));
if (args.Contains("--generate-keys"))
{
    using var generated = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    Console.WriteLine("LICENSE_PRIVATE_KEY_BASE64=" + Convert.ToBase64String(generated.ExportPkcs8PrivateKey()));
    Console.WriteLine("LICENSE_PUBLIC_KEY_BASE64=" + Convert.ToBase64String(generated.ExportSubjectPublicKeyInfo()));
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.AddSingleton<LicenseDatabase>();
builder.Services.AddSingleton<LicenseSigner>();
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    options.OnRejected=async(context,ct)=>{context.HttpContext.Response.Headers.RetryAfter="60";await context.HttpContext.Response.WriteAsJsonAsync(new{error="Demasiadas solicitudes.",code="RATE_LIMITED"},ct);};
    options.AddPolicy("license-check",context=>RateLimitPartition.GetFixedWindowLimiter(ClientKey(context),_=>new FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
    options.AddPolicy("license-admin",context=>RateLimitPartition.GetFixedWindowLimiter(ClientKey(context),_=>new FixedWindowRateLimiterOptions{PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true}));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (SqlException error)
    {
        Console.Error.WriteLine(error);
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { error = "No fue posible consultar la base central de licencias.", code = error.Number == 208 ? "LICENSE_SCHEMA_MISSING" : "LICENSE_DATABASE_ERROR" });
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(error);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "Ocurrió un error interno en el servidor de licencias.", code = "LICENSE_SERVER_ERROR" });
    }
});
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    if(context.Request.IsHttps)context.Response.Headers["Strict-Transport-Security"]="max-age=31536000; includeSubDomains";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapGet("/api/health", async (LicenseDatabase db, CancellationToken ct) =>
{
    try { await db.Ping(ct); return Results.Ok(new { ok = true, service = "registro-agencias-license-server" }); }
    catch (Exception error) { Console.Error.WriteLine(error); return Results.Json(new { ok = false, error = "No fue posible conectar con la base de datos." }, statusCode: 503); }
});

app.MapPost("/api/v1/license/check", async (HttpContext context, LicenseCheckRequest body, LicenseDatabase db, CancellationToken ct) =>
{
    try
    {
        var signer = context.RequestServices.GetRequiredService<LicenseSigner>();
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var domain = NormalizeDomain(body.Domain);
        if (string.IsNullOrWhiteSpace(body.LicenseKey) || body.LicenseKey.Length>128 || string.IsNullOrWhiteSpace(body.InstallationId) || body.InstallationId.Length>128 || string.IsNullOrWhiteSpace(domain) || domain.Length>253)
            return Results.BadRequest(new { error = "Solicitud de licencia incompleta." });
        var result = await db.Check(body.LicenseKey.Trim(), body.InstallationId.Trim(), domain, ip, ct);
        var payload = new LicensePayload(body.LicenseKey.Trim(), body.InstallationId.Trim(), domain, result.Status, result.Message, result.ExpiresAt, DateTimeOffset.UtcNow);
        return Results.Ok(signer.Sign(payload));
    }
    catch (Exception error)
    {
        Console.Error.WriteLine(error);
        return Results.Json(new { error = "Falló la validación central.", code = "LICENSE_CHECK_ERROR" }, statusCode: 500);
    }
}).RequireRateLimiting("license-check");

var admin = app.MapGroup("/api/admin").RequireRateLimiting("license-admin").AddEndpointFilter(async (invocation, next) =>
{
    var context = invocation.HttpContext;
    var configured = Environment.GetEnvironmentVariable("LICENSE_ADMIN_KEY") ?? "";
    var supplied = context.Request.Headers["X-Admin-Key"].ToString();
    if (configured.Length < 32 || !Fixed(configured, supplied)) return Results.Unauthorized();
    return await next(invocation);
});
admin.MapGet("/summary", async (LicenseDatabase db, CancellationToken ct) => Results.Ok(await db.Summary(ct)));
admin.MapGet("/licenses", async (LicenseDatabase db, CancellationToken ct) => Results.Ok(new { licenses = await db.List(ct) }));
admin.MapPost("/licenses", async (CreateLicenseRequest body, LicenseDatabase db, CancellationToken ct) =>
{
    var domain = NormalizeDomain(body.Domain);
    if (string.IsNullOrWhiteSpace(body.ClientName) || body.ClientName.Length>160 || string.IsNullOrWhiteSpace(domain) || domain.Length>253) return Results.BadRequest(new { error = "Cliente y dominio son obligatorios y deben tener una longitud válida." });
    return Results.Ok(await db.Create(body with { Domain = domain }, ct));
});
admin.MapPut("/licenses/{id:long}", async (long id, UpdateLicenseRequest body, LicenseDatabase db, CancellationToken ct) =>
{
    var valid = new[] { "ACTIVE", "MAINTENANCE", "SUSPENDED", "EXPIRED", "REVOKED" };
    var status = (body.Status ?? "").Trim().ToUpperInvariant();
    if (!valid.Contains(status)||(body.Message?.Length??0)>1000) return Results.BadRequest(new { error = "Estado o mensaje inválido." });
    return await db.Update(id, body with { Status = status }, ct) ? Results.Ok(new { ok = true }) : Results.NotFound();
});
admin.MapGet("/logs", async (int? limit, LicenseDatabase db, CancellationToken ct) =>
{
    try { return Results.Ok(new { logs = await db.Logs(Math.Clamp(limit ?? 100, 1, 500), ct) }); }
    catch (Exception error)
    {
        Console.Error.WriteLine(error);
        return Results.Json(new { error = "No fue posible consultar el historial.", code = "LICENSE_LOGS_ERROR" }, statusCode: 500);
    }
});

app.MapFallbackToFile("index.html");
app.Run();

static string NormalizeDomain(string? value)
{
    var raw = (value ?? "").Trim().ToLowerInvariant();
    if (Uri.TryCreate(raw.Contains("://") ? raw : "https://" + raw, UriKind.Absolute, out var uri)) return uri.IdnHost.TrimEnd('.');
    return raw.Split('/')[0].TrimEnd('.');
}
static string ClientKey(HttpContext context)=>context.Connection.RemoteIpAddress?.ToString()??"unknown";
static bool Fixed(string left, string right)
{
    var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
    return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
}
static void LoadEnv(string path)
{
    if (!File.Exists(path)) return;
    foreach (var raw in File.ReadAllLines(path))
    {
        var line = raw.Trim(); if (line.Length == 0 || line.StartsWith('#')) continue;
        var separator = line.IndexOf('='); if (separator < 1) continue;
        var key = line[..separator].Trim(); var value = line[(separator + 1)..].Trim().Trim('"', '\'');
        if (Environment.GetEnvironmentVariable(key) is null) Environment.SetEnvironmentVariable(key, value);
    }
}

sealed class LicenseSigner
{
    readonly ECDsa key;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public LicenseSigner()
    {
        var d = Environment.GetEnvironmentVariable("LICENSE_PRIVATE_D_BASE64");
        var x = Environment.GetEnvironmentVariable("LICENSE_PUBLIC_X_BASE64");
        var y = Environment.GetEnvironmentVariable("LICENSE_PUBLIC_Y_BASE64");
        if (!string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(x) && !string.IsNullOrWhiteSpace(y))
        {
            key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = Convert.FromBase64String(d), Q = new ECPoint { X = Convert.FromBase64String(x), Y = Convert.FromBase64String(y) } });
            return;
        }
        var encoded = Environment.GetEnvironmentVariable("LICENSE_PRIVATE_KEY_BASE64") ?? throw new InvalidOperationException("Faltan los parámetros privados de firma.");
        key = ECDsa.Create(); key.ImportPkcs8PrivateKey(Convert.FromBase64String(encoded), out _);
    }
    public SignedEnvelope Sign(LicensePayload payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var signature = key.SignData(bytes, HashAlgorithmName.SHA256);
        return new(Convert.ToBase64String(bytes), Convert.ToBase64String(signature));
    }
}

sealed class LicenseDatabase
{
    readonly string connection;
    public LicenseDatabase(IConfiguration configuration) => connection = configuration.GetConnectionString("DefaultConnection") ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? throw new InvalidOperationException("Falta ConnectionStrings__DefaultConnection.");
    async Task<SqlConnection> Open(CancellationToken ct) { var c = new SqlConnection(connection); await c.OpenAsync(ct); return c; }
    public async Task Ping(CancellationToken ct) { await using var c = await Open(ct); await using var cmd = new SqlCommand("SELECT 1", c); await cmd.ExecuteScalarAsync(ct); }
    public async Task<CheckResult> Check(string key, string installation, string domain, string? ip, CancellationToken ct)
    {
        await using var c = await Open(ct); await using var tx = await c.BeginTransactionAsync(ct);
        long? id = null; string status = "REVOKED", message = "Licencia no reconocida."; DateTimeOffset? expires = null; string? expectedDomain = null, expectedInstallation = null;
        await using (var cmd = new SqlCommand("SELECT id,status,maintenance_message,expires_at,domain,installation_id FROM licenses WITH (UPDLOCK,ROWLOCK) WHERE license_key=@key", c, (SqlTransaction)tx))
        {
            cmd.Parameters.AddWithValue("@key", key); await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { id = r.GetInt64(0); status = r.GetString(1); message = r.IsDBNull(2) ? "" : r.GetString(2); expires = r.IsDBNull(3) ? null : new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc)); expectedDomain = r.IsDBNull(4) ? null : r.GetString(4); expectedInstallation = r.IsDBNull(5) ? null : r.GetString(5); }
        }
        var eventType = "CHECK";
        if (id is not null)
        {
            if (!string.Equals(expectedDomain, domain, StringComparison.OrdinalIgnoreCase)) { status = "REVOKED"; message = "El dominio no está autorizado."; eventType = "DOMAIN_MISMATCH"; }
            else if (string.IsNullOrWhiteSpace(expectedInstallation)) { await using var bind = new SqlCommand("UPDATE licenses SET installation_id=@installation WHERE id=@id", c, (SqlTransaction)tx); bind.Parameters.AddWithValue("@installation", installation); bind.Parameters.AddWithValue("@id", id); await bind.ExecuteNonQueryAsync(ct); eventType = "ACTIVATED"; }
            else if (!string.Equals(expectedInstallation, installation, StringComparison.Ordinal)) { status = "REVOKED"; message = "Esta instalación no está autorizada."; eventType = "INSTALLATION_MISMATCH"; }
            if (expires is not null && expires <= DateTimeOffset.UtcNow) status = "EXPIRED";
            await using var update = new SqlCommand("UPDATE licenses SET last_check_at=SYSUTCDATETIME(),last_ip=@ip WHERE id=@id", c, (SqlTransaction)tx); update.Parameters.AddWithValue("@ip", (object?)ip ?? DBNull.Value); update.Parameters.AddWithValue("@id", id); await update.ExecuteNonQueryAsync(ct);
        }
        await using (var log = new SqlCommand("INSERT INTO license_logs(license_id,event_type,installation_id,domain,ip,details) VALUES(@id,@event,@installation,@domain,@ip,@details)", c, (SqlTransaction)tx)) { log.Parameters.AddWithValue("@id", (object?)id ?? DBNull.Value); log.Parameters.AddWithValue("@event", eventType); log.Parameters.AddWithValue("@installation", installation); log.Parameters.AddWithValue("@domain", domain); log.Parameters.AddWithValue("@ip", (object?)ip ?? DBNull.Value); log.Parameters.AddWithValue("@details", status); await log.ExecuteNonQueryAsync(ct); }
        await tx.CommitAsync(ct);
        if (string.IsNullOrWhiteSpace(message)) message = status switch { "ACTIVE" => "Licencia activa.", "MAINTENANCE" => "El sistema se encuentra temporalmente en mantenimiento.", "SUSPENDED" => "El servicio está suspendido. Contacte al soporte técnico.", "EXPIRED" => "La licencia ha vencido.", _ => "Esta instalación no está autorizada." };
        return new(status, message, expires);
    }
    public async Task<object> Summary(CancellationToken ct) { await using var c = await Open(ct); await using var cmd = new SqlCommand("SELECT COUNT(*),COALESCE(SUM(CASE WHEN status='ACTIVE' THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN status='MAINTENANCE' THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN status='SUSPENDED' THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN status IN('EXPIRED','REVOKED') THEN 1 ELSE 0 END),0) FROM licenses", c); await using var r = await cmd.ExecuteReaderAsync(ct); await r.ReadAsync(ct); return new { total = r.GetInt32(0), active = Convert.ToInt32(r[1]), maintenance = Convert.ToInt32(r[2]), suspended = Convert.ToInt32(r[3]), blocked = Convert.ToInt32(r[4]) }; }
    public async Task<List<object>> List(CancellationToken ct) { var list = new List<object>(); await using var c = await Open(ct); await using var cmd = new SqlCommand("SELECT id,license_key,client_name,domain,installation_id,status,maintenance_message,expires_at,last_check_at,last_ip,created_at FROM licenses ORDER BY created_at DESC", c); await using var r = await cmd.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) list.Add(new { id=r.GetInt64(0),licenseKey=r.GetString(1),clientName=r.GetString(2),domain=r.GetString(3),installationId=r.IsDBNull(4)?null:r.GetString(4),status=r.GetString(5),message=r.IsDBNull(6)?null:r.GetString(6),expiresAt=r.IsDBNull(7)?(DateTime?)null:r.GetDateTime(7),lastCheckAt=r.IsDBNull(8)?(DateTime?)null:r.GetDateTime(8),lastIp=r.IsDBNull(9)?null:r.GetString(9),createdAt=r.GetDateTime(10)}); return list; }
    public async Task<object> Create(CreateLicenseRequest body, CancellationToken ct) { var key="LIC-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(4))+"-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(4))+"-"+Convert.ToHexString(RandomNumberGenerator.GetBytes(4)); await using var c=await Open(ct); await using var cmd=new SqlCommand("INSERT INTO licenses(license_key,client_name,domain,status,expires_at) OUTPUT INSERTED.id VALUES(@key,@client,@domain,'ACTIVE',@expires)",c);cmd.Parameters.AddWithValue("@key",key);cmd.Parameters.AddWithValue("@client",body.ClientName.Trim());cmd.Parameters.AddWithValue("@domain",body.Domain!);cmd.Parameters.AddWithValue("@expires",(object?)body.ExpiresAt?.UtcDateTime??DBNull.Value);var id=Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));return new{id,licenseKey=key,installationId=(string?)null}; }
    public async Task<bool> Update(long id, UpdateLicenseRequest body, CancellationToken ct) { await using var c=await Open(ct);await using var cmd=new SqlCommand("UPDATE licenses SET status=@status,maintenance_message=@message,expires_at=@expires WHERE id=@id",c);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@status",body.Status!);cmd.Parameters.AddWithValue("@message",string.IsNullOrWhiteSpace(body.Message)?DBNull.Value:body.Message.Trim());cmd.Parameters.AddWithValue("@expires",(object?)body.ExpiresAt?.UtcDateTime??DBNull.Value);return await cmd.ExecuteNonQueryAsync(ct)>0; }
    public async Task<List<object>> Logs(int limit,CancellationToken ct){var list=new List<object>();await using var c=await Open(ct);await using var cmd=new SqlCommand("SELECT TOP (@limit) l.id,l.event_type,l.installation_id,l.domain,l.ip,l.details,l.created_at,x.license_key FROM license_logs l LEFT JOIN licenses x ON x.id=l.license_id ORDER BY l.id DESC",c);cmd.Parameters.AddWithValue("@limit",limit);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))list.Add(new{id=r.GetInt64(0),eventType=r.GetString(1),installationId=r.IsDBNull(2)?null:r.GetString(2),domain=r.IsDBNull(3)?null:r.GetString(3),ip=r.IsDBNull(4)?null:r.GetString(4),details=r.IsDBNull(5)?null:r.GetString(5),createdAt=r.GetDateTime(6),licenseKey=r.IsDBNull(7)?null:r.GetString(7)});return list;}
}

sealed record LicenseCheckRequest([property: JsonPropertyName("license_key")] string LicenseKey, [property: JsonPropertyName("installation_id")] string InstallationId, string Domain);
sealed record LicensePayload(string LicenseKey,string InstallationId,string Domain,string Status,string? Message,DateTimeOffset? ExpiresAt,DateTimeOffset CheckedAt);
sealed record SignedEnvelope(string Payload,string Signature);
sealed record CheckResult(string Status,string Message,DateTimeOffset? ExpiresAt);
sealed record CreateLicenseRequest(string ClientName,string? Domain,DateTimeOffset? ExpiresAt);
sealed record UpdateLicenseRequest(string? Status,string? Message,DateTimeOffset? ExpiresAt);
