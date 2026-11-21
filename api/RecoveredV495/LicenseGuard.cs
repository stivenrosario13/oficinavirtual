using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

internal sealed class LicenseGuard
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private readonly IHttpClientFactory clients;

	private readonly SemaphoreSlim refreshLock = new SemaphoreSlim(1, 1);

	private readonly string serverUrl;

	private readonly string licenseKey;

	private readonly string installationId;

	private readonly string domain;

	private readonly string publicKeyPem;

	private readonly string publicKeyBase64;

	private readonly string publicKeyXBase64;

	private readonly string publicKeyYBase64;

	private readonly string cachePath;

	private readonly TimeSpan refreshInterval;

	private readonly TimeSpan gracePeriod;

	private volatile LicenseDecision? current;

	private SignedEnvelope? latestEnvelope;

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
		refreshInterval = TimeSpan.FromMinutes(Number("LICENSE_REFRESH_MINUTES", 30.0, 5.0, 1440.0));
		gracePeriod = TimeSpan.FromHours(Number("LICENSE_GRACE_HOURS", 48.0, 1.0, 168.0));
		cachePath = (string.IsNullOrWhiteSpace(Env("LICENSE_CACHE_PATH")) ? Path.Combine(environment.ContentRootPath, "App_Data", "license", "license.cache.json") : Path.GetFullPath(Env("LICENSE_CACHE_PATH"), environment.ContentRootPath));
		if (Enabled && (new string[4] { serverUrl, licenseKey, installationId, domain }.Any(string.IsNullOrWhiteSpace) || (string.IsNullOrWhiteSpace(publicKeyPem) && string.IsNullOrWhiteSpace(publicKeyBase64) && (string.IsNullOrWhiteSpace(publicKeyXBase64) || string.IsNullOrWhiteSpace(publicKeyYBase64)))))
		{
			current = LicenseDecision.ConfigurationError("La licencia del sistema no está configurada completamente.");
		}
	}

	public async Task<LicenseDecision> Check(CancellationToken ct)
	{
		if (!Enabled)
		{
			return LicenseDecision.Development();
		}
		LicenseDecision licenseDecision = current;
		if ((object)licenseDecision != null && licenseDecision.Code == "LICENSE_CONFIG")
		{
			return current;
		}
		LicenseDecision licenseDecision2 = current;
		LicenseDecision licenseDecision3 = licenseDecision2;
		if ((object)licenseDecision3 == null)
		{
			licenseDecision3 = await ReadCache(ct);
		}
		LicenseDecision snapshot = licenseDecision3;
		if ((object)snapshot != null && DateTimeOffset.UtcNow - snapshot.CheckedAt < refreshInterval)
		{
			return current = snapshot;
		}
		await refreshLock.WaitAsync(ct);
		try
		{
			LicenseDecision licenseDecision4 = current;
			LicenseDecision licenseDecision5 = licenseDecision4;
			if ((object)licenseDecision5 == null)
			{
				LicenseDecision licenseDecision6 = snapshot;
				if ((object)licenseDecision6 == null)
				{
					licenseDecision6 = await ReadCache(ct);
				}
				licenseDecision5 = licenseDecision6;
			}
			snapshot = licenseDecision5;
			if ((object)snapshot != null && DateTimeOffset.UtcNow - snapshot.CheckedAt < refreshInterval)
			{
				return snapshot;
			}
			try
			{
				LicenseDecision remote = await Refresh(ct);
				LastDiagnostic = null;
				current = remote;
				await WriteCache(remote, ct);
				return remote;
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				Console.Error.WriteLine("License server: " + ex.Message);
				LastDiagnostic = ex.GetType().Name + ": " + ex.Message;
				if ((object)snapshot != null && snapshot.AllowsUse && DateTimeOffset.UtcNow - snapshot.CheckedAt <= gracePeriod)
				{
					LicenseGuard licenseGuard = this;
					LicenseDecision licenseDecision7 = snapshot with
					{
						UsingGracePeriod = true,
						Message = "Validación temporal mediante la última licencia firmada."
					};
					licenseDecision = licenseDecision7;
					licenseGuard.current = licenseDecision7;
					return licenseDecision;
				}
				return current = new LicenseDecision(AllowsUse: false, "UNAVAILABLE", "LICENSE_UNAVAILABLE", "No fue posible validar la licencia del sistema. Contacte al soporte técnico.", null, DateTimeOffset.UtcNow, UsingGracePeriod: false, SignatureVerified: false);
			}
		}
		finally
		{
			refreshLock.Release();
		}
	}

	private async Task<LicenseDecision> Refresh(CancellationToken ct)
	{
		if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttps && !result.IsLoopback))
		{
			throw new InvalidOperationException("LICENSE_SERVER_URL debe utilizar HTTPS.");
		}
		HttpClient client = clients.CreateClient("license-server");
		using HttpResponseMessage response = await client.PostAsJsonAsync(serverUrl + "/api/v1/license/check", new
		{
			license_key = licenseKey,
			installation_id = installationId,
			domain = domain
		}, JsonOptions, ct);
		if (response.StatusCode == HttpStatusCode.TooManyRequests)
		{
			throw new HttpRequestException("El servidor de licencias limitó temporalmente las solicitudes.");
		}
		response.EnsureSuccessStatusCode();
		return Verify(latestEnvelope = (await response.Content.ReadFromJsonAsync<SignedEnvelope>(JsonOptions, ct)) ?? throw new CryptographicException("Respuesta de licencia vacía."));
	}

	private LicenseDecision Verify(SignedEnvelope envelope)
	{
		byte[] array = Convert.FromBase64String(envelope.Payload);
		byte[] signature = Convert.FromBase64String(envelope.Signature);
		using ECDsa eCDsa = ((!string.IsNullOrWhiteSpace(publicKeyXBase64) && !string.IsNullOrWhiteSpace(publicKeyYBase64)) ? ECDsa.Create(new ECParameters
		{
			Curve = ECCurve.NamedCurves.nistP256,
			Q = new ECPoint
			{
				X = Convert.FromBase64String(publicKeyXBase64),
				Y = Convert.FromBase64String(publicKeyYBase64)
			}
		}) : ECDsa.Create());
		if (string.IsNullOrWhiteSpace(publicKeyXBase64) || string.IsNullOrWhiteSpace(publicKeyYBase64))
		{
			if (!string.IsNullOrWhiteSpace(publicKeyBase64))
			{
				eCDsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out var _);
			}
			else
			{
				eCDsa.ImportFromPem(publicKeyPem);
			}
		}
		if (!eCDsa.VerifyData(array, signature, HashAlgorithmName.SHA256))
		{
			throw new CryptographicException("La firma de la licencia no es válida.");
		}
		LicensePayload licensePayload = JsonSerializer.Deserialize<LicensePayload>(array, JsonOptions) ?? throw new CryptographicException("El contenido firmado no es válido.");
		if (!Fixed(licensePayload.LicenseKey, licenseKey) || !Fixed(licensePayload.InstallationId, installationId) || !Fixed(licensePayload.Domain, domain))
		{
			throw new CryptographicException("La respuesta firmada no corresponde a esta instalación.");
		}
		if (licensePayload.CheckedAt > DateTimeOffset.UtcNow.AddMinutes(5.0) || DateTimeOffset.UtcNow - licensePayload.CheckedAt > TimeSpan.FromHours(24.0))
		{
			throw new CryptographicException("La fecha de la respuesta firmada no es aceptable.");
		}
		string text = licensePayload.Status.Trim().ToUpperInvariant();
		DateTimeOffset? expiresAt = licensePayload.ExpiresAt;
		if (expiresAt.HasValue)
		{
			DateTimeOffset valueOrDefault = expiresAt.GetValueOrDefault();
			if (valueOrDefault <= DateTimeOffset.UtcNow)
			{
				text = "EXPIRED";
			}
		}
		bool flag = text == "ACTIVE";
		string message = (string.IsNullOrWhiteSpace(licensePayload.Message) ? DefaultMessage(text) : licensePayload.Message.Trim());
		return new LicenseDecision(flag, text, flag ? "OK" : ("LICENSE_" + text), message, licensePayload.ExpiresAt, licensePayload.CheckedAt, UsingGracePeriod: false, SignatureVerified: true);
	}

	private async Task<LicenseDecision?> ReadCache(CancellationToken ct)
	{
		try
		{
			if (!File.Exists(cachePath))
			{
				return null;
			}
			SignedEnvelope signedEnvelope = JsonSerializer.Deserialize<SignedEnvelope>(await File.ReadAllTextAsync(cachePath, ct), JsonOptions);
			if ((object)signedEnvelope == null)
			{
				return null;
			}
			latestEnvelope = signedEnvelope;
			return Verify(signedEnvelope);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			Console.Error.WriteLine("License cache: " + ex.Message);
			return null;
		}
	}

	private async Task WriteCache(LicenseDecision decision, CancellationToken ct)
	{
		if ((object)latestEnvelope != null && decision.SignatureVerified)
		{
			string directoryName = Path.GetDirectoryName(cachePath);
			Directory.CreateDirectory(directoryName);
			string temporary = cachePath + ".tmp";
			await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(latestEnvelope, JsonOptions), ct);
			File.Move(temporary, cachePath, overwrite: true);
		}
	}

	private static string DefaultMessage(string status)
	{
		return status switch
		{
			"MAINTENANCE" => "El sistema se encuentra temporalmente en mantenimiento.", 
			"SUSPENDED" => "El servicio está suspendido. Contacte al soporte técnico.", 
			"EXPIRED" => "La licencia del sistema ha vencido.", 
			"REVOKED" => "Esta instalación ya no está autorizada.", 
			_ => "La licencia del sistema no permite continuar.", 
		};
	}

	private static bool Fixed(string left, string right)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(left.Trim().ToLowerInvariant());
		byte[] bytes2 = Encoding.UTF8.GetBytes(right.Trim().ToLowerInvariant());
		if (bytes.Length == bytes2.Length)
		{
			return CryptographicOperations.FixedTimeEquals(bytes, bytes2);
		}
		return false;
	}

	private static string Env(string key)
	{
		return Environment.GetEnvironmentVariable(key) ?? "";
	}

	private static bool Bool(string key)
	{
		bool result;
		return bool.TryParse(Env(key), out result) & result;
	}

	private static double Number(string key, double fallback, double min, double max)
	{
		if (!double.TryParse(Env(key), out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, min, max);
	}
}
