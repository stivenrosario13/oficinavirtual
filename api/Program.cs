using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[CompilerGenerated]
internal class Program
{
	private static void Main(string[] args)
	{
		LoadEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));
		if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "RegistroAgencias.Api.csproj")))
		{
			LoadEnv(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env.local"));
		}
		WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(args);
		StorageServerConfiguration.RestoreAndPersist(webApplicationBuilder.Configuration, webApplicationBuilder.Environment.ContentRootPath, webApplicationBuilder.Environment.WebRootPath);
		StorageFiles.Configure(webApplicationBuilder.Configuration);
		string fullPath = Path.GetFullPath(webApplicationBuilder.Configuration["STORAGE_QUEUE_PATH"] ?? Path.Combine(webApplicationBuilder.Environment.ContentRootPath, "App_Data", "StorageQueue"));
		string fullPath2 = Path.GetFullPath(webApplicationBuilder.Environment.WebRootPath ?? Path.Combine(webApplicationBuilder.Environment.ContentRootPath, "wwwroot"));
		if (fullPath.Equals(fullPath2, StringComparison.OrdinalIgnoreCase) || fullPath.StartsWith(fullPath2 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("STORAGE_QUEUE_PATH debe estar fuera de wwwroot.");
		}
		ServiceCollectionServiceExtensions.AddSingleton(implementationInstance: (StorageUploadQueue)(StorageFiles.Queue = new StorageUploadQueue(fullPath, webApplicationBuilder.Configuration)), services: webApplicationBuilder.Services);
		webApplicationBuilder.Services.AddSingleton<StorageUsageMonitor>();
		webApplicationBuilder.Services.AddSingleton<StorageDocumentUsage>();
		webApplicationBuilder.Services.AddHostedService<StorageUploadWorker>();
		string text = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Path.Combine(webApplicationBuilder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
		}
		Directory.CreateDirectory(text);
		webApplicationBuilder.Services.AddDataProtection().SetApplicationName("GrupoTejeda.OficinaVirtual").PersistKeysToFileSystem(new DirectoryInfo(text));
		webApplicationBuilder.WebHost.ConfigureKestrel((KestrelServerOptions options) =>
		{
			options.AddServerHeader = false;
			options.Limits.MaxRequestBodySize = 52428800L;
		});
		webApplicationBuilder.Services.Configure((FormOptions x) =>
		{
			x.MultipartBodyLengthLimit = 52428800L;
		});
		webApplicationBuilder.Services.AddResponseCompression((ResponseCompressionOptions options) =>
		{
			options.EnableForHttps = true;
			options.Providers.Add<BrotliCompressionProvider>();
			options.Providers.Add<GzipCompressionProvider>();
			options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new string[2] { "application/json", "image/svg+xml" });
		});
		webApplicationBuilder.Services.Configure((BrotliCompressionProviderOptions options) =>
		{
			options.Level = CompressionLevel.Fastest;
		});
		webApplicationBuilder.Services.Configure((GzipCompressionProviderOptions options) =>
		{
			options.Level = CompressionLevel.Fastest;
		});
		webApplicationBuilder.Services.AddSingleton<Database>();
		webApplicationBuilder.Services.AddSingleton<StorageMigrationJob>();
		webApplicationBuilder.Services.AddHostedService((IServiceProvider provider) => provider.GetRequiredService<StorageMigrationJob>());
		webApplicationBuilder.Services.AddSingleton<StorageVerificationJob>();
		webApplicationBuilder.Services.AddHostedService((IServiceProvider provider) => provider.GetRequiredService<StorageVerificationJob>());
		webApplicationBuilder.Services.AddSingleton<Tokens>();
		webApplicationBuilder.Services.AddSingleton<LiveNotificationBroker>();
		webApplicationBuilder.Services.AddSingleton<PwaPushService>();
		webApplicationBuilder.Services.AddHostedService((IServiceProvider provider) => provider.GetRequiredService<PwaPushService>());
		webApplicationBuilder.Services.AddSingleton<SupportPresenceTracker>();
		webApplicationBuilder.Services.AddSingleton<LicenseGuard>();
		webApplicationBuilder.Services.AddHttpClient("license-server", (HttpClient client) =>
		{
			client.Timeout = TimeSpan.FromSeconds(10.0);
			client.DefaultRequestHeaders.UserAgent.ParseAdd("RegistroAgencias-LicenseClient/1.0");
		});
		webApplicationBuilder.Services.AddHttpClient("geocoder", (HttpClient client) =>
		{
			client.Timeout = TimeSpan.FromSeconds(12.0);
			client.DefaultRequestHeaders.UserAgent.ParseAdd(Environment.GetEnvironmentVariable("GEOCODER_USER_AGENT") ?? "GrupoTejedaAuditorias/2026");
			client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-DO,es;q=0.9");
		});
		webApplicationBuilder.Services.AddRateLimiter((RateLimiterOptions options) =>
		{
			options.RejectionStatusCode = 429;
			options.OnRejected = async (OnRejectedContext context, CancellationToken ct) =>
			{
				context.HttpContext.Response.Headers.RetryAfter = "60";
				await context.HttpContext.Response.WriteAsJsonAsync(new
				{
					error = "Demasiadas solicitudes. Espera un minuto y vuelve a intentarlo.",
					code = "RATE_LIMITED"
				}, ct);
			};
			options.AddPolicy("login", (HttpContext context) => RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), (string _) => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 8,
				Window = TimeSpan.FromMinutes(1.0),
				QueueLimit = 0,
				AutoReplenishment = true
			}));
			options.AddPolicy("sensitive-write", (HttpContext context) => RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), (string _) => new FixedWindowRateLimiterOptions
			{
				PermitLimit = 60,
				Window = TimeSpan.FromMinutes(1.0),
				QueueLimit = 0,
				AutoReplenishment = true
			}));
		});
		webApplicationBuilder.Services.AddAuthentication("Cookies").AddCookie((CookieAuthenticationOptions options) =>
		{
			options.Cookie.Name = "oficina_admin";
			options.Cookie.HttpOnly = true;
			options.Cookie.SameSite = SameSiteMode.Strict;
			options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
			options.Cookie.MaxAge = TimeSpan.FromDays(3650.0);
			options.ExpireTimeSpan = TimeSpan.FromDays(3650.0);
			options.SlidingExpiration = true;
			options.Events.OnRedirectToAccessDenied = ApiCookieEvents.AccessDenied;
			options.Events.OnRedirectToLogin = (RedirectContext<CookieAuthenticationOptions> c) =>
			{
				c.Response.StatusCode = 401;
				c.Response.Headers.CacheControl = "no-store";
				return c.Response.WriteAsJsonAsync(new
				{
					error = "Tu sesión no está activa. Inicia sesión nuevamente.",
					code = "SESSION_REQUIRED"
				});
			};
		});
		webApplicationBuilder.Services.AddAuthorization((AuthorizationOptions options) =>
		{
			options.AddPolicy("PortalUser", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireRole("Administrator", "Administrative", "Viewer", "GroupAdministrator", "Fiscalizador", "Technology", "GeneralServices", "HumanResources", "Warehouse", "Department");
			});
			options.AddPolicy("PanelView", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) =>
				{
					IIdentity? identity = context.User.Identity;
					return identity != null && identity.IsAuthenticated && (context.User.IsInRole("Administrator") || context.User.IsInRole("Viewer") || context.User.IsInRole("GroupAdministrator") || context.User.IsInRole("Fiscalizador") || HasPermission(context.User, "canViewDashboard") || HasPermission(context.User, "canViewFieldAudits"));
				});
			});
			options.AddPolicy("PanelManage", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireRole("Administrator");
			});
			options.AddPolicy("FieldAudit", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireRole("Administrator", "GroupAdministrator");
			});
			options.AddPolicy("TicketCreate", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canCreateTickets"));
			});
			options.AddPolicy("TicketResolve", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canResolveTickets"));
			});
			options.AddPolicy("TicketRouting", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canAssignTickets"));
			});
			options.AddPolicy("SupportTechnician", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canViewSupportFindings"));
			});
			options.AddPolicy("SupportDashboard", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireRole("Administrator", "Technology", "GeneralServices", "HumanResources");
			});
			options.AddPolicy("SupportConfiguration", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canConfigureSupport"));
			});
			options.AddPolicy("SupportChat", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canUseSupportChat"));
			});
			options.AddPolicy("SupportChatManage", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canManageSupportChat"));
			});
			options.AddPolicy("SupportChatModerate", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canModerateSupportChat"));
			});
			options.AddPolicy("SupportChatDelete", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canDeleteSupportConversations"));
			});
			options.AddPolicy("TicketNotifications", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canViewTicketNotifications"));
			});
			options.AddPolicy("UserAdministration", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canManageUsers"));
			});
			options.AddPolicy("AuditManagement", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canManageAudits"));
			});
			options.AddPolicy("TicketDeletion", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canDeleteSupportTickets"));
			});
			options.AddPolicy("AgencyDirectoryManage", (AuthorizationPolicyBuilder policy) =>
			{
				policy.RequireAssertion((AuthorizationHandlerContext context) => HasPermission(context.User, "canManageAgencies"));
			});
		});
		WebApplication webApplication = webApplicationBuilder.Build();
		webApplication.MapGet("/api/admin/storage/status", (Func<StorageMigrationJob, IResult>)((StorageMigrationJob job) => Results.Ok(job.Status()))).RequireAuthorization("PanelManage");
		webApplication.MapGet("/api/admin/storage/analytics", (Func<StorageUploadQueue, IResult>)((StorageUploadQueue queue) => Results.Ok(queue.Analytics()))).RequireAuthorization("PanelManage");
		webApplication.MapGet("/api/admin/storage/documents", (Func<StorageDocumentUsage, CancellationToken, Task<IResult>>)(async (StorageDocumentUsage usage, CancellationToken ct) => Results.Ok(await usage.Read(ct)))).RequireAuthorization("PanelManage");
		webApplication.MapGet("/api/admin/storage/usage", (Func<StorageUsageMonitor, HttpContext, CancellationToken, Task<IResult>>)(async (StorageUsageMonitor usage, HttpContext context, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			return Results.Ok(await usage.Read(ct));
		})).RequireAuthorization("PanelManage");
		webApplication.MapGet("/api/admin/storage/queue", (Func<StorageUploadQueue, IResult>)((StorageUploadQueue queue) => Results.Ok(queue.Status()))).RequireAuthorization("PanelManage");
		webApplication.MapPost("/api/admin/storage/queue/resume", (Func<StorageUploadQueue, IResult>)((StorageUploadQueue queue) => (!queue.Resume()) ? Results.Conflict(new
		{
			error = "Hay una transferencia en curso. Espera a que termine."
		}) : Results.Ok(queue.Status()))).RequireAuthorization("PanelManage").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/admin/storage/verification", (Func<StorageVerificationJob, IResult>)((StorageVerificationJob job) => Results.Ok(job.Status()))).RequireAuthorization("PanelManage");
		webApplication.MapPost("/api/admin/storage/verify", (Func<StorageVerificationJob, StorageUploadQueue, IResult>)((StorageVerificationJob job, StorageUploadQueue queue) =>
		{
			DateTime? verificationBlockedUntil = queue.Status().VerificationBlockedUntil;
			if (verificationBlockedUntil.HasValue)
			{
				DateTime valueOrDefault = verificationBlockedUntil.GetValueOrDefault();
				if (valueOrDefault > DateTime.UtcNow)
				{
					return Results.Conflict(new
					{
						error = "Espera requerida para respetar los límites de subida.",
						retryAt = valueOrDefault
					});
				}
			}
			return (!job.Start()) ? Results.Conflict(new
			{
				error = "Ya hay una prueba en curso."
			}) : Results.Accepted("/api/admin/storage/verification", job.Status());
		})).RequireAuthorization("PanelManage").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/admin/storage/migrate", (Func<StorageMigrationJob, IResult>)((StorageMigrationJob job) => (!job.Start()) ? Results.Conflict(new
		{
			error = "Ya hay una copia en curso."
		}) : Results.Accepted("/api/admin/storage/status", job.Status()))).RequireAuthorization("PanelManage").RequireRateLimiting("sensitive-write");
		webApplication.MapMaintenanceDocumentEndpoints();
		webApplication.MapWorkshopEndpoints();
		webApplication.MapWorkshopStockExitEndpoints();
		webApplication.MapMaintenanceTemplateEndpoints();
		webApplication.MapInstitutionalDocumentEndpoints();
		webApplication.MapAdministrativeFileEndpoints();
		webApplication.MapAdministrativeAssetEndpoints();
		webApplication.MapWarehouseFulfillmentEndpoints();
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			context.Response.Headers["X-Content-Type-Options"] = "nosniff";
			context.Response.Headers["X-Frame-Options"] = "DENY";
			context.Response.Headers["Referrer-Policy"] = "no-referrer";
			context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(self), geolocation=(self), payment=(), usb=(), serial=()";
			context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; frame-src 'self' blob: https://www.openstreetmap.org; form-action 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self' https://nominatim.openstreetmap.org; worker-src 'self' blob:";
			context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
			context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
			context.Response.Headers["X-Application-Release"] = "V536-WEB-20261116-2120";
			if (context.Request.IsHttps)
			{
				context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
			}
			context.Response.OnStarting(() =>
			{
				string text2 = context.Request.Path.Value ?? "";
				string? contentType = context.Response.ContentType;
				if ((contentType != null && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)) || text2.Equals("/release-guard.js", StringComparison.OrdinalIgnoreCase) || text2.Equals("/version-web.json", StringComparison.OrdinalIgnoreCase))
				{
					context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
					context.Response.Headers.Pragma = "no-cache";
					context.Response.Headers.Expires = "0";
				}
				return Task.CompletedTask;
			});
			await next();
		});
		webApplication.UseResponseCompression();
		webApplication.Use((HttpContext context, Func<Task> next) =>
		{
			context.Response.Headers["X-Application-Release"] = "V536-WEB-20261116-2120";
			return next();
		});
		webApplication.UseDefaultFiles();
		FileExtensionContentTypeProvider publicContentTypes = new FileExtensionContentTypeProvider();
		publicContentTypes.Mappings[".reg"] = "application/octet-stream";
		webApplication.UseStaticFiles(new StaticFileOptions
		{
			ContentTypeProvider = publicContentTypes,
			OnPrepareResponse = (StaticFileResponseContext context) =>
			{
				string text2 = context.Context.Request.Path.Value ?? "";
				if (text2.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
				{
					context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
				}
			}
		});
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			try
			{
				await next();
			}
			catch (AgencyConflict)
			{
				context.Response.StatusCode = 409;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "Esta agencia ya fue registrada por otra persona."
				});
			}
			catch (StorageServiceException ex)
			{
				context.Response.StatusCode = ex.Status;
				await context.Response.WriteAsJsonAsync(new
				{
					error = ex.Message,
					code = ex.Code
				});
			}
			catch (SqlException ex2)
			{
				Console.Error.WriteLine(ex2);
				DatabaseDiagnostic databaseDiagnostic = Database.DescribeConnectionError(ex2);
				context.Response.StatusCode = 503;
				await context.Response.WriteAsJsonAsync(new
				{
					error = databaseDiagnostic.Message,
					code = databaseDiagnostic.Code
				});
			}
			catch (InvalidOperationException ex3) when (ex3.Message.StartsWith("MSSQL_CONFIG:", StringComparison.Ordinal))
			{
				Console.Error.WriteLine(ex3);
				context.Response.StatusCode = 503;
				await context.Response.WriteAsJsonAsync(new
				{
					error = ex3.Message.Substring("MSSQL_CONFIG:".Length),
					code = "MSSQL_CONFIG"
				});
			}
			catch (Exception value)
			{
				Console.Error.WriteLine(value);
				context.Response.StatusCode = 500;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "Ocurrió un error inesperado."
				});
			}
		});
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			if (!IsUnsafeMethod(context.Request.Method) || !IsCrossSiteRequest(context.Request))
			{
				await next();
			}
			else
			{
				context.Response.StatusCode = 403;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "Solicitud de otro sitio bloqueada.",
					code = "CROSS_SITE_REQUEST_BLOCKED"
				});
			}
		});
		webApplication.UseRateLimiter();
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			string text2 = context.Request.Path.Value ?? "";
			bool flag = !text2.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3;
				switch (text2)
				{
				case "/api/health":
				case "/api/version":
				case "/api/license/status":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = flag3;
			}
			if (flag2)
			{
				await next();
			}
			else
			{
				LicenseDecision licenseDecision = await context.RequestServices.GetRequiredService<LicenseGuard>().Check(context.RequestAborted);
				if (licenseDecision.AllowsUse)
				{
					await next();
				}
				else
				{
					context.Response.StatusCode = 423;
					await context.Response.WriteAsJsonAsync(new
					{
						error = licenseDecision.Message,
						code = licenseDecision.Code,
						status = licenseDecision.Status,
						expiresAt = licenseDecision.ExpiresAt,
						checkedAt = licenseDecision.CheckedAt
					});
				}
			}
		});
		webApplication.UseAuthentication();
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			string text2 = context.Request.Path.Value ?? "";
			int num;
			if ((context.User.Identity?.IsAuthenticated ?? false) && text2.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
			{
				switch (text2)
				{
				default:
					num = ((!(text2 == "/api/account/password/recover")) ? 1 : 0);
					break;
				case "/api/health":
				case "/api/version":
				case "/api/license/status":
				case "/api/admin/login":
					num = 0;
					break;
				}
			}
			else
			{
				num = 0;
			}
			bool refreshSession = (byte)num != 0;
			bool flag = refreshSession && context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") == "legacy-admin";
			bool flag2 = flag;
			if (flag2)
			{
				flag2 = !(await context.RequestServices.GetRequiredService<Database>().LegacyPasswordAccessEnabled(context.RequestAborted));
			}
			if (flag2)
			{
				context.Response.StatusCode = 401;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "La contraseña cambió. Inicia sesión nuevamente.",
					code = "SESSION_REVOKED"
				});
			}
			else
			{
				if (refreshSession && Guid.TryParse(context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"), out var userId))
				{
					Database database = context.RequestServices.GetRequiredService<Database>();
					try
					{
						await database.EnsureUnifiedSupportProfile(WithEffectivePermissions, context.RequestAborted);
					}
					catch (Exception exception)
					{
						context.RequestServices.GetRequiredService<ILogger<Program>>().LogError(exception, "No se pudo unificar el perfil Soporte; la solicitud continuará con los permisos actuales.");
					}
					AdminIdentity adminIdentity = await database.Account(userId, context.RequestAborted);
					if ((object)adminIdentity == null)
					{
						await context.SignOutAsync();
						context.Response.StatusCode = 401;
						await context.Response.WriteAsJsonAsync(new
						{
							error = "La cuenta fue desactivada o ya no existe."
						});
						return;
					}
					if ((context.User.FindFirstValue("recovery_version") ?? "") != adminIdentity.RecoveryVersion)
					{
						context.Response.StatusCode = 401;
						await context.Response.WriteAsJsonAsync(new
						{
							error = "La contraseña cambió. Inicia sesión nuevamente.",
							code = "SESSION_REVOKED"
						});
						return;
					}
					adminIdentity = EmmaAdministrativeAccess.Scope(adminIdentity);
					string text3 = JsonSerializer.Serialize(AccountPermissions(adminIdentity));
					if (context.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") != adminIdentity.Role || context.User.FindFirstValue("must_change_password") != (adminIdentity.MustChangePassword ? "true" : "false") || context.User.FindFirstValue("avatar_url") != (adminIdentity.AvatarUrl ?? "") || context.User.FindFirstValue("display_name") != adminIdentity.DisplayName || context.User.Identity?.Name != adminIdentity.Email || context.User.FindFirstValue("support_team") != (adminIdentity.SupportTeam ?? "") || context.User.FindFirstValue("permissions_json") != text3 || string.IsNullOrWhiteSpace(context.User.FindFirstValue("operational_session_id")))
					{
						await SignInAccount(context, adminIdentity);
					}
					string sessionTrace = context.User.FindFirstValue("operational_session_id") ?? "";
					if (OperationalActivityThrottle.ShouldWrite(sessionTrace))
					{
						try
						{
							await database.TouchOperationalSession(sessionTrace, text2, context.RequestAborted);
						}
						catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
						{
							context.RequestServices.GetRequiredService<ILogger<Program>>().LogWarning(exception, "No se pudo actualizar la actividad de la sesión.");
						}
					}
				}
				await next();
			}
		});
		webApplication.UseAuthorization();
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			string text2 = context.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
			string text3 = context.User.FindFirstValue("support_team");
			string text4;
			if (text2 == "Administrator")
			{
				text4 = "administration";
			}
			else
			{
				string text5;
				if (text2 == "GroupAdministrator")
				{
					text5 = "supervisors";
				}
				else
				{
					string text7;
					bool flag;
					string text6;
					switch (text3)
					{
					case "TECHNICIANS":
						text7 = "technicians";
						break;
					case "CALL_CENTER":
					case "TECHNICAL_FAILURE":
						flag = true;
						goto IL_00a3;
					default:
						{
							flag = false;
							goto IL_00a3;
						}
						IL_00a3:
						if (flag)
						{
							text6 = "support";
						}
						else
						{
							bool flag2 = ((text2 == "Technology" || text2 == "GeneralServices") ? true : false);
							text6 = (flag2 ? "technicians" : "other");
						}
						text7 = text6;
						break;
					}
					text5 = text7;
				}
				text4 = text5;
			}
			string text8 = text4;
			using (StorageOrigin.Scope("new", text8))
			{
				await next();
			}
		});
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			if (WarehouseRequesterProfile.IsRequester(context.User) && !WarehouseRequesterProfile.ApiAllowed(context.Request.Path.Value ?? "", context.Request.Method))
			{
				context.Response.StatusCode = 403;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "Este perfil solo permite enviar y consultar solicitudes propias a Almacén."
				});
			}
			else
			{
				string text2 = OperationalPermissions.Required(context.Request.Path.Value ?? "", context.Request.Method);
				if (!(context.User.Identity?.IsAuthenticated ?? false) || text2 == null || OperationalPermissions.Allowed(context.User, text2))
				{
					await next();
				}
				else
				{
					context.Response.StatusCode = 403;
					await context.Response.WriteAsJsonAsync(new
					{
						error = "Tu cuenta no tiene permiso para esta operación.",
						permission = text2
					});
				}
			}
		});
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			if (!context.User.IsInRole("Department") || DepartmentAccounts.ApiAllowed(context.Request.Path.Value ?? "", context.Request.Method))
			{
				await next();
			}
			else
			{
				context.Response.StatusCode = 403;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "Este recurso no pertenece al departamento asignado."
				});
			}
		});
		webApplication.MapGet("/api/department-support", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			string scope = AreaManagerAccess.Scope(context.User);
			if (scope == null || scope == "ALL")
			{
				return Results.Forbid();
			}
			string tickets = await db.DepartmentSupport(scope, ct);
			string text2 = await db.DepartmentSupportMembers(scope, ct);
			return Results.Content("{\"tickets\":" + tickets + ",\"members\":" + text2 + "}", "application/json");
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/manager-team-members", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			string text2 = AreaManagerAccess.RequestedScope(context.User, context.Request.Query["scope"].FirstOrDefault());
			return (text2 == null) ? Results.Forbid() : Results.Content("{\"members\":" + await db.DepartmentSupportMembers(text2, ct) + "}", "application/json");
		})).RequireAuthorization("PortalUser");
		webApplication.Use(async (HttpContext context, Func<Task> next) =>
		{
			bool flag = context.User.IsInRole("Administrative") && !AdministrativeProfiles.ApiAllowed(context.Request.Path.Value ?? "") && !EmmaAdministrativeAccess.ApiAllowed(context.User, context.Request.Path.Value ?? "", context.Request.Method) && !AdministrativeTicketAccess.ApiAllowed(context.Request.Path.Value ?? "", context.Request.Method, AreaManagerAccess.Allowed(context.User));
			bool flag2 = flag;
			if (flag2)
			{
				bool flag3 = AreaManagerAccess.Allowed(context.User) && context.Request.Method == "GET";
				bool flag4 = flag3;
				if (flag4)
				{
					string value = context.Request.Path.Value;
					bool flag5 = ((value == "/api/department-support" || value == "/api/department-audit" || value == "/api/manager-team-members") ? true : false);
					flag4 = flag5;
				}
				flag2 = !flag4;
			}
			if (flag2)
			{
				context.Response.StatusCode = 403;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "Este recurso no pertenece al perfil administrativo."
				});
			}
			else
			{
				await next();
			}
		});
		webApplication.Use(TemporaryPasswordGate.Invoke);
		webApplication.MapGet("/api/health", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) =>
		{
			DatabaseHealth databaseHealth = await db.Health(ct);
			return databaseHealth.Ok ? Results.Ok(new
			{
				ok = true,
				service = "registro-agencias-dotnet",
				database = "sql-server-2025",
				connectionMode = databaseHealth.Mode
			}) : Results.Json(new
			{
				ok = false,
				error = databaseHealth.Message,
				code = databaseHealth.Code
			}, (JsonSerializerOptions?)null, (string?)null, (int?)503);
		}));
		webApplication.MapGet("/api/version", (Func<IResult>)(() => Results.Ok(new
		{
			version = "2026.11.02.499",
			storage = "warehouse-forms-profile-v5-20261024",
			fieldIncidentReport = "three-department-create-ticket-v1",
			warehouseSubmissionFix = "all-operational-requesters-and-confirmation-20261010",
			administrativeProfiles = "six-scoped-accounts-20261002",
			administrativeForms = "excel-bulk-import-edit-revision-audit-v454",
			fuelForms = "gasoline-registration-gas-approval-date-filters-v455",
			warehouseEditing = "diorly-ana-direct-button-search-inventory-v459",
			weekendLoans = "vittini-usage-confirmed-returns-v455",
			ticketModems = "admin-and-technical-failure-internal-ticket-agency-linked-v456",
			supervisorWaskar = "active-group-audit-evidence-normal-login-verified-v456",
			modemForm = "self-contained-layout-code-linked-agency-v457",
			auditPhotoGps = "best-valid-reading-real-accuracy-all-supervisors-v481",
			executiveReports = "administrator-department-separated-with-workshop-v484",
			warehouseWeekendTheme = "institutional-dark-white-text-v459",
			vittiniWarehouseVisibility = "active-technical-failure-team-requests-v460",
			supervisorWarehouseDestinations = "agency-or-multiple-agencies-only-v461",
			warehouseProfessionalWorkspace = "sidebar-daily-control-sticky-status-dark-editor-v462",
			administrativeFileExplorer = "tree-only-folders-file-type-icons-v463",
			administrativeFileActions = "context-menu-native-office-open-with-v490",
			voiceOperations = "multi-item-same-warehouse-request-natural-additions-v499",
			administrativeNavigation = "shared-administration-sections-all-departments-v480",
			departmentalAdministrativeProfile = "single-profile-exact-administration-permissions-all-departments-v480",
			warehouseDestinations = "group-agency-own-administration-own-only-supervisors-scoped",
			release = "V554-V542-DARWIN-20261120",
			managerTicketControl = "department-card-collapsible-users-directory-v495",
			callCenterTransfers = "supervisors-and-technical-failure-shared-resolution",
			queryRecovery = "independent-cache-bounded-transient-read-retry",
			storageConfiguration = "private-durable-server-backup",
			warehouseInventory = "2026-10-27-inventario-corregido-134",
			warehouseEntryQuantityLimit = 10000,
			design = "modern-live-queue-controls-V307",
			dataMode = "mssql-direct-assignment-with-group-scope",
			databaseConnection = "sql-server-2025-monster-internal-public-fallback",
			assembly = Assembly.GetExecutingAssembly().GetName().Name,
			supportTickets = "personal-technician-assigned-resolved-only-v404",
			maintenance = "catalog-department-constraint-recovery-v418-20260930",
			workshopTracking = "workshop-daily-movements-selectable-calendar-branded-pdf-v484",
			workshopDailyReports = "three-range-reports-and-area-totals-v485",
			agencyTransitions = "department-only-visible-completed-evidence-v304",
			supportNotifications = "finding-author-name-login-v362",
			findingAreaFilters = "selected-department-tickets-and-findings-v400",
			supportChat = "device-local-time-and-role-safe-mobile-chat-push-v321",
			supportReports = "real-audit-total-v362",
			reportDateRange = "independent-from-to-calendars-including-call-center-v489",
			callCenterAgentReport = "two-independent-from-to-calendars-v489",
			selectedFolderTheme = "dark-tree-selection-no-global-white-v491",
			cacheGuard = "synchronized-html-script-header-release-v500",
			supportCatalog = "technology-cpu-failure-V300",
			permissions = "technician-resolution-all-profiles-v438-20261017",
			liveUpdates = "selected-department-tickets-and-findings-v400",
			loginStability = "server-side-transient-db-retry-and-persistent-cookie-fallback-v328",
			userAdministrationStability = "inactive-user-reactivation-adminusers-v554",
			deployment = "monster-double-route-v500",
			performance = "mobile-on-demand-voice-directory-hidden-tab-polling-v500",
			portal = "files-communications-four-card-home-v401",
			dashboard = "department-filtered-findings-v400",
			ticketData = "agency-directory-5000-chunked-import-v395",
			responsiblePicker = "field-technicians-restored-for-support-team-assignment-v310",
			userAdministration = "single-administrative-profile-all-departments-v480",
			fieldAuditPhotos = "authenticated-profile-access-token-recovery-v323"
		})));
		webApplication.MapGet("/api/license/status", (Func<LicenseGuard, CancellationToken, Task<IResult>>)(async (LicenseGuard guard, CancellationToken ct) =>
		{
			LicenseDecision licenseDecision = await guard.Check(ct);
			return Results.Ok(new
			{
				enabled = guard.Enabled,
				allowed = licenseDecision.AllowsUse,
				status = licenseDecision.Status,
				code = licenseDecision.Code,
				message = licenseDecision.Message,
				expiresAt = licenseDecision.ExpiresAt,
				checkedAt = licenseDecision.CheckedAt,
				usingGracePeriod = licenseDecision.UsingGracePeriod,
				signatureVerified = licenseDecision.SignatureVerified
			});
		}));
		webApplication.MapGet("/api/geocode/reverse", (Func<double?, double?, IHttpClientFactory, CancellationToken, Task<IResult>>)(async (double? lat, double? lng, IHttpClientFactory clients, CancellationToken ct) =>
		{
			bool flag;
			if (lat.HasValue)
			{
				double valueOrDefault = lat.GetValueOrDefault();
				if (!(valueOrDefault < -90.0) && !(valueOrDefault > 90.0))
				{
					flag = false;
					goto IL_0068;
				}
			}
			flag = true;
			goto IL_0068;
			IL_0068:
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4;
			if (!flag3)
			{
				if (lng.HasValue)
				{
					double valueOrDefault2 = lng.GetValueOrDefault();
					if (!(valueOrDefault2 < -180.0) && !(valueOrDefault2 > 180.0))
					{
						flag4 = false;
						goto IL_00af;
					}
				}
				flag4 = true;
				goto IL_00af;
			}
			goto IL_00b3;
			IL_00af:
			flag3 = flag4;
			goto IL_00b3;
			IL_00b3:
			if (flag3)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Las coordenadas no son válidas."
				});
			}
			string latitude = lat.Value.ToString("0.########", CultureInfo.InvariantCulture);
			string longitude = lng.Value.ToString("0.########", CultureInfo.InvariantCulture);
			HttpClient client = clients.CreateClient("geocoder");
			string direccion = "";
			string sector = "";
			string municipio = "";
			string provincia = "";
			string displayName = "";
			List<string> sources = new List<string>();
			try
			{
				string requestUri = $"https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/reverseGeocode?location={longitude}%2C{latitude}&f=json&langCode=es&featureTypes=PointAddress%2CStreetAddress%2CStreetName";
				using HttpResponseMessage response = await client.GetAsync(requestUri, ct);
				if (response.IsSuccessStatusCode)
				{
					using JsonDocument jsonDocument = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
					if (jsonDocument.RootElement.TryGetProperty("address", out var value))
					{
						direccion = Pick(value, new string[3] { "Address", "ShortLabel", "Match_addr" });
						sector = Pick(value, new string[3] { "Sector", "Neighborhood", "District" });
						municipio = Pick(value, new string[2] { "City", "Subregion" });
						provincia = Pick(value, new string[1] { "Region" });
						displayName = Pick(value, new string[2] { "LongLabel", "Match_addr" });
						sources.Add("ArcGIS");
					}
				}
			}
			catch (Exception ex) when (!ct.IsCancellationRequested)
			{
				Console.Error.WriteLine("ArcGIS reverse geocode: " + ex.Message);
			}
			if (string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(sector) || string.IsNullOrWhiteSpace(municipio) || string.IsNullOrWhiteSpace(provincia))
			{
				try
				{
					string requestUri2 = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude}&lon={longitude}&zoom=18&addressdetails=1&layer=address&accept-language=es";
					using HttpResponseMessage response = await client.GetAsync(requestUri2, ct);
					if (response.IsSuccessStatusCode)
					{
						using JsonDocument jsonDocument2 = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
						JsonElement rootElement = jsonDocument2.RootElement;
						if (rootElement.TryGetProperty("address", out var value2))
						{
							string text2 = Pick(value2, new string[4] { "road", "pedestrian", "residential", "footway" });
							string text3 = Pick(value2, new string[1] { "house_number" });
							string text4 = string.Join(" ", new string[2] { text2, text3 }.Where((string x) => !string.IsNullOrWhiteSpace(x))).Trim();
							if (string.IsNullOrWhiteSpace(text4))
							{
								text4 = Pick(rootElement, new string[2] { "name", "display_name" }).Split(',')[0].Trim();
							}
							direccion = First(direccion, text4);
							sector = First(sector, Pick(value2, new string[6] { "suburb", "neighbourhood", "quarter", "residential", "hamlet", "village" }));
							municipio = First(municipio, Pick(value2, new string[5] { "city", "town", "municipality", "city_district", "county" }));
							provincia = First(provincia, Pick(value2, new string[3] { "state", "region", "state_district" }));
							displayName = First(displayName, Pick(rootElement, new string[1] { "display_name" }));
							sources.Add("OpenStreetMap");
						}
					}
				}
				catch (Exception ex2) when (!ct.IsCancellationRequested)
				{
					Console.Error.WriteLine("OpenStreetMap reverse geocode: " + ex2.Message);
				}
			}
			if (string.IsNullOrWhiteSpace(sector))
			{
				try
				{
					if (sources.Contains("OpenStreetMap"))
					{
						await Task.Delay(1100, ct);
					}
					string requestUri3 = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude}&lon={longitude}&zoom=15&addressdetails=1&accept-language=es";
					using HttpResponseMessage response = await client.GetAsync(requestUri3, ct);
					if (response.IsSuccessStatusCode)
					{
						using JsonDocument jsonDocument3 = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
						JsonElement rootElement2 = jsonDocument3.RootElement;
						if (rootElement2.TryGetProperty("address", out var value3))
						{
							sector = First(sector, Pick(value3, new string[8] { "suburb", "neighbourhood", "quarter", "city_district", "village", "hamlet", "town", "municipality" }));
							municipio = First(municipio, Pick(value3, new string[4] { "city", "town", "municipality", "county" }));
							provincia = First(provincia, Pick(value3, new string[3] { "state", "region", "state_district" }));
							displayName = First(displayName, Pick(rootElement2, new string[1] { "display_name" }));
							if (!sources.Contains("OpenStreetMap"))
							{
								sources.Add("OpenStreetMap");
							}
						}
					}
				}
				catch (Exception ex3) when (!ct.IsCancellationRequested)
				{
					Console.Error.WriteLine("OpenStreetMap area reverse geocode: " + ex3.Message);
				}
			}
			if (string.IsNullOrWhiteSpace(municipio) || string.IsNullOrWhiteSpace(provincia))
			{
				try
				{
					string requestUri4 = $"https://api.bigdatacloud.net/data/reverse-geocode-client?latitude={latitude}&longitude={longitude}&localityLanguage=es";
					using HttpResponseMessage response = await client.GetAsync(requestUri4, ct);
					if (response.IsSuccessStatusCode)
					{
						using JsonDocument jsonDocument4 = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
						JsonElement rootElement3 = jsonDocument4.RootElement;
						municipio = First(municipio, Pick(rootElement3, new string[2] { "city", "locality" }));
						provincia = First(provincia, Pick(rootElement3, new string[1] { "principalSubdivision" }));
						displayName = First(displayName, string.Join(", ", new string[3]
						{
							Pick(rootElement3, new string[1] { "locality" }),
							Pick(rootElement3, new string[1] { "principalSubdivision" }),
							Pick(rootElement3, new string[1] { "countryName" })
						}.Where((string x) => !string.IsNullOrWhiteSpace(x))));
						sources.Add("BigDataCloud");
					}
				}
				catch (Exception ex4) when (!ct.IsCancellationRequested)
				{
					Console.Error.WriteLine("BigDataCloud reverse geocode: " + ex4.Message);
				}
			}
			if (string.IsNullOrWhiteSpace(direccion) && string.IsNullOrWhiteSpace(sector) && string.IsNullOrWhiteSpace(municipio) && string.IsNullOrWhiteSpace(provincia))
			{
				return Results.Json(new
				{
					error = "Los servicios de dirección no respondieron para esta coordenada. Vuelve a intentarlo."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)502);
			}
			return Results.Ok(new
			{
				direccion = direccion,
				sector = sector,
				municipio = municipio,
				provincia = provincia,
				displayName = displayName,
				complete = (!string.IsNullOrWhiteSpace(direccion) && !string.IsNullOrWhiteSpace(sector) && !string.IsNullOrWhiteSpace(municipio) && !string.IsNullOrWhiteSpace(provincia)),
				source = string.Join(" + ", sources)
			});
		})).RequireAuthorization("FieldAudit");
		webApplication.MapPost("/api/operational-errors", (Func<OperationalErrorReport, HttpContext, Database, CancellationToken, Task<IResult>>)(async (OperationalErrorReport body, HttpContext context, Database db, CancellationToken ct) =>
		{
			await db.RecordOperationalError(body, context.User, ct);
			return Results.Accepted();
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/department-audit", (Func<string, DateTime?, DateTime?, int?, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string? area, DateTime? from, DateTime? to, int? offset, HttpContext context, Database db, CancellationToken ct) =>
		{
			string text2;
			bool flag3;
			if (EmmaAdministrativeAccess.Allowed(context.User))
			{
				bool flag = string.IsNullOrWhiteSpace(area);
				bool flag2 = flag;
				if (!flag2)
				{
					flag3 = ((area == "ADMINISTRATIVE" || area == "ALL") ? true : false);
					flag2 = flag3;
				}
				text2 = (flag2 ? "ADMINISTRATIVE" : null);
			}
			else
			{
				text2 = AreaManagerAccess.RequestedScope(context.User, area);
			}
			string text3 = text2;
			if (text3 == null)
			{
				return Results.Forbid();
			}
			DateTime date = (from ?? DateTime.UtcNow.AddDays(-30.0)).Date;
			DateTime dateTime = (to ?? DateTime.UtcNow).Date.AddDays(1.0);
			bool flag4 = dateTime <= date || dateTime - date > TimeSpan.FromDays(367.0);
			bool flag5 = flag4;
			if (!flag5)
			{
				if (offset.HasValue)
				{
					int valueOrDefault = offset.GetValueOrDefault();
					if (valueOrDefault < 0 || valueOrDefault > 1000000)
					{
						flag3 = true;
						goto IL_0178;
					}
				}
				flag3 = false;
				goto IL_0178;
			}
			goto IL_017c;
			IL_0178:
			flag5 = flag3;
			goto IL_017c;
			IL_017c:
			if (flag5)
			{
				return Results.BadRequest(new
				{
					error = "Selecciona un período de hasta un año."
				});
			}
			return Results.Content(await db.DepartmentOperationalAudit(text3, date.AddHours(4.0), dateTime.AddHours(4.0), offset.GetValueOrDefault(), ct), "application/json");
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/groups", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				groups = await db.RecurringAuditGroups(tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/group", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				groups = await db.RecurringAuditGroups(tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/panel/groups", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				groups = await db.RecurringAuditGroups(tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PanelView");
		webApplication.MapGet("/api/panel/catalog", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				agencies = await db.RecurringPanelAgencies(null, tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PanelView");
		webApplication.MapGet("/api/agencies", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string? grupo, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(grupo))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Debes indicar un grupo."
				});
			}
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				agencies = await db.RecurringAuditAgencies(grupo.Trim(), tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/panel/agencies", (Func<string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (string? grupo, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(grupo))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Debes indicar un grupo."
				});
			}
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				agencies = await db.RecurringPanelAgencies(grupo.Trim(), tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PanelView");
		webApplication.MapPost("/api/admin/login", (Func<AdminLogin, HttpContext, Database, CancellationToken, Task<IResult>>)(async (AdminLogin login, HttpContext context, Database db, CancellationToken ct) =>
		{
			string email = NormalizeLogin(login.Email);
			string password = login.Password ?? "";
			if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
			{
				return Results.Json(new
				{
					error = "Escribe el usuario y la contraseña."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			AdminIdentity account = null;
			try
			{
				account = await RetryDatabaseAsync<AdminIdentity>(() => db.AuthenticateUser(email, password, ct), ct, "login");
			}
			catch (SqlException ex) when ((uint)(ex.Number - 207) <= 1u)
			{
				Console.Error.WriteLine("Se usará el acceso principal configurado porque no está instalada la gestión avanzada de usuarios: " + ex.Message);
			}
			if ((object)account == null)
			{
				string legacyEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim().ToLowerInvariant();
				string environmentVariable = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
				bool flag = !string.IsNullOrWhiteSpace(legacyEmail) && !string.IsNullOrWhiteSpace(environmentVariable) && Fixed(email, legacyEmail) && Fixed(password, environmentVariable);
				bool flag2 = flag;
				if (flag2)
				{
					flag2 = await db.LegacyPasswordAccessEnabled(ct);
				}
				if (flag2)
				{
					account = new AdminIdentity("legacy-admin", legacyEmail, "Administrador principal", "Administrator", MustChangePassword: false, null, new Dictionary<string, bool>(), null);
				}
			}
			if ((object)account == null)
			{
				return Results.Json(new
				{
					error = "Credenciales incorrectas o usuario desactivado."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			if (EnedraineCallCenterProfile.IsTarget(account.Email))
			{
				try
				{
					await db.EnsureUnifiedSupportProfile(WithEffectivePermissions, ct);
					account = (await db.Account(Guid.Parse(account.Id), ct)) ?? account;
				}
				catch (Exception exception) when (!ct.IsCancellationRequested)
				{
					context.RequestServices.GetRequiredService<ILogger<Program>>().LogError(exception, "No se pudo actualizar el perfil de Call Center; se conserva el acceso actual.");
				}
			}
			await SignInAccount(context, account);
			string persistentSessionToken = null;
			try
			{
				persistentSessionToken = await RetryDatabaseAsync<string>(() => db.CreatePersistentLoginToken(account.Id, ct, account.RecoveryVersion), ct, "token persistente");
			}
			catch (SqlException ex2) when (!ct.IsCancellationRequested)
			{
				Console.Error.WriteLine("No se pudo guardar el token persistente; se conserva la cookie de sesión: " + ex2.Message);
			}
			catch (InvalidOperationException ex3) when (!ct.IsCancellationRequested && ex3.Message.StartsWith("MSSQL_CONFIG:", StringComparison.Ordinal))
			{
				Console.Error.WriteLine("No se pudo guardar el token persistente; se conserva la cookie de sesión: " + ex3.Message);
			}
			return Results.Ok(SessionPayload(account, persistentSessionToken));
		})).RequireRateLimiting("login");
		webApplication.MapPost("/api/admin/session/restore", (Func<PersistentSessionInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (PersistentSessionInput body, HttpContext context, Database db, CancellationToken ct) =>
		{
			AdminIdentity account = await db.RestorePersistentLogin(body.Token, ct);
			if ((object)account == null)
			{
				return Results.Json(new
				{
					error = "La sesión guardada ya no es válida."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)401);
			}
			await SignInAccount(context, account);
			return Results.Ok(SessionPayload(account));
		})).RequireRateLimiting("login");
		webApplication.MapPost("/api/account/recovery-code", (Func<RecoveryCodeRequest, HttpContext, Database, CancellationToken, Task<IResult>>)(async (RecoveryCodeRequest body, HttpContext context, Database db, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			string text2 = context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "";
			if (!Guid.TryParse(text2, out var _))
			{
				return Results.Conflict(new
				{
					error = "Esta cuenta necesita un usuario permanente para habilitar la recuperación."
				});
			}
			string text3 = await db.GenerateRecoveryCode(text2, body.CurrentPassword ?? "", ct);
			return (text3 == null) ? Results.Json(new
			{
				error = "Confirma tu contraseña actual. Si tu contraseña es temporal, cámbiala primero."
			}, (JsonSerializerOptions?)null, (string?)null, (int?)400) : Results.Ok(new
			{
				code = text3
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("login");
		webApplication.MapPost("/api/account/recovery-code/generate", (Func<RecoveryCodeLoginRequest, HttpContext, Database, CancellationToken, Task<IResult>>)(async (RecoveryCodeLoginRequest body, HttpContext context, Database db, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			string text2 = await db.GenerateRecoveryCodeFromLogin(NormalizeLogin(body.UserName), body.CurrentPassword ?? "", ct);
			return (text2 == null) ? Results.Json(new
			{
				error = "No fue posible generar el código. Revisa tu usuario y contraseña actual. Si tu contraseña es temporal, inicia sesión y cámbiala primero."
			}, (JsonSerializerOptions?)null, (string?)null, (int?)400) : Results.Ok(new
			{
				code = text2
			});
		})).AllowAnonymous().RequireRateLimiting("login");
		webApplication.MapPost("/api/account/password/recover", (Func<PasswordRecoveryRequest, HttpContext, Database, CancellationToken, Task<IResult>>)(async (PasswordRecoveryRequest body, HttpContext context, Database db, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			if (body.NewPassword == null || body.NewPassword.Length < 8 || body.NewPassword.Length > 128)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La nueva contraseña debe tener entre 8 y 128 caracteres."
				});
			}
			if (!(await db.RecoverPassword(NormalizeLogin(body.UserName), body.Code ?? "", body.NewPassword, ct)))
			{
				return Results.Json(new
				{
					error = "No fue posible recuperar la cuenta con esos datos. Revisa el usuario y el código. Si no tienes un código válido, contacta al administrador."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)400);
			}
			await context.SignOutAsync();
			return Results.Ok(new
			{
				ok = true
			});
		})).AllowAnonymous().RequireRateLimiting("login");
		webApplication.MapPost("/api/admin/presence", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			bool active = bool.TryParse(context.Request.Query["active"], out bool value) && value;
			await db.PulseOperationalSession(context.User.FindFirstValue("operational_session_id"), active, ct);
			return Results.Ok(new { ok = true, active });
		}));
		webApplication.MapPost("/api/admin/logout", (Func<PersistentSessionInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (PersistentSessionInput? body, HttpContext context, Database db, CancellationToken ct) =>
		{
			await db.RevokePersistentLogin(body?.Token, ct);
			await db.EndOperationalSession(context.User.FindFirstValue("operational_session_id"), ct);
			await context.SignOutAsync();
			return Results.Ok(new
			{
				ok = true
			});
		}));
		webApplication.MapGet("/api/admin/session", (Func<HttpContext, IResult>)((HttpContext context) =>
		{
			IIdentity? identity = context.User.Identity;
			return (identity == null || !identity.IsAuthenticated) ? Results.Ok(new
			{
				authenticated = false
			}) : Results.Ok(SessionPayloadFromContext(context));
		}));
		webApplication.MapGet("/api/dashboard", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(await db.RecurringAuditDashboard(tuple.Item1, tuple.Item2, ct));
		})).RequireAuthorization("PanelView");
		webApplication.MapGet("/api/executive-report", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			if (!context.User.IsInRole("Administrator")) return Results.Forbid();
			DateTime? from = DateTime.TryParseExact(context.Request.Query["from"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedFrom) ? parsedFrom : null;
			DateTime? to = DateTime.TryParseExact(context.Request.Query["to"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedTo) ? parsedTo : null;
			if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date) return Results.BadRequest(new { error = "La fecha Desde no puede ser posterior a la fecha Hasta." });
			return Results.Ok(await db.ExecutiveReport(from, to, ct));
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/incident-ticket-report", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			string login = AdministrativeProfiles.Login(context.User.Identity?.Name ?? "");
			if (!context.User.IsInRole("Administrator") && login is not ("yessica.mercedes" or "yesica.mercedes")) return Results.Forbid();
			DateTime? from = DateTime.TryParseExact(context.Request.Query["from"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedFrom) ? parsedFrom : null;
			DateTime? to = DateTime.TryParseExact(context.Request.Query["to"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsedTo) ? parsedTo : null;
			if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date) return Results.BadRequest(new { error = "La fecha Desde no puede ser posterior a la fecha Hasta." });
			return Results.Ok(new { tickets = await db.IncidentTicketReport(from, to, ct) });
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/admin/audits/{profileId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid profileId, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			AuditDetailRow auditDetailRow = await db.AuditDetail(profileId, tuple.Item1, tuple.Item2, ct);
			return ((object)auditDetailRow == null) ? Results.NotFound(new
			{
				error = "No encontramos esta auditoría o no pertenece a tus grupos."
			}) : Results.Ok(auditDetailRow);
		})).RequireAuthorization("PanelView");
		webApplication.MapGet("/api/tickets/agencies", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			List<TicketAgencyRow> agencies = await db.TicketAgencies(scope.UserId, scope.Role, ct);
			TicketAgencyCatalogStats ticketAgencyCatalogStats = await db.TicketAgencyCatalogStats(scope.UserId, scope.Role, ct);
			return Results.Ok(new
			{
				agencies = agencies,
				totalActive = ticketAgencyCatalogStats.TotalActive,
				invalidIds = ticketAgencyCatalogStats.InvalidIds,
				catalogComplete = (ticketAgencyCatalogStats.InvalidIds == 0L && agencies.Count == ticketAgencyCatalogStats.TotalActive)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/agency-locations", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				agencies = await db.AgencyLocations(tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/technology/agencies/{id:guid}", (Func<Guid, AgencyDirectoryUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, AgencyDirectoryUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string text2 = ValidateAgencyDirectoryUpdate(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			if (!(await db.UpdateAgencyDirectory(id, body, context.User.Identity?.Name ?? "Usuario", ct)))
			{
				return Results.NotFound(new
				{
					error = "Agencia no encontrada."
				});
			}
			broker.Publish("agency-directory-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("AgencyDirectoryManage").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/technology/agencies/import", (Func<AgencyDirectoryImport, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (AgencyDirectoryImport body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string text2 = ValidateAgencyDirectoryImport(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			AgencyDirectoryImportResult agencyDirectoryImportResult = await db.ImportAgencyDirectory(body.Agencies, body.FileName, context.User.Identity?.Name ?? "Usuario", ct);
			if (agencyDirectoryImportResult.Created > 0 || agencyDirectoryImportResult.Updated > 0)
			{
				broker.Publish("agency-directory-updated");
			}
			return Results.Ok(agencyDirectoryImportResult);
		})).RequireAuthorization("AgencyDirectoryManage").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/support/catalog", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Ok(new
		{
			departments = await db.SupportCatalog(ct)
		}))).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/support/directory", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Ok(new
		{
			departments = await db.SupportCatalogReadOnly(ct)
		}))).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/support/metrics", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			List<SupportDepartmentMetricRow> metrics = scope.Role == "Administrator"
				? await db.GlobalSupportDepartmentMetrics(ct)
				: await db.SupportDepartmentMetrics(scope.UserId, scope.Role, HasPermission(context.User, "canAssignTickets") || AreaManagerAccess.Scope(context.User) != null, HasPermission(context.User, "canViewSupportFindings"), context.User.FindFirstValue("support_team"), context.User.Identity?.Name, ct, AreaManagerAccess.Scope(context.User) != null);
			return Results.Ok(new { metrics });
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/support/overview", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			Task<List<SupportDepartmentRow>> departments = db.SupportCatalogReadOnly(ct);
			Task<List<SupportDepartmentMetricRow>> metrics = scope.Role == "Administrator"
				? db.GlobalSupportDepartmentMetrics(ct)
				: db.SupportDepartmentMetrics(scope.UserId, scope.Role, HasPermission(context.User, "canAssignTickets") || AreaManagerAccess.Scope(context.User) != null, HasPermission(context.User, "canViewSupportFindings"), context.User.FindFirstValue("support_team"), context.User.Identity?.Name, ct, AreaManagerAccess.Scope(context.User) != null);
			await Task.WhenAll(departments, metrics);
			return Results.Ok(new
			{
				departments = await departments,
				metrics = await metrics
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/admin/support/departments/{code}", (Func<string, SupportDepartmentInput, Database, CancellationToken, Task<IResult>>)(async (string code, SupportDepartmentInput body, Database db, CancellationToken ct) =>
		{
			body = body with
			{
				Code = code.ToUpperInvariant()
			};
			await db.UpsertSupportDepartment(body, ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportConfiguration");
		webApplication.MapPost("/api/admin/support/categories", (Func<SupportCategoryInput, Database, CancellationToken, Task<IResult>>)(async (SupportCategoryInput body, Database db, CancellationToken ct) =>
		{
			await db.UpsertSupportCategory(null, body, ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportConfiguration");
		webApplication.MapPut("/api/admin/support/categories/{id:guid}", (Func<Guid, SupportCategoryInput, Database, CancellationToken, Task<IResult>>)(async (Guid id, SupportCategoryInput body, Database db, CancellationToken ct) =>
		{
			await db.UpsertSupportCategory(id, body, ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportConfiguration");
		webApplication.MapDelete("/api/admin/support/categories/{id:guid}", (Func<Guid, Database, CancellationToken, Task<IResult>>)(async (Guid id, Database db, CancellationToken ct) => (await db.DeleteSupportCategory(id, ct)) ? Results.Ok(new
		{
			ok = true
		}) : Results.NotFound())).RequireAuthorization("SupportConfiguration");
		webApplication.MapGet("/api/tickets", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				tickets = await db.Tickets(tuple.Item1, tuple.Item2, HasPermission(context.User, "canAssignTickets") || AreaManagerAccess.Scope(context.User) != null, context.User.FindFirstValue("support_team"), context.User.Identity?.Name, ct, AreaManagerAccess.Scope(context.User) != null)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/tickets/technicians", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Ok(new
		{
			technicians = await db.SupportTechnicians(ct)
		}))).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/tickets/alerts", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				count = await db.TicketAlerts(tuple.Item1, tuple.Item2, HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("support_team"), context.User.Identity?.Name, ct)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/tickets/notifications", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			List<TicketNotificationRow> notifications=[];
			try { notifications.AddRange(await db.ResolvedTicketNotifications(scope.UserId, scope.Role, HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("support_team"), ct)); }
			catch(Exception exception) when(exception is not OperationCanceledException) { Console.Error.WriteLine($"Notificaciones de tickets omitidas: {exception.Message}"); }
			try { notifications.AddRange(await db.AutomaticFindingNotifications(scope.UserId, scope.Role, ct)); }
			catch(Exception exception) when(exception is not OperationCanceledException) { Console.Error.WriteLine($"Notificaciones de hallazgos omitidas: {exception.Message}"); }
			return Results.Ok(new
			{
				notifications = notifications.OrderByDescending((TicketNotificationRow item) => item.UpdatedAt).Take(100)
			});
		})).RequireAuthorization("TicketNotifications");
		webApplication.MapGet("/api/push/public-key", (Func<PwaPushService, IResult>)((PwaPushService push) => Results.Ok(new
		{
			publicKey = push.PublicKey,
			keySource = (push.UsesPersistedFallbackKeys ? "APP_DATA" : "MONSTER_ENVIRONMENT")
		}))).RequireAuthorization("PortalUser");
		webApplication.MapPost("/api/push/subscriptions", (Func<PushSubscriptionInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (PushSubscriptionInput body, HttpContext context, Database db, CancellationToken ct) =>
		{
			string text2 = body.Endpoint?.Trim();
			string text3 = body.Keys?.P256dh?.Trim();
			string text4 = body.Keys?.Auth?.Trim();
			if (string.IsNullOrWhiteSpace(text2) || text2.Length > 2048 || string.IsNullOrWhiteSpace(text3) || string.IsNullOrWhiteSpace(text4) || text3.Length > 512 || text4.Length > 512)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La suscripción push no es válida."
				});
			}
			await db.SavePushSubscription(UserScope(context).UserId, text2, text3, text4, ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapMethods("/api/push/test", new string[2] { "GET", "POST" }, (Func<HttpContext, PwaPushService, CancellationToken, Task<IResult>>)(async (HttpContext context, PwaPushService push, CancellationToken ct) =>
		{
			PushTestResult pushTestResult = await push.SendTest(UserScope(context).UserId, ct);
			if (pushTestResult.Sent > 0)
			{
				return Results.Ok(new
				{
					ok = true,
					sent = pushTestResult.Sent,
					registered = pushTestResult.Registered
				});
			}
			return (pushTestResult.Registered == 0) ? Results.Conflict(new
			{
				error = "Este dispositivo todavía no está suscrito. Pulsa Activar notificaciones e inténtalo nuevamente.",
				code = "PUSH_SUBSCRIPTION_REQUIRED"
			}) : Results.Json(new
			{
				error = (pushTestResult.Error ?? "El proveedor no confirmó la notificación."),
				code = "PUSH_DELIVERY_FAILED",
				registered = pushTestResult.Registered
			}, (JsonSerializerOptions?)null, (string?)null, (int?)502);
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/tickets/statistics", (Func<DateTime?, DateTime?, HttpContext, Database, CancellationToken, Task<IResult>>)(async (DateTime? from, DateTime? to, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(await db.TicketStatistics(tuple.Item1, tuple.Item2, HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("support_team"), from, to, ct));
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/dashboard/home-summary", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string userId, string role) = UserScope(context);
			return Results.Ok(await db.HomeSummary(userId, role, HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("support_team"), ct));
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/live/stream", (Func<HttpContext, LiveNotificationBroker, CancellationToken, Task>)(async (HttpContext context, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-cache, no-transform";
			context.Response.Headers.Connection = "keep-alive";
			context.Response.Headers["X-Accel-Buffering"] = "no";
			context.Response.ContentType = "text/event-stream";
			LiveSubscription subscription = broker.Subscribe();
			try
			{
				await context.Response.WriteAsync("retry: 10000\n: connected\n\n", ct);
				await context.Response.Body.FlushAsync(ct);
				Task<bool> pendingRead = subscription.Reader.WaitToReadAsync(ct).AsTask();
				while (!ct.IsCancellationRequested)
				{
					Task task = Task.Delay(TimeSpan.FromSeconds(15.0), ct);
					if (await Task.WhenAny(pendingRead, task) != pendingRead)
					{
						await context.Response.WriteAsync($": heartbeat {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n\n", ct);
					}
					else
					{
						if (!(await pendingRead))
						{
							break;
						}
						string item;
						while (subscription.Reader.TryRead(out item))
						{
							await context.Response.WriteAsync("event: refresh\ndata: " + item + "\n\n", ct);
						}
						pendingRead = subscription.Reader.WaitToReadAsync(ct).AsTask();
					}
					await context.Response.Body.FlushAsync(ct);
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
			}
			catch (IOException)
			{
			}
			catch (ObjectDisposedException)
			{
			}
			finally
			{
				broker.Unsubscribe(subscription.Id);
			}
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/tickets/findings", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				findings = await db.AutomaticFindings(tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), HasPermission(context.User, "canAssignTickets"), ct)
			});
		})).RequireAuthorization("SupportTechnician");
		webApplication.MapPut("/api/tickets/findings/{id:guid}/assign", (Func<Guid, FindingAssignment, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, FindingAssignment body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			if (!HasPermission(context.User, "canAssignTickets"))
			{
				return Results.Forbid();
			}
			if (!(await db.AssignAutomaticFinding(id, body.AssignedTechnicianId, tuple.Item1, tuple.Item2, canAssignTickets: true, context.User.FindFirstValue("display_name") ?? "Soporte", ct)))
			{
				return Results.NotFound(new
				{
					error = "Hallazgo no encontrado, finalizado, fuera de tu departamento o responsable no válido."
				});
			}
			broker.Publish("finding-assigned");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportTechnician");
		webApplication.MapPut("/api/tickets/findings/{id:guid}/resolve", (Func<Guid, HttpRequest, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, HttpRequest request, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (!request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Describe la solución y adjunta las fotografías de evidencia."
				});
			}
			IFormCollection formCollection = await request.ReadFormAsync(ct);
			string diagnosis = formCollection["diagnosis"].ToString().Trim();
			string resolution = formCollection["resolution"].ToString().Trim();
			string verification = formCollection["verification"].ToString().Trim();
			string recommendations = formCollection["recommendations"].ToString().Trim();
			if (diagnosis.Length > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "El diagnóstico supera 2,000 caracteres."
				});
			}
			int length = resolution.Length;
			if ((length < 10 || length > 3000) ? true : false)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Describe el trabajo y la solución aplicada (mínimo 10 caracteres)."
				});
			}
			if (verification.Length > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La verificación supera 2,000 caracteres."
				});
			}
			if (recommendations.Length > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Las recomendaciones superan 2,000 caracteres."
				});
			}
			length = formCollection.Files.Count;
			if ((length < 1 || length > 6) ? true : false)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Adjunta entre 1 y 6 fotografías de evidencia."
				});
			}
			double? latitude = (double.TryParse(formCollection["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? new double?(result) : ((double?)null));
			double? longitude = (double.TryParse(formCollection["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) ? new double?(result2) : ((double?)null));
			double? accuracy = (double.TryParse(formCollection["accuracyMeters"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) ? new double?(result3) : ((double?)null));
			DateTime? locationCapturedAt = (DateTime.TryParse(formCollection["locationCapturedAt"], null, DateTimeStyles.RoundtripKind, out var result4) ? new DateTime?(result4) : ((DateTime?)null));
			HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };
			List<FindingEvidenceUpload> evidence = new List<FindingEvidenceUpload>();
			long totalBytes = 0L;
			foreach (IFormFile file in formCollection.Files)
			{
				if (file.Length == 0L || file.Length > 8388608 || !allowed.Contains(file.ContentType))
				{
					return Results.UnprocessableEntity(new
					{
						error = "Cada evidencia debe ser JPG, PNG o WebP y pesar menos de 8 MB."
					});
				}
				totalBytes += file.Length;
				if (totalBytes > 41943040)
				{
					return Results.UnprocessableEntity(new
					{
						error = "Las evidencias superan el límite total de 40 MB."
					});
				}
				await using (Stream input = file.OpenReadStream())
				{
					using MemoryStream buffer = new MemoryStream();
					await input.CopyToAsync(buffer, ct);
					byte[] data = buffer.ToArray();
					if (!ValidEvidenceImage(data, file.ContentType))
					{
						return Results.UnprocessableEntity(new
						{
							error = "Una evidencia no contiene una imagen válida."
						});
					}
					evidence.Add(new FindingEvidenceUpload(Path.GetFileName(file.FileName), file.ContentType, data, latitude, longitude, accuracy, locationCapturedAt));
				}
				IResult result5 = null;
			}
			(string, string) tuple = UserScope(context);
			return (await db.ResolveAutomaticFinding(id, diagnosis, resolution, verification, recommendations, tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("display_name") ?? "Técnico", context.User.Identity?.Name ?? "", evidence, ct)) ? Results.Ok(new
			{
				ok = true,
				evidence = evidence.Count
			}) : Results.NotFound(new
			{
				error = "Hallazgo no encontrado, no asignado a tu usuario o fuera de tu departamento."
			});
		})).RequireAuthorization("SupportTechnician");
		webApplication.MapPut("/api/tickets/findings/{id:guid}/cancel", (Func<Guid, HttpRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, HttpRequest request, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Describe por qué se anula el hallazgo y adjunta fotografías de evidencia."
				});
			}
			IFormCollection formCollection = await request.ReadFormAsync(ct);
			string reason = formCollection["reason"].ToString().Trim();
			if (reason.Length > 0)
			{
				if (!HasPermission(context.User, "canCancelFindingWithReason"))
				{
					return Results.Forbid();
				}
				int length = reason.Length;
				if ((length < 10 || length > 2000) ? true : false)
				{
					return Results.UnprocessableEntity(new
					{
						error = "El motivo de anulación debe tener entre 10 y 2,000 caracteres."
					});
				}
			}
			else if (!HasPermission(context.User, "canCancelFindingWithoutReason"))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Describe el motivo de la anulación."
				});
			}
			IReadOnlyList<IFormFile> files = formCollection.Files.GetFiles("evidence");
			if (files.Count > 6 || formCollection.Files.Count != files.Count)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Adjunta hasta 6 fotografías de evidencia."
				});
			}
			if ((files.Count > 0 && !HasPermission(context.User, "canCancelFindingWithEvidence")) || (files.Count == 0 && !HasPermission(context.User, "canCancelFindingWithoutEvidence")))
			{
				return Results.Forbid();
			}
			HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };
			List<FindingEvidenceUpload> evidence = new List<FindingEvidenceUpload>();
			long totalBytes = 0L;
			foreach (IFormFile file in files)
			{
				if (file.Length == 0L || file.Length > 8388608 || !allowed.Contains(file.ContentType))
				{
					return Results.UnprocessableEntity(new
					{
						error = "Cada evidencia debe ser JPG, PNG o WebP y pesar menos de 8 MB."
					});
				}
				totalBytes += file.Length;
				if (totalBytes > 41943040)
				{
					return Results.UnprocessableEntity(new
					{
						error = "Las evidencias superan el límite total de 40 MB."
					});
				}
				await using (Stream input = file.OpenReadStream())
				{
					using MemoryStream buffer = new MemoryStream();
					await input.CopyToAsync(buffer, ct);
					byte[] data = buffer.ToArray();
					if (!ValidEvidenceImage(data, file.ContentType))
					{
						return Results.UnprocessableEntity(new
						{
							error = "Una evidencia no contiene una imagen válida."
						});
					}
					evidence.Add(new FindingEvidenceUpload(Path.GetFileName(file.FileName), file.ContentType, data));
				}
				IResult result = null;
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.CancelAutomaticFinding(id, reason, tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("display_name") ?? "Usuario", context.User.Identity?.Name ?? "", evidence, HasPermission(context.User, "canCancelFindingWithEvidence"), HasPermission(context.User, "canCancelFindingWithoutEvidence"), HasPermission(context.User, "canCancelFindingWithReason"), HasPermission(context.User, "canCancelFindingWithoutReason"), ct);
			if (!ticketActionResult.Ok)
			{
				return (ticketActionResult.Code == "CONFLICT") ? Results.Conflict(new
				{
					error = ticketActionResult.Error
				}) : Results.NotFound(new
				{
					error = ticketActionResult.Error
				});
			}
			broker.Publish("finding-cancelled");
			return Results.Ok(new
			{
				ok = true,
				evidence = evidence.Count
			});
		})).RequireAuthorization("SupportTechnician");
		webApplication.MapGet("/api/tickets/findings/evidence/{evidenceId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid evidenceId, HttpContext context, Database db, CancellationToken ct) =>
		{
			(byte[], string)? tuple = await db.FindingEvidencePhoto(evidenceId, UserScope(context).Role, ct);
			return (!tuple.HasValue) ? Results.NotFound() : Results.File(tuple.Value.Item1, tuple.Value.Item2, null, enableRangeProcessing: true);
		})).RequireAuthorization("SupportTechnician");
		webApplication.MapPost("/api/tickets", (Func<HttpRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (HttpRequest request, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			List<TicketEvidenceUpload> evidence = new List<TicketEvidenceUpload>();
			string supervisorEscalationReason = null;
			IFormCollection form;
			TicketCreate body;
			if (request.HasFormContentType)
			{
				form = await request.ReadFormAsync(ct);
				Guid? agencyId = (Guid.TryParse(Text("agencyId"), out var result) ? new Guid?(result) : ((Guid?)null));
				bool? allowDuplicate = (bool.TryParse(Text("allowDuplicate"), out var result2) ? new bool?(result2) : ((bool?)null));
				body = new TicketCreate(agencyId, Text("category"), Text("assignedDepartment"), Text("priority"), Text("subject"), Text("description"), Text("ticketType"), Text("assignedTechnicianId"), Text("assignedTeam"), allowDuplicate);
				supervisorEscalationReason = Text("supervisorEscalationReason");
				if (scope.Role == "GroupAdministrator")
				{
					bool flag = supervisorEscalationReason == null;
					bool flag2 = flag;
					int length;
					if (!flag2)
					{
						length = supervisorEscalationReason.Length;
						bool flag3 = ((length < 15 || length > 1000) ? true : false);
						flag2 = flag3;
					}
					if (flag2)
					{
						return Results.UnprocessableEntity(new
						{
							error = "Explica qué revisaste y por qué el caso no puede resolverse en la agencia (mínimo 15 caracteres)."
						});
					}
					IReadOnlyList<IFormFile> files = form.Files.GetFiles("evidence");
					length = files.Count;
					if ((length < 1 || length > 4) ? true : false)
					{
						return Results.UnprocessableEntity(new
						{
							error = "Adjunta entre 1 y 4 fotografías que demuestren por qué el caso debe escalarse."
						});
					}
					HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };
					long totalBytes = 0L;
					foreach (IFormFile file in files)
					{
						if (file.Length == 0L || file.Length > 8388608 || !allowed.Contains(file.ContentType))
						{
							return Results.UnprocessableEntity(new
							{
								error = "Cada evidencia debe ser JPG, PNG o WebP y pesar menos de 8 MB."
							});
						}
						totalBytes += file.Length;
						if (totalBytes > 25165824)
						{
							return Results.UnprocessableEntity(new
							{
								error = "Las evidencias superan el límite total de 24 MB."
							});
						}
						await using (Stream input = file.OpenReadStream())
						{
							using MemoryStream buffer = new MemoryStream();
							await input.CopyToAsync(buffer, ct);
							byte[] data = buffer.ToArray();
							if (!ValidEvidenceImage(data, file.ContentType))
							{
								return Results.UnprocessableEntity(new
								{
									error = "Una evidencia no contiene una imagen válida."
								});
							}
							evidence.Add(new TicketEvidenceUpload(Path.GetFileName(file.FileName), file.ContentType, data));
						}
						IResult result3 = null;
					}
				}
			}
			else
			{
				if (scope.Role == "GroupAdministrator")
				{
					return Results.UnprocessableEntity(new
					{
						error = "Los supervisores deben justificar el escalamiento y adjuntar evidencia fotográfica."
					});
				}
				try
				{
					body = await request.ReadFromJsonAsync<TicketCreate>(ct);
				}
				catch (JsonException)
				{
					return Results.BadRequest(new
					{
						error = "La información del ticket no tiene un formato válido."
					});
				}
			}
			if ((object)body == null)
			{
				return Results.BadRequest(new
				{
					error = "Completa la información del ticket."
				});
			}
			bool hasValue = body.WorkshopStockId.HasValue;
			bool flag4 = hasValue;
			if (flag4)
			{
				bool flag5 = !WorkshopActor.From(context).CanOperate || body.WorkshopStockId == Guid.Empty || body.TicketType != "INTERNAL";
				bool flag6 = flag5;
				if (!flag6)
				{
					string assignedDepartment = body.AssignedDepartment;
					bool flag3 = ((assignedDepartment == "TECHNOLOGY" || assignedDepartment == "GENERAL_SERVICES") ? true : false);
					flag6 = !flag3;
				}
				flag4 = flag6;
			}
			if (flag4)
			{
				return Results.Forbid();
			}
			bool flag7 = scope.Role == "GroupAdministrator";
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag3;
				switch (body?.AssignedDepartment)
				{
				case "TECHNOLOGY":
				case "GENERAL_SERVICES":
				case "HUMAN_RESOURCES":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag8 = !flag3;
			}
			if (flag8)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Los supervisores solo pueden crear tickets para Tecnología, Servicios Generales y Recursos Humanos."
				});
			}
			string text2 = ValidateTicket(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			try
			{
				SupportTicketRow supportTicketRow = await db.CreateTicket(body, scope.UserId, scope.Role, context.User.FindFirstValue("display_name") ?? "Usuario", context.User.Identity?.Name ?? "", context.User.FindFirstValue("support_team"), HasPermission(context.User, "canAssignTickets"), evidence, supervisorEscalationReason, ct, body.WorkshopStockId);
				if ((object)supportTicketRow == null)
				{
					return Results.Forbid();
				}
				broker.Publish("ticket-created");
				if (body.WorkshopStockId.HasValue)
				{
					broker.Publish("maintenance-workshop-updated");
				}
				return Results.Json(supportTicketRow, (JsonSerializerOptions?)null, (string?)null, (int?)201);
			}
			catch (TicketDuplicateException ex2)
			{
				return Results.Conflict(new
				{
					error = $"Ya existe el ticket #{ex2.TicketNumber} con esta avería para la agencia.",
					code = "DUPLICATE_TICKET",
					duplicateTicketId = ex2.TicketId,
					duplicateTicketNumber = ex2.TicketNumber,
					duplicateStatus = ex2.Status,
					duplicateSubject = ex2.Subject
				});
			}
			catch (SqlException ex3) when (body.WorkshopStockId.HasValue && ex3.Number == 50001)
			{
				return Results.Conflict(new
				{
					error = ex3.Message
				});
			}
			string? Text(string key)
			{
				string text3 = form[key].ToString().Trim();
				if (!string.IsNullOrWhiteSpace(text3))
				{
					return text3;
				}
				return null;
			}
		})).DisableAntiforgery().RequireAuthorization("TicketCreate");
		webApplication.MapPut("/api/tickets/{id:guid}", (Func<Guid, TicketUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, TicketUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string text2 = ValidateTicketUpdate(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.UpdateTicket(id, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", context.User.FindFirstValue("support_team"), HasPermission(context.User, "canAssignTickets"), ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("ticket-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/tickets/{id:guid}/route", (Func<Guid, TicketRoute, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, TicketRoute body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string text2 = ValidateTicketRoute(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.RouteTicket(id, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", context.User.FindFirstValue("support_team"), ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("ticket-routed");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("TicketRouting");
		webApplication.MapPut("/api/tickets/{id:guid}/share", (Func<Guid, TicketShare, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, TicketShare body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (body.Departments == null || body.Departments.Count > 10 || (body.Comment?.Length ?? 0) > 1000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona hasta 10 departamentos y agrega un comentario de hasta 1,000 caracteres."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.ShareTicket(id, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", context.User.FindFirstValue("support_team"), ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("ticket-shared");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("TicketRouting");
		webApplication.MapPost("/api/tickets/{id:guid}/resolve", (Func<Guid, HttpRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, HttpRequest request, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa la solución y adjunta la evidencia."
				});
			}
			IFormCollection form = await request.ReadFormAsync(ct);
			string status = form["status"].ToString().Trim().ToUpperInvariant();
			if (string.IsNullOrWhiteSpace(status))
			{
				status = "RESOLVED";
			}
			string resolution = form["resolution"].ToString().Trim();
			string text2 = status;
			if ((!(text2 == "RESOLVED") && !(text2 == "CLOSED")) || 1 == 0)
			{
				return Results.UnprocessableEntity(new
				{
					error = "El estado final debe ser resuelto o cerrado."
				});
			}
			int length = resolution.Length;
			if ((length < 10 || length > 2000) ? true : false)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Describe la solución aplicada (entre 10 y 2,000 caracteres)."
				});
			}
			length = form.Files.Count;
			if ((length < 1 || length > 8) ? true : false)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Adjunta entre 1 y 8 fotografías de evidencia."
				});
			}
			string replacementSerial = form["replacementSerial"].ToString().Trim();
			string removedSerial = form["removedSerial"].ToString().Trim();
			string text3 = await db.WorkshopTicketReplacementSerial(id, ct);
			if (!string.IsNullOrWhiteSpace(text3))
			{
				replacementSerial = text3;
			}
			if (replacementSerial.Length > 120 || removedSerial.Length > 120)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Los seriales del equipo deben tener hasta 120 caracteres."
				});
			}
			string text4 = (string.IsNullOrWhiteSpace(replacementSerial) ? "" : ("\n\nSerial del equipo reemplazado: " + replacementSerial)) + (string.IsNullOrWhiteSpace(removedSerial) ? "" : ("\nSerial del equipo retirado: " + removedSerial));
			if (resolution.Length + text4.Length > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La solución y los seriales juntos deben tener hasta 2,000 caracteres."
				});
			}
			resolution += text4;
			double? latitude = (double.TryParse(form["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? new double?(result) : ((double?)null));
			double? longitude = (double.TryParse(form["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) ? new double?(result2) : ((double?)null));
			double? accuracy = (double.TryParse(form["accuracyMeters"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) ? new double?(result3) : ((double?)null));
			DateTime? locationCapturedAt = (DateTime.TryParse(form["locationCapturedAt"], null, DateTimeStyles.RoundtripKind, out var result4) ? new DateTime?(result4) : ((DateTime?)null));
			HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };
			List<TicketEvidenceUpload> evidence = new List<TicketEvidenceUpload>();
			long totalBytes = 0L;
			foreach (IFormFile file in form.Files)
			{
				if (file.Length == 0L || file.Length > 8388608 || !allowed.Contains(file.ContentType))
				{
					return Results.UnprocessableEntity(new
					{
						error = "Cada evidencia debe ser JPG, PNG o WebP y pesar menos de 8 MB."
					});
				}
				totalBytes += file.Length;
				if (totalBytes > 67108864)
				{
					return Results.UnprocessableEntity(new
					{
						error = "Las evidencias superan el límite total de 64 MB."
					});
				}
				await using (Stream input = file.OpenReadStream())
				{
					using MemoryStream buffer = new MemoryStream();
					await input.CopyToAsync(buffer, ct);
					byte[] data = buffer.ToArray();
					if (!ValidEvidenceImage(data, file.ContentType))
					{
						return Results.UnprocessableEntity(new
						{
							error = "Una evidencia no contiene una imagen válida."
						});
					}
					evidence.Add(new TicketEvidenceUpload(Path.GetFileName(file.FileName), file.ContentType, data));
				}
				IResult result5 = null;
			}
			evidence = evidence.Select((TicketEvidenceUpload item) => item with
			{
				Latitude = latitude,
				Longitude = longitude,
				AccuracyMeters = accuracy,
				LocationCapturedAt = locationCapturedAt
			}).ToList();
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.ResolveTicket(id, status, resolution, evidence, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", context.User.FindFirstValue("support_team"), HasPermission(context.User, "canAssignTickets"), HasPermission(context.User, "canResolveTickets"), ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("ticket-resolved");
			return Results.Ok(new
			{
				ok = true,
				evidence = evidence.Count
			});
		})).RequireAuthorization("PortalUser").DisableAntiforgery();
		webApplication.MapGet("/api/tickets/evidence/{evidenceId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid evidenceId, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			(byte[], string, string)? tuple2 = await db.TicketEvidencePhoto(evidenceId, tuple.Item1, tuple.Item2, HasPermission(context.User, "canAssignTickets"), context.User.FindFirstValue("support_team"), ct);
			return (!tuple2.HasValue) ? Results.NotFound() : Results.File(tuple2.Value.Item1, tuple2.Value.Item2, null, enableRangeProcessing: true);
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/agency-transitions", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			List<AgencyTransitionRow> list = await db.AgencyTransitions(UserScope(context).Role, ct);
			return (list == null) ? Results.Forbid() : Results.Ok(new
			{
				projects = list
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPost("/api/agency-transitions", (Func<AgencyTransitionCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (AgencyTransitionCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag = !body.AgencyId.HasValue;
			bool flag2 = flag;
			if (!flag2)
			{
				string projectType = body.ProjectType;
				bool flag3 = ((projectType == "CONSTRUCTION" || projectType == "RESTRUCTURING") ? true : false);
				flag2 = !flag3;
			}
			if (flag2 || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona la agencia, el tipo de proceso y agrega notas de hasta 2,000 caracteres."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.CreateAgencyTransition(body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/agency-transitions/{id:guid}/stage", (Func<Guid, AgencyTransitionStageUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, AgencyTransitionStageUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string status = body.Status;
			bool flag = ((status == "IN_PROGRESS" || status == "COMPLETED") ? true : false);
			if (!flag || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona un estado válido y agrega notas de hasta 2,000 caracteres."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.UpdateAgencyTransitionStage(id, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/agency-transitions/{id:guid}/status", (Func<Guid, AgencyTransitionStatusUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, AgencyTransitionStatusUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string status = body.Status;
			bool flag = ((status == "ACTIVE" || status == "PENDING") ? true : false);
			if (!flag || (body.Reason?.Length ?? 0) > 1000 || (body.Status == "PENDING" && (body.Reason?.Trim().Length ?? 0) < 5))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Para dejar el proceso pendiente, indica un motivo de al menos 5 caracteres."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.UpdateAgencyTransitionStatus(id, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-status-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPut("/api/agency-transitions/{id:guid}/stages/{stageId:guid}", (Func<Guid, Guid, AgencyTransitionStageEdit, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, AgencyTransitionStageEdit body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if ((body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La descripción admite hasta 2,000 caracteres."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.EditAgencyTransitionStage(id, stageId, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-edited");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPut("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}/assignment", (Func<Guid, Guid, Guid, AgencyTransitionAssignmentUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, Guid progressId, AgencyTransitionAssignmentUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string assignmentType = body.AssignmentType;
			bool flag = ((assignmentType == "TECHNICIAN" || assignmentType == "CONTRACTOR") ? true : false);
			if (!flag || (body.ContractorName?.Length ?? 0) > 160)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona un técnico o escribe un contratista válido."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.AssignAgencyTransitionProgress(id, stageId, progressId, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-record-assigned");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}/evidence", (Func<Guid, Guid, Guid, HttpRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, Guid progressId, HttpRequest request, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Toma una fotografía con la cámara y el GPS activado."
				});
			}
			IFormCollection formCollection = await request.ReadFormAsync(ct);
			int count = formCollection.Files.Count;
			if ((count < 1 || count > 4) ? true : false)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Adjunta entre 1 y 4 fotografías de evidencia."
				});
			}
			double? latitude = (double.TryParse(formCollection["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? new double?(result) : ((double?)null));
			double? longitude = (double.TryParse(formCollection["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) ? new double?(result2) : ((double?)null));
			double? accuracy = (double.TryParse(formCollection["accuracyMeters"], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) ? new double?(result3) : ((double?)null));
			DateTime? locationCapturedAt = (DateTime.TryParse(formCollection["locationCapturedAt"], null, DateTimeStyles.RoundtripKind, out var result4) ? new DateTime?(result4) : ((DateTime?)null));
			bool flag;
			if (latitude.HasValue)
			{
				double valueOrDefault = latitude.GetValueOrDefault();
				if (!(valueOrDefault < -90.0) && !(valueOrDefault > 90.0))
				{
					flag = false;
					goto IL_020c;
				}
			}
			flag = true;
			goto IL_020c;
			IL_02e9:
			bool flag3;
			bool flag2 = flag3;
			goto IL_02ed;
			IL_0258:
			bool flag5;
			bool flag4 = flag5 || (latitude == 0.0 && longitude == 0.0);
			flag2 = flag4;
			if (!flag2)
			{
				if (accuracy.HasValue)
				{
					double valueOrDefault2 = accuracy.GetValueOrDefault();
					if (!(valueOrDefault2 <= 0.0))
					{
						flag3 = false;
						goto IL_02e9;
					}
				}
				flag3 = true;
				goto IL_02e9;
			}
			goto IL_02ed;
			IL_020c:
			bool flag6 = flag;
			flag5 = flag6;
			if (!flag5)
			{
				if (longitude.HasValue)
				{
					double valueOrDefault2 = longitude.GetValueOrDefault();
					if (!(valueOrDefault2 < -180.0) && !(valueOrDefault2 > 180.0))
					{
						flag3 = false;
						goto IL_0254;
					}
				}
				flag3 = true;
				goto IL_0254;
			}
			goto IL_0258;
			IL_0254:
			flag5 = flag3;
			goto IL_0258;
			IL_02ed:
			if (flag2 || !locationCapturedAt.HasValue)
			{
				return Results.UnprocessableEntity(new
				{
					error = "No se recibió una ubicación GPS válida. Activa la ubicación y toma la foto nuevamente."
				});
			}
			if (locationCapturedAt.Value.ToUniversalTime() < DateTime.UtcNow.AddHours(-2.0) || locationCapturedAt.Value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(10.0))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La ubicación GPS no es reciente. Toma la fotografía nuevamente desde este formulario."
				});
			}
			HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };
			List<AgencyTransitionEvidenceUpload> files = new List<AgencyTransitionEvidenceUpload>();
			long totalBytes = 0L;
			foreach (IFormFile file in formCollection.Files)
			{
				if (file.Length == 0L || file.Length > 8388608 || !allowed.Contains(file.ContentType))
				{
					return Results.UnprocessableEntity(new
					{
						error = "Cada evidencia debe ser JPG, PNG o WebP y pesar menos de 8 MB."
					});
				}
				totalBytes += file.Length;
				if (totalBytes > 33554432)
				{
					return Results.UnprocessableEntity(new
					{
						error = "Las evidencias superan el límite total de 32 MB."
					});
				}
				await using (Stream input = file.OpenReadStream())
				{
					using MemoryStream buffer = new MemoryStream();
					await input.CopyToAsync(buffer, ct);
					byte[] data = buffer.ToArray();
					if (!ValidEvidenceImage(data, file.ContentType))
					{
						return Results.UnprocessableEntity(new
						{
							error = "Una evidencia no contiene una imagen válida."
						});
					}
					files.Add(new AgencyTransitionEvidenceUpload(Path.GetFileName(file.FileName), file.ContentType, data, latitude, longitude, accuracy, locationCapturedAt));
				}
				IResult result5 = null;
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.AddAgencyTransitionEvidence(id, stageId, progressId, files, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-evidence-added");
			return Results.Ok(new
			{
				ok = true,
				evidence = files.Count,
				gpsVerified = true,
				latitude = latitude,
				longitude = longitude,
				accuracyMeters = accuracy
			});
		})).RequireAuthorization("PortalUser").DisableAntiforgery()
			.RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/agency-transitions/evidence/{evidenceId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid evidenceId, HttpContext context, Database db, CancellationToken ct) =>
		{
			(byte[], string)? tuple = await db.AgencyTransitionEvidencePhoto(evidenceId, UserScope(context).Role, ct);
			return (!tuple.HasValue) ? Results.NotFound() : Results.File(tuple.Value.Item1, tuple.Value.Item2, null, enableRangeProcessing: true);
		})).RequireAuthorization("PortalUser");
		webApplication.MapDelete("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}/evidence/{evidenceId:guid}", (Func<Guid, Guid, Guid, Guid, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, Guid progressId, Guid evidenceId, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			TicketActionResult ticketActionResult = await db.DeleteAgencyTransitionEvidence(id, stageId, progressId, evidenceId, UserScope(context).Role, ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-evidence-deleted");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPut("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}", (Func<Guid, Guid, Guid, AgencyTransitionProgressUpdate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, Guid progressId, AgencyTransitionProgressUpdate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag = (body.Note?.Length ?? 0) > 2000;
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3;
				switch (body.Status)
				{
				case null:
				case "IN_PROGRESS":
				case "COMPLETED":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = !flag3;
			}
			if (flag2 || (string.IsNullOrWhiteSpace(body.Note) && body.Status == null))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Indica el texto actualizado o el estado del registro."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.UpdateAgencyTransitionProgress(id, stageId, progressId, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-progress-updated");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapDelete("/api/agency-transitions/{id:guid}/stages/{stageId:guid}/progress/{progressId:guid}", (Func<Guid, Guid, Guid, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, Guid progressId, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.DeleteAgencyTransitionProgress(id, stageId, progressId, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-progress-deleted");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapDelete("/api/agency-transitions/{id:guid}/stages/{stageId:guid}", (Func<Guid, Guid, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Guid stageId, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.DeleteAgencyTransitionStageContent(id, stageId, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-stage-content-deleted");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapDelete("/api/agency-transitions/{id:guid}", (Func<Guid, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			TicketActionResult ticketActionResult = await db.DeleteAgencyTransition(id, UserScope(context).Role, ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("agency-transition-deleted");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/maintenance/movements", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			string supportTeam = context.User.FindFirstValue("support_team");
			List<MaintenanceMovementRow> rows = await db.MaintenanceMovements(scope.UserId, scope.Role, supportTeam, ct);
			return (rows == null) ? Results.Forbid() : Results.Ok(new
			{
				movements = rows,
				products = await db.MaintenanceProducts(scope.Role, supportTeam, ct),
				pendingStock = await db.WarehousePendingStock(scope.Role, supportTeam, ct)
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPost("/api/maintenance/movements", (Func<MaintenanceMovementCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (MaintenanceMovementCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string text2 = (body.OperationalArea ?? "").Trim().ToUpperInvariant();
			string movement = (body.MovementType ?? "").Trim().ToUpperInvariant();
			bool flag = ((text2 == "WORKSHOP" || text2 == "WAREHOUSE") ? true : false);
			bool flag2 = !flag;
			switch (movement)
			{
			case "ENTRY":
			case "EXIT":
			case "REQUEST":
			case "NEW_DELIVERY":
			case "DAMAGED_RETURN":
			case "TRANSFER_TO_WORKSHOP":
			case "REPLACEMENT":
			case "COMPONENT_REPLACEMENT":
			case "REPAIR":
			case "DISCHARGE":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag3 = !flag;
			bool flag4 = text2 == "WAREHOUSE";
			bool flag5 = flag4;
			if (flag5)
			{
				switch (movement)
				{
				case "ENTRY":
				case "EXIT":
				case "REQUEST":
				case "NEW_DELIVERY":
				case "DAMAGED_RETURN":
				case "TRANSFER_TO_WORKSHOP":
				case "DISCHARGE":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				flag5 = !flag;
			}
			bool flag6 = flag5;
			if (!flag6)
			{
				bool flag7 = text2 == "WORKSHOP";
				bool flag8 = flag7;
				if (flag8)
				{
					switch (movement)
					{
					case "ENTRY":
					case "EXIT":
					case "TRANSFER_TO_WORKSHOP":
					case "REPAIR":
					case "REPLACEMENT":
					case "COMPONENT_REPLACEMENT":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					flag8 = !flag;
				}
				flag6 = flag8;
			}
			bool flag9 = flag6;
			bool flag10 = (!(text2 == "WORKSHOP") || !(movement == "ENTRY")) && movement != "REQUEST" && (string.IsNullOrWhiteSpace(body.DeliveredByName) || string.IsNullOrWhiteSpace(body.ReceivedByName));
			string text3 = movement;
			flag = ((text3 == "NEW_DELIVERY" || text3 == "EXIT") ? true : false);
			bool flag11 = flag && body.TechnicianUserId == null;
			bool flag12 = movement == "REQUEST" && !body.AgencyId.HasValue;
			bool flag13 = text2 == "WAREHOUSE";
			bool flag14 = flag13;
			if (flag14)
			{
				switch (movement)
				{
				case "ENTRY":
				case "REQUEST":
				case "TRANSFER_TO_WORKSHOP":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				flag14 = flag;
			}
			int num = (flag14 ? 10000 : 1000);
			if (body.Quantity < 1 || body.Quantity > num)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Indica una cantidad entera entre 1 y " + ((num == 10000) ? "10,000" : "1,000") + " unidades."
				});
			}
			if ((flag2 | flag3 | flag9 | flag10 | flag11 | flag12) || string.IsNullOrWhiteSpace(body.DocumentNumber) || string.IsNullOrWhiteSpace(body.EquipmentType) || string.IsNullOrWhiteSpace(body.FailureCause) || (movement != "REQUEST" && (!(text2 == "WAREHOUSE") || !(movement == "ENTRY")) && string.IsNullOrWhiteSpace(body.SerialNumber)) || (body.EquipmentType?.Length ?? 0) > 100 || (body.ComponentType?.Length ?? 0) > 100 || (body.Notes?.Length ?? 0) > 2000 || (body.SerialNumber?.Length ?? 0) > 120 || (body.SignatureData?.Length ?? 0) > 500000 || (body.DeliveredByName?.Length ?? 0) > 160 || (body.ReceivedByName?.Length ?? 0) > 160 || (body.DestinationName?.Length ?? 0) > 200)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa los datos obligatorios. Las entradas a Almacén permiten serial opcional. Las solicitudes requieren agencia y equipo; las salidas y entregas conservan sus requisitos de serial y custodia."
				});
			}
			if (body.SignatureData != null && !body.SignatureData.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La firma digital no tiene un formato válido."
				});
			}
			(string, string) tuple = UserScope(context);
			WorkshopActor actor = WorkshopActor.From(context);
			TicketActionResult ticketActionResult = await db.CreateMaintenanceMovement(body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), actor.Name, ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("maintenance-movement-created");
			bool flag15 = body.TechnicianUserId != null && movement != "REQUEST";
			bool flag16 = flag15;
			if (flag16)
			{
				flag16 = await db.NotifyMaintenanceReceiptCreated(body.DocumentNumber, actor, ct);
			}
			if (flag16)
			{
				broker.Publish("chat-message");
				broker.Publish("institutional-document-created");
			}
			return Results.Ok(new
			{
				ok = true,
				documentNumber = body.DocumentNumber,
				qrToken = body.QrToken
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapGet("/api/maintenance/procurement", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			MaintenanceProcurementSnapshot maintenanceProcurementSnapshot = await db.MaintenanceProcurement(UserScope(context).Role, context.User.FindFirstValue("support_team"), ct);
			return ((object)maintenanceProcurementSnapshot == null) ? Results.Forbid() : Results.Ok(maintenanceProcurementSnapshot);
		})).RequireAuthorization("PortalUser");
		webApplication.MapPost("/api/maintenance/suppliers", (Func<MaintenanceSupplierCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (MaintenanceSupplierCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(body.Name) || (body.Name?.Length ?? 0) > 160 || (body.TaxId?.Length ?? 0) > 40 || (body.ContactName?.Length ?? 0) > 120 || (body.Phone?.Length ?? 0) > 50 || (body.Email?.Length ?? 0) > 160)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa un proveedor válido."
				});
			}
			TicketActionResult ticketActionResult = await db.CreateMaintenanceSupplier(body, UserScope(context).Role, context.User.FindFirstValue("support_team"), context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("maintenance-supplier-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/maintenance/requisitions", (Func<MaintenanceRequisitionCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (MaintenanceRequisitionCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag = string.IsNullOrWhiteSpace(body.Department) || string.IsNullOrWhiteSpace(body.ProductName) || body.ProductName.Length > 160;
			bool flag2 = flag;
			if (!flag2)
			{
				int quantity = body.Quantity;
				bool flag3 = ((quantity < 1 || quantity > 100000) ? true : false);
				flag2 = flag3;
			}
			bool flag4 = flag2;
			if (!flag4)
			{
				bool flag3;
				switch (body.Priority)
				{
				case "LOW":
				case "MEDIUM":
				case "HIGH":
				case "CRITICAL":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag4 = !flag3;
			}
			if (flag4 || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa departamento, artículo, cantidad y prioridad de la requisición."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.CreateMaintenanceRequisition(body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("maintenance-requisition-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/maintenance/purchase-orders", (Func<MaintenancePurchaseOrderCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (MaintenancePurchaseOrderCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag = body.SupplierId == Guid.Empty || string.IsNullOrWhiteSpace(body.Department) || string.IsNullOrWhiteSpace(body.ProductName);
			bool flag2 = flag;
			if (!flag2)
			{
				int quantity = body.Quantity;
				bool flag3 = ((quantity < 1 || quantity > 100000) ? true : false);
				flag2 = flag3;
			}
			if (flag2 || body.UnitCost < 0m || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa proveedor, departamento, artículo, cantidad y costo de la orden."
				});
			}
			TicketActionResult ticketActionResult = await db.CreateMaintenancePurchaseOrder(body, UserScope(context).Role, context.User.FindFirstValue("support_team"), context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("maintenance-purchase-order-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/maintenance/purchase-orders/{id:guid}/receive", (Func<Guid, MaintenancePurchaseOrderReceive, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, MaintenancePurchaseOrderReceive body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			int quantity = body.Quantity;
			bool flag = ((quantity < 1 || quantity > 100000) ? true : false);
			if (flag || string.IsNullOrWhiteSpace(body.SerialNumber) || (body.SerialNumber?.Length ?? 0) > 120 || (body.ReceivedByName?.Length ?? 0) > 160 || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Indica cantidad recibida, serial/lote y responsable de recepción."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.ReceiveMaintenancePurchaseOrder(id, body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("maintenance-purchase-order-received");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/maintenance/returns", (Func<MaintenanceReturnCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (MaintenanceReturnCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			bool flag = string.IsNullOrWhiteSpace(body.Department) || string.IsNullOrWhiteSpace(body.ProductName) || string.IsNullOrWhiteSpace(body.SerialNumber) || string.IsNullOrWhiteSpace(body.Reason);
			bool flag2 = flag;
			if (!flag2)
			{
				int quantity = body.Quantity;
				bool flag3 = ((quantity < 1 || quantity > 100000) ? true : false);
				flag2 = flag3;
			}
			if (flag2 || (body.Notes?.Length ?? 0) > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa artículo, serial/lote, cantidad y motivo de devolución."
				});
			}
			(string, string) tuple = UserScope(context);
			TicketActionResult ticketActionResult = await db.CreateMaintenanceReturn(body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("support_team"), context.User.FindFirstValue("display_name") ?? "Usuario", ct);
			if (!ticketActionResult.Ok)
			{
				return TicketActionResponse(ticketActionResult);
			}
			broker.Publish("maintenance-return-created");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapDelete("/api/tickets/{id:guid}", (Func<Guid, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!(await db.DeleteTicket(id, ct)))
			{
				return Results.NotFound(new
				{
					error = "Ticket no encontrado."
				});
			}
			broker.Publish("ticket-deleted");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("TicketDeletion");
		webApplication.MapGet("/api/support/chat/conversations", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				conversations = await db.SupportConversations(tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("SupportChat");
		webApplication.MapGet("/api/support/chat/contacts", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) => Results.Ok(new
		{
			contacts = await db.SupportContacts(UserScope(context).UserId, ct)
		}))).RequireAuthorization("SupportChat");
		webApplication.MapPost("/api/support/chat/direct", (Func<SupportDirectConversationCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (SupportDirectConversationCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(body.TargetUserId))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona un contacto."
				});
			}
			(string, string) tuple = UserScope(context);
			SupportConversationRow supportConversationRow = await db.CreateDirectSupportConversation(body.TargetUserId, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", context.User.Identity?.Name ?? "", ct);
			if ((object)supportConversationRow == null)
			{
				return Results.NotFound(new
				{
					error = "El contacto ya no está disponible."
				});
			}
			broker.Publish("chat-direct-created");
			return Results.Ok(supportConversationRow);
		})).RequireAuthorization("SupportChat");
		webApplication.MapGet("/api/support/chat/conversations/{id:guid}/messages", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				messages = await db.SupportMessages(id, tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("SupportChat");
		webApplication.MapGet("/api/support/chat/notifications", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			return Results.Ok(new
			{
				notifications = await db.SupportChatNotifications(tuple.Item1, tuple.Item2, ct)
			});
		})).RequireAuthorization("SupportChat");
		webApplication.MapPost("/api/notifications/state", (Func<NotificationStateInput, HttpContext, Database, CancellationToken, Task<IResult>>)(async (NotificationStateInput body, HttpContext context, Database db, CancellationToken ct) =>
		{
			bool flag = body.Keys == null || body.Keys.Count == 0 || body.Keys.Count > 200;
			bool flag2 = flag;
			if (!flag2)
			{
				string action = body.Action;
				bool flag3 = ((action == "READ" || action == "DISMISS") ? true : false);
				flag2 = !flag3;
			}
			if (flag2)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona notificaciones y una acción válida."
				});
			}
			await db.SetNotificationState(UserScope(context).UserId, body.Keys, body.Action == "DISMISS", ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("TicketNotifications").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/support/chat/conversations", (Func<SupportConversationCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (SupportConversationCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(body.Department) || body.Department.Length > 40 || (body.Category?.Length ?? 0) > 60)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona un departamento y un tipo de ayuda válidos."
				});
			}
			(string, string) tuple = UserScope(context);
			SupportConversationRow supportConversationRow = await db.CreateSupportConversation(body, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Supervisor", context.User.Identity?.Name ?? "", ct);
			if ((object)supportConversationRow == null)
			{
				return Results.Forbid();
			}
			broker.Publish("chat-created");
			return Results.Json(supportConversationRow, (JsonSerializerOptions?)null, (string?)null, (int?)201);
		})).RequireAuthorization("SupportChat");
		webApplication.MapPost("/api/support/chat/conversations/{id:guid}/messages", (Func<Guid, SupportMessageCreate, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, SupportMessageCreate body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(body.Message) || body.Message.Trim().Length > 2000)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Escribe un mensaje de hasta 2,000 caracteres."
				});
			}
			(string, string) tuple = UserScope(context);
			if (!(await db.AddSupportMessage(id, body.Message.Trim(), tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct)))
			{
				return Results.NotFound(new
				{
					error = "Conversación no encontrada."
				});
			}
			broker.Publish("chat-message");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportChat");
		webApplication.MapDelete("/api/support/chat/messages/{id:guid}", (Func<Guid, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			if (!(await db.DeleteSupportMessage(id, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct)))
			{
				return Results.NotFound(new
				{
					error = "El mensaje no existe o no tienes permiso para eliminarlo."
				});
			}
			broker.Publish("chat-message-deleted");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportChat").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/support/chat/conversations/{id:guid}/voice", (Func<Guid, HttpRequest, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, HttpRequest request, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			if (!request.HasFormContentType)
			{
				return Results.BadRequest(new
				{
					error = "Adjunta una nota de voz valida."
				});
			}
			IFormCollection form = await request.ReadFormAsync(ct);
			IFormFile voice = form.Files.GetFile("voice");
			if (voice == null || voice.Length == 0L || voice.Length > 10485760)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La nota de voz debe pesar hasta 10 MB."
				});
			}
			string[] source = new string[6] { "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav", "audio/x-wav" };
			string contentType = (voice.ContentType ?? "").Split(';')[0].ToLowerInvariant();
			if (!Enumerable.Contains(source, contentType))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Formato de audio no compatible."
				});
			}
			IResult result;
			await using (MemoryStream stream = new MemoryStream())
			{
				await voice.CopyToAsync(stream, ct);
				byte[] array = stream.ToArray();
				if (!ValidAudio(array, contentType))
				{
					result = Results.UnprocessableEntity(new
					{
						error = "El archivo no contiene una nota de voz válida."
					});
				}
				else
				{
					int? durationSeconds = (int.TryParse(form["durationSeconds"], out var result2) ? new int?(Math.Clamp(result2, 0, 600)) : ((int?)null));
					(string, string) tuple = UserScope(context);
					if (!(await db.AddSupportVoiceNote(id, array, contentType, Path.GetFileName(voice.FileName), durationSeconds, tuple.Item1, tuple.Item2, context.User.FindFirstValue("display_name") ?? "Usuario", ct)))
					{
						result = Results.NotFound();
					}
					else
					{
						broker.Publish("chat-message");
						result = Results.Ok(new
						{
							ok = true
						});
					}
				}
			}
			return result;
		})).RequireAuthorization("SupportChat").DisableAntiforgery()
			.RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/support/chat/voice/{messageId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid messageId, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			(byte[], string, string)? tuple2 = await db.SupportVoiceNote(messageId, tuple.Item1, tuple.Item2, ct);
			return (!tuple2.HasValue) ? Results.NotFound() : Results.File(tuple2.Value.Item1, tuple2.Value.Item2, tuple2.Value.Item3, enableRangeProcessing: true);
		})).RequireAuthorization("SupportChat");
		webApplication.MapPut("/api/support/chat/conversations/{id:guid}/status", (Func<Guid, SupportConversationStatus, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, SupportConversationStatus body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string[] source = new string[4] { "WAITING_SUPPORT", "IN_PROGRESS", "RESOLVED", "CLOSED" };
			if (!Enumerable.Contains(source, body.Status))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Estado de conversación no válido."
				});
			}
			(string, string) tuple = UserScope(context);
			if (!(await db.UpdateSupportConversationStatus(id, body.Status, tuple.Item1, tuple.Item2, ct)))
			{
				return Results.NotFound(new
				{
					error = "Conversación no encontrada."
				});
			}
			broker.Publish("chat-status");
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("SupportChatManage");
		webApplication.MapPost("/api/support/chat/conversations/{id:guid}/control", (Func<Guid, SupportConversationControl, HttpContext, Database, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (Guid id, SupportConversationControl body, HttpContext context, Database db, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			string action = (body.Action ?? "").Trim().ToUpperInvariant();
			string[] source = new string[7] { "MUTE", "UNMUTE", "RESTRICT", "UNRESTRICT", "BLOCK", "UNBLOCK", "DELETE" };
			if (!Enumerable.Contains(source, action))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Control de conversación no válido."
				});
			}
			bool flag;
			switch (action)
			{
			case "RESTRICT":
			case "UNRESTRICT":
			case "BLOCK":
			case "UNBLOCK":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag && !HasPermission(context.User, "canModerateSupportChat"))
			{
				return Results.Forbid();
			}
			(string UserId, string Role) scope = UserScope(context);
			bool flag2 = action == "DELETE" && !HasPermission(context.User, "canDeleteSupportConversations");
			bool flag3 = flag2;
			if (flag3)
			{
				flag3 = !(await db.CanDeleteDirectConversation(id, scope.UserId, scope.Role, ct));
			}
			if (flag3)
			{
				return Results.Forbid();
			}
			if (!(await db.UpdateSupportConversationControl(id, action, scope.UserId, scope.Role, ct)))
			{
				return Results.NotFound(new
				{
					error = "Conversación no encontrada."
				});
			}
			broker.Publish("chat-control");
			return Results.Ok(new
			{
				ok = true,
				action = action
			});
		})).RequireAuthorization("SupportChat");
		webApplication.MapPost("/api/support/chat/presence", (Func<SupportPresenceInput, HttpContext, Database, SupportPresenceTracker, LiveNotificationBroker, CancellationToken, Task<IResult>>)(async (SupportPresenceInput body, HttpContext context, Database db, SupportPresenceTracker tracker, LiveNotificationBroker broker, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			Guid? conversationId = body.ConversationId;
			string department;
			if (conversationId.HasValue)
			{
				Guid valueOrDefault = conversationId.GetValueOrDefault();
				string text2 = await db.SupportPresenceContext(valueOrDefault, scope.UserId, scope.Role, ct);
				if (text2 == null)
				{
					return Results.NotFound(new
					{
						error = "Conversación no encontrada."
					});
				}
				department = text2;
			}
			else
			{
				department = scope.Role switch
				{
					"Technology" => "TECHNOLOGY", 
					"GeneralServices" => "GENERAL_SERVICES", 
					"HumanResources" => "HUMAN_RESOURCES", 
					_ => "", 
				};
			}
			tracker.Touch(scope.UserId, context.User.FindFirstValue("display_name") ?? "Usuario", scope.Role, department, body.ConversationId, body.IsTyping);
			broker.Publish("chat-presence");
			return Results.Ok(tracker.Snapshot(department, body.ConversationId, scope.UserId));
		})).RequireAuthorization("SupportChat");
		webApplication.MapGet("/api/support/chat/presence/{conversationId:guid}", (Func<Guid, HttpContext, Database, SupportPresenceTracker, CancellationToken, Task<IResult>>)(async (Guid conversationId, HttpContext context, Database db, SupportPresenceTracker tracker, CancellationToken ct) =>
		{
			(string UserId, string Role) scope = UserScope(context);
			string text2 = await db.SupportPresenceContext(conversationId, scope.UserId, scope.Role, ct);
			return (text2 == null) ? Results.NotFound() : Results.Ok(tracker.Snapshot(text2, conversationId, scope.UserId));
		})).RequireAuthorization("SupportChat");
		webApplication.MapGet("/api/account/contact-profile", (Func<HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			Guid result;
			return Guid.TryParse(tuple.Item1, out result) ? Results.Ok(await db.ContactProfile(result, ct)) : Results.Ok(new
			{
				id = tuple.Item1,
				email = (context.User.Identity?.Name ?? ""),
				displayName = (context.User.FindFirstValue("display_name") ?? "Administrador principal"),
				role = tuple.Item2,
				contact = (string)null,
				avatarUrl = (string)null,
				groups = Array.Empty<string>()
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/account/contact-profile", (Func<AccountContactUpdate, HttpContext, Database, CancellationToken, Task<IResult>>)(async (AccountContactUpdate body, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (!Guid.TryParse(UserScope(context).UserId, out var result))
			{
				return Results.Conflict(new
				{
					error = "La cuenta de recuperación no guarda un perfil. Crea un usuario administrador permanente."
				});
			}
			if ((body.Contact?.Length ?? 0) > 80)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La información de contacto supera 80 caracteres."
				});
			}
			await db.UpdateOwnContact(result, body.Contact, ct);
			return Results.Ok(new
			{
				ok = true
			});
		})).RequireAuthorization("PortalUser");
		webApplication.MapPut("/api/admin/audits/{profileId:guid}", (Func<Guid, AuditUpdate, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid profileId, AuditUpdate body, HttpContext context, Database db, CancellationToken ct) =>
		{
			string text2 = ValidateAuditUpdate(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			AuditDetailRow auditDetailRow = await db.UpdateAudit(profileId, body, context.User.Identity?.Name ?? "admin", ct);
			return ((object)auditDetailRow == null) ? Results.NotFound(new
			{
				error = "No encontramos esta auditoría."
			}) : Results.Ok(auditDetailRow);
		})).RequireAuthorization("AuditManagement");
		webApplication.MapDelete("/api/admin/audits/{profileId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid profileId, HttpContext context, Database db, CancellationToken ct) => (await db.DeleteAudit(profileId, context.User.Identity?.Name ?? "admin", ct)) ? Results.Ok(new
		{
			ok = true
		}) : Results.NotFound(new
		{
			error = "No encontramos esta auditoría."
		}))).RequireAuthorization("AuditManagement");
		webApplication.MapGet("/api/admin/users", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Ok(new
		{
			users = (await db.AdminUsers(ct)).Select(WithEffectivePermissions)
		}))).RequireAuthorization("UserAdministration");
		webApplication.MapGet("/api/admin/access-profiles", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Ok(new
		{
			profiles = await db.UserAccessPresets(ct)
		}))).RequireAuthorization("UserAdministration");
		webApplication.MapPut("/api/admin/access-profiles", (Func<UserAccessPreset, HttpContext, Database, CancellationToken, Task<IResult>>)(async (UserAccessPreset body, HttpContext context, Database db, CancellationToken ct) =>
		{
			string[] source = new string[16]
			{
				"TECHNOLOGY", "GENERAL_SERVICES", "HUMAN_RESOURCES", "ADMINISTRATIVE", "WAREHOUSE", "WORKSHOP", "COMMUNICATION", "COMMERCIALIZATION", "TREASURY", "ACCOUNTING",
				"GENERAL_AUDIT", "VAULT", "CASHIER", "CALL_CENTER", "TECHNICAL_FAILURE", "SUPERVISION"
			};
			string[] source2 = new string[9] { "ADMINISTRATOR", "DEPARTMENT_MANAGER", "SUPPORT", "WAREHOUSE_REQUESTER", "TECHNICIAN", "SUPERVISOR", "ADMINISTRATIVE", "FISCALIZADOR", "VIEWER" };
			HashSet<string> keys = Permissions("Administrator").Keys.Concat(OperationalPermissions.Defaults("Administrator", null).Keys).ToHashSet();
			if (!Enumerable.Contains(source, body.Department) || !Enumerable.Contains(source2, body.Profile) || body.Permissions == null || body.Permissions.Count > 100 || body.Permissions.Keys.Any((string key) => !keys.Contains(key)))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Perfil, departamento o permisos no válidos."
				});
			}
			if (body.Profile == "SUPPORT")
			{
				return Results.Conflict(new
				{
					error = "El perfil Soporte utiliza la configuración compartida cuando está disponible. Puedes ajustar los permisos del usuario en este formulario."
				});
			}
			await db.SaveUserAccessPreset(body, context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "", ct);
			return Results.Ok(body);
		})).RequireAuthorization("UserAdministration").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/admin/groups-catalog", (Func<Database, CancellationToken, Task<IResult>>)(async (Database db, CancellationToken ct) => Results.Ok(new
		{
			groups = await db.AllGroupNames(ct)
		}))).RequireAuthorization("UserAdministration");
		webApplication.MapPost("/api/admin/users", (Func<AdminUserCreate, Database, CancellationToken, Task<IResult>>)(async (AdminUserCreate body, Database db, CancellationToken ct) =>
		{
			body = body with
			{
				Permissions = new Dictionary<string, bool>(body.Permissions ?? new Dictionary<string, bool>())
				{
					["usesExplicitPermissions"] = true,
					["usesCustomizedPermissions"] = true
				}
			};
			if (SupportProfilePolicy.IsSupport(body.Role ?? "", body.SupportTeam, body.Permissions))
			{
				body = body with
				{
					Permissions = SupportProfilePolicy.ForSave(body.Permissions, Database.SupportStandard)
				};
			}
			string text2 = ValidateUserWithTeam(body.Email, body.DisplayName, body.Role, body.Password, passwordRequired: true, body.SupportTeam);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			try
			{
				return Results.Json(WithEffectivePermissions(await db.CreateAdminUser(body, ct)), (JsonSerializerOptions?)null, (string?)null, (int?)201);
			}
			catch (AdminUserConflictException)
			{
				return Results.Conflict(new
				{
					error = "Ya existe una cuenta activa con ese usuario. Usa otro nombre de usuario."
				});
			}
			catch (SqlException ex2) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				int number = ex2.Number;
				return (number == 2601 || number == 2627) ? true : false;
			}).Invoke())
			{
				return Results.Conflict(new
				{
					error = "Ya existe una cuenta con ese usuario."
				});
			}
		})).RequireAuthorization("UserAdministration");
		webApplication.MapPut("/api/admin/users/{id:guid}", (Func<Guid, AdminUserUpdate, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, AdminUserUpdate body, HttpContext context, Database db, CancellationToken ct) =>
		{
			body = body with
			{
				Permissions = new Dictionary<string, bool>(body.Permissions ?? new Dictionary<string, bool>())
				{
					["usesExplicitPermissions"] = true,
					["usesCustomizedPermissions"] = true
				}
			};
			AdminUserRow? existing = (await db.AdminUsers(ct)).FirstOrDefault(user => user.Id == id);
			bool flag = SupportProfilePolicy.IsSupport(body.Role ?? "", body.SupportTeam, body.Permissions);
			bool flag2 = flag;
			if (flag2)
			{
				string text2 = existing?.Email;
				bool flag3 = ((text2 == "ross.mariel" || text2 == "ross.mariel@grupotejeda.local") ? true : false);
				flag2 = !flag3;
			}
			if (flag2)
			{
				body = body with
				{
					Permissions = SupportProfilePolicy.ForSave(body.Permissions, Database.SupportStandard)
				};
			}
			if ((object)existing == null)
			{
				return Results.NotFound(new
				{
					error = "Usuario no encontrado."
				});
			}
			string text3 = ValidateUserWithTeam(ExistingUserLogin.Unchanged(body.Email, existing.Email) ? "existing.account" : body.Email, body.DisplayName, body.Role, body.Password, passwordRequired: false, body.SupportTeam);
			if (text3 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text3
				});
			}
			if (context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") == id.ToString() && (!body.IsActive || body.Role != "ADMINISTRATOR"))
			{
				return Results.Conflict(new
				{
					error = "No puedes quitarte tu propio acceso de administrador."
				});
			}
			try
			{
				AdminUserRow adminUserRow = await db.UpdateAdminUser(id, body, ct);
				string text2 = existing.Email;
				if ((text2 == "ross.mariel" || text2 == "ross.mariel@grupotejeda.local") ? true : false)
				{
					Database.InvalidateSupportStandard();
				}
				return ((object)adminUserRow == null) ? Results.NotFound(new
				{
					error = "Usuario no encontrado."
				}) : Results.Ok(WithEffectivePermissions(adminUserRow));
			}
			catch (SqlException ex) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				int number = ex.Number;
				return (number == 2601 || number == 2627) ? true : false;
			}).Invoke())
			{
				return Results.Conflict(new
				{
					error = "Ya existe una cuenta con ese usuario."
				});
			}
		})).RequireAuthorization("UserAdministration");
		webApplication.MapDelete("/api/admin/users/{id:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid id, HttpContext context, Database db, CancellationToken ct) =>
		{
			IResult result = ((context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") == id.ToString()) ? Results.Conflict(new
			{
				error = "No puedes eliminar la cuenta con la que estás conectado."
			}) : ((await db.DeleteAdminUser(id, context.User.Identity?.Name ?? "", ct)) ? Results.Ok(new
			{
				ok = true
			}) : Results.NotFound(new
			{
				error = "Usuario no encontrado o ya fue eliminado."
			})));
			return result;
		})).RequireAuthorization("UserAdministration");
		webApplication.MapPut("/api/account/password", (Func<PasswordChange, HttpContext, Database, CancellationToken, Task<IResult>>)(async (PasswordChange body, HttpContext context, Database db, CancellationToken ct) =>
		{
			context.Response.Headers.CacheControl = "no-store";
			if (string.IsNullOrEmpty(body.CurrentPassword) || string.IsNullOrEmpty(body.NewPassword))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Completa la contraseña actual y la nueva."
				});
			}
			if (body.CurrentPassword.Length > 128 || body.NewPassword.Length < 8 || body.NewPassword.Length > 128 || body.NewPassword == "123456")
			{
				return Results.UnprocessableEntity(new
				{
					error = "La nueva contraseña debe tener entre 8 y 128 caracteres y no puede ser la clave temporal."
				});
			}
			(string, string) tuple = UserScope(context);
			AdminIdentity adminIdentity;
			if (Guid.TryParse(tuple.Item1, out var result))
			{
				adminIdentity = await db.ChangeOwnPassword(result, body.CurrentPassword, body.NewPassword, ct);
			}
			else
			{
				AdminIdentity adminIdentity2 = ((!(tuple.Item1 == "legacy-admin") || !(tuple.Item2 == "Administrator")) ? null : (await db.ChangeLegacyPassword(context.User.Identity?.Name ?? "", body.CurrentPassword, body.NewPassword, ct)));
				adminIdentity = adminIdentity2;
			}
			AdminIdentity account = adminIdentity;
			if ((object)account == null)
			{
				return Results.Json(new
				{
					error = "La contraseña actual no es correcta o este acceso ya fue reemplazado. Revisa tus datos e inicia sesión nuevamente si es necesario."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)400);
			}
			await SignInAccount(context, account);
			string token = null;
			try
			{
				token = await db.CreatePersistentLoginToken(account.Id, ct, account.RecoveryVersion);
			}
			catch (Exception exception) when (!ct.IsCancellationRequested)
			{
				context.RequestServices.GetRequiredService<ILogger<Program>>().LogWarning(exception, "Contraseña cambiada; se conserva la cookie actual sin token persistente.");
			}
			return Results.Ok(SessionPayload(account, token));
		})).RequireAuthorization("PortalUser").RequireRateLimiting("login");
		webApplication.MapPost("/api/account/avatar", (Func<HttpRequest, HttpContext, Database, CancellationToken, Task<IResult>>)(async (HttpRequest request, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (!Guid.TryParse(UserScope(context).UserId, out var id))
			{
				return Results.Conflict(new
				{
					error = "La cuenta de recuperación no admite fotografía."
				});
			}
			if (!request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona una fotografía."
				});
			}
			IFormFile file = (await request.ReadFormAsync(ct)).Files.GetFile("avatar");
			if (file == null || file.Length == 0L)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona una fotografía."
				});
			}
			if (file.Length > 5242880)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La foto de perfil supera 5 MB."
				});
			}
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg", "image/png", "image/webp" };
			if (!hashSet.Contains(file.ContentType))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La foto debe ser JPG, PNG o WebP."
				});
			}
			IResult result;
			await using (Stream input = file.OpenReadStream())
			{
				using MemoryStream buffer = new MemoryStream();
				await input.CopyToAsync(buffer, ct);
				byte[] data = buffer.ToArray();
				if (!ValidEvidenceImage(data, file.ContentType))
				{
					result = Results.UnprocessableEntity(new
					{
						error = "El archivo no contiene una fotografía válida."
					});
				}
				else
				{
					await db.AttachAvatar(id, data, file.ContentType, ct);
					result = Results.Ok(new
					{
						avatarUrl = $"/api/account/avatar/{id}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
					});
				}
			}
			return result;
		})).RequireAuthorization("PortalUser").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/account/avatar/{userId:guid}", (Func<Guid, Database, CancellationToken, Task<IResult>>)(async (Guid userId, Database db, CancellationToken ct) =>
		{
			(byte[], string)? tuple = await db.Avatar(userId, ct);
			return (!tuple.HasValue) ? Results.NotFound() : Results.File(tuple.Value.Item1, tuple.Value.Item2, null, enableRangeProcessing: true);
		})).RequireAuthorization("PortalUser");
		webApplication.MapPost("/api/submit", (Func<Submit, HttpContext, Database, Tokens, CancellationToken, Task<IResult>>)(async (Submit body, HttpContext http, Database db, Tokens tokens, CancellationToken ct) =>
		{
			if (!string.IsNullOrEmpty(body.Website))
			{
				return Results.BadRequest(new
				{
					error = "Solicitud rechazada."
				});
			}
			if (!Guid.TryParse(body.AgencyId, out var agencyId))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La agencia no es válida."
				});
			}
			string text2 = Validate(body);
			if (text2 != null)
			{
				return Results.UnprocessableEntity(new
				{
					error = text2
				});
			}
			HashSet<string> allowedDamageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"WIRING", "SCREENS", "CONNECTIVITY", "FURNITURE", "GENERAL_PAINTING", "GENERAL_INVERTER", "GENERAL_BATTERIES", "GENERAL_RESTRUCTURING", "GENERAL_LIGHTING", "GENERAL_METALWORK",
				"GENERAL_GENERATOR_REQUEST", "GENERAL_ELECTRICAL", "GENERAL_SHUTTER", "GENERAL_OTHER"
			};
			if (body.Answers?.DamageFound == "yes" && (body.Answers.DamageTypes == null || body.Answers.DamageTypes.Count == 0))
			{
				return Results.UnprocessableEntity(new
				{
					error = "Indica al menos un tipo de avería."
				});
			}
			StructuralAnswers? answers = body.Answers;
			if ((object)answers != null && answers.DamageTypes?.Any((string value) => !allowedDamageTypes.Contains(value)) == true)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La clasificación de la avería no es válida."
				});
			}
			(string UserId, string Role) scope = UserScope(http);
			if (!(await db.CanAccessAgency(agencyId, scope.UserId, scope.Role, ct)))
			{
				return Results.Forbid();
			}
			Guid profileId = await db.Submit(agencyId, body, scope.UserId, http.User.Identity?.Name, http.User.FindFirstValue("display_name"), http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString(), ct);
			return Results.Json(new
			{
				ok = true,
				profileId = profileId,
				uploadToken = tokens.Create(profileId.ToString())
			}, (JsonSerializerOptions?)null, (string?)null, (int?)201);
		})).RequireAuthorization("FieldAudit").RequireRateLimiting("sensitive-write");
		webApplication.MapPost("/api/image", (Func<HttpRequest, Database, Tokens, CancellationToken, Task<IResult>>)(async (HttpRequest request, Database db, Tokens tokens, CancellationToken ct) =>
		{
			if (!request.HasFormContentType)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona una imagen válida."
				});
			}
			IFormCollection form = await request.ReadFormAsync(ct);
			string text2 = form["profileId"].ToString();
			string photoType = form["photoType"].ToString();
			double photoLat = 0.0;
			double photoLng = 0.0;
			double photoAccuracy = 0.0;
			DateTimeOffset photoCaptured = default;
			bool validGeo = double.TryParse(form["photoLatitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out photoLat) && double.TryParse(form["photoLongitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out photoLng) && double.TryParse(form["photoAccuracy"], NumberStyles.Float, CultureInfo.InvariantCulture, out photoAccuracy) && DateTimeOffset.TryParse(form["photoCapturedAt"], out photoCaptured);
			if (!Guid.TryParse(text2, out var id))
			{
				return Results.Json(new
				{
					error = "No se pudo identificar la auditoría de esta fotografía."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
			}
			(string, string) tuple = UserScope(request.HttpContext);
			bool flag = !tokens.Verify(form["token"].ToString(), text2);
			bool flag2 = flag;
			if (flag2)
			{
				flag2 = !(await db.CanAccessProfile(id, tuple.Item1, tuple.Item2, ct));
			}
			if (flag2)
			{
				return Results.Json(new
				{
					error = "Tu usuario no tiene acceso a esta auditoría."
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403);
			}
			IFormFile file = form.Files.GetFile("image");
			if (file == null || file.Length == 0L)
			{
				return Results.UnprocessableEntity(new
				{
					error = "Selecciona una imagen válida."
				});
			}
			if (file.Length > 20971520)
			{
				return Results.UnprocessableEntity(new
				{
					error = "La imagen supera el máximo de 20 MB."
				});
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["image/jpeg"] = "jpg",
				["image/jpg"] = "jpg",
				["image/png"] = "png",
				["image/webp"] = "webp",
				["image/heic"] = "heic",
				["image/heif"] = "heif",
				["image/heic-sequence"] = "heic",
				["image/heif-sequence"] = "heif"
			};
			if (!dictionary.TryGetValue(file.ContentType, out var _))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La cámara envió un formato no reconocido. Se aceptan JPG, PNG, WebP, HEIC y HEIF."
				});
			}
			if (!Enumerable.Contains(new string[5] { "frontal", "operativa", "entorno", "inversor", "bateria" }, photoType))
			{
				return Results.UnprocessableEntity(new
				{
					error = "El tipo de fotografía no es válido."
				});
			}
			if (!validGeo || !FieldAuditGps.ValidCapture(photoLat, photoLng, photoAccuracy, photoCaptured, DateTimeOffset.UtcNow))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La fotografía no contiene una ubicación GPS válida."
				});
			}
			if (!(await db.PhotoMatchesProfile(id, photoLat, photoLng, photoAccuracy, photoType, ct)))
			{
				return Results.UnprocessableEntity(new
				{
					error = "La ubicación de esta fotografía no coincide con el punto verificado de la banca."
				});
			}
			IResult result;
			await using (Stream input = file.OpenReadStream())
			{
				using MemoryStream buffer = new MemoryStream();
				await input.CopyToAsync(buffer, ct);
				byte[] data = buffer.ToArray();
				if (!ValidEvidenceImage(data, file.ContentType))
				{
					result = Results.UnprocessableEntity(new
					{
						error = "El archivo enviado no contiene una imagen válida."
					});
				}
				else
				{
					await db.AttachPhoto(id, photoType, data, file.ContentType, photoLat, photoLng, photoAccuracy, photoCaptured.UtcDateTime, ct);
					result = Results.Ok(new
					{
						ok = true
					});
				}
			}
			return result;
		})).RequireAuthorization("FieldAudit").RequireRateLimiting("sensitive-write");
		webApplication.MapGet("/api/images/{profileId:guid}", (Func<Guid, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid profileId, HttpContext context, Database db, CancellationToken ct) =>
		{
			(string, string) tuple = UserScope(context);
			if (!(await db.CanAccessProfile(profileId, tuple.Item1, tuple.Item2, ct)))
			{
				return Results.Forbid();
			}
			(byte[], string)? tuple2 = await db.Photo(profileId, ct);
			context.Response.Headers.CacheControl = "no-store";
			return (!tuple2.HasValue) ? Results.NotFound() : Results.File(tuple2.Value.Item1, tuple2.Value.Item2);
		})).RequireAuthorization("PanelView");
		webApplication.MapGet("/api/images/{profileId:guid}/{photoType}", (Func<Guid, string, HttpContext, Database, CancellationToken, Task<IResult>>)(async (Guid profileId, string photoType, HttpContext context, Database db, CancellationToken ct) =>
		{
			if (!Enumerable.Contains(new string[5] { "frontal", "operativa", "entorno", "inversor", "bateria" }, photoType))
			{
				return Results.NotFound();
			}
			(string, string) tuple = UserScope(context);
			if (!(await db.CanAccessProfile(profileId, tuple.Item1, tuple.Item2, ct)))
			{
				return Results.Forbid();
			}
			(byte[], string)? tuple2 = await db.Photo(profileId, photoType, ct);
			context.Response.Headers.CacheControl = "no-store";
			return (!tuple2.HasValue) ? Results.NotFound() : Results.File(tuple2.Value.Item1, tuple2.Value.Item2);
		})).RequireAuthorization("PanelView");
		webApplication.Map("/api/{**path}", (Func<IResult>)(() => Results.NotFound(new
		{
			error = "La ruta solicitada no existe."
		})));
		webApplication.MapFallbackToFile("index.html");
		webApplication.Run();
		static Dictionary<string, bool> AccountPermissions(AdminIdentity account)
		{
			account = EmmaAdministrativeAccess.Scope(account);
			string role = account.Role;
			string role2 = account.Role;
			bool value = ((role2 == "Administrator" || role2 == "Fiscalizador") ? true : false);
			Dictionary<string, bool> dictionary = Permissions(role, value, account.Role == "Administrator", account.Permissions);
			// La auditoría de campo es una capacidad propia del rol Supervisor.
			// No puede quedar oculta por un permiso personalizado antiguo: el API ya
			// autoriza a GroupAdministrator para crear auditorías y subir evidencia.
			if (account.Role == "GroupAdministrator")
			{
				dictionary["canAudit"] = true;
				dictionary["canViewDashboard"] = true;
			}
			if (account.Permissions == null || !account.Permissions.ContainsKey("canManageDepartment"))
			{
				dictionary["canManageDepartment"] = AreaManagerAccess.Default(account.Role, account.SupportTeam, account.Email);
			}
			role2 = account.SupportTeam;
			value = ((role2 == "WAREHOUSE" || role2 == "WORKSHOP") ? true : false);
			bool flag = value;
			if (flag)
			{
				string[] array = new string[17]
				{
					"canViewDashboard", "canAudit", "canExportAudits", "canManageUsers", "canManageAudits", "canManageAgencies", "canCreateTickets", "canAssignTickets", "canResolveTickets", "canViewAllSupportDepartments",
					"canConfigureSupport", "canViewSupportFindings", "canManageSupportChat", "canModerateSupportChat", "canDeleteSupportTickets", "canDeleteSupportConversations", "canShareSupportConversations"
				};
				foreach (string key in array)
				{
					dictionary[key] = false;
				}
			}
			if (account.SupportTeam == "WAREHOUSE")
			{
				if (account.Permissions == null || !account.Permissions.ContainsKey("canAuthorizeWarehouseRequests"))
				{
					dictionary["canAuthorizeWarehouseRequests"] = true;
				}
				if (account.Permissions == null || !account.Permissions.ContainsKey("canLoadWarehouseInventory"))
				{
					dictionary["canLoadWarehouseInventory"] = true;
				}
			}
			if (account.Role == "Administrative" || (account.Role == "Department" && DepartmentAccounts.Standalone(account.SupportTeam)))
			{
				dictionary["canCreateTickets"] = true;
			}
			if (account.Role == "Administrator")
			{
				dictionary["canManageUsers"] = true;
			}
			bool flag2 = account.Role == "GroupAdministrator";
			bool flag3 = flag2;
			if (!flag3)
			{
				switch (account.Role)
				{
				case "Technology":
				case "GeneralServices":
				case "HumanResources":
					value = true;
					break;
				default:
					value = false;
					break;
				}
				flag3 = value && !flag;
			}
			if (flag3)
			{
				dictionary["canViewSupportFindings"] = true;
			}
			if (IsDepartmentTicketResolver(account.Role, account.SupportTeam))
			{
				dictionary["canResolveTickets"] = true;
			}
			if (account.Email.Equals("deliana.solis@grupotejeda.local", StringComparison.OrdinalIgnoreCase))
			{
				dictionary["canAssignTickets"] = true;
				dictionary["canViewSupportDashboard"] = true;
				dictionary["canViewSupportFindings"] = true;
				dictionary["canCancelFindingWithoutEvidence"] = true;
				dictionary["canCancelFindingWithReason"] = true;
				dictionary["canCancelFindingWithoutReason"] = false;
			}
			if (account.Role == "GeneralServices")
			{
				dictionary["canCreateTickets"] = dictionary["canAssignTickets"] && account.SupportTeam != "WORKSHOP";
				dictionary["canViewSupportDashboard"] = true;
			}
			if (account.Role == "Technology" && IsTechnologySupportTeam(account.SupportTeam))
			{
				dictionary["canViewTicketNotifications"] = true;
			}
			bool flag4 = account.Role == "Technology";
			bool flag5 = flag4;
			if (flag5)
			{
				role2 = account.SupportTeam;
				value = ((role2 == "CALL_CENTER" || role2 == "TECHNICAL_FAILURE") ? true : false);
				flag5 = value;
			}
			if (flag5)
			{
				dictionary["canCreateTickets"] = true;
				dictionary["canAssignTickets"] = true;
			}
			if (account.Role == "Technology" && account.SupportTeam == "TECHNICIANS")
			{
				dictionary["canUseSupportChat"] = true;
				dictionary["canShareSupportConversations"] = true;
				dictionary["canViewTicketNotifications"] = true;
			}
			dictionary["canManageAgencies"] = !flag && (account.Role == "Administrator" || (account.Role == "Technology" && IsFranklinAgencyAdministrator(account.Email)));
			if (account.Role == "Warehouse" || account.SupportTeam == "WAREHOUSE")
			{
				dictionary["canCreateTickets"] = true;
			}
			if (dictionary.GetValueOrDefault("canManageDepartment"))
			{
				dictionary["canViewSupportDashboard"] = true;
			}
			if (account.Role != "Administrator")
			{
				AreaManagerAccess.ApplyViews(dictionary, account.Permissions);
			}
			dictionary["canManage"] = dictionary["canManageUsers"];
			OperationalPermissions.Apply(dictionary, account.Permissions, account.Role, account.SupportTeam, account.Email);
			ExplicitUserPermissions.Apply(dictionary, account.Permissions, account.Role);
			TechnicianResolutionPermissions.Apply(dictionary, account.Permissions, account.Role, account.SupportTeam);
			CallCenterSupportProfile.Apply(dictionary, account.Permissions, account.Role, account.SupportTeam);
			WarehouseRequesterProfile.Apply(dictionary, account.Permissions);
			if (account.Role == "Technology" && IsFranklinAgencyAdministrator(account.Email))
			{
				dictionary["canManageAgencies"] = true;
			}
			return dictionary;
		}
		static bool Bad(string? value, int max)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value.Trim().Length > max;
			}
			return true;
		}
		static bool Bad2(string? value, int max)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value.Trim().Length > max;
			}
			return true;
		}
		static bool Bad3(string? value, int max)
		{
			if (!string.IsNullOrWhiteSpace(value))
			{
				return value.Trim().Length > max;
			}
			return true;
		}
		static bool Bad4(string? x, int n)
		{
			if (!string.IsNullOrWhiteSpace(x))
			{
				return x.Trim().Length > n;
			}
			return true;
		}
		static string ClientKey(HttpContext context)
		{
			return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
		}
		static string First(string current, string fallback)
		{
			if (!string.IsNullOrWhiteSpace(current))
			{
				return current;
			}
			return fallback;
		}
		static bool Fixed(string? value, string expected)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
			byte[] bytes2 = Encoding.UTF8.GetBytes(expected);
			if (bytes.Length == bytes2.Length)
			{
				return CryptographicOperations.FixedTimeEquals(bytes, bytes2);
			}
			return false;
		}
		static bool HasPermission(ClaimsPrincipal user, string key)
		{
			return PermissionClaim(user).GetValueOrDefault(key);
		}
		static bool IsCrossSiteRequest(HttpRequest request)
		{
			string text2 = request.Headers["Sec-Fetch-Site"].ToString();
			if (string.Equals(text2, "cross-site", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (!string.IsNullOrWhiteSpace(text2))
			{
				return false;
			}
			string text3 = request.Headers.Origin.ToString();
			if (string.IsNullOrWhiteSpace(text3) || !Uri.TryCreate(text3, UriKind.Absolute, out Uri result))
			{
				return false;
			}
			return !string.Equals(result.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase);
		}
		static bool IsDepartmentTicketResolver(string role, string? supportTeam)
		{
			bool flag;
			switch (role)
			{
			case "Administrator":
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag2 = flag;
			bool flag3 = flag2;
			if (flag3)
			{
				bool flag4 = ((supportTeam == "WAREHOUSE" || supportTeam == "WORKSHOP") ? true : false);
				flag3 = !flag4 || (role == "GeneralServices" && supportTeam == "WORKSHOP");
			}
			return flag3;
		}
		static bool IsFranklinAgencyAdministrator(string? email)
		{
			string login = NormalizeLogin(email);
			return login is "franklin.calderon@grupotejeda.local" or "rosmarlin.vittini@grupotejeda.local" or "jordaniel.ortega@grupotejeda.local";
		}
		static bool IsTechnologySupportTeam(string? supportTeam)
		{
			switch (supportTeam)
			{
			case "CALL_CENTER":
			case "TECHNICAL_FAILURE":
			case "TECHNICIANS":
				return true;
			default:
				return false;
			}
		}
		static bool IsTransientSqlFailure(SqlException error)
		{
			switch (error.Number)
			{
			case -2:
			case 0:
			case 40:
			case 53:
			case 64:
			case 233:
			case 258:
			case 1205:
			case 10053:
			case 10054:
			case 10060:
			case 11001:
			case 40197:
			case 40501:
			case 40613:
			case 49918:
			case 49919:
			case 49920:
				return true;
			default:
				return false;
			}
		}
		static bool IsUnsafeMethod(string method)
		{
			switch (method)
			{
			case "POST":
			case "PUT":
			case "PATCH":
			case "DELETE":
				return true;
			default:
				return false;
			}
		}
		static void LoadEnv(string path)
		{
			if (!File.Exists(path))
			{
				return;
			}
			foreach (string item2 in File.ReadLines(path))
			{
				string text2 = item2.Trim();
				if (text2.Length != 0 && !text2.StartsWith('#'))
				{
					int num = text2.IndexOf('=');
					if (num >= 1)
					{
						string variable = text2.Substring(0, num).Trim();
						string value = text2.Substring(num + 1).Trim().Trim('"', '\'');
						if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
						{
							Environment.SetEnvironmentVariable(variable, value);
						}
					}
				}
			}
		}
		static string NormalizeLogin(string? value)
		{
			string text2 = (value ?? "").Trim().ToLowerInvariant();
			if (!string.IsNullOrWhiteSpace(text2) && !text2.Contains('@'))
			{
				return text2 + "@grupotejeda.local";
			}
			return text2;
		}
		static Dictionary<string, bool> PermissionClaim(ClaimsPrincipal user)
		{
			string text2 = user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "Viewer";
			string text3 = user.FindFirstValue("support_team");
			Dictionary<string, bool> dictionary = Permissions(text2);
			dictionary["canManageDepartment"] = AreaManagerAccess.Allowed(user);
			Dictionary<string, bool> dictionary2 = null;
			try
			{
				dictionary2 = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.FindFirstValue("permissions_json") ?? "");
				if (dictionary2 != null)
				{
					foreach (KeyValuePair<string, bool> item3 in dictionary2)
					{
						if (dictionary.ContainsKey(item3.Key))
						{
							dictionary[item3.Key] = item3.Value;
						}
					}
				}
			}
			catch
			{
			}
			if (text2 == "Administrative" || (text2 == "Department" && DepartmentAccounts.Standalone(text3)))
			{
				dictionary["canCreateTickets"] = true;
			}
			if (text2 == "Administrator")
			{
				dictionary["canManageUsers"] = true;
			}
			bool flag = text2 == "GroupAdministrator";
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3;
				switch (text2)
				{
				case "Technology":
				case "GeneralServices":
				case "HumanResources":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				bool flag4 = flag3;
				bool flag5 = flag4;
				if (flag5)
				{
					bool flag6 = ((text3 == "WAREHOUSE" || text3 == "WORKSHOP") ? true : false);
					flag5 = !flag6;
				}
				flag2 = flag5;
			}
			if (flag2)
			{
				dictionary["canViewSupportFindings"] = true;
			}
			if (IsDepartmentTicketResolver(text2, text3))
			{
				dictionary["canResolveTickets"] = true;
			}
			if (text2 == "GeneralServices")
			{
				dictionary["canCreateTickets"] = dictionary["canAssignTickets"] && text3 != "WORKSHOP";
				dictionary["canViewSupportDashboard"] = true;
			}
			if (text2 == "Technology" && IsTechnologySupportTeam(text3))
			{
				dictionary["canViewTicketNotifications"] = true;
			}
			bool flag7 = text2 == "Technology";
			bool flag8 = flag7;
			if (flag8)
			{
				bool flag3 = ((text3 == "CALL_CENTER" || text3 == "TECHNICAL_FAILURE") ? true : false);
				flag8 = flag3;
			}
			if (flag8)
			{
				dictionary["canCreateTickets"] = true;
				dictionary["canAssignTickets"] = true;
			}
			if (text2 == "Technology" && text3 == "TECHNICIANS")
			{
				dictionary["canUseSupportChat"] = true;
				dictionary["canShareSupportConversations"] = true;
			}
			if (text2 == "Technology" && IsFranklinAgencyAdministrator(user.Identity?.Name))
			{
				dictionary["canManageAgencies"] = true;
			}
			if (text2 == "Warehouse" || text3 == "WAREHOUSE")
			{
				dictionary["canCreateTickets"] = true;
			}
			if (text2 != "Administrator")
			{
				AreaManagerAccess.ApplyViews(dictionary, dictionary2);
			}
			dictionary["canManage"] = dictionary["canManageUsers"];
			OperationalPermissions.Apply(dictionary, dictionary2, text2, text3, user.Identity?.Name ?? "");
			ExplicitUserPermissions.Apply(dictionary, dictionary2, text2);
			TechnicianResolutionPermissions.Apply(dictionary, dictionary2, text2, text3);
			CallCenterSupportProfile.Apply(dictionary, dictionary2, text2, text3);
			WarehouseRequesterProfile.Apply(dictionary, dictionary2);
			if (text2 == "Technology" && IsFranklinAgencyAdministrator(user.Identity?.Name))
			{
				dictionary["canManageAgencies"] = true;
			}
			return dictionary;
		}
		static Dictionary<string, bool> Permissions(string role, bool? exportPermission = null, bool? managePermission = null, Dictionary<string, bool>? custom = null)
		{
			bool flag;
			switch (role)
			{
			case "Administrator":
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			bool flag2 = flag;
			Dictionary<string, bool> dictionary = new Dictionary<string, bool>
			{
				{
					"canViewFieldAudits",
					role == "Administrator"
				},
				{
					"canManageDepartment",
					role == "Administrator"
				},
				{ "canView", true },
				{
					"canManage",
					managePermission ?? (role == "Administrator")
				},
				{
					"canManageUsers",
					role == "Administrator"
				},
				{
					"canManageAudits",
					role == "Administrator"
				},
				{
					"canManageAgencies",
					role == "Administrator"
				}
			};
			Dictionary<string, bool> dictionary2 = dictionary;
			switch (role)
			{
			case "Administrator":
			case "Viewer":
			case "GroupAdministrator":
			case "Fiscalizador":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			dictionary2.Add("canViewDashboard", flag);
			Dictionary<string, bool> dictionary3 = dictionary;
			bool value = ((role == "Administrator" || role == "GroupAdministrator") ? true : false);
			dictionary3.Add("canAudit", value);
			Dictionary<string, bool> dictionary4 = dictionary;
			bool value2;
			switch (role)
			{
			case "Administrator":
			case "GroupAdministrator":
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				value2 = true;
				break;
			default:
				value2 = false;
				break;
			}
			dictionary4.Add("canCreateTickets", value2);
			dictionary.Add("canResolveTickets", flag2);
			dictionary.Add("canAssignTickets", flag2);
			dictionary.Add("canConfigureProfile", value: true);
			Dictionary<string, bool> dictionary5 = dictionary;
			bool? flag3 = exportPermission;
			bool value3;
			if (flag3.HasValue)
			{
				value3 = flag3 == true;
			}
			else
			{
				bool flag4 = ((role == "Administrator" || role == "Fiscalizador") ? true : false);
				value3 = flag4;
			}
			dictionary5.Add("canExportAudits", value3);
			dictionary.Add("canViewSupportDashboard", flag2);
			dictionary.Add("canViewAllSupportDepartments", role == "Administrator");
			dictionary.Add("canConfigureSupport", role == "Administrator");
			dictionary.Add("canViewSupportFindings", flag2 || role == "GroupAdministrator");
			dictionary.Add("canCancelFindingWithEvidence", flag2);
			dictionary.Add("canCancelFindingWithoutEvidence", value: false);
			dictionary.Add("canCancelFindingWithReason", flag2);
			dictionary.Add("canCancelFindingWithoutReason", value: false);
			Dictionary<string, bool> dictionary6 = dictionary;
			bool value4;
			switch (role)
			{
			case "Administrator":
			case "GroupAdministrator":
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				value4 = true;
				break;
			default:
				value4 = false;
				break;
			}
			dictionary6.Add("canUseSupportChat", value4);
			dictionary.Add("canManageSupportChat", flag2);
			dictionary.Add("canModerateSupportChat", flag2);
			dictionary.Add("canDeleteSupportConversations", role == "Administrator");
			Dictionary<string, bool> dictionary7 = dictionary;
			bool value5;
			switch (role)
			{
			case "Administrator":
			case "GroupAdministrator":
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				value5 = true;
				break;
			default:
				value5 = false;
				break;
			}
			dictionary7.Add("canShareSupportConversations", value5);
			dictionary.Add("canDeleteSupportTickets", role == "Administrator");
			dictionary.Add("canViewTicketNotifications", value: true);
			dictionary.Add("canAuthorizeWarehouseRequests", role == "Administrator");
			dictionary.Add("canLoadWarehouseInventory", role == "Administrator");
			dictionary.Add("canCreateWarehouseArticles", role == "Administrator");
			Dictionary<string, bool> dictionary8 = dictionary;
			if (custom != null)
			{
				foreach (KeyValuePair<string, bool> item4 in custom)
				{
					if (dictionary8.ContainsKey(item4.Key))
					{
						dictionary8[item4.Key] = item4.Value;
					}
				}
			}
			dictionary8["canManage"] = dictionary8["canManageUsers"];
			dictionary8["canViewTicketNotifications"] = role != "Administrative" || (custom?.GetValueOrDefault("canViewTicketNotifications") ?? false);
			return dictionary8;
		}
		static string Pick(JsonElement element, params string[] keys)
		{
			foreach (string propertyName in keys)
			{
				if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
				{
					return value.GetString();
				}
			}
			return "";
		}
		static async Task<T> RetryDatabaseAsync<T>(Func<Task<T>> action, CancellationToken ct, string operation) where T : notnull
		{
			SqlException last = null;
			for (int attempt = 0; attempt < 3; attempt++)
			{
				try
				{
					return await action();
				}
				catch (SqlException ex) when (!ct.IsCancellationRequested && IsTransientSqlFailure(ex))
				{
					last = ex;
					Console.Error.WriteLine($"SQL {operation} (reintento {attempt + 1}/3): {ex.Message}");
					if (attempt < 2)
					{
						await Task.Delay(400 * (attempt + 1), ct);
					}
				}
			}
			if (last != null)
			{
				throw last;
			}
			throw new InvalidOperationException("MSSQL_RETRY:No fue posible completar la operación de base de datos.");
		}
		static object SessionPayload(AdminIdentity account, string? persistentSessionToken = null)
		{
			account = EmmaAdministrativeAccess.Scope(account);
			string role = account.Role;
			bool flag = ((role == "Administrator" || role == "Fiscalizador") ? true : false);
			bool flag2 = flag;
			bool flag3 = account.Role == "Administrator";
			string id = account.Id;
			string email = account.Email;
			string displayName = account.DisplayName;
			string role2 = account.Role;
			bool mustChangePassword = account.MustChangePassword;
			string avatarUrl = account.AvatarUrl;
			string text2 = ((account.Role == "Department") ? account.SupportTeam : null);
			string text3 = ((account.Role == "Department") ? null : account.SupportTeam);
			string text4;
			if (account.Role == "GroupAdministrator")
			{
				text4 = "assigned-groups";
			}
			else
			{
				switch (account.Role)
				{
				case "Technology":
				case "GeneralServices":
				case "HumanResources":
				case "Warehouse":
				case "Department":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				text4 = (flag ? "support-department" : "all");
			}
			string id2 = id;
			string email2 = email;
			string displayName2 = displayName;
			string role3 = role2;
			bool mustChangePassword2 = mustChangePassword;
			string avatarUrl2 = avatarUrl;
			string assignedDepartment = text2;
			string supportTeam = text3;
			string scope = text4;
			string supportDepartment;
			if (account.Role == "Administrative")
			{
				supportDepartment = "ADMINISTRATIVE";
			}
			else if (account.Role == "Department")
			{
				supportDepartment = account.SupportTeam;
			}
			else if (account.Role == "Technology")
			{
				supportDepartment = "TECHNOLOGY";
			}
			else if (account.Role == "GeneralServices")
			{
				supportDepartment = "GENERAL_SERVICES";
			}
			else
			{
				supportDepartment = ((account.Role == "HumanResources") ? "HUMAN_RESOURCES" : null);
			}
			return new
			{
				authenticated = true,
				id = id2,
				email = email2,
				displayName = displayName2,
				role = role3,
				mustChangePassword = mustChangePassword2,
				avatarUrl = avatarUrl2,
				assignedDepartment = assignedDepartment,
				supportTeam = supportTeam,
				scope = scope,
				supportDepartment = supportDepartment,
				permissions = AccountPermissions(account),
				persistentSessionToken = persistentSessionToken
			};
		}
		static object SessionPayloadFromContext(HttpContext context)
		{
			string text2 = context.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "Viewer";
			string id = context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "";
			string email = context.User.Identity?.Name ?? "";
			string displayName = context.User.FindFirstValue("display_name") ?? "Usuario";
			string role = text2;
			bool mustChangePassword = context.User.FindFirstValue("must_change_password") == "true";
			string avatarUrl = (string.IsNullOrWhiteSpace(context.User.FindFirstValue("avatar_url")) ? null : context.User.FindFirstValue("avatar_url"));
			string assignedDepartment = ((text2 == "Department") ? context.User.FindFirstValue("support_team") : null);
			string supportTeam;
			if (text2 == "Department")
			{
				supportTeam = null;
			}
			else
			{
				supportTeam = (string.IsNullOrWhiteSpace(context.User.FindFirstValue("support_team")) ? null : context.User.FindFirstValue("support_team"));
			}
			string scope;
			bool flag;
			switch (text2)
			{
			case "GroupAdministrator":
				scope = "assigned-groups";
				break;
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
			case "Warehouse":
			case "Department":
				flag = true;
				goto IL_0177;
			default:
				{
					flag = false;
					goto IL_0177;
				}
				IL_0177:
				scope = (flag ? "support-department" : "all");
				break;
			}
			return new
			{
				authenticated = true,
				id = id,
				email = email,
				displayName = displayName,
				role = role,
				mustChangePassword = mustChangePassword,
				avatarUrl = avatarUrl,
				assignedDepartment = assignedDepartment,
				supportTeam = supportTeam,
				scope = scope,
				supportDepartment = text2 switch
				{
					"HumanResources" => "HUMAN_RESOURCES", 
					"GeneralServices" => "GENERAL_SERVICES", 
					"Technology" => "TECHNOLOGY", 
					"Department" => context.User.FindFirstValue("support_team"), 
					_ => null, 
				},
				permissions = PermissionClaim(context.User)
			};
		}
		static async Task SignInAccount(HttpContext context, AdminIdentity account)
		{
			account = EmmaAdministrativeAccess.Scope(account);
			Dictionary<string, bool> value = AccountPermissions(account);
			string operationalSessionId = context.User.FindFirstValue("operational_session_id") ?? "";
			if (string.IsNullOrWhiteSpace(operationalSessionId))
			{
				try
				{
					operationalSessionId = await context.RequestServices.GetRequiredService<Database>().BeginOperationalSession(account, context.RequestAborted);
				}
				catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
				{
					context.RequestServices.GetRequiredService<ILogger<Program>>().LogWarning(exception, "No se pudo iniciar el registro de actividad del usuario.");
				}
			}
			ClaimsIdentity identity = new ClaimsIdentity(new _003C_003Ez__ReadOnlyArray<Claim>(new Claim[10]
			{
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", account.Id),
				new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", account.Email),
				new Claim("recovery_version", account.RecoveryVersion),
				new Claim("display_name", account.DisplayName),
				new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", account.Role),
				new Claim("must_change_password", account.MustChangePassword ? "true" : "false"),
				new Claim("avatar_url", account.AvatarUrl ?? ""),
				new Claim("support_team", account.SupportTeam ?? ""),
				new Claim("permissions_json", JsonSerializer.Serialize(value)),
				new Claim("operational_session_id", operationalSessionId)
			}), "Cookies");
			ClaimsPrincipal principal = (context.User = new ClaimsPrincipal(identity));
			await context.SignInAsync("Cookies", principal, new AuthenticationProperties
			{
				IsPersistent = true,
				AllowRefresh = true,
				ExpiresUtc = DateTimeOffset.UtcNow.AddYears(10)
			});
		}
		static IResult TicketActionResponse(TicketActionResult result)
		{
			return result.Code switch
			{
				"FORBIDDEN" => Results.Json(new
				{
					error = result.Error
				}, (JsonSerializerOptions?)null, (string?)null, (int?)403), 
				"NOT_FOUND" => Results.NotFound(new
				{
					error = result.Error
				}), 
				"CONFLICT" => Results.Conflict(new
				{
					error = result.Error
				}), 
				_ => Results.UnprocessableEntity(new
				{
					error = result.Error
				}), 
			};
		}
		static (string UserId, string Role) UserScope(HttpContext context)
		{
			return (UserId: context.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "", Role: DepartmentAccounts.SupportScopeRole(context.User, context.Request.Path.Value ?? "", AreaManagerAccess.Allowed(context.User)));
		}
		static bool ValidAudio(byte[] data, string contentType)
		{
			switch (contentType)
			{
			case "audio/webm":
				return data.Length > 4 && data[0] == 26 && data[1] == 69 && data[2] == 223 && data[3] == 163;
			case "audio/ogg":
				return data.Length > 4 && Encoding.ASCII.GetString(data, 0, 4) == "OggS";
			case "audio/mp4":
				return data.Length > 12 && Encoding.ASCII.GetString(data, 4, 4) == "ftyp";
			case "audio/mpeg":
				return data.Length > 3 && (Encoding.ASCII.GetString(data, 0, 3) == "ID3" || (data[0] == byte.MaxValue && (data[1] & 0xE0) == 224));
			case "audio/wav":
			case "audio/x-wav":
				return data.Length > 12 && Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && Encoding.ASCII.GetString(data, 8, 4) == "WAVE";
			default:
				return false;
			}
		}
		static bool ValidEvidenceImage(byte[] data, string contentType)
		{
			string text2 = contentType.ToLowerInvariant();
			if (text2 != null)
			{
				int length = text2.Length;
				if (length != 9)
				{
					if (length != 10)
					{
						if (length == 19)
						{
							char c = text2[9];
							if (c != 'c')
							{
								if (c == 'f' && text2 == "image/heif-sequence")
								{
									goto IL_01cd;
								}
							}
							else if (text2 == "image/heic-sequence")
							{
								goto IL_01cd;
							}
						}
					}
					else
					{
						switch (text2[9])
						{
						case 'g':
							break;
						case 'p':
							if (text2 == "image/webp")
							{
								return data.Length > 12 && Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && Encoding.ASCII.GetString(data, 8, 4) == "WEBP";
							}
							goto IL_024b;
						case 'c':
							goto IL_00ba;
						case 'f':
							goto IL_00cf;
						default:
							goto IL_024b;
						}
						if (text2 == "image/jpeg")
						{
							goto IL_0132;
						}
					}
				}
				else
				{
					char c = text2[6];
					if (c != 'j')
					{
						if (c == 'p' && text2 == "image/png")
						{
							return data.Length > 8 && data[0] == 137 && data[1] == 80 && data[2] == 78 && data[3] == 71;
						}
					}
					else if (text2 == "image/jpg")
					{
						goto IL_0132;
					}
				}
			}
			goto IL_024b;
			IL_00ba:
			if (text2 == "image/heic")
			{
				goto IL_01cd;
			}
			goto IL_024b;
			IL_0132:
			return data.Length > 3 && data[0] == byte.MaxValue && data[1] == 216 && data[2] == byte.MaxValue;
			IL_01cd:
			return data.Length > 12 && Encoding.ASCII.GetString(data, 4, 4) == "ftyp" && Enumerable.Contains(new string[8] { "heic", "heix", "hevc", "hevx", "heim", "heis", "mif1", "msf1" }, Encoding.ASCII.GetString(data, 8, 4));
			IL_024b:
			return false;
			IL_00cf:
			if (text2 == "image/heif")
			{
				goto IL_01cd;
			}
			goto IL_024b;
		}
		static string? Validate(Submit b)
		{
			if (Bad4(b.EmployeeName, 160))
			{
				return "El nombre del empleado es obligatorio.";
			}
			if (Bad4(b.Direccion, 300))
			{
				return "La dirección es obligatoria.";
			}
			if (Bad4(b.Sector, 120))
			{
				return "El sector es obligatorio.";
			}
			if (Bad4(b.Municipio, 120))
			{
				return "El municipio es obligatorio.";
			}
			if (Bad4(b.Provincia, 120))
			{
				return "La provincia es obligatoria.";
			}
			if (b.TipoEstablecimiento != "Banca de lotería")
			{
				return "El establecimiento debe registrarse como banca de lotería.";
			}
			if ((b.Observations?.Length ?? 0) > 1000)
			{
				return "La observación supera el máximo de 1,000 caracteres.";
			}
			StructuralAnswers answers = b.Answers;
			string[] source = new string[2] { "yes", "no" };
			string[] source2 = new string[3] { "yes", "no", "not_applicable" };
			string[] cashBoxTypes = new string[3] { "cash_register", "wooden_box", "none" };
			if ((object)answers == null || !Enumerable.Contains(source, answers.Painted) || !Enumerable.Contains(source, answers.RazaSticker) || !Enumerable.Contains(source, answers.RealSticker) || !Enumerable.Contains(source, answers.LotekaRemoved) || !Enumerable.Contains(source, answers.DamageFound) || !Enumerable.Contains(source2, answers.PrinterMaintained) || !Enumerable.Contains(source2, answers.InverterPresent) || !Enumerable.Contains(source2, answers.BatteryPresent) || !Enumerable.Contains(cashBoxTypes, answers.CashBoxType))
			{
				return "Completa las nueve verificaciones del formulario.";
			}
			Geo geolocation = b.Geolocation;
			if ((object)geolocation == null || !FieldAuditGps.Valid(geolocation.Latitude, geolocation.Longitude, geolocation.AccuracyMeters))
			{
				return "La ubicación o la precisión GPS no es válida.";
			}
			if (geolocation.Source != "browser_geolocation")
			{
				return "La ubicación debe capturarse desde el dispositivo.";
			}
			if (!geolocation.CapturedAt.HasValue)
			{
				return "La evidencia de ubicación no tiene una fecha válida.";
			}
			double totalSeconds = (DateTimeOffset.UtcNow - geolocation.CapturedAt.Value).TotalSeconds;
			if (totalSeconds < -120.0)
			{
				return "La fecha de la ubicación está en el futuro. Vuelve a capturarla.";
			}
			return null;
		}
		static string? ValidateAgencyDirectoryImport(AgencyDirectoryImport body)
		{
			if (body.Agencies == null || body.Agencies.Count == 0)
			{
				return "El archivo no contiene agencias para importar.";
			}
			if (body.Agencies.Count > 5000)
			{
				return "El archivo supera el límite de 5,000 agencias por importación.";
			}
			if ((body.FileName?.Length ?? 0) > 200)
			{
				return "El nombre del archivo es demasiado largo.";
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			int num = 0;
			while (num < body.Agencies.Count)
			{
				AgencyDirectoryImportRow agencyDirectoryImportRow = body.Agencies[num];
				int value = ((agencyDirectoryImportRow.RowNumber > 1) ? agencyDirectoryImportRow.RowNumber : (num + 2));
				if (Bad2(agencyDirectoryImportRow.Codigo, 100) || Bad2(agencyDirectoryImportRow.Terminal, 300) || Bad2(agencyDirectoryImportRow.Grupo, 150))
				{
					return $"Fila {value}: completa Código, Terminal y Grupo.";
				}
				string text2 = agencyDirectoryImportRow.Codigo.Trim();
				if (dictionary.TryGetValue(text2, out var value2))
				{
					return $"Fila {value}: el código {text2} ya aparece en la fila {value2}.";
				}
				dictionary[text2] = value;
				if ((agencyDirectoryImportRow.Region?.Length ?? 0) > 120 || (agencyDirectoryImportRow.Direccion?.Length ?? 0) > 300 || (agencyDirectoryImportRow.Sector?.Length ?? 0) > 120 || (agencyDirectoryImportRow.Municipio?.Length ?? 0) > 120 || (agencyDirectoryImportRow.Provincia?.Length ?? 0) > 120)
				{
					return $"Fila {value}: uno de los campos opcionales supera el tamaño permitido.";
				}
				if (agencyDirectoryImportRow.Latitude.HasValue != agencyDirectoryImportRow.Longitude.HasValue)
				{
					return $"Fila {value}: completa juntas la latitud y la longitud.";
				}
				double? latitude = agencyDirectoryImportRow.Latitude;
				bool flag;
				if (latitude.HasValue)
				{
					double valueOrDefault = latitude.GetValueOrDefault();
					if (valueOrDefault < -90.0 || valueOrDefault > 90.0)
					{
						flag = true;
						goto IL_029a;
					}
				}
				flag = false;
				goto IL_029a;
				IL_029a:
				bool flag2 = flag;
				bool flag3 = flag2;
				bool flag4;
				if (!flag3)
				{
					double? longitude = agencyDirectoryImportRow.Longitude;
					if (longitude.HasValue)
					{
						double valueOrDefault2 = longitude.GetValueOrDefault();
						if (valueOrDefault2 < -180.0 || valueOrDefault2 > 180.0)
						{
							flag4 = true;
							goto IL_02e2;
						}
					}
					flag4 = false;
					goto IL_02e2;
				}
				goto IL_02e6;
				IL_02e6:
				if (flag3 || (agencyDirectoryImportRow.Latitude == 0.0 && agencyDirectoryImportRow.Longitude == 0.0))
				{
					return $"Fila {value}: las coordenadas no son válidas.";
				}
				num++;
				continue;
				IL_02e2:
				flag3 = flag4;
				goto IL_02e6;
			}
			return null;
		}
		static string? ValidateAgencyDirectoryUpdate(AgencyDirectoryUpdate body)
		{
			if (Bad(body.Codigo, 100) || Bad(body.Terminal, 300) || Bad(body.Grupo, 150))
			{
				return "Completa el código, la terminal y el grupo de la agencia.";
			}
			if ((body.Direccion?.Length ?? 0) > 300 || (body.Sector?.Length ?? 0) > 120 || (body.Municipio?.Length ?? 0) > 120 || (body.Provincia?.Length ?? 0) > 120)
			{
				return "La dirección de la agencia supera el tamaño permitido.";
			}
			if (body.Latitude.HasValue != body.Longitude.HasValue)
			{
				return "Completa juntas la latitud y la longitud.";
			}
			double? latitude = body.Latitude;
			bool flag;
			if (latitude.HasValue)
			{
				double valueOrDefault = latitude.GetValueOrDefault();
				if (valueOrDefault < -90.0 || valueOrDefault > 90.0)
				{
					flag = true;
					goto IL_00f4;
				}
			}
			flag = false;
			goto IL_00f4;
			IL_00f4:
			bool flag2 = flag;
			bool flag3 = flag2;
			bool flag4;
			if (!flag3)
			{
				double? longitude = body.Longitude;
				if (longitude.HasValue)
				{
					double valueOrDefault2 = longitude.GetValueOrDefault();
					if (valueOrDefault2 < -180.0 || valueOrDefault2 > 180.0)
					{
						flag4 = true;
						goto IL_0139;
					}
				}
				flag4 = false;
				goto IL_0139;
			}
			goto IL_013d;
			IL_0139:
			flag3 = flag4;
			goto IL_013d;
			IL_013d:
			if (flag3 || (body.Latitude == 0.0 && body.Longitude == 0.0))
			{
				return "Las coordenadas de la agencia no son válidas.";
			}
			return null;
		}
		static string? ValidateAuditUpdate(AuditUpdate b)
		{
			if (Bad3(b.Codigo, 100) || Bad3(b.Terminal, 300) || Bad3(b.Grupo, 150))
			{
				return "Completa el código, terminal y grupo.";
			}
			if (Bad3(b.EmployeeName, 160) || Bad3(b.Direccion, 300) || Bad3(b.Sector, 120) || Bad3(b.Municipio, 120) || Bad3(b.Provincia, 120))
			{
				return "Completa los datos del empleado y la dirección.";
			}
			if (!FieldAuditGps.Valid(b.Latitude, b.Longitude, b.Accuracy))
			{
				return "Las coordenadas o la precisión no son válidas.";
			}
			if ((b.Observations?.Length ?? 0) > 1000)
			{
				return "La observación supera 1,000 caracteres.";
			}
			StructuralAnswers answers = b.Answers;
			string[] source = new string[2] { "yes", "no" };
			string[] source2 = new string[3] { "yes", "no", "not_applicable" };
			string[] cashBoxTypes = new string[3] { "cash_register", "wooden_box", "none" };
			if ((object)answers == null || !Enumerable.Contains(source, answers.Painted) || !Enumerable.Contains(source, answers.RazaSticker) || !Enumerable.Contains(source, answers.RealSticker) || !Enumerable.Contains(source, answers.LotekaRemoved) || !Enumerable.Contains(source, answers.DamageFound) || !Enumerable.Contains(source2, answers.PrinterMaintained) || !Enumerable.Contains(source2, answers.InverterPresent) || !Enumerable.Contains(source2, answers.BatteryPresent) || answers.CashBoxType != null && !Enumerable.Contains(cashBoxTypes, answers.CashBoxType))
			{
				return "Completa las respuestas de la auditoría.";
			}
			return null;
		}
		static string? ValidateTicket(TicketCreate body)
		{
			string[] source = new string[4] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
			if (!body.AgencyId.HasValue)
			{
				return "Selecciona una agencia.";
			}
			bool flag;
			switch (body.TicketType)
			{
			case null:
			case "SUPPORT":
			case "INTERNAL":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				return "Selecciona un tipo de ticket válido.";
			}
			if (string.IsNullOrWhiteSpace(body.Category) || body.Category.Length > 60)
			{
				return "Selecciona una categoría válida.";
			}
			if (string.IsNullOrWhiteSpace(body.AssignedDepartment) || body.AssignedDepartment.Length > 40)
			{
				return "Selecciona un departamento válido.";
			}
			if (!Enumerable.Contains(source, body.Priority))
			{
				return "Selecciona una prioridad válida.";
			}
			if (string.IsNullOrWhiteSpace(body.Subject) || body.Subject.Trim().Length > 180)
			{
				return "Escribe un asunto de hasta 180 caracteres.";
			}
			if (string.IsNullOrWhiteSpace(body.Description) || body.Description.Trim().Length > 2000)
			{
				return "Describe la incidencia en un máximo de 2,000 caracteres.";
			}
			return null;
		}
		static string? ValidateTicketRoute(TicketRoute body)
		{
			if (string.IsNullOrWhiteSpace(body.AssignedDepartment) || body.AssignedDepartment.Length > 40)
			{
				return "Selecciona un departamento válido.";
			}
			bool flag;
			switch (body.AssignedTeam)
			{
			case null:
			case "CALL_CENTER":
			case "TECHNICAL_FAILURE":
			case "TECHNICIANS":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				return "Selecciona un equipo de Tecnología válido.";
			}
			if (body.AssignedDepartment != "TECHNOLOGY" && body.AssignedTeam != null)
			{
				return "Los equipos Call Center, Avería Técnica y Técnicos solo pertenecen a Tecnología.";
			}
			if ((body.Comment?.Length ?? 0) > 1000)
			{
				return "El comentario supera 1,000 caracteres.";
			}
			return null;
		}
		static string? ValidateTicketUpdate(TicketUpdate body)
		{
			string[] source = new string[6] { "OPEN", "IN_PROGRESS", "PENDING", "RESOLVED", "CLOSED", "CANCELLED" };
			string[] source2 = new string[4] { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
			if (!Enumerable.Contains(source, body.Status))
			{
				return "Selecciona un estado válido.";
			}
			if (!Enumerable.Contains(source2, body.Priority))
			{
				return "Selecciona una prioridad válida.";
			}
			if ((body.Resolution?.Length ?? 0) > 2000)
			{
				return "La solución o motivo supera 2,000 caracteres.";
			}
			bool flag;
			switch (body.Status)
			{
			case "PENDING":
			case "RESOLVED":
			case "CLOSED":
			case "CANCELLED":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag && string.IsNullOrWhiteSpace(body.Resolution))
			{
				if (!(body.Status == "PENDING"))
				{
					if (!(body.Status == "CANCELLED"))
					{
						return "Escribe la solución antes de resolver o cerrar el ticket.";
					}
					return "Escribe el motivo de la anulación.";
				}
				return "Escribe el motivo por el que el ticket quedará pendiente.";
			}
			string status = body.Status;
			flag = ((status == "PENDING" || status == "CANCELLED") ? true : false);
			if (flag && body.Resolution.Trim().Length < 5)
			{
				return "El motivo debe tener al menos 5 caracteres.";
			}
			return null;
		}
		static string? ValidateUser(string? email, string? displayName, string? role, string? password, bool passwordRequired)
		{
			string input = (email ?? "").Trim().ToLowerInvariant().Replace("@grupotejeda.local", "");
			if (!Regex.IsMatch(input, "^[a-z0-9]+\\.[a-z0-9]+$"))
			{
				return "El usuario debe usar el formato nombre.apellido.";
			}
			if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 160)
			{
				return "Escribe el nombre del usuario.";
			}
			if (role != "ADMINISTRATOR" && role != "ADMINISTRATIVE" && role != "VIEWER" && role != "GROUP_ADMIN" && role != "FISCALIZADOR" && role != "TECHNOLOGY" && role != "GENERAL_SERVICES" && role != "HUMAN_RESOURCES" && role != "WAREHOUSE" && role != "DEPARTMENT")
			{
				return "Selecciona un rol válido.";
			}
			if (passwordRequired && string.IsNullOrWhiteSpace(password))
			{
				return "La contraseña es obligatoria.";
			}
			if (!string.IsNullOrEmpty(password) && password != "123456" && password.Length < 8)
			{
				return "La nueva contraseña debe tener al menos 8 caracteres.";
			}
			if ((password?.Length ?? 0) > 128)
			{
				return "La contraseña es demasiado larga.";
			}
			return null;
		}
		static string? ValidateUserWithTeam(string? email, string? displayName, string? role, string? password, bool passwordRequired, string? supportTeam)
		{
			if (role == "DEPARTMENT")
			{
				if (DepartmentAccounts.Valid(supportTeam))
				{
					return ValidateUser(email, displayName, role, password, passwordRequired);
				}
				return "Selecciona un departamento válido.";
			}
			if (!string.IsNullOrWhiteSpace(supportTeam))
			{
				bool flag = role == "TECHNOLOGY";
				bool flag2 = flag;
				if (flag2)
				{
					bool flag3;
					switch (supportTeam)
					{
					case "CALL_CENTER":
					case "TECHNICAL_FAILURE":
					case "TECHNICIANS":
					case "WAREHOUSE":
					case "WORKSHOP":
						flag3 = true;
						break;
					default:
						flag3 = false;
						break;
					}
					flag2 = flag3;
				}
				if (!flag2 && (!(role == "GENERAL_SERVICES") || !(supportTeam == "WORKSHOP")) && (!(role == "WAREHOUSE") || !(supportTeam == "WAREHOUSE")))
				{
					return "El área asignada no corresponde con el departamento del usuario.";
				}
			}
			return ValidateUser(email, displayName, role, password, passwordRequired);
		}
		static AdminUserRow WithEffectivePermissions(AdminUserRow user)
		{
			string role = user.Role switch
			{
				"ADMINISTRATOR" => "Administrator", 
				"ADMINISTRATIVE" => "Administrative", 
				"GROUP_ADMIN" => "GroupAdministrator", 
				"FISCALIZADOR" => "Fiscalizador", 
				"TECHNOLOGY" => "Technology", 
				"GENERAL_SERVICES" => "GeneralServices", 
				"HUMAN_RESOURCES" => "HumanResources", 
				"WAREHOUSE" => "Warehouse", 
				"DEPARTMENT" => "Department", 
				_ => "Viewer", 
			};
			return user with
			{
				EffectivePermissions = AccountPermissions(new AdminIdentity(user.Id.ToString(), user.Email, user.DisplayName, role, user.MustChangePassword, user.AvatarUrl, user.Permissions, user.SupportTeam))
			};
		}
	}
}
