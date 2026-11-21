using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

internal sealed partial class Database
{
	private sealed record BundledAgencyLocation(string Codigo, string? Direccion, string? Sector, string? Municipio, string? Provincia, double Latitude, double Longitude);

	private sealed record TicketWorkflowState(string AgencyGroup, string Department, string? Team, string TicketType, string Status, string Priority, string? TechnicianId, string CreatedByUserId, double? AgencyLatitude, double? AgencyLongitude);

	private static readonly SemaphoreSlim AdministrativeAssetSchemaLock = new SemaphoreSlim(1, 1);

	private static bool administrativeAssetSchemaReady;

	private static readonly SemaphoreSlim AdministrativeFolderSchemaLock = new SemaphoreSlim(1, 1);

	private static bool administrativeFolderSchemaReady;

	private const string AgencyLocationSeedId = "SONADORA-2026-09-07";

	private static readonly SemaphoreSlim DocumentSchemaLock = new SemaphoreSlim(1, 1);

	private static bool documentSchemaReady;

	private static readonly SemaphoreSlim TemplateSchemaLock = new SemaphoreSlim(1, 1);

	private static bool templateSchemaReady;

	private static readonly SemaphoreSlim PersistentSessionSchemaLock = new SemaphoreSlim(1, 1);

	private static bool persistentSessionSchemaReady;

	private readonly List<(string Value, string Mode)> connections = new List<(string, string)>();

	private readonly string? configurationError;

	private static readonly JsonSerializerOptions StorageJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private static readonly SemaphoreSlim SupportChatSchemaLock = new SemaphoreSlim(1, 1);

	private static bool supportChatSchemaReady;

	private static readonly SemaphoreSlim UserPermissionSchemaLock = new SemaphoreSlim(1, 1);

	private static bool userPermissionSchemaReady;

	private static readonly SemaphoreSlim FindingEvidenceSchemaLock = new SemaphoreSlim(1, 1);

	private static bool findingEvidenceSchemaReady;

	private static readonly SemaphoreSlim TicketWorkflowSchemaLock = new SemaphoreSlim(1, 1);

	private static bool ticketWorkflowSchemaReady;

	private static readonly SemaphoreSlim NotificationReceiptSchemaLock = new SemaphoreSlim(1, 1);

	private static bool notificationReceiptSchemaReady;

	private static readonly SemaphoreSlim MaintenanceSchemaLock = new SemaphoreSlim(1, 1);

	private static bool maintenanceSchemaReady;

	private static readonly SemaphoreSlim AgencyTransitionEvidenceSchemaLock = new SemaphoreSlim(1, 1);

	private static bool agencyTransitionEvidenceSchemaReady;

	private static readonly SemaphoreSlim AgencyDirectorySchemaLock = new SemaphoreSlim(1, 1);

	private static bool agencyDirectorySchemaReady;

	private static readonly SemaphoreSlim UserAccessPresetSchemaLock = new SemaphoreSlim(1, 1);

	private static bool userAccessPresetSchemaReady;

	private static readonly HashSet<string> WarehouseConsumables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Papel de baño", "Vasos", "Cucharas", "Marcadores", "Lapiceros", "Liquipapers", "Folders", "Hojas blancas", "Grapas", "Clips",
		"Cinta adhesiva", "Servilletas", "Bolsas de basura"
	};

	private static readonly SemaphoreSlim WarehouseFulfillmentSchemaLock = new SemaphoreSlim(1, 1);

	private static bool warehouseFulfillmentSchemaReady;

	private static readonly SemaphoreSlim WarehouseAnalyticsLock = new SemaphoreSlim(1, 1);

	private static object warehouseAnalyticsCache = WarehouseEmptyAnalytics();

	private static DateTime warehouseAnalyticsCachedUtc;

	internal const string WarehouseCleanupKey = "V418_20260928_KEEP_PENDING_WAREHOUSE_REQUESTS";

	private static bool namedCallCenterCorrectionReady;

	private static readonly JsonSerializerOptions InstitutionalJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private static readonly string[] InstitutionalDepartments = DepartmentAccounts.Names.Values.Distinct().ToArray();

	private static readonly SemaphoreSlim InstitutionalSchemaLock = new SemaphoreSlim(1, 1);

	private static bool institutionalSchemaReady;

	private static readonly SemaphoreSlim RecoverySchemaLock = new SemaphoreSlim(1, 1);

	private static bool recoverySchemaReady;

	private static readonly SemaphoreSlim SupportStandardLock = new SemaphoreSlim(1, 1);

	private static Dictionary<string, bool>? supportStandard;

	private static readonly SemaphoreSlim RepairedInventorySchemaLock = new SemaphoreSlim(1, 1);

	private static readonly SemaphoreSlim WorkshopSchemaLock = new SemaphoreSlim(1, 1);

	private static bool workshopSchemaReady;

	private const string WorkshopAccess = "(@admin=1 OR @operator=1 OR m.created_by_user_id=@user OR m.technician_user_id=@user\r\n  OR (@warehouse=1 AND (m.operational_area='WAREHOUSE' OR c.status='DELIVERED'))\r\n  OR (@technologyManager=1 AND m.department='TECHNOLOGY')\r\n  OR m.department=@department\r\n  OR (@team='WORKSHOP' AND m.department=@department))";

	public static Dictionary<string, bool>? SupportStandard
	{
		get
		{
			if (supportStandard != null)
			{
				return new Dictionary<string, bool>(supportStandard);
			}
			return null;
		}
	}

	private static bool PasswordChangeValid(string password)
	{
		int length = password.Length;
		if (length >= 8 && length <= 128)
		{
			return password != "123456";
		}
		return false;
	}

	private static async Task DisableLegacyPasswordAccess(SqlConnection connection, SqlTransaction transaction, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("UPDATE dbo.account_recovery SET session_version=@version WHERE user_id=N'legacy-admin'; IF @@ROWCOUNT=0 INSERT dbo.account_recovery(user_id,session_version) VALUES(N'legacy-admin',@version);", connection, transaction);
		command.Parameters.AddWithValue("@version", Guid.NewGuid().ToString());
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task<string> RotatePasswordSessions(SqlConnection connection, SqlTransaction transaction, string userId, CancellationToken ct)
	{
		string version = Guid.NewGuid().ToString();
		string result;
		await using (SqlCommand command = new SqlCommand("UPDATE dbo.account_recovery SET session_version=@version,code_hash=NULL,used_at=SYSUTCDATETIME() WHERE user_id=@id;\nIF @@ROWCOUNT=0 INSERT dbo.account_recovery(user_id,session_version) VALUES(@id,@version);\nIF OBJECT_ID(N'dbo.persistent_login_tokens',N'U') IS NOT NULL\n    UPDATE dbo.persistent_login_tokens SET revoked_at=SYSUTCDATETIME() WHERE user_id=@id AND revoked_at IS NULL;", connection, transaction))
		{
			command.Parameters.AddWithValue("@id", userId);
			command.Parameters.AddWithValue("@version", version);
			await command.ExecuteNonQueryAsync(ct);
			result = version;
		}
		return result;
	}

	public async Task<bool> LegacyPasswordAccessEnabled(CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureRecoverySchema(connection, ct);
			bool flag;
			await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.account_recovery WHERE user_id=N'legacy-admin' AND session_version<>N''", connection))
			{
				flag = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0;
			}
			result = flag;
		}
		return result;
	}

	public async Task<AdminIdentity?> ChangeLegacyPassword(string email, string currentPassword, string newPassword, CancellationToken ct)
	{
		string configuredEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim().ToLowerInvariant();
		string environmentVariable = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
		if (!PasswordChangeValid(newPassword) || string.IsNullOrWhiteSpace(configuredEmail) || string.IsNullOrEmpty(environmentVariable) || !string.Equals(email, configuredEmail, StringComparison.OrdinalIgnoreCase) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(currentPassword)), SHA256.HashData(Encoding.UTF8.GetBytes(environmentVariable))))
		{
			return null;
		}
		AdminIdentity result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			await EnsureRecoverySchema(connection, ct);
			AdminIdentity adminIdentity3;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string id;
				AdminIdentity adminIdentity2;
				await using (SqlCommand read = new SqlCommand("SELECT CONVERT(nvarchar(80),id),role,is_active FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE email=@email", connection, transaction))
				{
					read.Parameters.AddWithValue("@email", configuredEmail);
					AdminIdentity adminIdentity;
					await using (SqlDataReader reader = await read.ExecuteReaderAsync(ct))
					{
						if (await reader.ReadAsync(ct))
						{
							if (reader.GetString(1) != "ADMINISTRATOR" || !reader.GetBoolean(2))
							{
								adminIdentity = null;
								goto IL_05c2;
							}
							id = reader.GetString(0);
						}
						else
						{
							id = Guid.NewGuid().ToString();
						}
					}
					goto end_IL_0399;
					IL_05c2:
					adminIdentity2 = adminIdentity;
					goto IL_06a0;
					end_IL_0399:;
				}
				adminIdentity2 = null;
				await using (SqlCommand read = new SqlCommand("SELECT session_version FROM dbo.account_recovery WITH(UPDLOCK,HOLDLOCK) WHERE user_id=N'legacy-admin'", connection, transaction))
				{
					if (await read.ExecuteScalarAsync(ct) is string { Length: >0 })
					{
						adminIdentity2 = null;
						goto IL_0840;
					}
				}
				byte[] bytes = RandomNumberGenerator.GetBytes(16);
				await using (SqlCommand read = new SqlCommand("UPDATE dbo.admin_users SET password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=0,updated_at=SYSUTCDATETIME() WHERE id=@id;\nIF @@ROWCOUNT=0 INSERT dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password)\n    VALUES(@id,@email,N'Administrador principal','ADMINISTRATOR',@salt,@hash,210000,1,0);", connection, transaction))
				{
					read.Parameters.AddWithValue("@id", id);
					read.Parameters.AddWithValue("@email", configuredEmail);
					read.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = bytes;
					read.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = PasswordHash(newPassword, bytes, 210000);
					await read.ExecuteNonQueryAsync(ct);
				}
				await RotatePasswordSessions(connection, transaction, id, ct);
				await DisableLegacyPasswordAccess(connection, transaction, ct);
				await transaction.CommitAsync(ct);
				adminIdentity3 = await Account(Guid.Parse(id), ct);
				goto end_IL_035a;
				IL_06a0:
				adminIdentity3 = adminIdentity2;
				goto end_IL_035a;
				IL_0840:
				adminIdentity3 = adminIdentity2;
				end_IL_035a:;
			}
			result = adminIdentity3;
		}
		return result;
	}

	private static async Task EnsureAdministrativeAssetSchema(SqlConnection c, CancellationToken ct)
	{
		if (administrativeAssetSchemaReady)
		{
			return;
		}
		await AdministrativeAssetSchemaLock.WaitAsync(ct);
		try
		{
			if (!administrativeAssetSchemaReady)
			{
				await using SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.administrative_assets',N'U') IS NULL\r\nBEGIN\r\n  CREATE TABLE dbo.administrative_assets(id uniqueidentifier NOT NULL PRIMARY KEY,kind varchar(12) NOT NULL,label nvarchar(160) NOT NULL,identifier nvarchar(100) NOT NULL,status varchar(20) NOT NULL,holder_type varchar(12) NULL,holder_name nvarchar(160) NULL,agency_code nvarchar(40) NULL,notes nvarchar(2000) NULL,created_utc datetime2 NOT NULL,updated_utc datetime2 NOT NULL,request_id bigint NULL);\r\n  CREATE UNIQUE INDEX UX_administrative_assets_identifier ON dbo.administrative_assets(identifier);\r\n  CREATE INDEX IX_administrative_assets_kind_status ON dbo.administrative_assets(kind,status);\r\nEND\r\nIF COL_LENGTH(N'dbo.administrative_assets',N'department') IS NULL\r\n  EXEC(N'ALTER TABLE dbo.administrative_assets ADD department nvarchar(80) NOT NULL CONSTRAINT DF_administrative_asset_department DEFAULT N''Administración'';');", c);
				await cmd.ExecuteNonQueryAsync(ct);
				administrativeAssetSchemaReady = true;
			}
		}
		finally
		{
			AdministrativeAssetSchemaLock.Release();
		}
	}

	private static AdministrativeAsset ReadAdministrativeAsset(SqlDataReader r)
	{
		return new AdministrativeAsset(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetDateTime(9), r.GetDateTime(10), r.IsDBNull(11) ? ((long?)null) : new long?(r.GetInt64(11)));
	}

	public async Task<List<AdministrativeAsset>> AdministrativeAssets(CancellationToken ct, string department = "Administración")
	{
		List<AdministrativeAsset> result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeAssetSchema(c, ct);
			List<AdministrativeAsset> list2;
			await using (SqlCommand cmd = new SqlCommand("SELECT id,kind,label,identifier,status,holder_type,holder_name,agency_code,notes,created_utc,updated_utc,request_id FROM dbo.administrative_assets WHERE department=@department ORDER BY kind,label", c))
			{
				cmd.Parameters.AddWithValue("@department", department);
				List<AdministrativeAsset> rows = new List<AdministrativeAsset>();
				List<AdministrativeAsset> list;
				await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
				{
					while (await r.ReadAsync(ct))
					{
						rows.Add(ReadAdministrativeAsset(r));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<AdministrativeAsset?> SaveAdministrativeAsset(Guid? id, AdministrativeAssetInput input, CancellationToken ct, string department = "Administración")
	{
		AdministrativeAsset result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeAssetSchema(c, ct);
			Guid key = id ?? Guid.NewGuid();
			AdministrativeAsset administrativeAsset;
			await using (SqlCommand cmd = new SqlCommand((!id.HasValue) ? "INSERT INTO dbo.administrative_assets(id,kind,label,identifier,status,holder_type,holder_name,agency_code,notes,created_utc,updated_utc,department) VALUES(@id,@kind,@label,@identifier,@status,@holderType,@holderName,@agencyCode,@notes,SYSUTCDATETIME(),SYSUTCDATETIME(),@department)" : "UPDATE dbo.administrative_assets SET kind=@kind,label=@label,identifier=@identifier,status=@status,holder_type=@holderType,holder_name=@holderName,agency_code=@agencyCode,notes=@notes,updated_utc=SYSUTCDATETIME() WHERE id=@id AND department=@department", c))
			{
				cmd.Parameters.AddWithValue("@department", department);
				cmd.Parameters.AddWithValue("@id", key);
				cmd.Parameters.AddWithValue("@kind", input.Kind);
				cmd.Parameters.AddWithValue("@label", input.Label.Trim());
				cmd.Parameters.AddWithValue("@identifier", input.Identifier.Trim());
				cmd.Parameters.AddWithValue("@status", input.Status);
				cmd.Parameters.AddWithValue("@holderType", ((object)input.HolderType) ?? ((object)DBNull.Value));
				cmd.Parameters.AddWithValue("@holderName", ((object)input.HolderName?.Trim()) ?? ((object)DBNull.Value));
				cmd.Parameters.AddWithValue("@agencyCode", ((object)input.AgencyCode?.Trim()) ?? ((object)DBNull.Value));
				cmd.Parameters.AddWithValue("@notes", ((object)input.Notes?.Trim()) ?? ((object)DBNull.Value));
				try
				{
					if (await cmd.ExecuteNonQueryAsync(ct) != 0)
					{
						goto end_IL_0356;
					}
					administrativeAsset = null;
					goto end_IL_01ba;
					end_IL_0356:;
				}
				catch (SqlException ex) when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					int number = ex.Number;
					return (number == 2601 || number == 2627) ? true : false;
				}).Invoke())
				{
					administrativeAsset = null;
					goto end_IL_01ba;
				}
				AdministrativeAsset administrativeAsset3;
				await using (SqlCommand read = new SqlCommand("SELECT id,kind,label,identifier,status,holder_type,holder_name,agency_code,notes,created_utc,updated_utc,request_id FROM dbo.administrative_assets WHERE id=@id", c))
				{
					read.Parameters.AddWithValue("@id", key);
					AdministrativeAsset administrativeAsset2;
					await using (SqlDataReader r = await read.ExecuteReaderAsync(ct))
					{
						administrativeAsset2 = ((await r.ReadAsync(ct)) ? ReadAdministrativeAsset(r) : null);
					}
					administrativeAsset3 = administrativeAsset2;
				}
				administrativeAsset = administrativeAsset3;
				end_IL_01ba:;
			}
			result = administrativeAsset;
		}
		return result;
	}

	public async Task<string?> DeliverAdministrativeRequest(long id, DocumentActor actor, AdministrativeDeliveryInput input, CancellationToken ct)
	{
		string role = actor.Role;
		if ((!(role == "Administrator") && !(role == "Administrative")) || 1 == 0)
		{
			return "Solo Administración puede registrar esta entrega.";
		}
		string token = (string.IsNullOrWhiteSpace(input.SealQr) ? null : WarehouseQrToken(input.SealQr));
		string number = AdministrativeDeliveryQr.RequestNumber(input.SealQr);
		if (token == null && number == null)
		{
			return "Escanea completo el QR del formulario o del sello AUTORIZADO A RETIRAR de esta solicitud.";
		}
		bool flag = string.IsNullOrWhiteSpace(input.ReceivedBy);
		bool flag2 = flag;
		if (!flag2)
		{
			int length = input.ReceivedBy.Trim().Length;
			bool flag3 = ((length < 3 || length > 160) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return "Indica quién recibe el activo o documento.";
		}
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			await EnsureAdministrativeAssetSchema(c, ct);
			string text4;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text = null;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", c, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text = "Solicitud no disponible.";
					}
					else
					{
						InstitutionalFile d = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						d.Id = id;
						if (!d.Accessible(actor))
						{
							text = "No tienes acceso a este formulario.";
						}
						else if ((object)d.AdministrativeRequest == null || d.RecipientDepartment != "Administración" || d.Channel != "requirements" || d.Status != "approved" || (object)d.AdministrativeDelivery != null || d.Trashed)
						{
							text = "Autoriza primero la solicitud administrativa pendiente de entrega.";
						}
						else if (d.AdministrativeRequest.FormType == "INCIDENT_REPORT")
						{
							text = "Los informes de incidentes no requieren entrega de activos.";
						}
						else if ((token == null || !string.Equals(token, d.VerificationToken, StringComparison.OrdinalIgnoreCase)) && (number == null || !string.Equals(number, d.AdministrativeRequest.Number, StringComparison.Ordinal)))
						{
							text = "El QR escaneado no corresponde a esta solicitud. Escanea su sello AUTORIZADO A RETIRAR.";
						}
						else
						{
							InternetRequestInfo internetRequestInfo = d.AdministrativeRequest?.Internet;
							role = d.AdministrativeRequest?.AssetType;
							bool flag3 = ((role == "FLOTA" || role == "MODEM") ? true : false);
							string requiredKind;
							if (flag3)
							{
								requiredKind = d.AdministrativeRequest.AssetType;
							}
							else
							{
								requiredKind = ((internetRequestInfo?.ItemKind == "EQUIPO") ? "MODEM" : null);
							}
							int num = ((requiredKind != null) ? ((!(internetRequestInfo?.ItemKind == "EQUIPO")) ? 1 : internetRequestInfo.ModemQuantity) : 0);
							List<Guid>? first = input.AssetIds ?? new List<Guid>();
							Guid? assetId = input.AssetId;
							IEnumerable<Guid> second;
							if (assetId.HasValue)
							{
								Guid valueOrDefault = assetId.GetValueOrDefault();
								IEnumerable<Guid> enumerable = new _003C_003Ez__ReadOnlySingleElementList<Guid>(valueOrDefault);
								second = enumerable;
							}
							else
							{
								IEnumerable<Guid> enumerable = Array.Empty<Guid>();
								second = enumerable;
							}
							List<Guid> requestedIds = first.Concat(second).ToList();
							if (requestedIds.Count != num || requestedIds.Distinct().Count() != requestedIds.Count)
							{
								text = ((num == 0) ? "Esta solicitud no requiere activos de inventario." : $"Selecciona exactamente {num} unidad(es) disponible(s) del inventario.");
							}
							else
							{
								List<string> identifiers = new List<string>();
								string status = null;
								foreach (Guid assetId2 in requestedIds.OrderBy((Guid value) => value))
								{
									await using (SqlCommand asset = new SqlCommand("SELECT kind,identifier,status FROM dbo.administrative_assets WITH(UPDLOCK,HOLDLOCK) WHERE id=@asset AND department=N'Administración'", c, tx))
									{
										asset.Parameters.AddWithValue("@asset", assetId2);
										await using (SqlDataReader r = await asset.ExecuteReaderAsync(ct))
										{
											if (!(await r.ReadAsync(ct)))
											{
												text = "Uno de los activos no existe.";
												break;
											}
											string kind = r.GetString(0);
											string identifier = r.GetString(1);
											status = r.GetString(2);
											await r.DisposeAsync();
											if (kind != requiredKind || status != "DISPONIBLE")
											{
												text = "Selecciona activos disponibles del tipo solicitado.";
												break;
											}
											identifiers.Add(identifier);
											await using SqlCommand assign = new SqlCommand("UPDATE dbo.administrative_assets SET status='ASIGNADO',holder_type=@type,holder_name=@name,agency_code=@agency,request_id=@request,updated_utc=SYSUTCDATETIME() WHERE id=@asset AND department=N'Administración'", c, tx);
											assign.Parameters.AddWithValue("@type", d.AdministrativeRequest.TargetType);
											assign.Parameters.AddWithValue("@name", d.AdministrativeRequest.TargetName);
											assign.Parameters.AddWithValue("@agency", ((object)d.AdministrativeRequest.AgencyCode) ?? ((object)DBNull.Value));
											assign.Parameters.AddWithValue("@request", id);
											assign.Parameters.AddWithValue("@asset", assetId2);
											await assign.ExecuteNonQueryAsync(ct);
										}
									}
									if (text != null) break;
								}
								if (text != null) goto end_IL_03e7;
								DateTime utcNow = DateTime.UtcNow;
								d.AdministrativeDelivery = new AdministrativeDeliveryInfo(d.AdministrativeRequest?.Number ?? $"ADM-{id:D6}", actor.Name, actor.Login, input.ReceivedBy.Trim(), utcNow, (requestedIds.FirstOrDefault() == Guid.Empty) ? ((Guid?)null) : new Guid?(requestedIds.FirstOrDefault()), identifiers.FirstOrDefault(), identifiers);
								d.History.Add(new InstitutionalEvent("deliver", actor.Name, actor.Login, utcNow, d.Status, d.ReceiptCode, d.ReceiverLogin, d.ReceivedUtc, null));
								await using (SqlCommand save = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", c, tx))
								{
									save.Parameters.AddWithValue("@id", id);
									save.Parameters.AddWithValue("@json", JsonSerializer.Serialize(d, InstitutionalJson));
									await save.ExecuteNonQueryAsync(ct);
									await tx.CommitAsync(ct);
									status = null;
								}
								text = status;
							}
						}
					}
					end_IL_03e7:;
				}
				text4 = text;
			}
			result = text4;
		}
		return result;
	}

	private static bool SameFolderUser(string? left, string? right)
	{
		return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private static async Task EnsureAdministrativeFolderSchema(SqlConnection connection, CancellationToken ct)
	{
		if (administrativeFolderSchemaReady)
		{
			return;
		}
		await AdministrativeFolderSchemaLock.WaitAsync(ct);
		try
		{
			if (!administrativeFolderSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.administrative_document_folders', N'U') IS NULL\r\nBEGIN\r\n    EXEC(N'CREATE TABLE dbo.administrative_document_folders (\r\n        id uniqueidentifier NOT NULL PRIMARY KEY,\r\n        name nvarchar(120) NOT NULL,\r\n        parent_id uniqueidentifier NULL REFERENCES dbo.administrative_document_folders(id),\r\n        department nvarchar(80) NOT NULL,\r\n        is_shared bit NOT NULL DEFAULT 0,\r\n        created_by nvarchar(80) NOT NULL,\r\n        created_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()\r\n    );\r\n    CREATE INDEX IX_administrative_document_folders_department_parent\r\n    ON dbo.administrative_document_folders(department,parent_id,name);');\r\nEND\r\nELSE\r\nBEGIN\r\n    IF COL_LENGTH(N'dbo.administrative_document_folders', N'department') IS NULL\r\n        EXEC(N'ALTER TABLE dbo.administrative_document_folders ADD department nvarchar(80) NULL;');\r\n\r\n    IF COL_LENGTH(N'dbo.administrative_document_folders', N'is_shared') IS NULL\r\n        EXEC(N'ALTER TABLE dbo.administrative_document_folders ADD is_shared bit NOT NULL CONSTRAINT DF_administrative_document_folders_is_shared DEFAULT 0;');\r\n\r\n    IF EXISTS (\r\n        SELECT 1 FROM sys.columns\r\n        WHERE object_id=OBJECT_ID(N'dbo.administrative_document_folders')\r\n          AND name=N'department' AND is_nullable=1\r\n    )\r\n    BEGIN\r\n        EXEC(N'UPDATE dbo.administrative_document_folders SET department=N''Administración'' WHERE department IS NULL;');\r\n        EXEC(N'ALTER TABLE dbo.administrative_document_folders ALTER COLUMN department nvarchar(80) NOT NULL;');\r\n    END;\r\n\r\n    IF NOT EXISTS (\r\n        SELECT 1 FROM sys.indexes\r\n        WHERE object_id=OBJECT_ID(N'dbo.administrative_document_folders')\r\n          AND name=N'IX_administrative_document_folders_department_parent'\r\n    )\r\n        EXEC(N'CREATE INDEX IX_administrative_document_folders_department_parent ON dbo.administrative_document_folders(department,parent_id,name);');\r\nEND;\r\nIF COL_LENGTH(N'dbo.administrative_document_folders',N'shared_user_ids') IS NULL\r\n    EXEC(N'ALTER TABLE dbo.administrative_document_folders ADD shared_user_ids nvarchar(max) NOT NULL CONSTRAINT DF_folder_shared_users DEFAULT N''[]'' CHECK(ISJSON(shared_user_ids)=1);');", connection);
				await command.ExecuteNonQueryAsync(ct);
				administrativeFolderSchemaReady = true;
			}
		}
		finally
		{
			AdministrativeFolderSchemaLock.Release();
		}
	}

	private static async Task<List<AdministrativeFolder>> LoadAdministrativeFolders(SqlConnection c, string? department, CancellationToken ct, SqlTransaction? tx = null)
	{
		List<AdministrativeFolder> rows = new List<AdministrativeFolder>();
		List<AdministrativeFolder> result;
		await using (SqlCommand cmd = new SqlCommand("SELECT id,name,parent_id,department,is_shared,created_by,created_at,shared_user_ids FROM dbo.administrative_document_folders " + ((tx == null) ? "" : "WITH(UPDLOCK,HOLDLOCK) ") + ((department == null) ? "" : "WHERE department=@dept ") + "ORDER BY name", c, tx))
		{
			if (department != null)
			{
				cmd.Parameters.AddWithValue("@dept", department);
			}
			List<AdministrativeFolder> list;
			await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
			{
				while (await r.ReadAsync(ct))
				{
					rows.Add(new AdministrativeFolder(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? ((Guid?)null) : new Guid?(r.GetGuid(2)), r.GetString(3), r.GetBoolean(4), r.GetString(5), DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc))
					{
						SharedUserIds = (JsonSerializer.Deserialize<List<string>>(r.GetString(7)) ?? new List<string>())
					});
				}
				list = rows;
			}
			result = list;
		}
		return result;
	}

	private static AdministrativeFolder? FolderRoot(Guid id, List<AdministrativeFolder> folders)
	{
		HashSet<Guid> hashSet = new HashSet<Guid>();
		AdministrativeFolder administrativeFolder = folders.Find((AdministrativeFolder f) => f.Id == id);
		while ((object)administrativeFolder != null && hashSet.Add(administrativeFolder.Id))
		{
			Guid? parentId = administrativeFolder.ParentId;
			if (parentId.HasValue)
			{
				Guid parent = parentId.GetValueOrDefault();
				administrativeFolder = folders.Find((AdministrativeFolder f) => f.Id == parent);
				continue;
			}
			return administrativeFolder;
		}
		return null;
	}

	private static bool FolderReadable(AdministrativeFolder folder, List<AdministrativeFolder> folders, DocumentActor actor)
	{
		AdministrativeFolder administrativeFolder = FolderRoot(folder.Id, folders);
		if (actor.Allowed && (object)administrativeFolder != null)
		{
			if (!SameFolderUser(administrativeFolder.CreatedBy, actor.Id))
			{
				return administrativeFolder.SharedUserIds.Any((string id) => SameFolderUser(id, actor.Id));
			}
			return true;
		}
		return false;
	}

	private static bool FolderWritable(AdministrativeFolder folder, List<AdministrativeFolder> folders, DocumentActor actor)
	{
		return FolderReadable(folder, folders, actor);
	}

	public async Task<List<AdministrativeFolder>> AdministrativeFolders(DocumentActor actor, CancellationToken ct)
	{
		List<AdministrativeFolder> result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			List<AdministrativeFolder> rows = await LoadAdministrativeFolders(c, null, ct);
			result = (from f in rows
				where FolderReadable(f, rows, actor)
				select f with
				{
					IsShared = (FolderRoot(f.Id, rows).SharedUserIds.Count > 0),
					SharedUserIds = FolderRoot(f.Id, rows).SharedUserIds.ToList(),
					CanManage = SameFolderUser(FolderRoot(f.Id, rows)?.CreatedBy, actor.Id),
					CanWrite = FolderWritable(f, rows, actor)
				}).ToList();
		}
		return result;
	}

	private static async Task ApplyAdministrativeFolderAccess(SqlConnection c, IEnumerable<InstitutionalFile> files, DocumentActor actor, CancellationToken ct, SqlTransaction? tx = null)
	{
		List<InstitutionalFile> scoped = files.Where((InstitutionalFile f) => f.Area == "files" && f.FolderId.HasValue).ToList();
		if (scoped.Count == 0)
		{
			return;
		}
		if (tx == null)
		{
			await EnsureAdministrativeFolderSchema(c, ct);
		}
		List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
		foreach (InstitutionalFile file in scoped)
		{
			AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) =>
			{
				Guid id = f.Id;
				Guid? folderId = file.FolderId;
				return id == folderId;
			});
			file.FolderReadAllowed = (object)administrativeFolder != null && FolderReadable(administrativeFolder, list, actor);
			file.FolderWriteAllowed = (object)administrativeFolder != null && FolderWritable(administrativeFolder, list, actor);
		}
	}

	public async Task<bool> AdministrativeFolderWritable(Guid id, DocumentActor actor, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct);
			AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == id);
			result = (object)administrativeFolder != null && FolderWritable(administrativeFolder, list, actor);
		}
		return result;
	}

	public async Task<string?> AdministrativeFolderDepartment(Guid id, DocumentActor actor, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct);
			AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == id);
			result = (((object)administrativeFolder != null && FolderWritable(administrativeFolder, list, actor)) ? administrativeFolder.Department : null);
		}
		return result;
	}

	public async Task<AdministrativeFolder?> CreateAdministrativeFolder(string name, Guid? parentId, DocumentActor actor, List<string>? userIds, CancellationToken ct)
	{
		AdministrativeFolder result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			AdministrativeFolder administrativeFolder2;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
				List<string> users = (userIds ?? new List<string>()).Where((string left) => !SameFolderUser(left, actor.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				string targetDepartment = actor.Department;
				if (!parentId.HasValue)
				{
					goto IL_0374;
				}
				Guid parent = parentId.GetValueOrDefault();
				AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == parent);
				if ((object)administrativeFolder != null && FolderWritable(administrativeFolder, list, actor))
				{
					users = FolderRoot(parent, list).SharedUserIds.ToList();
					targetDepartment = administrativeFolder.Department;
					goto IL_0374;
				}
				administrativeFolder2 = null;
				goto end_IL_021d;
				IL_0374:
				bool flag = !parentId.HasValue;
				bool flag2 = flag;
				if (flag2)
				{
					flag2 = !(await ValidFolderRecipients(c, tx, users, ct));
				}
				if (flag2)
				{
					administrativeFolder2 = null;
				}
				else
				{
					Guid id = Guid.NewGuid();
					AdministrativeFolder administrativeFolder3;
					await using (SqlCommand cmd = new SqlCommand("INSERT dbo.administrative_document_folders(id,name,parent_id,department,is_shared,created_by,shared_user_ids) VALUES(@id,@name,@parent,@dept,@shared,@actor,@users)", c, tx))
					{
						cmd.Parameters.AddWithValue("@id", id);
						cmd.Parameters.AddWithValue("@name", name);
						cmd.Parameters.AddWithValue("@parent", ((object)parentId) ?? DBNull.Value);
						cmd.Parameters.AddWithValue("@dept", targetDepartment);
						cmd.Parameters.AddWithValue("@shared", users.Count > 0);
						cmd.Parameters.AddWithValue("@actor", actor.Id);
						cmd.Parameters.AddWithValue("@users", JsonSerializer.Serialize(users));
						await cmd.ExecuteNonQueryAsync(ct);
						await tx.CommitAsync(ct);
						administrativeFolder3 = new AdministrativeFolder(id, name, parentId, targetDepartment, users.Count > 0, actor.Id, DateTime.UtcNow)
						{
							SharedUserIds = users,
							CanManage = !parentId.HasValue,
							CanWrite = true
						};
					}
					administrativeFolder2 = administrativeFolder3;
				}
				end_IL_021d:;
			}
			result = administrativeFolder2;
		}
		return result;
	}

	private static async Task<bool> ValidFolderRecipients(SqlConnection c, SqlTransaction tx, List<string> ids, CancellationToken ct)
	{
		if (ids.Count > 20)
		{
			return false;
		}
		if (ids.Count == 0)
		{
			return true;
		}
		List<InstitutionalUser> users = await ReadInstitutionalUsers(c, ct, tx);
		return ids.All((string id) => users.Any((InstitutionalUser u) => SameFolderUser(u.UserName, id)));
	}

	public async Task<string?> SetAdministrativeFolderRecipients(Guid id, List<string> ids, DocumentActor actor, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			string text;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
				AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == id);
				if ((object)administrativeFolder == null || !FolderReadable(administrativeFolder, list, actor) || !SameFolderUser(administrativeFolder.CreatedBy, actor.Id))
				{
					text = "Solo quien creó la carpeta puede administrar sus accesos.";
				}
				else if (administrativeFolder.ParentId.HasValue)
				{
					text = "Los accesos se administran desde la carpeta principal y se heredan en sus subcarpetas.";
				}
				else
				{
					ids = ids.Where((string user) => !SameFolderUser(user, actor.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
					if (!(await ValidFolderRecipients(c, tx, ids, ct)))
					{
						text = "Selecciona hasta 20 usuarios activos.";
					}
					else
					{
						string text2;
						await using (SqlCommand cmd = new SqlCommand("UPDATE dbo.administrative_document_folders SET shared_user_ids=@users,is_shared=@shared WHERE id=@id", c, tx))
						{
							cmd.Parameters.AddWithValue("@id", id);
							cmd.Parameters.AddWithValue("@users", JsonSerializer.Serialize(ids));
							cmd.Parameters.AddWithValue("@shared", ids.Count > 0);
							await cmd.ExecuteNonQueryAsync(ct);
							await tx.CommitAsync(ct);
							text2 = null;
						}
						text = text2;
					}
				}
			}
			result = text;
		}
		return result;
	}

	public async Task<bool> RenameAdministrativeFolder(Guid id, string name, DocumentActor actor, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			bool flag;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
				AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == id);
				if ((object)administrativeFolder == null || !FolderWritable(administrativeFolder, list, actor))
				{
					flag = false;
				}
				else
				{
					bool flag2;
					await using (SqlCommand cmd = new SqlCommand("UPDATE dbo.administrative_document_folders SET name=@name WHERE id=@id AND department=@dept", c, tx))
					{
						cmd.Parameters.AddWithValue("@name", name);
						cmd.Parameters.AddWithValue("@id", id);
						cmd.Parameters.AddWithValue("@dept", administrativeFolder.Department);
						cmd.Parameters.AddWithValue("@actor", actor.Id);
						bool renamed = await cmd.ExecuteNonQueryAsync(ct) == 1;
						await tx.CommitAsync(ct);
						flag2 = renamed;
					}
					flag = flag2;
				}
			}
			result = flag;
		}
		return result;
	}

	public async Task<string?> MoveAdministrativeFolder(Guid id, Guid? parentId, DocumentActor actor, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			string text;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
				AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == id);
				if ((object)administrativeFolder == null || !FolderWritable(administrativeFolder, list, actor))
				{
					text = "Solo quien creó la carpeta puede moverla.";
				}
				else
				{
					AdministrativeFolder parent = list.Find((AdministrativeFolder f) =>
					{
						Guid id3 = f.Id;
						Guid? guid2 = parentId;
						return id3 == guid2;
					});
					if (parentId.HasValue && ((object)parent == null || !FolderWritable(parent, list, actor)))
					{
						text = "Selecciona una carpeta propia del departamento.";
					}
					else
					{
						AdministrativeFolder administrativeFolder2 = FolderRoot(id, list);
						object obj;
						if (parentId.HasValue)
						{
							Guid valueOrDefault = parentId.GetValueOrDefault();
							obj = FolderRoot(valueOrDefault, list);
						}
						else
						{
							obj = null;
						}
						AdministrativeFolder administrativeFolder3 = (AdministrativeFolder)obj;
						Guid id2 = administrativeFolder2.Id;
						Guid? guid = administrativeFolder3?.Id;
						if (id2 != guid && !SameFolderUser(administrativeFolder2.CreatedBy, actor.Id))
						{
							text = "Solo el creador puede cambiar los accesos al mover una carpeta fuera de su carpeta principal.";
						}
						else
						{
							HashSet<Guid> hashSet = new HashSet<Guid> { id };
							while (true)
							{
								if ((object)parent != null)
								{
									if (!hashSet.Add(parent.Id))
									{
										text = "Una carpeta no puede moverse dentro de sí misma.";
										break;
									}
									parent = list.Find((AdministrativeFolder f) =>
									{
										Guid id3 = f.Id;
										Guid? parentId2 = parent.ParentId;
										return id3 == parentId2;
									});
									continue;
								}
								List<string> sharedUserIds = FolderRoot(id, list).SharedUserIds;
								string text2;
								await using (SqlCommand cmd = new SqlCommand("UPDATE dbo.administrative_document_folders SET parent_id=@parent,shared_user_ids=@users,is_shared=@shared WHERE id=@id", c, tx))
								{
									cmd.Parameters.AddWithValue("@id", id);
									cmd.Parameters.AddWithValue("@parent", ((object)parentId) ?? DBNull.Value);
									cmd.Parameters.AddWithValue("@users", JsonSerializer.Serialize(sharedUserIds));
									cmd.Parameters.AddWithValue("@shared", sharedUserIds.Count > 0);
									await cmd.ExecuteNonQueryAsync(ct);
									await tx.CommitAsync(ct);
									text2 = null;
								}
								text = text2;
								break;
							}
						}
					}
				}
			}
			result = text;
		}
		return result;
	}

	public async Task<bool> DeleteAdministrativeFolder(Guid id, DocumentActor actor, CancellationToken ct)
	{
		bool result2;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			await EnsureAdministrativeFolderSchema(c, ct);
			bool flag;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
				AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == id);
				if ((object)administrativeFolder == null || !FolderWritable(administrativeFolder, list, actor))
				{
					flag = false;
				}
				else
				{
					bool flag2;
					await using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.administrative_document_folders WHERE id=@id AND department=@dept AND NOT EXISTS(SELECT 1 FROM dbo.administrative_document_folders WHERE parent_id=@id) AND NOT EXISTS(SELECT 1 FROM dbo.institutional_files WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.folderId'))=@id)", c, tx))
					{
						cmd.Parameters.AddWithValue("@id", id);
						cmd.Parameters.AddWithValue("@dept", administrativeFolder.Department);
						cmd.Parameters.AddWithValue("@actor", actor.Id);
						bool result = await cmd.ExecuteNonQueryAsync(ct) == 1;
						await tx.CommitAsync(ct);
						flag2 = result;
					}
					flag = flag2;
				}
			}
			result2 = flag;
		}
		return result2;
	}

	public async Task<string?> MoveAdministrativeFile(long id, Guid? folderId, string department, DocumentActor actor, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureAdministrativeFolderSchema(c, ct);
			await EnsureInstitutionalSchema(c, ct);
			string text;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, null, ct, tx);
				if (folderId.HasValue)
				{
					Guid target = folderId.GetValueOrDefault();
					AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == target);
					if ((object)administrativeFolder != null && FolderWritable(administrativeFolder, list, actor))
					{
						department = administrativeFolder.Department;
						goto IL_0397;
					}
					text = "Selecciona una carpeta compartida contigo.";
				}
				else
				{
					if (string.Equals(department, actor.Department, StringComparison.OrdinalIgnoreCase))
					{
						goto IL_0397;
					}
					text = "Selecciona tu propio departamento.";
				}
				goto end_IL_028a;
				IL_0397:
				string text2;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND area='files'", c, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text2 = "El archivo no está disponible.";
					}
					else
					{
						InstitutionalFile file = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						await ApplyAdministrativeFolderAccess(c, new _003C_003Ez__ReadOnlySingleElementList<InstitutionalFile>(file), actor, ct, tx);
						if (file.Trashed || !file.Accessible(actor) || (!file.FolderWriteAllowed && !SameFolderUser(file.OwnerUserName, actor.Id)))
						{
							text2 = "El archivo no está disponible.";
						}
						else
						{
							file.FolderId = folderId;
							file.SharedWith.RemoveAll((DocumentAccess access) => access.Id < 0);
							string text3;
							await using (SqlCommand save = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", c, tx))
							{
								save.Parameters.AddWithValue("@id", id);
								save.Parameters.AddWithValue("@json", JsonSerializer.Serialize(file, InstitutionalJson));
								await save.ExecuteNonQueryAsync(ct);
								await tx.CommitAsync(ct);
								text3 = null;
							}
							text2 = text3;
						}
					}
				}
				text = text2;
				end_IL_028a:;
			}
			result = text;
		}
		return result;
	}

	public async Task<string?> InstitutionalEditAdministrativeForm(long id, int revision, DocumentActor actor, InstitutionalFile replacement, byte[] data, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			string text3;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", c, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text = "El formulario ya no está disponible.";
					}
					else
					{
						InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						institutionalFile.Id = id;
						if (!AdministrativeFormEditing.CanEdit(institutionalFile, actor))
						{
							text = "No puedes editar este formulario. Los autorizados o entregados están cerrados.";
						}
						else if (institutionalFile.AdministrativeRevision != revision)
						{
							text = "Otra persona modificó el formulario. Vuelve a abrirlo antes de guardar.";
						}
						else
						{
							AdministrativeRequestInfo administrativeRequest = replacement.AdministrativeRequest;
							if (institutionalFile.AdministrativeRequest?.Internet?.TicketId != administrativeRequest?.Internet?.TicketId)
							{
								text = "La edición debe conservar el ticket vinculado.";
							}
							else if ((object)administrativeRequest == null || administrativeRequest.Number != institutionalFile.AdministrativeRequest.Number || administrativeRequest.FormType != institutionalFile.AdministrativeRequest.FormType || administrativeRequest.AssetType != institutionalFile.AdministrativeRequest.AssetType)
							{
								text = "La edición debe conservar el folio y el tipo de formulario.";
							}
							else
							{
								institutionalFile.History.Add(new InstitutionalEvent("edit_form", actor.Name, actor.Login, DateTime.UtcNow, institutionalFile.Status, institutionalFile.ReceiptCode, institutionalFile.ReceiverLogin, institutionalFile.ReceivedUtc, institutionalFile.ReceiptSignature, institutionalFile.AdministrativeRequest));
								institutionalFile.AdministrativeRevision++;
								institutionalFile.AdministrativeRequest = administrativeRequest;
								institutionalFile.Title = replacement.Title;
								institutionalFile.Description = replacement.Description;
								institutionalFile.OriginalFileName = replacement.OriginalFileName;
								institutionalFile.ContentType = "application/pdf";
								institutionalFile.Size = data.Length;
								institutionalFile.Status = (institutionalFile.AdministrativeRegistrationOnly ? "registered" : "pending");
								institutionalFile.ReceivedUtc = null;
								institutionalFile.ReceivedByUserName = null;
								institutionalFile.ReceiverDisplayName = null;
								institutionalFile.ReceiverLogin = null;
								institutionalFile.ReceiptCode = null;
								institutionalFile.VerificationToken = null;
								institutionalFile.ReceiptSignature = null;
								institutionalFile.DecisionBy = null;
								institutionalFile.DecisionUtc = null;
								institutionalFile.CorrectionNote = null;
								string text2;
								await using (SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id", c, tx))
								{
									update.Parameters.AddWithValue("@id", id);
									SqlParameterCollection parameters = update.Parameters;
									parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(institutionalFile, InstitutionalJson), upload: true, ct));
									parameters = update.Parameters;
									parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
									await update.ExecuteNonQueryAsync(ct);
									await tx.CommitAsync(ct);
									text2 = null;
								}
								text = text2;
							}
						}
					}
				}
				text3 = text;
			}
			result = text3;
		}
		return result;
	}

	private async Task<long> InstitutionalAdministrativeUpload(InstitutionalFile document, byte[] data, CancellationToken ct)
	{
		AdministrativeFuel.Normalize(document);
		long result2;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			long num4;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(ct)))
			{
				long num3;
				await using (SqlCommand gate = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 50001,'No se pudo confirmar el formulario.',1;", c, tx))
				{
					gate.Parameters.AddWithValue("@key", "administrative-form:" + document.OwnerUserName + ":" + document.AdministrativeRequest.Number);
					await gate.ExecuteNonQueryAsync(ct);
					long num2;
					await using (SqlCommand find = new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND JSON_VALUE(metadata,'$.administrativeRequest.number')=@number", c, tx))
					{
						find.Parameters.AddWithValue("@owner", document.OwnerUserName);
						find.Parameters.AddWithValue("@number", document.AdministrativeRequest.Number);
						long num;
						await using (SqlDataReader reader = await find.ExecuteReaderAsync(ct))
						{
							if (await reader.ReadAsync(ct))
							{
								long id = reader.GetInt64(0);
								InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson);
								if (institutionalFile.Department != document.Department || JsonSerializer.Serialize(institutionalFile.AdministrativeRequest, InstitutionalJson) != JsonSerializer.Serialize(document.AdministrativeRequest, InstitutionalJson))
								{
									throw new AdministrativeFormConflictException();
								}
								await reader.DisposeAsync();
								await tx.CommitAsync(ct);
								num = id;
								goto IL_06f3;
							}
						}
						await using (SqlCommand insert = new SqlCommand("INSERT dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@department,'communications','requirements',@json,@bytes)", c, tx))
						{
							insert.Parameters.AddWithValue("@owner", document.OwnerUserName);
							insert.Parameters.AddWithValue("@department", document.Department);
							SqlParameterCollection parameters = insert.Parameters;
							parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(document, InstitutionalJson), upload: true, ct));
							parameters = insert.Parameters;
							parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
							long result = Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
							await tx.CommitAsync(ct);
							num = result;
						}
						num2 = num;
						goto end_IL_0395;
						IL_06f3:
						num2 = num;
						end_IL_0395:;
					}
					num3 = num2;
				}
				num4 = num3;
			}
			result2 = num4;
		}
		return result2;
	}

	private static async Task EnsureAdministrativeProfiles(SqlConnection connection, CancellationToken ct)
	{
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(ct));
		await using (SqlCommand schema = new SqlCommand("DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='V418_ADMINISTRATIVE_PROFILES_20261002',@LockMode='Exclusive',@LockOwner='Transaction';\nIF @lock<0 THROW 50001,'No se pudo preparar Administración.',1;\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.admin_users') AND name='CK_admin_users_role' AND definition NOT LIKE '%ADMINISTRATIVE%')\nBEGIN\n  ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;\n  ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role CHECK(role IN('ADMINISTRATOR','ADMINISTRATIVE','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE'));\nEND;", connection, tx))
		{
			await schema.ExecuteNonQueryAsync(ct);
		}
		(string, string)[] array = new (string, string)[6]
		{
			("emma.mendoza", "Emma Mercedes Mendoza Reynoso"),
			("derihan.alisha", "Derihan Alisha Echavarria Rivas"),
			("whilmaly.sulay", "Whilmaly Sulay Lora Noesi"),
			("sandra.ramirez", "Sandra Lucia Ramirez Ortiz"),
			("emely.figueroa", "Emely Figueroa"),
			("diana.ventura", "Diana Marte Ventura")
		};
		(string, string)[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string, string) tuple = array2[i];
			string item = tuple.Item1;
			string item2 = tuple.Item2;
			byte[] bytes = RandomNumberGenerator.GetBytes(16);
			await using SqlCommand schema = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE LOWER(REPLACE(email,'@grupotejeda.local',''))=@login)\n  INSERT dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)\n  VALUES(CONVERT(char(36),NEWID()),@email,@name,'ADMINISTRATIVE',@salt,@hash,210000,1,N'Administración',1,N'{}',NULL);", connection, tx);
			schema.Parameters.AddWithValue("@login", item);
			schema.Parameters.AddWithValue("@email", item + "@grupotejeda.local");
			schema.Parameters.AddWithValue("@name", item2);
			schema.Parameters.AddWithValue("@salt", bytes);
			schema.Parameters.AddWithValue("@hash", PasswordHash("123456", bytes, 210000));
			await schema.ExecuteNonQueryAsync(ct);
		}
		await tx.CommitAsync(ct);
	}

	internal static async Task EnsureBryanManagerProfile(SqlConnection connection, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("SET XACT_ABORT ON;\nBEGIN TRANSACTION;\nDECLARE @lock int;\nEXEC @lock=sys.sp_getapplock @Resource=N'V418_BRYAN_MANAGER_20260930',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;\nIF @lock<0 THROW 50001,'No se pudo bloquear la actualización del perfil de gerente.',1;\nIF OBJECT_ID(N'dbo.user_access_profile_updates',N'U') IS NULL\n CREATE TABLE dbo.user_access_profile_updates(update_key varchar(100) NOT NULL PRIMARY KEY,user_id nvarchar(80) NOT NULL,previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\nIF NOT EXISTS(SELECT 1 FROM dbo.user_access_profile_updates WHERE update_key='V418_BRYAN_MANAGER_20260930')\nBEGIN\n DECLARE @id nvarchar(80),@previous nvarchar(max);\n SELECT @id=CONVERT(nvarchar(80),id),@previous=permissions_json FROM dbo.admin_users\n  WHERE LOWER(email)=N'bryan.arias@grupotejeda.local' AND (role='WAREHOUSE' OR (role='TECHNOLOGY' AND support_team='WAREHOUSE'));\n IF @id IS NOT NULL\n BEGIN\n  UPDATE dbo.admin_users SET permissions_json=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,\n   '$.canManageDepartment',CAST(1 AS bit)),'$.canViewFieldAudits',CAST(1 AS bit)),'$.canViewDashboard',CAST(1 AS bit)),'$.canViewSupportDashboard',CAST(1 AS bit)),updated_at=SYSUTCDATETIME()\n   WHERE CONVERT(nvarchar(80),id)=@id;\n  INSERT dbo.user_access_profile_updates(update_key,user_id,previous_permissions) VALUES('V418_BRYAN_MANAGER_20260930',@id,@previous);\n END\nEND\nCOMMIT;", connection);
		await command.ExecuteNonQueryAsync(ct);
	}

	internal static async Task EnsureCallCenterSupportProfile(SqlConnection connection, CancellationToken ct)
	{
		await EnsureUserAccessPresetSchema(connection, ct);
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		try
		{
			await using (SqlCommand schema = new SqlCommand("DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='call-center-support-v442',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;\nIF @lock<0 THROW 50001,'No se pudo preparar el perfil Call Center.',1;\nIF OBJECT_ID(N'dbo.call_center_support_backups',N'U') IS NULL CREATE TABLE dbo.call_center_support_backups(id bigint IDENTITY PRIMARY KEY,user_id nvarchar(80) NOT NULL,permissions_json nvarchar(max) NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\nIF OBJECT_ID(N'dbo.call_center_preset_backups',N'U') IS NULL CREATE TABLE dbo.call_center_preset_backups(id bigint IDENTITY PRIMARY KEY,permissions_json nvarchar(max) NOT NULL,updated_by nvarchar(100) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", connection, tx))
			{
				await schema.ExecuteNonQueryAsync(ct);
			}
			List<(string Id, string? Raw)> users = new List<(string, string)>();
			await using (SqlCommand schema = new SqlCommand("SELECT CONVERT(nvarchar(80),id),permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE role='TECHNOLOGY' AND support_team='CALL_CENTER'", connection, tx))
			{
				await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					users.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
				}
			}
			foreach (var item in users)
			{
				Dictionary<string, bool> original = JsonSerializer.Deserialize<Dictionary<string, bool>>(item.Raw ?? "{}") ?? new Dictionary<string, bool>();
				if (original.GetValueOrDefault("usesCustomizedPermissions"))
				{
					continue;
				}
				if (!CallCenterSupportProfile.Applies("TECHNOLOGY", "CALL_CENTER", original))
				{
					continue;
				}
				Dictionary<string, bool> dictionary = CallCenterSupportProfile.Normalize(original);
				if (original.Count != dictionary.Count || !dictionary.All((KeyValuePair<string, bool> p) => original.TryGetValue(p.Key, out var value) && value == p.Value))
				{
					await using SqlCommand schema = new SqlCommand("INSERT dbo.call_center_support_backups(user_id,permissions_json) VALUES(@id,@previous); UPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id", connection, tx);
					schema.Parameters.AddWithValue("@id", item.Id);
					schema.Parameters.AddWithValue("@previous", ((object)item.Raw) ?? ((object)DBNull.Value));
					schema.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(dictionary));
					await schema.ExecuteNonQueryAsync(ct);
				}
			}
			await using (SqlCommand schema = new SqlCommand("DECLARE @previous nvarchar(max)=(SELECT permissions_json FROM dbo.user_access_presets WITH(UPDLOCK,HOLDLOCK) WHERE department='CALL_CENTER' AND profile='SUPPORT');\nIF @previous IS NOT NULL AND @previous<>@permissions\n    INSERT dbo.call_center_preset_backups(permissions_json,updated_by) SELECT permissions_json,updated_by FROM dbo.user_access_presets WHERE department='CALL_CENTER' AND profile='SUPPORT';\nIF @previous IS NULL INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by) VALUES('CALL_CENTER','SUPPORT',@permissions,'system:call-center-v442');\nELSE IF @previous<>@permissions UPDATE dbo.user_access_presets SET permissions_json=@permissions,updated_by='system:call-center-v442',updated_at=SYSUTCDATETIME() WHERE department='CALL_CENTER' AND profile='SUPPORT';", connection, tx))
			{
				schema.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(CallCenterSupportProfile.Permissions()));
				await schema.ExecuteNonQueryAsync(ct);
			}
			// Yessica administra exclusivamente Call Center. Se conserva el perfil
			// completo de soporte y se eliminan alcances heredados de otras áreas.
			await using (SqlCommand yessica = new SqlCommand("UPDATE dbo.admin_users SET role='TECHNOLOGY',support_team='CALL_CENTER',permissions_json=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,'$.canManageDepartment',CAST(1 AS bit)),'$.canViewDashboard',CAST(1 AS bit)),'$.canViewSupportDashboard',CAST(1 AS bit)),'$.canViewAllSupportDepartments',CAST(0 AS bit)),'$.canViewDocuments',CAST(1 AS bit)),'$.canReceiveDocuments',CAST(1 AS bit)),'$.canSignDocuments',CAST(1 AS bit)),'$.canDecideDocuments',CAST(1 AS bit)),updated_at=SYSUTCDATETIME() WHERE LOWER(LTRIM(RTRIM(email))) IN(N'yessica.mercedes',N'yessica.mercedes@grupotejeda.local',N'yesica.mercedes',N'yesica.mercedes@grupotejeda.local');", connection, tx))
			{
				await yessica.ExecuteNonQueryAsync(ct);
			}
			await tx.CommitAsync(ct);
		}
		catch
		{
			await tx.RollbackAsync(ct);
			throw;
		}
	}

	public async Task<AgencyDirectoryImportResult> ImportAgencyDirectory(IReadOnlyList<AgencyDirectoryImportRow> rows, string? fileName, string actor, CancellationToken ct)
	{
		AgencyDirectoryImportResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyDirectorySchema(connection, ct);
			AgencyDirectoryImportResult agencyDirectoryImportResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				Guid batchId = Guid.NewGuid();
				try
				{
					await using (SqlCommand staging = new SqlCommand("CREATE TABLE #agency_import(\n    row_number int NOT NULL,\n    codigo nvarchar(100) NOT NULL,\n    terminal nvarchar(300) NOT NULL,\n    grupo nvarchar(150) NOT NULL,\n    region nvarchar(120) NULL,\n    direccion nvarchar(300) NULL,\n    sector nvarchar(120) NULL,\n    municipio nvarchar(120) NULL,\n    provincia nvarchar(120) NULL,\n    latitude float NULL,\n    longitude float NULL,\n    group_conflict nvarchar(300) NULL\n);", connection, transaction))
					{
						await staging.ExecuteNonQueryAsync(ct);
					}
					List<AgencyImportGroup> existingGroups = new List<AgencyImportGroup>();
					await using (SqlCommand staging = new SqlCommand("SELECT grupo,COUNT(*) FROM dbo.agencies WITH(UPDLOCK,HOLDLOCK) GROUP BY grupo;", connection, transaction))
					{
						await using SqlDataReader reader = await staging.ExecuteReaderAsync(ct);
						while (await reader.ReadAsync(ct))
						{
							existingGroups.Add(new AgencyImportGroup(reader.GetString(0), reader.GetInt32(1)));
						}
					}
					AgencyImportGroups agencyImportGroups = new AgencyImportGroups(existingGroups, rows.Select((AgencyDirectoryImportRow row) => row.Grupo));
					DataTable dataTable = new DataTable();
					dataTable.Columns.Add("row_number", typeof(int));
					dataTable.Columns.Add("codigo", typeof(string));
					dataTable.Columns.Add("terminal", typeof(string));
					dataTable.Columns.Add("grupo", typeof(string));
					dataTable.Columns.Add("region", typeof(string));
					dataTable.Columns.Add("direccion", typeof(string));
					dataTable.Columns.Add("sector", typeof(string));
					dataTable.Columns.Add("municipio", typeof(string));
					dataTable.Columns.Add("provincia", typeof(string));
					dataTable.Columns.Add("latitude", typeof(double));
					dataTable.Columns.Add("longitude", typeof(double));
					dataTable.Columns.Add("group_conflict", typeof(string));
					foreach (AgencyDirectoryImportRow row in rows)
					{
						string text = row.Codigo.Trim();
						string text2 = row.Terminal.Trim();
						(string, string) tuple = agencyImportGroups.Resolve(row.Grupo);
						var (text3, _) = tuple;
						dataTable.Rows.Add(row.RowNumber, text, text2, text3, Db(string.IsNullOrWhiteSpace(row.Region) ? null : row.Region.Trim()), Db(string.IsNullOrWhiteSpace(row.Direccion) ? null : row.Direccion.Trim()), Db(string.IsNullOrWhiteSpace(row.Sector) ? null : row.Sector.Trim()), Db(string.IsNullOrWhiteSpace(row.Municipio) ? null : row.Municipio.Trim()), Db(string.IsNullOrWhiteSpace(row.Provincia) ? null : row.Provincia.Trim()), Db(row.Latitude), Db(row.Longitude), Db(tuple.Item2));
					}
					using (SqlBulkCopy bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, transaction))
					{
						bulk.DestinationTableName = "#agency_import";
						bulk.BatchSize = 1000;
						bulk.BulkCopyTimeout = 120;
						await bulk.WriteToServerAsync(dataTable, ct);
					}
					await using (SqlCommand staging = new SqlCommand("SELECT i.*,existing.target_id,\r\n    CASE WHEN i.group_conflict IS NOT NULL THEN i.group_conflict\r\n         WHEN batch_code.matches>1 THEN N'El código aparece varias veces en el archivo'\n         WHEN existing.matches>1 THEN N'El código ya pertenece a varias agencias; requiere revisión'\n         WHEN existing.matches=0 AND terminal_match.matches>0 THEN N'La terminal ya existe con otro código, incluso en otro grupo'\r\n         WHEN existing.matches=0 AND batch_match.matches>1 THEN N'La terminal aparece con varios códigos en el archivo'\r\n         ELSE NULL END AS conflict_reason\r\nINTO #agency_import_resolution\r\nFROM #agency_import i\r\nCROSS APPLY(SELECT COUNT(*) matches,MIN(a.id) target_id FROM dbo.agencies a WITH(UPDLOCK,HOLDLOCK)\r\n    WHERE UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(i.codigo)) existing\r\nCROSS APPLY(SELECT COUNT(*) matches FROM dbo.agencies a WITH(UPDLOCK,HOLDLOCK)\r\n    WHERE UPPER(LTRIM(RTRIM(a.terminal)))=UPPER(i.terminal)) terminal_match\r\nCROSS APPLY(SELECT COUNT(*) matches FROM #agency_import other\n    WHERE UPPER(other.terminal)=UPPER(i.terminal)) batch_match\nCROSS APPLY(SELECT COUNT(*) matches FROM #agency_import other\n    WHERE UPPER(other.codigo)=UPPER(i.codigo)) batch_code;", connection, transaction))
					{
						staging.CommandTimeout = 120;
						await staging.ExecuteNonQueryAsync(ct);
					}
					List<string> conflicts = new List<string>();
					await using (SqlCommand staging = new SqlCommand("SELECT row_number,CONCAT(codigo,N': ',conflict_reason)\r\nFROM #agency_import_resolution WHERE conflict_reason IS NOT NULL ORDER BY row_number;", connection, transaction))
					{
						await using SqlDataReader reader = await staging.ExecuteReaderAsync(ct);
						while (await reader.ReadAsync(ct))
						{
							conflicts.Add($"Fila {reader.GetInt32(0)}: {reader.GetString(1)}");
						}
					}
					int updated;
					await using (SqlCommand staging = new SqlCommand("UPDATE a SET terminal=m.terminal,grupo=m.grupo,region=COALESCE(m.region,a.region),\r\n    directory_direccion=COALESCE(m.direccion,a.directory_direccion),directory_sector=COALESCE(m.sector,a.directory_sector),\r\n    directory_municipio=COALESCE(m.municipio,a.directory_municipio),directory_provincia=COALESCE(m.provincia,a.directory_provincia),\r\n    expected_latitude=COALESCE(m.latitude,a.expected_latitude),expected_longitude=COALESCE(m.longitude,a.expected_longitude),\r\n    is_active=1,updated_at=SYSUTCDATETIME()\r\nFROM dbo.agencies a INNER JOIN #agency_import_resolution m ON m.target_id=a.id\r\nWHERE m.conflict_reason IS NULL;", connection, transaction))
					{
						staging.CommandTimeout = 120;
						updated = await staging.ExecuteNonQueryAsync(ct);
					}
					int created;
					await using (SqlCommand staging = new SqlCommand("INSERT INTO dbo.agencies\r\n    (id,source_key,codigo,terminal,grupo,region,directory_direccion,directory_sector,directory_municipio,directory_provincia,expected_latitude,expected_longitude,is_active,status,created_at,updated_at)\r\nSELECT CONVERT(char(36),NEWID()),CONCAT(N'EXCEL:',CONVERT(nvarchar(36),NEWID())),i.codigo,i.terminal,i.grupo,i.region,i.direccion,i.sector,i.municipio,i.provincia,i.latitude,i.longitude,1,'PENDING',SYSUTCDATETIME(),SYSUTCDATETIME()\r\nFROM #agency_import_resolution i\r\nWHERE i.target_id IS NULL AND i.conflict_reason IS NULL;", connection, transaction))
					{
						staging.CommandTimeout = 120;
						created = await staging.ExecuteNonQueryAsync(ct);
					}
					await using (SqlCommand staging = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)\nVALUES('agency_import',@id,'AGENCY_DIRECTORY_EXCEL_IMPORTED',@after,@actor);", connection, transaction))
					{
						staging.Parameters.AddWithValue("@id", batchId.ToString());
						staging.Parameters.AddWithValue("@after", JsonSerializer.Serialize(new
						{
							fileName = fileName,
							total = rows.Count,
							created = created,
							updated = updated,
							skipped = conflicts.Count,
							conflicts = conflicts.Take(100)
						}));
						staging.Parameters.AddWithValue("@actor", actor);
						await staging.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
					agencyDirectoryImportResult = new AgencyDirectoryImportResult(rows.Count, created, updated, conflicts.Count, conflicts.Take(100).ToList());
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = agencyDirectoryImportResult;
		}
		return result;
	}

	public async Task<List<AgencyLocationRow>> AgencyLocations(string userId, string role, CancellationToken ct)
	{
		List<AgencyLocationRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyDirectorySchema(connection, ct);
			List<AgencyLocationRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.id,a.codigo,a.terminal,a.grupo,\n       COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),\n       COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),\n       CASE WHEN p.latitude BETWEEN -90 AND 90 AND p.longitude BETWEEN -180 AND 180 AND (p.latitude<>0 OR p.longitude<>0) THEN p.latitude ELSE a.expected_latitude END,\n       CASE WHEN p.latitude BETWEEN -90 AND 90 AND p.longitude BETWEEN -180 AND 180 AND (p.latitude<>0 OR p.longitude<>0) THEN p.longitude ELSE a.expected_longitude END\nFROM dbo.agencies a\nOUTER APPLY (SELECT TOP(1) p.* FROM dbo.agency_profiles p WHERE p.agency_id=a.id ORDER BY p.submitted_at DESC,p.id DESC) p\nWHERE a.is_active=1 AND (@scoped=0 OR EXISTS(\n    SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))\nORDER BY a.grupo,a.terminal,a.id;", connection))
			{
				AddScope(command, userId, role);
				SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				List<AgencyLocationRow> list;
				try
				{
					List<AgencyLocationRow> rows = new List<AgencyLocationRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new AgencyLocationRow(Text(0), Text(1) ?? "", Text(2) ?? "", Text(3) ?? "", Text(4), Text(5), Text(6), Text(7), reader.IsDBNull(8) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetValue(8))), reader.IsDBNull(9) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetValue(9)))));
					}
					list = rows;
				}
				finally
				{
					if (reader != null)
					{
						await reader.DisposeAsync();
					}
				}
				list2 = list;
				string? Text(int i)
				{
					if (!reader.IsDBNull(i))
					{
						return reader.GetString(i);
					}
					return null;
				}
			}
			result = list2;
		}
		return result;
	}

	private static async Task EnsureBundledAgencyLocations(SqlConnection connection, CancellationToken ct)
	{
		string path = Path.Combine(AppContext.BaseDirectory, "Data", "agency-locations-sonadora-20260907.json");
		if (!File.Exists(path))
		{
			return;
		}
		List<BundledAgencyLocation> rows = JsonSerializer.Deserialize<List<BundledAgencyLocation>>(await File.ReadAllTextAsync(path, ct), StorageJson) ?? new List<BundledAgencyLocation>();
		if (rows.Count == 0)
		{
			return;
		}
		await using SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		try
		{
			await using (SqlCommand schemaCommand = new SqlCommand("IF OBJECT_ID(N'dbo.agency_location_imports',N'U') IS NULL\nBEGIN\n    CREATE TABLE dbo.agency_location_imports(\n        source_id varchar(80) NOT NULL CONSTRAINT PK_agency_location_imports PRIMARY KEY,\n        source_rows int NOT NULL,\n        matched_agencies int NOT NULL,\n        applied_at datetime2(3) NOT NULL CONSTRAINT DF_agency_location_imports_applied DEFAULT SYSUTCDATETIME()\n    );\nEND;", connection, transaction))
			{
				await schemaCommand.ExecuteNonQueryAsync(ct);
			}
			await using (SqlCommand schemaCommand = new SqlCommand("SELECT COUNT(*) FROM dbo.agency_location_imports WITH(UPDLOCK,HOLDLOCK) WHERE source_id=@source;", connection, transaction))
			{
				schemaCommand.Parameters.AddWithValue("@source", "SONADORA-2026-09-07");
				if (Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(ct)) <= 0)
				{
					goto end_IL_0356;
				}
				await transaction.CommitAsync(ct);
				goto end_IL_01d4;
				end_IL_0356:;
			}
			await using (SqlCommand schemaCommand = new SqlCommand("CREATE TABLE #agency_locations(\n    codigo nvarchar(100) NOT NULL PRIMARY KEY,\n    direccion nvarchar(300) NULL,\n    sector nvarchar(120) NULL,\n    municipio nvarchar(120) NULL,\n    provincia nvarchar(120) NULL,\n    latitude float NOT NULL,\n    longitude float NOT NULL\n);", connection, transaction))
			{
				await schemaCommand.ExecuteNonQueryAsync(ct);
			}
			DataTable dataTable = new DataTable();
			dataTable.Columns.Add("codigo", typeof(string));
			dataTable.Columns.Add("direccion", typeof(string));
			dataTable.Columns.Add("sector", typeof(string));
			dataTable.Columns.Add("municipio", typeof(string));
			dataTable.Columns.Add("provincia", typeof(string));
			dataTable.Columns.Add("latitude", typeof(double));
			dataTable.Columns.Add("longitude", typeof(double));
			foreach (BundledAgencyLocation item in rows)
			{
				dataTable.Rows.Add(item.Codigo, Db(item.Direccion), Db(item.Sector), Db(item.Municipio), Db(item.Provincia), item.Latitude, item.Longitude);
			}
			using (SqlBulkCopy bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, transaction))
			{
				bulk.DestinationTableName = "#agency_locations";
				bulk.BatchSize = 500;
				await bulk.WriteToServerAsync(dataTable, ct);
			}
			await using (SqlCommand syncCommand = new SqlCommand("DECLARE @matched int=(SELECT COUNT(*) FROM dbo.agencies a INNER JOIN #agency_locations s ON UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(LTRIM(RTRIM(s.codigo))));\nUPDATE a SET\n    directory_direccion=COALESCE(s.direccion,a.directory_direccion),\n    directory_sector=COALESCE(s.sector,a.directory_sector),\n    directory_municipio=COALESCE(s.municipio,a.directory_municipio),\n    directory_provincia=COALESCE(s.provincia,a.directory_provincia),\n    expected_latitude=s.latitude,expected_longitude=s.longitude,updated_at=SYSUTCDATETIME()\nFROM dbo.agencies a INNER JOIN #agency_locations s ON UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(LTRIM(RTRIM(s.codigo)));\nUPDATE p SET\n    direccion=COALESCE(s.direccion,p.direccion),sector=COALESCE(s.sector,p.sector),\n    municipio=COALESCE(s.municipio,p.municipio),provincia=COALESCE(s.provincia,p.provincia),\n    latitude=s.latitude,longitude=s.longitude,updated_at=SYSUTCDATETIME()\nFROM dbo.agency_profiles p INNER JOIN dbo.agencies a ON a.id=p.agency_id INNER JOIN #agency_locations s ON UPPER(LTRIM(RTRIM(a.codigo)))=UPPER(LTRIM(RTRIM(s.codigo)));\nINSERT INTO dbo.agency_location_imports(source_id,source_rows,matched_agencies) VALUES(@source,@rows,@matched);\nINSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)\nVALUES('agency_import',@source,'AGENCY_LOCATIONS_SYNCHRONIZED',CONCAT(N'{\"sourceRows\":',@rows,N',\"matchedAgencies\":',@matched,N'}'),N'system@grupotejeda.local');\nSELECT @matched;", connection, transaction))
			{
				syncCommand.Parameters.AddWithValue("@source", "SONADORA-2026-09-07");
				syncCommand.Parameters.AddWithValue("@rows", rows.Count);
				await syncCommand.ExecuteScalarAsync(ct);
				await transaction.CommitAsync(ct);
			}
			return;
			end_IL_01d4:;
		}
		catch
		{
			await transaction.RollbackAsync(ct);
			throw;
		}
	}

	private static async Task EnsureDocumentSchema(SqlConnection connection, CancellationToken ct)
	{
		await EnsureMaintenanceSchema(connection, ct);
		if (documentSchemaReady)
		{
			return;
		}
		await DocumentSchemaLock.WaitAsync(ct);
		try
		{
			if (!documentSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.maintenance_document_marks',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_document_marks(\r\n    movement_id uniqueidentifier NOT NULL PRIMARY KEY REFERENCES dbo.maintenance_movements(id) ON DELETE CASCADE,\r\n    received_at datetime2(3) NULL,receiver_user_id nvarchar(80) NULL,receiver_name nvarchar(160) NULL,receiver_login nvarchar(256) NULL,\r\n    stamp_x float NOT NULL DEFAULT(109),stamp_y float NOT NULL DEFAULT(191),\r\n    signature_data nvarchar(max) NULL,signer_user_id nvarchar(80) NULL,signer_name nvarchar(160) NULL,signer_login nvarchar(256) NULL,signed_at datetime2(3) NULL\r\n);\r\nIF OBJECT_ID(N'dbo.maintenance_document_signatures',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_document_signatures(\r\n    movement_id uniqueidentifier NOT NULL REFERENCES dbo.maintenance_movements(id) ON DELETE CASCADE,\r\n    signature_role varchar(10) NOT NULL CHECK(signature_role IN('DELIVERY','RECEIPT')),\r\n    signature_data nvarchar(max) NOT NULL,signer_user_id nvarchar(80) NOT NULL,\r\n    signer_name nvarchar(160) NOT NULL,signer_login nvarchar(256) NOT NULL,signed_at datetime2(3) NOT NULL,\r\n    PRIMARY KEY(movement_id,signature_role)\r\n);\r\nIF COL_LENGTH(N'dbo.maintenance_document_marks',N'signature_layout_version') IS NULL\r\nBEGIN\r\n    ALTER TABLE dbo.maintenance_document_marks ADD signature_layout_version tinyint NOT NULL DEFAULT(2);\r\n    UPDATE dbo.maintenance_document_marks SET stamp_y=191 WHERE stamp_x=109 AND stamp_y=237;\r\nEND;\r\nIF OBJECT_ID(N'dbo.maintenance_saved_signatures',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_saved_signatures(user_id nvarchar(80) NOT NULL PRIMARY KEY,signature_data nvarchar(max) NOT NULL,updated_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()));\r\nIF OBJECT_ID(N'dbo.maintenance_department_requests',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_department_requests(\r\n    id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT(NEWID()),department varchar(40) NOT NULL,\r\n    product_name nvarchar(160) NOT NULL,quantity int NOT NULL,notes nvarchar(2000) NULL,\r\n    created_by_name nvarchar(160) NOT NULL,created_by_login nvarchar(256) NOT NULL,\r\n    created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),status varchar(20) NOT NULL DEFAULT('SENT')\r\n);\r\nIF COL_LENGTH(N'dbo.maintenance_requisitions',N'decided_by_login') IS NULL ALTER TABLE dbo.maintenance_requisitions ADD decided_by_login nvarchar(256) NULL,decided_by_name nvarchar(160) NULL,decided_at datetime2(3) NULL,decision_reason nvarchar(1000) NULL;\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_requisitions') AND name='CK_maintenance_requisition_status' AND definition NOT LIKE '%APPROVED%')\r\nBEGIN\r\n    ALTER TABLE dbo.maintenance_requisitions DROP CONSTRAINT CK_maintenance_requisition_status;\r\n    ALTER TABLE dbo.maintenance_requisitions WITH CHECK ADD CONSTRAINT CK_maintenance_requisition_status CHECK(status IN('PENDING','APPROVED','REJECTED','ORDERED','PARTIAL','FULFILLED','CANCELLED'));\r\nEND;", connection);
				await command.ExecuteNonQueryAsync(ct);
				documentSchemaReady = true;
			}
		}
		finally
		{
			DocumentSchemaLock.Release();
		}
	}

	private static async Task<bool> CanAccessMaintenanceDocument(SqlConnection connection, Guid id, string userId, string role, string? team, CancellationToken ct)
	{
		bool result;
		await using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.maintenance_movements m WHERE m.id=@id AND (\r\n    @role='Administrator'\r\n    OR m.created_by_user_id=@user OR m.technician_user_id=@user\r\n    OR (@team='WAREHOUSE' AND m.operational_area='WAREHOUSE' AND m.department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'))\r\n    OR (@team='WORKSHOP' AND (m.operational_area='WORKSHOP' OR m.movement_type='TRANSFER_TO_WORKSHOP'))\r\n    OR (@team NOT IN('WAREHOUSE','WORKSHOP','TECHNICIANS') AND @department<>'' AND m.department=@department)\r\n    OR (@team='TECHNICIANS' AND m.department=@department AND m.technician_user_id=@user)\r\n    OR (@role='GroupAdministrator' AND m.technician_user_id=@user AND EXISTS(SELECT 1 FROM dbo.agencies a JOIN dbo.admin_user_groups g ON g.group_name=a.grupo WHERE a.id=m.agency_id AND g.user_id=@user))\r\n);", connection))
		{
			cmd.Parameters.AddWithValue("@id", id);
			cmd.Parameters.AddWithValue("@user", userId);
			cmd.Parameters.AddWithValue("@role", role);
			cmd.Parameters.AddWithValue("@team", team ?? "");
			cmd.Parameters.AddWithValue("@department", DepartmentForRole(role));
			result = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
		}
		return result;
	}

	public async Task<string?> MaintenanceDocument(Guid id, string userId, string role, string? team, CancellationToken ct)
	{
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			if (!(await CanAccessMaintenanceDocument(connection, id, userId, role, team, ct)))
			{
				result = null;
			}
			else
			{
				string text;
				await using (SqlCommand command = new SqlCommand("SELECT m.id,m.document_number documentNumber,d.received_at receivedAt,d.receiver_name receiverName,d.receiver_login receiverLogin,\r\n    COALESCE(d.stamp_x,109) stampX,COALESCE(d.stamp_y,191) stampY,COALESCE(NULLIF(d.signature_data,''),m.signature_data) signatureData,d.signer_name signerName,d.signer_login signerLogin,d.signed_at signedAt,\r\n    JSON_QUERY((SELECT s.signature_data data,s.signer_name name,s.signer_login login,s.signed_at signedAt FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) deliverySignature,\r\n    JSON_QUERY((SELECT s.signature_data data,s.signer_name name,s.signer_login login,s.signed_at signedAt FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)) receiptSignature,\r\n    CAST(CASE WHEN d.received_at IS NULL\r\n        AND EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY')\r\n        AND EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT')\r\n        AND (m.movement_type<>'REQUEST' OR m.operational_area<>'WAREHOUSE' OR @team='WAREHOUSE') THEN 1 ELSE 0 END AS bit) canReceive,\r\n    CAST(CASE WHEN d.receiver_user_id=@user OR @admin=1 THEN 1 ELSE 0 END AS bit) canMove,\r\n    CAST(CASE WHEN (m.movement_type<>'REQUEST' OR m.operational_area<>'WAREHOUSE' OR @team<>'WAREHOUSE') AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY' AND s.signer_user_id<>@user) THEN 1 ELSE 0 END AS bit) canSignDelivery,\r\n    CAST(CASE WHEN (m.movement_type<>'REQUEST' OR m.operational_area<>'WAREHOUSE' OR @team='WAREHOUSE') AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT' AND s.signer_user_id<>@user) THEN 1 ELSE 0 END AS bit) canSignReceipt,\r\n    CASE WHEN m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'ENTREGADO' ELSE 'RECIBIDO' END stampLabel,\r\n    CAST(1 AS bit) requiresBothSignatures\r\nFROM dbo.maintenance_movements m LEFT JOIN dbo.maintenance_document_marks d ON d.movement_id=m.id WHERE m.id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;", connection))
				{
					command.Parameters.AddWithValue("@id", id);
					command.Parameters.AddWithValue("@user", userId);
					command.Parameters.AddWithValue("@team", team ?? "");
					command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
					text = await StorageFiles.TransformJson((string)(await command.ExecuteScalarAsync(ct)), upload: false, ct);
				}
				result = text;
			}
		}
		return result;
	}

	public async Task<TicketActionResult> MarkMaintenanceDocument(Guid id, MaintenanceDocumentAction body, string userId, string role, string? team, string actor, string login, CancellationToken ct)
	{
		bool flag = body.Action == "SIGN";
		bool flag2 = flag;
		if (flag2)
		{
			string signatureRole = body.SignatureRole;
			bool flag3 = ((signatureRole == "DELIVERY" || signatureRole == "RECEIPT") ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			return new TicketActionResult(Ok: false, "VALIDATION", "Selecciona si firmas como quien entrega o quien recibe. Actualiza el formulario.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			if (!(await CanAccessMaintenanceDocument(connection, id, userId, role, team, ct)))
			{
				result = new TicketActionResult(Ok: false, "FORBIDDEN", "No tienes acceso a este formulario.");
			}
			else
			{
				TicketActionResult ticketActionResult3;
				await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
				{
					try
					{
						await using (SqlCommand ensure = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.maintenance_document_marks WITH(UPDLOCK,HOLDLOCK) WHERE movement_id=@id) INSERT INTO dbo.maintenance_document_marks(movement_id,stamp_y) VALUES(@id,191);", connection, transaction))
						{
							ensure.Parameters.AddWithValue("@id", id);
							await ensure.ExecuteNonQueryAsync(ct);
						}
						bool hasDelivery = false;
						bool hasReceipt = false;
						bool isRequestToWarehouse = false;
						string operationalArea;
						TicketActionResult ticketActionResult2;
						await using (SqlCommand ensure = new SqlCommand("SELECT m.movement_type,m.operational_area,CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY') THEN 1 ELSE 0 END AS bit),CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT') THEN 1 ELSE 0 END AS bit) FROM dbo.maintenance_movements m WHERE m.id=@id;", connection, transaction))
						{
							ensure.Parameters.AddWithValue("@id", id);
							TicketActionResult ticketActionResult;
							await using (SqlDataReader reader = await ensure.ExecuteReaderAsync(ct))
							{
								if (!(await reader.ReadAsync(ct)))
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Formulario no encontrado.");
									goto IL_084b;
								}
								string text = reader.GetString(0);
								operationalArea = reader.GetString(1);
								hasDelivery = reader.GetBoolean(2);
								hasReceipt = reader.GetBoolean(3);
								isRequestToWarehouse = text == "REQUEST" && operationalArea == "WAREHOUSE";
							}
							goto end_IL_0583;
							IL_084b:
							ticketActionResult2 = ticketActionResult;
							goto IL_092a;
							end_IL_0583:;
						}
						ticketActionResult2 = null;
						if (operationalArea == "WORKSHOP")
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult3 = new TicketActionResult(Ok: false, "FORBIDDEN", "La recepción se registra desde las etapas de Taller. Este formulario no utiliza firmas.");
						}
						else if (((body.Action == "SIGN") & isRequestToWarehouse) && body.SignatureRole == "DELIVERY" && team == "WAREHOUSE")
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult3 = new TicketActionResult(Ok: false, "FORBIDDEN", "Almacén General solo firma como quien recibe esta solicitud.");
						}
						else if (((body.Action == "SIGN") & isRequestToWarehouse) && body.SignatureRole == "RECEIPT" && team != "WAREHOUSE")
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult3 = new TicketActionResult(Ok: false, "FORBIDDEN", "El departamento solicitante solo firma como quien entrega esta solicitud.");
						}
						else if (((body.Action == "RECEIVE") & isRequestToWarehouse) && team != "WAREHOUSE")
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult3 = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén General puede recibir esta solicitud.");
						}
						else if (body.Action == "RECEIVE" && (!hasDelivery || !hasReceipt))
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult3 = new TicketActionResult(Ok: false, "CONFLICT", "El formulario debe estar firmado por quien entrega y quien recibe antes de confirmar el sello.");
						}
						else
						{
							await using (SqlCommand command = new SqlCommand(body.Action switch
							{
								"RECEIVE" => "UPDATE dbo.maintenance_document_marks SET received_at=SYSUTCDATETIME(),receiver_user_id=@user,receiver_name=@actor,receiver_login=@login,stamp_x=@x,stamp_y=@y WHERE movement_id=@id AND received_at IS NULL;", 
								"MOVE" => "UPDATE dbo.maintenance_document_marks SET stamp_x=@x,stamp_y=@y WHERE movement_id=@id AND received_at IS NOT NULL AND (receiver_user_id=@user OR @admin=1);", 
								"SIGN" => "MERGE dbo.maintenance_document_signatures WITH(HOLDLOCK) AS target USING(SELECT @id movement_id,@signatureRole signature_role) AS source ON target.movement_id=source.movement_id AND target.signature_role=source.signature_role WHEN MATCHED AND target.signer_user_id=@user THEN UPDATE SET signature_data=@signature,signer_name=@actor,signer_login=@login,signed_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(movement_id,signature_role,signature_data,signer_user_id,signer_name,signer_login,signed_at) VALUES(@id,@signatureRole,@signature,@user,@actor,@login,SYSUTCDATETIME());", 
								_ => throw new InvalidOperationException("Acción no válida."), 
							}, connection, transaction))
							{
								command.Parameters.AddWithValue("@id", id);
								command.Parameters.AddWithValue("@user", userId);
								command.Parameters.AddWithValue("@actor", actor);
								command.Parameters.AddWithValue("@login", login);
								command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
								command.Parameters.AddWithValue("@x", body.X);
								command.Parameters.AddWithValue("@y", body.Y);
								SqlParameterCollection parameters = command.Parameters;
								parameters.AddWithValue("@signature", Db(await StorageFiles.StoreDataUrl(body.SignatureData, ct)));
								command.Parameters.AddWithValue("@signatureRole", Db(body.SignatureRole));
								if (await command.ExecuteNonQueryAsync(ct) != 1)
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult2 = new TicketActionResult(Ok: false, "CONFLICT", "El formulario ya fue recibido o firmado por otra persona. Actualiza para consultar los datos.");
								}
								else
								{
									await transaction.CommitAsync(ct);
									ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
								}
							}
							ticketActionResult3 = ticketActionResult2;
						}
						goto end_IL_03be;
						IL_092a:
						ticketActionResult3 = ticketActionResult2;
						end_IL_03be:;
					}
					catch
					{
						await transaction.RollbackAsync(ct);
						throw;
					}
				}
				result = ticketActionResult3;
			}
		}
		return result;
	}

	public async Task<string?> OwnSavedSignature(string userId, CancellationToken ct)
	{
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			string text;
			await using (SqlCommand command = new SqlCommand("SELECT signature_data FROM dbo.maintenance_saved_signatures WHERE user_id=@user", connection))
			{
				command.Parameters.AddWithValue("@user", userId);
				text = await StorageFiles.ReadDataUrl((await command.ExecuteScalarAsync(ct)) as string, ct);
			}
			result = text;
		}
		return result;
	}

	public async Task SaveOwnSignature(string userId, string signature, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await EnsureDocumentSchema(connection, ct);
		await using SqlCommand command = new SqlCommand("MERGE dbo.maintenance_saved_signatures WITH(HOLDLOCK) AS target USING(SELECT @user user_id) AS source ON target.user_id=source.user_id WHEN MATCHED THEN UPDATE SET signature_data=@signature,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(user_id,signature_data) VALUES(@user,@signature);", connection);
		command.Parameters.AddWithValue("@user", userId);
		SqlParameterCollection parameters = command.Parameters;
		parameters.AddWithValue("@signature", await StorageFiles.StoreDataUrl(signature, ct));
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<string> MaintenanceTrack(string code, string userId, string role, string? team, CancellationToken ct)
	{
		string normalized = MaintenanceTraceCode.Normalize(code);
		string shortCode = (normalized.Contains('|') ? normalized.Substring(normalized.IndexOf('|') + 1) : normalized);
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			await EnsureInstitutionalSchema(connection, ct);
			string text;
			await using (SqlCommand command = new SqlCommand("DECLARE @movement uniqueidentifier=(SELECT TOP 1 m.id FROM dbo.maintenance_movements m WHERE m.qr_token IN(@code,@shortCode) OR m.document_number IN(@code,@shortCode) ORDER BY m.occurred_at DESC,m.created_at DESC);\r\nSELECT COALESCE((SELECT m.id,m.document_number documentNumber,m.equipment_type equipmentType,m.serial_number serialNumber,m.department,m.movement_type movementType,m.operational_area operationalArea,\r\n    CASE WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.warehouseDelivery.at') IS NOT NULL THEN 'DELIVERED' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.status')='approved' THEN 'APPROVED' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.status')='rejected' THEN 'REJECTED' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.receivedUtc') IS NOT NULL AND JSON_VALUE(institutional.metadata,'$.warehousePreparedUtc') IS NOT NULL THEN 'AUTHORIZATION' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.receivedUtc') IS NOT NULL THEN 'DISPATCH' WHEN d.received_at IS NOT NULL AND m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'DELIVERED' WHEN d.received_at IS NOT NULL THEN 'RECEIVED' ELSE 'SENT' END status,\r\n    CASE WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.warehouseDelivery.at') IS NOT NULL THEN 'Equipo retirado y entregado' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.status')='approved' THEN 'Autorizada por Almacén' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.status')='rejected' THEN 'Rechazada por Almacén' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.receivedUtc') IS NOT NULL AND JSON_VALUE(institutional.metadata,'$.warehousePreparedUtc') IS NOT NULL THEN 'Pendiente de autorización final' WHEN m.movement_type='REQUEST' AND JSON_VALUE(institutional.metadata,'$.receivedUtc') IS NOT NULL THEN 'En despacho: pendiente de asignar equipo y serial' WHEN d.received_at IS NOT NULL AND m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'Entregado' WHEN d.received_at IS NOT NULL THEN 'Recibido' ELSE 'Pendiente de recepción en Almacén' END statusText,\r\n    m.created_at sentAt,m.created_by_name senderName,COALESCE(m.delivered_by_name,m.created_by_name) returnName,d.receiver_name receivedByName,CAST(NULL AS nvarchar(120)) replacementSerial,\r\n    JSON_QUERY(COALESCE((SELECT * FROM (\r\n        SELECT CONVERT(varchar(36),m.id)+'-created' id,'CREATED' action,'SENT' status,m.failure_cause note,m.created_by_name actorName,'' actorLogin,m.created_at occurredAt,CAST(NULL AS nvarchar(120)) replacementSerial\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-warehouse-sent','SENT_TO_WAREHOUSE','SENT',N'Solicitud enviada a Almacén',JSON_VALUE(institutional.metadata,'$.ownerDisplayName'),JSON_VALUE(institutional.metadata,'$.ownerLogin'),TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.sentUtc')),NULL WHERE TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.sentUtc')) IS NOT NULL\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-warehouse-received','RECEIVED_WAREHOUSE','RECEIVED',N'Sello institucional de recibido',JSON_VALUE(institutional.metadata,'$.receiverDisplayName'),JSON_VALUE(institutional.metadata,'$.receiverLogin'),TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.receivedUtc')),NULL WHERE TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.receivedUtc')) IS NOT NULL\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-warehouse-prepared','DISPATCHED','AUTHORIZATION',N'Equipo y serial asignados; pendiente de autorización final',JSON_VALUE(institutional.metadata,'$.warehousePreparedBy'),JSON_VALUE(institutional.metadata,'$.warehousePreparedLogin'),TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.warehousePreparedUtc')),NULL WHERE TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.warehousePreparedUtc')) IS NOT NULL\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-warehouse-decision',CASE WHEN JSON_VALUE(institutional.metadata,'$.status')='rejected' THEN 'REJECTED' ELSE 'APPROVED' END,UPPER(JSON_VALUE(institutional.metadata,'$.status')),JSON_VALUE(institutional.metadata,'$.correctionNote'),JSON_VALUE(institutional.metadata,'$.decisionBy'),'',TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.decisionUtc')),NULL WHERE JSON_VALUE(institutional.metadata,'$.status') IN('approved','rejected') AND TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.decisionUtc')) IS NOT NULL\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-warehouse-withdrawn','WITHDRAWN','DELIVERED',CONCAT(N'Equipo retirado por ',JSON_VALUE(institutional.metadata,'$.warehouseDelivery.receivedBy')),JSON_VALUE(institutional.metadata,'$.warehouseDelivery.deliveredBy'),JSON_VALUE(institutional.metadata,'$.warehouseDelivery.deliveredLogin'),TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.warehouseDelivery.at')),NULL WHERE TRY_CONVERT(datetime2,JSON_VALUE(institutional.metadata,'$.warehouseDelivery.at')) IS NOT NULL\r\n        UNION ALL SELECT CONVERT(varchar(36),delivery.id)+'-dispatch','DISPATCHED','SENT',N'Salida preparada desde el inventario de Almacén',delivery.created_by_name,'',delivery.created_at,delivery.serial_number FROM dbo.maintenance_movements delivery WHERE delivery.movement_type='NEW_DELIVERY' AND delivery.notes LIKE '%'+m.document_number+'%'\r\n        UNION ALL SELECT CONVERT(varchar(36),delivery.id)+'-delivered','DELIVERED','DELIVERED',N'Equipo entregado al técnico o departamento solicitante',mark.receiver_name,mark.receiver_login,mark.received_at,delivery.serial_number FROM dbo.maintenance_movements delivery JOIN dbo.maintenance_document_marks mark ON mark.movement_id=delivery.id WHERE delivery.movement_type='NEW_DELIVERY' AND delivery.notes LIKE '%'+m.document_number+'%' AND mark.received_at IS NOT NULL\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-delivery','SIGN_DELIVERY','SENT','Firma de quien entrega',s.signer_name,s.signer_login,s.signed_at,NULL FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='DELIVERY'\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-receipt','SIGN_RECEIPT','SENT','Firma de quien recibe',s.signer_name,s.signer_login,s.signed_at,NULL FROM dbo.maintenance_document_signatures s WHERE s.movement_id=m.id AND s.signature_role='RECEIPT'\r\n        UNION ALL SELECT CONVERT(varchar(36),m.id)+'-received','RECEIVE',CASE WHEN m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'DELIVERED' ELSE 'RECEIVED' END,CASE WHEN m.movement_type IN('EXIT','NEW_DELIVERY') THEN 'Sello de entregado aplicado' ELSE 'Sello de recibido aplicado' END,d.receiver_name,d.receiver_login,d.received_at,NULL WHERE d.received_at IS NOT NULL\r\n    ) events ORDER BY occurredAt FOR JSON PATH),'[]')) events\r\n    FROM dbo.maintenance_movements m LEFT JOIN dbo.maintenance_document_marks d ON d.movement_id=m.id OUTER APPLY(SELECT TOP 1 f.metadata FROM dbo.institutional_files f WHERE f.area='communications' AND f.channel='requirements' AND (JSON_VALUE(f.metadata,'$.originalFileName')=m.document_number+'.pdf' OR JSON_VALUE(f.metadata,'$.title') LIKE '%'+m.document_number+'%') ORDER BY f.id DESC) institutional WHERE m.id=@movement AND (\r\n    @role='Administrator'\r\n    OR (@team='WAREHOUSE' AND m.operational_area='WAREHOUSE' AND m.department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'))\r\n    OR (@team='WORKSHOP' AND (m.operational_area='WORKSHOP' OR m.movement_type='TRANSFER_TO_WORKSHOP'))\r\n    OR (@team NOT IN('WAREHOUSE','WORKSHOP','TECHNICIANS') AND @department<>'' AND m.department=@department)\r\n    OR (@team='TECHNICIANS' AND m.department=@department AND m.technician_user_id=@user)\r\n) FOR JSON PATH),'[]');", connection))
			{
				command.Parameters.AddWithValue("@code", normalized);
				command.Parameters.AddWithValue("@shortCode", shortCode);
				command.Parameters.AddWithValue("@user", userId);
				command.Parameters.AddWithValue("@role", role);
				command.Parameters.AddWithValue("@team", team ?? "");
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				text = ((string)(await command.ExecuteScalarAsync(ct))) ?? "[]";
			}
			result = text;
		}
		return result;
	}

	public async Task<string?> WarehouseRequestBoard(string role, string? team, CancellationToken ct)
	{
		if (role == "GeneralServices" && team == "TECHNICIANS")
		{
			return null;
		}
		string department = DepartmentForRole(role);
		bool all = role == "Administrator";
		bool warehouse = team == "WAREHOUSE";
		bool flag = !all;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3;
			switch (department)
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
			flag2 = !flag3;
		}
		if (flag2)
		{
			return null;
		}
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			string text;
			await using (SqlCommand command = new SqlCommand("SELECT JSON_QUERY(COALESCE((SELECT id,department,product_name productName,quantity,notes,created_by_name createdByName,created_by_login createdByLogin,created_at createdAt,status FROM dbo.maintenance_department_requests WHERE @all=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')) OR department=@dept ORDER BY created_at DESC FOR JSON PATH),'[]')) requests,\r\nJSON_QUERY(COALESCE((SELECT id,requisition_number requisitionNumber,department,product_name productName,quantity_requested quantityRequested,priority,status,notes,requested_by_name requestedByName,created_at createdAt,decided_by_login decidedByLogin,decided_by_name decidedByName,decided_at decidedAt,decision_reason decisionReason FROM dbo.maintenance_requisitions WHERE @all=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')) OR department=@dept ORDER BY created_at DESC FOR JSON PATH),'[]')) requirements FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;", connection))
			{
				command.Parameters.AddWithValue("@all", all ? 1 : 0);
				command.Parameters.AddWithValue("@warehouse", warehouse ? 1 : 0);
				command.Parameters.AddWithValue("@dept", department);
				text = await StorageFiles.ReadDataUrl((await command.ExecuteScalarAsync(ct)) as string, ct);
			}
			result = text;
		}
		return result;
	}

	public async Task<TicketActionResult> SendWarehouseRequest(WarehouseDepartmentRequest body, string role, string? team, string actor, string login, CancellationToken ct)
	{
		if (role == "GeneralServices" && team == "TECHNICIANS")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Los técnicos gestionan únicamente sus tickets asignados.");
		}
		if (role != "Administrator" && team != "WAREHOUSE")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o Administración puede enviar solicitudes a los departamentos.");
		}
		bool flag;
		switch (body.Department)
		{
		case "TECHNOLOGY":
		case "GENERAL_SERVICES":
		case "HUMAN_RESOURCES":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Las comunicaciones del Almacén solo pueden dirigirse a Tecnología, Servicios Generales o Recursos Humanos.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.maintenance_department_requests(department,product_name,quantity,notes,created_by_name,created_by_login) VALUES(@dept,@product,@quantity,@notes,@actor,@login);", connection))
			{
				command.Parameters.AddWithValue("@dept", body.Department);
				command.Parameters.AddWithValue("@product", body.ProductName.Trim());
				command.Parameters.AddWithValue("@quantity", body.Quantity);
				command.Parameters.AddWithValue("@notes", Db(body.Notes));
				command.Parameters.AddWithValue("@actor", actor);
				command.Parameters.AddWithValue("@login", login);
				await command.ExecuteNonQueryAsync(ct);
				ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> DecideWarehouseRequirement(Guid id, WarehouseDecision body, string role, string? team, string actor, string login, CancellationToken ct)
	{
		if (role != "Administrator" && team != "WAREHOUSE")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o Administración puede autorizar o rechazar requerimientos.");
		}
		bool warehouse = team == "WAREHOUSE";
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlCommand command = new SqlCommand("UPDATE dbo.maintenance_requisitions SET status=@status,decided_by_login=@login,decided_by_name=@actor,decided_at=SYSUTCDATETIME(),decision_reason=@reason,updated_at=SYSUTCDATETIME() WHERE id=@id AND status='PENDING' AND (@admin=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')));", connection))
			{
				command.Parameters.AddWithValue("@id", id);
				command.Parameters.AddWithValue("@status", body.Status);
				command.Parameters.AddWithValue("@login", login);
				command.Parameters.AddWithValue("@actor", actor);
				command.Parameters.AddWithValue("@reason", body.Reason.Trim());
				command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@warehouse", warehouse ? 1 : 0);
				ticketActionResult = ((await command.ExecuteNonQueryAsync(ct) == 1) ? new TicketActionResult(Ok: true, "OK", null) : new TicketActionResult(Ok: false, "CONFLICT", "El requerimiento ya fue decidido, no pertenece al alcance del almacén o dejó de estar pendiente."));
			}
			result = ticketActionResult;
		}
		return result;
	}

	private static async Task EnsureTemplateSchema(SqlConnection c, CancellationToken ct)
	{
		if (templateSchemaReady)
		{
			return;
		}
		await TemplateSchemaLock.WaitAsync(ct);
		try
		{
			if (!templateSchemaReady)
			{
				await using SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.maintenance_templates',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_templates(id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT(NEWID()),title nvarchar(160) NOT NULL,description nvarchar(2000) NULL,category varchar(20) NOT NULL,department varchar(40) NOT NULL,original_name nvarchar(200) NOT NULL,file_data varbinary(max) NOT NULL,file_size int NOT NULL,created_by_name nvarchar(160) NOT NULL,created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),is_archived bit NOT NULL DEFAULT(0),updated_by_name nvarchar(160) NULL,updated_at datetime2(3) NULL);", c);
				await cmd.ExecuteNonQueryAsync(ct);
				templateSchemaReady = true;
			}
		}
		finally
		{
			TemplateSchemaLock.Release();
		}
	}

	public async Task<string?> MaintenanceTemplates(string role, string? team, CancellationToken ct)
	{
		string dept = DepartmentForRole(role);
		bool all = role == "Administrator" || team == "WAREHOUSE";
		if (!all && dept == "")
		{
			return null;
		}
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureTemplateSchema(c, ct);
			string text;
			await using (SqlCommand cmd = new SqlCommand("SELECT id,title,description,category,department,original_name originalName,file_size size,created_by_name createdByName,created_at createdAt,is_archived archived,updated_by_name updatedByName,updated_at updatedAt FROM dbo.maintenance_templates WHERE (@all=1 OR (is_archived=0 AND (department='ALL' OR department=@dept))) ORDER BY created_at DESC FOR JSON PATH", c))
			{
				cmd.Parameters.AddWithValue("@all", all);
				cmd.Parameters.AddWithValue("@dept", dept);
				text = ((string)(await cmd.ExecuteScalarAsync(ct))) ?? "[]";
			}
			result = text;
		}
		return result;
	}

	public async Task<(byte[] Data, string Name)?> MaintenanceTemplateFile(Guid id, string role, string? team, CancellationToken ct)
	{
		string dept = DepartmentForRole(role);
		bool all = role == "Administrator" || team == "WAREHOUSE";
		if (!all && dept == "")
		{
			return null;
		}
		(byte[] Data, string Name)? result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureTemplateSchema(c, ct);
			(byte[] Data, string Name)? tuple3;
			await using (SqlCommand cmd = new SqlCommand("SELECT file_data,original_name FROM dbo.maintenance_templates WHERE id=@id AND (@all=1 OR (is_archived=0 AND (department='ALL' OR department=@dept)))", c))
			{
				cmd.Parameters.AddWithValue("@id", id);
				cmd.Parameters.AddWithValue("@all", all);
				cmd.Parameters.AddWithValue("@dept", dept);
				(byte[] Data, string Name)? tuple2;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					(byte[], string)? tuple = ((!(await reader.ReadAsync(ct))) ? (((byte[], string)?)null) : new (byte[], string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1))));
					tuple2 = tuple;
				}
				tuple3 = tuple2;
			}
			result = tuple3;
		}
		return result;
	}

	public async Task AddMaintenanceTemplate(string title, string description, string category, string department, string name, byte[] bytes, string actor, CancellationToken ct)
	{
		await using SqlConnection c = await Open(ct);
		await EnsureTemplateSchema(c, ct);
		await using SqlCommand cmd = new SqlCommand("INSERT INTO dbo.maintenance_templates(title,description,category,department,original_name,file_data,file_size,created_by_name) VALUES(@title,@description,@category,@dept,@name,@bytes,@size,@actor)", c);
		cmd.Parameters.AddWithValue("@title", title);
		cmd.Parameters.AddWithValue("@description", Db(description));
		cmd.Parameters.AddWithValue("@category", category);
		cmd.Parameters.AddWithValue("@dept", department);
		cmd.Parameters.AddWithValue("@name", name);
		SqlParameterCollection parameters = cmd.Parameters;
		parameters.AddWithValue("@bytes", await StorageFiles.Store(bytes, "maintenance-template", ct));
		cmd.Parameters.AddWithValue("@size", bytes.Length);
		cmd.Parameters.AddWithValue("@actor", actor);
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public async Task<bool> ArchiveMaintenanceTemplate(Guid id, bool archived, string actor, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureTemplateSchema(c, ct);
			bool flag;
			await using (SqlCommand cmd = new SqlCommand("UPDATE dbo.maintenance_templates SET is_archived=@archived,updated_by_name=@actor,updated_at=SYSUTCDATETIME() WHERE id=@id", c))
			{
				cmd.Parameters.AddWithValue("@id", id);
				cmd.Parameters.AddWithValue("@archived", archived);
				cmd.Parameters.AddWithValue("@actor", actor);
				flag = await cmd.ExecuteNonQueryAsync(ct) == 1;
			}
			result = flag;
		}
		return result;
	}

	private static byte[] PersistentSessionHash(string token)
	{
		return SHA256.HashData(Encoding.UTF8.GetBytes(token));
	}

	private async Task EnsurePersistentSessionSchema(SqlConnection connection, CancellationToken ct)
	{
		if (persistentSessionSchemaReady)
		{
			return;
		}
		await PersistentSessionSchemaLock.WaitAsync(ct);
		try
		{
			if (!persistentSessionSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.persistent_login_tokens',N'U') IS NULL\nBEGIN\n    CREATE TABLE dbo.persistent_login_tokens(\n        id uniqueidentifier NOT NULL CONSTRAINT DF_persistent_login_tokens_id DEFAULT NEWID(),\n        user_id char(36) NOT NULL,\n        token_hash varbinary(32) NOT NULL,\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_persistent_login_tokens_created DEFAULT SYSUTCDATETIME(),\n        last_used_at datetime2(3) NOT NULL CONSTRAINT DF_persistent_login_tokens_used DEFAULT SYSUTCDATETIME(),\n        revoked_at datetime2(3) NULL,\n        CONSTRAINT PK_persistent_login_tokens PRIMARY KEY(id),\n        CONSTRAINT UQ_persistent_login_tokens_hash UNIQUE(token_hash),\n        CONSTRAINT FK_persistent_login_tokens_user FOREIGN KEY(user_id) REFERENCES dbo.admin_users(id) ON DELETE CASCADE\n    );\n    CREATE INDEX IX_persistent_login_tokens_user ON dbo.persistent_login_tokens(user_id,revoked_at);\nEND", connection);
				await command.ExecuteNonQueryAsync(ct);
				persistentSessionSchemaReady = true;
			}
		}
		finally
		{
			PersistentSessionSchemaLock.Release();
		}
	}

	public async Task<string?> CreatePersistentLoginToken(string userId, CancellationToken ct, string recoveryVersion = "")
	{
		if (!Guid.TryParse(userId, out var id))
		{
			return null;
		}
		string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsurePersistentSessionSchema(connection, ct);
			await EnsureRecoverySchema(connection, ct);
			string text;
			await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.persistent_login_tokens(user_id,token_hash) SELECT @userId,@hash FROM dbo.admin_users u WITH(UPDLOCK,HOLDLOCK) WHERE u.id=@userId AND u.is_active=1 AND COALESCE((SELECT session_version FROM dbo.account_recovery r WHERE r.user_id=CONVERT(nvarchar(80),u.id)),N'')=@version;", connection))
			{
				command.Parameters.AddWithValue("@userId", id.ToString());
				command.Parameters.AddWithValue("@version", recoveryVersion);
				command.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = PersistentSessionHash(token);
				text = ((await command.ExecuteNonQueryAsync(ct) == 1) ? token : null);
			}
			result = text;
		}
		return result;
	}

	public async Task<AdminIdentity?> RestorePersistentLogin(string? token, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || !token.All(Uri.IsHexDigit))
		{
			return null;
		}
		AdminIdentity result2;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsurePersistentSessionSchema(connection, ct);
			Guid? userId = null;
			await using (SqlCommand command = new SqlCommand("UPDATE dbo.persistent_login_tokens SET last_used_at=SYSUTCDATETIME() OUTPUT inserted.user_id WHERE token_hash=@hash AND revoked_at IS NULL;", connection))
			{
				command.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = PersistentSessionHash(token);
				if (Guid.TryParse(Convert.ToString(await command.ExecuteScalarAsync(ct)), out var result))
				{
					userId = result;
				}
			}
			if (userId.HasValue)
			{
				Guid valueOrDefault = userId.GetValueOrDefault();
				AdminIdentity account = await Account(valueOrDefault, ct);
				AdminIdentity adminIdentity;
				await using (SqlCommand stillValid = new SqlCommand("SELECT COUNT(*) FROM dbo.persistent_login_tokens WHERE token_hash=@hash AND revoked_at IS NULL", connection))
				{
					stillValid.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = PersistentSessionHash(token);
					adminIdentity = ((Convert.ToInt32(await stillValid.ExecuteScalarAsync(ct)) == 1) ? account : null);
				}
				result2 = adminIdentity;
			}
			else
			{
				result2 = null;
			}
		}
		return result2;
	}

	public async Task RevokePersistentLogin(string? token, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || !token.All(Uri.IsHexDigit))
		{
			return;
		}
		await using SqlConnection connection = await Open(ct);
		await EnsurePersistentSessionSchema(connection, ct);
		await using SqlCommand command = new SqlCommand("UPDATE dbo.persistent_login_tokens SET revoked_at=COALESCE(revoked_at,SYSUTCDATETIME()) WHERE token_hash=@hash;", connection);
		command.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = PersistentSessionHash(token);
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task EnsureAgencyDirectorySchema(SqlConnection connection, CancellationToken ct)
	{
		if (agencyDirectorySchemaReady)
		{
			return;
		}
		await AgencyDirectorySchemaLock.WaitAsync(ct);
		try
		{
			if (!agencyDirectorySchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.agencies',N'U') IS NOT NULL\r\nBEGIN\r\n    IF COL_LENGTH(N'dbo.agencies',N'directory_direccion') IS NULL ALTER TABLE dbo.agencies ADD directory_direccion nvarchar(300) NULL;\r\n    IF COL_LENGTH(N'dbo.agencies',N'directory_sector') IS NULL ALTER TABLE dbo.agencies ADD directory_sector nvarchar(120) NULL;\r\n    IF COL_LENGTH(N'dbo.agencies',N'directory_municipio') IS NULL ALTER TABLE dbo.agencies ADD directory_municipio nvarchar(120) NULL;\r\n    IF COL_LENGTH(N'dbo.agencies',N'directory_provincia') IS NULL ALTER TABLE dbo.agencies ADD directory_provincia nvarchar(120) NULL;\r\nEND;", connection);
				await command.ExecuteNonQueryAsync(ct);
				await EnsureBundledAgencyLocations(connection, ct);
				agencyDirectorySchemaReady = true;
			}
		}
		finally
		{
			AgencyDirectorySchemaLock.Release();
		}
	}

	private static async Task EnsureAgencyTransitionEvidenceSchema(SqlConnection connection, CancellationToken ct)
	{
		if (agencyTransitionEvidenceSchemaReady)
		{
			return;
		}
		await AgencyTransitionEvidenceSchemaLock.WaitAsync(ct);
		try
		{
			if (!agencyTransitionEvidenceSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.agency_transition_progress',N'U') IS NOT NULL\r\n   AND OBJECT_ID(N'dbo.agency_transition_progress_evidence',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.agency_transition_progress_evidence(\r\n        id uniqueidentifier NOT NULL CONSTRAINT PK_agency_transition_progress_evidence PRIMARY KEY DEFAULT(NEWID()),\r\n        project_id uniqueidentifier NOT NULL,\r\n        stage_id uniqueidentifier NOT NULL,\r\n        progress_id uniqueidentifier NOT NULL,\r\n        file_name nvarchar(260) NOT NULL,\r\n        content_type varchar(80) NOT NULL,\r\n        photo_data varbinary(max) NOT NULL,\r\n        uploaded_by_user_id nvarchar(80) NOT NULL,\r\n        uploaded_by_name nvarchar(160) NOT NULL,\r\n        latitude decimal(9,6) NULL,\r\n        longitude decimal(10,6) NULL,\r\n        accuracy_meters decimal(8,2) NULL,\r\n        location_captured_at datetime2(3) NULL,\r\n        captured_at datetime2(3) NOT NULL CONSTRAINT DF_agency_transition_progress_evidence_captured DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT FK_agency_transition_progress_evidence_progress FOREIGN KEY(progress_id) REFERENCES dbo.agency_transition_progress(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_agency_transition_progress_evidence_progress ON dbo.agency_transition_progress_evidence(progress_id,captured_at,id);\r\nEND;\r\nIF OBJECT_ID(N'dbo.agency_transition_progress_evidence',N'U') IS NOT NULL\r\nBEGIN\r\n    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'latitude') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD latitude decimal(9,6) NULL;\r\n    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'longitude') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD longitude decimal(10,6) NULL;\r\n    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD accuracy_meters decimal(8,2) NULL;\r\n    IF COL_LENGTH(N'dbo.agency_transition_progress_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.agency_transition_progress_evidence ADD location_captured_at datetime2(3) NULL;\r\nEND;", connection);
				await command.ExecuteNonQueryAsync(ct);
				agencyTransitionEvidenceSchemaReady = true;
			}
		}
		finally
		{
			AgencyTransitionEvidenceSchemaLock.Release();
		}
	}

	private static async Task EnsureAgencyTransitionStages(SqlConnection connection, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.agency_transition_projects') AND name=N'CK_agency_transition_status' AND definition NOT LIKE '%PENDING%')\r\n    ALTER TABLE dbo.agency_transition_projects DROP CONSTRAINT CK_agency_transition_status;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.agency_transition_projects') AND name=N'CK_agency_transition_status')\r\n    ALTER TABLE dbo.agency_transition_projects WITH CHECK ADD CONSTRAINT CK_agency_transition_status CHECK(status IN('ACTIVE','PENDING','COMPLETED','CANCELLED'));\r\nINSERT INTO dbo.agency_transition_stages(project_id,department,stage_order,status,started_at,completed_at,updated_at)\r\nSELECT p.id,v.department,v.stage_order,\r\n  CASE\r\n    WHEN p.status='COMPLETED' OR\r\n      (v.stage_order=1 AND p.current_stage IN('TECHNOLOGY','HUMAN_RESOURCES','COMPLETED')) OR\r\n      (v.stage_order=2 AND p.current_stage IN('HUMAN_RESOURCES','COMPLETED')) OR\r\n      (v.stage_order=3 AND p.current_stage='COMPLETED') THEN 'COMPLETED'\r\n    WHEN p.current_stage=v.department THEN 'IN_PROGRESS'\r\n    ELSE 'PENDING'\r\n  END,\r\n  CASE WHEN p.current_stage=v.department OR p.status='COMPLETED' OR\r\n    (v.stage_order=1 AND p.current_stage IN('TECHNOLOGY','HUMAN_RESOURCES','COMPLETED')) OR\r\n    (v.stage_order=2 AND p.current_stage IN('HUMAN_RESOURCES','COMPLETED')) OR\r\n    (v.stage_order=3 AND p.current_stage='COMPLETED') THEN COALESCE(p.created_at,SYSUTCDATETIME()) ELSE NULL END,\r\n  CASE WHEN p.status='COMPLETED' OR\r\n    (v.stage_order=1 AND p.current_stage IN('TECHNOLOGY','HUMAN_RESOURCES','COMPLETED')) OR\r\n    (v.stage_order=2 AND p.current_stage IN('HUMAN_RESOURCES','COMPLETED')) OR\r\n    (v.stage_order=3 AND p.current_stage='COMPLETED') THEN COALESCE(p.completed_at,p.updated_at) ELSE NULL END,\r\n  SYSUTCDATETIME()\r\nFROM dbo.agency_transition_projects p\r\nCROSS JOIN (VALUES('GENERAL_SERVICES',CONVERT(tinyint,1)),('TECHNOLOGY',CONVERT(tinyint,2)),('HUMAN_RESOURCES',CONVERT(tinyint,3)))v(department,stage_order)\r\nWHERE p.status<>'CANCELLED'\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.agency_transition_stages s WHERE s.project_id=p.id AND s.department=v.department);", connection);
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task<(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit)> AgencyTransitionProgressCapabilities(SqlConnection connection, CancellationToken ct)
	{
		(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) result;
		await using (SqlCommand command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.agency_transition_progress',N'U') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'entry_type') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'updated_at') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'deleted_at') IS NULL THEN 0 ELSE 1 END,CASE WHEN COL_LENGTH(N'dbo.agency_transition_progress',N'deleted_by_user_id') IS NULL OR COL_LENGTH(N'dbo.agency_transition_progress',N'deleted_by_name') IS NULL THEN 0 ELSE 1 END;", connection))
		{
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) tuple;
			await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
			{
				await reader.ReadAsync(ct);
				tuple = (Exists: reader.GetInt32(0) == 1, EntryType: reader.GetInt32(1) == 1, UpdatedAt: reader.GetInt32(2) == 1, DeletedAt: reader.GetInt32(3) == 1, DeleteAudit: reader.GetInt32(4) == 1);
			}
			result = tuple;
		}
		return result;
	}

	private static async Task EnsureNotificationReceiptSchema(SqlConnection connection, CancellationToken ct)
	{
		if (notificationReceiptSchemaReady)
		{
			return;
		}
		await NotificationReceiptSchemaLock.WaitAsync(ct);
		try
		{
			if (!notificationReceiptSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.support_notification_receipts',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_notification_receipts(\r\n        user_id nvarchar(80) NOT NULL,\r\n        notification_key nvarchar(100) NOT NULL,\r\n        read_at datetime2(3) NULL,\r\n        dismissed_at datetime2(3) NULL,\r\n        updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_notification_receipts_updated DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_notification_receipts PRIMARY KEY(user_id,notification_key)\r\n    );\r\n    CREATE INDEX IX_support_notification_receipts_user ON dbo.support_notification_receipts(user_id,dismissed_at,read_at);\r\nEND;", connection);
				await command.ExecuteNonQueryAsync(ct);
				notificationReceiptSchemaReady = true;
			}
		}
		finally
		{
			NotificationReceiptSchemaLock.Release();
		}
	}

	private static async Task EnsureTicketWorkflowSchema(SqlConnection connection, CancellationToken ct)
	{
		if (ticketWorkflowSchemaReady)
		{
			return;
		}
		await TicketWorkflowSchemaLock.WaitAsync(ct);
		try
		{
			if (ticketWorkflowSchemaReady)
			{
				return;
			}
			await using (SqlCommand schemaCommand = new SqlCommand("IF COL_LENGTH(N'dbo.support_tickets',N'ticket_type') IS NULL ALTER TABLE dbo.support_tickets ADD ticket_type varchar(15) NOT NULL CONSTRAINT DF_support_tickets_type DEFAULT('SUPPORT') WITH VALUES;\r\nIF COL_LENGTH(N'dbo.support_tickets',N'assigned_technician_id') IS NULL ALTER TABLE dbo.support_tickets ADD assigned_technician_id nvarchar(80) NULL;\r\nIF COL_LENGTH(N'dbo.support_tickets',N'assigned_team') IS NULL ALTER TABLE dbo.support_tickets ADD assigned_team varchar(30) NULL;\r\nIF COL_LENGTH(N'dbo.support_tickets',N'resolved_by_user_id') IS NULL ALTER TABLE dbo.support_tickets ADD resolved_by_user_id nvarchar(80) NULL;\r\nIF COL_LENGTH(N'dbo.support_tickets',N'resolved_by_name') IS NULL ALTER TABLE dbo.support_tickets ADD resolved_by_name nvarchar(160) NULL;\r\nIF COL_LENGTH(N'dbo.support_tickets',N'resolved_at') IS NULL ALTER TABLE dbo.support_tickets ADD resolved_at datetime2(3) NULL;\r\nIF OBJECT_ID(N'dbo.support_ticket_shares',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_ticket_shares(\r\n        ticket_id uniqueidentifier NOT NULL,\r\n        department_code varchar(40) NOT NULL,\r\n        shared_by_user_id nvarchar(80) NOT NULL,\r\n        shared_by_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_shares_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_ticket_shares PRIMARY KEY(ticket_id,department_code),\r\n        CONSTRAINT FK_support_ticket_shares_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_support_ticket_shares_department ON dbo.support_ticket_shares(department_code,created_at DESC);\r\nEND;\r\nIF OBJECT_ID(N'dbo.support_ticket_history',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_ticket_history(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_support_ticket_history_id DEFAULT(NEWID()),\r\n        ticket_id uniqueidentifier NOT NULL,\r\n        action varchar(40) NOT NULL,\r\n        from_department varchar(40) NULL,\r\n        to_department varchar(40) NULL,\r\n        from_team varchar(30) NULL,\r\n        to_team varchar(30) NULL,\r\n        actor_user_id nvarchar(80) NULL,\r\n        actor_name nvarchar(160) NOT NULL,\r\n        comment nvarchar(1000) NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_history_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_ticket_history PRIMARY KEY(id),\r\n        CONSTRAINT FK_support_ticket_history_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_support_ticket_history_ticket ON dbo.support_ticket_history(ticket_id,created_at,id);\r\nEND;\r\nIF OBJECT_ID(N'dbo.support_ticket_evidence',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_ticket_evidence(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_support_ticket_evidence_id DEFAULT(NEWID()),\r\n        ticket_id uniqueidentifier NOT NULL,\r\n        file_name nvarchar(260) NOT NULL,\r\n        content_type varchar(80) NOT NULL,\r\n        photo_data varbinary(max) NOT NULL,\r\n        uploaded_by_user_id nvarchar(80) NOT NULL,\r\n        uploaded_by_name nvarchar(160) NOT NULL,\r\n        captured_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_evidence_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_ticket_evidence PRIMARY KEY(id),\r\n        CONSTRAINT FK_support_ticket_evidence_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_support_ticket_evidence_ticket ON dbo.support_ticket_evidence(ticket_id,captured_at,id);\r\nEND;\r\nIF OBJECT_ID(N'dbo.support_ticket_notifications',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_ticket_notifications(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_support_ticket_notifications_id DEFAULT(NEWID()),\r\n        ticket_id uniqueidentifier NOT NULL,\r\n        event_type varchar(30) NOT NULL,\r\n        recipient_user_id nvarchar(80) NULL,\r\n        target_department varchar(40) NULL,\r\n        message nvarchar(500) NULL,\r\n        actor_user_id nvarchar(80) NULL,\r\n        actor_name nvarchar(160) NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_ticket_notifications_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_ticket_notifications PRIMARY KEY(id),\r\n        CONSTRAINT FK_support_ticket_notifications_ticket FOREIGN KEY(ticket_id) REFERENCES dbo.support_tickets(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_support_ticket_notifications_recipient ON dbo.support_ticket_notifications(recipient_user_id,created_at DESC);\r\n    CREATE INDEX IX_support_ticket_notifications_department ON dbo.support_ticket_notifications(target_department,created_at DESC);\r\nEND;", connection))
			{
				await schemaCommand.ExecuteNonQueryAsync(ct);
			}
			await using SqlCommand backfillCommand = new SqlCommand("IF COL_LENGTH(N'dbo.support_ticket_evidence',N'latitude') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD latitude float NULL;\r\nIF COL_LENGTH(N'dbo.support_ticket_evidence',N'longitude') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD longitude float NULL;\r\nIF COL_LENGTH(N'dbo.support_ticket_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD accuracy_meters float NULL;\r\nIF COL_LENGTH(N'dbo.support_ticket_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD location_captured_at datetime2(3) NULL;\r\nIF COL_LENGTH(N'dbo.support_ticket_evidence',N'location_source') IS NULL ALTER TABLE dbo.support_ticket_evidence ADD location_source varchar(30) NULL;\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status' AND definition NOT LIKE '%PENDING%')\r\n    ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_status;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_status')\r\n    ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_status CHECK(status IN('OPEN','IN_PROGRESS','PENDING','RESOLVED','CLOSED','CANCELLED'));\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_team' AND definition NOT LIKE '%TECHNICIANS%')\r\n    ALTER TABLE dbo.support_tickets DROP CONSTRAINT CK_support_tickets_team;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'CK_support_tickets_team')\r\n    ALTER TABLE dbo.support_tickets WITH CHECK ADD CONSTRAINT CK_support_tickets_team CHECK(assigned_team IS NULL OR assigned_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'));\r\nIF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'IX_support_tickets_team_assignment')\r\n    CREATE INDEX IX_support_tickets_team_assignment ON dbo.support_tickets(assigned_department,assigned_team,status,assigned_technician_id,updated_at DESC);\r\nUPDATE dbo.support_tickets\r\nSET assigned_team=CASE\r\n    WHEN assigned_technician_id IS NOT NULL THEN 'TECHNICIANS'\r\n    WHEN EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=created_by_user_id AND creator.support_team='CALL_CENTER') THEN 'CALL_CENTER'\r\n    ELSE 'TECHNICAL_FAILURE'\r\nEND\r\nWHERE assigned_department='TECHNOLOGY' AND (assigned_team IS NULL OR assigned_team='');\r\nUPDATE t SET assigned_team='TECHNICAL_FAILURE',status=CASE WHEN t.status='OPEN' THEN 'IN_PROGRESS' ELSE t.status END,updated_at=SYSUTCDATETIME()\r\nFROM dbo.support_tickets t\r\nWHERE t.assigned_department='TECHNOLOGY' AND t.assigned_technician_id IS NULL AND ISNULL(t.assigned_team,'') NOT IN('TECHNICAL_FAILURE','CALL_CENTER','TECHNICIANS');\r\nUPDATE dbo.support_tickets SET status='IN_PROGRESS',updated_at=SYSUTCDATETIME() WHERE assigned_technician_id IS NOT NULL AND status='OPEN';\r\nUPDATE dbo.support_tickets SET resolved_at=COALESCE(closed_at,updated_at) WHERE status IN('RESOLVED','CLOSED') AND resolved_at IS NULL;\r\nINSERT INTO dbo.support_ticket_history(ticket_id,action,to_department,to_team,actor_user_id,actor_name,comment,created_at)\r\nSELECT t.id,'CREATED',t.assigned_department,t.assigned_team,t.created_by_user_id,t.created_by_name,N'Ticket existente incorporado al historial.',t.created_at\r\nFROM dbo.support_tickets t WHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id);\r\nINSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)\r\nSELECT t.id,'CREATED',t.assigned_department,N'Se abrió el ticket.',t.created_by_user_id,t.created_by_name,t.created_at\r\nFROM dbo.support_tickets t WHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type='CREATED' AND n.recipient_user_id IS NULL AND n.target_department=t.assigned_department);\r\nINSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)\r\nSELECT t.id,'CREATED',CONVERT(nvarchar(80),u.id),N'Se abrió un ticket en una agencia bajo tu supervisión.',t.created_by_user_id,t.created_by_name,t.created_at\r\nFROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id INNER JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo INNER JOIN dbo.admin_users u ON u.id=ug.user_id AND u.role='GROUP_ADMIN' AND u.is_active=1\r\nWHERE NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type='CREATED' AND n.recipient_user_id=CONVERT(nvarchar(80),u.id));\r\nINSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)\r\nSELECT t.id,t.status,t.assigned_department,CASE WHEN t.status='CLOSED' THEN N'El ticket fue cerrado.' ELSE N'El ticket fue resuelto.' END,t.resolved_by_user_id,t.resolved_by_name,COALESCE(t.resolved_at,t.closed_at,t.updated_at)\r\nFROM dbo.support_tickets t WHERE t.status IN('RESOLVED','CLOSED') AND NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type=t.status AND n.recipient_user_id IS NULL AND n.target_department=t.assigned_department);\r\nINSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)\r\nSELECT t.id,t.status,CONVERT(nvarchar(80),u.id),CASE WHEN t.status='CLOSED' THEN N'El ticket de tu agencia fue cerrado.' ELSE N'El ticket de tu agencia fue resuelto.' END,t.resolved_by_user_id,t.resolved_by_name,COALESCE(t.resolved_at,t.closed_at,t.updated_at)\r\nFROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id INNER JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo INNER JOIN dbo.admin_users u ON u.id=ug.user_id AND u.role='GROUP_ADMIN' AND u.is_active=1\r\nWHERE t.status IN('RESOLVED','CLOSED') AND NOT EXISTS(SELECT 1 FROM dbo.support_ticket_notifications n WHERE n.ticket_id=t.id AND n.event_type=t.status AND n.recipient_user_id=CONVERT(nvarchar(80),u.id));", connection);
			await backfillCommand.ExecuteNonQueryAsync(ct);
			await using SqlCommand institutionalTableCheck = new SqlCommand("SELECT OBJECT_ID(N'dbo.institutional_files',N'U')", connection);
			if (!((await institutionalTableCheck.ExecuteScalarAsync(ct)) is DBNull))
			{
				await using SqlCommand schemaCommand = new SqlCommand("UPDATE t SET ticket_type='INTERNAL',\r\n    assigned_team=CASE WHEN t.assigned_department='TECHNOLOGY' AND t.assigned_team='TECHNICAL_FAILURE' AND u.support_team='CALL_CENTER' THEN 'CALL_CENTER' ELSE t.assigned_team END,\r\n    updated_at=SYSUTCDATETIME()\r\nFROM dbo.support_tickets t\r\nJOIN dbo.institutional_files f ON TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(f.metadata)=1 THEN f.metadata ELSE N'{}' END,'$.warehouseRequest.ticketId'))=t.id\r\nLEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=t.created_by_user_id\r\nWHERE t.ticket_type='SUPPORT' AND JSON_VALUE(CASE WHEN ISJSON(f.metadata)=1 THEN f.metadata ELSE N'{}' END,'$.warehouseRequest.failureCategory') IS NOT NULL;", connection);
				await schemaCommand.ExecuteNonQueryAsync(ct);
			}
			ticketWorkflowSchemaReady = true;
		}
		finally
		{
			TicketWorkflowSchemaLock.Release();
		}
	}

	private static async Task EnsureFindingEvidenceSchema(SqlConnection connection, CancellationToken ct)
	{
		if (findingEvidenceSchemaReady)
		{
			return;
		}
		await FindingEvidenceSchemaLock.WaitAsync(ct);
		try
		{
			if (!findingEvidenceSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF COL_LENGTH(N'dbo.automatic_findings',N'diagnosis') IS NULL ALTER TABLE dbo.automatic_findings ADD diagnosis nvarchar(2000) NULL;\r\nIF COL_LENGTH(N'dbo.automatic_findings',N'verification') IS NULL ALTER TABLE dbo.automatic_findings ADD verification nvarchar(2000) NULL;\r\nIF COL_LENGTH(N'dbo.automatic_findings',N'recommendations') IS NULL ALTER TABLE dbo.automatic_findings ADD recommendations nvarchar(2000) NULL;\r\nIF COL_LENGTH(N'dbo.automatic_findings',N'assigned_technician_id') IS NULL ALTER TABLE dbo.automatic_findings ADD assigned_technician_id nvarchar(80) NULL;\r\nIF OBJECT_ID(N'dbo.finding_resolution_evidence',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.finding_resolution_evidence(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_finding_resolution_evidence_id DEFAULT(NEWID()),\r\n        finding_id uniqueidentifier NOT NULL,\r\n        file_name nvarchar(260) NOT NULL,\r\n        content_type varchar(80) NOT NULL,\r\n        photo_data varbinary(max) NOT NULL,\r\n        uploaded_by_name nvarchar(160) NOT NULL,\r\n        captured_at datetime2(3) NOT NULL CONSTRAINT DF_finding_resolution_evidence_date DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_finding_resolution_evidence PRIMARY KEY(id),\r\n        CONSTRAINT FK_finding_resolution_evidence_finding FOREIGN KEY(finding_id) REFERENCES dbo.automatic_findings(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_finding_resolution_evidence_finding ON dbo.finding_resolution_evidence(finding_id,captured_at,id);\r\nEND;\r\nIF COL_LENGTH(N'dbo.finding_resolution_evidence',N'latitude') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD latitude float NULL;\r\nIF COL_LENGTH(N'dbo.finding_resolution_evidence',N'longitude') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD longitude float NULL;\r\nIF COL_LENGTH(N'dbo.finding_resolution_evidence',N'accuracy_meters') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD accuracy_meters float NULL;\r\nIF COL_LENGTH(N'dbo.finding_resolution_evidence',N'location_captured_at') IS NULL ALTER TABLE dbo.finding_resolution_evidence ADD location_captured_at datetime2(3) NULL;\r\nIF OBJECT_ID(N'dbo.automatic_finding_notifications',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.automatic_finding_notifications(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_automatic_finding_notifications_id DEFAULT(NEWID()) PRIMARY KEY,\r\n        finding_id uniqueidentifier NOT NULL,\r\n        recipient_user_id nvarchar(80) NOT NULL,\r\n        event_type varchar(30) NOT NULL,\r\n        message nvarchar(500) NOT NULL,\r\n        actor_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_automatic_finding_notifications_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT FK_automatic_finding_notifications_finding FOREIGN KEY(finding_id) REFERENCES dbo.automatic_findings(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_automatic_finding_notifications_recipient ON dbo.automatic_finding_notifications(recipient_user_id,created_at DESC);\r\nEND;\r\nINSERT INTO dbo.automatic_finding_notifications(finding_id,recipient_user_id,event_type,message,actor_name,created_at)\r\nSELECT af.id,af.assigned_technician_id,'ASSIGNED',N'Tienes un hallazgo automático asignado: '+af.title,N'Soporte',af.updated_at\r\nFROM dbo.automatic_findings af WHERE af.assigned_technician_id IS NOT NULL\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.automatic_finding_notifications n WHERE n.finding_id=af.id AND n.recipient_user_id=af.assigned_technician_id AND n.event_type IN('ASSIGNED','REASSIGNED'));\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'CK_automatic_findings_status' AND definition NOT LIKE '%CANCELLED%')\r\n    ALTER TABLE dbo.automatic_findings DROP CONSTRAINT CK_automatic_findings_status;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'CK_automatic_findings_status')\r\n    ALTER TABLE dbo.automatic_findings WITH CHECK ADD CONSTRAINT CK_automatic_findings_status CHECK(status IN('OPEN','IN_PROGRESS','RESOLVED','CLOSED','CANCELLED'));\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'CK_automatic_findings_department' AND definition NOT LIKE '%HUMAN_RESOURCES%')\r\n    ALTER TABLE dbo.automatic_findings DROP CONSTRAINT CK_automatic_findings_department;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.automatic_findings') AND name=N'CK_automatic_findings_department')\r\n    ALTER TABLE dbo.automatic_findings WITH CHECK ADD CONSTRAINT CK_automatic_findings_department CHECK(assigned_department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));\r\nUPDATE dbo.automatic_findings SET\r\n    assigned_department='GENERAL_SERVICES',\r\n    assigned_technician_id=CASE WHEN status IN('OPEN','IN_PROGRESS') THEN NULL ELSE assigned_technician_id END,\r\n    updated_at=CASE WHEN assigned_department<>'GENERAL_SERVICES' THEN SYSUTCDATETIME() ELSE updated_at END\r\nWHERE finding_key IN('NO_INVERTER','NO_BATTERY','DAMAGE_FURNITURE','DAMAGE_PAINTING','DAMAGE_INVERTER','DAMAGE_BATTERIES','DAMAGE_RESTRUCTURING','DAMAGE_LIGHTING','DAMAGE_METALWORK','DAMAGE_GENERATOR_REQUEST','DAMAGE_ELECTRICAL','DAMAGE_SHUTTER','DAMAGE_GENERAL_OTHER') AND assigned_department<>'GENERAL_SERVICES';\r\nUPDATE dbo.automatic_findings SET\r\n    assigned_department='TECHNOLOGY',\r\n    assigned_technician_id=CASE WHEN status IN('OPEN','IN_PROGRESS') THEN NULL ELSE assigned_technician_id END,\r\n    updated_at=CASE WHEN assigned_department<>'TECHNOLOGY' THEN SYSUTCDATETIME() ELSE updated_at END\r\nWHERE finding_key IN('PRINTER_NO_MAINTENANCE','DAMAGE_WIRING','DAMAGE_SCREENS','DAMAGE_CONNECTIVITY','DAMAGE_REPORTED') AND assigned_department<>'TECHNOLOGY';", connection);
				await command.ExecuteNonQueryAsync(ct);
				findingEvidenceSchemaReady = true;
			}
		}
		finally
		{
			FindingEvidenceSchemaLock.Release();
		}
	}

	private static async Task EnsureMaintenanceSchema(SqlConnection connection, CancellationToken ct)
	{
		if (maintenanceSchemaReady)
		{
			return;
		}
		await MaintenanceSchemaLock.WaitAsync(ct);
		try
		{
			if (!maintenanceSchemaReady)
			{
				await using (SqlCommand columnsCommand = new SqlCommand("IF COL_LENGTH(N'dbo.maintenance_movements',N'operational_area') IS NULL ALTER TABLE dbo.maintenance_movements ADD operational_area varchar(20) NOT NULL CONSTRAINT DF_maintenance_operational_area DEFAULT('WORKSHOP');\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'document_number') IS NULL ALTER TABLE dbo.maintenance_movements ADD document_number varchar(40) NULL;\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'qr_token') IS NULL ALTER TABLE dbo.maintenance_movements ADD qr_token nvarchar(200) NULL;\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'delivered_by_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD delivered_by_name nvarchar(160) NULL;\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'received_by_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD received_by_name nvarchar(160) NULL;\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'destination_name') IS NULL ALTER TABLE dbo.maintenance_movements ADD destination_name nvarchar(200) NULL;\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'signature_data') IS NULL ALTER TABLE dbo.maintenance_movements ADD signature_data nvarchar(max) NULL;\r\nIF COL_LENGTH(N'dbo.maintenance_movements',N'occurred_at') IS NULL ALTER TABLE dbo.maintenance_movements ADD occurred_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_occurred_at DEFAULT(SYSUTCDATETIME());", connection))
				{
					await columnsCommand.ExecuteNonQueryAsync(ct);
				}
				await using (SqlCommand columnsCommand = new SqlCommand("UPDATE dbo.maintenance_movements SET document_number=CONCAT('LEG-',REPLACE(CONVERT(varchar(36),id),'-','')) WHERE document_number IS NULL;\r\nUPDATE dbo.maintenance_movements SET qr_token=CONCAT('REAL-MNT|',document_number) WHERE qr_token IS NULL;\r\nIF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'agency_id' AND is_nullable=0)\r\nBEGIN\r\n    IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'FK_maintenance_agency') ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT FK_maintenance_agency;\r\n    IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'IX_maintenance_agency_date') DROP INDEX IX_maintenance_agency_date ON dbo.maintenance_movements;\r\n    ALTER TABLE dbo.maintenance_movements ALTER COLUMN agency_id char(36) NULL;\r\nEND;\r\nIF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'FK_maintenance_agency') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT FK_maintenance_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id);\r\nIF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'IX_maintenance_agency_date') CREATE INDEX IX_maintenance_agency_date ON dbo.maintenance_movements(agency_id,created_at DESC);\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_type' AND definition NOT LIKE '%TRANSFER_TO_WORKSHOP%') ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_type;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_type') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_type CHECK(movement_type IN('ENTRY','EXIT','REQUEST','NEW_DELIVERY','DAMAGED_RETURN','TRANSFER_TO_WORKSHOP','REPLACEMENT','COMPONENT_REPLACEMENT','REPAIR','DISCHARGE'));\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_operational_area') ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_operational_area CHECK(operational_area IN('WORKSHOP','WAREHOUSE'));\r\nIF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'UX_maintenance_document_number') CREATE UNIQUE INDEX UX_maintenance_document_number ON dbo.maintenance_movements(document_number) WHERE document_number IS NOT NULL;\r\nIF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'UX_maintenance_qr_token') CREATE UNIQUE INDEX UX_maintenance_qr_token ON dbo.maintenance_movements(qr_token) WHERE qr_token IS NOT NULL;\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department' AND (definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%COMMERCIALIZATION%' OR definition NOT LIKE '%TREASURY%' OR definition NOT LIKE '%ACCOUNTING%' OR definition NOT LIKE '%GENERAL_AUDIT%' OR definition NOT LIKE '%VAULT%' OR definition NOT LIKE '%CASHIER%'))\r\n    ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_department;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department')\r\n    ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE','ADMINISTRATIVE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','CALL_CENTER','TECHNICAL_FAILURE','SUPERVISION'));", connection))
				{
					await columnsCommand.ExecuteNonQueryAsync(ct);
				}
				await using (SqlCommand columnsCommand = new SqlCommand("IF OBJECT_ID(N'dbo.maintenance_products',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.maintenance_products(\r\n        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_products PRIMARY KEY DEFAULT(NEWID()),\r\n        department varchar(40) NOT NULL,\r\n        scan_code nvarchar(120) NOT NULL,\r\n        product_name nvarchar(240) NOT NULL,\r\n        component_type nvarchar(100) NULL,\r\n        created_by_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_products_created DEFAULT(SYSUTCDATETIME()),\r\n        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_products_updated DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT UQ_maintenance_products_department_code UNIQUE(department,scan_code),\r\n        CONSTRAINT CK_maintenance_products_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE','ADMINISTRATIVE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','CALL_CENTER','TECHNICAL_FAILURE','SUPERVISION'))\r\n    );\r\n    CREATE INDEX IX_maintenance_products_name ON dbo.maintenance_products(department,product_name);\r\nEND;\r\nIF COL_LENGTH(N'dbo.maintenance_products',N'product_name')<480\r\n    ALTER TABLE dbo.maintenance_products ALTER COLUMN product_name nvarchar(240) NOT NULL;\r\n-- Match movement departments before seeding serials; older catalogs rejected\r\n-- warehouse/internal deliveries and blocked all maintenance reads on restart.\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_products') AND name=N'CK_maintenance_products_department' AND (definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%COMMERCIALIZATION%' OR definition NOT LIKE '%TREASURY%' OR definition NOT LIKE '%ACCOUNTING%' OR definition NOT LIKE '%GENERAL_AUDIT%' OR definition NOT LIKE '%VAULT%' OR definition NOT LIKE '%CASHIER%'))\r\n    ALTER TABLE dbo.maintenance_products DROP CONSTRAINT CK_maintenance_products_department;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_products') AND name=N'CK_maintenance_products_department')\r\n    ALTER TABLE dbo.maintenance_products WITH CHECK ADD CONSTRAINT CK_maintenance_products_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE','ADMINISTRATIVE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','CALL_CENTER','TECHNICAL_FAILURE','SUPERVISION'));", connection))
				{
					await columnsCommand.ExecuteNonQueryAsync(ct);
				}
				await using (SqlCommand columnsCommand = new SqlCommand("MERGE dbo.maintenance_products AS target\r\nUSING(\r\n    SELECT department,UPPER(LTRIM(RTRIM(serial_number))) scan_code,MAX(equipment_type) product_name,MAX(component_type) component_type,MAX(created_by_name) created_by_name\r\n    FROM dbo.maintenance_movements\r\n    WHERE serial_number IS NOT NULL AND LTRIM(RTRIM(serial_number))<>''\r\n    GROUP BY department,UPPER(LTRIM(RTRIM(serial_number)))\r\n) AS source\r\nON target.department=source.department AND target.scan_code=source.scan_code\r\nWHEN NOT MATCHED THEN INSERT(department,scan_code,product_name,component_type,created_by_name) VALUES(source.department,source.scan_code,source.product_name,source.component_type,source.created_by_name);", connection))
				{
					await columnsCommand.ExecuteNonQueryAsync(ct);
				}
				await using (SqlCommand columnsCommand = new SqlCommand("IF OBJECT_ID(N'dbo.maintenance_suppliers',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.maintenance_suppliers(\r\n        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_suppliers PRIMARY KEY DEFAULT(NEWID()),\r\n        name nvarchar(160) NOT NULL,\r\n        tax_id nvarchar(40) NULL,\r\n        contact_name nvarchar(120) NULL,\r\n        phone nvarchar(50) NULL,\r\n        email nvarchar(160) NULL,\r\n        is_active bit NOT NULL CONSTRAINT DF_maintenance_supplier_active DEFAULT(1),\r\n        created_by_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_supplier_created DEFAULT(SYSUTCDATETIME()),\r\n        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_supplier_updated DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT UQ_maintenance_supplier_name UNIQUE(name)\r\n    );\r\nEND;\r\nIF OBJECT_ID(N'dbo.maintenance_requisitions',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.maintenance_requisitions(\r\n        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_requisitions PRIMARY KEY DEFAULT(NEWID()),\r\n        requisition_number varchar(40) NOT NULL,\r\n        department varchar(40) NOT NULL,\r\n        product_name nvarchar(160) NOT NULL,\r\n        quantity_requested int NOT NULL,\r\n        quantity_fulfilled int NOT NULL CONSTRAINT DF_maintenance_requisition_fulfilled DEFAULT(0),\r\n        priority varchar(20) NOT NULL,\r\n        status varchar(20) NOT NULL CONSTRAINT DF_maintenance_requisition_status DEFAULT('PENDING'),\r\n        notes nvarchar(2000) NULL,\r\n        requested_by_user_id char(36) NOT NULL,\r\n        requested_by_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_requisition_created DEFAULT(SYSUTCDATETIME()),\r\n        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_requisition_updated DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT UQ_maintenance_requisition_number UNIQUE(requisition_number),\r\n        CONSTRAINT CK_maintenance_requisition_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),\r\n        CONSTRAINT CK_maintenance_requisition_quantity CHECK(quantity_requested>0 AND quantity_fulfilled>=0),\r\n        CONSTRAINT CK_maintenance_requisition_priority CHECK(priority IN('LOW','MEDIUM','HIGH','CRITICAL')),\r\n        CONSTRAINT CK_maintenance_requisition_status CHECK(status IN('PENDING','ORDERED','PARTIAL','FULFILLED','CANCELLED'))\r\n    );\r\n    CREATE INDEX IX_maintenance_requisition_status ON dbo.maintenance_requisitions(status,priority,created_at DESC);\r\nEND;\r\nIF OBJECT_ID(N'dbo.maintenance_purchase_orders',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.maintenance_purchase_orders(\r\n        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_purchase_orders PRIMARY KEY DEFAULT(NEWID()),\r\n        order_number varchar(40) NOT NULL,\r\n        supplier_id uniqueidentifier NOT NULL,\r\n        requisition_id uniqueidentifier NULL,\r\n        department varchar(40) NOT NULL,\r\n        product_name nvarchar(160) NOT NULL,\r\n        quantity_ordered int NOT NULL,\r\n        quantity_received int NOT NULL CONSTRAINT DF_maintenance_po_received DEFAULT(0),\r\n        unit_cost decimal(18,2) NOT NULL,\r\n        status varchar(20) NOT NULL CONSTRAINT DF_maintenance_po_status DEFAULT('ISSUED'),\r\n        expected_at datetime2(3) NULL,\r\n        notes nvarchar(2000) NULL,\r\n        created_by_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_po_created DEFAULT(SYSUTCDATETIME()),\r\n        updated_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_po_updated DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT UQ_maintenance_po_number UNIQUE(order_number),\r\n        CONSTRAINT FK_maintenance_po_supplier FOREIGN KEY(supplier_id) REFERENCES dbo.maintenance_suppliers(id),\r\n        CONSTRAINT FK_maintenance_po_requisition FOREIGN KEY(requisition_id) REFERENCES dbo.maintenance_requisitions(id),\r\n        CONSTRAINT CK_maintenance_po_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),\r\n        CONSTRAINT CK_maintenance_po_quantity CHECK(quantity_ordered>0 AND quantity_received>=0 AND quantity_received<=quantity_ordered),\r\n        CONSTRAINT CK_maintenance_po_status CHECK(status IN('DRAFT','ISSUED','PARTIAL','RECEIVED','CANCELLED'))\r\n    );\r\n    CREATE INDEX IX_maintenance_po_status ON dbo.maintenance_purchase_orders(status,created_at DESC);\r\nEND;\r\nIF OBJECT_ID(N'dbo.maintenance_returns',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.maintenance_returns(\r\n        id uniqueidentifier NOT NULL CONSTRAINT PK_maintenance_returns PRIMARY KEY DEFAULT(NEWID()),\r\n        return_number varchar(40) NOT NULL,\r\n        supplier_id uniqueidentifier NULL,\r\n        purchase_order_id uniqueidentifier NULL,\r\n        department varchar(40) NOT NULL,\r\n        product_name nvarchar(160) NOT NULL,\r\n        serial_number nvarchar(120) NOT NULL,\r\n        quantity int NOT NULL,\r\n        reason nvarchar(500) NOT NULL,\r\n        status varchar(20) NOT NULL CONSTRAINT DF_maintenance_return_status DEFAULT('REGISTERED'),\r\n        notes nvarchar(2000) NULL,\r\n        created_by_name nvarchar(160) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_maintenance_return_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT UQ_maintenance_return_number UNIQUE(return_number),\r\n        CONSTRAINT FK_maintenance_return_supplier FOREIGN KEY(supplier_id) REFERENCES dbo.maintenance_suppliers(id),\r\n        CONSTRAINT FK_maintenance_return_po FOREIGN KEY(purchase_order_id) REFERENCES dbo.maintenance_purchase_orders(id),\r\n        CONSTRAINT CK_maintenance_return_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),\r\n        CONSTRAINT CK_maintenance_return_quantity CHECK(quantity>0),\r\n        CONSTRAINT CK_maintenance_return_status CHECK(status IN('REGISTERED','SENT','CREDITED','CLOSED'))\r\n    );\r\n    CREATE INDEX IX_maintenance_return_date ON dbo.maintenance_returns(created_at DESC);\r\nEND;", connection))
				{
					await columnsCommand.ExecuteNonQueryAsync(ct);
				}
				maintenanceSchemaReady = true;
			}
		}
		finally
		{
			MaintenanceSchemaLock.Release();
		}
	}

	private static async Task EnsureUserPermissionSchema(SqlConnection connection, CancellationToken ct)
	{
		if (userPermissionSchemaReady)
		{
			return;
		}
		await UserPermissionSchemaLock.WaitAsync(ct);
		try
		{
			if (!userPermissionSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF COL_LENGTH('dbo.admin_users','permissions_json') IS NULL ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;\r\nIF COL_LENGTH('dbo.admin_users','support_team') IS NULL ALTER TABLE dbo.admin_users ADD support_team varchar(30) NULL;\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_role' AND (definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%DEPARTMENT%'))\r\n    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_role')\r\n    EXEC sys.sp_executesql N'ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role CHECK(role IN(''ADMINISTRATOR'',''VIEWER'',''GROUP_ADMIN'',''FISCALIZADOR'',''TECHNOLOGY'',''GENERAL_SERVICES'',''HUMAN_RESOURCES'',''WAREHOUSE'',''ADMINISTRATIVE'',''DEPARTMENT''))';\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team' AND (definition NOT LIKE '%TECHNICIANS%' OR definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%WORKSHOP%' OR definition NOT LIKE '%GENERAL_SERVICES%' OR definition NOT LIKE '%role=''WAREHOUSE''%' OR definition NOT LIKE '%DEPARTMENT%' OR definition NOT LIKE '%GENERAL_AUDIT%' OR definition NOT LIKE '%VAULT%' OR definition NOT LIKE '%CASHIER%' OR definition NOT LIKE '%role=''DEPARTMENT'' AND support_team IN(''TECHNOLOGY''%'))\r\n    ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_support_team;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_support_team')\r\n    EXEC sys.sp_executesql N'ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_support_team CHECK((role<>''DEPARTMENT'' AND support_team IS NULL) OR (role=''DEPARTMENT'' AND support_team IN(''TECHNOLOGY'',''GENERAL_SERVICES'',''HUMAN_RESOURCES'',''ADMINISTRATIVE'',''WAREHOUSE'',''WORKSHOP'',''COMMUNICATION'',''COMMERCIALIZATION'',''TREASURY'',''ACCOUNTING'',''GENERAL_AUDIT'',''VAULT'',''CASHIER'',''CALL_CENTER'',''TECHNICAL_FAILURE'',''SUPERVISION'')) OR (role=''TECHNOLOGY'' AND support_team IN(''CALL_CENTER'',''TECHNICAL_FAILURE'',''TECHNICIANS'',''WAREHOUSE'',''WORKSHOP'')) OR (role=''GENERAL_SERVICES'' AND support_team=''WORKSHOP'') OR (role=''WAREHOUSE'' AND support_team=''WAREHOUSE''))';\r\nIF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='jordy.arias@grupotejeda.local')\r\n    UPDATE dbo.admin_users SET email='jordy.arias@grupotejeda.local' WHERE email='yordy.arias@grupotejeda.local';\r\nUPDATE dbo.admin_users SET display_name=N'Jordy Arias' WHERE email='jordy.arias@grupotejeda.local' OR LOWER(display_name)=N'yordy arias';\r\nIF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='michael.tejeda@grupotejeda.local')\r\n    UPDATE dbo.admin_users SET email='michael.tejeda@grupotejeda.local' WHERE email='maicol.tejeda@grupotejeda.local';\r\nUPDATE dbo.admin_users SET display_name=N'Michael Tejeda' WHERE email='michael.tejeda@grupotejeda.local' OR LOWER(display_name)=N'maicol tejeda';\r\nUPDATE dbo.admin_users SET role='TECHNOLOGY',support_team=NULL,permissions_json=NULL,is_active=1,updated_at=SYSUTCDATETIME()\r\nWHERE LOWER(REPLACE(email,'@grupotejeda.local','')) IN(N'joel.vizcaino',N'eddi.bono',N'yesica.mercedes',N'franklin.calderon',N'jordaniel.ortega');\r\nUPDATE u SET role='GENERAL_SERVICES',support_team=NULL,\r\n    permissions_json=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(\r\n        CASE WHEN ISJSON(u.permissions_json)=1 THEN u.permissions_json ELSE N'{}' END,\r\n        '$.canAssignTickets',CAST(1 AS bit)),'$.canCreateTickets',CAST(1 AS bit)),'$.canResolveTickets',CAST(1 AS bit)),'$.canViewSupportDashboard',CAST(1 AS bit)),\r\n        '$.canViewSupportFindings',CAST(1 AS bit)),'$.canCancelFindingWithoutEvidence',CAST(1 AS bit)),'$.canCancelFindingWithReason',CAST(1 AS bit)),'$.canCancelFindingWithoutReason',CAST(0 AS bit)),\r\n    updated_at=CASE WHEN u.role<>'GENERAL_SERVICES' OR ISNULL(u.support_team,'')<>'' THEN SYSUTCDATETIME() ELSE u.updated_at END\r\nFROM dbo.admin_users u WHERE LOWER(u.email)=N'deliana.solis@grupotejeda.local';\r\nDECLARE @warehouseSalt varbinary(16)=0x7BDEAB3354DED475F071FA4B58B3AB62;\r\nDECLARE @warehouseHash varbinary(32)=0x2096941A8EDD017184CAA3E80BA9CC5097824ABE84149E3FD91C58F049B7F8C5;\r\nINSERT INTO dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)\r\nSELECT CONVERT(char(36),NEWID()),source.email,source.display_name,'WAREHOUSE',@warehouseSalt,@warehouseHash,210000,1,N'Almacén · '+source.position,1,source.permissions_json,'WAREHOUSE'\r\nFROM(VALUES\r\n  (N'bryan.arias@grupotejeda.local',N'Bryan Arias',N'Gerente',N'{\"canAuthorizeWarehouseRequests\":true,\"canLoadWarehouseInventory\":true}'),\r\n  (N'diorly.abreu@grupotejeda.local',N'Diorly Abreu',N'Encargada',N'{\"canAuthorizeWarehouseRequests\":true,\"canLoadWarehouseInventory\":true}'),\r\n  (N'ana.rodriguez@grupotejeda.local',N'Ana Rodriguez',N'Operadora',N'{\"canAuthorizeWarehouseRequests\":false,\"canLoadWarehouseInventory\":false}'),\r\n  (N'fernando.ortiz@grupotejeda.local',N'Fernando Ortiz',N'Operador',N'{\"canAuthorizeWarehouseRequests\":false,\"canLoadWarehouseInventory\":false}'),\r\n  (N'jordan.hellys@grupotejeda.local',N'Jordan Hellys',N'Operador',N'{\"canAuthorizeWarehouseRequests\":false,\"canLoadWarehouseInventory\":false}'),\r\n  (N'joan.matos@grupotejeda.local',N'Joan Matos',N'Operador',N'{\"canAuthorizeWarehouseRequests\":false,\"canLoadWarehouseInventory\":false}')\r\n)source(email,display_name,position,permissions_json)\r\nWHERE NOT EXISTS(SELECT 1 FROM dbo.admin_users existing WHERE LOWER(existing.email)=LOWER(source.email));\r\n-- Existing warehouse accounts may predate the warehouse role. Keep\r\n-- their identities, passwords and saved permissions while fixing\r\n-- the role/team used by QR tracking and pickup.\r\nUPDATE u SET role='WAREHOUSE',support_team='WAREHOUSE',updated_at=SYSUTCDATETIME()\r\nFROM dbo.admin_users u\r\nWHERE LOWER(u.email) IN(N'bryan.arias@grupotejeda.local',N'diorly.abreu@grupotejeda.local',N'ana.rodriguez@grupotejeda.local',N'fernando.ortiz@grupotejeda.local',N'jordan.hellys@grupotejeda.local',N'joan.matos@grupotejeda.local')\r\n  AND (u.role<>'WAREHOUSE' OR ISNULL(u.support_team,'')<>'WAREHOUSE');\r\nDECLARE @adonisPermissions nvarchar(max)=N'{\"canView\":true,\"canManage\":false,\"canManageUsers\":false,\"canManageAudits\":false,\"canViewDashboard\":false,\"canAudit\":false,\"canCreateTickets\":false,\"canAssignTickets\":false,\"canResolveTickets\":true,\"canConfigureProfile\":true,\"canExportAudits\":false,\"canViewSupportDashboard\":true,\"canViewAllSupportDepartments\":false,\"canConfigureSupport\":false,\"canViewSupportFindings\":true,\"canUseSupportChat\":true,\"canManageSupportChat\":false,\"canModerateSupportChat\":false,\"canDeleteSupportConversations\":false,\"canShareSupportConversations\":true,\"canDeleteSupportTickets\":false,\"canViewTicketNotifications\":true}';\r\nIF EXISTS(SELECT 1 FROM dbo.admin_users WHERE LOWER(email)=N'adonis.batista@grupotejeda.local')\r\n    UPDATE dbo.admin_users SET display_name=N'Adonis Batista',role='GENERAL_SERVICES',region=N'Técnicos Servicios Generales',support_team=NULL,is_active=1,permissions_json=@adonisPermissions,updated_at=SYSUTCDATETIME() WHERE LOWER(email)=N'adonis.batista@grupotejeda.local';\r\nELSE\r\n    INSERT INTO dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,must_change_password,permissions_json,support_team)\r\n    VALUES(CONVERT(char(36),NEWID()),N'adonis.batista@grupotejeda.local',N'Adonis Batista','GENERAL_SERVICES',@warehouseSalt,@warehouseHash,210000,1,N'Técnicos Servicios Generales',1,@adonisPermissions,NULL);", connection);
				await command.ExecuteNonQueryAsync(ct);
				await EnsureBryanManagerProfile(connection, ct);
				await EnsureAdministrativeProfiles(connection, ct);
				await EnsureGledysAdministrativeProfile(connection, ct);
				await EnsureLegacyAccountRecovery(connection, ct);
				await EnsureTechnicianResolutionPermissions(connection, ct);
				await RestoreWarehouseAuthorizers(connection, ct);
				await WarehouseArticleCreationAccess.EnsureGrants(connection, ct);
				await EnsureCallCenterSupportProfile(connection, ct);
				userPermissionSchemaReady = true;
			}
		}
		finally
		{
			UserPermissionSchemaLock.Release();
		}
	}

	private static Dictionary<string, bool> ReadPermissions(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new Dictionary<string, bool>();
		}
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, bool>>(json, StorageJson) ?? new Dictionary<string, bool>();
		}
		catch
		{
			return new Dictionary<string, bool>();
		}
	}

	private static string? WritePermissions(Dictionary<string, bool>? permissions)
	{
		if (permissions != null)
		{
			return JsonSerializer.Serialize(permissions, StorageJson);
		}
		return null;
	}

	public Database(IConfiguration configuration)
	{
		string text = FirstValue(configuration.GetConnectionString("DefaultConnection"), Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"), configuration["MSSQL_CONNECTION_STRING"], Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING"));
		if (string.IsNullOrWhiteSpace(text))
		{
			configurationError = "No se encontró la conexión. Agrega ConnectionStrings__DefaultConnection en MonsterASP y reinicia el sitio.";
			return;
		}
		try
		{
			text = text.Trim();
			string[] array = new string[2] { "ConnectionStrings__DefaultConnection=", "MSSQL_CONNECTION_STRING=" };
			foreach (string text2 in array)
			{
				if (text.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
				{
					text = text.Substring(text2.Length).Trim();
					break;
				}
			}
			if (text.Length >= 2)
			{
				if (text[0] == '"')
				{
					string text3 = text;
					if (text3[text3.Length - 1] == '"')
					{
						goto IL_0101;
					}
				}
				if (text[0] == '\'')
				{
					string text4 = text;
					if (text4[text4.Length - 1] == '\'')
					{
						goto IL_0101;
					}
				}
			}
			goto IL_0118;
			IL_0118:
			SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder(text);
			if (string.IsNullOrWhiteSpace(sqlConnectionStringBuilder.DataSource) || string.IsNullOrWhiteSpace(sqlConnectionStringBuilder.InitialCatalog) || string.IsNullOrWhiteSpace(sqlConnectionStringBuilder.UserID))
			{
				configurationError = "La conexión de SQL Server está incompleta: debe contener Server, Database y User Id.";
				return;
			}
			if (sqlConnectionStringBuilder.ConnectTimeout == 0)
			{
				sqlConnectionStringBuilder.ConnectTimeout = 30;
			}
			string text5 = sqlConnectionStringBuilder.DataSource.Replace(".public.databaseasp.net", ".databaseasp.net", StringComparison.OrdinalIgnoreCase);
			if (!string.Equals(text5, sqlConnectionStringBuilder.DataSource, StringComparison.OrdinalIgnoreCase))
			{
				SqlConnectionStringBuilder sqlConnectionStringBuilder2 = new SqlConnectionStringBuilder(sqlConnectionStringBuilder.ConnectionString)
				{
					DataSource = text5
				};
				connections.Add((sqlConnectionStringBuilder2.ConnectionString, "monster-internal"));
			}
			connections.Add((sqlConnectionStringBuilder.ConnectionString, sqlConnectionStringBuilder.DataSource.Contains(".public.databaseasp.net", StringComparison.OrdinalIgnoreCase) ? "monster-public" : "configured"));
			connections = connections.DistinctBy(((string Value, string Mode) item) => item.Value, StringComparer.Ordinal).ToList();
			return;
			IL_0101:
			string text6 = text;
			text = text6.Substring(1, text6.Length - 1 - 1);
			goto IL_0118;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("SQL Server connection configuration: " + ex.Message);
			configurationError = "El formato de la conexión de SQL Server no es válido. Copia la cadena completa desde el panel de MonsterASP.";
		}
	}

	private static string? FirstValue(params string?[] values)
	{
		return values.FirstOrDefault((string value) => !string.IsNullOrWhiteSpace(value));
	}

	private static object Db(object? value)
	{
		return value ?? DBNull.Value;
	}

	private static bool ReadBooleanValue(SqlDataReader reader, int index)
	{
		if (reader.IsDBNull(index))
		{
			return false;
		}
		object value = reader.GetValue(index);
		if (!(value is bool result))
		{
			if (!(value is byte b))
			{
				if (!(value is short num))
				{
					if (!(value is int num2))
					{
						if (value is long num3)
						{
							return num3 != 0;
						}
						return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
					}
					return num2 != 0;
				}
				return num != 0;
			}
			return b != 0;
		}
		return result;
	}

	private static void AddScope(SqlCommand command, string userId, string role)
	{
		command.Parameters.AddWithValue("@scoped", (role == "GroupAdministrator") ? 1 : 0);
		command.Parameters.AddWithValue("@userId", userId);
	}

	private static bool IsGlobal(string role)
	{
		switch (role)
		{
		case "Administrator":
		case "Viewer":
		case "Fiscalizador":
			return true;
		default:
			return false;
		}
	}

	private static string DepartmentForRole(string role)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			return DepartmentAccounts.SupportDepartment(role);
		}
		return role switch
		{
			"Technology" => "TECHNOLOGY", 
			"GeneralServices" => "GENERAL_SERVICES", 
			"HumanResources" => "HUMAN_RESOURCES", 
			_ => "", 
		};
	}

	private static string ClaimRole(string databaseRole)
	{
		return databaseRole switch
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
	}

	private static string? AvatarUrl(string id, bool hasAvatar)
	{
		if (!hasAvatar)
		{
			return null;
		}
		return "/api/account/avatar/" + id;
	}

	private static byte[] PasswordHash(string password, byte[] salt, int iterations)
	{
		return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
	}

	private async Task<SqlConnection> Open(CancellationToken ct)
	{
		if (configurationError != null)
		{
			throw new InvalidOperationException("MSSQL_CONFIG:" + configurationError);
		}
		Exception last = null;
		foreach (var candidate in connections)
		{
			for (int attempt = 0; attempt < 3; attempt++)
			{
				SqlConnection connection = new SqlConnection(candidate.Value);
				try
				{
					await connection.OpenAsync(ct);
					return connection;
				}
				catch (Exception ex) when (!ct.IsCancellationRequested)
				{
					last = ex;
					Console.Error.WriteLine($"SQL Server {candidate.Mode} (intento {attempt + 1}/3): {ex.Message}");
					await connection.DisposeAsync();
					if (IsTransientConnectionFailure(ex) && attempt != 2)
					{
						await Task.Delay(300 * (attempt + 1), ct);
						continue;
					}
				}
				break;
			}
		}
		throw last ?? new InvalidOperationException("MSSQL_CONFIG:No existe una conexión de SQL Server utilizable.");
	}

	private static bool IsTransientConnectionFailure(Exception error)
	{
		bool flag = error is TimeoutException;
		bool flag2 = flag;
		if (!flag2)
		{
			SqlException ex = error as SqlException;
			bool flag3 = ex != null;
			bool flag4 = flag3;
			if (flag4)
			{
				bool flag5;
				switch (ex.Number)
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
					flag5 = true;
					break;
				default:
					flag5 = false;
					break;
				}
				flag4 = flag5;
			}
			flag2 = flag4;
		}
		return flag2;
	}

	private static async Task EnsureSupportChatSchema(SqlConnection connection, CancellationToken ct)
	{
		if (supportChatSchemaReady)
		{
			return;
		}
		await SupportChatSchemaLock.WaitAsync(ct);
		try
		{
			if (!supportChatSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.support_conversations',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_conversations(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_support_conversations_id DEFAULT(NEWID()),\r\n        supervisor_user_id nvarchar(80) NOT NULL,\r\n        supervisor_name nvarchar(160) NOT NULL,\r\n        supervisor_username nvarchar(180) NOT NULL,\r\n        assigned_department varchar(40) NOT NULL,\r\n        category varchar(60) NULL,\r\n        status varchar(25) NOT NULL CONSTRAINT DF_support_conversations_status DEFAULT('WAITING_SUPPORT'),\r\n        requested_agent bit NOT NULL CONSTRAINT DF_support_conversations_agent DEFAULT(0),\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversations_created DEFAULT(SYSUTCDATETIME()),\r\n        updated_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversations_updated DEFAULT(SYSUTCDATETIME()),\r\n        last_message_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversations_last_message DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_conversations PRIMARY KEY(id),\r\n        CONSTRAINT CK_support_conversations_status CHECK(status IN('WAITING_SUPPORT','IN_PROGRESS','RESOLVED','CLOSED'))\r\n    );\r\n    CREATE INDEX IX_support_conversations_routing ON dbo.support_conversations(assigned_department,status,last_message_at DESC);\r\n    CREATE INDEX IX_support_conversations_supervisor ON dbo.support_conversations(supervisor_user_id,last_message_at DESC);\r\nEND;\r\nIF OBJECT_ID(N'dbo.support_messages',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_messages(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_support_messages_id DEFAULT(NEWID()),\r\n        conversation_id uniqueidentifier NOT NULL,\r\n        sender_user_id nvarchar(80) NULL,\r\n        sender_name nvarchar(160) NOT NULL,\r\n        sender_role varchar(40) NOT NULL,\r\n        message nvarchar(2000) NOT NULL,\r\n        is_bot bit NOT NULL CONSTRAINT DF_support_messages_bot DEFAULT(0),\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_messages_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_messages PRIMARY KEY(id),\r\n        CONSTRAINT FK_support_messages_conversation FOREIGN KEY(conversation_id) REFERENCES dbo.support_conversations(id) ON DELETE CASCADE\r\n    );\r\n    CREATE INDEX IX_support_messages_conversation ON dbo.support_messages(conversation_id,created_at,id);\r\nEND;\r\nIF COL_LENGTH('dbo.support_conversations','is_restricted') IS NULL\r\n    ALTER TABLE dbo.support_conversations ADD is_restricted bit NOT NULL CONSTRAINT DF_support_conversations_restricted DEFAULT(0);\r\nIF COL_LENGTH('dbo.support_conversations','is_blocked') IS NULL\r\n    ALTER TABLE dbo.support_conversations ADD is_blocked bit NOT NULL CONSTRAINT DF_support_conversations_blocked DEFAULT(0);\r\nIF COL_LENGTH('dbo.support_conversations','deleted_at') IS NULL\r\n    ALTER TABLE dbo.support_conversations ADD deleted_at datetime2(3) NULL;\r\nIF OBJECT_ID(N'dbo.support_conversation_controls',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_conversation_controls(\r\n        id uniqueidentifier NOT NULL CONSTRAINT DF_support_conversation_controls_id DEFAULT(NEWID()),\r\n        conversation_id uniqueidentifier NOT NULL,\r\n        action varchar(30) NOT NULL,\r\n        actor_user_id nvarchar(80) NOT NULL,\r\n        actor_role varchar(40) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversation_controls_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_conversation_controls PRIMARY KEY(id),\r\n        CONSTRAINT FK_support_conversation_controls_conversation FOREIGN KEY(conversation_id) REFERENCES dbo.support_conversations(id)\r\n    );\r\n    CREATE INDEX IX_support_conversation_controls_conversation ON dbo.support_conversation_controls(conversation_id,created_at DESC);\r\nEND;\r\nIF OBJECT_ID(N'dbo.support_conversation_mutes',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_conversation_mutes(\r\n        conversation_id uniqueidentifier NOT NULL,\r\n        user_id nvarchar(80) NOT NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_conversation_mutes_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT PK_support_conversation_mutes PRIMARY KEY(conversation_id,user_id),\r\n        CONSTRAINT FK_support_conversation_mutes_conversation FOREIGN KEY(conversation_id) REFERENCES dbo.support_conversations(id) ON DELETE CASCADE\r\n    );\r\nEND;\r\nIF OBJECT_ID(N'dbo.support_voice_notes',N'U') IS NULL\r\nBEGIN\r\n    CREATE TABLE dbo.support_voice_notes(\r\n        message_id uniqueidentifier NOT NULL PRIMARY KEY,\r\n        audio_data varbinary(max) NOT NULL,\r\n        content_type varchar(100) NOT NULL,\r\n        file_name nvarchar(180) NULL,\r\n        duration_seconds int NULL,\r\n        created_at datetime2(3) NOT NULL CONSTRAINT DF_support_voice_notes_created DEFAULT(SYSUTCDATETIME()),\r\n        CONSTRAINT FK_support_voice_notes_message FOREIGN KEY(message_id) REFERENCES dbo.support_messages(id) ON DELETE CASCADE\r\n    );\r\nEND;\r\nIF COL_LENGTH('dbo.support_messages','deleted_at') IS NULL\r\n    ALTER TABLE dbo.support_messages ADD deleted_at datetime2(3) NULL;\r\nIF COL_LENGTH('dbo.support_messages','deleted_by_user_id') IS NULL\r\n    ALTER TABLE dbo.support_messages ADD deleted_by_user_id nvarchar(80) NULL;\r\nIF COL_LENGTH('dbo.support_messages','deleted_by_name') IS NULL\r\n    ALTER TABLE dbo.support_messages ADD deleted_by_name nvarchar(160) NULL;", connection);
				await command.ExecuteNonQueryAsync(ct);
				supportChatSchemaReady = true;
			}
		}
		finally
		{
			SupportChatSchemaLock.Release();
		}
	}

	public async Task<AdminIdentity?> AuthenticateUser(string email, string password, CancellationToken ct)
	{
		AdminIdentity result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			await EnsureRecoverySchema(connection, ct);
			string id;
			string storedEmail;
			string name;
			string databaseRole;
			byte[] salt;
			byte[] hash;
			int iterations;
			bool mustChange;
			bool hasAvatar;
			string permissionsJson;
			string supportTeam;
			string recoveryVersion;
			AdminIdentity adminIdentity2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP (1) id,email,display_name,role,password_salt,password_hash,password_iterations,\r\n               must_change_password,CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,permissions_json,support_team,COALESCE((SELECT session_version FROM dbo.account_recovery r WHERE r.user_id=CONVERT(nvarchar(80),admin_users.id)),N'')\r\nFROM dbo.admin_users\r\nWHERE email=@email AND is_active=1;", connection))
			{
				command.Parameters.AddWithValue("@email", email);
				AdminIdentity adminIdentity;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					if (!(await reader.ReadAsync(ct)))
					{
						adminIdentity = null;
						goto IL_04d0;
					}
					id = reader.GetString(0);
					storedEmail = reader.GetString(1);
					name = reader.GetString(2);
					databaseRole = reader.GetString(3);
					salt = (byte[])reader[4];
					hash = (byte[])reader[5];
					iterations = reader.GetInt32(6);
					mustChange = reader.GetBoolean(7);
					hasAvatar = reader.GetInt32(8) == 1;
					permissionsJson = (reader.IsDBNull(9) ? null : reader.GetString(9));
					supportTeam = (reader.IsDBNull(10) ? null : reader.GetString(10));
					recoveryVersion = reader.GetString(11);
				}
				goto end_IL_01ec;
				IL_04d0:
				adminIdentity2 = adminIdentity;
				goto IL_05ae;
				end_IL_01ec:;
			}
			byte[] array = PasswordHash(password, salt, iterations);
			bool flag = array.Length == hash.Length && CryptographicOperations.FixedTimeEquals(array, hash);
			bool flag2 = mustChange && password == "123456";
			if (!flag && !flag2)
			{
				result = null;
			}
			else
			{
				await using (SqlCommand command = new SqlCommand("UPDATE dbo.admin_users SET last_login_at=SYSUTCDATETIME(),updated_at=SYSUTCDATETIME() WHERE id=@id;", connection))
				{
					command.Parameters.AddWithValue("@id", id);
					await command.ExecuteNonQueryAsync(ct);
				}
				result = new AdminIdentity(id, storedEmail, name, ClaimRole(databaseRole), mustChange, AvatarUrl(id, hasAvatar), ReadPermissions(permissionsJson), supportTeam)
				{
					RecoveryVersion = recoveryVersion
				};
			}
			goto end_IL_00e1;
			IL_05ae:
			result = adminIdentity2;
			end_IL_00e1:;
		}
		return result;
	}

	public async Task<AdminIdentity?> Account(Guid id, CancellationToken ct)
	{
		AdminIdentity result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			await EnsureRecoverySchema(connection, ct);
			AdminIdentity adminIdentity2;
			await using (SqlCommand command = new SqlCommand("SELECT email,display_name,role,must_change_password,CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,permissions_json,support_team,COALESCE((SELECT session_version FROM dbo.account_recovery r WHERE r.user_id=CONVERT(nvarchar(80),admin_users.id)),N'')\r\nFROM dbo.admin_users WHERE id=@id AND is_active=1;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				AdminIdentity adminIdentity;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					adminIdentity = ((await reader.ReadAsync(ct)) ? new AdminIdentity(id.ToString(), reader.GetString(0), reader.GetString(1), ClaimRole(reader.GetString(2)), reader.GetBoolean(3), AvatarUrl(id.ToString(), reader.GetInt32(4) == 1), ReadPermissions(reader.IsDBNull(5) ? null : reader.GetString(5)), reader.IsDBNull(6) ? null : reader.GetString(6))
					{
						RecoveryVersion = reader.GetString(7)
					} : null);
				}
				adminIdentity2 = adminIdentity;
			}
			result = adminIdentity2;
		}
		return result;
	}

	public async Task<List<AdminUserRow>> AdminUsers(CancellationToken ct)
	{
		List<AdminUserRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			List<AdminUserRow> users = new List<AdminUserRow>();
			await using (SqlCommand command = new SqlCommand("SELECT id,email,display_name,role,is_active,last_login_at,created_at,region,contact,\r\n       CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,must_change_password,permissions_json,support_team\r\nFROM dbo.admin_users ORDER BY role,display_name,email;", connection))
			{
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					Guid id = Guid.Parse(reader.GetString(0));
					users.Add(new AdminUserRow(id, reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.IsDBNull(5) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(5)), reader.GetDateTime(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), AvatarUrl(id.ToString(), reader.GetInt32(9) == 1), reader.GetBoolean(10), new List<string>(), 0, ReadPermissions(reader.IsDBNull(11) ? null : reader.GetString(11)), reader.IsDBNull(12) ? null : reader.GetString(12)));
				}
			}
			Dictionary<Guid, List<string>> groups = new Dictionary<Guid, List<string>>();
			Dictionary<Guid, int> agencyCounts = new Dictionary<Guid, int>();
			await using (SqlCommand command = new SqlCommand("SELECT ug.user_id,ug.group_name,COUNT(a.id) AS agency_count\r\nFROM dbo.admin_user_groups ug\r\nLEFT JOIN dbo.agencies a ON a.grupo=ug.group_name AND a.is_active=1\r\nGROUP BY ug.user_id,ug.group_name\r\nORDER BY ug.user_id,ug.group_name;", connection))
			{
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					Guid key = Guid.Parse(reader.GetString(0));
					if (!groups.TryGetValue(key, out List<string> value))
					{
						value = (groups[key] = new List<string>());
					}
					value.Add(reader.GetString(1));
					agencyCounts[key] = agencyCounts.GetValueOrDefault(key) + reader.GetInt32(2);
				}
			}
			foreach (AdminUserRow item in users)
			{
				if (!item.Permissions.ContainsKey("canManageDepartment"))
				{
					item.Permissions["canManageDepartment"] = AreaManagerAccess.Default(ClaimRole(item.Role), item.SupportTeam, item.Email);
				}
			}
			result = users.Select((AdminUserRow user) => user with
			{
				Groups = (groups.GetValueOrDefault(user.Id) ?? new List<string>()),
				AgencyCount = agencyCounts.GetValueOrDefault(user.Id)
			}).ToList();
		}
		return result;
	}

	private async Task<AdminUserRow?> AdminUser(Guid id, CancellationToken ct)
	{
		return (await AdminUsers(ct)).FirstOrDefault((AdminUserRow user) => user.Id == id);
	}

	public async Task<List<string>> AllGroupNames(CancellationToken ct)
	{
		List<string> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<string> list2;
			await using (SqlCommand command = new SqlCommand("SELECT DISTINCT grupo FROM dbo.agencies WHERE is_active=1 ORDER BY grupo;", connection))
			{
				List<string> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<string> groups = new List<string>();
					while (await reader.ReadAsync(ct))
					{
						groups.Add(reader.GetString(0));
					}
					list = groups;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<AdminUserRow> CreateAdminUser(AdminUserCreate body, CancellationToken ct)
	{
		Guid id = Guid.NewGuid();
		byte[] salt = RandomNumberGenerator.GetBytes(16);
		byte[] hash = PasswordHash(body.Password, salt, 210000);
		string email = body.Email.Trim().ToLowerInvariant();
		AdminUserRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			AdminUserRow adminUserRow;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					Guid? existingId = null;
					bool existingActive = false;
					await using (SqlCommand existing = new SqlCommand("SELECT TOP(1) id,is_active FROM dbo.admin_users WITH (UPDLOCK,HOLDLOCK) WHERE email=@email;", connection, transaction))
					{
						existing.Parameters.AddWithValue("@email", email);
						await using SqlDataReader reader = await existing.ExecuteReaderAsync(ct);
						if (await reader.ReadAsync(ct))
						{
							existingId = Guid.Parse(reader.GetString(0));
							existingActive = reader.GetBoolean(1);
						}
					}
					Guid activeId = default;
					int num;
					if (existingId.HasValue)
					{
						activeId = existingId.GetValueOrDefault();
						num = 1;
					}
					else
					{
						num = 0;
					}
					if (((uint)num & (existingActive ? 1u : 0u)) != 0)
					{
						throw new AdminUserConflictException(activeId);
					}
					id = existingId ?? id;
					await using (SqlCommand existing = new SqlCommand("IF EXISTS(SELECT 1 FROM dbo.admin_users WHERE id=@id)\r\n    UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,password_salt=@salt,password_hash=@hash,password_iterations=210000,is_active=1,region=@region,contact=@contact,must_change_password=1,permissions_json=@permissions,support_team=@supportTeam,updated_at=SYSUTCDATETIME() WHERE id=@id;\r\nELSE\r\n    INSERT INTO dbo.admin_users\r\n      (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,contact,must_change_password,permissions_json,support_team)\r\n    VALUES(@id,@email,@name,@role,@salt,@hash,210000,1,@region,@contact,1,@permissions,@supportTeam);", connection, transaction))
					{
						existing.Parameters.AddWithValue("@id", id.ToString());
						existing.Parameters.AddWithValue("@email", email);
						existing.Parameters.AddWithValue("@name", body.DisplayName.Trim());
						existing.Parameters.AddWithValue("@role", body.Role);
						existing.Parameters.AddWithValue("@salt", salt);
						existing.Parameters.AddWithValue("@hash", hash);
						existing.Parameters.AddWithValue("@region", Db(string.IsNullOrWhiteSpace(body.Region) ? null : body.Region.Trim()));
						existing.Parameters.AddWithValue("@contact", Db(string.IsNullOrWhiteSpace(body.Contact) ? null : body.Contact.Trim()));
						existing.Parameters.AddWithValue("@permissions", Db(WritePermissions(body.Permissions)));
						existing.Parameters.AddWithValue("@supportTeam", Db(((body.Role == "TECHNOLOGY" || body.Role == "GENERAL_SERVICES" || body.Role == "WAREHOUSE" || body.Role == "DEPARTMENT") && !string.IsNullOrWhiteSpace(body.SupportTeam)) ? body.SupportTeam : null));
						await existing.ExecuteNonQueryAsync(ct);
					}
					await ReplaceAssignments(connection, transaction, id, body.Role, body.Region, body.Groups, ct);
					await transaction.CommitAsync(ct);
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
				adminUserRow = await AdminUser(id, ct);
			}
			result = adminUserRow;
		}
		return result;
	}

	public async Task<AdminUserRow?> UpdateAdminUser(Guid id, AdminUserUpdate body, CancellationToken ct)
	{
		AdminUserRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			AdminUserRow adminUserRow2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					bool flag = !string.IsNullOrEmpty(body.Password);
					string cmdText = (flag ? "UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,is_active=@active,region=@region,contact=@contact,permissions_json=@permissions,support_team=@supportTeam,password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=1,updated_at=SYSUTCDATETIME() WHERE id=@id;" : "UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,is_active=@active,region=@region,contact=@contact,permissions_json=@permissions,support_team=@supportTeam,updated_at=SYSUTCDATETIME() WHERE id=@id;");
					AdminUserRow adminUserRow;
					await using (SqlCommand command = new SqlCommand(cmdText, connection, transaction))
					{
						command.Parameters.AddWithValue("@id", id.ToString());
						command.Parameters.AddWithValue("@email", body.Email.Trim().ToLowerInvariant());
						command.Parameters.AddWithValue("@name", body.DisplayName.Trim());
						command.Parameters.AddWithValue("@role", body.Role);
						command.Parameters.AddWithValue("@active", body.IsActive);
						command.Parameters.AddWithValue("@region", Db(string.IsNullOrWhiteSpace(body.Region) ? null : body.Region.Trim()));
						command.Parameters.AddWithValue("@contact", Db(string.IsNullOrWhiteSpace(body.Contact) ? null : body.Contact.Trim()));
						command.Parameters.AddWithValue("@permissions", Db(WritePermissions(body.Permissions)));
						command.Parameters.AddWithValue("@supportTeam", Db(((body.Role == "TECHNOLOGY" || body.Role == "GENERAL_SERVICES" || body.Role == "WAREHOUSE" || body.Role == "DEPARTMENT") && !string.IsNullOrWhiteSpace(body.SupportTeam)) ? body.SupportTeam : null));
						if (flag)
						{
							byte[] bytes = RandomNumberGenerator.GetBytes(16);
							command.Parameters.AddWithValue("@salt", bytes);
							command.Parameters.AddWithValue("@hash", PasswordHash(body.Password, bytes, 210000));
						}
						if (await command.ExecuteNonQueryAsync(ct) == 0)
						{
							await transaction.RollbackAsync(ct);
							adminUserRow = null;
							goto IL_067c;
						}
					}
					await ReplaceAssignments(connection, transaction, id, body.Role, body.Region, body.Groups, ct);
					await transaction.CommitAsync(ct);
					goto end_IL_0230;
					IL_067c:
					adminUserRow2 = adminUserRow;
					goto end_IL_020c;
					end_IL_0230:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
				adminUserRow2 = await AdminUser(id, ct);
				end_IL_020c:;
			}
			result = adminUserRow2;
		}
		return result;
	}

	public async Task<bool> DeleteAdminUser(Guid id, string actorEmail, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			bool flag3;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					string email = null;
					string displayName = null;
					string role = null;
					bool flag2;
					await using (SqlCommand before = new SqlCommand("SELECT TOP(1) email,display_name,role FROM dbo.admin_users WHERE id=@id AND is_active=1;", connection, transaction))
					{
						before.Parameters.AddWithValue("@id", id.ToString());
						bool flag;
						await using (SqlDataReader reader = await before.ExecuteReaderAsync(ct))
						{
							if (!(await reader.ReadAsync(ct)))
							{
								await transaction.RollbackAsync(ct);
								flag = false;
								goto IL_0541;
							}
							email = reader.GetString(0);
							displayName = reader.GetString(1);
							role = reader.GetString(2);
						}
						goto end_IL_02bb;
						IL_0541:
						flag2 = flag;
						goto IL_0618;
						end_IL_02bb:;
					}
					await using (SqlCommand before = new SqlCommand("UPDATE dbo.admin_users SET is_active=0,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
					{
						before.Parameters.AddWithValue("@id", id.ToString());
						await before.ExecuteNonQueryAsync(ct);
					}
					await using (SqlCommand before = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NOT NULL DELETE FROM dbo.pwa_push_subscriptions WHERE user_id=@id;", connection, transaction))
					{
						before.Parameters.AddWithValue("@id", id.ToString());
						await before.ExecuteNonQueryAsync(ct);
					}
					await using (SqlCommand before = new SqlCommand("IF OBJECT_ID(N'dbo.audit_log',N'U') IS NOT NULL INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,actor_email) VALUES('admin_user',@id,'USER_DEACTIVATED',@before,@actor);", connection, transaction))
					{
						before.Parameters.AddWithValue("@id", id.ToString());
						before.Parameters.AddWithValue("@before", JsonSerializer.Serialize(new { email, displayName, role }));
						before.Parameters.AddWithValue("@actor", string.IsNullOrWhiteSpace(actorEmail) ? ((IConvertible)DBNull.Value) : ((IConvertible)actorEmail));
						await before.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
					flag3 = true;
					goto end_IL_0263;
					IL_0618:
					flag3 = flag2;
					end_IL_0263:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = flag3;
		}
		return result;
	}

	private static async Task ReplaceAssignments(SqlConnection connection, SqlTransaction transaction, Guid id, string role, string? region, List<string>? requested, CancellationToken ct)
	{
		await using (SqlCommand delete = new SqlCommand("DELETE FROM dbo.admin_user_groups WHERE user_id=@id;", connection, transaction))
		{
			delete.Parameters.AddWithValue("@id", id.ToString());
			await delete.ExecuteNonQueryAsync(ct);
		}
		if (role != "GROUP_ADMIN")
		{
			return;
		}
		foreach (string item in (from value in requested ?? new List<string>()
			where !string.IsNullOrWhiteSpace(value)
			select value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
		{
			await using SqlCommand delete = new SqlCommand("INSERT INTO dbo.admin_user_groups(user_id,group_name,region)\r\nSELECT @id,@group,@region\r\nWHERE EXISTS(SELECT 1 FROM dbo.agencies WHERE grupo=@group AND is_active=1);", connection, transaction);
			delete.Parameters.AddWithValue("@id", id.ToString());
			delete.Parameters.AddWithValue("@group", item);
			delete.Parameters.AddWithValue("@region", Db(string.IsNullOrWhiteSpace(region) ? null : region.Trim()));
			await delete.ExecuteNonQueryAsync(ct);
		}
	}

	public async Task<AdminIdentity?> ChangeOwnPassword(Guid id, string currentPassword, string newPassword, CancellationToken ct)
	{
		if (!PasswordChangeValid(newPassword))
		{
			return null;
		}
		AdminIdentity result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			await EnsureRecoverySchema(connection, ct);
			AdminIdentity adminIdentity3;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string email;
				string name;
				string databaseRole;
				byte[] salt;
				byte[] hash;
				int iterations;
				bool hasAvatar;
				bool mustChange;
				Dictionary<string, bool> permissions;
				string supportTeam;
				AdminIdentity adminIdentity2;
				await using (SqlCommand command = new SqlCommand("SELECT email,display_name,role,password_salt,password_hash,password_iterations,\r\n       CASE WHEN avatar_data IS NULL THEN 0 ELSE 1 END,must_change_password,permissions_json,support_team,\r\n       COALESCE((SELECT session_version FROM dbo.account_recovery r WHERE r.user_id=CONVERT(nvarchar(80),admin_users.id)),N'')\r\nFROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND is_active=1;", connection, transaction))
				{
					command.Parameters.AddWithValue("@id", id.ToString());
					AdminIdentity adminIdentity;
					await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
					{
						if (!(await reader.ReadAsync(ct)))
						{
							adminIdentity = null;
							goto IL_05ca;
						}
						email = reader.GetString(0);
						name = reader.GetString(1);
						databaseRole = reader.GetString(2);
						salt = (byte[])reader[3];
						hash = (byte[])reader[4];
						iterations = reader.GetInt32(5);
						hasAvatar = reader.GetInt32(6) == 1;
						mustChange = reader.GetBoolean(7);
						permissions = ReadPermissions(reader.IsDBNull(8) ? null : reader.GetString(8));
						supportTeam = (reader.IsDBNull(9) ? null : reader.GetString(9));
						reader.GetString(10);
					}
					goto end_IL_02ea;
					IL_05ca:
					adminIdentity2 = adminIdentity;
					goto IL_06a8;
					end_IL_02ea:;
				}
				byte[] array = PasswordHash(currentPassword, salt, iterations);
				bool flag = array.Length == hash.Length && CryptographicOperations.FixedTimeEquals(array, hash);
				bool flag2 = mustChange && currentPassword == "123456";
				if (!flag && !flag2)
				{
					adminIdentity3 = null;
				}
				else
				{
					byte[] bytes = RandomNumberGenerator.GetBytes(16);
					byte[] value = PasswordHash(newPassword, bytes, 210000);
					await using (SqlCommand command = new SqlCommand("UPDATE dbo.admin_users SET password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=0,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
					{
						command.Parameters.AddWithValue("@salt", bytes);
						command.Parameters.AddWithValue("@hash", value);
						command.Parameters.AddWithValue("@id", id.ToString());
						await command.ExecuteNonQueryAsync(ct);
					}
					string recoveryVersion = await RotatePasswordSessions(connection, transaction, id.ToString(), ct);
					if (string.Equals(email, Environment.GetEnvironmentVariable("ADMIN_EMAIL")?.Trim(), StringComparison.OrdinalIgnoreCase))
					{
						await DisableLegacyPasswordAccess(connection, transaction, ct);
					}
					await transaction.CommitAsync(ct);
					adminIdentity3 = new AdminIdentity(id.ToString(), email, name, ClaimRole(databaseRole), MustChangePassword: false, AvatarUrl(id.ToString(), hasAvatar), permissions, supportTeam)
					{
						RecoveryVersion = recoveryVersion
					};
				}
				goto end_IL_02ab;
				IL_06a8:
				adminIdentity3 = adminIdentity2;
				end_IL_02ab:;
			}
			result = adminIdentity3;
		}
		return result;
	}

	public async Task AttachAvatar(Guid id, byte[] data, string mime, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("UPDATE dbo.admin_users SET avatar_data=@data,avatar_content_type=@mime,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection);
		SqlParameterCollection parameters = command.Parameters;
		parameters.AddWithValue("@data", await StorageFiles.Store(data, "photo", ct));
		command.Parameters.AddWithValue("@mime", mime);
		command.Parameters.AddWithValue("@id", id.ToString());
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<(byte[] Data, string Mime)?> Avatar(Guid id, CancellationToken ct)
	{
		(byte[] Data, string Mime)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			(byte[] Data, string Mime)? tuple2;
			await using (SqlCommand command = new SqlCommand("SELECT avatar_data,avatar_content_type FROM dbo.admin_users WHERE id=@id AND avatar_data IS NOT NULL;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				(byte[] Data, string Mime)? tuple;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct))
				{
					tuple = ((await reader.ReadAsync(ct)) ? new (byte[], string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1))) : (((byte[], string)?)null));
				}
				tuple2 = tuple;
			}
			result = tuple2;
		}
		return result;
	}

	public async Task<DatabaseHealth> Health(CancellationToken ct)
	{
		if (configurationError != null)
		{
			return new DatabaseHealth(Ok: false, "MSSQL_CONFIG", configurationError, null);
		}
		try
		{
			DatabaseHealth result;
			await using (SqlConnection connection = await Open(ct))
			{
				await using (SqlCommand command = new SqlCommand("SELECT 1;", connection))
				{
					await command.ExecuteScalarAsync(ct);
				}
				DatabaseHealth databaseHealth;
				await using (SqlCommand command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.agencies',N'U') IS NOT NULL\r\n              AND OBJECT_ID(N'dbo.agency_profiles',N'U') IS NOT NULL\r\n              AND COL_LENGTH(N'dbo.agencies',N'is_active') IS NOT NULL\r\n            THEN 1 ELSE 0 END;", connection))
				{
					if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0)
					{
						databaseHealth = new DatabaseHealth(Ok: false, "MSSQL_SCHEMA", "SQL Server conectó, pero falta el esquema principal o la carga de agencias.", ConnectionMode(connection.ConnectionString));
						goto IL_03aa;
					}
				}
				result = new DatabaseHealth(Ok: true, "OK", "Conexión de SQL Server activa.", ConnectionMode(connection.ConnectionString));
				goto end_IL_00e4;
				IL_03aa:
				result = databaseHealth;
				end_IL_00e4:;
			}
			return result;
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			Console.Error.WriteLine(ex);
			DatabaseDiagnostic databaseDiagnostic = DescribeConnectionError(ex);
			return new DatabaseHealth(Ok: false, databaseDiagnostic.Code, databaseDiagnostic.Message, null);
		}
	}

	private static string ConnectionMode(string value)
	{
		SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder(value);
		if (!sqlConnectionStringBuilder.DataSource.Contains(".public.databaseasp.net", StringComparison.OrdinalIgnoreCase))
		{
			if (!sqlConnectionStringBuilder.DataSource.EndsWith(".databaseasp.net", StringComparison.OrdinalIgnoreCase))
			{
				return "configured";
			}
			return "monster-internal";
		}
		return "monster-public";
	}

	public static DatabaseDiagnostic DescribeConnectionError(Exception error)
	{
		if (error is InvalidOperationException ex && ex.Message.StartsWith("MSSQL_CONFIG:", StringComparison.Ordinal))
		{
			return new DatabaseDiagnostic("MSSQL_CONFIG", ex.Message.Substring("MSSQL_CONFIG:".Length));
		}
		if (error is TimeoutException)
		{
			return new DatabaseDiagnostic("MSSQL_TIMEOUT", "SQL Server tardó demasiado en responder. Verifica el servidor y reinicia el sitio.");
		}
		if (error is SqlException ex2)
		{
			if (ex2.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) || ex2.Message.Contains("certificado", StringComparison.OrdinalIgnoreCase))
			{
				return new DatabaseDiagnostic("MSSQL_TLS", "SQL Server rechazó el certificado TLS. Usa Encrypt=True;TrustServerCertificate=True en la cadena de MonsterASP.");
			}
			switch (ex2.Number)
			{
			case 18456:
				return new DatabaseDiagnostic("MSSQL_AUTH", "SQL Server rechazó el usuario o la contraseña. Copia nuevamente la cadena desde MonsterASP.");
			case 4060:
				return new DatabaseDiagnostic("MSSQL_DATABASE", "SQL Server no pudo abrir la base indicada. Verifica Database y los permisos del usuario.");
			case 207:
			case 208:
				return new DatabaseDiagnostic("MSSQL_SCHEMA", "SQL Server conectó, pero falta una tabla o columna. Ejecuta los archivos T-SQL de configuración en orden.");
			case -2:
			case 40:
			case 53:
			case 258:
			case 11001:
				return new DatabaseDiagnostic("MSSQL_HOST", "No se pudo alcanzar SQL Server. En MonsterASP se probará primero el host interno y después el host público configurado.");
			default:
				return new DatabaseDiagnostic($"MSSQL_{ex2.Number}", "SQL Server rechazó la conexión o la consulta. Revisa el registro de la aplicación en MonsterASP.");
			}
		}
		return new DatabaseDiagnostic("MSSQL_CONNECTION", "No fue posible abrir la conexión con SQL Server. Revisa las variables del sitio y reinícialo.");
	}

	public async Task<bool> CanAccessAgency(Guid id, string userId, string role, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			bool flag;
			await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.agencies a\r\nWHERE a.id=@id AND a.is_active=1\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ));", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				AddScope(command, userId, role);
				flag = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
			}
			result = flag;
		}
		return result;
	}

	public async Task<bool> CanAccessProfile(Guid id, string userId, string role, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			bool flag;
			await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.agency_profiles p\r\nINNER JOIN dbo.agencies a ON a.id=p.agency_id\r\nWHERE p.id=@id\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ));", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				AddScope(command, userId, role);
				flag = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
			}
			result = flag;
		}
		return result;
	}

	public async Task<List<GroupRow>> Groups(string userId, string role, CancellationToken ct)
	{
		List<GroupRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<GroupRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.grupo,COUNT(*) AS pending FROM dbo.agencies a\r\nWHERE a.status='PENDING'\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\nGROUP BY a.grupo ORDER BY a.grupo;", connection))
			{
				AddScope(command, userId, role);
				List<GroupRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<GroupRow> groups = new List<GroupRow>();
					while (await reader.ReadAsync(ct))
					{
						groups.Add(new GroupRow(reader.GetString(0), reader.GetInt32(1)));
					}
					list = groups;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<GroupRow>> PanelGroups(string userId, string role, CancellationToken ct)
	{
		List<GroupRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<GroupRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.grupo,COUNT(*) FROM dbo.agencies a\r\nWHERE a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\nGROUP BY a.grupo ORDER BY a.grupo;", connection))
			{
				AddScope(command, userId, role);
				List<GroupRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<GroupRow> groups = new List<GroupRow>();
					while (await reader.ReadAsync(ct))
					{
						groups.Add(new GroupRow(reader.GetString(0), reader.GetInt32(1)));
					}
					list = groups;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<AgencyRow>> Agencies(string group, string userId, string role, CancellationToken ct)
	{
		List<AgencyRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<AgencyRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.id,a.codigo,a.terminal,a.expected_latitude,a.expected_longitude FROM dbo.agencies a\r\nWHERE a.status='PENDING' AND a.grupo=@group\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\nORDER BY a.terminal;", connection))
			{
				command.Parameters.AddWithValue("@group", group);
				AddScope(command, userId, role);
				List<AgencyRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<AgencyRow> agencies = new List<AgencyRow>();
					while (await reader.ReadAsync(ct))
					{
						agencies.Add(new AgencyRow(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? ((double?)null) : new double?(reader.GetDouble(3)), reader.IsDBNull(4) ? ((double?)null) : new double?(reader.GetDouble(4))));
					}
					list = agencies;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<DashboardAgency>> PanelAgencies(string? group, string userId, string role, CancellationToken ct)
	{
		List<DashboardAgency> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<DashboardAgency> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.codigo,a.terminal,a.grupo,a.status,\r\n       CASE WHEN a.status='COMPLETED' THEN 100\r\n            WHEN a.status='REVIEW_REQUIRED' THEN 75 ELSE 0 END\r\nFROM dbo.agencies a\r\nWHERE (@group IS NULL OR a.grupo=@group)\r\n  AND a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\nORDER BY a.terminal;", connection))
			{
				command.Parameters.Add("@group", SqlDbType.NVarChar, 120).Value = ((object)group) ?? ((object)DBNull.Value);
				AddScope(command, userId, role);
				List<DashboardAgency> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<DashboardAgency> agencies = new List<DashboardAgency>();
					while (await reader.ReadAsync(ct))
					{
						agencies.Add(new DashboardAgency(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), null));
					}
					list = agencies;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<DashboardRow> Dashboard(string userId, string role, CancellationToken ct)
	{
		DashboardRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<DashboardGroup> groups = new List<DashboardGroup>();
			int total = 0;
			int completed = 0;
			int pending = 0;
			int review = 0;
			string scopeSql = ((role == "GroupAdministrator") ? "  AND EXISTS(\r\n      SELECT 1 FROM dbo.admin_user_groups ug\r\n      WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  )" : "");
			string cmdText = "SELECT a.grupo,COUNT(*) AS total,\r\n       SUM(CASE WHEN a.status='COMPLETED' THEN 1 ELSE 0 END),\r\n       SUM(CASE WHEN a.status='PENDING' THEN 1 ELSE 0 END),\r\n       SUM(CASE WHEN a.status='REVIEW_REQUIRED' THEN 1 ELSE 0 END),\r\n       CAST(NULL AS datetime2) AS last_updated\r\nFROM dbo.agencies a\r\nWHERE a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')\r\n  " + scopeSql + "\r\nGROUP BY a.grupo ORDER BY a.grupo;";
			await using (SqlCommand command = new SqlCommand(cmdText, connection))
			{
				if (role == "GroupAdministrator")
				{
					command.Parameters.AddWithValue("@userId", userId);
				}
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					int @int = reader.GetInt32(1);
					int num = Convert.ToInt32(reader.GetValue(2));
					int num2 = Convert.ToInt32(reader.GetValue(3));
					int num3 = Convert.ToInt32(reader.GetValue(4));
					groups.Add(new DashboardGroup(reader.GetString(0), @int, num, num2, (@int != 0) ? ((int)Math.Round((double)num * 100.0 / (double)@int)) : 0, reader.IsDBNull(5) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(5))));
					total += @int;
					completed += num;
					pending += num2;
					review += num3;
				}
			}
			List<DashboardAgency> agencies = new List<DashboardAgency>();
			string cmdText2 = "SELECT a.codigo,a.terminal,a.grupo,a.status,\r\n       CASE\r\n         WHEN a.status='COMPLETED' THEN 100\r\n         WHEN a.status='REVIEW_REQUIRED' THEN 75\r\n         ELSE 0\r\n       END AS percent,\r\n       CAST(NULL AS datetime2) AS last_updated\r\nFROM dbo.agencies a\r\nWHERE a.status IN ('PENDING','COMPLETED','REVIEW_REQUIRED')\r\n  " + scopeSql + "\r\nORDER BY a.grupo,a.terminal;";
			try
			{
				await using SqlCommand command = new SqlCommand(cmdText2, connection);
				if (role == "GroupAdministrator")
				{
					command.Parameters.AddWithValue("@userId", userId);
				}
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					agencies.Add(new DashboardAgency(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.IsDBNull(5) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(5))));
				}
			}
			catch (SqlException value)
			{
				Console.Error.WriteLine($"Dashboard agencies optional query: {value}");
			}
			List<DashboardAdministrator> administrators = new List<DashboardAdministrator>();
			List<CompletedAudit> audits = new List<CompletedAudit>();
			int auditsTotal = 0;
			string cmdText3 = "SELECT COUNT(*)\r\nFROM dbo.agency_profiles p INNER JOIN dbo.agencies a ON a.id=p.agency_id\r\nWHERE 1=1\r\n  " + scopeSql + ";";
			await using (SqlCommand command = new SqlCommand(cmdText3, connection))
			{
				if (role == "GroupAdministrator")
				{
					command.Parameters.AddWithValue("@userId", userId);
				}
				auditsTotal = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
			}
			string cmdText4 = "SELECT a.codigo,a.terminal,a.grupo,p.municipio,p.provincia,p.tipo_establecimiento,\r\n       p.latitude,p.longitude,p.accuracy_meters,p.submitted_at,p.id,p.observations,p.employee_name,\r\n       COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),\r\n                (SELECT TOP (1) NULLIF(u.email,'') FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')),\r\n                (SELECT TOP (1) NULLIF(l.actor_email,'') FROM dbo.audit_log l WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED' ORDER BY l.created_at),\r\n                (SELECT TOP (1) NULLIF(u.email,'')\r\n                 FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id\r\n                 WHERE ug.group_name=a.grupo AND u.role='GROUP_ADMIN' AND u.is_active=1\r\n                 ORDER BY u.display_name,u.email))\r\nFROM dbo.agencies a INNER JOIN dbo.agency_profiles p ON p.agency_id=a.id\r\nWHERE 1=1\r\n  " + scopeSql + "\r\nORDER BY p.submitted_at DESC;";
			try
			{
				await using SqlCommand command = new SqlCommand(cmdText4, connection);
				if (role == "GroupAdministrator")
				{
					command.Parameters.AddWithValue("@userId", userId);
				}
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					audits.Add(new CompletedAudit(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDouble(6), reader.GetDouble(7), reader.GetDouble(8), DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc), Guid.Parse(reader.GetString(10)), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13)));
				}
			}
			catch (SqlException value2)
			{
				Console.Error.WriteLine($"Dashboard audits optional query: {value2}");
			}
			int percent = ((total != 0) ? ((int)Math.Round((double)completed * 100.0 / (double)total)) : 0);
			result = new DashboardRow(total, completed, pending, review, percent, groups.Count, Enumerable.Count(groups, (DashboardGroup group) => group.Percent == 100), auditsTotal, groups, agencies, administrators, audits);
		}
		return result;
	}

	public async Task<AuditDetailRow?> AuditDetail(Guid id, string userId, string role, CancellationToken ct)
	{
		AuditDetailRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			string codigo;
			string terminal;
			string grupo;
			string municipio;
			string provincia;
			string type;
			double latitude;
			double longitude;
			double accuracy;
			DateTime submittedAt;
			string observations;
			string employeeName;
			string employeeCode;
			string direccion;
			string sector;
			JsonElement answers;
			string submittedByLogin;
			AuditDetailRow auditDetailRow2;
			await using (SqlCommand command = new SqlCommand("SELECT a.codigo,a.terminal,a.grupo,p.municipio,p.provincia,p.tipo_establecimiento,p.latitude,p.longitude,\r\n       p.accuracy_meters,p.submitted_at,p.observations,p.employee_name,p.employee_code,p.direccion,p.sector,p.structural_answers,\r\n       COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),\r\n                (SELECT TOP (1) NULLIF(u.email,'') FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')),\r\n                (SELECT TOP (1) NULLIF(l.actor_email,'') FROM dbo.audit_log l WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED' ORDER BY l.created_at),\r\n                (SELECT TOP (1) NULLIF(u.email,'')\r\n                 FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id\r\n                 WHERE ug.group_name=a.grupo AND u.role='GROUP_ADMIN' AND u.is_active=1\r\n                 ORDER BY u.display_name,u.email))\r\nFROM dbo.agencies a INNER JOIN dbo.agency_profiles p ON p.agency_id=a.id\r\nWHERE p.id=@id\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ));", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				AddScope(command, userId, role);
				AuditDetailRow auditDetailRow;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					if (!(await reader.ReadAsync(ct)))
					{
						auditDetailRow = null;
						goto IL_04b9;
					}
					codigo = reader.GetString(0);
					terminal = reader.GetString(1);
					grupo = reader.GetString(2);
					municipio = reader.GetString(3);
					provincia = reader.GetString(4);
					type = reader.GetString(5);
					latitude = reader.GetDouble(6);
					longitude = reader.GetDouble(7);
					accuracy = reader.GetDouble(8);
					submittedAt = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
					observations = (reader.IsDBNull(10) ? null : reader.GetString(10));
					employeeName = (reader.IsDBNull(11) ? "Sin nombre registrado" : reader.GetString(11));
					employeeCode = (reader.IsDBNull(12) ? null : reader.GetString(12));
					direccion = reader.GetString(13);
					sector = reader.GetString(14);
					answers = NormalizeAnswers(reader.IsDBNull(15) ? "{}" : reader.GetString(15));
					submittedByLogin = (reader.IsDBNull(16) ? null : reader.GetString(16));
				}
				goto end_IL_0118;
				IL_04b9:
				auditDetailRow2 = auditDetailRow;
				goto IL_0597;
				end_IL_0118:;
			}
			List<AuditPhotoRow> photos = new List<AuditPhotoRow>();
			await using (SqlCommand command = new SqlCommand("SELECT photo_type,latitude,longitude,accuracy_meters,captured_at FROM dbo.audit_photos\r\nWHERE profile_id=@id ORDER BY CASE photo_type WHEN 'frontal' THEN 1 WHEN 'operativa' THEN 2 WHEN 'entorno' THEN 3 WHEN 'inversor' THEN 4 WHEN 'bateria' THEN 5 ELSE 6 END;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					string text = reader.GetString(0);
					photos.Add(new AuditPhotoRow(text, $"/api/images/{id}/{text}", reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3), reader.GetDateTime(4)));
				}
			}
			result = new AuditDetailRow(codigo, terminal, grupo, municipio, provincia, type, latitude, longitude, accuracy, submittedAt, id, observations, employeeName, employeeCode, direccion, sector, answers, submittedByLogin, photos);
			goto end_IL_00df;
			IL_0597:
			result = auditDetailRow2;
			end_IL_00df:;
		}
		return result;
	}

	public async Task<AuditDetailRow?> AuditDetail(Guid id, CancellationToken ct)
	{
		return await AuditDetail(id, "", "Administrator", ct);
	}

	public async Task<AuditDetailRow?> UpdateAudit(Guid id, AuditUpdate body, string actor, CancellationToken ct)
	{
		AuditDetailRow before = await AuditDetail(id, ct);
		if ((object)before == null)
		{
			return null;
		}
		AuditDetailRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			AuditDetailRow auditDetailRow;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					string agencyId;
					await using (SqlCommand find = new SqlCommand("SELECT agency_id FROM dbo.agency_profiles WITH (UPDLOCK,ROWLOCK) WHERE id=@id;", connection, transaction))
					{
						find.Parameters.AddWithValue("@id", id.ToString());
						agencyId = (string)(await find.ExecuteScalarAsync(ct));
					}
					if (agencyId != null)
					{
						await using (SqlCommand find = new SqlCommand("UPDATE dbo.agencies SET codigo=@codigo,terminal=@terminal,grupo=@grupo,status='COMPLETED',updated_at=SYSUTCDATETIME() WHERE id=@agency;", connection, transaction))
						{
							find.Parameters.AddWithValue("@codigo", body.Codigo.Trim());
							find.Parameters.AddWithValue("@terminal", body.Terminal.Trim());
							find.Parameters.AddWithValue("@grupo", body.Grupo.Trim());
							find.Parameters.AddWithValue("@agency", agencyId);
							await find.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand find = new SqlCommand("UPDATE dbo.agency_profiles SET employee_name=@employeeName,employee_code=@employeeCode,direccion=@direccion,\r\n    sector=@sector,municipio=@municipio,provincia=@provincia,latitude=@latitude,longitude=@longitude,\r\n    accuracy_meters=@accuracy,structural_answers=@answers,observations=@observations,updated_at=SYSUTCDATETIME()\r\nWHERE id=@id;", connection, transaction))
						{
							find.Parameters.AddWithValue("@employeeName", body.EmployeeName.Trim());
							find.Parameters.AddWithValue("@employeeCode", Db(string.IsNullOrWhiteSpace(body.EmployeeCode) ? null : body.EmployeeCode.Trim()));
							find.Parameters.AddWithValue("@direccion", body.Direccion.Trim());
							find.Parameters.AddWithValue("@sector", body.Sector.Trim());
							find.Parameters.AddWithValue("@municipio", body.Municipio.Trim());
							find.Parameters.AddWithValue("@provincia", body.Provincia.Trim());
							find.Parameters.AddWithValue("@latitude", body.Latitude);
							find.Parameters.AddWithValue("@longitude", body.Longitude);
							find.Parameters.AddWithValue("@accuracy", body.Accuracy);
							find.Parameters.AddWithValue("@answers", JsonSerializer.Serialize(body.Answers, StorageJson));
							find.Parameters.AddWithValue("@observations", Db(string.IsNullOrWhiteSpace(body.Observations) ? null : body.Observations.Trim()));
							find.Parameters.AddWithValue("@id", id.ToString());
							await find.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand find = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,after_data,actor_email) VALUES('agency_profile',@id,'PROFILE_CORRECTED',@before,@after,@actor);", connection, transaction))
						{
							find.Parameters.AddWithValue("@id", id.ToString());
							find.Parameters.AddWithValue("@before", JsonSerializer.Serialize(before));
							find.Parameters.AddWithValue("@after", JsonSerializer.Serialize(body));
							find.Parameters.AddWithValue("@actor", actor);
							await find.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						goto end_IL_0296;
					}
					await transaction.RollbackAsync(ct);
					auditDetailRow = null;
					goto end_IL_025e;
					end_IL_0296:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
				auditDetailRow = await AuditDetail(id, ct);
				end_IL_025e:;
			}
			result = auditDetailRow;
		}
		return result;
	}

	public async Task<bool> DeleteAudit(Guid id, string actor, CancellationToken ct)
	{
		AuditDetailRow before = await AuditDetail(id, ct);
		if ((object)before == null)
		{
			return false;
		}
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			bool flag;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					string agencyId;
					await using (SqlCommand find = new SqlCommand("SELECT agency_id FROM dbo.agency_profiles WITH (UPDLOCK,ROWLOCK) WHERE id=@id;", connection, transaction))
					{
						find.Parameters.AddWithValue("@id", id.ToString());
						agencyId = (string)(await find.ExecuteScalarAsync(ct));
					}
					if (agencyId == null)
					{
						await transaction.RollbackAsync(ct);
						flag = false;
					}
					else
					{
						await using (SqlCommand find = new SqlCommand("DELETE FROM dbo.agency_profiles WHERE id=@id;", connection, transaction))
						{
							find.Parameters.AddWithValue("@id", id.ToString());
							await find.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand find = new SqlCommand("UPDATE dbo.agencies SET status=CASE WHEN EXISTS(SELECT 1 FROM dbo.agency_profiles remaining WHERE remaining.agency_id=@agency) THEN 'COMPLETED' ELSE 'PENDING' END,updated_at=SYSUTCDATETIME() WHERE id=@agency;", connection, transaction))
						{
							find.Parameters.AddWithValue("@agency", agencyId);
							await find.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand find = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,actor_email) VALUES('agency_profile',@id,'PROFILE_DELETED_AND_AGENCY_REOPENED',@before,@actor);", connection, transaction))
						{
							find.Parameters.AddWithValue("@id", id.ToString());
							find.Parameters.AddWithValue("@before", JsonSerializer.Serialize(before));
							find.Parameters.AddWithValue("@actor", actor);
							await find.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						flag = true;
					}
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = flag;
		}
		return result;
	}

	public async Task<Guid> Submit(Guid agency, Submit body, string? submittedByUserId, string? submittedByLogin, string? submittedByName, string? ip, string? agent, CancellationToken ct)
	{
		Guid result;
		await using (SqlConnection connection = await Open(ct))
		{
			Guid guid2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					string canonicalLogin = submittedByLogin?.Trim().ToLowerInvariant();
					string canonicalName = submittedByName?.Trim();
					if (!string.IsNullOrWhiteSpace(submittedByUserId))
					{
						await using SqlCommand submitter = new SqlCommand("SELECT TOP (1) email,display_name FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@userId AND is_active=1;", connection, transaction);
						submitter.Parameters.AddWithValue("@userId", submittedByUserId);
						await using SqlDataReader submitterReader = await submitter.ExecuteReaderAsync(ct);
						if (await submitterReader.ReadAsync(ct))
						{
							canonicalLogin = submitterReader.GetString(0).Trim().ToLowerInvariant();
							canonicalName = submitterReader.GetString(1).Trim();
						}
					}
					string status = null;
					double? expectedLatitude = null;
					double? expectedLongitude = null;
					await using (SqlCommand submitter = new SqlCommand("SELECT status,expected_latitude,expected_longitude FROM dbo.agencies WITH (UPDLOCK,ROWLOCK) WHERE id=@id AND is_active=1;", connection, transaction))
					{
						submitter.Parameters.AddWithValue("@id", agency.ToString());
						await using SqlDataReader submitterReader = await submitter.ExecuteReaderAsync(ct);
						if (await submitterReader.ReadAsync(ct))
						{
							status = submitterReader.GetString(0);
							expectedLatitude = (submitterReader.IsDBNull(1) ? ((double?)null) : new double?(submitterReader.GetDouble(1)));
							expectedLongitude = (submitterReader.IsDBNull(2) ? ((double?)null) : new double?(submitterReader.GetDouble(2)));
						}
					}
					if (status == null || status != "PENDING" && status != "COMPLETED" && status != "REVIEW_REQUIRED")
					{
						throw new AgencyConflict();
					}
					Guid id = Guid.NewGuid();
					Geo geolocation = body.Geolocation;
					double? num = ((expectedLatitude.HasValue && expectedLongitude.HasValue) ? new double?(Haversine(geolocation.Latitude, geolocation.Longitude, expectedLatitude.Value, expectedLongitude.Value)) : ((double?)null));
					string value = JsonSerializer.Serialize(new
					{
						id = id,
						agencyId = agency,
						completionPercent = 100
					});
					Guid guid;
					await using (SqlCommand insert = new SqlCommand("INSERT INTO dbo.agency_profiles\r\n  (id,agency_id,employee_name,employee_code,direccion,sector,municipio,provincia,tipo_establecimiento,\r\n   tipo_establecimiento_otro,latitude,longitude,accuracy_meters,distance_from_agency_meters,\r\n   location_captured_at,location_source,structural_answers,observations,completion_percent,submitted_by_context)\r\nVALUES(@id,@agency,@employeeName,@employeeCode,@direccion,@sector,@municipio,@provincia,@tipo,@otro,@lat,@lng,\r\n       @accuracy,@distance,@captured,@source,@answers,@observations,100,@context);\r\nUPDATE dbo.agencies SET status='COMPLETED',updated_at=SYSUTCDATETIME() WHERE id=@agency;\r\nINSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email) VALUES('agency_profile',@id,'PROFILE_CREATED',@after,@login);", connection, transaction))
					{
						insert.Parameters.AddWithValue("@id", id.ToString());
						insert.Parameters.AddWithValue("@agency", agency.ToString());
						insert.Parameters.AddWithValue("@employeeName", body.EmployeeName.Trim());
						insert.Parameters.AddWithValue("@employeeCode", Db(string.IsNullOrWhiteSpace(body.EmployeeCode) ? null : body.EmployeeCode.Trim()));
						insert.Parameters.AddWithValue("@direccion", body.Direccion.Trim());
						insert.Parameters.AddWithValue("@sector", body.Sector.Trim());
						insert.Parameters.AddWithValue("@municipio", body.Municipio.Trim());
						insert.Parameters.AddWithValue("@provincia", body.Provincia.Trim());
						insert.Parameters.AddWithValue("@tipo", body.TipoEstablecimiento);
						insert.Parameters.AddWithValue("@otro", DBNull.Value);
						insert.Parameters.AddWithValue("@lat", geolocation.Latitude);
						insert.Parameters.AddWithValue("@lng", geolocation.Longitude);
						insert.Parameters.AddWithValue("@accuracy", geolocation.AccuracyMeters);
						insert.Parameters.AddWithValue("@distance", Db(num));
						insert.Parameters.AddWithValue("@captured", geolocation.CapturedAt.Value.UtcDateTime);
						insert.Parameters.AddWithValue("@source", geolocation.Source);
						insert.Parameters.AddWithValue("@answers", JsonSerializer.Serialize(body.Answers, StorageJson));
						insert.Parameters.AddWithValue("@observations", Db(string.IsNullOrWhiteSpace(body.Observations) ? null : body.Observations.Trim()));
						insert.Parameters.AddWithValue("@context", JsonSerializer.Serialize(new
						{
							userId = submittedByUserId,
							login = canonicalLogin,
							displayName = canonicalName,
							ip = ip,
							userAgent = agent,
							submittedAt = DateTimeOffset.UtcNow
						}));
						insert.Parameters.AddWithValue("@after", value);
						insert.Parameters.AddWithValue("@login", Db(canonicalLogin));
						await insert.ExecuteNonQueryAsync(ct);
						StructuralAnswers answers = body.Answers;
						List<(string, string, string, string, string)> list = new List<(string, string, string, string, string)>();
						if (answers.Painted == "no")
						{
							list.Add(("NOT_PAINTED", "Agencia no pintada", "La agencia no cumple con la pintura requerida.", "GENERAL_SERVICES", "LOW"));
						}
						if (answers.RazaSticker == "no")
						{
							list.Add(("NO_RAZA_STICKER", "Falta sticker de Raza", "No fue colocado el sticker de Raza.", "GENERAL_SERVICES", "LOW"));
						}
						if (answers.RealSticker == "no")
						{
							list.Add(("NO_REAL_STICKER", "Falta sticker de Real", "No fue colocado el sticker de Real.", "GENERAL_SERVICES", "LOW"));
						}
						if (answers.LotekaRemoved == "no")
						{
							list.Add(("LOTEKA_NOT_REMOVED", "Publicidad de Loteka pendiente", "No fue retirada toda la publicidad de Loteka.", "GENERAL_SERVICES", "MEDIUM"));
						}
						if (answers.PrinterMaintained == "no")
						{
							list.Add(("PRINTER_NO_MAINTENANCE", "Impresora sin mantenimiento", "La impresora no recibió mantenimiento; es indispensable para imprimir tickets.", "TECHNOLOGY", "CRITICAL"));
						}
						if (answers.InverterPresent == "no")
						{
							list.Add(("NO_INVERTER", "Agencia sin inversor", "La agencia no tiene inversor.", "GENERAL_SERVICES", "HIGH"));
						}
						if (answers.BatteryPresent == "no")
						{
							list.Add(("NO_BATTERY", "Agencia sin batería", "La agencia no tiene batería.", "GENERAL_SERVICES", "HIGH"));
						}
						Dictionary<string, (string, string, string, string, string)> dictionary = new Dictionary<string, (string, string, string, string, string)>(StringComparer.OrdinalIgnoreCase)
						{
							["WIRING"] = ("DAMAGE_WIRING", "Falla de cableado de datos", "Se identificó una avería en el cableado tecnológico o de red.", "TECHNOLOGY", "HIGH"),
							["SCREENS"] = ("DAMAGE_SCREENS", "Falla de pantallas", "Se identificó una avería en pantallas o monitores.", "TECHNOLOGY", "HIGH"),
							["CONNECTIVITY"] = ("DAMAGE_CONNECTIVITY", "Falla de conectividad", "La conectividad impide o compromete la operación.", "TECHNOLOGY", "CRITICAL"),
							["FURNITURE"] = ("DAMAGE_FURNITURE", "Reestructuración física / Sheetrock", "Se identificó una avería histórica en el mobiliario o la infraestructura física.", "GENERAL_SERVICES", "MEDIUM"),
							["GENERAL_PAINTING"] = ("DAMAGE_PAINTING", "Pintura", "Se identificó una avería o necesidad de pintura.", "GENERAL_SERVICES", "LOW"),
							["GENERAL_INVERTER"] = ("DAMAGE_INVERTER", "Inversor", "Se identificó una avería en el inversor.", "GENERAL_SERVICES", "HIGH"),
							["GENERAL_BATTERIES"] = ("DAMAGE_BATTERIES", "Baterías", "Se identificó una avería o deterioro en las baterías.", "GENERAL_SERVICES", "HIGH"),
							["GENERAL_RESTRUCTURING"] = ("DAMAGE_RESTRUCTURING", "Reestructuración física / Sheetrock", "Se identificó una avería que requiere reestructuración física o trabajo de sheetrock.", "GENERAL_SERVICES", "MEDIUM"),
							["GENERAL_LIGHTING"] = ("DAMAGE_LIGHTING", "Iluminación", "Se identificó una falla de iluminación.", "GENERAL_SERVICES", "MEDIUM"),
							["GENERAL_METALWORK"] = ("DAMAGE_METALWORK", "Herrería", "Se identificó una avería que requiere trabajo de herrería.", "GENERAL_SERVICES", "MEDIUM"),
							["GENERAL_GENERATOR_REQUEST"] = ("DAMAGE_GENERATOR_REQUEST", "Requisición planta", "Se identificó la necesidad de requisición de una planta eléctrica.", "GENERAL_SERVICES", "HIGH"),
							["GENERAL_ELECTRICAL"] = ("DAMAGE_ELECTRICAL", "Avería eléctrica", "Se identificó una avería eléctrica.", "GENERAL_SERVICES", "CRITICAL"),
							["GENERAL_SHUTTER"] = ("DAMAGE_SHUTTER", "Falla en el shutter", "Se identificó una falla en el shutter.", "GENERAL_SERVICES", "HIGH"),
							["GENERAL_OTHER"] = ("DAMAGE_GENERAL_OTHER", "Otro servicio general", "Se identificó otra avería correspondiente a Servicios Generales.", "GENERAL_SERVICES", "MEDIUM")
						};
						if (answers.DamageFound == "yes")
						{
							foreach (string item in answers.DamageTypes ?? new List<string>())
							{
								if (dictionary.TryGetValue(item, out var value2))
								{
									list.Add((value2.Item1, value2.Item2, value2.Item3, value2.Item4, value2.Item5));
								}
							}
						}
						foreach (var item2 in list)
						{
							await using SqlCommand submitter = new SqlCommand("INSERT INTO dbo.automatic_findings(id,profile_id,agency_id,finding_key,title,detail,assigned_department,priority) VALUES(NEWID(),@profile,@agency,@key,@title,@detail,@department,@priority);", connection, transaction);
							submitter.Parameters.AddWithValue("@profile", id.ToString());
							submitter.Parameters.AddWithValue("@agency", agency.ToString());
							submitter.Parameters.AddWithValue("@key", item2.Item1);
							submitter.Parameters.AddWithValue("@title", item2.Item2);
							submitter.Parameters.AddWithValue("@detail", item2.Item3);
							submitter.Parameters.AddWithValue("@department", item2.Item4);
							submitter.Parameters.AddWithValue("@priority", item2.Item5);
							await submitter.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						guid = id;
					}
					guid2 = guid;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = guid2;
		}
		return result;
	}

	private static double Haversine(double lat1, double lon1, double lat2, double lon2)
	{
		double num = (lat2 - lat1) * Math.PI / 180.0;
		double num2 = (lon2 - lon1) * Math.PI / 180.0;
		double num3 = Math.Sin(num / 2.0) * Math.Sin(num / 2.0) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(num2 / 2.0) * Math.Sin(num2 / 2.0);
		return Math.Round(12742000.0 * Math.Atan2(Math.Sqrt(num3), Math.Sqrt(1.0 - num3)), 1);
	}

	private static JsonElement NormalizeAnswers(string json)
	{
		using (JsonDocument jsonDocument = JsonDocument.Parse(json))
		{
			JsonElement rootElement = jsonDocument.RootElement;
			return JsonSerializer.SerializeToElement(new Dictionary<string, string>
			{
				["painted"] = Read(rootElement, "painted", "Painted"),
				["razaSticker"] = Read(rootElement, "razaSticker", "RazaSticker"),
				["realSticker"] = Read(rootElement, "realSticker", "RealSticker"),
				["lotekaRemoved"] = Read(rootElement, "lotekaRemoved", "LotekaRemoved"),
				["damageFound"] = Read(rootElement, "damageFound", "DamageFound"),
				["printerMaintained"] = Read(rootElement, "printerMaintained", "PrinterMaintained"),
				["inverterPresent"] = Read(rootElement, "inverterPresent", "InverterPresent"),
				["batteryPresent"] = Read(rootElement, "batteryPresent", "BatteryPresent"),
				["cashBoxType"] = Read(rootElement, "cashBoxType", "CashBoxType")
			});
		}
		static string? Read(JsonElement source, string camel, string pascal)
		{
			if (source.ValueKind != JsonValueKind.Object)
			{
				return null;
			}
			if (source.TryGetProperty(camel, out var value) && value.ValueKind == JsonValueKind.String)
			{
				return value.GetString();
			}
			if (source.TryGetProperty(pascal, out var value2) && value2.ValueKind == JsonValueKind.String)
			{
				return value2.GetString();
			}
			return null;
		}
	}

	public async Task<bool> PhotoMatchesProfile(Guid id, double lat, double lng, double accuracy, string photoType, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			bool flag2;
			await using (SqlCommand command = new SqlCommand("SELECT latitude,longitude,accuracy_meters FROM dbo.agency_profiles WHERE id=@id;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				bool flag;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					flag = await reader.ReadAsync(ct) && FieldAuditGps.Matches(lat, lng, accuracy, reader.GetDouble(0), reader.GetDouble(1), reader.GetDouble(2), photoType is "frontal" or "operativa" or "entorno" ? 3000.0 : FieldAuditGps.MatchRadiusMeters);
				}
				flag2 = flag;
			}
			result = flag2;
		}
		return result;
	}

	public async Task AttachPhoto(Guid id, string type, byte[] data, string mime, double lat, double lng, double accuracy, DateTime captured, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("IF EXISTS(SELECT 1 FROM dbo.audit_photos WHERE profile_id=@id AND photo_type=@type)\r\n    UPDATE dbo.audit_photos SET photo_data=@data,content_type=@mime,latitude=@lat,longitude=@lng,accuracy_meters=@accuracy,captured_at=@captured,created_at=SYSUTCDATETIME() WHERE profile_id=@id AND photo_type=@type;\r\nELSE\r\n    INSERT INTO dbo.audit_photos(id,profile_id,photo_type,photo_data,content_type,latitude,longitude,accuracy_meters,captured_at)\r\n    VALUES(@photoId,@id,@type,@data,@mime,@lat,@lng,@accuracy,@captured);", connection);
		command.Parameters.AddWithValue("@photoId", Guid.NewGuid().ToString());
		command.Parameters.AddWithValue("@id", id.ToString());
		command.Parameters.AddWithValue("@type", type);
		SqlParameterCollection parameters = command.Parameters;
		parameters.AddWithValue("@data", await StorageFiles.Store(data, "photo", ct));
		command.Parameters.AddWithValue("@mime", mime);
		command.Parameters.AddWithValue("@lat", lat);
		command.Parameters.AddWithValue("@lng", lng);
		command.Parameters.AddWithValue("@accuracy", accuracy);
		command.Parameters.AddWithValue("@captured", captured);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<(byte[] Data, string Mime)?> Photo(Guid id, CancellationToken ct)
	{
		(byte[] Data, string Mime)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			(byte[] Data, string Mime)? tuple2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP (1) photo_data,content_type FROM dbo.audit_photos WHERE profile_id=@id ORDER BY CASE photo_type WHEN 'frontal' THEN 1 WHEN 'operativa' THEN 2 WHEN 'entorno' THEN 3 WHEN 'inversor' THEN 4 WHEN 'bateria' THEN 5 ELSE 6 END;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				(byte[] Data, string Mime)? tuple;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct))
				{
					tuple = ((await reader.ReadAsync(ct)) ? new (byte[], string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1))) : (((byte[], string)?)null));
				}
				tuple2 = tuple;
			}
			result = tuple2;
		}
		return result;
	}

	public async Task<(byte[] Data, string Mime)?> Photo(Guid id, string type, CancellationToken ct)
	{
		(byte[] Data, string Mime)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			(byte[] Data, string Mime)? tuple2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP (1) photo_data,content_type FROM dbo.audit_photos WHERE profile_id=@id AND photo_type=@type;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				command.Parameters.AddWithValue("@type", type);
				(byte[] Data, string Mime)? tuple;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct))
				{
					tuple = ((await reader.ReadAsync(ct)) ? new (byte[], string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1))) : (((byte[], string)?)null));
				}
				tuple2 = tuple;
			}
			result = tuple2;
		}
		return result;
	}

	public async Task<List<TicketAgencyRow>> TicketAgencies(string userId, string role, CancellationToken ct)
	{
		List<TicketAgencyRow> result2;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			await EnsureAgencyDirectorySchema(connection, ct);
			List<TicketAgencyRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.id,a.codigo,a.terminal,a.grupo,\r\n       COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),\r\n       COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),\r\n       COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude),a.region\r\nFROM dbo.agencies a\r\nLEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id\r\nWHERE a.is_active=1\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\nORDER BY a.grupo,a.terminal;", connection))
			{
				AddScope(command, userId, role);
				List<TicketAgencyRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<TicketAgencyRow> rows = new List<TicketAgencyRow>();
					HashSet<Guid> seen = new HashSet<Guid>();
					while (await reader.ReadAsync(ct))
					{
						if (Guid.TryParse(reader.GetString(0), out var result) && seen.Add(result))
						{
							rows.Add(new TicketAgencyRow(result, reader.IsDBNull(1) ? "" : reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetValue(8))), reader.IsDBNull(9) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetValue(9))), reader.IsDBNull(10) ? null : reader.GetString(10)));
						}
					}
					list = rows;
				}
				list2 = list;
			}
			result2 = list2;
		}
		return result2;
	}

	public async Task<TicketAgencyCatalogStats> TicketAgencyCatalogStats(string userId, string role, CancellationToken ct)
	{
		TicketAgencyCatalogStats result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			TicketAgencyCatalogStats ticketAgencyCatalogStats2;
			await using (SqlCommand command = new SqlCommand("SELECT COUNT_BIG(*),COALESCE(SUM(CASE WHEN TRY_CONVERT(uniqueidentifier,a.id) IS NULL THEN 1 ELSE 0 END),0)\r\nFROM dbo.agencies a\r\nWHERE a.is_active=1\r\n  AND (@scoped=0 OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ));", connection))
			{
				AddScope(command, userId, role);
				TicketAgencyCatalogStats ticketAgencyCatalogStats;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					await reader.ReadAsync(ct);
					ticketAgencyCatalogStats = new TicketAgencyCatalogStats(Convert.ToInt64(reader.GetValue(0)), Convert.ToInt64(reader.GetValue(1)));
				}
				ticketAgencyCatalogStats2 = ticketAgencyCatalogStats;
			}
			result = ticketAgencyCatalogStats2;
		}
		return result;
	}

	public async Task<bool> UpdateAgencyDirectory(Guid id, AgencyDirectoryUpdate body, string actor, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyDirectorySchema(connection, ct);
			bool flag;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					Dictionary<string, object?> before = null;
					await using (SqlCommand read = new SqlCommand("SELECT a.codigo,a.terminal,a.grupo,\r\n       COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),\r\n       COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),\r\n       COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude)\r\nFROM dbo.agencies a WITH(UPDLOCK,ROWLOCK)\r\nLEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id\r\nWHERE a.id=@id;", connection, transaction))
					{
						read.Parameters.AddWithValue("@id", id.ToString());
						await using SqlDataReader reader = await read.ExecuteReaderAsync(ct);
						if (await reader.ReadAsync(ct))
						{
							before = new Dictionary<string, object>
							{
								["codigo"] = reader.GetString(0),
								["terminal"] = reader.GetString(1),
								["grupo"] = reader.GetString(2),
								["direccion"] = (reader.IsDBNull(3) ? null : reader.GetString(3)),
								["sector"] = (reader.IsDBNull(4) ? null : reader.GetString(4)),
								["municipio"] = (reader.IsDBNull(5) ? null : reader.GetString(5)),
								["provincia"] = (reader.IsDBNull(6) ? null : reader.GetString(6)),
								["latitude"] = (reader.IsDBNull(7) ? null : ((object)reader.GetDouble(7))),
								["longitude"] = (reader.IsDBNull(8) ? null : ((object)reader.GetDouble(8)))
							};
						}
					}
					if (before == null)
					{
						await transaction.RollbackAsync(ct);
						flag = false;
					}
					else
					{
						await using (SqlCommand read = new SqlCommand("UPDATE dbo.agencies SET codigo=@codigo,terminal=@terminal,grupo=@grupo,\r\n    directory_direccion=@direccion,directory_sector=@sector,\r\n    directory_municipio=@municipio,directory_provincia=@provincia,\r\n    expected_latitude=@latitude,expected_longitude=@longitude,\r\n    updated_at=SYSUTCDATETIME()\r\nWHERE id=@id;", connection, transaction))
						{
							read.Parameters.AddWithValue("@id", id.ToString());
							read.Parameters.AddWithValue("@codigo", body.Codigo.Trim());
							read.Parameters.AddWithValue("@terminal", body.Terminal.Trim());
							read.Parameters.AddWithValue("@grupo", body.Grupo.Trim());
							read.Parameters.AddWithValue("@direccion", Db(string.IsNullOrWhiteSpace(body.Direccion) ? null : body.Direccion.Trim()));
							read.Parameters.AddWithValue("@sector", Db(string.IsNullOrWhiteSpace(body.Sector) ? null : body.Sector.Trim()));
							read.Parameters.AddWithValue("@municipio", Db(string.IsNullOrWhiteSpace(body.Municipio) ? null : body.Municipio.Trim()));
							read.Parameters.AddWithValue("@provincia", Db(string.IsNullOrWhiteSpace(body.Provincia) ? null : body.Provincia.Trim()));
							read.Parameters.AddWithValue("@latitude", Db(body.Latitude));
							read.Parameters.AddWithValue("@longitude", Db(body.Longitude));
							await read.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand read = new SqlCommand("UPDATE dbo.agency_profiles SET\r\n    direccion=COALESCE(@direccion,direccion),sector=COALESCE(@sector,sector),\r\n    municipio=COALESCE(@municipio,municipio),provincia=COALESCE(@provincia,provincia),\r\n    latitude=COALESCE(@latitude,latitude),longitude=COALESCE(@longitude,longitude),\r\n    updated_at=SYSUTCDATETIME()\r\nWHERE agency_id=@id;", connection, transaction))
						{
							read.Parameters.AddWithValue("@id", id.ToString());
							read.Parameters.AddWithValue("@direccion", Db(string.IsNullOrWhiteSpace(body.Direccion) ? null : body.Direccion.Trim()));
							read.Parameters.AddWithValue("@sector", Db(string.IsNullOrWhiteSpace(body.Sector) ? null : body.Sector.Trim()));
							read.Parameters.AddWithValue("@municipio", Db(string.IsNullOrWhiteSpace(body.Municipio) ? null : body.Municipio.Trim()));
							read.Parameters.AddWithValue("@provincia", Db(string.IsNullOrWhiteSpace(body.Provincia) ? null : body.Provincia.Trim()));
							read.Parameters.AddWithValue("@latitude", Db(body.Latitude));
							read.Parameters.AddWithValue("@longitude", Db(body.Longitude));
							await read.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand read = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,after_data,actor_email) VALUES('agency',@id,'AGENCY_DIRECTORY_UPDATED',@before,@after,@actor);", connection, transaction))
						{
							read.Parameters.AddWithValue("@id", id.ToString());
							read.Parameters.AddWithValue("@before", JsonSerializer.Serialize(before));
							read.Parameters.AddWithValue("@after", JsonSerializer.Serialize(body));
							read.Parameters.AddWithValue("@actor", actor);
							await read.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						flag = true;
					}
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = flag;
		}
		return result;
	}

	public async Task<List<SupportTicketRow>> Tickets(string userId, string role, CancellationToken ct)
	{
		List<SupportTicketRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			await EnsureAgencyDirectorySchema(connection, ct);
			List<SupportTicketRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.category,t.assigned_department,t.priority,t.status,\r\n       t.subject,t.description,t.resolution,t.created_by_name,t.created_by_username,\r\n       t.created_at,t.updated_at,t.closed_at,t.is_automatic,ISNULL(t.ticket_type,'SUPPORT'),t.assigned_technician_id,u.display_name,\r\n       COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),\r\n       COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),\r\n       COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude)\r\nFROM dbo.support_tickets t\r\nINNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nLEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id\r\nLEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=t.assigned_technician_id\r\nWHERE (@scoped=0 OR t.assigned_technician_id=@userId OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\n  AND (@department='' OR t.assigned_department=@department)\r\n  AND (@ownOnly=0 OR t.assigned_technician_id=@userId)\r\nORDER BY CASE t.status WHEN 'OPEN' THEN 1 WHEN 'IN_PROGRESS' THEN 2 WHEN 'PENDING' THEN 3 WHEN 'RESOLVED' THEN 4 ELSE 5 END,\r\n         CASE t.priority WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END,\r\n         t.created_at DESC;", connection))
			{
				AddScope(command, userId, role);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				command.Parameters.AddWithValue("@ownOnly", (role == "GroupAdministrator") ? 1 : 0);
				List<SupportTicketRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<SupportTicketRow> rows = new List<SupportTicketRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new SupportTicketRow(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetDateTime(14), reader.GetDateTime(15), reader.IsDBNull(16) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(16)), reader.GetBoolean(17), reader.GetString(18), reader.IsDBNull(19) ? null : reader.GetString(19), reader.IsDBNull(20) ? null : reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21), reader.IsDBNull(22) ? null : reader.GetString(22), reader.IsDBNull(23) ? null : reader.GetString(23), reader.IsDBNull(24) ? null : reader.GetString(24), reader.IsDBNull(25) ? ((double?)null) : new double?(reader.GetDouble(25)), reader.IsDBNull(26) ? ((double?)null) : new double?(reader.GetDouble(26))));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<SupportTicketRow>> Tickets(string userId, string role, bool canAssignTickets, string? supportTeam, string? userEmail, CancellationToken ct, bool areaManager = false, Guid? onlyTicketId = null)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		List<SupportTicketRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			List<SupportTicketRow> list5;
			await using (SqlCommand command = new SqlCommand("DECLARE @visible TABLE(id uniqueidentifier NOT NULL PRIMARY KEY);\r\nINSERT INTO @visible(id)\r\nSELECT t.id\r\nFROM dbo.support_tickets t\r\nINNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nOUTER APPLY (SELECT CASE WHEN\r\n    t.assigned_technician_id=@userId\r\n    OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))\r\n    OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(@userEmail)))\r\n    OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LEFT(LTRIM(RTRIM(@userEmail)),CHARINDEX('@',LTRIM(RTRIM(@userEmail))+'@')-1))\r\n    OR EXISTS(SELECT 1 FROM dbo.admin_users assigned_user WHERE CONVERT(nvarchar(80),assigned_user.id)=LTRIM(RTRIM(t.assigned_technician_id)) AND LOWER(LTRIM(RTRIM(assigned_user.email)))=LOWER(LTRIM(RTRIM(@userEmail))))\r\n    THEN 1 ELSE 0 END AS is_current) assignment\r\n-- Las expresiones históricas se conservan como referencia de compatibilidad:\r\n-- @groupSupervisor=1 AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))\r\n-- @supportTeam='TECHNICIANS' AND (t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))\r\nWHERE (@ticketId IS NULL OR t.id=@ticketId) AND (\r\n  @administrator=1\r\n  OR (@administrativeRequester=1 AND t.created_by_user_id=@userId)\r\n  OR (@supportTeam='WAREHOUSE' AND t.created_by_user_id=@userId)\r\n  OR (@areaManager=1 AND @supportTeam='WAREHOUSE' AND EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND (creator.role='WAREHOUSE' OR creator.support_team='WAREHOUSE')))\r\n  OR (@groupSupervisor=1 AND (assignment.is_current=1 OR t.created_by_user_id=@userId))\r\n  OR (@supportDepartment<>'' AND (\r\n        (@supportTeam='CALL_CENTER' AND (\r\n            (t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')\r\n            OR (t.ticket_type='INTERNAL' AND EXISTS(SELECT 1 FROM dbo.support_ticket_history cc\r\n                WHERE cc.ticket_id=t.id AND cc.from_team='CALL_CENTER' AND (\r\n                    (cc.action='ESCALATED' AND cc.to_team='TECHNICAL_FAILURE')\r\n                    OR (cc.action='ESCALATED_TO_SUPERVISOR' AND cc.to_team='TECHNICIANS'))))\r\n        ))\r\n        OR (@supportTeam='TECHNICIANS' AND assignment.is_current=1 AND t.assigned_department=@supportDepartment)\r\n        OR (@supportTeam='TECHNICAL_FAILURE' AND (\r\n            (assignment.is_current=1 AND t.assigned_department=@supportDepartment)\r\n            OR (@canAssign=1 AND t.assigned_department='TECHNOLOGY' AND t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS'))\r\n        ))\r\n        OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS') AND (\r\n            (assignment.is_current=1 AND t.assigned_department=@supportDepartment)\r\n            OR (@canAssign=1 AND (\r\n                (t.assigned_department=@supportDepartment AND (@supportTeam='' OR t.assigned_team=@supportTeam))\r\n                OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@supportDepartment)\r\n            ))\r\n        ))\r\n     ))) OPTION(RECOMPILE);\r\n\r\nSELECT t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.category,t.assigned_department,t.priority,t.status,\r\n       t.subject,t.description,t.resolution,t.created_by_name,t.created_by_username,\r\n       t.created_at,t.updated_at,t.closed_at,t.is_automatic,ISNULL(t.ticket_type,'SUPPORT'),t.assigned_technician_id,u.display_name,\r\n       COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),\r\n       COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),\r\n       COALESCE(p.latitude,a.expected_latitude),COALESCE(p.longitude,a.expected_longitude),\r\n       CASE WHEN t.assigned_department='TECHNOLOGY' AND COALESCE(ru.support_team,u.support_team) IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS')\r\n            THEN COALESCE(ru.support_team,u.support_team) ELSE t.assigned_team END,\r\n        COALESCE(t.resolved_by_name,ru.display_name,CASE WHEN t.status IN('RESOLVED','CLOSED') THEN u.display_name END),\r\n        CAST(CASE WHEN\r\n            LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))\r\n            OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(@userEmail)))\r\n            OR (NULLIF(LTRIM(RTRIM(@userEmail)),'') IS NOT NULL AND LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LEFT(LTRIM(RTRIM(@userEmail)),CHARINDEX('@',LTRIM(RTRIM(@userEmail))+'@')-1)))\r\n            OR EXISTS(SELECT 1 FROM dbo.admin_users assigned_user WHERE CONVERT(nvarchar(80),assigned_user.id)=LTRIM(RTRIM(t.assigned_technician_id)) AND LOWER(LTRIM(RTRIM(assigned_user.email)))=LOWER(LTRIM(RTRIM(@userEmail))))\r\n        THEN 1 ELSE 0 END AS bit),\r\n        CAST(CASE WHEN t.created_by_user_id=@userId THEN 1 ELSE 0 END AS bit)\r\nFROM @visible v\r\nINNER JOIN dbo.support_tickets t ON t.id=v.id\r\nINNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nLEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id\r\nLEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=t.assigned_technician_id\r\nLEFT JOIN dbo.admin_users ru ON CONVERT(nvarchar(80),ru.id)=t.resolved_by_user_id\r\nORDER BY CASE t.status WHEN 'OPEN' THEN 1 WHEN 'IN_PROGRESS' THEN 2 WHEN 'PENDING' THEN 3 WHEN 'RESOLVED' THEN 4 ELSE 5 END,\r\n         CASE t.priority WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END,t.created_at DESC;\r\n\r\nSELECT h.ticket_id,h.id,h.action,h.from_department,h.to_department,h.from_team,h.to_team,h.actor_name,h.comment,h.created_at\r\nFROM dbo.support_ticket_history h INNER JOIN @visible v ON v.id=h.ticket_id ORDER BY h.created_at,h.id;\r\nSELECT s.ticket_id,s.department_code FROM dbo.support_ticket_shares s INNER JOIN @visible v ON v.id=s.ticket_id ORDER BY s.department_code;\r\nSELECT e.ticket_id,e.id,e.file_name,e.content_type,e.captured_at,e.latitude,e.longitude,e.accuracy_meters,e.location_captured_at FROM dbo.support_ticket_evidence e INNER JOIN @visible v ON v.id=e.ticket_id ORDER BY e.captured_at,e.id;", connection))
			{
				command.Parameters.AddWithValue("@administrator", (role == "Administrator") ? 1 : 0);
				command.Parameters.Add("@ticketId", SqlDbType.UniqueIdentifier).Value = ((object)onlyTicketId) ?? DBNull.Value;
				command.Parameters.AddWithValue("@groupSupervisor", (role == "GroupAdministrator") ? 1 : 0);
				command.Parameters.AddWithValue("@supportDepartment", DepartmentForRole(role));
				command.Parameters.AddWithValue("@supportTeam", (role == "Warehouse") ? "WAREHOUSE" : (supportTeam ?? ""));
				command.Parameters.AddWithValue("@userEmail", userEmail ?? "");
				command.Parameters.AddWithValue("@administrativeRequester", (DepartmentAccounts.IsSupportScope(role) || (role == "Administrative" && EmmaAdministrativeAccess.IsEmma(userEmail))) ? 1 : 0);
				command.Parameters.AddWithValue("@areaManager", areaManager ? 1 : 0);
				command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
				command.Parameters.AddWithValue("@userId", userId);
				List<SupportTicketRow> list4;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<SupportTicketRow> rows = new List<SupportTicketRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new SupportTicketRow(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetDateTime(14), reader.GetDateTime(15), reader.IsDBNull(16) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(16)), ReadBooleanValue(reader, 17), reader.GetString(18), reader.IsDBNull(19) ? null : reader.GetString(19), reader.IsDBNull(20) ? null : reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21), reader.IsDBNull(22) ? null : reader.GetString(22), reader.IsDBNull(23) ? null : reader.GetString(23), reader.IsDBNull(24) ? null : reader.GetString(24), reader.IsDBNull(25) ? ((double?)null) : new double?(reader.GetDouble(25)), reader.IsDBNull(26) ? ((double?)null) : new double?(reader.GetDouble(26)), reader.IsDBNull(27) ? null : reader.GetString(27), new List<string>(), new List<TicketHistoryRow>(), new List<TicketEvidenceRow>(), reader.IsDBNull(28) ? null : reader.GetString(28))
						{
							IsAssignedToCurrentUser = ReadBooleanValue(reader, 29),
							IsCreatedByCurrentUser = ReadBooleanValue(reader, 30)
						});
					}
					Dictionary<Guid, List<TicketHistoryRow>> history = new Dictionary<Guid, List<TicketHistoryRow>>();
					Dictionary<Guid, List<string>> shares = new Dictionary<Guid, List<string>>();
					Dictionary<Guid, List<TicketEvidenceRow>> evidence = new Dictionary<Guid, List<TicketEvidenceRow>>();
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							Guid guid = reader.GetGuid(0);
							if (!history.TryGetValue(guid, out List<TicketHistoryRow> value))
							{
								value = (history[guid] = new List<TicketHistoryRow>());
							}
							value.Add(new TicketHistoryRow(reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetDateTime(9)));
						}
					}
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							Guid guid2 = reader.GetGuid(0);
							if (!shares.TryGetValue(guid2, out List<string> value2))
							{
								value2 = (shares[guid2] = new List<string>());
							}
							value2.Add(reader.GetString(1));
						}
					}
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							Guid guid3 = reader.GetGuid(0);
							if (!evidence.TryGetValue(guid3, out List<TicketEvidenceRow> value3))
							{
								value3 = (evidence[guid3] = new List<TicketEvidenceRow>());
							}
							Guid guid4 = reader.GetGuid(1);
							value3.Add(new TicketEvidenceRow(guid4, reader.GetString(2), reader.GetString(3), reader.GetDateTime(4), $"/api/tickets/evidence/{guid4}", reader.IsDBNull(5) ? ((double?)null) : new double?(reader.GetDouble(5)), reader.IsDBNull(6) ? ((double?)null) : new double?(reader.GetDouble(6)), reader.IsDBNull(7) ? ((double?)null) : new double?(reader.GetDouble(7)), reader.IsDBNull(8) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(8))));
						}
					}
					list4 = rows.Select((SupportTicketRow row) => row with
					{
						History = (history.GetValueOrDefault(row.Id) ?? new List<TicketHistoryRow>()),
						SharedWithDepartments = (shares.GetValueOrDefault(row.Id) ?? new List<string>()),
						Evidence = (evidence.GetValueOrDefault(row.Id) ?? new List<TicketEvidenceRow>()),
						IsAssignedToCurrentUser = (row.IsAssignedToCurrentUser || string.Equals(row.AssignedTechnicianId?.Trim(), userId.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(row.AssignedTechnicianId?.Trim(), userEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
					}).ToList();
				}
				list5 = list4;
			}
			result = list5;
		}
		return result;
	}

	public async Task<SupportTicketRow?> CreateTicket(TicketCreate body, string userId, string role, string displayName, string username, CancellationToken ct)
	{
		SupportTicketRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			SupportTicketRow supportTicketRow;
			await using (SqlCommand access = new SqlCommand("SELECT COUNT(*) FROM dbo.agencies a\r\nWHERE a.id=@agency AND a.is_active=1\r\n  AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));", connection))
			{
				access.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
				AddScope(access, userId, role);
				if (Convert.ToInt32(await access.ExecuteScalarAsync(ct)) == 0)
				{
					supportTicketRow = null;
					goto IL_02f7;
				}
			}
			supportTicketRow = null;
			Guid id = Guid.NewGuid();
			await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.support_tickets(id,agency_id,category,assigned_department,priority,subject,description,created_by_user_id,created_by_name,created_by_username,ticket_type,assigned_technician_id)\r\nVALUES(@id,@agency,@category,@department,@priority,@subject,@description,@userId,@name,@username,@ticketType,@technician,SYSUTCDATETIME(),SYSUTCDATETIME());", connection))
			{
				command.Parameters.AddWithValue("@id", id);
				command.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
				command.Parameters.AddWithValue("@category", body.Category);
				command.Parameters.AddWithValue("@department", body.AssignedDepartment);
				command.Parameters.AddWithValue("@priority", (body.AssignedDepartment == "TECHNOLOGY" && body.Category == "CPU_FAILURE") ? "CRITICAL" : body.Priority);
				command.Parameters.AddWithValue("@subject", body.Subject.Trim());
				command.Parameters.AddWithValue("@description", body.Description.Trim());
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@name", displayName);
				command.Parameters.AddWithValue("@username", username);
				command.Parameters.AddWithValue("@ticketType", (body.TicketType == "INTERNAL") ? "INTERNAL" : "SUPPORT");
				command.Parameters.AddWithValue("@technician", Db(body.AssignedTechnicianId));
				await command.ExecuteNonQueryAsync(ct);
				supportTicketRow = (await Tickets(userId, role, ct)).FirstOrDefault((SupportTicketRow ticket) => ticket.Id == id);
			}
			result = supportTicketRow;
			goto end_IL_00da;
			IL_02f7:
			result = supportTicketRow;
			end_IL_00da:;
		}
		return result;
	}

	private static string DatabaseRoleForDepartment(string department)
	{
		if (DepartmentAccounts.LegacyStandalone(department))
		{
			return "DEPARTMENT";
		}
		return department switch
		{
			"TECHNOLOGY" => "TECHNOLOGY", 
			"GENERAL_SERVICES" => "GENERAL_SERVICES", 
			"HUMAN_RESOURCES" => "HUMAN_RESOURCES", 
			_ => "", 
		};
	}

	private static async Task<bool> ValidTicketAssignee(SqlConnection connection, SqlTransaction transaction, string? technicianId, string department, string? team, string ticketType, string agencyGroup, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(technicianId))
		{
			return true;
		}
		bool result;
		await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users u\r\nWHERE CONVERT(nvarchar(80),u.id)=@technician AND u.is_active=1 AND (\r\n    (u.role=@departmentRole AND (u.role<>'DEPARTMENT' OR u.support_team=@department) AND (\r\n        (@department='TECHNOLOGY' AND (\r\n            (@team='CALL_CENTER' AND u.support_team='CALL_CENTER')\r\n            OR (@team='TECHNICAL_FAILURE' AND u.support_team='TECHNICAL_FAILURE')\r\n            OR (@team='TECHNICAL_FAILURE' AND (u.support_team='TECHNICIANS' OR (ISNULL(u.support_team,'')='' AND JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true' AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true')))\r\n            OR (@team='TECHNICIANS' AND (u.support_team='TECHNICIANS' OR u.support_team IS NULL))\r\n        ))\r\n        OR (@department<>'TECHNOLOGY' AND (u.support_team='TECHNICIANS' OR\r\n            (u.role='GENERAL_SERVICES' AND (u.support_team='WORKSHOP' OR JSON_VALUE(u.permissions_json,'$.canAssignTickets')='false')) OR (\r\n            JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'\r\n            AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true'\r\n        )))\r\n    ))\r\n    OR u.role='GROUP_ADMIN'\r\n);", connection, transaction))
		{
			command.Parameters.AddWithValue("@technician", technicianId);
			command.Parameters.AddWithValue("@department", department);
			command.Parameters.AddWithValue("@departmentRole", DatabaseRoleForDepartment(department));
			command.Parameters.AddWithValue("@team", team ?? "");
			command.Parameters.AddWithValue("@ticketType", ticketType);
			command.Parameters.AddWithValue("@agencyGroup", agencyGroup);
			result = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
		}
		return result;
	}

	private static async Task AddTicketHistory(SqlConnection connection, SqlTransaction transaction, Guid ticketId, string action, string? fromDepartment, string? toDepartment, string? fromTeam, string? toTeam, string userId, string actorName, string? comment, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("INSERT INTO dbo.support_ticket_history(ticket_id,action,from_department,to_department,from_team,to_team,actor_user_id,actor_name,comment,created_at) VALUES(@ticket,@action,@fromDepartment,@toDepartment,@fromTeam,@toTeam,@userId,@actor,@comment,SYSUTCDATETIME());", connection, transaction);
		command.Parameters.AddWithValue("@ticket", ticketId);
		command.Parameters.AddWithValue("@action", action);
		command.Parameters.AddWithValue("@fromDepartment", Db(fromDepartment));
		command.Parameters.AddWithValue("@toDepartment", Db(toDepartment));
		command.Parameters.AddWithValue("@fromTeam", Db(fromTeam));
		command.Parameters.AddWithValue("@toTeam", Db(toTeam));
		command.Parameters.AddWithValue("@userId", Db(string.IsNullOrWhiteSpace(userId) ? null : userId));
		command.Parameters.AddWithValue("@actor", actorName);
		command.Parameters.AddWithValue("@comment", Db(string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()));
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task AddTicketNotifications(SqlConnection connection, SqlTransaction transaction, Guid ticketId, string eventType, string? recipientUserId, string? targetDepartment, string agencyGroup, string userId, string actorName, string message, bool notifySupervisors, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("DECLARE @occurredAt datetime2(3)=SYSUTCDATETIME();\r\nIF @targetDepartment<>''\r\n    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,target_department,message,actor_user_id,actor_name,created_at)\r\n    VALUES(@ticket,@event,@targetDepartment,@message,@userId,@actor,@occurredAt);\r\nIF @recipient<>''\r\n    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)\r\n    VALUES(@ticket,@event,@recipient,@message,@userId,@actor,@occurredAt);\r\nINSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)\r\nSELECT @ticket,@event,t.created_by_user_id,@message,@userId,@actor,@occurredAt\r\nFROM dbo.support_tickets t INNER JOIN dbo.admin_users creator ON CONVERT(nvarchar(80),creator.id)=t.created_by_user_id\r\nWHERE t.id=@ticket AND creator.is_active=1 AND creator.role='GROUP_ADMIN'\r\n  AND t.created_by_user_id<>@recipient;\r\nIF @notifySupervisors=1\r\n    INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name,created_at)\r\n    SELECT @ticket,@event,CONVERT(nvarchar(80),u.id),@message,@userId,@actor,@occurredAt\r\n    FROM dbo.admin_users u\r\n    WHERE u.is_active=1 AND u.role='GROUP_ADMIN'\r\n      AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id AND ug.group_name=@agencyGroup)\r\n      AND (@recipient='' OR CONVERT(nvarchar(80),u.id)<>@recipient)\r\n      AND NOT EXISTS(SELECT 1 FROM dbo.support_tickets t WHERE t.id=@ticket AND t.created_by_user_id=CONVERT(nvarchar(80),u.id));", connection, transaction);
		command.Parameters.AddWithValue("@ticket", ticketId);
		command.Parameters.AddWithValue("@event", eventType);
		command.Parameters.AddWithValue("@recipient", recipientUserId ?? "");
		command.Parameters.AddWithValue("@targetDepartment", targetDepartment ?? "");
		command.Parameters.AddWithValue("@agencyGroup", agencyGroup);
		command.Parameters.AddWithValue("@userId", Db(string.IsNullOrWhiteSpace(userId) ? null : userId));
		command.Parameters.AddWithValue("@actor", actorName);
		command.Parameters.AddWithValue("@message", message);
		command.Parameters.AddWithValue("@notifySupervisors", notifySupervisors ? 1 : 0);
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task AddTechnologyResolutionNotifications(SqlConnection connection, SqlTransaction transaction, Guid ticketId, string eventType, string userId, string actorName, string message, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("INSERT INTO dbo.support_ticket_notifications(ticket_id,event_type,recipient_user_id,message,actor_user_id,actor_name)\r\nSELECT @ticket,@event,CONVERT(nvarchar(80),u.id),@message,@userId,@actor\r\nFROM dbo.admin_users u\r\nINNER JOIN dbo.support_tickets t ON t.id=@ticket\r\nWHERE u.is_active=1 AND u.role='TECHNOLOGY'\r\n  AND (u.support_team IS NULL OR u.support_team=t.assigned_team)\r\n  AND CONVERT(nvarchar(80),u.id)<>@userId;", connection, transaction);
		command.Parameters.AddWithValue("@ticket", ticketId);
		command.Parameters.AddWithValue("@event", eventType);
		command.Parameters.AddWithValue("@userId", userId);
		command.Parameters.AddWithValue("@actor", actorName);
		command.Parameters.AddWithValue("@message", message);
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task<bool> CanWorkTicket(SqlConnection connection, SqlTransaction transaction, Guid ticketId, string userId, string role, string? supportTeam, bool canAssignTickets, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool result;
		await using (SqlCommand command = new SqlCommand("SELECT COUNT(*)\r\nFROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nOUTER APPLY (SELECT CASE WHEN\r\n    t.assigned_technician_id=@userId\r\n    OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))\r\n    OR EXISTS(SELECT 1 FROM dbo.admin_users actor_account WHERE CONVERT(nvarchar(80),actor_account.id)=@userId AND (LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(actor_account.email))) OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LEFT(LTRIM(RTRIM(actor_account.email)),CHARINDEX('@',LTRIM(RTRIM(actor_account.email))+'@')-1))))\r\n    OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_users assigned_account\r\n        INNER JOIN dbo.admin_users actor_account ON CONVERT(nvarchar(80),actor_account.id)=@userId\r\n        WHERE CONVERT(nvarchar(80),assigned_account.id)=LTRIM(RTRIM(t.assigned_technician_id))\r\n          AND LOWER(LTRIM(RTRIM(assigned_account.email)))=LOWER(LTRIM(RTRIM(actor_account.email)))\r\n    )\r\n    THEN 1 ELSE 0 END AS is_current) assignment\r\n-- Compatibilidad con asignaciones históricas:\r\n-- t.assigned_technician_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))\r\nWHERE t.id=@ticket AND (\r\n    @administrator=1\r\n    OR (@groupSupervisor=1 AND assignment.is_current=1)\r\n    OR (@supportDepartment<>'' AND (\r\n        (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')\r\n        OR (@supportTeam='TECHNICAL_FAILURE' AND (\r\n            (assignment.is_current=1 AND t.assigned_department=@supportDepartment)\r\n            OR (@canAssign=1 AND t.assigned_department='TECHNOLOGY' AND t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS'))\r\n        ))\r\n        OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE') AND (\r\n            (assignment.is_current=1 AND t.assigned_department=@supportDepartment)\r\n            OR (@canAssign=1 AND (\r\n                (t.assigned_department=@supportDepartment AND (@supportTeam='' OR t.assigned_team=@supportTeam))\r\n                OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@supportDepartment)\r\n            ))\r\n        ))\r\n    ))\r\n);", connection, transaction))
		{
			command.Parameters.AddWithValue("@ticket", ticketId);
			command.Parameters.AddWithValue("@userId", userId);
			command.Parameters.AddWithValue("@administrator", (role == "Administrator") ? 1 : 0);
			command.Parameters.AddWithValue("@groupSupervisor", (role == "GroupAdministrator") ? 1 : 0);
			command.Parameters.AddWithValue("@supportDepartment", DepartmentForRole(role));
			command.Parameters.AddWithValue("@supportTeam", DepartmentAccounts.IsSupportScope(role) ? "" : (supportTeam ?? ""));
			command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
			result = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
		}
		return result;
	}

	private static async Task<bool> CanAdministerTicket(SqlConnection connection, SqlTransaction transaction, Guid ticketId, string role, string? supportTeam, bool canAssignTickets, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool result;
		await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.support_tickets t\r\nWHERE t.id=@ticket AND (\r\n    @administrator=1\r\n    OR (@canAssign=1 AND @department<>'' AND t.assigned_department=@department\r\n        AND (@supportTeam='' OR t.assigned_team=@supportTeam OR (@supportTeam='TECHNICAL_FAILURE' AND t.assigned_team='TECHNICIANS')))\r\n);", connection, transaction))
		{
			command.Parameters.AddWithValue("@ticket", ticketId);
			command.Parameters.AddWithValue("@administrator", (role == "Administrator") ? 1 : 0);
			command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
			command.Parameters.AddWithValue("@department", DepartmentForRole(role));
			command.Parameters.AddWithValue("@supportTeam", DepartmentAccounts.IsSupportScope(role) ? "" : (supportTeam ?? ""));
			result = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
		}
		return result;
	}

	public async Task<SupportTicketRow?> CreateTicket(TicketCreate body, string userId, string role, string displayName, string username, string? supportTeam, bool canAssignTickets, IReadOnlyList<TicketEvidenceUpload> creationEvidence, string? supervisorEscalationReason, CancellationToken ct, Guid? workshopStockId = null)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool flag = role == "GroupAdministrator";
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3;
			switch (body.AssignedDepartment)
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
			flag2 = !flag3;
		}
		if (flag2)
		{
			return null;
		}
		if (DepartmentAccounts.IsSupportScope(role) && body.AssignedDepartment != DepartmentForRole(role) && !string.IsNullOrWhiteSpace(body.AssignedTechnicianId))
		{
			return null;
		}
		if (workshopStockId.HasValue && !new WorkshopActor(userId, role, supportTeam, displayName, username).CanOperate)
		{
			return null;
		}
		if (role == "Administrative")
		{
			bool flag4 = body.TicketType != "INTERNAL";
			bool flag5 = flag4;
			if (!flag5)
			{
				bool flag3;
				switch (body.AssignedDepartment)
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
				flag5 = !flag3;
			}
			if (flag5 || !string.IsNullOrWhiteSpace(body.AssignedTechnicianId))
			{
				return null;
			}
			canAssignTickets = false;
		}
		bool warehouseRequester = role == "Warehouse" || supportTeam == "WAREHOUSE";
		if (warehouseRequester)
		{
			bool flag6 = body.TicketType != "INTERNAL";
			bool flag7 = flag6;
			if (!flag7)
			{
				bool flag3;
				switch (body.AssignedDepartment)
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
				flag7 = !flag3;
			}
			if (flag7 || !string.IsNullOrWhiteSpace(body.AssignedTechnicianId))
			{
				return null;
			}
			canAssignTickets = false;
		}
		if (!warehouseRequester && role == "GeneralServices" && (!canAssignTickets || supportTeam == "WORKSHOP"))
		{
			return null;
		}
		SupportTicketRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			await EnsureUserPermissionSchema(connection, ct);
			string ticketType = ((body.TicketType == "INTERNAL") ? "INTERNAL" : "SUPPORT");
			string department = body.AssignedDepartment.Trim().ToUpperInvariant();
			string team = ((!(department == "TECHNOLOGY")) ? null : body.AssignedTeam?.Trim().ToUpperInvariant());
			string priority = ((department == "TECHNOLOGY" && string.Equals(body.Category?.Trim(), "CPU_FAILURE", StringComparison.OrdinalIgnoreCase)) ? "CRITICAL" : body.Priority.Trim().ToUpperInvariant());
			if (department == "TECHNOLOGY" && team == null)
			{
				team = "TECHNICAL_FAILURE";
			}
			bool flag3;
			if (supportTeam == "CALL_CENTER")
			{
				if (!(ticketType != "INTERNAL") && !(department != "TECHNOLOGY"))
				{
					team = "CALL_CENTER";
					goto IL_0722;
				}
				result = null;
			}
			else
			{
				if (supportTeam == "TECHNICAL_FAILURE" && department == "TECHNOLOGY")
				{
					team = "TECHNICAL_FAILURE";
					goto IL_0722;
				}
				bool flag8 = !workshopStockId.HasValue && !warehouseRequester;
				bool flag9 = flag8;
				if (flag9)
				{
					switch (role)
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
					flag9 = flag3;
				}
				if (!flag9 || !(department != DepartmentForRole(role)))
				{
					goto IL_0722;
				}
				result = null;
			}
			goto end_IL_0449;
			IL_0722:
			if (team != null)
			{
				switch (team)
				{
				case "CALL_CENTER":
				case "TECHNICAL_FAILURE":
				case "TECHNICIANS":
					break;
				default:
					goto IL_0761;
				}
			}
			flag3 = true;
			goto IL_0764;
			IL_0764:
			if (!flag3)
			{
				result = null;
			}
			else
			{
				if (workshopStockId.HasValue)
				{
					await EnsureWorkshopSchema(connection, ct);
					await EnsureWorkshopStockExitSchema(connection, ct);
				}
				SupportTicketRow supportTicketRow2;
				await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
				{
					try
					{
						string agencyGroup;
						SupportTicketRow supportTicketRow;
						await using (SqlCommand access = new SqlCommand("SELECT TOP(1)a.grupo FROM dbo.agencies a\r\nWHERE a.id=@agency AND a.is_active=1\r\n  AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));", connection, transaction))
						{
							access.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
							access.Parameters.AddWithValue("@scoped", (role == "GroupAdministrator") ? 1 : 0);
							access.Parameters.AddWithValue("@userId", userId);
							agencyGroup = ((await access.ExecuteScalarAsync(ct)) as string) ?? "";
							if (string.IsNullOrWhiteSpace(agencyGroup))
							{
								await transaction.RollbackAsync(ct);
								supportTicketRow = null;
								goto IL_0be0;
							}
						}
						supportTicketRow = null;
						await using (SqlCommand access = new SqlCommand("SELECT COUNT(*) FROM dbo.support_departments d WHERE d.code=@department AND d.is_active=1 AND EXISTS(SELECT 1 FROM dbo.support_categories c WHERE c.department_code=d.code AND c.code=@category AND c.is_active=1);", connection, transaction))
						{
							access.Parameters.AddWithValue("@department", department);
							access.Parameters.AddWithValue("@category", body.Category);
							if (Convert.ToInt32(await access.ExecuteScalarAsync(ct)) == 0)
							{
								await transaction.RollbackAsync(ct);
								supportTicketRow = null;
								goto IL_0e22;
							}
						}
						if (body.AllowDuplicate != true)
						{
							await using SqlCommand access = new SqlCommand("SELECT TOP(1)t.id,t.ticket_number,t.status,t.subject\r\nFROM dbo.support_tickets t WITH(UPDLOCK,HOLDLOCK)\r\nWHERE t.agency_id=@agency\r\n  AND t.assigned_department=@department\r\n  AND t.category=@category\r\n  AND ISNULL(t.ticket_type,'SUPPORT')=@ticketType\r\n  AND t.status NOT IN('RESOLVED','CLOSED','CANCELLED')\r\nORDER BY t.created_at,t.ticket_number;", connection, transaction);
							access.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
							access.Parameters.AddWithValue("@department", department);
							access.Parameters.AddWithValue("@category", body.Category);
							access.Parameters.AddWithValue("@ticketType", ticketType);
							await using SqlDataReader duplicateReader = await access.ExecuteReaderAsync(ct);
							if (await duplicateReader.ReadAsync(ct))
							{
								TicketDuplicateException duplicate = new TicketDuplicateException(duplicateReader.GetGuid(0), duplicateReader.GetInt64(1), duplicateReader.GetString(2), duplicateReader.GetString(3));
								await duplicateReader.DisposeAsync();
								throw duplicate;
							}
						}
						string requestedTechnicianId;
						if (supportTeam == "CALL_CENTER")
						{
							requestedTechnicianId = userId;
						}
						else
						{
							requestedTechnicianId = ((role == "GroupAdministrator") ? null : body.AssignedTechnicianId);
						}
						if (!string.IsNullOrWhiteSpace(requestedTechnicianId) && !canAssignTickets)
						{
							await transaction.RollbackAsync(ct);
							supportTicketRow2 = null;
						}
						else if (!(await ValidTicketAssignee(connection, transaction, requestedTechnicianId, department, team, ticketType, agencyGroup, ct)))
						{
							await transaction.RollbackAsync(ct);
							supportTicketRow2 = null;
						}
						else
						{
							Guid id = Guid.NewGuid();
							await using (SqlCommand access = new SqlCommand("INSERT INTO dbo.support_tickets(id,agency_id,category,assigned_department,assigned_team,priority,status,subject,description,created_by_user_id,created_by_name,created_by_username,ticket_type,assigned_technician_id,created_at,updated_at)\r\nVALUES(@id,@agency,@category,@department,@team,@priority,CASE WHEN @technician IS NULL THEN 'OPEN' ELSE 'IN_PROGRESS' END,@subject,@description,@userId,@name,@username,@ticketType,@technician,SYSUTCDATETIME(),SYSUTCDATETIME());", connection, transaction))
							{
								access.Parameters.AddWithValue("@id", id);
								access.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
								access.Parameters.AddWithValue("@category", body.Category);
								access.Parameters.AddWithValue("@department", department);
								access.Parameters.AddWithValue("@team", Db(team));
								access.Parameters.AddWithValue("@priority", priority);
								access.Parameters.AddWithValue("@subject", body.Subject.Trim());
								access.Parameters.AddWithValue("@description", body.Description.Trim());
								access.Parameters.AddWithValue("@userId", userId);
								access.Parameters.AddWithValue("@name", displayName);
								access.Parameters.AddWithValue("@username", username);
								access.Parameters.AddWithValue("@ticketType", ticketType);
								access.Parameters.AddWithValue("@technician", Db(string.IsNullOrWhiteSpace(requestedTechnicianId) ? null : requestedTechnicianId));
								await access.ExecuteNonQueryAsync(ct);
							}
							await AddTicketHistory(connection, transaction, id, "CREATED", null, department, null, team, userId, displayName, null, ct);
							foreach (TicketEvidenceUpload item in creationEvidence)
							{
								await using SqlCommand access = new SqlCommand("INSERT INTO dbo.support_ticket_evidence(ticket_id,file_name,content_type,photo_data,uploaded_by_user_id,uploaded_by_name) VALUES(@ticket,@fileName,@contentType,@data,@userId,@actor);", connection, transaction);
								access.Parameters.AddWithValue("@ticket", id);
								access.Parameters.AddWithValue("@fileName", item.FileName);
								access.Parameters.AddWithValue("@contentType", item.ContentType);
								SqlParameter sqlParameter = access.Parameters.Add("@data", SqlDbType.VarBinary, -1);
								sqlParameter.Value = await StorageFiles.Store(item.Data, "ticket-evidence", ct);
								access.Parameters.AddWithValue("@userId", userId);
								access.Parameters.AddWithValue("@actor", displayName);
								await access.ExecuteNonQueryAsync(ct);
							}
							if (role == "GroupAdministrator" && !string.IsNullOrWhiteSpace(supervisorEscalationReason))
							{
								await AddTicketHistory(connection, transaction, id, "SUPERVISOR_ESCALATED", null, department, null, team, userId, displayName, supervisorEscalationReason.Trim(), ct);
							}
							await AddTicketNotifications(connection, transaction, id, "CREATED", null, department, agencyGroup, userId, displayName, $"Se abrió un ticket {((ticketType == "INTERNAL") ? "interno" : "de soporte")} en {department}.", notifySupervisors: true, ct);
							if (!string.IsNullOrWhiteSpace(requestedTechnicianId))
							{
								await AddTicketNotifications(connection, transaction, id, "ASSIGNED", requestedTechnicianId, null, agencyGroup, userId, displayName, "Se te asignó un nuevo ticket.", notifySupervisors: false, ct);
							}
							if (workshopStockId.HasValue)
							{
								await RecordWorkshopStockExit(connection, transaction, workshopStockId.Value, id, userId, displayName, ct);
							}
							await transaction.CommitAsync(ct);
							supportTicketRow2 = (await Tickets(userId, (!workshopStockId.HasValue) ? role : "Administrator", canAssignTickets: true, supportTeam, username, ct, areaManager: false, id)).FirstOrDefault();
						}
						goto end_IL_0969;
						IL_0be0:
						supportTicketRow2 = supportTicketRow;
						goto end_IL_0969;
						IL_0e22:
						supportTicketRow2 = supportTicketRow;
						end_IL_0969:;
					}
					catch
					{
						await transaction.RollbackAsync(ct);
						throw;
					}
				}
				result = supportTicketRow2;
			}
			goto end_IL_0449;
			IL_0761:
			flag3 = false;
			goto IL_0764;
			end_IL_0449:;
		}
		return result;
	}

	public async Task<bool> UpdateTicket(Guid id, TicketUpdate body, string role, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			bool flag;
			await using (SqlCommand command = new SqlCommand("UPDATE dbo.support_tickets SET status=CASE WHEN @technician IS NOT NULL AND @status='OPEN' THEN 'IN_PROGRESS' ELSE @status END,priority=CASE WHEN assigned_department='TECHNOLOGY' AND category='CPU_FAILURE' THEN 'CRITICAL' ELSE @priority END,resolution=@resolution,assigned_technician_id=@technician,\r\n    updated_at=SYSUTCDATETIME(),closed_at=CASE WHEN @status='CLOSED' THEN COALESCE(closed_at,SYSUTCDATETIME()) ELSE NULL END\r\nWHERE id=@id AND (@department='' OR assigned_department=@department);", connection))
			{
				command.Parameters.AddWithValue("@id", id);
				command.Parameters.AddWithValue("@status", body.Status);
				command.Parameters.AddWithValue("@priority", body.Priority);
				command.Parameters.AddWithValue("@resolution", Db(string.IsNullOrWhiteSpace(body.Resolution) ? null : body.Resolution.Trim()));
				command.Parameters.AddWithValue("@technician", Db(body.AssignedTechnicianId));
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				flag = await command.ExecuteNonQueryAsync(ct) == 1;
			}
			result = flag;
		}
		return result;
	}

	private static async Task<TicketWorkflowState?> LoadTicketState(SqlConnection connection, SqlTransaction transaction, Guid id, CancellationToken ct)
	{
		TicketWorkflowState result;
		await using (SqlCommand command = new SqlCommand("SELECT a.grupo,t.assigned_department,t.assigned_team,ISNULL(t.ticket_type,'SUPPORT'),t.status,t.priority,t.assigned_technician_id,t.created_by_user_id,\r\n       COALESCE(profile_location.latitude,a.expected_latitude),COALESCE(profile_location.longitude,a.expected_longitude)\r\nFROM dbo.support_tickets t WITH(UPDLOCK,ROWLOCK)\r\nINNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nOUTER APPLY(SELECT TOP(1)p.latitude,p.longitude FROM dbo.agency_profiles p WHERE p.agency_id=a.id ORDER BY p.submitted_at DESC,p.id DESC) profile_location\r\nWHERE t.id=@id;", connection, transaction))
		{
			command.Parameters.AddWithValue("@id", id);
			TicketWorkflowState ticketWorkflowState;
			await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
			{
				ticketWorkflowState = ((await reader.ReadAsync(ct)) ? new TicketWorkflowState(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? ((double?)null) : new double?(reader.GetDouble(8)), reader.IsDBNull(9) ? ((double?)null) : new double?(reader.GetDouble(9))) : null);
			}
			result = ticketWorkflowState;
		}
		return result;
	}

	public async Task<TicketActionResult> UpdateTicket(Guid id, TicketUpdate body, string userId, string role, string actorName, string? supportTeam, bool canAssignTickets, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		string status = body.Status;
		if ((status == "RESOLVED" || status == "CLOSED") ? true : false)
		{
			return new TicketActionResult(Ok: false, "VALIDATION", "Para resolver o cerrar el ticket debes adjuntar evidencia en la acción de resolución.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					TicketWorkflowState state = await LoadTicketState(connection, transaction, id, ct);
					string requestedTechnician;
					string requestedTeam;
					bool flag;
					if ((object)state == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Ticket no encontrado.");
					}
					else
					{
						switch (state.Status)
						{
						case "RESOLVED":
						case "CLOSED":
						case "CANCELLED":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (!flag)
						{
							requestedTechnician = (string.IsNullOrWhiteSpace(body.AssignedTechnicianId) ? null : body.AssignedTechnicianId);
							requestedTeam = (string.IsNullOrWhiteSpace(body.AssignedTeam) ? state.Team : body.AssignedTeam.Trim().ToUpperInvariant());
							if (state.Department != "TECHNOLOGY")
							{
								requestedTeam = null;
							}
							if (requestedTeam != null)
							{
								switch (requestedTeam)
								{
								case "CALL_CENTER":
								case "TECHNICAL_FAILURE":
								case "TECHNICIANS":
									break;
								default:
									goto IL_05a2;
								}
							}
							flag = true;
							goto IL_05a5;
						}
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Un ticket finalizado no puede reabrirse desde esta acción.");
					}
					goto end_IL_0315;
					IL_05a5:
					bool changesResponsible;
					if (!flag)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "El equipo de Tecnología no es válido.");
					}
					else
					{
						changesResponsible = !string.Equals(state.TechnicianId, requestedTechnician, StringComparison.OrdinalIgnoreCase);
						if ((supportTeam == "CALL_CENTER") & changesResponsible)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Call Center solo puede compartir el ticket con otros departamentos; no puede asignar responsables.");
						}
						else if ((body.Status == "CANCELLED") | changesResponsible)
						{
							if (await CanAdministerTicket(connection, transaction, id, role, supportTeam, canAssignTickets, ct))
							{
								goto IL_09e1;
							}
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el administrador o el soporte del área propietaria puede anular el ticket o cambiar su responsable.");
						}
						else
						{
							if (await CanWorkTicket(connection, transaction, id, userId, role, supportTeam, canAssignTickets, ct))
							{
								goto IL_09e1;
							}
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "No tienes permiso para actualizar este ticket.");
						}
					}
					goto end_IL_0315;
					IL_05a2:
					flag = false;
					goto IL_05a5;
					IL_09e1:
					if (!(await ValidTicketAssignee(connection, transaction, body.AssignedTechnicianId, state.Department, requestedTeam, state.TicketType, state.AgencyGroup, ct)))
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "El técnico no pertenece al departamento o a la agencia del ticket.");
					}
					else
					{
						switch (body.Status)
						{
						case "RESOLVED":
						case "CLOSED":
						case "CANCELLED":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						bool terminal = flag;
						await using (SqlCommand command = new SqlCommand("UPDATE dbo.support_tickets SET status=CASE WHEN @technician IS NOT NULL AND @status='OPEN' THEN 'IN_PROGRESS' ELSE @status END,priority=@priority,resolution=@resolution,assigned_technician_id=@technician,assigned_team=@team,\r\n    resolved_by_user_id=CASE WHEN @terminal=1 THEN @userId ELSE resolved_by_user_id END,\r\n    resolved_by_name=CASE WHEN @terminal=1 THEN @actor ELSE resolved_by_name END,\r\n    resolved_at=CASE WHEN @terminal=1 THEN COALESCE(resolved_at,SYSUTCDATETIME()) ELSE NULL END,\r\n    updated_at=SYSUTCDATETIME(),closed_at=CASE WHEN @status IN('CLOSED','CANCELLED') THEN COALESCE(closed_at,SYSUTCDATETIME()) ELSE NULL END\r\nWHERE id=@id;", connection, transaction))
						{
							command.Parameters.AddWithValue("@id", id);
							command.Parameters.AddWithValue("@status", body.Status);
							command.Parameters.AddWithValue("@priority", body.Priority);
							command.Parameters.AddWithValue("@resolution", Db(string.IsNullOrWhiteSpace(body.Resolution) ? null : body.Resolution.Trim()));
							command.Parameters.AddWithValue("@technician", Db(string.IsNullOrWhiteSpace(body.AssignedTechnicianId) ? null : body.AssignedTechnicianId));
							command.Parameters.AddWithValue("@team", Db(requestedTeam));
							command.Parameters.AddWithValue("@terminal", terminal ? 1 : 0);
							command.Parameters.AddWithValue("@userId", userId);
							command.Parameters.AddWithValue("@actor", actorName);
							await command.ExecuteNonQueryAsync(ct);
						}
						string action;
						if (terminal)
						{
							action = body.Status;
						}
						else if (changesResponsible)
						{
							if (requestedTechnician == null)
							{
								action = "UNASSIGNED";
							}
							else
							{
								action = ((state.TechnicianId == null) ? "ASSIGNED" : "REASSIGNED");
							}
						}
						else
						{
							action = "STATUS_CHANGED";
						}
						await AddTicketHistory(connection, transaction, id, action, state.Department, state.Department, state.Team, requestedTeam, userId, actorName, body.Resolution, ct);
						if (changesResponsible && requestedTechnician != null)
						{
							await AddTicketNotifications(connection, transaction, id, (state.TechnicianId == null) ? "ASSIGNED" : "REASSIGNED", requestedTechnician, null, state.AgencyGroup, userId, actorName, (state.TechnicianId == null) ? "Se te asignó un ticket de soporte." : "Se te reasignó un ticket de soporte.", notifySupervisors: false, ct);
						}
						if (!terminal && body.Status == "PENDING" && state.Status != "PENDING")
						{
							await AddTicketNotifications(connection, transaction, id, "PENDING", requestedTechnician, state.Department, state.AgencyGroup, userId, actorName, "El ticket quedó pendiente: " + body.Resolution, notifySupervisors: false, ct);
						}
						if (!terminal && body.Status == "IN_PROGRESS" && state.Status == "PENDING")
						{
							await AddTicketNotifications(connection, transaction, id, "UPDATED", requestedTechnician, state.Department, state.AgencyGroup, userId, actorName, "El ticket fue reanudado y volvió a estar en proceso.", notifySupervisors: false, ct);
						}
						if (terminal)
						{
							await AddTicketNotifications(connection, transaction, id, body.Status, null, state.Department, state.AgencyGroup, userId, actorName, $"El ticket fue {((body.Status == "CLOSED") ? "cerrado" : ((body.Status == "CANCELLED") ? "anulado" : "resuelto"))} por {actorName}.", notifySupervisors: true, ct);
						}
						await transaction.CommitAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					}
					end_IL_0315:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> RouteTicket(Guid id, TicketRoute body, string userId, string role, string actorName, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					TicketWorkflowState state = await LoadTicketState(connection, transaction, id, ct);
					string department;
					TicketActionResult ticketActionResult2;
					string team;
					bool flag;
					if ((object)state == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Ticket no encontrado.");
					}
					else
					{
						switch (state.Status)
						{
						case "RESOLVED":
						case "CLOSED":
						case "CANCELLED":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (!flag)
						{
							department = body.AssignedDepartment.Trim().ToUpperInvariant();
							await using (SqlCommand catalog = new SqlCommand("SELECT COUNT(*) FROM dbo.support_departments WHERE code=@department AND is_active=1;", connection, transaction))
							{
								catalog.Parameters.AddWithValue("@department", department);
								if (Convert.ToInt32(await catalog.ExecuteScalarAsync(ct)) == 0)
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult2 = new TicketActionResult(Ok: false, "VALIDATION", "El departamento de destino no está activo.");
									goto IL_070b;
								}
							}
							ticketActionResult2 = null;
							team = ((!(department == "TECHNOLOGY")) ? null : (body.AssignedTeam?.Trim().ToUpperInvariant() ?? ((department == state.Department) ? state.Team : "TECHNICAL_FAILURE")));
							if (team != null)
							{
								switch (team)
								{
								case "CALL_CENTER":
								case "TECHNICAL_FAILURE":
								case "TECHNICIANS":
									break;
								default:
									goto IL_07d5;
								}
							}
							flag = true;
							goto IL_07d8;
						}
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "No se puede reasignar un ticket finalizado.");
					}
					goto end_IL_0313;
					IL_07d8:
					bool supervisorEscalation;
					string requestedTechnician;
					bool changesResponsible;
					if (!flag)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "El equipo de Tecnología no es válido.");
					}
					else
					{
						supervisorEscalation = supportTeam == "CALL_CENTER" && team == "TECHNICIANS" && !string.IsNullOrWhiteSpace(body.AssignedTechnicianId);
						bool callCenterEscalation = supportTeam == "CALL_CENTER" && state.TicketType == "INTERNAL" && state.Department == "TECHNOLOGY" && state.Team == "CALL_CENTER" && department == "TECHNOLOGY" && ((team == "TECHNICAL_FAILURE" && string.IsNullOrWhiteSpace(body.AssignedTechnicianId)) | supervisorEscalation);
						if (supervisorEscalation)
						{
							await using SqlCommand catalog = new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@id AND role='GROUP_ADMIN' AND is_active=1;", connection, transaction);
							catalog.Parameters.AddWithValue("@id", body.AssignedTechnicianId);
							if (Convert.ToInt32(await catalog.ExecuteScalarAsync(ct)) == 0)
							{
								await transaction.RollbackAsync(ct);
								ticketActionResult2 = new TicketActionResult(Ok: false, "VALIDATION", "Selecciona un supervisor activo para recibir el ticket.");
								goto IL_0b5f;
							}
						}
						if (supportTeam == "CALL_CENTER" && !callCenterEscalation)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Call Center solo puede escalar sus tickets internos a Avería Técnica o a un supervisor.");
						}
						else
						{
							bool flag2 = role == "Technology";
							bool flag3 = flag2;
							if (flag3)
							{
								switch (supportTeam)
								{
								case "TECHNICIANS":
								case "WAREHOUSE":
								case "WORKSHOP":
									flag = true;
									break;
								default:
									flag = false;
									break;
								}
								flag3 = flag;
							}
							if (flag3)
							{
								await transaction.RollbackAsync(ct);
								ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Los técnicos no pueden asignar ni transferir tickets.");
							}
							else
							{
								requestedTechnician = (string.IsNullOrWhiteSpace(body.AssignedTechnicianId) ? null : body.AssignedTechnicianId);
								changesResponsible = !string.Equals(state.TechnicianId, requestedTechnician, StringComparison.OrdinalIgnoreCase);
								bool flag4 = !string.Equals(state.Department, department, StringComparison.OrdinalIgnoreCase);
								if ((state.TechnicianId != null) & flag4)
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Primero deja el ticket sin responsable antes de transferirlo a otro departamento.");
								}
								else if (changesResponsible && !callCenterEscalation)
								{
									if (await CanAdministerTicket(connection, transaction, id, role, supportTeam, canAssignTickets: true, ct))
									{
										goto IL_1069;
									}
									await transaction.RollbackAsync(ct);
									ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el administrador o el soporte del área propietaria puede cambiar el responsable.");
								}
								else
								{
									bool flag5 = !callCenterEscalation;
									bool flag6 = flag5;
									if (flag6)
									{
										flag6 = !(await CanWorkTicket(connection, transaction, id, userId, role, supportTeam, canAssignTickets: true, ct));
									}
									if (!flag6)
									{
										goto IL_1069;
									}
									await transaction.RollbackAsync(ct);
									ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "No tienes permiso para reasignar este ticket.");
								}
							}
						}
					}
					goto end_IL_0313;
					IL_070b:
					ticketActionResult = ticketActionResult2;
					goto end_IL_0313;
					IL_1069:
					if (supportTeam == "CALL_CENTER" && state.TicketType != "INTERNAL")
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Call Center solo puede escalar tickets internos.");
					}
					else if (!(await ValidTicketAssignee(connection, transaction, body.AssignedTechnicianId, department, team, state.TicketType, state.AgencyGroup, ct)))
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "El técnico seleccionado no pertenece al destino o no supervisa esta agencia.");
					}
					else
					{
						await using (SqlCommand catalog = new SqlCommand("UPDATE dbo.support_tickets SET assigned_department=@department,assigned_team=@team,assigned_technician_id=@technician,status=CASE WHEN @technician IS NULL THEN 'OPEN' ELSE 'IN_PROGRESS' END,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
						{
							catalog.Parameters.AddWithValue("@id", id);
							catalog.Parameters.AddWithValue("@department", department);
							catalog.Parameters.AddWithValue("@team", Db(team));
							catalog.Parameters.AddWithValue("@technician", Db(string.IsNullOrWhiteSpace(body.AssignedTechnicianId) ? null : body.AssignedTechnicianId));
							await catalog.ExecuteNonQueryAsync(ct);
						}
						bool assigned = requestedTechnician != null;
						bool flag7 = !assigned && (state.Department != department || state.Team != team);
						string action;
						if (supervisorEscalation)
						{
							action = "ESCALATED_TO_SUPERVISOR";
						}
						else if (flag7)
						{
							action = "ESCALATED";
						}
						else if (changesResponsible)
						{
							if (assigned)
							{
								action = ((state.TechnicianId == null) ? "ASSIGNED" : "REASSIGNED");
							}
							else
							{
								action = "UNASSIGNED";
							}
						}
						else
						{
							action = "ROUTED";
						}
						await AddTicketHistory(connection, transaction, id, action, state.Department, department, state.Team, team, userId, actorName, body.Comment, ct);
						SqlConnection connection2 = connection;
						SqlTransaction transaction2 = transaction;
						Guid ticketId = id;
						string eventType = action;
						string recipientUserId = requestedTechnician;
						string targetDepartment = department;
						string agencyGroup = state.AgencyGroup;
						string userId2 = userId;
						string actorName2 = actorName;
						string message;
						if (assigned)
						{
							message = ((state.TechnicianId == null) ? "Se te asignó un ticket de soporte." : "Se te reasignó un ticket de soporte.");
						}
						else
						{
							message = "El ticket fue enviado a " + department + ((team == null) ? "" : (" / " + team)) + ".";
						}
						await AddTicketNotifications(connection2, transaction2, ticketId, eventType, recipientUserId, targetDepartment, agencyGroup, userId2, actorName2, message, !assigned, ct);
						await transaction.CommitAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					}
					goto end_IL_0313;
					IL_07d5:
					flag = false;
					goto IL_07d8;
					IL_0b5f:
					ticketActionResult = ticketActionResult2;
					end_IL_0313:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> ShareTicket(Guid id, TicketShare body, string userId, string role, string actorName, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					TicketWorkflowState state = await LoadTicketState(connection, transaction, id, ct);
					List<string> departments;
					if ((object)state == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Ticket no encontrado.");
					}
					else
					{
						bool flag;
						switch (state.Status)
						{
						case "RESOLVED":
						case "CLOSED":
						case "CANCELLED":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (flag)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "No se puede compartir un ticket finalizado.");
						}
						else
						{
							bool flag2 = role == "Technology";
							bool flag3 = flag2;
							if (flag3)
							{
								switch (supportTeam)
								{
								case "TECHNICIANS":
								case "WAREHOUSE":
								case "WORKSHOP":
									flag = true;
									break;
								default:
									flag = false;
									break;
								}
								flag3 = flag;
							}
							if (flag3)
							{
								await transaction.RollbackAsync(ct);
								ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Los técnicos no pueden compartir ni transferir tickets.");
							}
							else if (!(await CanWorkTicket(connection, transaction, id, userId, role, supportTeam, canAssignTickets: true, ct)))
							{
								await transaction.RollbackAsync(ct);
								ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "No tienes permiso para compartir este ticket.");
							}
							else
							{
								departments = (from value in body.Departments ?? new List<string>()
									where !string.IsNullOrWhiteSpace(value)
									select value.Trim().ToUpperInvariant() into value
									where value != state.Department
									select value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
								if (departments.Count <= 0)
								{
									goto IL_0ad0;
								}
								HashSet<string> valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
								await using (SqlCommand command = new SqlCommand("SELECT code FROM dbo.support_departments WHERE is_active=1;", connection, transaction))
								{
									await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
									while (await reader.ReadAsync(ct))
									{
										valid.Add(reader.GetString(0));
									}
								}
								if (!departments.Any((string value) => !valid.Contains(value)))
								{
									goto IL_0ad0;
								}
								await transaction.RollbackAsync(ct);
								ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "Uno de los departamentos seleccionados no está activo.");
							}
						}
					}
					goto end_IL_02bf;
					IL_0ad0:
					await using (SqlCommand command = new SqlCommand("DELETE FROM dbo.support_ticket_shares WHERE ticket_id=@ticket;", connection, transaction))
					{
						command.Parameters.AddWithValue("@ticket", id);
						await command.ExecuteNonQueryAsync(ct);
					}
					foreach (string department in departments)
					{
						await using SqlCommand command = new SqlCommand("INSERT INTO dbo.support_ticket_shares(ticket_id,department_code,shared_by_user_id,shared_by_name) VALUES(@ticket,@department,@userId,@actor);", connection, transaction);
						command.Parameters.AddWithValue("@ticket", id);
						command.Parameters.AddWithValue("@department", department);
						command.Parameters.AddWithValue("@userId", userId);
						command.Parameters.AddWithValue("@actor", actorName);
						await command.ExecuteNonQueryAsync(ct);
						await AddTicketNotifications(connection, transaction, id, "ESCALATED", null, department, state.AgencyGroup, userId, actorName, actorName + " compartió un ticket con " + department + ".", notifySupervisors: false, ct);
					}
					SqlConnection connection2 = connection;
					SqlTransaction transaction2 = transaction;
					Guid ticketId = id;
					string department2 = state.Department;
					string department3 = state.Department;
					string? team = state.Team;
					string? team2 = state.Team;
					string userId2 = userId;
					string actorName2 = actorName;
					string? comment;
					if (string.IsNullOrWhiteSpace(body.Comment))
					{
						comment = ((departments.Count == 0) ? "Se retiraron los departamentos compartidos." : ("Compartido con: " + string.Join(", ", departments)));
					}
					else
					{
						comment = body.Comment;
					}
					await AddTicketHistory(connection2, transaction2, ticketId, "SHARED", department2, department3, team, team2, userId2, actorName2, comment, ct);
					await transaction.CommitAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					end_IL_02bf:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> ResolveTicket(Guid id, string status, string resolution, List<TicketEvidenceUpload> evidence, string userId, string role, string actorName, string? supportTeam, bool canAssignTickets, bool canResolveTickets, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		if (role != "GroupAdministrator" && !canResolveTickets)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Tu perfil no tiene permiso para resolver tickets.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					TicketWorkflowState state = await LoadTicketState(connection, transaction, id, ct);
					TicketActionResult ticketActionResult2;
					if ((object)state == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Ticket no encontrado.");
					}
					else
					{
						bool flag;
						switch (state.Status)
						{
						case "RESOLVED":
						case "CLOSED":
						case "CANCELLED":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (flag)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Este ticket ya fue finalizado.");
						}
						else if (!(await CanWorkTicket(connection, transaction, id, userId, role, supportTeam, canAssignTickets, ct)))
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "El ticket no está asignado a tu usuario o departamento.");
						}
						else
						{
							await using (SqlCommand linkedRequest = new SqlCommand("IF OBJECT_ID(N'dbo.institutional_files',N'U') IS NOT NULL SELECT TOP(1) metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.warehouseRequest.ticketId'))=@ticket ORDER BY id DESC", connection, transaction))
							{
								linkedRequest.Parameters.AddWithValue("@ticket", id);
								if (await linkedRequest.ExecuteScalarAsync(ct) is string json)
								{
									InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
									if (institutionalFile == null || institutionalFile.WarehouseAssigned.Count < 1 || institutionalFile.Status != "approved" || (object)institutionalFile.WarehouseDelivery == null)
									{
										await transaction.RollbackAsync(ct);
										ticketActionResult2 = new TicketActionResult(Ok: false, "CONFLICT", "El ticket tiene una solicitud de Almacén vinculada. Primero debe autorizarse, retirarse y entregarse el equipo antes de registrar la instalación.");
										goto IL_086e;
									}
								}
							}
							bool flag2 = role == "Technology" && supportTeam == "TECHNICIANS";
							bool flag3 = flag2;
							if (!flag3)
							{
								flag = ((role == "GeneralServices" || role == "HumanResources") ? true : false);
								flag3 = flag && !canAssignTickets;
							}
							if (flag3 && evidence.Any((TicketEvidenceUpload ticketEvidenceUpload2) => !ticketEvidenceUpload2.Latitude.HasValue || !ticketEvidenceUpload2.Longitude.HasValue || !ticketEvidenceUpload2.AccuracyMeters.HasValue || !ticketEvidenceUpload2.LocationCapturedAt.HasValue))
							{
								await transaction.RollbackAsync(ct);
								ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "La evidencia del técnico asignado debe incluir GPS, coordenadas, precisión y hora de captura.");
							}
							else
							{
								if (!(role == "GroupAdministrator"))
								{
									goto IL_0d0f;
								}
								if (!string.Equals(state.TechnicianId, userId, StringComparison.OrdinalIgnoreCase))
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "El supervisor solo puede resolver tickets asignados directamente a su cuenta.");
								}
								else if (!state.AgencyLatitude.HasValue || !state.AgencyLongitude.HasValue)
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "La agencia no tiene una ubicación verificada. Completa primero su Auditoría de campo.");
								}
								else
								{
									DateTime now = DateTime.UtcNow;
									if (evidence.Any((TicketEvidenceUpload ticketEvidenceUpload2) => !ticketEvidenceUpload2.Latitude.HasValue || !ticketEvidenceUpload2.Longitude.HasValue || !ticketEvidenceUpload2.AccuracyMeters.HasValue || !ticketEvidenceUpload2.LocationCapturedAt.HasValue || ticketEvidenceUpload2.AccuracyMeters <= 0.0 || ticketEvidenceUpload2.AccuracyMeters > 50.0 || ticketEvidenceUpload2.LocationCapturedAt < now.AddMinutes(-10.0) || ticketEvidenceUpload2.LocationCapturedAt > now.AddMinutes(2.0)))
									{
										await transaction.RollbackAsync(ct);
										ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "La resolución del supervisor requiere GPS reciente, hora de captura y precisión máxima de 50 metros.");
									}
									else
									{
										TicketEvidenceUpload ticketEvidenceUpload = evidence[0];
										double distance = Haversine(ticketEvidenceUpload.Latitude.Value, ticketEvidenceUpload.Longitude.Value, state.AgencyLatitude.Value, state.AgencyLongitude.Value);
										if (!(distance > 75.0))
										{
											goto IL_0d0f;
										}
										await transaction.RollbackAsync(ct);
										ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", $"Debes resolver el ticket desde la agencia. La evidencia se capturó a {Math.Round(distance)} metros del punto verificado.");
									}
								}
							}
						}
					}
					goto end_IL_032b;
					IL_086e:
					ticketActionResult = ticketActionResult2;
					goto end_IL_032b;
					IL_0d0f:
					await using (SqlCommand linkedRequest = new SqlCommand("UPDATE dbo.support_tickets SET status=@status,resolution=@resolution,resolved_by_user_id=@userId,resolved_by_name=@actor,\r\n    resolved_at=SYSUTCDATETIME(),closed_at=CASE WHEN @status='CLOSED' THEN SYSUTCDATETIME() ELSE NULL END,updated_at=SYSUTCDATETIME()\r\nWHERE id=@id;", connection, transaction))
					{
						linkedRequest.Parameters.AddWithValue("@id", id);
						linkedRequest.Parameters.AddWithValue("@status", status);
						linkedRequest.Parameters.AddWithValue("@resolution", resolution);
						linkedRequest.Parameters.AddWithValue("@userId", userId);
						linkedRequest.Parameters.AddWithValue("@actor", actorName);
						await linkedRequest.ExecuteNonQueryAsync(ct);
					}
					foreach (TicketEvidenceUpload item in evidence)
					{
						await using SqlCommand linkedRequest = new SqlCommand("INSERT INTO dbo.support_ticket_evidence(ticket_id,file_name,content_type,photo_data,uploaded_by_user_id,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at,location_source) VALUES(@ticket,@fileName,@contentType,@data,@userId,@actor,@latitude,@longitude,@accuracy,@locationCapturedAt,@locationSource);", connection, transaction);
						linkedRequest.Parameters.AddWithValue("@ticket", id);
						linkedRequest.Parameters.AddWithValue("@fileName", item.FileName);
						linkedRequest.Parameters.AddWithValue("@contentType", item.ContentType);
						SqlParameter sqlParameter = linkedRequest.Parameters.Add("@data", SqlDbType.VarBinary, -1);
						sqlParameter.Value = await StorageFiles.Store(item.Data, "ticket-evidence", ct);
						linkedRequest.Parameters.AddWithValue("@userId", userId);
						linkedRequest.Parameters.AddWithValue("@actor", actorName);
						linkedRequest.Parameters.AddWithValue("@latitude", Db(item.Latitude));
						linkedRequest.Parameters.AddWithValue("@longitude", Db(item.Longitude));
						linkedRequest.Parameters.AddWithValue("@accuracy", Db(item.AccuracyMeters));
						linkedRequest.Parameters.AddWithValue("@locationCapturedAt", Db(item.LocationCapturedAt));
						linkedRequest.Parameters.AddWithValue("@locationSource", Db((!item.Latitude.HasValue) ? null : "DEVICE_GPS"));
						await linkedRequest.ExecuteNonQueryAsync(ct);
					}
					await LinkWarehouseTicketResolution(connection, transaction, id, userId, actorName, ct);
					string resolutionMessage = $"El ticket fue {((status == "CLOSED") ? "cerrado" : "resuelto")} por {actorName}.";
					await AddTicketHistory(connection, transaction, id, status, state.Department, state.Department, state.Team, state.Team, userId, actorName, resolution, ct);
					await AddTicketNotifications(connection, transaction, id, status, null, state.Department, state.AgencyGroup, userId, actorName, resolutionMessage, notifySupervisors: true, ct);
					if (state.Department == "TECHNOLOGY")
					{
						await AddTechnologyResolutionNotifications(connection, transaction, id, status, userId, actorName, resolutionMessage, ct);
					}
					await transaction.CommitAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					end_IL_032b:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<(byte[] Data, string Mime, string FileName)?> TicketEvidencePhoto(Guid evidenceId, string userId, string role, bool canAssignTickets, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		(byte[] Data, string Mime, string FileName)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			(byte[] Data, string Mime, string FileName)? tuple2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP(1)e.photo_data,e.content_type,e.file_name\r\nFROM dbo.support_ticket_evidence e\r\nINNER JOIN dbo.support_tickets t ON t.id=e.ticket_id INNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nOUTER APPLY (SELECT CASE WHEN\r\n    t.assigned_technician_id=@userId\r\n    OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))\r\n    OR EXISTS(SELECT 1 FROM dbo.admin_users actor_account WHERE CONVERT(nvarchar(80),actor_account.id)=@userId AND (LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(actor_account.email))) OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LEFT(LTRIM(RTRIM(actor_account.email)),CHARINDEX('@',LTRIM(RTRIM(actor_account.email))+'@')-1))))\r\n    THEN 1 ELSE 0 END AS is_current) assignment\r\nWHERE e.id=@evidence AND (\r\n    @administrator=1\r\n    OR (@groupSupervisor=1 AND (assignment.is_current=1 OR t.created_by_user_id=@userId))\r\n    OR (t.created_by_user_id=@userId AND EXISTS(SELECT 1 FROM dbo.admin_users requester WHERE CONVERT(nvarchar(80),requester.id)=@userId AND requester.role='DEPARTMENT' AND requester.support_team=@department))\r\n    OR (@department<>'' AND (\r\n        (@supportTeam='CALL_CENTER' AND (\r\n            (t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')\r\n            OR (t.ticket_type='INTERNAL' AND EXISTS(SELECT 1 FROM dbo.support_ticket_history cc\r\n                WHERE cc.ticket_id=t.id AND cc.from_team='CALL_CENTER' AND (\r\n                    (cc.action='ESCALATED' AND cc.to_team='TECHNICAL_FAILURE')\r\n                    OR (cc.action='ESCALATED_TO_SUPERVISOR' AND cc.to_team='TECHNICIANS'))))\r\n        ))\r\n        OR (@supportTeam<>'CALL_CENTER' AND (\r\n            (assignment.is_current=1 AND t.assigned_department=@department)\r\n            OR (@canAssign=1 AND (\r\n                (t.assigned_department=@department AND (@supportTeam='' OR t.assigned_team IS NULL OR t.assigned_team=@supportTeam OR (@supportTeam='TECHNICAL_FAILURE' AND t.assigned_team='TECHNICIANS')))\r\n                OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@department)\r\n            ))\r\n        ))\r\n    ))\r\n);", connection))
			{
				command.Parameters.AddWithValue("@evidence", evidenceId);
				command.Parameters.AddWithValue("@administrator", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@groupSupervisor", (role == "GroupAdministrator") ? 1 : 0);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				command.Parameters.AddWithValue("@supportTeam", DepartmentAccounts.IsSupportScope(role) ? "" : (supportTeam ?? ""));
				command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
				command.Parameters.AddWithValue("@userId", userId);
				(byte[] Data, string Mime, string FileName)? tuple;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct))
				{
					tuple = ((await reader.ReadAsync(ct)) ? new (byte[], string, string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1), reader.GetString(2))) : (((byte[], string, string)?)null));
				}
				tuple2 = tuple;
			}
			result = tuple2;
		}
		return result;
	}

	public async Task<List<SupportTechnicianRow>> SupportTechnicians(CancellationToken ct)
	{
		List<SupportTechnicianRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserPermissionSchema(connection, ct);
			List<SupportTechnicianRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT CONVERT(nvarchar(80),u.id),u.display_name,\r\n       CASE u.role WHEN 'TECHNOLOGY' THEN 'TECHNOLOGY' WHEN 'GENERAL_SERVICES' THEN 'GENERAL_SERVICES' WHEN 'HUMAN_RESOURCES' THEN 'HUMAN_RESOURCES' WHEN 'DEPARTMENT' THEN COALESCE(u.support_team,'') ELSE '' END,\r\n       u.role,COALESCE(g.names,N''),u.support_team,\r\n       CASE WHEN u.role<>'GROUP_ADMIN' AND (\r\n           u.support_team='TECHNICIANS' OR (u.role='GENERAL_SERVICES' AND (u.support_team='WORKSHOP' OR JSON_VALUE(u.permissions_json,'$.canAssignTickets')='false')) OR (ISNULL(u.support_team,'') NOT IN('WAREHOUSE','WORKSHOP') AND\r\n               JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'\r\n               AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true'\r\n           )\r\n       ) THEN 1 ELSE 0 END\r\nFROM dbo.admin_users u\r\nOUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id)g\r\nWHERE u.is_active=1 AND u.role IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','GROUP_ADMIN','DEPARTMENT')\r\nORDER BY CASE WHEN u.role='GROUP_ADMIN' THEN 2 ELSE 1 END,u.display_name;", connection))
			{
				List<SupportTechnicianRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<SupportTechnicianRow> rows = new List<SupportTechnicianRow>();
					while (await reader.ReadAsync(ct))
					{
						string text = reader.GetString(3);
						rows.Add(new SupportTechnicianRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), ClaimRole(text), reader.GetString(4).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList(), text == "GROUP_ADMIN", reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetInt32(6) == 1));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<bool> DeleteTicket(Guid id, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			bool flag2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				await using (SqlCommand audit = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data) SELECT 'support_ticket',CONVERT(char(36),id),'TICKET_DELETED',CONCAT(N'{\"ticketNumber\":',ticket_number,N',\"subject\":\"',STRING_ESCAPE(subject,'json'),N'\"}') FROM dbo.support_tickets WHERE id=@id;", connection, transaction))
				{
					audit.Parameters.AddWithValue("@id", id);
					await audit.ExecuteNonQueryAsync(ct);
				}
				bool flag;
				await using (SqlCommand command = new SqlCommand("DELETE FROM dbo.support_tickets WHERE id=@id;", connection, transaction))
				{
					command.Parameters.AddWithValue("@id", id);
					bool deleted = await command.ExecuteNonQueryAsync(ct) == 1;
					if (!deleted)
					{
						await transaction.RollbackAsync(ct);
					}
					else
					{
						await transaction.CommitAsync(ct);
					}
					flag = deleted;
				}
				flag2 = flag;
			}
			result = flag2;
		}
		return result;
	}

	public async Task<int> TicketAlerts(string userId, string role, CancellationToken ct)
	{
		int result;
		await using (SqlConnection connection = await Open(ct))
		{
			int num;
			await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nWHERE t.status IN ('OPEN','IN_PROGRESS','PENDING')\r\n  AND (@department='' OR t.assigned_department=@department)\r\n  AND (@ownOnly=0 OR t.created_by_user_id=@userId OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)))\r\n  AND (@scoped=0 OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)) OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));", connection))
			{
				AddScope(command, userId, role);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				command.Parameters.AddWithValue("@ownOnly", (role == "GroupAdministrator") ? 1 : 0);
				num = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
			}
			result = num;
		}
		return result;
	}

	public async Task<List<TicketNotificationRow>> ResolvedTicketNotifications(string userId, string role, CancellationToken ct)
	{
		bool flag;
		switch (role)
		{
		case "GroupAdministrator":
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
		if (!flag)
		{
			return new List<TicketNotificationRow>();
		}
		List<TicketNotificationRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<TicketNotificationRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP (100) t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.assigned_department,\r\n       t.subject,t.resolution,t.status,t.updated_at\r\nFROM dbo.support_tickets t\r\nINNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nWHERE (@allProcesses=1 OR @supportDepartment<>'' OR t.created_by_user_id=@userId OR (@supervisor=1 AND LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))))\r\n  AND (@allProcesses=1\r\n       OR (@supportDepartment<>'' AND t.assigned_department=@supportDepartment AND t.status='OPEN')\r\n       OR (@supervisor=1 AND t.status IN ('IN_PROGRESS','PENDING','RESOLVED','CLOSED','CANCELLED')))\r\n  AND (@allProcesses=1 OR @supportDepartment<>'' OR EXISTS(\r\n        SELECT 1 FROM dbo.admin_user_groups ug\r\n        WHERE ug.user_id=@userId AND ug.group_name=a.grupo\r\n  ))\r\nORDER BY t.updated_at DESC,t.ticket_number DESC;", connection))
			{
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@allProcesses", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@supervisor", (role == "GroupAdministrator") ? 1 : 0);
				command.Parameters.AddWithValue("@supportDepartment", DepartmentForRole(role));
				List<TicketNotificationRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<TicketNotificationRow> rows = new List<TicketNotificationRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new TicketNotificationRow(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetDateTime(9)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<int> TicketAlerts(string userId, string role, bool canAssignTickets, string? supportTeam, string? userEmail, CancellationToken ct)
	{
		return Enumerable.Count(await Tickets(userId, role, canAssignTickets, supportTeam, userEmail, ct), (SupportTicketRow ticket) =>
		{
			switch (ticket.Status)
			{
			case "OPEN":
			case "IN_PROGRESS":
			case "PENDING":
				return true;
			default:
				return false;
			}
		});
	}

	public async Task<List<TicketNotificationRow>> ResolvedTicketNotifications(string userId, string role, bool canAssignTickets, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool flag;
		switch (role)
		{
		case "GroupAdministrator":
		case "Administrator":
		case "Viewer":
		case "Fiscalizador":
		case "Technology":
		case "GeneralServices":
		case "HumanResources":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return new List<TicketNotificationRow>();
		}
		List<TicketNotificationRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			await EnsureNotificationReceiptSchema(connection, ct);
			List<TicketNotificationRow> list2;
			await using (SqlCommand command = new SqlCommand("DECLARE @firstNotificationLoad bit=CASE WHEN EXISTS(SELECT 1 FROM dbo.support_notification_receipts WHERE user_id=@userId AND notification_key=N'SYSTEM:TICKET_INITIALIZED') THEN 0 ELSE 1 END;\r\nMERGE dbo.support_notification_receipts WITH(HOLDLOCK) AS target USING(SELECT @userId user_id,N'SYSTEM:TICKET_INITIALIZED' notification_key) source ON target.user_id=source.user_id AND target.notification_key=source.notification_key WHEN NOT MATCHED THEN INSERT(user_id,notification_key,read_at) VALUES(source.user_id,source.notification_key,SYSUTCDATETIME());\r\nSELECT TOP(100)t.id,t.ticket_number,a.codigo,a.terminal,a.grupo,t.assigned_department,t.subject,t.resolution,t.status,\r\n       n.created_at,n.event_type,n.message,n.actor_name,n.id,ISNULL(t.ticket_type,'SUPPORT'),CONVERT(bit,CASE WHEN @firstNotificationLoad=1 OR receipt.read_at IS NOT NULL THEN 1 ELSE 0 END)\r\nFROM dbo.support_ticket_notifications n\r\nINNER JOIN dbo.support_tickets t ON t.id=n.ticket_id INNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nLEFT JOIN dbo.support_notification_receipts receipt ON receipt.user_id=@userId AND receipt.notification_key=CONCAT(N'TICKET_EVENT:',CONVERT(nvarchar(36),n.id))\r\nWHERE (n.recipient_user_id=@userId\r\n   OR (@administrator=1 AND (n.recipient_user_id IS NULL OR n.event_type='ASSIGNED'))\r\n   OR (@groupSupervisor=1 AND n.recipient_user_id=@userId)\r\n   OR (@department<>'' AND (n.recipient_user_id=@userId OR (@supportTeam<>'TECHNICIANS' AND @canAssign=1 AND n.target_department=@department))))\r\n  AND (@supportTeam='' OR n.recipient_user_id=@userId OR (\r\n        (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')\r\n        OR (@supportTeam<>'CALL_CENTER' AND (\r\n            (t.assigned_department='TECHNOLOGY' AND (t.assigned_team IS NULL OR t.assigned_team=@supportTeam))\r\n            OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares shared WHERE shared.ticket_id=t.id AND shared.department_code='TECHNOLOGY')\r\n        ))\r\n  )) AND receipt.dismissed_at IS NULL\r\nORDER BY n.created_at DESC,n.id DESC;", connection))
			{
				command.Parameters.AddWithValue("@administrator", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@groupSupervisor", (role == "GroupAdministrator") ? 1 : 0);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
				command.Parameters.AddWithValue("@supportTeam", DepartmentAccounts.IsSupportScope(role) ? "" : (supportTeam ?? ""));
				command.Parameters.AddWithValue("@userId", userId);
				List<TicketNotificationRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<TicketNotificationRow> rows = new List<TicketNotificationRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new TicketNotificationRow(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetDateTime(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.GetGuid(13), reader.GetString(14), reader.GetBoolean(15)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<TicketNotificationRow>> AutomaticFindingNotifications(string userId, string role, CancellationToken ct)
	{
		List<TicketNotificationRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			await EnsureNotificationReceiptSchema(connection, ct);
			List<TicketNotificationRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP(100) af.id,CONVERT(bigint,0),a.codigo,a.terminal,a.grupo,af.assigned_department,af.title,af.resolution,af.status,n.created_at,n.event_type,n.message,n.actor_name,n.id,CONVERT(varchar(20),'FINDING'),CONVERT(bit,CASE WHEN receipt.read_at IS NOT NULL THEN 1 ELSE 0 END) FROM dbo.automatic_finding_notifications n INNER JOIN dbo.automatic_findings af ON af.id=n.finding_id INNER JOIN dbo.agencies a ON a.id=af.agency_id LEFT JOIN dbo.support_notification_receipts receipt ON receipt.user_id=@userId AND receipt.notification_key=CONCAT(N'FINDING_EVENT:',CONVERT(nvarchar(36),n.id)) WHERE n.recipient_user_id=@userId AND receipt.dismissed_at IS NULL AND (@department='' OR af.assigned_department=@department) ORDER BY n.created_at DESC,n.id DESC;", connection))
			{
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				List<TicketNotificationRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<TicketNotificationRow> rows = new List<TicketNotificationRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new TicketNotificationRow(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetDateTime(9), reader.GetString(10), reader.GetString(11), reader.GetString(12), reader.GetGuid(13), reader.GetString(14), reader.GetBoolean(15)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task SavePushSubscription(string userId, string endpoint, string p256dh, string auth, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\r\nMERGE dbo.pwa_push_subscriptions AS t USING(SELECT @endpoint endpoint) s ON t.endpoint=s.endpoint WHEN MATCHED THEN UPDATE SET user_id=@userId,p256dh=@p256dh,auth_key=@auth,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(user_id,endpoint,p256dh,auth_key) VALUES(@userId,@endpoint,@p256dh,@auth);", connection);
		command.Parameters.AddWithValue("@userId", userId);
		command.Parameters.AddWithValue("@endpoint", endpoint);
		command.Parameters.AddWithValue("@p256dh", p256dh);
		command.Parameters.AddWithValue("@auth", auth);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<List<(string Endpoint, string P256dh, string Auth)>> PushSubscriptions(string userId, CancellationToken ct)
	{
		List<(string Endpoint, string P256dh, string Auth)> result2;
		await using (SqlConnection connection = await Open(ct))
		{
			List<(string Endpoint, string P256dh, string Auth)> list2;
			await using (SqlCommand command = new SqlCommand("SELECT endpoint,p256dh,auth_key FROM dbo.pwa_push_subscriptions WHERE user_id=@userId;", connection))
			{
				command.Parameters.AddWithValue("@userId", userId);
				List<(string Endpoint, string P256dh, string Auth)> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<(string, string, string)> result = new List<(string, string, string)>();
					while (await reader.ReadAsync(ct))
					{
						result.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
					}
					list = result;
				}
				list2 = list;
			}
			result2 = list2;
		}
		return result2;
	}

	public async Task<List<PushDeliveryRow>> PendingPushDeliveries(CancellationToken ct)
	{
		List<PushDeliveryRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureNotificationReceiptSchema(connection, ct);
			List<PushDeliveryRow> list2;
			await using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\r\nIF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint));\r\nSELECT TOP(100)n.id,s.endpoint,s.p256dh,s.auth_key,t.ticket_number,t.subject,n.message,t.id,t.assigned_department,ISNULL(t.ticket_type,'SUPPORT'),t.status,n.created_at\r\nFROM dbo.support_ticket_notifications n\r\nINNER JOIN dbo.support_tickets t ON t.id=n.ticket_id\r\nINNER JOIN dbo.pwa_push_subscriptions s ON 1=1\r\nINNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=s.user_id AND u.is_active=1\r\nWHERE (n.recipient_user_id=s.user_id OR (n.recipient_user_id IS NULL AND n.target_department IS NOT NULL\r\n  AND (u.role=CASE n.target_department WHEN 'TECHNOLOGY' THEN 'TECHNOLOGY' WHEN 'GENERAL_SERVICES' THEN 'GENERAL_SERVICES' WHEN 'HUMAN_RESOURCES' THEN 'HUMAN_RESOURCES' ELSE '' END OR (u.role='DEPARTMENT' AND u.support_team=n.target_department AND JSON_VALUE(u.permissions_json,'$.canViewTicketNotifications')='true'))\r\n  AND (n.target_department<>'TECHNOLOGY' OR u.support_team IS NULL OR u.support_team=t.assigned_team)\r\n  AND (ISNULL(u.support_team,'')<>'TECHNICIANS' OR t.assigned_technician_id=s.user_id)))\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries d WHERE d.notification_id=n.id AND d.endpoint=s.endpoint)\r\n  AND n.created_at>=s.created_at\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.support_notification_receipts receipt WHERE receipt.user_id=s.user_id AND receipt.notification_key=CONCAT(N'TICKET_EVENT:',CONVERT(nvarchar(36),n.id)) AND (receipt.read_at IS NOT NULL OR receipt.dismissed_at IS NOT NULL))\r\nORDER BY n.created_at;", connection))
			{
				List<PushDeliveryRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<PushDeliveryRow> rows = new List<PushDeliveryRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new PushDeliveryRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetGuid(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetDateTime(11)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<ChatPushDeliveryRow>> PendingChatPushDeliveries(CancellationToken ct)
	{
		List<ChatPushDeliveryRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			await EnsureNotificationReceiptSchema(connection, ct);
			List<ChatPushDeliveryRow> list2;
			await using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\r\nIF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint));\r\nIF OBJECT_ID(N'dbo.pwa_push_chat_state',N'U') IS NULL CREATE TABLE dbo.pwa_push_chat_state(endpoint nvarchar(2048) NOT NULL PRIMARY KEY,initialized_at datetime2 NOT NULL);\r\nINSERT INTO dbo.pwa_push_chat_state(endpoint,initialized_at)\r\nSELECT s.endpoint,DATEADD(SECOND,-30,SYSUTCDATETIME()) FROM dbo.pwa_push_subscriptions s WHERE NOT EXISTS(SELECT 1 FROM dbo.pwa_push_chat_state state WHERE state.endpoint=s.endpoint);\r\nSELECT TOP(100)m.id,s.endpoint,s.p256dh,s.auth_key,c.id,c.assigned_department,m.sender_name,m.message,m.created_at\r\nFROM dbo.support_messages m\r\nINNER JOIN dbo.support_conversations c ON c.id=m.conversation_id\r\nINNER JOIN dbo.pwa_push_subscriptions s ON 1=1\r\nINNER JOIN dbo.pwa_push_chat_state push_state ON push_state.endpoint=s.endpoint AND m.created_at>=push_state.initialized_at\r\nINNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=s.user_id AND u.is_active=1\r\nWHERE m.is_bot=0 AND m.deleted_at IS NULL AND c.deleted_at IS NULL\r\n  AND ISNULL(m.sender_user_id,N'')<>s.user_id\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.support_conversation_mutes cm WHERE cm.conversation_id=c.id AND cm.user_id=s.user_id)\r\n  AND ((c.category LIKE 'DIRECT:%' AND (u.role='ADMINISTRATOR' OR c.supervisor_user_id=s.user_id OR SUBSTRING(c.category,8,80)=s.user_id))\r\n    OR (ISNULL(c.category,'') NOT LIKE 'DIRECT:%' AND (u.role='ADMINISTRATOR' OR (u.role='GROUP_ADMIN' AND c.supervisor_user_id=s.user_id)\r\n      OR (c.assigned_department='TECHNOLOGY' AND u.role='TECHNOLOGY')\r\n      OR (c.assigned_department='GENERAL_SERVICES' AND u.role='GENERAL_SERVICES')\r\n      OR (c.assigned_department='HUMAN_RESOURCES' AND u.role='HUMAN_RESOURCES')\r\n      OR (u.role='DEPARTMENT' AND JSON_VALUE(u.permissions_json,'$.canUseSupportChat')='true' AND (u.support_team=c.assigned_department OR c.supervisor_user_id=s.user_id)))))\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries d WHERE d.notification_id=m.id AND d.endpoint=s.endpoint)\r\n  AND m.created_at>=s.created_at\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.support_notification_receipts receipt WHERE receipt.user_id=s.user_id AND receipt.notification_key=CONCAT(N'CHAT_MESSAGE:',CONVERT(nvarchar(36),m.id)) AND (receipt.read_at IS NOT NULL OR receipt.dismissed_at IS NOT NULL))\r\nORDER BY m.created_at;", connection))
			{
				List<ChatPushDeliveryRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<ChatPushDeliveryRow> rows = new List<ChatPushDeliveryRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new ChatPushDeliveryRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetDateTime(8)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<PushDeliveryRow>> PendingFindingPushDeliveries(CancellationToken ct)
	{
		List<PushDeliveryRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			await EnsureNotificationReceiptSchema(connection, ct);
			List<PushDeliveryRow> list2;
			await using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_subscriptions',N'U') IS NULL CREATE TABLE dbo.pwa_push_subscriptions(id uniqueidentifier NOT NULL DEFAULT NEWID() PRIMARY KEY,user_id nvarchar(80) NOT NULL,endpoint nvarchar(2048) NOT NULL UNIQUE,p256dh nvarchar(512) NOT NULL,auth_key nvarchar(512) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()); IF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint)); SELECT TOP(100)n.id,s.endpoint,s.p256dh,s.auth_key,CONVERT(bigint,0),af.title,n.message,af.id,af.assigned_department,CONVERT(varchar(20),'FINDING'),af.status,n.created_at FROM dbo.automatic_finding_notifications n INNER JOIN dbo.automatic_findings af ON af.id=n.finding_id INNER JOIN dbo.pwa_push_subscriptions s ON s.user_id=n.recipient_user_id INNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=s.user_id AND u.is_active=1 WHERE n.created_at>=s.created_at AND NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries d WHERE d.notification_id=n.id AND d.endpoint=s.endpoint) AND NOT EXISTS(SELECT 1 FROM dbo.support_notification_receipts receipt WHERE receipt.user_id=s.user_id AND receipt.notification_key=CONCAT(N'FINDING_EVENT:',CONVERT(nvarchar(36),n.id)) AND (receipt.read_at IS NOT NULL OR receipt.dismissed_at IS NOT NULL)) ORDER BY n.created_at;", connection))
			{
				List<PushDeliveryRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<PushDeliveryRow> rows = new List<PushDeliveryRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new PushDeliveryRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetString(5), reader.GetString(6), reader.GetGuid(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetDateTime(11)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task MarkPushDelivered(Guid notificationId, string endpoint, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.pwa_push_deliveries',N'U') IS NULL CREATE TABLE dbo.pwa_push_deliveries(notification_id uniqueidentifier NOT NULL,endpoint nvarchar(2048) NOT NULL,delivered_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CONSTRAINT PK_pwa_push_deliveries PRIMARY KEY(notification_id,endpoint)); IF NOT EXISTS(SELECT 1 FROM dbo.pwa_push_deliveries WHERE notification_id=@id AND endpoint=@endpoint) INSERT INTO dbo.pwa_push_deliveries(notification_id,endpoint) VALUES(@id,@endpoint);", connection);
		command.Parameters.AddWithValue("@id", notificationId);
		command.Parameters.AddWithValue("@endpoint", endpoint);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task DeletePushSubscription(string endpoint, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("DELETE FROM dbo.pwa_push_subscriptions WHERE endpoint=@endpoint; IF OBJECT_ID(N'dbo.pwa_push_chat_state',N'U') IS NOT NULL DELETE FROM dbo.pwa_push_chat_state WHERE endpoint=@endpoint;", connection);
		command.Parameters.AddWithValue("@endpoint", endpoint);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<TicketStatisticsResult> TicketStatistics(string userId, string role, bool canAssignTickets, string? supportTeam, DateTime? from, DateTime? to, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		TicketStatisticsResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureTicketWorkflowSchema(connection, ct);
			DateTime fromDate = (from ?? DateTime.UtcNow.AddMonths(-12)).Date;
			DateTime toDate = (to ?? DateTime.UtcNow).Date.AddDays(1.0);
			if (toDate <= fromDate)
			{
				toDate = fromDate.AddDays(1.0);
			}
			if ((toDate - fromDate).TotalDays > 1096.0)
			{
				fromDate = toDate.AddYears(-3);
			}
			TicketStatisticsResult ticketStatisticsResult2;
			await using (SqlCommand command = new SqlCommand("DECLARE @visible TABLE(id uniqueidentifier NOT NULL PRIMARY KEY);\r\nINSERT INTO @visible(id)\r\nSELECT t.id FROM dbo.support_tickets t INNER JOIN dbo.agencies a ON a.id=t.agency_id\r\nWHERE t.created_at<@to AND (\r\n    @administrator=1\r\n    OR (@groupSupervisor=1 AND (\r\n        LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))\r\n        OR (t.created_by_user_id=@userId AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))\r\n    ))\r\n        OR (@department<>'' AND (LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId)) OR (@supportTeam<>'TECHNICIANS' AND @canAssign=1 AND (\r\n        (@supportTeam='CALL_CENTER' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')\r\n        OR (@supportTeam='TECHNICAL_FAILURE' AND t.assigned_department='TECHNOLOGY' AND t.assigned_team='TECHNICAL_FAILURE')\r\n        OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE') AND (\r\n            (t.assigned_department=@department AND (@supportTeam='' OR t.assigned_team=@supportTeam))\r\n            OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@department)\r\n        ))\r\n    ))))\r\n);\r\nSELECT SUM(CASE WHEN t.status IN('OPEN','IN_PROGRESS','PENDING') THEN 1 ELSE 0 END),SUM(CASE WHEN t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from THEN 1 ELSE 0 END)\r\nFROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id;\r\nSELECT COALESCE(t.resolved_by_user_id,t.assigned_technician_id),COALESCE(NULLIF(t.resolved_by_name,N''),u.display_name,N'Sin técnico'),t.assigned_department,\r\n       SUM(CASE WHEN t.status='RESOLVED' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='CLOSED' THEN 1 ELSE 0 END),COUNT(*),\r\n       CAST(AVG(CAST(DATEDIFF(MINUTE,t.created_at,COALESCE(t.resolved_at,t.closed_at,t.updated_at)) AS float))/60.0 AS float)\r\nFROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=COALESCE(t.resolved_by_user_id,t.assigned_technician_id)\r\nWHERE t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)<@to\r\nGROUP BY COALESCE(t.resolved_by_user_id,t.assigned_technician_id),COALESCE(NULLIF(t.resolved_by_name,N''),u.display_name,N'Sin técnico'),t.assigned_department ORDER BY COUNT(*) DESC;\r\nSELECT t.assigned_department,t.category,SUM(CASE WHEN t.status='RESOLVED' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='CLOSED' THEN 1 ELSE 0 END),COUNT(*),\r\n       CAST(AVG(CAST(DATEDIFF(MINUTE,t.created_at,COALESCE(t.resolved_at,t.closed_at,t.updated_at)) AS float))/60.0 AS float)\r\nFROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id\r\nWHERE t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)<@to\r\nGROUP BY t.assigned_department,t.category ORDER BY COUNT(*) DESC;\r\nSELECT t.assigned_department,SUM(CASE WHEN t.status='OPEN' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status IN('IN_PROGRESS','PENDING') THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='RESOLVED' THEN 1 ELSE 0 END),SUM(CASE WHEN t.status='CLOSED' THEN 1 ELSE 0 END),COUNT(*),\r\n       COALESCE(CAST(AVG(CASE WHEN t.status IN('RESOLVED','CLOSED') THEN CAST(DATEDIFF(MINUTE,t.created_at,COALESCE(t.resolved_at,t.closed_at,t.updated_at)) AS float) END)/60.0 AS float),0)\r\nFROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id GROUP BY t.assigned_department ORDER BY t.assigned_department;\r\nSELECT CONVERT(date,COALESCE(t.resolved_at,t.closed_at,t.updated_at)),t.assigned_department,COUNT(*)\r\nFROM dbo.support_tickets t INNER JOIN @visible v ON v.id=t.id\r\nWHERE t.status IN('RESOLVED','CLOSED') AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)>=@from AND COALESCE(t.resolved_at,t.closed_at,t.updated_at)<@to\r\nGROUP BY CONVERT(date,COALESCE(t.resolved_at,t.closed_at,t.updated_at)),t.assigned_department ORDER BY 1,2;", connection))
			{
				command.Parameters.AddWithValue("@administrator", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@groupSupervisor", (role == "GroupAdministrator") ? 1 : 0);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
				command.Parameters.AddWithValue("@supportTeam", DepartmentAccounts.IsSupportScope(role) ? "" : (supportTeam ?? ""));
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@from", fromDate);
				command.Parameters.AddWithValue("@to", toDate);
				TicketStatisticsResult ticketStatisticsResult;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					int active = 0;
					int resolved = 0;
					if (await reader.ReadAsync(ct))
					{
						active = ((!reader.IsDBNull(0)) ? reader.GetInt32(0) : 0);
						resolved = ((!reader.IsDBNull(1)) ? reader.GetInt32(1) : 0);
					}
					List<TicketTechnicianStatistic> technicians = new List<TicketTechnicianStatistic>();
					List<TicketCategoryStatistic> categories = new List<TicketCategoryStatistic>();
					List<TicketDepartmentStatistic> departments = new List<TicketDepartmentStatistic>();
					List<TicketHistoryPoint> history = new List<TicketHistoryPoint>();
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							technicians.Add(new TicketTechnicianStatistic(reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? 0.0 : Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture)));
						}
					}
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							categories.Add(new TicketCategoryStatistic(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.IsDBNull(5) ? 0.0 : Convert.ToDouble(reader.GetValue(5), CultureInfo.InvariantCulture)));
						}
					}
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							departments.Add(new TicketDepartmentStatistic(reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? 0.0 : Convert.ToDouble(reader.GetValue(6), CultureInfo.InvariantCulture)));
						}
					}
					if (await reader.NextResultAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							history.Add(new TicketHistoryPoint(reader.GetDateTime(0), reader.GetString(1), reader.GetInt32(2)));
						}
					}
					ticketStatisticsResult = new TicketStatisticsResult(fromDate, toDate.AddDays(-1.0), active, resolved, technicians, categories, departments, history);
				}
				ticketStatisticsResult2 = ticketStatisticsResult;
			}
			result = ticketStatisticsResult2;
		}
		return result;
	}

	public async Task<List<SupportContactRow>> SupportContacts(string userId, CancellationToken ct)
	{
		List<SupportContactRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<SupportContactRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT u.id,u.display_name,u.email,u.role,u.contact,\r\n       CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,\r\n       COALESCE(g.names,N'')\r\nFROM dbo.admin_users u\r\nOUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id)g\r\nWHERE u.is_active=1 AND CONVERT(nvarchar(80),u.id)<>@userId\r\n  AND u.role IN('ADMINISTRATOR','GROUP_ADMIN','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','DEPARTMENT')\r\nORDER BY CASE u.role WHEN 'GROUP_ADMIN' THEN 1 ELSE 2 END,u.display_name;", connection))
			{
				command.Parameters.AddWithValue("@userId", userId);
				List<SupportContactRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<SupportContactRow> rows = new List<SupportContactRow>();
					while (await reader.ReadAsync(ct))
					{
						string text = reader.GetString(0);
						rows.Add(new SupportContactRow(text, reader.GetString(1), reader.GetString(2), ClaimRole(reader.GetString(3)), reader.IsDBNull(4) ? null : reader.GetString(4), ReadBooleanValue(reader, 5) ? ("/api/account/avatar/" + text) : null, reader.GetString(6).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<SupportConversationRow>> SupportConversations(string userId, string role, CancellationToken ct)
	{
		bool flag;
		switch (role)
		{
		case "Administrator":
		case "GroupAdministrator":
		case "Technology":
		case "GeneralServices":
		case "HumanResources":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return new List<SupportConversationRow>();
		}
		List<SupportConversationRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			List<SupportConversationRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT c.id,peer.contact_user_id,\r\n       COALESCE(u.display_name,c.supervisor_name),COALESCE(u.email,c.supervisor_username),u.contact,\r\n       CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,COALESCE(user_groups.names,N''),\r\n       c.assigned_department,c.category,c.status,c.requested_agent,c.created_at,c.updated_at,c.last_message_at,COALESCE(last_message.message,N''),\r\n       CASE WHEN EXISTS(SELECT 1 FROM dbo.support_conversation_mutes cm WHERE cm.conversation_id=c.id AND cm.user_id=@userId) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,\r\n       c.is_restricted,c.is_blocked\r\nFROM dbo.support_conversations c\r\nOUTER APPLY(SELECT CASE WHEN c.category LIKE 'DIRECT:%' AND c.supervisor_user_id=@userId THEN SUBSTRING(c.category,8,80) ELSE c.supervisor_user_id END contact_user_id)peer\r\nLEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=peer.contact_user_id\r\nOUTER APPLY(SELECT TOP(1)m.message FROM dbo.support_messages m WHERE m.conversation_id=c.id AND m.deleted_at IS NULL ORDER BY m.created_at DESC,m.id DESC)last_message\r\nOUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE CONVERT(nvarchar(80),ug.user_id)=peer.contact_user_id)user_groups\r\nWHERE c.deleted_at IS NULL AND (\r\n    (c.category LIKE 'DIRECT:%' AND (@admin=1 OR c.supervisor_user_id=@userId OR SUBSTRING(c.category,8,80)=@userId))\r\n    OR\r\n    (ISNULL(c.category,'') NOT LIKE 'DIRECT:%' AND (@all=1 OR (@own=1 AND c.supervisor_user_id=@userId) OR (@department<>'' AND c.assigned_department=@department)))\r\n)\r\nORDER BY CASE c.status WHEN 'WAITING_SUPPORT' THEN 1 WHEN 'IN_PROGRESS' THEN 2 WHEN 'RESOLVED' THEN 3 ELSE 4 END,c.last_message_at DESC;", connection))
			{
				command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@all", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@own", (role == "GroupAdministrator" || DepartmentAccounts.IsSupportScope(role)) ? 1 : 0);
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				List<SupportConversationRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<SupportConversationRow> rows = new List<SupportConversationRow>();
					while (await reader.ReadAsync(ct))
					{
						string text = reader.GetString(1);
						rows.Add(new SupportConversationRow(reader.GetGuid(0), text, reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), ReadBooleanValue(reader, 5) ? ("/api/account/avatar/" + text) : null, reader.GetString(6).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList(), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetString(9), ReadBooleanValue(reader, 10), reader.GetDateTime(11), reader.GetDateTime(12), reader.GetDateTime(13), reader.GetString(14), ReadBooleanValue(reader, 15), ReadBooleanValue(reader, 16), ReadBooleanValue(reader, 17)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<SupportMessageRow>> SupportMessages(Guid conversationId, string userId, string role, CancellationToken ct)
	{
		List<SupportMessageRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = new List<SupportMessageRow>();
			}
			else
			{
				List<SupportMessageRow> list2;
				await using (SqlCommand command = new SqlCommand("SELECT m.id,m.conversation_id,m.sender_user_id,m.sender_name,m.sender_role,CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,CASE WHEN m.deleted_at IS NULL THEN m.message ELSE N'Mensaje eliminado' END,m.is_bot,m.created_at,CASE WHEN v.message_id IS NULL OR m.deleted_at IS NOT NULL THEN 0 ELSE 1 END,CONVERT(bit,CASE WHEN m.deleted_at IS NULL THEN 0 ELSE 1 END) FROM dbo.support_messages m LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=m.sender_user_id LEFT JOIN dbo.support_voice_notes v ON v.message_id=m.id WHERE m.conversation_id=@id ORDER BY m.created_at,m.id;", connection))
				{
					command.Parameters.AddWithValue("@id", conversationId);
					List<SupportMessageRow> list;
					await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
					{
						List<SupportMessageRow> rows = new List<SupportMessageRow>();
						while (await reader.ReadAsync(ct))
						{
							string text = (reader.IsDBNull(2) ? null : reader.GetString(2));
							Guid guid = reader.GetGuid(0);
							rows.Add(new SupportMessageRow(guid, reader.GetGuid(1), text, reader.GetString(3), reader.GetString(4), (text != null && ReadBooleanValue(reader, 5)) ? ("/api/account/avatar/" + text) : null, reader.GetString(6), ReadBooleanValue(reader, 7), reader.GetDateTime(8), ReadBooleanValue(reader, 9) ? $"/api/support/chat/voice/{guid}" : null, ReadBooleanValue(reader, 10)));
						}
						list = rows;
					}
					list2 = list;
				}
				result = list2;
			}
		}
		return result;
	}

	public async Task<List<SupportChatNotificationRow>> SupportChatNotifications(string userId, string role, CancellationToken ct)
	{
		bool flag;
		switch (role)
		{
		case "Administrator":
		case "GroupAdministrator":
		case "Technology":
		case "GeneralServices":
		case "HumanResources":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return new List<SupportChatNotificationRow>();
		}
		List<SupportChatNotificationRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			await EnsureNotificationReceiptSchema(connection, ct);
			List<SupportChatNotificationRow> list2;
			await using (SqlCommand command = new SqlCommand("DECLARE @firstNotificationLoad bit=CASE WHEN EXISTS(SELECT 1 FROM dbo.support_notification_receipts WHERE user_id=@userId AND notification_key=N'SYSTEM:CHAT_INITIALIZED') THEN 0 ELSE 1 END;\r\nMERGE dbo.support_notification_receipts WITH(HOLDLOCK) AS target USING(SELECT @userId user_id,N'SYSTEM:CHAT_INITIALIZED' notification_key) source ON target.user_id=source.user_id AND target.notification_key=source.notification_key WHEN NOT MATCHED THEN INSERT(user_id,notification_key,read_at) VALUES(source.user_id,source.notification_key,SYSUTCDATETIME());\r\nSELECT TOP (100) m.id,c.id,c.assigned_department,c.supervisor_name,m.sender_name,m.sender_role,m.message,m.created_at,CONVERT(bit,CASE WHEN @firstNotificationLoad=1 OR receipt.read_at IS NOT NULL THEN 1 ELSE 0 END)\r\nFROM dbo.support_messages m\r\nINNER JOIN dbo.support_conversations c ON c.id=m.conversation_id\r\nLEFT JOIN dbo.support_notification_receipts receipt ON receipt.user_id=@userId AND receipt.notification_key=CONCAT(N'CHAT_MESSAGE:',CONVERT(nvarchar(36),m.id))\r\nWHERE m.is_bot=0\r\n  AND m.deleted_at IS NULL\r\n  AND ISNULL(m.sender_user_id,N'')<>@userId\r\n  AND c.deleted_at IS NULL\r\n  AND receipt.dismissed_at IS NULL\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.support_conversation_mutes cm WHERE cm.conversation_id=c.id AND cm.user_id=@userId)\r\n  AND ((c.category LIKE 'DIRECT:%' AND (@admin=1 OR c.supervisor_user_id=@userId OR SUBSTRING(c.category,8,80)=@userId))\r\n       OR (ISNULL(c.category,'') NOT LIKE 'DIRECT:%' AND (@all=1 OR (@own=1 AND c.supervisor_user_id=@userId) OR (@department<>'' AND c.assigned_department=@department))))\r\nORDER BY m.created_at DESC,m.id DESC;", connection))
			{
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@all", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@own", (role == "GroupAdministrator" || DepartmentAccounts.IsSupportScope(role)) ? 1 : 0);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				List<SupportChatNotificationRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<SupportChatNotificationRow> rows = new List<SupportChatNotificationRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new SupportChatNotificationRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetDateTime(7), reader.GetBoolean(8)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task SetNotificationState(string userId, IEnumerable<string> keys, bool dismiss, CancellationToken ct)
	{
		List<string> safeKeys = keys.Where((string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 100 && (key.StartsWith("TICKET_EVENT:", StringComparison.Ordinal) || key.StartsWith("FINDING_EVENT:", StringComparison.Ordinal) || key.StartsWith("CHAT_MESSAGE:", StringComparison.Ordinal) || key.StartsWith("WAREHOUSE_REQUEST:", StringComparison.Ordinal))).Distinct(StringComparer.Ordinal).Take(200)
			.ToList();
		if (safeKeys.Count == 0)
		{
			return;
		}
		await using SqlConnection connection = await Open(ct);
		await EnsureNotificationReceiptSchema(connection, ct);
		await using SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct));
		try
		{
			foreach (string item in safeKeys)
			{
				await using SqlCommand command = new SqlCommand("MERGE dbo.support_notification_receipts AS target\r\nUSING(SELECT @userId user_id,@key notification_key) AS source\r\nON target.user_id=source.user_id AND target.notification_key=source.notification_key\r\nWHEN MATCHED THEN UPDATE SET read_at=COALESCE(target.read_at,SYSUTCDATETIME()),dismissed_at=CASE WHEN @dismiss=1 THEN COALESCE(target.dismissed_at,SYSUTCDATETIME()) ELSE target.dismissed_at END,updated_at=SYSUTCDATETIME()\r\nWHEN NOT MATCHED THEN INSERT(user_id,notification_key,read_at,dismissed_at) VALUES(@userId,@key,SYSUTCDATETIME(),CASE WHEN @dismiss=1 THEN SYSUTCDATETIME() ELSE NULL END);", connection, transaction);
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@key", item);
				command.Parameters.AddWithValue("@dismiss", dismiss ? 1 : 0);
				await command.ExecuteNonQueryAsync(ct);
			}
			await transaction.CommitAsync(ct);
		}
		catch
		{
			await transaction.RollbackAsync(ct);
			throw;
		}
	}

	public async Task<SupportConversationRow?> CreateSupportConversation(SupportConversationCreate body, string userId, string role, string displayName, string username, CancellationToken ct)
	{
		if (role != "GroupAdministrator" && !DepartmentAccounts.IsSupportScope(role))
		{
			return null;
		}
		SupportConversationRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			string departmentName = null;
			string categoryName = null;
			SupportConversationRow supportConversationRow2;
			await using (SqlCommand catalog = new SqlCommand("SELECT d.name,(SELECT TOP(1)c.name FROM dbo.support_categories c WHERE c.department_code=d.code AND c.code=@category AND c.is_active=1)\r\nFROM dbo.support_departments d WHERE d.code=@department AND d.is_active=1;", connection))
			{
				catalog.Parameters.AddWithValue("@department", body.Department);
				catalog.Parameters.AddWithValue("@category", Db(string.IsNullOrWhiteSpace(body.Category) ? null : body.Category));
				SqlDataReader reader = await catalog.ExecuteReaderAsync(ct);
				Exception obj = null;
				int num = 0;
				SupportConversationRow supportConversationRow = default;
				try
				{
					if (!(await reader.ReadAsync(ct)))
					{
						supportConversationRow = null;
						num = 1;
					}
					else
					{
						departmentName = reader.GetString(0);
						categoryName = (reader.IsDBNull(1) ? null : reader.GetString(1));
					}
				}
				catch (Exception obj2)
				{
					obj = obj2;
				}
				if (reader != null)
				{
					await reader.DisposeAsync();
				}
				Exception obj3 = obj;
				if (obj3 != null)
				{
					if (!(obj3 is Exception source))
					{
						throw obj3;
					}
					ExceptionDispatchInfo.Capture(source).Throw();
				}
				if (num == 1)
				{
					supportConversationRow2 = supportConversationRow;
					goto IL_0518;
				}
			}
			supportConversationRow2 = null;
			Guid id = Guid.NewGuid();
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				try
				{
					await using (SqlCommand catalog = new SqlCommand("INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,requested_agent) VALUES(@id,@userId,@name,@username,@department,@category,@agent);", connection, transaction))
					{
						catalog.Parameters.AddWithValue("@id", id);
						catalog.Parameters.AddWithValue("@userId", userId);
						catalog.Parameters.AddWithValue("@name", displayName);
						catalog.Parameters.AddWithValue("@username", username);
						catalog.Parameters.AddWithValue("@department", body.Department);
						catalog.Parameters.AddWithValue("@category", Db(string.IsNullOrWhiteSpace(body.Category) ? null : body.Category));
						catalog.Parameters.AddWithValue("@agent", body.RequestedAgent);
						await catalog.ExecuteNonQueryAsync(ct);
					}
					string value = (body.RequestedAgent ? ("Te comuniqué con un personal de " + departmentName + ". Describe la avería y el equipo continuará contigo por este chat.") : ("Seleccionaste " + (categoryName ?? departmentName) + ". Describe qué está ocurriendo y enviaré la solicitud al departamento correspondiente."));
					await using (SqlCommand catalog = new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_name,sender_role,message,is_bot) VALUES(@id,N'Asistente de soporte','BOT',@message,1);", connection, transaction))
					{
						catalog.Parameters.AddWithValue("@id", id);
						catalog.Parameters.AddWithValue("@message", value);
						await catalog.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
				supportConversationRow2 = (await SupportConversations(userId, role, ct)).FirstOrDefault((SupportConversationRow item) => item.Id == id);
			}
			result = supportConversationRow2;
			goto end_IL_0140;
			IL_0518:
			result = supportConversationRow2;
			end_IL_0140:;
		}
		return result;
	}

	public async Task<SupportConversationRow?> CreateDirectSupportConversation(string targetUserId, string userId, string role, string displayName, string username, CancellationToken ct)
	{
		SupportConversationRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			string targetId = default;
			string targetName = default;
			string targetUsername = default;
			string targetRole = default;
			SupportConversationRow supportConversationRow2;
			await using (SqlCommand target = new SqlCommand("SELECT id,display_name,email,role FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@target AND is_active=1 AND role IN('ADMINISTRATOR','GROUP_ADMIN','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','DEPARTMENT');", connection))
			{
				target.Parameters.AddWithValue("@target", targetUserId.Trim());
				SqlDataReader reader = await target.ExecuteReaderAsync(ct);
				Exception obj = null;
				int num = 0;
				SupportConversationRow supportConversationRow = default;
				try
				{
					if (!(await reader.ReadAsync(ct)))
					{
						supportConversationRow = null;
						num = 1;
					}
					else
					{
						targetId = reader.GetString(0);
						targetName = reader.GetString(1);
						targetUsername = reader.GetString(2);
						targetRole = ClaimRole(reader.GetString(3));
					}
				}
				catch (Exception obj2)
				{
					obj = obj2;
				}
				if (reader != null)
				{
					await reader.DisposeAsync();
				}
				Exception obj3 = obj;
				if (obj3 != null)
				{
					if (!(obj3 is Exception source))
					{
						throw obj3;
					}
					ExceptionDispatchInfo.Capture(source).Throw();
				}
				if (num == 1)
				{
					supportConversationRow2 = supportConversationRow;
					goto IL_04c9;
				}
			}
			supportConversationRow2 = null;
			string category = "DIRECT:" + userId;
			string department = DepartmentForRole(role);
			if (string.IsNullOrWhiteSpace(department))
			{
				department = DepartmentForRole(targetRole);
			}
			if (string.IsNullOrWhiteSpace(department))
			{
				department = "TECHNOLOGY";
			}
			await using (SqlCommand target = new SqlCommand("SELECT TOP(1) id FROM dbo.support_conversations WHERE supervisor_user_id=@target AND category=@category AND deleted_at IS NULL ORDER BY updated_at DESC;", connection))
			{
				target.Parameters.AddWithValue("@target", targetId);
				target.Parameters.AddWithValue("@category", category);
				object obj4 = await target.ExecuteScalarAsync(ct);
				if (obj4 is Guid)
				{
					Guid existingId = (Guid)obj4;
					supportConversationRow2 = (await SupportConversations(userId, role, ct)).FirstOrDefault((SupportConversationRow item) => item.Id == existingId);
					goto IL_07a3;
				}
			}
			supportConversationRow2 = null;
			Guid id = Guid.NewGuid();
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				try
				{
					await using (SqlCommand target = new SqlCommand("INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,requested_agent,status) VALUES(@id,@target,@name,@username,@department,@category,1,'IN_PROGRESS');", connection, transaction))
					{
						target.Parameters.AddWithValue("@id", id);
						target.Parameters.AddWithValue("@target", targetId);
						target.Parameters.AddWithValue("@name", targetName);
						target.Parameters.AddWithValue("@username", targetUsername);
						target.Parameters.AddWithValue("@department", department);
						target.Parameters.AddWithValue("@category", category);
						await target.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
				supportConversationRow2 = (await SupportConversations(userId, role, ct)).FirstOrDefault((SupportConversationRow item) => item.Id == id);
			}
			result = supportConversationRow2;
			goto end_IL_0122;
			IL_04c9:
			result = supportConversationRow2;
			goto end_IL_0122;
			IL_07a3:
			result = supportConversationRow2;
			end_IL_0122:;
		}
		return result;
	}

	public async Task<bool> AddSupportMessage(Guid conversationId, string message, string userId, string role, string displayName, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			bool flag;
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = false;
			}
			else
			{
				await using (SqlCommand state = new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND is_blocked=0 AND (@supervisor=0 OR is_restricted=0);", connection))
				{
					state.Parameters.AddWithValue("@id", conversationId);
					state.Parameters.AddWithValue("@supervisor", (role == "GroupAdministrator") ? 1 : 0);
					if (Convert.ToInt32(await state.ExecuteScalarAsync(ct)) == 0)
					{
						flag = false;
						goto IL_03a8;
					}
				}
				await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
				{
					try
					{
						await using (SqlCommand state = new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_user_id,sender_name,sender_role,message,is_bot) VALUES(@id,@userId,@name,@role,@message,0);", connection, transaction))
						{
							state.Parameters.AddWithValue("@id", conversationId);
							state.Parameters.AddWithValue("@userId", userId);
							state.Parameters.AddWithValue("@name", displayName);
							state.Parameters.AddWithValue("@role", DepartmentAccounts.IsSupportScope(role) ? "Department" : role);
							state.Parameters.AddWithValue("@message", message);
							await state.ExecuteNonQueryAsync(ct);
						}
						string value = ((role == "GroupAdministrator") ? "WAITING_SUPPORT" : "IN_PROGRESS");
						await using (SqlCommand state = new SqlCommand("UPDATE dbo.support_conversations SET status=CASE WHEN status IN('RESOLVED','CLOSED') THEN status ELSE @status END,updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
						{
							state.Parameters.AddWithValue("@id", conversationId);
							state.Parameters.AddWithValue("@status", value);
							await state.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						flag = true;
					}
					catch
					{
						await transaction.RollbackAsync(ct);
						throw;
					}
				}
				result = flag;
			}
			goto end_IL_00ff;
			IL_03a8:
			result = flag;
			end_IL_00ff:;
		}
		return result;
	}

	public async Task<bool> AddSupportVoiceNote(Guid conversationId, byte[] audio, string contentType, string? fileName, int? durationSeconds, string userId, string role, string displayName, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			bool flag;
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = false;
			}
			else
			{
				await using (SqlCommand state = new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND is_blocked=0 AND (@supervisor=0 OR is_restricted=0);", connection))
				{
					state.Parameters.AddWithValue("@id", conversationId);
					state.Parameters.AddWithValue("@supervisor", (role == "GroupAdministrator") ? 1 : 0);
					if (Convert.ToInt32(await state.ExecuteScalarAsync(ct)) == 0)
					{
						flag = false;
						goto IL_03b8;
					}
				}
				Guid messageId = Guid.NewGuid();
				await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
				{
					try
					{
						await using (SqlCommand state = new SqlCommand("INSERT INTO dbo.support_messages(id,conversation_id,sender_user_id,sender_name,sender_role,message,is_bot) VALUES(@messageId,@id,@userId,@name,@role,N'Nota de voz',0);", connection, transaction))
						{
							state.Parameters.AddWithValue("@messageId", messageId);
							state.Parameters.AddWithValue("@id", conversationId);
							state.Parameters.AddWithValue("@userId", userId);
							state.Parameters.AddWithValue("@name", displayName);
							state.Parameters.AddWithValue("@role", DepartmentAccounts.IsSupportScope(role) ? "Department" : role);
							await state.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand state = new SqlCommand("INSERT INTO dbo.support_voice_notes(message_id,audio_data,content_type,file_name,duration_seconds) VALUES(@messageId,@audio,@contentType,@fileName,@duration);", connection, transaction))
						{
							state.Parameters.AddWithValue("@messageId", messageId);
							state.Parameters.Add("@audio", SqlDbType.VarBinary, -1).Value = audio;
							state.Parameters.AddWithValue("@contentType", contentType);
							state.Parameters.AddWithValue("@fileName", Db(fileName));
							state.Parameters.AddWithValue("@duration", Db(durationSeconds));
							await state.ExecuteNonQueryAsync(ct);
						}
						await using (SqlCommand state = new SqlCommand("UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
						{
							state.Parameters.AddWithValue("@id", conversationId);
							await state.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						flag = true;
					}
					catch
					{
						await transaction.RollbackAsync(ct);
						throw;
					}
				}
				result = flag;
			}
			goto end_IL_010f;
			IL_03b8:
			result = flag;
			end_IL_010f:;
		}
		return result;
	}

	public async Task<bool> DeleteSupportMessage(Guid messageId, string userId, string role, string actorName, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			Guid conversationId;
			bool flag;
			await using (SqlCommand find = new SqlCommand("SELECT conversation_id FROM dbo.support_messages WHERE id=@id AND deleted_at IS NULL AND is_bot=0 AND (sender_user_id=@userId OR @admin=1);", connection))
			{
				find.Parameters.AddWithValue("@id", messageId);
				find.Parameters.AddWithValue("@userId", userId);
				find.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
				if (await find.ExecuteScalarAsync(ct) is Guid guid)
				{
					conversationId = guid;
					goto IL_0367;
				}
				flag = false;
			}
			result = flag;
			goto end_IL_0107;
			IL_0367:
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = false;
			}
			else
			{
				await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
				{
					try
					{
						bool flag2;
						await using (SqlCommand find = new SqlCommand("UPDATE dbo.support_messages SET deleted_at=SYSUTCDATETIME(),deleted_by_user_id=@userId,deleted_by_name=@actor WHERE id=@id AND deleted_at IS NULL;", connection, transaction))
						{
							find.Parameters.AddWithValue("@id", messageId);
							find.Parameters.AddWithValue("@userId", userId);
							find.Parameters.AddWithValue("@actor", actorName);
							if (await find.ExecuteNonQueryAsync(ct) != 1)
							{
								await transaction.RollbackAsync(ct);
								flag2 = false;
								goto IL_06ee;
							}
						}
						await using (SqlCommand find = new SqlCommand("UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME(),last_message_at=COALESCE((SELECT MAX(created_at) FROM dbo.support_messages WHERE conversation_id=@conversationId AND deleted_at IS NULL),created_at) WHERE id=@conversationId;", connection, transaction))
						{
							find.Parameters.AddWithValue("@conversationId", conversationId);
							await find.ExecuteNonQueryAsync(ct);
						}
						await transaction.CommitAsync(ct);
						flag = true;
						goto end_IL_04bf;
						IL_06ee:
						flag = flag2;
						end_IL_04bf:;
					}
					catch
					{
						await transaction.RollbackAsync(ct);
						throw;
					}
				}
				result = flag;
			}
			end_IL_0107:;
		}
		return result;
	}

	public async Task<(byte[] Audio, string ContentType, string FileName)?> SupportVoiceNote(Guid messageId, string userId, string role, CancellationToken ct)
	{
		(byte[] Audio, string ContentType, string FileName)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			Guid conversationId;
			(byte[] Audio, string ContentType, string FileName)? tuple;
			await using (SqlCommand find = new SqlCommand("SELECT m.conversation_id FROM dbo.support_messages m INNER JOIN dbo.support_voice_notes v ON v.message_id=m.id WHERE m.id=@id AND m.deleted_at IS NULL;", connection))
			{
				find.Parameters.AddWithValue("@id", messageId);
				if (await find.ExecuteScalarAsync(ct) is Guid guid)
				{
					conversationId = guid;
					goto IL_02ff;
				}
				tuple = null;
			}
			result = tuple;
			goto end_IL_00e7;
			IL_02ff:
			tuple = null;
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = null;
			}
			else
			{
				await using (SqlCommand command = new SqlCommand("SELECT audio_data,content_type,COALESCE(file_name,N'nota-de-voz.webm') FROM dbo.support_voice_notes WHERE message_id=@id;", connection))
				{
					command.Parameters.AddWithValue("@id", messageId);
					(byte[] Audio, string ContentType, string FileName)? tuple2;
					await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
					{
						tuple2 = ((await reader.ReadAsync(ct)) ? new (byte[], string, string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1), reader.GetString(2))) : (((byte[], string, string)?)null));
					}
					tuple = tuple2;
				}
				result = tuple;
			}
			end_IL_00e7:;
		}
		return result;
	}

	public async Task<bool> CanDeleteDirectConversation(Guid conversationId, string userId, string role, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			bool flag;
			await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND category LIKE 'DIRECT:%' AND (@admin=1 OR supervisor_user_id=@userId OR SUBSTRING(category,8,80)=@userId);", connection))
			{
				command.Parameters.AddWithValue("@id", conversationId);
				command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@userId", userId);
				flag = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
			}
			result = flag;
		}
		return result;
	}

	private static async Task<bool> CanManageDepartmentConversation(SqlConnection connection, Guid conversationId, string role, CancellationToken ct)
	{
		if (!DepartmentAccounts.IsSupportScope(role))
		{
			return true;
		}
		bool result;
		await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND (assigned_department=@department OR category LIKE 'DIRECT:%');", connection))
		{
			command.Parameters.AddWithValue("@id", conversationId);
			command.Parameters.AddWithValue("@department", DepartmentForRole(role));
			result = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
		}
		return result;
	}

	public async Task<bool> UpdateSupportConversationStatus(Guid conversationId, string status, string userId, string role, CancellationToken ct)
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
		if (!flag && !DepartmentAccounts.IsSupportScope(role))
		{
			return false;
		}
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = false;
			}
			else if (!(await CanManageDepartmentConversation(connection, conversationId, role, ct)))
			{
				result = false;
			}
			else
			{
				bool flag2;
				await using (SqlCommand command = new SqlCommand("UPDATE dbo.support_conversations SET status=@status,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection))
				{
					command.Parameters.AddWithValue("@id", conversationId);
					command.Parameters.AddWithValue("@status", status);
					flag2 = await command.ExecuteNonQueryAsync(ct) == 1;
				}
				result = flag2;
			}
		}
		return result;
	}

	public async Task<bool> UpdateSupportConversationControl(Guid conversationId, string action, string userId, string role, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = false;
			}
			else
			{
				string text = action;
				bool flag = ((text == "MUTE" || text == "UNMUTE") ? true : false);
				bool flag2 = !flag;
				bool flag3 = flag2;
				if (flag3)
				{
					flag3 = !(await CanManageDepartmentConversation(connection, conversationId, role, ct));
				}
				if (flag3)
				{
					result = false;
				}
				else
				{
					string sql = action switch
					{
						"MUTE" => "IF NOT EXISTS(SELECT 1 FROM dbo.support_conversation_mutes WHERE conversation_id=@id AND user_id=@userId) INSERT INTO dbo.support_conversation_mutes(conversation_id,user_id) VALUES(@id,@userId); UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						"UNMUTE" => "DELETE FROM dbo.support_conversation_mutes WHERE conversation_id=@id AND user_id=@userId; UPDATE dbo.support_conversations SET updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						"RESTRICT" => "UPDATE dbo.support_conversations SET is_restricted=1,updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						"UNRESTRICT" => "UPDATE dbo.support_conversations SET is_restricted=0,updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						"BLOCK" => "UPDATE dbo.support_conversations SET is_blocked=1,status='CLOSED',updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						"UNBLOCK" => "UPDATE dbo.support_conversations SET is_blocked=0,status='WAITING_SUPPORT',updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						"DELETE" => "UPDATE dbo.support_conversations SET deleted_at=SYSUTCDATETIME(),updated_at=SYSUTCDATETIME() WHERE id=@id AND deleted_at IS NULL;", 
						_ => null, 
					};
					if (sql == null)
					{
						result = false;
					}
					else
					{
						bool flag5;
						await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
						{
							try
							{
								bool flag4;
								await using (SqlCommand update = new SqlCommand(sql, connection, transaction))
								{
									update.Parameters.AddWithValue("@id", conversationId);
									update.Parameters.AddWithValue("@userId", userId);
									if (await update.ExecuteNonQueryAsync(ct) < 1)
									{
										await transaction.RollbackAsync(ct);
										flag4 = false;
										goto IL_06c0;
									}
								}
								await using (SqlCommand update = new SqlCommand("INSERT INTO dbo.support_conversation_controls(conversation_id,action,actor_user_id,actor_role) VALUES(@id,@action,@userId,@role);", connection, transaction))
								{
									update.Parameters.AddWithValue("@id", conversationId);
									update.Parameters.AddWithValue("@action", action);
									update.Parameters.AddWithValue("@userId", userId);
									update.Parameters.AddWithValue("@role", DepartmentAccounts.IsSupportScope(role) ? "Department" : role);
									await update.ExecuteNonQueryAsync(ct);
								}
								await transaction.CommitAsync(ct);
								flag5 = true;
								goto end_IL_04ac;
								IL_06c0:
								flag5 = flag4;
								end_IL_04ac:;
							}
							catch
							{
								await transaction.RollbackAsync(ct);
								throw;
							}
						}
						result = flag5;
					}
				}
			}
		}
		return result;
	}

	public async Task<string?> SupportPresenceContext(Guid conversationId, string userId, string role, CancellationToken ct)
	{
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureSupportChatSchema(connection, ct);
			if (!(await CanAccessSupportConversation(connection, conversationId, userId, role, ct)))
			{
				result = null;
			}
			else
			{
				string text;
				await using (SqlCommand command = new SqlCommand("SELECT assigned_department FROM dbo.support_conversations WHERE id=@id;", connection))
				{
					command.Parameters.AddWithValue("@id", conversationId);
					text = (string)(await command.ExecuteScalarAsync(ct));
				}
				result = text;
			}
		}
		return result;
	}

	public async Task<AccountContactProfileRow?> ContactProfile(Guid id, CancellationToken ct)
	{
		AccountContactProfileRow result;
		await using (SqlConnection connection = await Open(ct))
		{
			AccountContactProfileRow accountContactProfileRow2;
			await using (SqlCommand command = new SqlCommand("SELECT u.id,u.email,u.display_name,u.role,u.contact,CASE WHEN u.avatar_data IS NULL THEN 0 ELSE 1 END,COALESCE(g.names,N'') FROM dbo.admin_users u OUTER APPLY(SELECT STRING_AGG(ug.group_name,N'|') names FROM dbo.admin_user_groups ug WHERE ug.user_id=u.id)g WHERE u.id=@id AND u.is_active=1;", connection))
			{
				command.Parameters.AddWithValue("@id", id.ToString());
				AccountContactProfileRow accountContactProfileRow;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					accountContactProfileRow = ((await reader.ReadAsync(ct)) ? new AccountContactProfileRow(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), ReadBooleanValue(reader, 5) ? $"/api/account/avatar/{id}" : null, reader.GetString(6).Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()) : null);
				}
				accountContactProfileRow2 = accountContactProfileRow;
			}
			result = accountContactProfileRow2;
		}
		return result;
	}

	public async Task UpdateOwnContact(Guid id, string? contact, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("UPDATE dbo.admin_users SET contact=@contact,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection);
		command.Parameters.AddWithValue("@id", id);
		command.Parameters.AddWithValue("@contact", Db(string.IsNullOrWhiteSpace(contact) ? null : contact.Trim()));
		await command.ExecuteNonQueryAsync(ct);
	}

	private static async Task<bool> CanAccessSupportConversation(SqlConnection connection, Guid conversationId, string userId, string role, CancellationToken ct)
	{
		bool result;
		await using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.support_conversations WHERE id=@id AND deleted_at IS NULL AND ((category LIKE 'DIRECT:%' AND (@admin=1 OR supervisor_user_id=@userId OR SUBSTRING(category,8,80)=@userId)) OR (ISNULL(category,'') NOT LIKE 'DIRECT:%' AND (@all=1 OR (@own=1 AND supervisor_user_id=@userId) OR (@department<>'' AND assigned_department=@department))));", connection))
		{
			command.Parameters.AddWithValue("@id", conversationId);
			command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
			command.Parameters.AddWithValue("@all", (role == "Administrator") ? 1 : 0);
			command.Parameters.AddWithValue("@own", (role == "GroupAdministrator" || DepartmentAccounts.IsSupportScope(role)) ? 1 : 0);
			command.Parameters.AddWithValue("@userId", userId);
			command.Parameters.AddWithValue("@department", DepartmentForRole(role));
			result = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
		}
		return result;
	}

	public async Task<List<AutomaticFindingRow>> AutomaticFindings(string userId, string role, string? supportTeam, bool canAssignTickets, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		List<AutomaticFindingRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			List<AutomaticFindingRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT af.id,p.id,a.codigo,a.terminal,a.grupo,p.submitted_at,af.finding_key,af.title,af.detail,af.assigned_department,af.priority,af.status,\r\n       af.diagnosis,af.resolution,af.verification,af.recommendations,af.resolved_at,af.resolution_minutes,af.resolved_by_name,\r\n       (SELECT e.id AS [id],e.file_name AS [fileName],e.content_type AS [contentType],e.captured_at AS [capturedAt],e.latitude AS [latitude],e.longitude AS [longitude],e.accuracy_meters AS [accuracyMeters]\r\n        FROM dbo.finding_resolution_evidence e WHERE e.finding_id=af.id ORDER BY e.captured_at,e.id FOR JSON PATH) AS evidence_json,\r\n       af.assigned_technician_id,u.display_name,p.direccion,p.sector,p.municipio,p.provincia,p.latitude,p.longitude,\r\n       COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.displayName'),''),submitter.display_name,auditAuthor.display_name,groupAuthor.display_name,NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),auditAuthor.actor_email,groupAuthor.email,N'Autor no identificado'),\r\n       COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.userId'),''),CONVERT(nvarchar(80),submitter.id),CONVERT(nvarchar(80),auditAuthor.id),CONVERT(nvarchar(80),groupAuthor.id),N''),\r\n       COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),submitter.email,auditAuthor.actor_email,groupAuthor.email,N'')\r\n       ,p.observations,JSON_VALUE(p.structural_answers,'$.damageFound')\r\nFROM dbo.automatic_findings af\r\nINNER JOIN dbo.agency_profiles p ON p.id=af.profile_id\r\nINNER JOIN dbo.agencies a ON a.id=af.agency_id\r\nLEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id\r\nOUTER APPLY (SELECT TOP(1) source.id,source.display_name,source.email FROM dbo.admin_users source\r\n             WHERE CONVERT(nvarchar(80),source.id)=JSON_VALUE(p.submitted_by_context,'$.userId')\r\n                OR LOWER(source.email)=LOWER(JSON_VALUE(p.submitted_by_context,'$.login'))\r\n             ORDER BY CASE WHEN CONVERT(nvarchar(80),source.id)=JSON_VALUE(p.submitted_by_context,'$.userId') THEN 0 ELSE 1 END) submitter\r\nOUTER APPLY (SELECT TOP(1) source.id,source.display_name,l.actor_email\r\n             FROM dbo.audit_log l LEFT JOIN dbo.admin_users source ON LOWER(source.email)=LOWER(l.actor_email)\r\n             WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED'\r\n             ORDER BY l.created_at) auditAuthor\r\nOUTER APPLY (SELECT TOP(1) source.id,source.display_name,source.email\r\n             FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users source ON source.id=ug.user_id\r\n             WHERE ug.group_name=a.grupo AND source.role='GROUP_ADMIN' AND source.is_active=1\r\n             ORDER BY source.display_name,source.email) groupAuthor\r\nWHERE (@department='' OR af.assigned_department=@department)\r\n  AND (@technicianOnly=0 OR af.assigned_technician_id=@userId)\r\n  AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))\r\nORDER BY af.title,a.grupo,a.terminal;", connection))
			{
				AddScope(command, userId, role);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				SqlParameterCollection parameters = command.Parameters;
				bool flag = role == "GroupAdministrator";
				bool flag2 = flag;
				if (!flag2)
				{
					bool flag3;
					switch (role)
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
					flag2 = (flag3 || DepartmentAccounts.IsSupportScope(role)) && !canAssignTickets;
				}
				parameters.AddWithValue("@technicianOnly", flag2 ? 1 : 0);
				if (role == "GroupAdministrator")
				{
					command.Parameters["@scoped"].Value = 0;
				}
				List<AutomaticFindingRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<AutomaticFindingRow> rows = new List<AutomaticFindingRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new AutomaticFindingRow(reader.GetGuid(0), Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetDateTime(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetString(14), reader.IsDBNull(15) ? null : reader.GetString(15), reader.IsDBNull(16) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(16)), reader.IsDBNull(17) ? ((int?)null) : new int?(reader.GetInt32(17)), reader.IsDBNull(18) ? null : reader.GetString(18), JsonSerializer.Deserialize<List<FindingEvidenceRow>>(reader.IsDBNull(19) ? "[]" : reader.GetString(19), StorageJson) ?? new List<FindingEvidenceRow>(), reader.IsDBNull(20) ? null : reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21), reader.IsDBNull(22) ? null : reader.GetString(22), reader.IsDBNull(23) ? null : reader.GetString(23), reader.IsDBNull(24) ? null : reader.GetString(24), reader.IsDBNull(25) ? null : reader.GetString(25), reader.IsDBNull(26) ? ((double?)null) : new double?(reader.GetDouble(26)), reader.IsDBNull(27) ? ((double?)null) : new double?(reader.GetDouble(27)), reader.GetString(28), reader.GetString(29), reader.GetString(30), reader.IsDBNull(31) ? null : reader.GetString(31), reader.IsDBNull(32) ? null : reader.GetString(32)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<bool> AssignAutomaticFinding(Guid id, string? technicianId, string userId, string role, bool canAssignTickets, string actorName, CancellationToken ct)
	{
		if (!canAssignTickets)
		{
			return false;
		}
		technicianId = (string.IsNullOrWhiteSpace(technicianId) ? null : technicianId.Trim());
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			bool flag3;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					string previous = null;
					string title = "";
					string department = "";
					bool flag2;
					await using (SqlCommand command = new SqlCommand("UPDATE af SET assigned_technician_id=@technician,status=CASE WHEN @technician IS NOT NULL AND af.status='OPEN' THEN 'IN_PROGRESS' ELSE af.status END,updated_at=SYSUTCDATETIME() OUTPUT deleted.assigned_technician_id,inserted.title,inserted.assigned_department FROM dbo.automatic_findings af INNER JOIN dbo.agencies a ON a.id=af.agency_id WHERE af.id=@id AND af.status IN('OPEN','IN_PROGRESS') AND (@department='' OR af.assigned_department=@department) AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo)) AND (@technician IS NULL OR EXISTS(SELECT 1 FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=@technician AND u.is_active=1 AND (u.role=CASE af.assigned_department WHEN 'TECHNOLOGY' THEN 'TECHNOLOGY' WHEN 'GENERAL_SERVICES' THEN 'GENERAL_SERVICES' WHEN 'HUMAN_RESOURCES' THEN 'HUMAN_RESOURCES' ELSE '' END OR (u.role='DEPARTMENT' AND u.support_team=af.assigned_department) OR u.role='GROUP_ADMIN')));", connection, transaction))
					{
						command.Parameters.AddWithValue("@id", id);
						command.Parameters.AddWithValue("@technician", Db(technicianId));
						command.Parameters.AddWithValue("@department", DepartmentForRole(role));
						AddScope(command, userId, role);
						bool flag;
						await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
						{
							if (!(await reader.ReadAsync(ct)))
							{
								await transaction.RollbackAsync(ct);
								flag = false;
								goto IL_05ac;
							}
							previous = (reader.IsDBNull(0) ? null : reader.GetString(0));
							title = reader.GetString(1);
							department = reader.GetString(2);
						}
						goto end_IL_02c2;
						IL_05ac:
						flag2 = flag;
						goto IL_0683;
						end_IL_02c2:;
					}
					if (!string.IsNullOrWhiteSpace(technicianId) && !string.Equals(previous, technicianId, StringComparison.OrdinalIgnoreCase))
					{
						await using SqlCommand command = new SqlCommand("INSERT INTO dbo.automatic_finding_notifications(finding_id,recipient_user_id,event_type,message,actor_name) VALUES(@id,@recipient,@event,@message,@actor);", connection, transaction);
						command.Parameters.AddWithValue("@id", id);
						command.Parameters.AddWithValue("@recipient", technicianId);
						command.Parameters.AddWithValue("@event", (previous == null) ? "ASSIGNED" : "REASSIGNED");
						command.Parameters.AddWithValue("@message", $"Se te asignó el hallazgo automático «{title}» de {department}.");
						command.Parameters.AddWithValue("@actor", actorName);
						await command.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
					flag3 = true;
					goto end_IL_0262;
					IL_0683:
					flag3 = flag2;
					end_IL_0262:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = flag3;
		}
		return result;
	}

	public async Task<bool> ResolveAutomaticFinding(Guid id, string diagnosis, string resolution, string verification, string recommendations, string userId, string role, string? supportTeam, bool canAssignTickets, string resolvedByName, string resolvedByUsername, List<FindingEvidenceUpload> evidence, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			bool flag4;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				bool flag;
				await using (SqlCommand gpsRequired = new SqlCommand("SELECT COUNT(*) FROM dbo.automatic_findings af INNER JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id WHERE af.id=@id AND ((u.role='TECHNOLOGY' AND ISNULL(u.support_team,'TECHNICIANS') NOT IN('CALL_CENTER','TECHNICAL_FAILURE')) OR (u.role IN('GENERAL_SERVICES','HUMAN_RESOURCES') AND (u.support_team='WORKSHOP' OR JSON_VALUE(u.permissions_json,'$.canAssignTickets')='false')));", connection, transaction))
				{
					gpsRequired.Parameters.AddWithValue("@id", id);
					if (Convert.ToInt32(await gpsRequired.ExecuteScalarAsync(ct)) > 0 && evidence.Any((FindingEvidenceUpload findingEvidenceUpload) => !findingEvidenceUpload.Latitude.HasValue || !findingEvidenceUpload.Longitude.HasValue || !findingEvidenceUpload.AccuracyMeters.HasValue || !findingEvidenceUpload.LocationCapturedAt.HasValue))
					{
						await transaction.RollbackAsync(ct);
						flag = false;
						goto IL_047f;
					}
				}
				await using (SqlCommand command = new SqlCommand("UPDATE af SET status='RESOLVED',diagnosis=@diagnosis,resolution=@resolution,verification=@verification,recommendations=@recommendations,\r\n  resolved_at=COALESCE(af.resolved_at,SYSUTCDATETIME()),\r\n  resolution_minutes=COALESCE(af.resolution_minutes,DATEDIFF(MINUTE,af.created_at,SYSUTCDATETIME())),\r\n  resolved_by_name=@resolvedByName,resolved_by_username=@resolvedByUsername,\r\n  updated_at=SYSUTCDATETIME(),closed_at=COALESCE(af.closed_at,SYSUTCDATETIME())\r\nFROM dbo.automatic_findings af INNER JOIN dbo.agencies a ON a.id=af.agency_id\r\nWHERE af.id=@id AND (@department='' OR af.assigned_department=@department) AND (@technicianOnly=0 OR af.assigned_technician_id=@userId) AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo)) AND af.status NOT IN ('RESOLVED','CLOSED','CANCELLED');", connection, transaction))
				{
					command.Parameters.AddWithValue("@id", id);
					command.Parameters.AddWithValue("@diagnosis", diagnosis);
					command.Parameters.AddWithValue("@resolution", resolution);
					command.Parameters.AddWithValue("@verification", verification);
					command.Parameters.AddWithValue("@recommendations", Db(string.IsNullOrWhiteSpace(recommendations) ? null : recommendations));
					command.Parameters.AddWithValue("@department", DepartmentForRole(role));
					command.Parameters.AddWithValue("@userId", userId);
					SqlParameterCollection parameters = command.Parameters;
					bool flag2;
					switch (role)
					{
					case "Technology":
					case "GeneralServices":
					case "HumanResources":
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					parameters.AddWithValue("@technicianOnly", (((flag2 || DepartmentAccounts.IsSupportScope(role)) && !canAssignTickets) || role == "GroupAdministrator") ? 1 : 0);
					command.Parameters.AddWithValue("@scoped", 0);
					command.Parameters.AddWithValue("@resolvedByName", resolvedByName);
					command.Parameters.AddWithValue("@resolvedByUsername", resolvedByUsername);
					if (await command.ExecuteNonQueryAsync(ct) != 1)
					{
						await transaction.RollbackAsync(ct);
						flag = false;
					}
					else
					{
						foreach (FindingEvidenceUpload photo in evidence)
						{
							await using SqlCommand gpsRequired = new SqlCommand("INSERT INTO dbo.finding_resolution_evidence(id,finding_id,file_name,content_type,photo_data,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at) VALUES(NEWID(),@finding,@fileName,@contentType,@data,@actor,@latitude,@longitude,@accuracy,@locationCapturedAt);", connection, transaction);
							gpsRequired.Parameters.AddWithValue("@finding", id);
							gpsRequired.Parameters.AddWithValue("@fileName", photo.FileName);
							gpsRequired.Parameters.AddWithValue("@contentType", photo.ContentType);
							SqlParameterCollection parameters2 = gpsRequired.Parameters;
							parameters2.AddWithValue("@data", await StorageFiles.Store(photo.Data, "finding-evidence", ct));
							gpsRequired.Parameters.AddWithValue("@actor", resolvedByName);
							gpsRequired.Parameters.AddWithValue("@latitude", Db(photo.Latitude));
							gpsRequired.Parameters.AddWithValue("@longitude", Db(photo.Longitude));
							gpsRequired.Parameters.AddWithValue("@accuracy", Db(photo.AccuracyMeters));
							gpsRequired.Parameters.AddWithValue("@locationCapturedAt", Db(photo.LocationCapturedAt));
							await gpsRequired.ExecuteNonQueryAsync(ct);
						}
						bool flag3;
						await using (SqlCommand answersCommand = new SqlCommand("UPDATE p SET structural_answers=CASE af.finding_key\r\n  WHEN 'NO_INVERTER' THEN JSON_MODIFY(p.structural_answers,'$.inverterPresent','yes')\r\n  WHEN 'NO_BATTERY' THEN JSON_MODIFY(p.structural_answers,'$.batteryPresent','yes')\r\n  WHEN 'PRINTER_NO_MAINTENANCE' THEN JSON_MODIFY(p.structural_answers,'$.printerMaintained','yes')\r\n  WHEN 'NOT_PAINTED' THEN JSON_MODIFY(p.structural_answers,'$.painted','yes')\r\n  WHEN 'NO_RAZA_STICKER' THEN JSON_MODIFY(p.structural_answers,'$.razaSticker','yes')\r\n  WHEN 'NO_REAL_STICKER' THEN JSON_MODIFY(p.structural_answers,'$.realSticker','yes')\r\n  WHEN 'LOTEKA_NOT_REMOVED' THEN JSON_MODIFY(p.structural_answers,'$.lotekaRemoved','yes')\r\n  ELSE p.structural_answers END\r\nFROM dbo.agency_profiles p INNER JOIN dbo.automatic_findings af ON af.profile_id=p.id\r\nWHERE af.id=@id;\r\n\r\nUPDATE p SET structural_answers=JSON_MODIFY(p.structural_answers,'$.damageFound','no')\r\nFROM dbo.agency_profiles p INNER JOIN dbo.automatic_findings af ON af.profile_id=p.id\r\nWHERE af.id=@id AND af.finding_key LIKE 'DAMAGE[_]%'\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.automatic_findings pending\r\n    WHERE pending.profile_id=af.profile_id AND pending.finding_key LIKE 'DAMAGE[_]%'\r\n      AND pending.status NOT IN ('RESOLVED','CLOSED'));\r\n\r\nINSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data)\r\nSELECT 'automatic_finding',CONVERT(nvarchar(80),af.id),'FINDING_RESOLVED',\r\n  CONCAT(N'{\"findingKey\":\"',af.finding_key,N'\",\"resolutionMinutes\":',COALESCE(CONVERT(varchar(20),af.resolution_minutes),'0'),N',\"evidenceCount\":',@evidenceCount,N'}')\r\nFROM dbo.automatic_findings af WHERE af.id=@id;", connection, transaction))
						{
							answersCommand.Parameters.AddWithValue("@id", id);
							answersCommand.Parameters.AddWithValue("@evidenceCount", evidence.Count);
							await answersCommand.ExecuteNonQueryAsync(ct);
							await transaction.CommitAsync(ct);
							flag3 = true;
						}
						flag = flag3;
					}
				}
				flag4 = flag;
				goto end_IL_0257;
				IL_047f:
				flag4 = flag;
				end_IL_0257:;
			}
			result = flag4;
		}
		return result;
	}

	public async Task<TicketActionResult> CancelAutomaticFinding(Guid id, string reason, string userId, string role, string? supportTeam, bool canAssignTickets, string actorName, string actorLogin, List<FindingEvidenceUpload> evidence, bool withEvidence, bool withoutEvidence, bool withReason, bool withoutReason, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		if (evidence.Count > 6 || reason.Length > 2000 || (reason.Length > 0 && reason.Length < 10))
		{
			return new TicketActionResult(Ok: false, "VALIDATION", "Revisa el motivo y las imágenes de la anulación.");
		}
		if ((evidence.Count > 0 && !withEvidence) || (evidence.Count == 0 && !withoutEvidence) || (reason.Length > 0 && !withReason) || (reason.Length == 0 && !withoutReason))
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Tu perfil no permite esta combinación de motivo y evidencia.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			TicketActionResult ticketActionResult2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					TicketActionResult ticketActionResult;
					await using (SqlCommand command = new SqlCommand("UPDATE af SET status='CANCELLED',resolution=@reason,resolved_at=SYSUTCDATETIME(),resolution_minutes=DATEDIFF(MINUTE,af.created_at,SYSUTCDATETIME()),resolved_by_name=@actor,resolved_by_username=@login,updated_at=SYSUTCDATETIME(),closed_at=SYSUTCDATETIME() FROM dbo.automatic_findings af INNER JOIN dbo.agencies a ON a.id=af.agency_id WHERE af.id=@id AND af.status IN('OPEN','IN_PROGRESS') AND (@department='' OR af.assigned_department=@department) AND (@technicianOnly=0 OR af.assigned_technician_id=@userId) AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo));", connection, transaction))
					{
						command.Parameters.AddWithValue("@id", id);
						command.Parameters.AddWithValue("@reason", reason);
						command.Parameters.AddWithValue("@actor", actorName);
						command.Parameters.AddWithValue("@login", actorLogin);
						command.Parameters.AddWithValue("@department", DepartmentForRole(role));
						SqlParameterCollection parameters = command.Parameters;
						bool flag;
						switch (role)
						{
						case "Technology":
						case "GeneralServices":
						case "HumanResources":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						parameters.AddWithValue("@technicianOnly", (((flag || DepartmentAccounts.IsSupportScope(role)) && !canAssignTickets) || role == "GroupAdministrator") ? 1 : 0);
						AddScope(command, userId, role);
						if (await command.ExecuteNonQueryAsync(ct) != 1)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "El hallazgo ya fue finalizado o no pertenece a tu ámbito.");
							goto IL_062e;
						}
					}
					foreach (FindingEvidenceUpload photo in evidence)
					{
						await using SqlCommand command = new SqlCommand("INSERT INTO dbo.finding_resolution_evidence(id,finding_id,file_name,content_type,photo_data,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at) VALUES(NEWID(),@id,@name,@mime,@data,@actor,@latitude,@longitude,@accuracy,@captured);", connection, transaction);
						command.Parameters.AddWithValue("@id", id);
						command.Parameters.AddWithValue("@name", photo.FileName);
						command.Parameters.AddWithValue("@mime", photo.ContentType);
						SqlParameterCollection parameters2 = command.Parameters;
						parameters2.AddWithValue("@data", await StorageFiles.Store(photo.Data, "finding-evidence", ct));
						command.Parameters.AddWithValue("@actor", actorName);
						command.Parameters.AddWithValue("@latitude", Db(photo.Latitude));
						command.Parameters.AddWithValue("@longitude", Db(photo.Longitude));
						command.Parameters.AddWithValue("@accuracy", Db(photo.AccuracyMeters));
						command.Parameters.AddWithValue("@captured", Db(photo.LocationCapturedAt));
						await command.ExecuteNonQueryAsync(ct);
					}
					await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data) VALUES('automatic_finding',CONVERT(nvarchar(80),@id),'FINDING_CANCELLED',@data);", connection, transaction))
					{
						command.Parameters.AddWithValue("@id", id);
						command.Parameters.AddWithValue("@data", JsonSerializer.Serialize(new
						{
							reason = reason,
							actorName = actorName,
							actorLogin = actorLogin,
							evidenceCount = evidence.Count
						}));
						await command.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
					ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
					goto end_IL_0311;
					IL_062e:
					ticketActionResult2 = ticketActionResult;
					end_IL_0311:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult2;
		}
		return result;
	}

	public async Task<(byte[] Data, string Mime)?> FindingEvidencePhoto(Guid evidenceId, string role, CancellationToken ct)
	{
		(byte[] Data, string Mime)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureFindingEvidenceSchema(connection, ct);
			(byte[] Data, string Mime)? tuple3;
			await using (SqlCommand command = new SqlCommand("SELECT e.photo_data,e.content_type FROM dbo.finding_resolution_evidence e INNER JOIN dbo.automatic_findings af ON af.id=e.finding_id WHERE e.id=@id AND (@department='' OR af.assigned_department=@department);", connection))
			{
				command.Parameters.AddWithValue("@id", evidenceId);
				command.Parameters.AddWithValue("@department", DepartmentForRole(role));
				(byte[] Data, string Mime)? tuple2;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct))
				{
					(byte[], string)? tuple = ((!(await reader.ReadAsync(ct))) ? (((byte[], string)?)null) : new (byte[], string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1))));
					tuple2 = tuple;
				}
				tuple3 = tuple2;
			}
			result = tuple3;
		}
		return result;
	}

	public async Task<List<SupportDepartmentMetricRow>> SupportDepartmentMetrics(string userId, string role, bool canAssignTickets, bool includeFindings, string? supportTeam, string? userEmail, CancellationToken ct, bool areaManager = false)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		await using SqlConnection connection = await Open(ct);
		await EnsureTicketWorkflowSchema(connection, ct);
		await EnsureFindingEvidenceSchema(connection, ct);
		await using SqlCommand command = new SqlCommand("""
DECLARE @visible TABLE(id uniqueidentifier NOT NULL PRIMARY KEY,is_current bit NOT NULL);
INSERT INTO @visible(id,is_current)
SELECT t.id,assignment.is_current
FROM dbo.support_tickets t
INNER JOIN dbo.agencies a ON a.id=t.agency_id
OUTER APPLY (SELECT CAST(CASE WHEN
    t.assigned_technician_id=@userId
    OR LTRIM(RTRIM(t.assigned_technician_id))=LTRIM(RTRIM(@userId))
    OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LTRIM(RTRIM(@userEmail)))
    OR LOWER(LTRIM(RTRIM(t.assigned_technician_id)))=LOWER(LEFT(LTRIM(RTRIM(@userEmail)),CHARINDEX('@',LTRIM(RTRIM(@userEmail))+'@')-1))
    OR EXISTS(SELECT 1 FROM dbo.admin_users assigned_user WHERE CONVERT(nvarchar(80),assigned_user.id)=LTRIM(RTRIM(t.assigned_technician_id)) AND LOWER(LTRIM(RTRIM(assigned_user.email)))=LOWER(LTRIM(RTRIM(@userEmail))))
    THEN 1 ELSE 0 END AS bit) AS is_current) assignment
WHERE
  @administrator=1
  OR (@administrativeRequester=1 AND t.created_by_user_id=@userId)
  OR (@supportTeam='WAREHOUSE' AND t.created_by_user_id=@userId)
  OR (@areaManager=1 AND @supportTeam='WAREHOUSE' AND EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND (creator.role='WAREHOUSE' OR creator.support_team='WAREHOUSE')))
  OR (@groupSupervisor=1 AND (assignment.is_current=1 OR t.created_by_user_id=@userId))
  OR (@supportDepartment<>'' AND (
        (@supportTeam='CALL_CENTER' AND (
            (t.assigned_department='TECHNOLOGY' AND t.assigned_team='CALL_CENTER')
            OR (t.ticket_type='INTERNAL' AND EXISTS(SELECT 1 FROM dbo.support_ticket_history cc
                WHERE cc.ticket_id=t.id AND cc.from_team='CALL_CENTER' AND (
                    (cc.action='ESCALATED' AND cc.to_team='TECHNICAL_FAILURE')
                    OR (cc.action='ESCALATED_TO_SUPERVISOR' AND cc.to_team='TECHNICIANS'))))
        ))
        OR (@supportTeam='TECHNICIANS' AND assignment.is_current=1 AND t.assigned_department=@supportDepartment)
        OR (@supportTeam='TECHNICAL_FAILURE' AND (
            (assignment.is_current=1 AND t.assigned_department=@supportDepartment)
            OR (@canAssign=1 AND t.assigned_department='TECHNOLOGY' AND t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS'))
        ))
        OR (@supportTeam NOT IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS') AND (
            (assignment.is_current=1 AND t.assigned_department=@supportDepartment)
            OR (@canAssign=1 AND (
                (t.assigned_department=@supportDepartment AND (@supportTeam='' OR t.assigned_team=@supportTeam))
                OR EXISTS(SELECT 1 FROM dbo.support_ticket_shares s WHERE s.ticket_id=t.id AND s.department_code=@supportDepartment)
            ))
        ))
     ))) OPTION(RECOMPILE);

SELECT t.assigned_department,
       SUM(CASE WHEN t.status IN('OPEN','PENDING','IN_PROGRESS') THEN 1 ELSE 0 END) AS pending_tickets,
       SUM(CASE WHEN t.status IN('RESOLVED','CLOSED') THEN 1 ELSE 0 END) AS solved_tickets,
       SUM(CASE WHEN ISNULL(t.ticket_type,'SUPPORT')='SUPPORT' AND v.is_current=1 THEN 1 ELSE 0 END) AS supervisor_total,
       SUM(CASE WHEN ISNULL(t.ticket_type,'SUPPORT')='SUPPORT' AND v.is_current=1 AND t.status NOT IN('RESOLVED','CLOSED') THEN 1 ELSE 0 END) AS supervisor_pending,
       SUM(CASE WHEN ISNULL(t.ticket_type,'SUPPORT')='SUPPORT' AND v.is_current=1 AND t.status IN('RESOLVED','CLOSED') THEN 1 ELSE 0 END) AS supervisor_resolved
FROM @visible v INNER JOIN dbo.support_tickets t ON t.id=v.id
GROUP BY t.assigned_department;

SELECT af.assigned_department,
       SUM(CASE WHEN af.status IN('OPEN','PENDING','IN_PROGRESS') THEN 1 ELSE 0 END) AS pending_findings,
       SUM(CASE WHEN af.status IN('RESOLVED','CLOSED') THEN 1 ELSE 0 END) AS solved_findings
FROM dbo.automatic_findings af
INNER JOIN dbo.agencies a ON a.id=af.agency_id
WHERE @includeFindings=1 AND (@findingDepartment='' OR af.assigned_department=@findingDepartment)
  AND (@findingTechnicianOnly=0 OR af.assigned_technician_id=@userId)
  AND (@findingScoped=0 OR EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))
GROUP BY af.assigned_department;
""", connection);
		command.Parameters.AddWithValue("@administrator", role == "Administrator" ? 1 : 0);
		command.Parameters.AddWithValue("@groupSupervisor", role == "GroupAdministrator" ? 1 : 0);
		command.Parameters.AddWithValue("@supportDepartment", DepartmentForRole(role));
		command.Parameters.AddWithValue("@supportTeam", role == "Warehouse" ? "WAREHOUSE" : supportTeam ?? "");
		command.Parameters.AddWithValue("@userEmail", userEmail ?? "");
		command.Parameters.AddWithValue("@administrativeRequester", DepartmentAccounts.IsSupportScope(role) || role == "Administrative" && EmmaAdministrativeAccess.IsEmma(userEmail) ? 1 : 0);
		command.Parameters.AddWithValue("@areaManager", areaManager ? 1 : 0);
		command.Parameters.AddWithValue("@canAssign", canAssignTickets ? 1 : 0);
		command.Parameters.AddWithValue("@userId", userId);
		command.Parameters.AddWithValue("@findingDepartment", DepartmentForRole(role));
		command.Parameters.AddWithValue("@includeFindings", includeFindings ? 1 : 0);
		bool findingTechnicianOnly = role == "GroupAdministrator" || ((role is "Technology" or "GeneralServices" or "HumanResources" || DepartmentAccounts.IsSupportScope(role)) && !canAssignTickets);
		command.Parameters.AddWithValue("@findingTechnicianOnly", findingTechnicianOnly ? 1 : 0);
		command.Parameters.AddWithValue("@findingScoped", 0);

		Dictionary<string, SupportDepartmentMetricRow> metrics = new(StringComparer.OrdinalIgnoreCase);
		await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
		while (await reader.ReadAsync(ct))
		{
			string department = reader.GetString(0);
			int tickets = reader.GetInt32(1);
			int solvedTickets = reader.GetInt32(2);
			metrics[department] = new SupportDepartmentMetricRow(department, tickets, 0, solvedTickets, reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5));
		}
		if (await reader.NextResultAsync(ct))
		{
			while (await reader.ReadAsync(ct))
			{
				string department = reader.GetString(0);
				int findings = reader.GetInt32(1);
				int solvedFindings = reader.GetInt32(2);
				SupportDepartmentMetricRow current = metrics.GetValueOrDefault(department) ?? new SupportDepartmentMetricRow(department, 0, 0, 0, 0, 0, 0);
				metrics[department] = current with { Findings = findings, Solutions = current.Solutions + solvedFindings };
			}
		}
		return metrics.Values.OrderBy(item => item.Department).ToList();
	}

	public async Task<List<SupportDepartmentRow>> SupportCatalog(CancellationToken ct)
	{
		List<SupportDepartmentRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<SupportDepartmentRow> departments = new List<SupportDepartmentRow>();
			await using (SqlCommand administrativeCatalogCommand = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.support_departments WITH (UPDLOCK, HOLDLOCK) WHERE code='TREASURY')\r\n    INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active)\r\n    VALUES('TREASURY',N'Tesorería',N'Archivos, comunicaciones y solicitudes del departamento.','#0F766E',60,1);\r\nIF NOT EXISTS(SELECT 1 FROM dbo.support_departments WITH (UPDLOCK, HOLDLOCK) WHERE code='ACCOUNTING')\r\n    INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active)\r\n    VALUES('ACCOUNTING',N'Contabilidad',N'Archivos, comunicaciones y solicitudes del departamento.','#2563EB',70,1);\r\nIF NOT EXISTS(SELECT 1 FROM dbo.support_departments WITH (UPDLOCK, HOLDLOCK) WHERE code='COMMERCIALIZATION')\r\n    INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active)\r\n    VALUES('COMMERCIALIZATION',N'Comercialización',N'Archivos, comunicaciones y solicitudes del departamento.','#0E7490',50,1);\r\nIF NOT EXISTS(SELECT 1 FROM dbo.support_departments WITH (UPDLOCK, HOLDLOCK) WHERE code='ADMINISTRATIVE')\r\n    INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active)\r\n    VALUES('ADMINISTRATIVE',N'Departamento Administrativo',N'Solicitudes y gestiones administrativas.','#0F766E',40,1);\r\nIF NOT EXISTS(SELECT 1 FROM dbo.support_categories WITH (UPDLOCK, HOLDLOCK) WHERE department_code='ADMINISTRATIVE' AND code='ADMINISTRATIVE_REQUEST')\r\n    INSERT INTO dbo.support_categories(department_code,code,name,description,default_priority,requires_detail,detail_label,display_order,is_active)\r\n    VALUES('ADMINISTRATIVE','ADMINISTRATIVE_REQUEST',N'Solicitud administrativa',N'Gestión o requerimiento dirigido al Departamento Administrativo.','MEDIUM',1,N'Describe la gestión administrativa requerida',10,1);", connection))
			{
				await administrativeCatalogCommand.ExecuteNonQueryAsync(ct);
			}
			await using (SqlCommand administrativeCatalogCommand = new SqlCommand("INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active)\r\nSELECT v.code,v.name,N'Soporte del departamento.','#0E7490',80,1\r\nFROM (VALUES('GENERAL_AUDIT',N'Auditoría General'),('VAULT',N'Bóveda'),('CASHIER',N'Caja'),('COMMUNICATION',N'Comunicación'),('SUPERVISION',N'Supervisión'),('WAREHOUSE',N'Almacén'),('WORKSHOP',N'Taller'),('HUMAN_RESOURCES',N'Recursos Humanos')) v(code,name)\r\nWHERE NOT EXISTS(SELECT 1 FROM dbo.support_departments d WITH(UPDLOCK,HOLDLOCK) WHERE d.code=v.code);\r\nINSERT INTO dbo.support_categories(department_code,code,name,description,default_priority,requires_detail,detail_label,display_order,is_active)\r\nSELECT d.code,'DEPARTMENT_REQUEST',N'Solicitud al departamento',N'Incidencia o requerimiento del departamento.','MEDIUM',1,N'Describe la incidencia o solicitud',100,1\r\nFROM dbo.support_departments d WHERE d.code IN('TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','COMMERCIALIZATION','COMMUNICATION','SUPERVISION','WAREHOUSE','WORKSHOP','HUMAN_RESOURCES')\r\nAND NOT EXISTS(SELECT 1 FROM dbo.support_categories c WITH(UPDLOCK,HOLDLOCK) WHERE c.department_code=d.code AND c.code='DEPARTMENT_REQUEST');", connection))
			{
				await administrativeCatalogCommand.ExecuteNonQueryAsync(ct);
			}
			await using (SqlCommand administrativeCatalogCommand = new SqlCommand("INSERT INTO dbo.support_categories(department_code,code,name,description,default_priority,requires_detail,detail_label,display_order,is_active)\r\nSELECT d.code,'NEW_AGENCY',N'Agencia nueva',N'Instalación o atención de una agencia nueva.','MEDIUM',1,N'Describe lo requerido para la agencia nueva',95,1\r\nFROM dbo.support_departments d WHERE d.code IN('TECHNOLOGY','GENERAL_SERVICES')\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.support_categories c WITH(UPDLOCK,HOLDLOCK) WHERE c.department_code=d.code AND c.code='NEW_AGENCY');", connection))
			{
				await administrativeCatalogCommand.ExecuteNonQueryAsync(ct);
			}
			await using (SqlCommand administrativeCatalogCommand = new SqlCommand("IF OBJECT_ID(N'dbo.support_categories',N'U') IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.support_departments WHERE code='TECHNOLOGY') MERGE dbo.support_categories AS target USING(SELECT 'TECHNOLOGY' department_code,'CPU_FAILURE' code,N'Falla CPU' name,N'CPU sin encender, lenta o con avería de hardware.' description,'CRITICAL' default_priority,CAST(1 AS bit) requires_detail,N'Describe la falla de la CPU' detail_label,CAST(NULL AS nvarchar(max)) options_json,65 display_order,CAST(1 AS bit) is_active) AS source ON target.department_code=source.department_code AND target.code=source.code WHEN MATCHED THEN UPDATE SET name=source.name,description=source.description,default_priority=source.default_priority,requires_detail=source.requires_detail,detail_label=source.detail_label,options_json=source.options_json,display_order=source.display_order,is_active=source.is_active,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(department_code,code,name,description,default_priority,requires_detail,detail_label,options_json,display_order,is_active) VALUES(source.department_code,source.code,source.name,source.description,source.default_priority,source.requires_detail,source.detail_label,source.options_json,source.display_order,source.is_active);", connection))
			{
				await administrativeCatalogCommand.ExecuteNonQueryAsync(ct);
			}
			await using (SqlCommand administrativeCatalogCommand = new SqlCommand("IF OBJECT_ID(N'dbo.support_tickets',N'U') IS NOT NULL UPDATE dbo.support_tickets SET priority='CRITICAL',updated_at=SYSUTCDATETIME() WHERE assigned_department='TECHNOLOGY' AND category='CPU_FAILURE' AND ISNULL(priority,'')<>'CRITICAL';", connection))
			{
				await administrativeCatalogCommand.ExecuteNonQueryAsync(ct);
			}
			List<SupportDepartmentRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT d.code,d.name,d.description,d.accent_color,d.display_order,d.is_active,c.id,c.code,c.name,c.description,c.default_priority,c.requires_detail,c.detail_label,c.options_json,c.display_order,c.is_active FROM dbo.support_departments d LEFT JOIN dbo.support_categories c ON c.department_code=d.code WHERE d.is_active=1 ORDER BY d.display_order,c.display_order,c.name;", connection))
			{
				List<SupportDepartmentRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					Dictionary<string, SupportDepartmentRow> map = new Dictionary<string, SupportDepartmentRow>();
					while (await reader.ReadAsync(ct))
					{
						string text = reader.GetString(0);
						if (!map.TryGetValue(text, out SupportDepartmentRow value))
						{
							value = (map[text] = new SupportDepartmentRow(text, reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetBoolean(5), new List<SupportCategoryRow>()));
							departments.Add(value);
						}
						if (!reader.IsDBNull(6))
						{
							value.Categories.Add(new SupportCategoryRow(reader.GetGuid(6), reader.GetString(7), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetBoolean(11), reader.IsDBNull(12) ? null : reader.GetString(12), reader.IsDBNull(13) ? null : JsonSerializer.Deserialize<List<string>>(reader.GetString(13)), reader.GetInt32(14), reader.GetBoolean(15)));
						}
					}
					list = departments;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task UpsertSupportDepartment(SupportDepartmentInput body, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await using SqlCommand command = new SqlCommand("UPDATE dbo.support_departments SET name=@name,description=@description,accent_color=@color,display_order=@sort,is_active=@active,updated_at=SYSUTCDATETIME() WHERE code=@code;IF @@ROWCOUNT=0 INSERT INTO dbo.support_departments(code,name,description,accent_color,display_order,is_active) VALUES(@code,@name,@description,@color,@sort,@active);", connection);
		command.Parameters.AddWithValue("@code", body.Code);
		command.Parameters.AddWithValue("@name", body.Name);
		command.Parameters.AddWithValue("@description", Db(body.Description));
		command.Parameters.AddWithValue("@color", body.AccentColor ?? "#38BDF8");
		command.Parameters.AddWithValue("@sort", body.DisplayOrder);
		command.Parameters.AddWithValue("@active", body.IsActive);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task UpsertSupportCategory(Guid? id, SupportCategoryInput body, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		Guid guid = id ?? Guid.NewGuid();
		await using SqlCommand command = new SqlCommand("UPDATE dbo.support_categories SET department_code=@department,code=@code,name=@name,description=@description,default_priority=@priority,requires_detail=@requires,detail_label=@label,options_json=@options,display_order=@sort,is_active=@active,updated_at=SYSUTCDATETIME() WHERE id=@id;IF @@ROWCOUNT=0 INSERT INTO dbo.support_categories(id,department_code,code,name,description,default_priority,requires_detail,detail_label,options_json,display_order,is_active) VALUES(@id,@department,@code,@name,@description,@priority,@requires,@label,@options,@sort,@active);", connection);
		command.Parameters.AddWithValue("@id", guid);
		command.Parameters.AddWithValue("@department", body.DepartmentCode);
		command.Parameters.AddWithValue("@code", body.Code);
		command.Parameters.AddWithValue("@name", body.Name);
		command.Parameters.AddWithValue("@description", Db(body.Description));
		command.Parameters.AddWithValue("@priority", body.DefaultPriority);
		command.Parameters.AddWithValue("@requires", body.RequiresDetail);
		command.Parameters.AddWithValue("@label", Db(body.DetailLabel));
		command.Parameters.AddWithValue("@options", Db((body.Options == null) ? null : JsonSerializer.Serialize(body.Options)));
		command.Parameters.AddWithValue("@sort", body.DisplayOrder);
		command.Parameters.AddWithValue("@active", body.IsActive);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<bool> DeleteSupportCategory(Guid id, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection connection = await Open(ct))
		{
			bool flag;
			await using (SqlCommand command = new SqlCommand("DELETE FROM dbo.support_categories WHERE id=@id;", connection))
			{
				command.Parameters.AddWithValue("@id", id);
				flag = await command.ExecuteNonQueryAsync(ct) == 1;
			}
			result = flag;
		}
		return result;
	}

	public async Task<List<AgencyTransitionRow>?> AgencyTransitions(string role, CancellationToken ct)
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
		if (!flag)
		{
			return null;
		}
		List<AgencyTransitionRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyTransitionStages(connection, ct);
			await EnsureAgencyTransitionEvidenceSchema(connection, ct);
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) progressCapabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			List<AgencyTransitionRow> projects = new List<AgencyTransitionRow>();
			Dictionary<Guid, AgencyTransitionRow> map = new Dictionary<Guid, AgencyTransitionRow>();
			Dictionary<Guid, AgencyTransitionStageRow> stageMap = new Dictionary<Guid, AgencyTransitionStageRow>();
			Dictionary<Guid, AgencyTransitionProgressRow> progressMap = new Dictionary<Guid, AgencyTransitionProgressRow>();
			await using (SqlCommand command = new SqlCommand("SELECT p.id,p.agency_id,a.codigo,a.terminal,a.grupo,p.project_type,p.status,p.current_stage,p.notes,p.created_by_name,p.created_at,p.updated_at,p.completed_at FROM dbo.agency_transition_projects p INNER JOIN dbo.agencies a ON a.id=p.agency_id WHERE p.status<>'CANCELLED' ORDER BY CASE p.status WHEN 'ACTIVE' THEN 0 WHEN 'PENDING' THEN 1 ELSE 2 END,p.updated_at DESC;", connection))
			{
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					AgencyTransitionRow agencyTransitionRow = new AgencyTransitionRow(reader.GetGuid(0), Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetString(9), reader.GetDateTime(10), reader.GetDateTime(11), reader.IsDBNull(12) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(12)), new List<AgencyTransitionStageRow>());
					projects.Add(agencyTransitionRow);
					map[agencyTransitionRow.Id] = agencyTransitionRow;
				}
			}
			if (projects.Count == 0)
			{
				result = projects;
			}
			else
			{
				await using (SqlCommand command = new SqlCommand("SELECT s.id,s.project_id,s.department,s.stage_order,s.status,s.notes,s.started_at,s.completed_at,s.completed_by_name,s.updated_at FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id WHERE p.status<>'CANCELLED' ORDER BY s.project_id,s.stage_order;", connection))
				{
					await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
					while (await reader.ReadAsync(ct))
					{
						if (map.TryGetValue(reader.GetGuid(1), out AgencyTransitionRow value))
						{
							AgencyTransitionStageRow agencyTransitionStageRow = new AgencyTransitionStageRow(reader.GetGuid(0), reader.GetString(2), reader.GetByte(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(6)), reader.IsDBNull(7) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(7)), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetDateTime(9), new List<AgencyTransitionProgressRow>());
							value.Stages.Add(agencyTransitionStageRow);
							stageMap[agencyTransitionStageRow.Id] = agencyTransitionStageRow;
						}
					}
				}
				if (progressCapabilities.Exists)
				{
					try
					{
						string value2 = (progressCapabilities.EntryType ? "h.entry_type" : "CASE WHEN h.note LIKE '__ASSIGNMENT__%' THEN 'ASSIGNMENT' WHEN h.progress_status='EDITED' THEN 'STAGE_EDIT' WHEN h.progress_status='COMPLETED' THEN 'STAGE_COMPLETION' ELSE 'ADVANCE' END");
						string value3 = (progressCapabilities.UpdatedAt ? "h.updated_at" : "CAST(NULL AS datetime2(3))");
						string value4 = (progressCapabilities.DeletedAt ? "AND h.deleted_at IS NULL" : "");
						string cmdText = $"SELECT h.id,h.stage_id,h.note,h.progress_status,{value2},h.created_by_name,h.created_at,{value3} FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE p.status<>'CANCELLED' {value4} ORDER BY h.created_at,h.id;";
						await using (SqlCommand command = new SqlCommand(cmdText, connection))
						{
							await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
							while (await reader.ReadAsync(ct))
							{
								if (stageMap.TryGetValue(reader.GetGuid(1), out AgencyTransitionStageRow value5))
								{
									AgencyTransitionProgressRow agencyTransitionProgressRow = new AgencyTransitionProgressRow(reader.GetGuid(0), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetDateTime(6), reader.IsDBNull(7) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(7)), new List<AgencyTransitionEvidenceRow>());
									value5.Progress.Add(agencyTransitionProgressRow);
									progressMap[agencyTransitionProgressRow.Id] = agencyTransitionProgressRow;
								}
							}
						}
						if (progressMap.Count > 0)
						{
							await using SqlCommand command = new SqlCommand("SELECT e.id,e.progress_id,e.file_name,e.content_type,e.captured_at,e.latitude,e.longitude,e.accuracy_meters,e.location_captured_at,e.uploaded_by_name FROM dbo.agency_transition_progress_evidence e INNER JOIN dbo.agency_transition_projects p ON p.id=e.project_id WHERE p.status<>'CANCELLED' ORDER BY e.captured_at,e.id;", connection);
							await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
							while (await reader.ReadAsync(ct))
							{
								if (progressMap.TryGetValue(reader.GetGuid(1), out AgencyTransitionProgressRow value6))
								{
									value6.Evidence.Add(new AgencyTransitionEvidenceRow(reader.GetGuid(0), reader.GetString(2), reader.GetString(3), reader.GetDateTime(4), $"/api/agency-transitions/evidence/{reader.GetGuid(0)}", reader.IsDBNull(5) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetDecimal(5))), reader.IsDBNull(6) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetDecimal(6))), reader.IsDBNull(7) ? ((double?)null) : new double?(Convert.ToDouble(reader.GetDecimal(7))), reader.IsDBNull(8) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(8)), reader.IsDBNull(9) ? null : reader.GetString(9)));
								}
							}
						}
					}
					catch (SqlException)
					{
					}
				}
				result = projects;
			}
		}
		return result;
	}

	public async Task<TicketActionResult> CreateAgencyTransition(AgencyTransitionCreate body, string userId, string role, string actorName, CancellationToken ct)
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
		if (!flag)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Tu cuenta no puede crear procesos de construcción.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			TicketActionResult ticketActionResult2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				Guid id = Guid.NewGuid();
				TicketActionResult ticketActionResult;
				await using (SqlCommand exists = new SqlCommand("SELECT COUNT(*) FROM dbo.agency_transition_projects WITH(UPDLOCK,HOLDLOCK) WHERE agency_id=@agency AND status IN('ACTIVE','PENDING');", connection, transaction))
				{
					exists.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
					if (Convert.ToInt32(await exists.ExecuteScalarAsync(ct)) > 0)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Esta agencia ya tiene un proceso activo o pendiente.");
						goto IL_0445;
					}
				}
				ticketActionResult = null;
				await using (SqlCommand exists = new SqlCommand("INSERT INTO dbo.agency_transition_projects(id,agency_id,project_type,status,current_stage,notes,created_by_user_id,created_by_name) SELECT @id,a.id,@type,'ACTIVE','GENERAL_SERVICES',@notes,@userId,@actor FROM dbo.agencies a WHERE a.id=@agency;", connection, transaction))
				{
					exists.Parameters.AddWithValue("@id", id);
					exists.Parameters.AddWithValue("@agency", body.AgencyId.Value.ToString());
					exists.Parameters.AddWithValue("@type", body.ProjectType);
					exists.Parameters.AddWithValue("@notes", Db(body.Notes));
					exists.Parameters.AddWithValue("@userId", userId);
					exists.Parameters.AddWithValue("@actor", actorName);
					if (await exists.ExecuteNonQueryAsync(ct) != 1)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Agencia no encontrada.");
						goto IL_072c;
					}
				}
				await using (SqlCommand exists = new SqlCommand("INSERT INTO dbo.agency_transition_stages(project_id,department,stage_order,status,started_at) VALUES(@id,'GENERAL_SERVICES',1,'IN_PROGRESS',SYSUTCDATETIME()),(@id,'TECHNOLOGY',2,'PENDING',NULL),(@id,'HUMAN_RESOURCES',3,'PENDING',NULL);", connection, transaction))
				{
					exists.Parameters.AddWithValue("@id", id);
					await exists.ExecuteNonQueryAsync(ct);
				}
				await transaction.CommitAsync(ct);
				ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
				goto end_IL_0214;
				IL_0445:
				ticketActionResult2 = ticketActionResult;
				goto end_IL_0214;
				IL_072c:
				ticketActionResult2 = ticketActionResult;
				end_IL_0214:;
			}
			result = ticketActionResult2;
		}
		return result;
	}

	public async Task<TicketActionResult> UpdateAgencyTransitionStatus(Guid id, AgencyTransitionStatusUpdate body, string userId, string role, string actorName, CancellationToken ct)
	{
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyTransitionStages(connection, ct);
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) progressCapabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				try
				{
					string currentStatus = null;
					string currentDepartment = null;
					Guid? stageId = null;
					await using (SqlCommand command = new SqlCommand("SELECT p.status,p.current_stage,s.id FROM dbo.agency_transition_projects p WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.agency_transition_stages s ON s.project_id=p.id AND s.department=p.current_stage WHERE p.id=@id AND p.status IN('ACTIVE','PENDING');", connection, transaction))
					{
						command.Parameters.AddWithValue("@id", id);
						await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
						if (await reader.ReadAsync(ct))
						{
							currentStatus = reader.GetString(0);
							currentDepartment = reader.GetString(1);
							stageId = (reader.IsDBNull(2) ? ((Guid?)null) : new Guid?(reader.GetGuid(2)));
						}
					}
					if (currentStatus == null || currentDepartment == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Proceso no encontrado o ya finalizado.");
					}
					else
					{
						string text = DepartmentForRole(role);
						if (role != "Administrator" && text != currentDepartment)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el administrador o el departamento de la etapa actual puede cambiar el estado del proceso.");
						}
						else if (currentStatus == body.Status)
						{
							await transaction.CommitAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
						}
						else
						{
							await using (SqlCommand command = new SqlCommand("UPDATE dbo.agency_transition_projects SET status=@status,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
							{
								command.Parameters.AddWithValue("@id", id);
								command.Parameters.AddWithValue("@status", body.Status);
								await command.ExecuteNonQueryAsync(ct);
							}
							if (progressCapabilities.Exists && stageId.HasValue)
							{
								string value = ((body.Status == "PENDING") ? ("Proceso puesto en pendiente: " + body.Reason.Trim()) : "Proceso reanudado.");
								string value2 = (progressCapabilities.EntryType ? ",entry_type" : "");
								string value3 = (progressCapabilities.EntryType ? ",'STAGE_EDIT'" : "");
								string value4 = (progressCapabilities.UpdatedAt ? ",updated_at" : "");
								string value5 = (progressCapabilities.UpdatedAt ? ",SYSUTCDATETIME()" : "");
								await using SqlCommand command = new SqlCommand($"INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status{value2},created_by_user_id,created_by_name{value4}) VALUES(@project,@stage,@department,@note,'EDITED'{value3},@userId,@actor{value5});", connection, transaction);
								command.Parameters.AddWithValue("@project", id);
								command.Parameters.AddWithValue("@stage", stageId.Value);
								command.Parameters.AddWithValue("@department", currentDepartment);
								command.Parameters.AddWithValue("@note", value);
								command.Parameters.AddWithValue("@userId", userId);
								command.Parameters.AddWithValue("@actor", actorName);
								await command.ExecuteNonQueryAsync(ct);
							}
							await transaction.CommitAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
						}
					}
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> UpdateAgencyTransitionStage(Guid id, AgencyTransitionStageUpdate body, string userId, string role, string actorName, CancellationToken ct)
	{
		string department = DepartmentForRole(role);
		if (role != "Administrator" && string.IsNullOrEmpty(department))
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Tu cuenta no pertenece a uno de los departamentos del proceso.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) progressCapabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string current = null;
				await using (SqlCommand find = new SqlCommand("SELECT current_stage FROM dbo.agency_transition_projects WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND status='ACTIVE';", connection, transaction))
				{
					find.Parameters.AddWithValue("@id", id);
					current = (string)(await find.ExecuteScalarAsync(ct));
				}
				string status;
				string normalizedNote;
				TicketActionResult ticketActionResult2;
				Guid stageId;
				if (current == null)
				{
					await transaction.RollbackAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Proceso no encontrado o ya finalizado.");
				}
				else if (role != "Administrator" && department != current)
				{
					await transaction.RollbackAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "La etapa actual corresponde a " + current + ".");
				}
				else if (current == "COMPLETED")
				{
					await transaction.RollbackAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "El proceso ya está completado.");
				}
				else
				{
					status = body.Status;
					if (string.IsNullOrWhiteSpace(body.Notes))
					{
						normalizedNote = ((status == "COMPLETED") ? "Etapa completada." : null);
					}
					else
					{
						normalizedNote = body.Notes.Trim();
					}
					if (status == "IN_PROGRESS" && normalizedNote == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "Describe el registro antes de guardarlo.");
					}
					else
					{
						if (progressCapabilities.Exists)
						{
							string value = (progressCapabilities.EntryType ? "AND entry_type='ADVANCE'" : "AND note NOT LIKE '__ASSIGNMENT__%'");
							string value2 = (progressCapabilities.DeletedAt ? "AND deleted_at IS NULL" : "");
							await using (SqlCommand find = new SqlCommand($"SELECT COUNT(*) FROM dbo.agency_transition_progress WHERE project_id=@id AND department=@department {value} AND progress_status='IN_PROGRESS' {value2};", connection, transaction))
							{
								find.Parameters.AddWithValue("@id", id);
								find.Parameters.AddWithValue("@department", current);
								if (Convert.ToInt32(await find.ExecuteScalarAsync(ct)) > 0)
								{
									await transaction.RollbackAsync(ct);
									ticketActionResult2 = new TicketActionResult(Ok: false, "CONFLICT", (status == "IN_PROGRESS") ? "Completa el registro actual y adjunta sus evidencias antes de crear otro registro." : "Completa o elimina los registros pendientes antes de cerrar la etapa del departamento.");
									goto IL_0a21;
								}
							}
							ticketActionResult2 = null;
						}
						await using (SqlCommand find = new SqlCommand("UPDATE dbo.agency_transition_stages SET status=@status,notes=COALESCE(@notes,notes),started_at=COALESCE(started_at,SYSUTCDATETIME()),completed_at=CASE WHEN @status='COMPLETED' THEN SYSUTCDATETIME() ELSE NULL END,completed_by_user_id=CASE WHEN @status='COMPLETED' THEN @userId ELSE NULL END,completed_by_name=CASE WHEN @status='COMPLETED' THEN @actor ELSE NULL END,updated_at=SYSUTCDATETIME() WHERE project_id=@id AND department=@department; SELECT id FROM dbo.agency_transition_stages WHERE project_id=@id AND department=@department;", connection, transaction))
						{
							find.Parameters.AddWithValue("@id", id);
							find.Parameters.AddWithValue("@department", current);
							find.Parameters.AddWithValue("@status", status);
							find.Parameters.AddWithValue("@notes", Db(normalizedNote));
							find.Parameters.AddWithValue("@userId", userId);
							find.Parameters.AddWithValue("@actor", actorName);
							if (await find.ExecuteScalarAsync(ct) is Guid guid)
							{
								stageId = guid;
								goto IL_0d12;
							}
							await transaction.RollbackAsync(ct);
							ticketActionResult2 = new TicketActionResult(Ok: false, "NOT_FOUND", "Etapa no encontrada.");
						}
						ticketActionResult = ticketActionResult2;
					}
				}
				goto end_IL_02ec;
				IL_0a21:
				ticketActionResult = ticketActionResult2;
				goto end_IL_02ec;
				IL_0d12:
				if (progressCapabilities.Exists)
				{
					string value3 = (progressCapabilities.EntryType ? ",entry_type" : "");
					string value4 = (progressCapabilities.EntryType ? ",@entryType" : "");
					await using SqlCommand find = new SqlCommand($"INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status{value3},created_by_user_id,created_by_name) VALUES(@project,@stage,@department,@note,@status{value4},@userId,@actor);", connection, transaction);
					find.Parameters.AddWithValue("@project", id);
					find.Parameters.AddWithValue("@stage", stageId);
					find.Parameters.AddWithValue("@department", current);
					find.Parameters.AddWithValue("@note", normalizedNote);
					find.Parameters.AddWithValue("@status", status);
					if (progressCapabilities.EntryType)
					{
						find.Parameters.AddWithValue("@entryType", (status == "COMPLETED") ? "STAGE_COMPLETION" : "ADVANCE");
					}
					find.Parameters.AddWithValue("@userId", userId);
					find.Parameters.AddWithValue("@actor", actorName);
					await find.ExecuteNonQueryAsync(ct);
				}
				if (status == "COMPLETED")
				{
					string text = current;
					string text2;
					if (text == "GENERAL_SERVICES")
					{
						text2 = "TECHNOLOGY";
					}
					else
					{
						text2 = ((!(text == "TECHNOLOGY")) ? "COMPLETED" : "HUMAN_RESOURCES");
					}
					string next = text2;
					await using (SqlCommand find = new SqlCommand("UPDATE dbo.agency_transition_projects SET current_stage=@next,status=CASE WHEN @next='COMPLETED' THEN 'COMPLETED' ELSE 'ACTIVE' END,completed_at=CASE WHEN @next='COMPLETED' THEN SYSUTCDATETIME() ELSE NULL END,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
					{
						find.Parameters.AddWithValue("@id", id);
						find.Parameters.AddWithValue("@next", next);
						await find.ExecuteNonQueryAsync(ct);
					}
					if (next != "COMPLETED")
					{
						await using SqlCommand find = new SqlCommand("UPDATE dbo.agency_transition_stages SET status='IN_PROGRESS',started_at=COALESCE(started_at,SYSUTCDATETIME()),updated_at=SYSUTCDATETIME() WHERE project_id=@id AND department=@next;", connection, transaction);
						find.Parameters.AddWithValue("@id", id);
						find.Parameters.AddWithValue("@next", next);
						await find.ExecuteNonQueryAsync(ct);
					}
				}
				await transaction.CommitAsync(ct);
				ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
				end_IL_02ec:;
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> EditAgencyTransitionStage(Guid projectId, Guid stageId, AgencyTransitionStageEdit body, string userId, string role, string actorName, CancellationToken ct)
	{
		string department = DepartmentForRole(role);
		string normalizedNote = (string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim());
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) progressCapabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			TicketActionResult ticketActionResult2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				TicketActionResult ticketActionResult;
				await using (SqlCommand command = new SqlCommand("UPDATE s SET notes=@notes,updated_at=SYSUTCDATETIME() FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id WHERE s.id=@stageId AND p.id=@projectId AND p.status<>'CANCELLED' AND (@admin=1 OR s.department=@department); SELECT @@ROWCOUNT;", connection, transaction))
				{
					command.Parameters.AddWithValue("@notes", Db(normalizedNote));
					command.Parameters.AddWithValue("@stageId", stageId);
					command.Parameters.AddWithValue("@projectId", projectId);
					command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
					command.Parameters.AddWithValue("@department", department);
					int num = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
					if (num != 1)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "No tienes permiso para editar esta etapa.");
						goto IL_04fc;
					}
				}
				if (progressCapabilities.Exists && progressCapabilities.EntryType)
				{
					await using SqlCommand command = new SqlCommand("INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status,entry_type,created_by_user_id,created_by_name) SELECT @projectId,s.id,s.department,@historyNote,'EDITED','STAGE_EDIT',@userId,@actor FROM dbo.agency_transition_stages s WHERE s.id=@stageId AND s.project_id=@projectId;", connection, transaction);
					command.Parameters.AddWithValue("@projectId", projectId);
					command.Parameters.AddWithValue("@stageId", stageId);
					command.Parameters.AddWithValue("@historyNote", normalizedNote ?? "Se eliminó la descripción de la etapa.");
					command.Parameters.AddWithValue("@userId", userId);
					command.Parameters.AddWithValue("@actor", actorName);
					await command.ExecuteNonQueryAsync(ct);
				}
				await using (SqlCommand command = new SqlCommand("UPDATE dbo.agency_transition_projects SET updated_at=SYSUTCDATETIME() WHERE id=@projectId;", connection, transaction))
				{
					command.Parameters.AddWithValue("@projectId", projectId);
					await command.ExecuteNonQueryAsync(ct);
				}
				await transaction.CommitAsync(ct);
				ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
				goto end_IL_025e;
				IL_04fc:
				ticketActionResult2 = ticketActionResult;
				end_IL_025e:;
			}
			result = ticketActionResult2;
		}
		return result;
	}

	public async Task<TicketActionResult> AssignAgencyTransitionProgress(Guid projectId, Guid stageId, Guid progressId, AgencyTransitionAssignmentUpdate body, string userId, string role, string actorName, CancellationToken ct)
	{
		string ownerDepartment = DepartmentForRole(role);
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) capabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			if (!capabilities.Exists)
			{
				result = new TicketActionResult(Ok: false, "CONFLICT", "El historial de avances debe estar disponible para guardar responsables.");
			}
			else
			{
				TicketActionResult ticketActionResult;
				await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
				{
					string value = (capabilities.EntryType ? "AND h.entry_type='ADVANCE'" : "AND h.note NOT LIKE '__ASSIGNMENT__%'");
					string value2 = (capabilities.DeletedAt ? "AND h.deleted_at IS NULL" : "");
					string stageDepartment;
					await using (SqlCommand stageCommand = new SqlCommand($"SELECT s.department FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id INNER JOIN dbo.agency_transition_progress h ON h.project_id=p.id AND h.stage_id=s.id WHERE s.id=@stageId AND p.id=@projectId AND h.id=@progressId AND p.status<>'CANCELLED' {value} {value2};", connection, transaction))
					{
						stageCommand.Parameters.AddWithValue("@stageId", stageId);
						stageCommand.Parameters.AddWithValue("@projectId", projectId);
						stageCommand.Parameters.AddWithValue("@progressId", progressId);
						stageDepartment = (string)(await stageCommand.ExecuteScalarAsync(ct));
					}
					string assignmentType;
					string assignedUserId;
					string assignedName;
					TicketActionResult ticketActionResult3;
					if (stageDepartment == null)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Registro de trabajo no encontrado.");
					}
					else if (role != "Administrator" && ownerDepartment != stageDepartment)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el departamento propietario puede asignar el responsable de este registro.");
					}
					else
					{
						assignmentType = body.AssignmentType;
						assignedUserId = null;
						if (assignmentType == "TECHNICIAN")
						{
							if (!string.IsNullOrWhiteSpace(body.TechnicianUserId))
							{
								await using (SqlCommand stageCommand = new SqlCommand("SELECT TOP(1)CONVERT(nvarchar(80),u.id),u.display_name\r\nFROM dbo.admin_users u\r\nWHERE CONVERT(nvarchar(80),u.id)=@technicianId\r\n  AND u.is_active=1\r\n  AND u.role=@department\r\n  AND u.role<>'GROUP_ADMIN'\r\n  AND (u.support_team='TECHNICIANS' OR (\r\n      JSON_VALUE(u.permissions_json,'$.canResolveTickets')='true'\r\n      AND ISNULL(JSON_VALUE(u.permissions_json,'$.canAssignTickets'),'false')<>'true'\r\n  ));", connection, transaction))
								{
									stageCommand.Parameters.AddWithValue("@technicianId", body.TechnicianUserId.Trim());
									stageCommand.Parameters.AddWithValue("@department", stageDepartment);
									TicketActionResult ticketActionResult2;
									await using (SqlDataReader reader = await stageCommand.ExecuteReaderAsync(ct))
									{
										if (!(await reader.ReadAsync(ct)))
										{
											await reader.DisposeAsync();
											await transaction.RollbackAsync(ct);
											ticketActionResult2 = new TicketActionResult(Ok: false, "VALIDATION", "El usuario seleccionado no es un técnico activo de este departamento.");
											goto IL_0ae2;
										}
										assignedUserId = reader.GetString(0);
										assignedName = reader.GetString(1);
										await reader.DisposeAsync();
									}
									goto end_IL_075d;
									IL_0ae2:
									ticketActionResult3 = ticketActionResult2;
									goto IL_0bba;
									end_IL_075d:;
								}
								goto IL_0cc1;
							}
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "Selecciona un técnico del departamento.");
						}
						else
						{
							assignedName = body.ContractorName?.Trim() ?? "";
							int length = assignedName.Length;
							if ((length >= 2 && length <= 160) || 1 == 0)
							{
								goto IL_0cc1;
							}
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "VALIDATION", "Escribe el nombre completo del contratista.");
						}
					}
					goto end_IL_02d5;
					IL_0cc1:
					string value3 = "__ASSIGNMENT__" + JsonSerializer.Serialize(new
					{
						assignmentType = assignmentType,
						assignedUserId = assignedUserId,
						assignedName = assignedName,
						targetProgressId = progressId
					});
					string value4 = (capabilities.EntryType ? ",entry_type" : "");
					string value5 = (capabilities.EntryType ? ",'ASSIGNMENT'" : "");
					string value6 = (capabilities.UpdatedAt ? ",updated_at" : "");
					string value7 = (capabilities.UpdatedAt ? ",SYSUTCDATETIME()" : "");
					await using (SqlCommand stageCommand = new SqlCommand($"INSERT INTO dbo.agency_transition_progress(project_id,stage_id,department,note,progress_status{value4},created_by_user_id,created_by_name{value6}) VALUES(@projectId,@stageId,@department,@note,'IN_PROGRESS'{value5},@userId,@actor{value7});", connection, transaction))
					{
						stageCommand.Parameters.AddWithValue("@projectId", projectId);
						stageCommand.Parameters.AddWithValue("@stageId", stageId);
						stageCommand.Parameters.AddWithValue("@department", stageDepartment);
						stageCommand.Parameters.AddWithValue("@note", value3);
						stageCommand.Parameters.AddWithValue("@userId", userId);
						stageCommand.Parameters.AddWithValue("@actor", actorName);
						await stageCommand.ExecuteNonQueryAsync(ct);
					}
					await using (SqlCommand stageCommand = new SqlCommand("UPDATE dbo.agency_transition_projects SET updated_at=SYSUTCDATETIME() WHERE id=@projectId;", connection, transaction))
					{
						stageCommand.Parameters.AddWithValue("@projectId", projectId);
						await stageCommand.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					goto end_IL_02d5;
					IL_0bba:
					ticketActionResult = ticketActionResult3;
					end_IL_02d5:;
				}
				result = ticketActionResult;
			}
		}
		return result;
	}

	public async Task<TicketActionResult> UpdateAgencyTransitionProgress(Guid projectId, Guid stageId, Guid progressId, AgencyTransitionProgressUpdate body, string userId, string role, string actorName, CancellationToken ct)
	{
		string department = DepartmentForRole(role);
		string note = (string.IsNullOrWhiteSpace(body.Note) ? null : body.Note.Trim());
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) capabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			TicketActionResult ticketActionResult;
			if (!capabilities.Exists)
			{
				result = new TicketActionResult(Ok: false, "NOT_FOUND", "El historial de avances todavía no está disponible.");
			}
			else
			{
				await EnsureAgencyTransitionEvidenceSchema(connection, ct);
				string updated = (capabilities.UpdatedAt ? ",updated_at=SYSUTCDATETIME()" : "");
				string typeFilter = (capabilities.EntryType ? "AND h.entry_type='ADVANCE'" : "");
				string visibleFilter = (capabilities.DeletedAt ? "AND h.deleted_at IS NULL" : "");
				if (body.Status == "COMPLETED")
				{
					await using (SqlCommand evidenceCount = new SqlCommand("SELECT COUNT(*) FROM dbo.agency_transition_progress_evidence e INNER JOIN dbo.agency_transition_progress h ON h.id=e.progress_id INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id WHERE e.progress_id=@progressId AND e.project_id=@projectId AND e.stage_id=@stageId AND (@admin=1 OR s.department=@department);", connection))
					{
						evidenceCount.Parameters.AddWithValue("@progressId", progressId);
						evidenceCount.Parameters.AddWithValue("@projectId", projectId);
						evidenceCount.Parameters.AddWithValue("@stageId", stageId);
						evidenceCount.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
						evidenceCount.Parameters.AddWithValue("@department", department);
						int num = Convert.ToInt32(await evidenceCount.ExecuteScalarAsync(ct));
						if ((num < 1 || num > 4) ? true : false)
						{
							ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Sube entre 1 y 4 fotografías de evidencia antes de completar este registro.");
							goto IL_04be;
						}
					}
					ticketActionResult = null;
				}
				string cmdText = $"UPDATE h SET note=COALESCE(@note,h.note),progress_status=COALESCE(@status,h.progress_status){updated} FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {typeFilter} {visibleFilter} AND (@admin=1 OR s.department=@department);";
				await using (SqlCommand command = new SqlCommand(cmdText, connection))
				{
					command.Parameters.AddWithValue("@note", Db(note));
					command.Parameters.AddWithValue("@status", Db(body.Status));
					command.Parameters.AddWithValue("@progressId", progressId);
					command.Parameters.AddWithValue("@projectId", projectId);
					command.Parameters.AddWithValue("@stageId", stageId);
					command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
					command.Parameters.AddWithValue("@department", department);
					ticketActionResult = ((await command.ExecuteNonQueryAsync(ct) == 1) ? new TicketActionResult(Ok: true, "OK", null) : new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el departamento propietario puede modificar este registro."));
				}
				result = ticketActionResult;
			}
			goto end_IL_010d;
			IL_04be:
			result = ticketActionResult;
			end_IL_010d:;
		}
		return result;
	}

	public async Task<TicketActionResult> AddAgencyTransitionEvidence(Guid projectId, Guid stageId, Guid progressId, List<AgencyTransitionEvidenceUpload> files, string userId, string role, string actorName, CancellationToken ct)
	{
		int count = files.Count;
		if ((count < 1 || count > 4) ? true : false)
		{
			return new TicketActionResult(Ok: false, "VALIDATION", "Selecciona entre 1 y 4 fotografías.");
		}
		string department = DepartmentForRole(role);
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyTransitionEvidenceSchema(connection, ct);
			(bool, bool, bool, bool, bool) tuple = await AgencyTransitionProgressCapabilities(connection, ct);
			string typeFilter = (tuple.Item2 ? "AND h.entry_type='ADVANCE'" : "");
			string visibleFilter = (tuple.Item4 ? "AND h.deleted_at IS NULL" : "");
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string cmdText = $"SELECT h.progress_status FROM dbo.agency_transition_progress h WITH(UPDLOCK,HOLDLOCK) INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {typeFilter} {visibleFilter} AND (@admin=1 OR s.department=@department);";
				string status;
				await using (SqlCommand target = new SqlCommand(cmdText, connection, transaction))
				{
					target.Parameters.AddWithValue("@progressId", progressId);
					target.Parameters.AddWithValue("@projectId", projectId);
					target.Parameters.AddWithValue("@stageId", stageId);
					target.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
					target.Parameters.AddWithValue("@department", department);
					status = (string)(await target.ExecuteScalarAsync(ct));
				}
				TicketActionResult ticketActionResult2;
				if (status == null)
				{
					await transaction.RollbackAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el departamento propietario puede agregar evidencias a este registro.");
				}
				else if (status == "COMPLETED")
				{
					await transaction.RollbackAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "El registro está completado y sus evidencias ya no pueden modificarse.");
				}
				else
				{
					await using (SqlCommand target = new SqlCommand("SELECT COUNT(*) FROM dbo.agency_transition_progress_evidence WITH(UPDLOCK,HOLDLOCK) WHERE progress_id=@progressId;", connection, transaction))
					{
						target.Parameters.AddWithValue("@progressId", progressId);
						int existing = Convert.ToInt32(await target.ExecuteScalarAsync(ct));
						if (existing + files.Count > 4)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult2 = new TicketActionResult(Ok: false, "CONFLICT", $"Este registro admite un máximo de 4 fotografías. Puedes agregar {Math.Max(0, 4 - existing)} más.");
							goto IL_092c;
						}
					}
					foreach (AgencyTransitionEvidenceUpload file in files)
					{
						await using SqlCommand target = new SqlCommand("INSERT INTO dbo.agency_transition_progress_evidence(id,project_id,stage_id,progress_id,file_name,content_type,photo_data,uploaded_by_user_id,uploaded_by_name,latitude,longitude,accuracy_meters,location_captured_at) VALUES(@id,@projectId,@stageId,@progressId,@fileName,@contentType,@data,@userId,@actor,@latitude,@longitude,@accuracy,@locationCapturedAt);", connection, transaction);
						target.Parameters.AddWithValue("@id", Guid.NewGuid());
						target.Parameters.AddWithValue("@projectId", projectId);
						target.Parameters.AddWithValue("@stageId", stageId);
						target.Parameters.AddWithValue("@progressId", progressId);
						target.Parameters.AddWithValue("@fileName", file.FileName);
						target.Parameters.AddWithValue("@contentType", file.ContentType);
						SqlParameter sqlParameter = target.Parameters.Add("@data", SqlDbType.VarBinary, -1);
						sqlParameter.Value = await StorageFiles.Store(file.Data, "transition-evidence", ct);
						target.Parameters.AddWithValue("@userId", userId);
						target.Parameters.AddWithValue("@actor", actorName);
						target.Parameters.AddWithValue("@latitude", Db(file.Latitude));
						target.Parameters.AddWithValue("@longitude", Db(file.Longitude));
						target.Parameters.AddWithValue("@accuracy", Db(file.AccuracyMeters));
						target.Parameters.AddWithValue("@locationCapturedAt", Db(file.LocationCapturedAt));
						await target.ExecuteNonQueryAsync(ct);
					}
					await using (SqlCommand target = new SqlCommand("UPDATE dbo.agency_transition_projects SET updated_at=SYSUTCDATETIME() WHERE id=@projectId;", connection, transaction))
					{
						target.Parameters.AddWithValue("@projectId", projectId);
						await target.ExecuteNonQueryAsync(ct);
					}
					await transaction.CommitAsync(ct);
					ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
				}
				goto end_IL_034a;
				IL_092c:
				ticketActionResult = ticketActionResult2;
				end_IL_034a:;
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> DeleteAgencyTransitionEvidence(Guid projectId, Guid stageId, Guid progressId, Guid evidenceId, string role, CancellationToken ct)
	{
		string department = DepartmentForRole(role);
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyTransitionEvidenceSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlCommand command = new SqlCommand("DELETE e FROM dbo.agency_transition_progress_evidence e INNER JOIN dbo.agency_transition_progress h ON h.id=e.progress_id INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE e.id=@evidenceId AND e.progress_id=@progressId AND e.project_id=@projectId AND e.stage_id=@stageId AND h.progress_status<>'COMPLETED' AND p.status<>'CANCELLED' AND (@admin=1 OR s.department=@department);", connection))
			{
				command.Parameters.AddWithValue("@evidenceId", evidenceId);
				command.Parameters.AddWithValue("@progressId", progressId);
				command.Parameters.AddWithValue("@projectId", projectId);
				command.Parameters.AddWithValue("@stageId", stageId);
				command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
				command.Parameters.AddWithValue("@department", department);
				ticketActionResult = ((await command.ExecuteNonQueryAsync(ct) == 1) ? new TicketActionResult(Ok: true, "OK", null) : new TicketActionResult(Ok: false, "FORBIDDEN", "La evidencia no existe, el registro está completado o no tienes permiso para eliminarla."));
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<(byte[] Data, string Mime)?> AgencyTransitionEvidencePhoto(Guid evidenceId, string role, CancellationToken ct)
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
		if (!flag)
		{
			return null;
		}
		(byte[] Data, string Mime)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureAgencyTransitionEvidenceSchema(connection, ct);
			(byte[] Data, string Mime)? tuple3;
			await using (SqlCommand command = new SqlCommand("SELECT photo_data,content_type FROM dbo.agency_transition_progress_evidence WHERE id=@id;", connection))
			{
				command.Parameters.AddWithValue("@id", evidenceId);
				(byte[] Data, string Mime)? tuple2;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct))
				{
					(byte[], string)? tuple = ((!(await reader.ReadAsync(ct))) ? (((byte[], string)?)null) : new (byte[], string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1))));
					tuple2 = tuple;
				}
				tuple3 = tuple2;
			}
			result = tuple3;
		}
		return result;
	}

	public async Task<TicketActionResult> DeleteAgencyTransitionProgress(Guid projectId, Guid stageId, Guid progressId, string userId, string role, string actorName, CancellationToken ct)
	{
		string department = DepartmentForRole(role);
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			(bool, bool, bool, bool, bool) tuple = await AgencyTransitionProgressCapabilities(connection, ct);
			if (!tuple.Item1)
			{
				result = new TicketActionResult(Ok: false, "NOT_FOUND", "El historial de avances todavía no está disponible.");
			}
			else
			{
				string text = (tuple.Item2 ? "AND h.entry_type='ADVANCE'" : "");
				string value = (tuple.Item4 ? "AND h.deleted_at IS NULL" : "");
				string cmdText = ((tuple.Item4 && tuple.Item5) ? $"UPDATE h SET deleted_at=SYSUTCDATETIME(),deleted_by_user_id=@userId,deleted_by_name=@actor{(tuple.Item3 ? ",updated_at=SYSUTCDATETIME()" : "")} FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' {text} {value} AND (@admin=1 OR s.department=@department);" : ("DELETE h FROM dbo.agency_transition_progress h INNER JOIN dbo.agency_transition_stages s ON s.id=h.stage_id INNER JOIN dbo.agency_transition_projects p ON p.id=h.project_id WHERE h.id=@progressId AND h.project_id=@projectId AND h.stage_id=@stageId AND p.status<>'CANCELLED' " + text + " AND (@admin=1 OR s.department=@department);"));
				TicketActionResult ticketActionResult;
				await using (SqlCommand command = new SqlCommand(cmdText, connection))
				{
					command.Parameters.AddWithValue("@userId", userId);
					command.Parameters.AddWithValue("@actor", actorName);
					command.Parameters.AddWithValue("@progressId", progressId);
					command.Parameters.AddWithValue("@projectId", projectId);
					command.Parameters.AddWithValue("@stageId", stageId);
					command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
					command.Parameters.AddWithValue("@department", department);
					ticketActionResult = ((await command.ExecuteNonQueryAsync(ct) == 1) ? new TicketActionResult(Ok: true, "OK", null) : new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el departamento propietario puede eliminar este registro."));
				}
				result = ticketActionResult;
			}
		}
		return result;
	}

	public async Task<TicketActionResult> DeleteAgencyTransitionStageContent(Guid projectId, Guid stageId, string userId, string role, string actorName, CancellationToken ct)
	{
		string department = DepartmentForRole(role);
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			(bool Exists, bool EntryType, bool UpdatedAt, bool DeletedAt, bool DeleteAudit) capabilities = await AgencyTransitionProgressCapabilities(connection, ct);
			TicketActionResult ticketActionResult2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				TicketActionResult ticketActionResult;
				await using (SqlCommand command = new SqlCommand("UPDATE s SET notes=NULL,updated_at=SYSUTCDATETIME() FROM dbo.agency_transition_stages s INNER JOIN dbo.agency_transition_projects p ON p.id=s.project_id WHERE s.id=@stageId AND p.id=@projectId AND p.status<>'CANCELLED' AND (@admin=1 OR s.department=@department); SELECT @@ROWCOUNT;", connection, transaction))
				{
					command.Parameters.AddWithValue("@stageId", stageId);
					command.Parameters.AddWithValue("@projectId", projectId);
					command.Parameters.AddWithValue("@admin", (role == "Administrator") ? 1 : 0);
					command.Parameters.AddWithValue("@department", department);
					if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Solo el departamento propietario puede eliminar el contenido de esta etapa.");
						goto IL_0494;
					}
				}
				if (capabilities.Exists)
				{
					string text = (capabilities.EntryType ? "AND entry_type<>'STAGE_COMPLETION'" : "");
					string cmdText = ((capabilities.DeletedAt && capabilities.DeleteAudit) ? $"UPDATE dbo.agency_transition_progress SET deleted_at=SYSUTCDATETIME(),deleted_by_user_id=@userId,deleted_by_name=@actor{(capabilities.UpdatedAt ? ",updated_at=SYSUTCDATETIME()" : "")} WHERE project_id=@projectId AND stage_id=@stageId {text} AND deleted_at IS NULL;" : ("DELETE FROM dbo.agency_transition_progress WHERE project_id=@projectId AND stage_id=@stageId " + text + ";"));
					await using SqlCommand command = new SqlCommand(cmdText, connection, transaction);
					command.Parameters.AddWithValue("@userId", userId);
					command.Parameters.AddWithValue("@actor", actorName);
					command.Parameters.AddWithValue("@projectId", projectId);
					command.Parameters.AddWithValue("@stageId", stageId);
					await command.ExecuteNonQueryAsync(ct);
				}
				await transaction.CommitAsync(ct);
				ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
				goto end_IL_021b;
				IL_0494:
				ticketActionResult2 = ticketActionResult;
				end_IL_021b:;
			}
			result = ticketActionResult2;
		}
		return result;
	}

	public async Task<TicketActionResult> DeleteAgencyTransition(Guid id, string role, CancellationToken ct)
	{
		if (role != "Administrator")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo administración puede eliminar un proceso.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			TicketActionResult ticketActionResult;
			await using (SqlCommand command = new SqlCommand("UPDATE dbo.agency_transition_projects SET status='CANCELLED',updated_at=SYSUTCDATETIME() WHERE id=@id AND status<>'CANCELLED';", connection))
			{
				command.Parameters.AddWithValue("@id", id);
				ticketActionResult = ((await command.ExecuteNonQueryAsync(ct) == 1) ? new TicketActionResult(Ok: true, "OK", null) : new TicketActionResult(Ok: false, "NOT_FOUND", "Proceso no encontrado."));
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<List<MaintenanceMovementRow>?> MaintenanceMovements(string userId, string role, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		string department = DepartmentForRole(role);
		bool flag = role != "Administrator";
		bool flag2 = flag;
		if (flag2)
		{
			string text = supportTeam;
			bool flag3 = ((text == "WAREHOUSE" || text == "WORKSHOP") ? true : false);
			flag2 = !flag3;
		}
		bool flag4 = flag2;
		if (flag4)
		{
			bool flag3;
			switch (department)
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
			flag4 = !flag3;
		}
		if (flag4)
		{
			return null;
		}
		List<MaintenanceMovementRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureRepairedInventorySchema(connection, ct);
			List<MaintenanceMovementRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT m.id,m.agency_id,COALESCE(a.codigo,'ALMACEN'),COALESCE(a.terminal,'Inventario central'),COALESCE(a.grupo,'Oficina principal'),m.ticket_id,m.department,m.technician_user_id,m.technician_name,m.movement_type,m.equipment_type,m.component_type,m.failure_cause,m.serial_number,m.quantity,m.notes,m.created_by_name,m.created_at,m.operational_area,m.document_number,m.qr_token,m.delivered_by_name,m.received_by_name,m.destination_name,CAST(NULL AS nvarchar(max)) signature_data,m.occurred_at,CAST(NULL AS nvarchar(256)) received_by_login,CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE m.operational_area='WAREHOUSE' AND repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at) THEN 1 ELSE 0 END AS bit) is_repaired FROM dbo.maintenance_movements m LEFT JOIN dbo.agencies a ON a.id=m.agency_id WHERE (@department='' OR m.department=@department) AND (@area='' OR m.operational_area=@area OR (@area='WORKSHOP' AND m.movement_type='TRANSFER_TO_WORKSHOP')) AND (@technicianOnly=0 OR m.technician_user_id=@userId) ORDER BY m.occurred_at DESC,m.created_at DESC;", connection))
			{
				command.Parameters.AddWithValue("@department", (role == "Administrator" || supportTeam == "WAREHOUSE") ? "" : department);
				SqlParameterCollection parameters = command.Parameters;
				string value;
				if (supportTeam == "WAREHOUSE")
				{
					value = "WAREHOUSE";
				}
				else
				{
					value = ((supportTeam == "WORKSHOP") ? "WORKSHOP" : "");
				}
				parameters.AddWithValue("@area", value);
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@technicianOnly", (role == "Technology" && supportTeam == "TECHNICIANS") ? 1 : 0);
				List<MaintenanceMovementRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<MaintenanceMovementRow> rows = new List<MaintenanceMovementRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new MaintenanceMovementRow(reader.GetGuid(0), reader.IsDBNull(1) ? ((Guid?)null) : new Guid?(Guid.Parse(reader.GetString(1))), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? ((Guid?)null) : new Guid?(reader.GetGuid(5)), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetString(12), reader.IsDBNull(13) ? null : reader.GetString(13), reader.GetInt32(14), reader.IsDBNull(15) ? null : reader.GetString(15), reader.GetString(16), reader.GetDateTime(17), reader.GetString(18), reader.GetString(19), reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21), reader.IsDBNull(22) ? null : reader.GetString(22), reader.IsDBNull(23) ? null : reader.GetString(23), null, reader.GetDateTime(25), reader.IsDBNull(26) ? null : reader.GetString(26), reader.GetBoolean(27)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<List<MaintenanceProductRow>> MaintenanceProducts(string role, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		string department = DepartmentForRole(role);
		List<MaintenanceProductRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			List<MaintenanceProductRow> list2;
			await using (SqlCommand command = new SqlCommand("SELECT id,department,scan_code,product_name,component_type,updated_at FROM dbo.maintenance_products WHERE (@department='' OR department=@department) ORDER BY product_name,scan_code;", connection))
			{
				command.Parameters.AddWithValue("@department", (role == "Administrator" || supportTeam == "WAREHOUSE") ? "" : department);
				List<MaintenanceProductRow> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<MaintenanceProductRow> rows = new List<MaintenanceProductRow>();
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new MaintenanceProductRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetDateTime(5)));
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<TicketActionResult> CreateMaintenanceMovement(MaintenanceMovementCreate body, string userId, string role, string? supportTeam, string actorName, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		string text = DepartmentForRole(role);
		string text2 = (body.Department ?? "").Trim().ToUpperInvariant();
		bool warehouse = supportTeam == "WAREHOUSE" || role == "Warehouse";
		bool flag = ((role == "Administrator") | warehouse) && string.Equals(body.OperationalArea?.Trim(), "WAREHOUSE", StringComparison.OrdinalIgnoreCase) && string.Equals(body.MovementType?.Trim(), "ENTRY", StringComparison.OrdinalIgnoreCase);
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = (((text2 != null && text2.Length == 0) || text2 == "WAREHOUSE") ? true : false);
			flag2 = flag3;
		}
		bool flag4 = flag2;
		if (flag4)
		{
			text2 = "WAREHOUSE";
		}
		bool flag5 = warehouse && !flag4;
		bool flag6 = flag5;
		if (flag6)
		{
			bool flag3;
			switch (text2)
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
			flag6 = !flag3;
		}
		if (flag6)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "El Almacén gestiona inventario de Tecnología, Servicios Generales y Recursos Humanos.");
		}
		string department = (((role == "Administrator") | warehouse) ? text2 : text);
		bool flag7 = !flag4;
		bool flag8 = flag7;
		if (flag8)
		{
			bool flag3;
			switch (department)
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
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Selecciona Tecnología, Servicios Generales o Recursos Humanos para registrar el mantenimiento.");
		}
		if (role != "Administrator" && !warehouse && !string.IsNullOrWhiteSpace(body.Department) && !string.Equals(body.Department, text, StringComparison.OrdinalIgnoreCase))
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo puedes registrar mantenimientos de tu departamento.");
		}
		string requestedArea = (body.OperationalArea ?? "").Trim().ToUpperInvariant();
		if (warehouse && requestedArea != "WAREHOUSE")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "La cuenta de Almacén solo puede registrar operaciones de Almacén.");
		}
		if (supportTeam == "WORKSHOP" && requestedArea != "WORKSHOP")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "La cuenta de Taller solo puede registrar operaciones de Taller.");
		}
		string movementType = body.MovementType.Trim().ToUpperInvariant();
		if (requestedArea == "WORKSHOP" && movementType != "TRANSFER_TO_WORKSHOP")
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Registra y gestiona equipos desde el nuevo formulario de Taller.");
		}
		string equipment = body.EquipmentType.Trim();
		string serialNumber = (string.IsNullOrWhiteSpace(body.SerialNumber) ? null : body.SerialNumber.Trim().ToUpperInvariant());
		bool technicianOnly = role == "Technology" && supportTeam == "TECHNICIANS";
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			TicketActionResult ticketActionResult;
			if (warehouse && movementType == "ENTRY")
			{
				await using (SqlCommand permission = new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@userId AND COALESCE(JSON_VALUE(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,'$.canLoadWarehouseInventory'),'true')='true';", connection))
				{
					permission.Parameters.AddWithValue("@userId", userId);
					if (Convert.ToInt32(await permission.ExecuteScalarAsync(ct)) != 1)
					{
						ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Tu cuenta no tiene permiso para cargar inventario.");
						goto IL_06f9;
					}
				}
				await using (SqlCommand existingProduct = new SqlCommand("SELECT COUNT(*) FROM dbo.warehouse_products WHERE is_active=1 AND LOWER(LTRIM(RTRIM(description)))=LOWER(LTRIM(RTRIM(@equipment)));", connection))
				{
					existingProduct.Parameters.AddWithValue("@equipment", equipment);
					if (Convert.ToInt32(await existingProduct.ExecuteScalarAsync(ct)) == 0)
					{
						await using SqlCommand createPermission = new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@userId AND (role='ADMINISTRATOR' OR COALESCE(JSON_VALUE(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,'$.canCreateWarehouseArticles'),CASE WHEN LOWER(LTRIM(RTRIM(email))) IN(N'diorly.abreu',N'diorly.abreu@grupotejeda.local',N'ana.rodriguez',N'ana.rodriguez@grupotejeda.local') THEN 'true' ELSE 'false' END)='true');", connection);
						createPermission.Parameters.AddWithValue("@userId", userId);
						if (Convert.ToInt32(await createPermission.ExecuteScalarAsync(ct)) != 1)
						{
							ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "Tu cuenta puede registrar entradas de artículos existentes, pero no crear artículos nuevos.");
							goto IL_06f9;
						}
					}
				}
				ticketActionResult = null;
			}
			string text3 = movementType;
			if ((text3 == "EXIT" || text3 == "NEW_DELIVERY") ? true : false)
			{
				await using (SqlCommand permission = new SqlCommand("SELECT COUNT(*) FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@recipient AND is_active=1 AND ((role='TECHNOLOGY' AND (support_team IS NULL OR support_team IN('CALL_CENTER','TECHNICAL_FAILURE','TECHNICIANS'))) OR role='GENERAL_SERVICES' OR role='GROUP_ADMIN');", connection))
				{
					permission.Parameters.AddWithValue("@recipient", body.TechnicianUserId ?? "");
					if (Convert.ToInt32(await permission.ExecuteScalarAsync(ct)) != 1)
					{
						ticketActionResult = new TicketActionResult(Ok: false, "FORBIDDEN", "El destinatario debe pertenecer a Tecnología, Servicios Generales o ser supervisor activo.");
						goto IL_08ed;
					}
				}
				ticketActionResult = null;
			}
			bool flag9 = requestedArea == "WAREHOUSE";
			bool flag10 = flag9;
			if (flag10)
			{
				bool flag3;
				switch (movementType)
				{
				case "ENTRY":
				case "REQUEST":
				case "TRANSFER_TO_WORKSHOP":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag10 = flag3;
			}
			if (flag10)
			{
				await EnsureWarehouseWithdrawalSchema(connection, ct);
			}
			await using (SqlTransaction movementTransaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				if (requestedArea == "WAREHOUSE" && movementType == "ENTRY")
				{
					await EnsureWarehouseEntryProduct(connection, movementTransaction, equipment, body.Quantity, body.OccurredAt ?? DateTime.UtcNow, ct, serialNumber != null);
				}
				bool flag11 = requestedArea == "WAREHOUSE";
				bool flag12 = flag11;
				if (flag12)
				{
					bool flag3;
					switch (movementType)
					{
					case "EXIT":
					case "NEW_DELIVERY":
					case "TRANSFER_TO_WORKSHOP":
					case "DISCHARGE":
						flag3 = true;
						break;
					default:
						flag3 = false;
						break;
					}
					flag12 = flag3;
				}
				if (!flag12)
				{
					goto IL_0d58;
				}
				string text4 = await WarehouseProtectReservedStock(connection, movementTransaction, department, equipment, serialNumber, body.Quantity, ct);
				if (text4 == null)
				{
					goto IL_0d58;
				}
				ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", text4);
				goto end_IL_0b64;
				IL_0d58:
				TicketActionResult ticketActionResult2;
				await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at) SELECT @id,@agency,@ticket,@department,@technicianId,@technicianName,@movement,@equipment,@component,@cause,@serial,@quantity,@notes,@userId,@actor,@area,@document,@qr,@delivered,@received,@destination,@signature,@occurred WHERE @agency IS NULL OR EXISTS(SELECT 1 FROM dbo.agencies WHERE id=@agency);", connection, movementTransaction))
				{
					command.Parameters.AddWithValue("@id", Guid.NewGuid());
					command.Parameters.Add("@agency", SqlDbType.Char, 36).Value = ((!body.AgencyId.HasValue) ? ((IConvertible)DBNull.Value) : ((IConvertible)body.AgencyId.Value.ToString()));
					command.Parameters.AddWithValue("@ticket", (!body.TicketId.HasValue) ? DBNull.Value : ((object)body.TicketId.Value));
					command.Parameters.AddWithValue("@department", department);
					command.Parameters.AddWithValue("@technicianId", Db(technicianOnly ? userId : body.TechnicianUserId));
					SqlParameterCollection parameters = command.Parameters;
					object value;
					if (technicianOnly)
					{
						value = actorName;
					}
					else
					{
						value = (string.IsNullOrWhiteSpace(body.TechnicianName) ? actorName : body.TechnicianName.Trim());
					}
					parameters.AddWithValue("@technicianName", value);
					command.Parameters.AddWithValue("@movement", body.MovementType.Trim().ToUpperInvariant());
					command.Parameters.AddWithValue("@equipment", body.EquipmentType.Trim());
					command.Parameters.AddWithValue("@component", Db(string.IsNullOrWhiteSpace(body.ComponentType) ? null : body.ComponentType.Trim()));
					command.Parameters.AddWithValue("@cause", body.FailureCause.Trim());
					command.Parameters.AddWithValue("@serial", Db(serialNumber));
					command.Parameters.AddWithValue("@quantity", body.Quantity);
					command.Parameters.AddWithValue("@notes", Db(string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim()));
					command.Parameters.AddWithValue("@userId", userId);
					command.Parameters.AddWithValue("@actor", actorName);
					command.Parameters.AddWithValue("@area", body.OperationalArea.Trim().ToUpperInvariant());
					command.Parameters.AddWithValue("@document", body.DocumentNumber.Trim().ToUpperInvariant());
					command.Parameters.AddWithValue("@qr", string.IsNullOrWhiteSpace(body.QrToken) ? ("REAL-MNT|" + body.DocumentNumber.Trim().ToUpperInvariant()) : body.QrToken.Trim());
					command.Parameters.AddWithValue("@delivered", Db(string.IsNullOrWhiteSpace(body.DeliveredByName) ? null : body.DeliveredByName.Trim()));
					command.Parameters.AddWithValue("@received", Db(string.IsNullOrWhiteSpace(body.ReceivedByName) ? null : body.ReceivedByName.Trim()));
					command.Parameters.AddWithValue("@destination", Db(string.IsNullOrWhiteSpace(body.DestinationName) ? null : body.DestinationName.Trim()));
					SqlParameterCollection parameters2 = command.Parameters;
					parameters2.AddWithValue("@signature", Db(await StorageFiles.StoreDataUrl(string.IsNullOrWhiteSpace(body.SignatureData) ? null : body.SignatureData, ct)));
					command.Parameters.AddWithValue("@occurred", body.OccurredAt ?? DateTime.UtcNow);
					int num;
					try
					{
						num = await command.ExecuteNonQueryAsync(ct);
					}
					catch (SqlException ex) when (((Func<bool>)delegate
					{
						// Could not convert BlockContainer to single expression
						int number = ex.Number;
						return (number == 2601 || number == 2627) ? true : false;
					}).Invoke())
					{
						ticketActionResult2 = new TicketActionResult(Ok: false, "CONFLICT", "Ese número de formulario o código QR ya fue registrado.");
						goto end_IL_0da0;
					}
					if (num != 1)
					{
						ticketActionResult2 = new TicketActionResult(Ok: false, "NOT_FOUND", "Agencia no encontrada.");
					}
					else
					{
						await movementTransaction.CommitAsync(ct);
						if (serialNumber != null)
						{
							try
							{
								await using SqlCommand permission = new SqlCommand("MERGE dbo.maintenance_products AS target USING(SELECT @department department,@scan scan_code,@product product_name,@component component_type,@actor created_by_name) AS source ON target.department=source.department AND target.scan_code=source.scan_code WHEN MATCHED THEN UPDATE SET product_name=source.product_name,component_type=source.component_type,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(department,scan_code,product_name,component_type,created_by_name) VALUES(source.department,source.scan_code,source.product_name,source.component_type,source.created_by_name);", connection);
								permission.Parameters.AddWithValue("@department", department);
								permission.Parameters.AddWithValue("@scan", serialNumber);
								permission.Parameters.AddWithValue("@product", body.EquipmentType.Trim());
								permission.Parameters.AddWithValue("@component", Db(string.IsNullOrWhiteSpace(body.ComponentType) ? null : body.ComponentType.Trim()));
								permission.Parameters.AddWithValue("@actor", actorName);
								await permission.ExecuteNonQueryAsync(ct);
							}
							catch (SqlException ex2)
							{
								Console.Error.WriteLine("Maintenance product catalog: " + ex2.Message);
							}
						}
						ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
					}
					end_IL_0da0:;
				}
				ticketActionResult = ticketActionResult2;
				end_IL_0b64:;
			}
			result = ticketActionResult;
			goto end_IL_04e3;
			IL_08ed:
			result = ticketActionResult;
			goto end_IL_04e3;
			IL_06f9:
			result = ticketActionResult;
			end_IL_04e3:;
		}
		return result;
	}

	public async Task<MaintenanceProcurementSnapshot?> MaintenanceProcurement(string role, string? supportTeam, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		string department = DepartmentForRole(role);
		bool flag = role != "Administrator";
		bool flag2 = flag;
		if (flag2)
		{
			string text = supportTeam;
			bool flag3 = ((text == "WAREHOUSE" || text == "WORKSHOP") ? true : false);
			flag2 = !flag3;
		}
		bool flag4 = flag2;
		if (flag4)
		{
			bool flag3;
			switch (department)
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
			flag4 = !flag3;
		}
		if (flag4)
		{
			return null;
		}
		MaintenanceProcurementSnapshot result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			bool flag5 = role == "Administrator" || supportTeam == "WORKSHOP";
			bool flag6 = supportTeam == "WAREHOUSE";
			MaintenanceProcurementSnapshot maintenanceProcurementSnapshot2;
			await using (SqlCommand command = new SqlCommand("SELECT id,name,tax_id,contact_name,phone,email,is_active,created_at FROM dbo.maintenance_suppliers WHERE is_active=1 ORDER BY name;\r\nSELECT id,requisition_number,department,product_name,quantity_requested,quantity_fulfilled,priority,status,requested_by_name,created_at,updated_at,notes FROM dbo.maintenance_requisitions WHERE (@all=1 OR (@warehouse=1 AND department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')) OR department=@department) ORDER BY CASE priority WHEN 'CRITICAL' THEN 1 WHEN 'HIGH' THEN 2 WHEN 'MEDIUM' THEN 3 ELSE 4 END,created_at DESC;\r\nSELECT p.id,p.order_number,p.supplier_id,s.name,p.requisition_id,p.department,p.product_name,p.quantity_ordered,p.quantity_received,p.unit_cost,p.status,p.expected_at,p.created_by_name,p.created_at,p.updated_at,p.notes FROM dbo.maintenance_purchase_orders p JOIN dbo.maintenance_suppliers s ON s.id=p.supplier_id WHERE (@all=1 OR (@warehouse=1 AND p.department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')) OR p.department=@department) ORDER BY p.created_at DESC;\r\nSELECT r.id,r.return_number,r.supplier_id,s.name,r.purchase_order_id,r.department,r.product_name,r.serial_number,r.quantity,r.reason,r.status,r.created_by_name,r.created_at,r.notes FROM dbo.maintenance_returns r LEFT JOIN dbo.maintenance_suppliers s ON s.id=r.supplier_id WHERE (@all=1 OR (@warehouse=1 AND r.department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')) OR r.department=@department) ORDER BY r.created_at DESC;", connection))
			{
				command.Parameters.AddWithValue("@all", flag5 ? 1 : 0);
				command.Parameters.AddWithValue("@warehouse", flag6 ? 1 : 0);
				command.Parameters.AddWithValue("@department", department);
				MaintenanceProcurementSnapshot maintenanceProcurementSnapshot;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<MaintenanceSupplierRow> suppliers = new List<MaintenanceSupplierRow>();
					while (await reader.ReadAsync(ct))
					{
						suppliers.Add(new MaintenanceSupplierRow(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6), reader.GetDateTime(7)));
					}
					await reader.NextResultAsync(ct);
					List<MaintenanceRequisitionRow> requisitions = new List<MaintenanceRequisitionRow>();
					while (await reader.ReadAsync(ct))
					{
						requisitions.Add(new MaintenanceRequisitionRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetDateTime(9), reader.GetDateTime(10), reader.IsDBNull(11) ? null : reader.GetString(11)));
					}
					await reader.NextResultAsync(ct);
					List<MaintenancePurchaseOrderRow> orders = new List<MaintenancePurchaseOrderRow>();
					while (await reader.ReadAsync(ct))
					{
						orders.Add(new MaintenancePurchaseOrderRow(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.IsDBNull(4) ? ((Guid?)null) : new Guid?(reader.GetGuid(4)), reader.GetString(5), reader.GetString(6), reader.GetInt32(7), reader.GetInt32(8), reader.GetDecimal(9), reader.GetString(10), reader.IsDBNull(11) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(11)), reader.GetString(12), reader.GetDateTime(13), reader.GetDateTime(14), reader.IsDBNull(15) ? null : reader.GetString(15)));
					}
					await reader.NextResultAsync(ct);
					List<MaintenanceReturnRow> returns = new List<MaintenanceReturnRow>();
					while (await reader.ReadAsync(ct))
					{
						returns.Add(new MaintenanceReturnRow(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? ((Guid?)null) : new Guid?(reader.GetGuid(2)), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? ((Guid?)null) : new Guid?(reader.GetGuid(4)), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetInt32(8), reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetDateTime(12), reader.IsDBNull(13) ? null : reader.GetString(13)));
					}
					maintenanceProcurementSnapshot = new MaintenanceProcurementSnapshot(suppliers, requisitions, orders, returns);
				}
				maintenanceProcurementSnapshot2 = maintenanceProcurementSnapshot;
			}
			result = maintenanceProcurementSnapshot2;
		}
		return result;
	}

	public async Task<TicketActionResult> CreateMaintenanceSupplier(MaintenanceSupplierCreate body, string role, string? supportTeam, string actorName, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		if (role != "Administrator" && supportTeam != "WAREHOUSE" && (!(role == "Technology") || supportTeam != null))
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o Administración de Tecnología puede registrar proveedores.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlCommand command = new SqlCommand("MERGE dbo.maintenance_suppliers AS target USING(SELECT @name name) AS source ON target.name=source.name WHEN MATCHED THEN UPDATE SET tax_id=@tax,contact_name=@contact,phone=@phone,email=@email,is_active=1,updated_at=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(name,tax_id,contact_name,phone,email,created_by_name) VALUES(@name,@tax,@contact,@phone,@email,@actor);", connection))
			{
				command.Parameters.AddWithValue("@name", body.Name.Trim());
				command.Parameters.AddWithValue("@tax", Db(body.TaxId?.Trim()));
				command.Parameters.AddWithValue("@contact", Db(body.ContactName?.Trim()));
				command.Parameters.AddWithValue("@phone", Db(body.Phone?.Trim()));
				command.Parameters.AddWithValue("@email", Db(body.Email?.Trim()));
				command.Parameters.AddWithValue("@actor", actorName);
				await command.ExecuteNonQueryAsync(ct);
				ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> CreateMaintenanceRequisition(MaintenanceRequisitionCreate body, string userId, string role, string? supportTeam, string actorName, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		string text = DepartmentForRole(role);
		string text2 = body.Department.Trim().ToUpperInvariant();
		bool flag = supportTeam == "WAREHOUSE";
		bool flag2 = flag;
		bool flag3 = flag2;
		bool flag4;
		if (flag3)
		{
			switch (text2)
			{
			case "TECHNOLOGY":
			case "GENERAL_SERVICES":
			case "HUMAN_RESOURCES":
				flag4 = true;
				break;
			default:
				flag4 = false;
				break;
			}
			flag3 = !flag4;
		}
		if (flag3)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "El almacén organiza requerimientos de Tecnología, Servicios Generales y Recursos Humanos.");
		}
		string department = (((role == "Administrator") | flag) ? text2 : text);
		switch (department)
		{
		case "TECHNOLOGY":
		case "GENERAL_SERVICES":
		case "HUMAN_RESOURCES":
			flag4 = true;
			break;
		default:
			flag4 = false;
			break;
		}
		if (!flag4)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Selecciona un departamento válido.");
		}
		if (role != "Administrator" && !flag && text2 != text)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo puedes solicitar artículos para tu departamento.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			string value = $"REQ-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 25).ToUpperInvariant();
			TicketActionResult ticketActionResult;
			await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.maintenance_requisitions(id,requisition_number,department,product_name,quantity_requested,priority,notes,requested_by_user_id,requested_by_name) VALUES(NEWID(),@number,@department,@product,@quantity,@priority,@notes,@userId,@actor);", connection))
			{
				command.Parameters.AddWithValue("@number", value);
				command.Parameters.AddWithValue("@department", department);
				command.Parameters.AddWithValue("@product", body.ProductName.Trim());
				command.Parameters.AddWithValue("@quantity", body.Quantity);
				command.Parameters.AddWithValue("@priority", body.Priority);
				command.Parameters.AddWithValue("@notes", Db(body.Notes?.Trim()));
				command.Parameters.AddWithValue("@userId", userId);
				command.Parameters.AddWithValue("@actor", actorName);
				await command.ExecuteNonQueryAsync(ct);
				ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
			}
			result = ticketActionResult;
		}
		return result;
	}

	public async Task<TicketActionResult> CreateMaintenancePurchaseOrder(MaintenancePurchaseOrderCreate body, string role, string? supportTeam, string actorName, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool flag = role == "Technology" && supportTeam == null;
		bool flag2 = supportTeam == "WAREHOUSE";
		if (role != "Administrator" && !flag2 && !flag)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o Administración de Tecnología puede emitir órdenes de compra.");
		}
		string department = body.Department.Trim().ToUpperInvariant();
		bool flag3;
		switch (department)
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
		bool flag4 = !flag3 || (flag && department != "TECHNOLOGY");
		bool flag5 = flag4;
		if (!flag5)
		{
			bool flag6 = flag2;
			bool flag7 = flag6;
			if (flag7)
			{
				bool flag8;
				switch (department)
				{
				case "TECHNOLOGY":
				case "GENERAL_SERVICES":
				case "HUMAN_RESOURCES":
					flag8 = true;
					break;
				default:
					flag8 = false;
					break;
				}
				flag7 = !flag8;
			}
			flag5 = flag7;
		}
		if (flag5)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo puedes gestionar órdenes de Tecnología, Servicios Generales o Recursos Humanos desde Almacén.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureDocumentSchema(connection, ct);
			string number = $"OC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 24).ToUpperInvariant();
			TicketActionResult ticketActionResult2;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				try
				{
					TicketActionResult ticketActionResult;
					await using (SqlCommand validation = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_suppliers WHERE id=@supplier AND is_active=1) AND (@requisition IS NULL OR EXISTS(SELECT 1 FROM dbo.maintenance_requisitions WHERE id=@requisition AND department=@department AND status IN('APPROVED','ORDERED','PARTIAL'))) THEN 1 ELSE 0 END;", connection, transaction))
					{
						validation.Parameters.AddWithValue("@supplier", body.SupplierId);
						validation.Parameters.AddWithValue("@requisition", (!body.RequisitionId.HasValue) ? DBNull.Value : ((object)body.RequisitionId.Value));
						validation.Parameters.AddWithValue("@department", department);
						if (Convert.ToInt32(await validation.ExecuteScalarAsync(ct)) != 1)
						{
							await transaction.RollbackAsync(ct);
							ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "Proveedor o requisición no disponible.");
							goto IL_064e;
						}
					}
					ticketActionResult = null;
					await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.maintenance_purchase_orders(id,order_number,supplier_id,requisition_id,department,product_name,quantity_ordered,unit_cost,status,expected_at,notes,created_by_name) VALUES(NEWID(),@number,@supplier,@requisition,@department,@product,@quantity,@cost,'ISSUED',@expected,@notes,@actor); UPDATE dbo.maintenance_requisitions SET status='ORDERED',updated_at=SYSUTCDATETIME() WHERE id=@requisition AND status='APPROVED';", connection, transaction))
					{
						command.Parameters.AddWithValue("@number", number);
						command.Parameters.AddWithValue("@supplier", body.SupplierId);
						command.Parameters.AddWithValue("@requisition", (!body.RequisitionId.HasValue) ? DBNull.Value : ((object)body.RequisitionId.Value));
						command.Parameters.AddWithValue("@department", department);
						command.Parameters.AddWithValue("@product", body.ProductName.Trim());
						command.Parameters.AddWithValue("@quantity", body.Quantity);
						command.Parameters.AddWithValue("@cost", body.UnitCost);
						command.Parameters.AddWithValue("@expected", (!body.ExpectedAt.HasValue) ? DBNull.Value : ((object)body.ExpectedAt.Value));
						command.Parameters.AddWithValue("@notes", Db(body.Notes?.Trim()));
						command.Parameters.AddWithValue("@actor", actorName);
						await command.ExecuteNonQueryAsync(ct);
						await transaction.CommitAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					}
					ticketActionResult2 = ticketActionResult;
					goto end_IL_03d3;
					IL_064e:
					ticketActionResult2 = ticketActionResult;
					end_IL_03d3:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult2;
		}
		return result;
	}

	public async Task<TicketActionResult> ReceiveMaintenancePurchaseOrder(Guid id, MaintenancePurchaseOrderReceive body, string userId, string role, string? supportTeam, string actorName, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool technologyManager = role == "Technology" && supportTeam == null;
		bool warehouse = supportTeam == "WAREHOUSE";
		if (role != "Administrator" && !warehouse && !technologyManager)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o Administración de Tecnología puede recibir mercancía.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			TicketActionResult ticketActionResult3;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				try
				{
					string department;
					string product;
					int ordered;
					int received;
					Guid? requisition;
					string supplier;
					TicketActionResult ticketActionResult2;
					await using (SqlCommand select = new SqlCommand("SELECT p.department,p.product_name,p.quantity_ordered,p.quantity_received,p.requisition_id,s.name FROM dbo.maintenance_purchase_orders p JOIN dbo.maintenance_suppliers s ON s.id=p.supplier_id WHERE p.id=@id AND p.status IN('ISSUED','PARTIAL') AND (@technologyManager=0 OR p.department='TECHNOLOGY') AND (@warehouse=0 OR p.department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));", connection, transaction))
					{
						select.Parameters.AddWithValue("@id", id);
						select.Parameters.AddWithValue("@technologyManager", technologyManager ? 1 : 0);
						select.Parameters.AddWithValue("@warehouse", warehouse ? 1 : 0);
						TicketActionResult ticketActionResult;
						await using (SqlDataReader reader = await select.ExecuteReaderAsync(ct))
						{
							if (!(await reader.ReadAsync(ct)))
							{
								await reader.DisposeAsync();
								await transaction.RollbackAsync(ct);
								ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "La orden no existe, ya fue recibida o no pertenece al alcance del almacén.");
								goto IL_06b9;
							}
							department = reader.GetString(0);
							product = reader.GetString(1);
							ordered = reader.GetInt32(2);
							received = reader.GetInt32(3);
							requisition = (reader.IsDBNull(4) ? ((Guid?)null) : new Guid?(reader.GetGuid(4)));
							supplier = reader.GetString(5);
						}
						goto end_IL_0317;
						IL_06b9:
						ticketActionResult2 = ticketActionResult;
						goto IL_0797;
						end_IL_0317:;
					}
					ticketActionResult2 = null;
					if (body.Quantity > ordered - received)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult3 = new TicketActionResult(Ok: false, "CONFLICT", $"Solo quedan {ordered - received} unidades pendientes en la orden.");
					}
					else
					{
						int num = received + body.Quantity;
						string value = ((num == ordered) ? "RECEIVED" : "PARTIAL");
						string value2 = $"REC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 25).ToUpperInvariant();
						await using (SqlCommand command = new SqlCommand("UPDATE dbo.maintenance_purchase_orders SET quantity_received=@received,status=@status,updated_at=SYSUTCDATETIME() WHERE id=@id; UPDATE dbo.maintenance_requisitions SET quantity_fulfilled=CASE WHEN quantity_fulfilled+@quantity>quantity_requested THEN quantity_requested ELSE quantity_fulfilled+@quantity END,status=CASE WHEN quantity_fulfilled+@quantity>=quantity_requested THEN 'FULFILLED' ELSE 'PARTIAL' END,updated_at=SYSUTCDATETIME() WHERE id=@requisition; INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at) VALUES(NEWID(),NULL,NULL,@department,NULL,@actor,'ENTRY',@product,NULL,'Recepción física contra orden de compra',@serial,@quantity,@notes,@userId,@actor,'WAREHOUSE',@document,CONCAT('REAL-MNT|',@document),@supplier,@receiver,'Almacén central',NULL,SYSUTCDATETIME());", connection, transaction))
						{
							command.Parameters.AddWithValue("@received", num);
							command.Parameters.AddWithValue("@status", value);
							command.Parameters.AddWithValue("@id", id);
							command.Parameters.AddWithValue("@quantity", body.Quantity);
							command.Parameters.AddWithValue("@requisition", (!requisition.HasValue) ? DBNull.Value : ((object)requisition.Value));
							command.Parameters.AddWithValue("@department", department);
							command.Parameters.AddWithValue("@actor", actorName);
							command.Parameters.AddWithValue("@product", product);
							command.Parameters.AddWithValue("@serial", body.SerialNumber.Trim().ToUpperInvariant());
							command.Parameters.AddWithValue("@notes", Db(body.Notes?.Trim()));
							command.Parameters.AddWithValue("@userId", userId);
							command.Parameters.AddWithValue("@document", value2);
							command.Parameters.AddWithValue("@supplier", supplier);
							command.Parameters.AddWithValue("@receiver", string.IsNullOrWhiteSpace(body.ReceivedByName) ? actorName : body.ReceivedByName.Trim());
							await command.ExecuteNonQueryAsync(ct);
							await transaction.CommitAsync(ct);
							ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
						}
						ticketActionResult3 = ticketActionResult2;
					}
					goto end_IL_02d0;
					IL_0797:
					ticketActionResult3 = ticketActionResult2;
					end_IL_02d0:;
				}
				catch
				{
					await transaction.RollbackAsync(ct);
					throw;
				}
			}
			result = ticketActionResult3;
		}
		return result;
	}

	public async Task<TicketActionResult> CreateMaintenanceReturn(MaintenanceReturnCreate body, string userId, string role, string? supportTeam, string actorName, CancellationToken ct)
	{
		if (DepartmentAccounts.IsSupportScope(role))
		{
			supportTeam = null;
		}
		bool flag = role == "Technology" && supportTeam == null;
		bool flag2 = supportTeam == "WAREHOUSE";
		if (role != "Administrator" && !flag2 && !flag)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o Administración de Tecnología puede registrar devoluciones.");
		}
		string department = body.Department.Trim().ToUpperInvariant();
		bool flag3;
		switch (department)
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
		bool flag4 = !flag3 || (flag && department != "TECHNOLOGY");
		bool flag5 = flag4;
		if (!flag5)
		{
			bool flag6 = flag2;
			bool flag7 = flag6;
			if (flag7)
			{
				bool flag8;
				switch (department)
				{
				case "TECHNOLOGY":
				case "GENERAL_SERVICES":
				case "HUMAN_RESOURCES":
					flag8 = true;
					break;
				default:
					flag8 = false;
					break;
				}
				flag7 = !flag8;
			}
			flag5 = flag7;
		}
		if (flag5)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo puedes gestionar devoluciones de Tecnología, Servicios Generales o Recursos Humanos desde Almacén.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			string number = $"DEV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 25).ToUpperInvariant();
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			TicketActionResult ticketActionResult;
			await using (SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				string text = await WarehouseProtectReservedStock(connection, transaction, department, body.ProductName.Trim(), body.SerialNumber.Trim().ToUpperInvariant(), body.Quantity, ct);
				if (text != null)
				{
					ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", text);
				}
				else
				{
					try
					{
						TicketActionResult ticketActionResult2;
						await using (SqlCommand command = new SqlCommand("INSERT INTO dbo.maintenance_returns(id,return_number,supplier_id,purchase_order_id,department,product_name,serial_number,quantity,reason,notes,created_by_name) VALUES(NEWID(),@number,@supplier,@order,@department,@product,@serial,@quantity,@reason,@notes,@actor); INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at) VALUES(NEWID(),NULL,NULL,@department,NULL,@actor,'EXIT',@product,NULL,@reason,@serial,@quantity,@notes,@userId,@actor,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@actor,COALESCE((SELECT name FROM dbo.maintenance_suppliers WHERE id=@supplier),'Proveedor / devolución'),'Devolución a proveedor',NULL,SYSUTCDATETIME());", connection, transaction))
						{
							command.Parameters.AddWithValue("@number", number);
							command.Parameters.AddWithValue("@supplier", (!body.SupplierId.HasValue) ? DBNull.Value : ((object)body.SupplierId.Value));
							command.Parameters.AddWithValue("@order", (!body.PurchaseOrderId.HasValue) ? DBNull.Value : ((object)body.PurchaseOrderId.Value));
							command.Parameters.AddWithValue("@department", department);
							command.Parameters.AddWithValue("@product", body.ProductName.Trim());
							command.Parameters.AddWithValue("@serial", body.SerialNumber.Trim().ToUpperInvariant());
							command.Parameters.AddWithValue("@quantity", body.Quantity);
							command.Parameters.AddWithValue("@reason", body.Reason.Trim());
							command.Parameters.AddWithValue("@notes", Db(body.Notes?.Trim()));
							command.Parameters.AddWithValue("@actor", actorName);
							command.Parameters.AddWithValue("@userId", userId);
							await command.ExecuteNonQueryAsync(ct);
							await transaction.CommitAsync(ct);
							ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
						}
						ticketActionResult = ticketActionResult2;
					}
					catch (SqlException ex) when (ex.Number == 547)
					{
						await transaction.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "NOT_FOUND", "La orden o el proveedor seleccionado ya no existe.");
					}
					catch
					{
						await transaction.RollbackAsync(ct);
						throw;
					}
				}
			}
			result = ticketActionResult;
		}
		return result;
	}

	internal static async Task EnsureTechnicianResolutionPermissions(SqlConnection connection, CancellationToken ct)
	{
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		try
		{
			await using (SqlCommand schema = new SqlCommand("DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='technician-resolution-permissions-v438',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;\nIF @lock<0 THROW 50001,'No se pudo verificar los permisos técnicos.',1;\nIF OBJECT_ID(N'dbo.technician_resolution_permission_backups',N'U') IS NULL\nCREATE TABLE dbo.technician_resolution_permission_backups(id bigint IDENTITY PRIMARY KEY,user_id nvarchar(80) NOT NULL,permissions_json nvarchar(max) NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", connection, tx))
			{
				await schema.ExecuteNonQueryAsync(ct);
			}
			List<(string Id, string Role, string? Team, string? Raw)> users = new List<(string, string, string, string)>();
			await using (SqlCommand schema = new SqlCommand("SELECT CONVERT(nvarchar(80),id),role,support_team,permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE role IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','DEPARTMENT')", connection, tx))
			{
				await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					users.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
				}
			}
			foreach (var item in users)
			{
				Dictionary<string, bool> dictionary;
				try
				{
					dictionary = JsonSerializer.Deserialize<Dictionary<string, bool>>(item.Raw ?? "{}") ?? new Dictionary<string, bool>();
				}
				catch (JsonException)
				{
					continue;
				}
				if (TechnicianResolutionPermissions.IsTechnician(item.Role, item.Team, dictionary) && (!dictionary.GetValueOrDefault("canResolveTickets") || !dictionary.GetValueOrDefault("canViewSupportDashboard")))
				{
					dictionary["canResolveTickets"] = true;
					dictionary["canViewSupportDashboard"] = true;
					await using SqlCommand schema = new SqlCommand("INSERT dbo.technician_resolution_permission_backups(user_id,permissions_json) VALUES(@id,@previous);\nUPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;", connection, tx);
					schema.Parameters.AddWithValue("@id", item.Id);
					schema.Parameters.AddWithValue("@previous", ((object)item.Raw) ?? ((object)DBNull.Value));
					schema.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(dictionary));
					await schema.ExecuteNonQueryAsync(ct);
				}
			}
			await tx.CommitAsync(ct);
		}
		catch
		{
			await tx.RollbackAsync(ct);
			throw;
		}
	}

	private static async Task EnsureUserAccessPresetSchema(SqlConnection connection, CancellationToken ct)
	{
		if (userAccessPresetSchemaReady)
		{
			return;
		}
		await UserAccessPresetSchemaLock.WaitAsync(ct);
		try
		{
			if (!userAccessPresetSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.user_access_presets',N'U') IS NULL\nCREATE TABLE dbo.user_access_presets(\n    department nvarchar(40) NOT NULL,\n    profile nvarchar(40) NOT NULL,\n    permissions_json nvarchar(max) NOT NULL,\n    updated_by nvarchar(100) NOT NULL,\n    updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),\n    CONSTRAINT PK_user_access_presets PRIMARY KEY(department,profile));", connection);
				await command.ExecuteNonQueryAsync(ct);
				userAccessPresetSchemaReady = true;
			}
		}
		finally
		{
			UserAccessPresetSchemaLock.Release();
		}
	}

	public async Task<List<UserAccessPreset>> UserAccessPresets(CancellationToken ct)
	{
		List<UserAccessPreset> result2;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureUserAccessPresetSchema(connection, ct);
			List<UserAccessPreset> list2;
			await using (SqlCommand command = new SqlCommand("SELECT department,profile,permissions_json FROM dbo.user_access_presets ORDER BY department,profile", connection))
			{
				List<UserAccessPreset> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					List<UserAccessPreset> result = new List<UserAccessPreset>();
					while (await reader.ReadAsync(ct))
					{
						result.Add(new UserAccessPreset(reader.GetString(0), reader.GetString(1), JsonSerializer.Deserialize<Dictionary<string, bool>>(reader.GetString(2)) ?? new Dictionary<string, bool>()));
					}
					list = result;
				}
				list2 = list;
			}
			result2 = list2;
		}
		return result2;
	}

	public async Task SaveUserAccessPreset(UserAccessPreset preset, string actor, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await EnsureUserAccessPresetSchema(connection, ct);
		await using SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		await using SqlCommand command = new SqlCommand("UPDATE dbo.user_access_presets WITH(UPDLOCK,HOLDLOCK)\nSET permissions_json=@permissions,updated_by=@actor,updated_at=SYSUTCDATETIME()\nWHERE department=@department AND profile=@profile;\nIF @@ROWCOUNT=0 INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by)\nVALUES(@department,@profile,@permissions,@actor);", connection, transaction);
		command.Parameters.AddWithValue("@department", preset.Department);
		command.Parameters.AddWithValue("@profile", preset.Profile);
		command.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(preset.Permissions));
		command.Parameters.AddWithValue("@actor", actor);
		await command.ExecuteNonQueryAsync(ct);
		await transaction.CommitAsync(ct);
	}

	private static bool IsWarehouseConsumable(string? name)
	{
		if (!string.IsNullOrWhiteSpace(name))
		{
			return WarehouseConsumables.Contains(name.Trim());
		}
		return false;
	}

	public async Task<string?> WarehouseRemoveInventory(DocumentActor actor, WarehouseInventoryRemovalInput input, CancellationToken ct)
	{
		if (!actor.CanDeleteInventory)
		{
			return "Solo Diorly Abreu y Bryan Arias pueden eliminar existencias.";
		}
		bool flag;
		switch (input.Department)
		{
		case "TECHNOLOGY":
		case "GENERAL_SERVICES":
		case "HUMAN_RESOURCES":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag || string.IsNullOrWhiteSpace(input.Equipment) || input.Equipment.Length > 100 || input.ExpectedQuantity < 1)
		{
			return "Equipo o cantidad no válido.";
		}
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			await EnsureRepairedInventorySchema(connection, ct);
			string text2;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				await using (SqlCommand stockLock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", connection, tx))
				{
					await stockLock.ExecuteScalarAsync(ct);
				}
				string text;
				await using (SqlCommand stockLock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE department=@department AND LOWER(equipment_name)=LOWER(@equipment) AND status='PENDING_PICKUP'", connection, tx))
				{
					stockLock.Parameters.AddWithValue("@department", input.Department);
					stockLock.Parameters.AddWithValue("@equipment", input.Equipment.Trim());
					if (Convert.ToInt64(await stockLock.ExecuteScalarAsync(ct)) > 0)
					{
						text = "Este equipo tiene unidades pendientes de retiro. No se puede eliminar mientras estén reservadas.";
						goto IL_06b9;
					}
				}
				List<(string Serial, int Quantity)> balances = new List<(string, int)>();
				await using (SqlCommand stockLock = new SqlCommand("SELECT serial_number,SUM(CASE WHEN movement_type IN('ENTRY','DAMAGED_RETURN') THEN quantity WHEN movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE department=@department AND operational_area='WAREHOUSE' AND LOWER(LTRIM(RTRIM(equipment_type)))=LOWER(@equipment) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements r ON r.id=rr.movement_id WHERE r.department=m.department AND UPPER(LTRIM(RTRIM(r.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND r.occurred_at<=m.occurred_at) GROUP BY serial_number", connection, tx))
				{
					stockLock.Parameters.AddWithValue("@department", input.Department);
					stockLock.Parameters.AddWithValue("@equipment", input.Equipment.Trim());
					await using SqlDataReader reader = await stockLock.ExecuteReaderAsync(ct);
					while (await reader.ReadAsync(ct))
					{
						balances.Add((reader.IsDBNull(0) ? "" : reader.GetString(0), reader.GetInt32(1)));
					}
				}
				if (balances.Sum(((string Serial, int Quantity) row) => row.Quantity) != input.ExpectedQuantity)
				{
					text2 = "Las existencias cambiaron. Actualiza el inventario antes de eliminar.";
				}
				else if (balances.Any(((string Serial, int Quantity) row) => row.Quantity < 0))
				{
					text2 = "Este equipo necesita una revisión de movimientos antes de eliminar sus existencias.";
				}
				else
				{
					foreach (var item in balances.Where(((string Serial, int Quantity) row) => row.Quantity > 0))
					{
						string value = $"DEL-ALM-{Guid.NewGuid():N}";
						await using SqlCommand stockLock = new SqlCommand("INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at)\r\nVALUES(NEWID(),NULL,NULL,@department,NULL,@actorName,'DISCHARGE',@equipment,NULL,'Eliminación de existencias desde inventario',@serial,@quantity,@reason,@actorId,@actorName,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@actorName,@actorName,'Almacén central',NULL,SYSUTCDATETIME())", connection, tx);
						stockLock.Parameters.AddWithValue("@department", input.Department);
						stockLock.Parameters.AddWithValue("@equipment", input.Equipment.Trim());
						stockLock.Parameters.AddWithValue("@serial", item.Serial);
						stockLock.Parameters.AddWithValue("@quantity", item.Quantity);
						stockLock.Parameters.AddWithValue("@actorId", actor.Id);
						stockLock.Parameters.AddWithValue("@actorName", actor.Name);
						stockLock.Parameters.AddWithValue("@number", value);
						stockLock.Parameters.AddWithValue("@reason", string.IsNullOrWhiteSpace(input.Reason) ? "Eliminado desde inventario; se conserva el historial." : input.Reason.Trim().Substring(0, Math.Min(input.Reason.Trim().Length, 500)));
						await stockLock.ExecuteNonQueryAsync(ct);
					}
					await tx.CommitAsync(ct);
					text2 = null;
				}
				goto end_IL_03b6;
				IL_06b9:
				text2 = text;
				end_IL_03b6:;
			}
			result = text2;
		}
		return result;
	}

	private async Task<InstitutionalFile> WarehouseRecoverLegacyRequest(SqlConnection connection, InstitutionalFile document, string originalJson, CancellationToken ct)
	{
		if ((object)document.WarehouseRequest != null || document.Area != "communications" || document.Channel != "requirements" || document.RecipientDepartment != "Almacén")
		{
			return document;
		}
		string number = Path.GetFileNameWithoutExtension(document.OriginalFileName);
		if (!number.StartsWith("SOL-ALM-", StringComparison.Ordinal) || number.Length > 40)
		{
			return document;
		}
		await EnsureMaintenanceSchema(connection, ct);
		InstitutionalFile result2;
		await using (SqlCommand find = new SqlCommand("SELECT m.agency_id,m.technician_user_id,m.technician_name,m.quantity,m.notes,a.codigo,a.terminal,a.grupo FROM dbo.maintenance_movements m JOIN dbo.agencies a ON a.id=m.agency_id WHERE m.document_number=@number AND m.movement_type='REQUEST' AND m.operational_area='WAREHOUSE' AND m.department=@department", connection))
		{
			find.Parameters.AddWithValue("@number", number);
			find.Parameters.AddWithValue("@department", WarehouseDepartmentCode(document.Department) ?? "");
			WarehouseRequestInfo recovered = null;
			InstitutionalFile institutionalFile;
			await using (SqlDataReader reader = await find.ExecuteReaderAsync(ct))
			{
				if (!(await reader.ReadAsync(ct)) || reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(4))
				{
					institutionalFile = document;
					goto IL_064c;
				}
				List<WarehouseRequestLine> list = new List<WarehouseRequestLine>();
				string[] array = reader.GetString(4).Split('\n');
				int num = 0;
				while (true)
				{
					if (num < array.Length)
					{
						string text = array[num];
						string[] array2 = text.Trim().Split(" · ");
						if (array2.Length < 2 || !int.TryParse(array2[1], out var result))
						{
							goto IL_049c;
						}
						Match match = Regex.Match(array2[0], "^\\d+\\. (Equipo|Componente|Devolución): (.+)$");
						bool flag = !match.Success;
						bool flag2 = flag;
						if (!flag2)
						{
							bool flag3 = ((result < 1 || result > 10000) ? true : false);
							flag2 = flag3;
						}
						if (flag2 || match.Groups[2].Value.Length > 100)
						{
							goto IL_049c;
						}
						string value = match.Groups[1].Value;
						string text2;
						if (value == "Equipo")
						{
							text2 = "EQUIPMENT";
						}
						else
						{
							text2 = ((!(value == "Componente")) ? "RETURN" : "COMPONENT");
						}
						string text3 = text2;
						string text4 = array2.Skip(2).FirstOrDefault((string part) => part.StartsWith("Serial: ", StringComparison.Ordinal))?.Substring(8);
						if (!(text3 == "RETURN") || !string.IsNullOrWhiteSpace(text4))
						{
							list.Add(new WarehouseRequestLine(text3, match.Groups[2].Value, result, text4, null));
							goto IL_049c;
						}
						institutionalFile = document;
					}
					else
					{
						int count = list.Count;
						bool flag3 = ((count < 1 || count > 30) ? true : false);
						if (!flag3 && list.Sum((WarehouseRequestLine line) => line.Quantity) == reader.GetInt32(3))
						{
							recovered = new WarehouseRequestInfo(number, Guid.Parse(reader.GetString(0)), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(1), reader.GetString(2), "MEDIUM", list);
							break;
						}
						institutionalFile = document;
					}
					goto IL_064c;
					IL_049c:
					num++;
				}
			}
			institutionalFile = null;
			document.WarehouseRequest = recovered;
			await using (SqlCommand save = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@metadata WHERE id=@id AND metadata=@original", connection))
			{
				save.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(document, InstitutionalJson));
				save.Parameters.AddWithValue("@id", document.Id);
				save.Parameters.AddWithValue("@original", originalJson);
				if (await save.ExecuteNonQueryAsync(ct) == 1)
				{
					institutionalFile = document;
				}
				else
				{
					InstitutionalFile institutionalFile3;
					await using (SqlCommand latest = new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id", connection))
					{
						latest.Parameters.AddWithValue("@id", document.Id);
						InstitutionalFile institutionalFile2 = ((!(await latest.ExecuteScalarAsync(ct) is string json)) ? document : JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson));
						institutionalFile2.Id = document.Id;
						institutionalFile3 = institutionalFile2;
					}
					institutionalFile = institutionalFile3;
				}
			}
			result2 = institutionalFile;
			goto end_IL_0185;
			IL_064c:
			result2 = institutionalFile;
			end_IL_0185:;
		}
		return result2;
	}

	private static object WarehouseEmptyAnalytics()
	{
		return new
		{
			requests = 0,
			units = 0,
			articles = Array.Empty<object>(),
			departments = Array.Empty<object>(),
			agencies = Array.Empty<object>(),
			groups = Array.Empty<object>(),
			terminals = Array.Empty<object>()
		};
	}

	private static async Task EnsureWarehouseFulfillmentSchema(SqlConnection connection, CancellationToken ct)
	{
		if (warehouseFulfillmentSchemaReady)
		{
			return;
		}
		await WarehouseFulfillmentSchemaLock.WaitAsync(ct);
		try
		{
			if (warehouseFulfillmentSchemaReady)
			{
				return;
			}
			await using SqlCommand command = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department' AND (definition NOT LIKE '%WAREHOUSE%' OR definition NOT LIKE '%ADMINISTRATIVE%' OR definition NOT LIKE '%TREASURY%' OR definition NOT LIKE '%ACCOUNTING%' OR definition NOT LIKE '%GENERAL_AUDIT%' OR definition NOT LIKE '%VAULT%' OR definition NOT LIKE '%CASHIER%'))\r\n    ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_department;\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_department')\r\n    ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_department CHECK(department IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','WAREHOUSE','ADMINISTRATIVE','WORKSHOP','COMMUNICATION','COMMERCIALIZATION','SUPERVISION','TECHNICAL_FAILURE','CALL_CENTER','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER'));\r\nIF OBJECT_ID(N'dbo.warehouse_equipment_allocations',N'U') IS NULL\r\nBEGIN\r\n  CREATE TABLE dbo.warehouse_equipment_allocations(\r\n    id bigint IDENTITY PRIMARY KEY,\r\n    document_id bigint NOT NULL REFERENCES dbo.institutional_files(id),\r\n    line_index int NOT NULL,\r\n    department varchar(40) NOT NULL,\r\n    equipment_name nvarchar(100) NOT NULL,\r\n    serial_number nvarchar(120) NOT NULL,\r\n    quantity int NOT NULL,\r\n    status varchar(20) NOT NULL DEFAULT('PENDING_PICKUP'),\r\n    delivery_number varchar(40) NULL,\r\n    delivered_by nvarchar(160) NULL,\r\n    received_by nvarchar(160) NULL,\r\n    withdrawn_at datetime2(3) NULL,\r\n    CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'))\r\n  );\r\n  CREATE INDEX IX_warehouse_allocations_document ON dbo.warehouse_equipment_allocations(document_id,status);\r\n  CREATE INDEX IX_warehouse_allocations_stock ON dbo.warehouse_equipment_allocations(department,equipment_name,status);\r\n  CREATE UNIQUE INDEX UX_warehouse_pending_serial ON dbo.warehouse_equipment_allocations(department,serial_number) WHERE status='PENDING_PICKUP';\r\nEND;\r\nIF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.warehouse_equipment_allocations') AND name='CK_warehouse_allocation_status' AND definition NOT LIKE '%CANCELLED%')\r\nBEGIN\r\n  ALTER TABLE dbo.warehouse_equipment_allocations DROP CONSTRAINT CK_warehouse_allocation_status;\r\n  ALTER TABLE dbo.warehouse_equipment_allocations WITH CHECK ADD CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'));\r\nEND;\r\nIF COL_LENGTH('dbo.warehouse_equipment_allocations','product_id') IS NULL\r\n  ALTER TABLE dbo.warehouse_equipment_allocations ADD product_id nvarchar(40) NULL;\r\nIF COL_LENGTH('dbo.warehouse_equipment_allocations','equipment_name') IS NOT NULL\r\n  ALTER TABLE dbo.warehouse_equipment_allocations ALTER COLUMN equipment_name nvarchar(240) NOT NULL;\r\nIF COL_LENGTH('dbo.maintenance_movements','equipment_type') IS NOT NULL\r\n  ALTER TABLE dbo.maintenance_movements ALTER COLUMN equipment_type nvarchar(240) NOT NULL;\r\nIF OBJECT_ID(N'dbo.warehouse_products',N'U') IS NULL\r\nBEGIN\r\n  CREATE TABLE dbo.warehouse_products(\r\n    product_id nvarchar(40) NOT NULL PRIMARY KEY,\r\n    description nvarchar(240) NOT NULL,\r\n    is_product bit NOT NULL,\r\n    warehouse nvarchar(80) NOT NULL,\r\n    official_stock int NOT NULL,\r\n    unit_id nvarchar(40) NOT NULL,\r\n    division nvarchar(120) NOT NULL,\r\n    product_group nvarchar(120) NOT NULL,\r\n    last_purchase_date date NULL,\r\n    tracks_serial bit NOT NULL,\r\n    catalog_version nvarchar(40) NOT NULL,\r\n    imported_at datetime2(3) NOT NULL,\r\n    is_active bit NOT NULL DEFAULT(1)\r\n  );\r\n  CREATE INDEX IX_warehouse_products_description ON dbo.warehouse_products(description,is_active);\r\nEND;", connection);
			await command.ExecuteNonQueryAsync(ct);
			await EnsureOfficialWarehouseCatalog(connection, ct);
			string path = Path.Combine(AppContext.BaseDirectory, "Data", "warehouse-products-2026-09-28.json");
			if (File.Exists(path))
			{
				WarehouseCatalogImport warehouseCatalogImport = JsonSerializer.Deserialize<WarehouseCatalogImport>(await File.ReadAllTextAsync(path, ct), new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});
				if ((object)warehouseCatalogImport == null || warehouseCatalogImport.Version != "2026-09-28-inventario-real-172" || warehouseCatalogImport.Products.Count != 172)
				{
					throw new InvalidOperationException("El inventario actualizado no está completo.");
				}
				await ApplyWarehouseCatalogSnapshot(connection, warehouseCatalogImport, ct);
			}
			string path2 = Path.Combine(AppContext.BaseDirectory, "Data", "warehouse-products-2026-10-27.json");
			if (File.Exists(path2))
			{
				WarehouseCatalogImport warehouseCatalogImport2 = JsonSerializer.Deserialize<WarehouseCatalogImport>(await File.ReadAllTextAsync(path2, ct), new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});
				if ((object)warehouseCatalogImport2 == null || warehouseCatalogImport2.Version != "2026-10-27-inventario-corregido-134" || warehouseCatalogImport2.Products.Count != 134)
				{
					throw new InvalidOperationException("El inventario corregido no está completo.");
				}
				await ApplyWarehouseCatalogSnapshot(connection, warehouseCatalogImport2, ct);
			}
			warehouseFulfillmentSchemaReady = true;
		}
		finally
		{
			WarehouseFulfillmentSchemaLock.Release();
		}
	}

	private static async Task EnsureOfficialWarehouseCatalog(SqlConnection connection, CancellationToken ct)
	{
		string path = Path.Combine(AppContext.BaseDirectory, "Data", "warehouse-products-2026-09-17.json");
		if (!File.Exists(path))
		{
			return;
		}
		WarehouseCatalogImport catalog = JsonSerializer.Deserialize<WarehouseCatalogImport>(await File.ReadAllTextAsync(path, ct), new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true
		});
		if (catalog?.Products == null || catalog.Products.Count == 0)
		{
			return;
		}
		await using (SqlCommand current = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.warehouse_products", connection))
		{
			if (Convert.ToInt64(await current.ExecuteScalarAsync(ct)) > 0)
			{
				return;
			}
		}
		await ReplaceWarehouseCatalog(connection, null, catalog, ct);
	}

	private static bool CatalogConsumable(string group, bool tracksSerial)
	{
		if (!tracksSerial)
		{
			if (!group.StartsWith("Materiales", StringComparison.OrdinalIgnoreCase))
			{
				return group.Equals("Vasos", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}

	internal static async Task ApplyWarehouseCatalogSnapshot(SqlConnection connection, WarehouseCatalogImport catalog, CancellationToken ct)
	{
		if (catalog.Products.Count == 0 || catalog.Products.Select((WarehouseCatalogProduct p) => p.IdProducto).Distinct(StringComparer.OrdinalIgnoreCase).Count() != catalog.Products.Count || catalog.Products.Any((WarehouseCatalogProduct p) => p.Existencia < 0))
		{
			throw new InvalidOperationException("Inventario actualizado inválido.");
		}
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		await using (SqlCommand gate = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='WarehouseCatalogSnapshot',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; IF @result<0 THROW 50000,'No se pudo bloquear la actualización de inventario',1;", connection, tx))
		{
			await gate.ExecuteNonQueryAsync(ct);
		}
		await using (SqlCommand gate = new SqlCommand("IF OBJECT_ID('dbo.warehouse_catalog_snapshots','U') IS NULL CREATE TABLE dbo.warehouse_catalog_snapshots(version nvarchar(120) NOT NULL PRIMARY KEY,source_file nvarchar(240),applied_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),previous_catalog nvarchar(max) NOT NULL,applied_catalog nvarchar(max) NOT NULL);", connection, tx))
		{
			await gate.ExecuteNonQueryAsync(ct);
		}
		await using (SqlCommand gate = new SqlCommand("SELECT COUNT(*) FROM dbo.warehouse_catalog_snapshots WITH(UPDLOCK,HOLDLOCK) WHERE version=@version", connection, tx))
		{
			gate.Parameters.AddWithValue("@version", catalog.Version);
			if (Convert.ToInt32(await gate.ExecuteScalarAsync(ct)) > 0)
			{
				await tx.CommitAsync(ct);
				return;
			}
		}
		await using (SqlCommand gate = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", connection, tx))
		{
			await gate.ExecuteScalarAsync(ct);
		}
		await using (SqlCommand gate = new SqlCommand("INSERT dbo.warehouse_catalog_snapshots(version,source_file,previous_catalog,applied_catalog) SELECT @version,@source,(SELECT * FROM dbo.warehouse_products FOR JSON PATH),@catalog", connection, tx))
		{
			gate.Parameters.AddWithValue("@version", catalog.Version);
			gate.Parameters.AddWithValue("@source", catalog.SourceFile ?? "");
			gate.Parameters.AddWithValue("@catalog", JsonSerializer.Serialize(catalog));
			await gate.ExecuteNonQueryAsync(ct);
		}
		await ReplaceWarehouseCatalog(connection, tx, catalog, ct);
		await tx.CommitAsync(ct);
	}

	private static async Task ReplaceWarehouseCatalog(SqlConnection connection, SqlTransaction? transaction, WarehouseCatalogImport catalog, CancellationToken ct)
	{
		string value = JsonSerializer.Serialize(catalog.Products);
		await using SqlCommand command = new SqlCommand("DECLARE @now datetime2(3)=SYSUTCDATETIME();\r\nUPDATE dbo.warehouse_products SET is_active=0;\r\nMERGE dbo.warehouse_products AS target\r\nUSING (\r\n  SELECT idProducto,descripcion,producto,almacen,existencia,idUnidad,division,grupo,TRY_CONVERT(date,fechaUltimaCompra) fechaUltimaCompra,seriales\r\n  FROM OPENJSON(@products) WITH(\r\n    idProducto nvarchar(40) '$.IdProducto', descripcion nvarchar(240) '$.Descripcion', producto bit '$.Producto', almacen nvarchar(80) '$.Almacen',\r\n    existencia int '$.Existencia', idUnidad nvarchar(40) '$.IdUnidad', division nvarchar(120) '$.Division', grupo nvarchar(120) '$.Grupo',\r\n    fechaUltimaCompra nvarchar(30) '$.FechaUltimaCompra', seriales bit '$.Seriales'\r\n  )\r\n) AS source ON target.product_id=source.idProducto\r\nWHEN MATCHED THEN UPDATE SET description=source.descripcion,is_product=source.producto,warehouse=source.almacen,official_stock=source.existencia,unit_id=source.idUnidad,division=source.division,product_group=source.grupo,last_purchase_date=source.fechaUltimaCompra,tracks_serial=source.seriales,catalog_version=@version,imported_at=@now,is_active=1\r\nWHEN NOT MATCHED THEN INSERT(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,last_purchase_date,tracks_serial,catalog_version,imported_at,is_active)\r\n  VALUES(source.idProducto,source.descripcion,source.producto,source.almacen,source.existencia,source.idUnidad,source.division,source.grupo,source.fechaUltimaCompra,source.seriales,@version,@now,1);", connection, transaction);
		command.Parameters.AddWithValue("@products", value);
		command.Parameters.AddWithValue("@version", catalog.Version);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<string?> WarehouseReplaceCatalog(DocumentActor actor, WarehouseCatalogImport input, CancellationToken ct)
	{
		if (!actor.Manager || (actor.Team != "WAREHOUSE" && actor.Role != "Administrator"))
		{
			return "Solo Almacén o Administración puede reemplazar el catálogo oficial.";
		}
		bool flag = input.Products == null;
		bool flag2 = flag;
		if (!flag2)
		{
			int count = input.Products.Count;
			bool flag3 = ((count < 1 || count > 5000) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return "El catálogo debe contener entre 1 y 5,000 productos.";
		}
		if (input.Products.Any((WarehouseCatalogProduct item) => string.IsNullOrWhiteSpace(item.IdProducto) || item.IdProducto.Length > 40 || string.IsNullOrWhiteSpace(item.Descripcion) || item.Descripcion.Length > 240 || item.Existencia < -1073741824))
		{
			return "Revisa los códigos, descripciones y existencias del archivo.";
		}
		if (input.Products.Select((WarehouseCatalogProduct item) => item.IdProducto.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != input.Products.Count)
		{
			return "El archivo contiene IdProducto duplicados.";
		}
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			string text;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				await ReplaceWarehouseCatalog(connection, tx, input with
				{
					Version = (string.IsNullOrWhiteSpace(input.Version) ? DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss") : input.Version.Trim())
				}, ct);
				await tx.CommitAsync(ct);
				text = null;
			}
			result = text;
		}
		return result;
	}

	private static string? WarehouseDepartmentCode(string name)
	{
		return DepartmentAccounts.Names.FirstOrDefault((KeyValuePair<string, string> entry) => entry.Value == name).Key;
	}

	public async Task<WarehouseRequestInfo?> WarehouseCanonicalRequest(WarehouseRequestInfo request, DocumentActor actor, string sourceDepartment, CancellationToken ct)
	{
		WeekendLoanPeriod weekendLoan = request.WeekendLoan;
		if ((object)weekendLoan != null && (!WeekendLoans.Requester(actor) || !WeekendLoans.Valid(weekendLoan) || request.DestinationType != "DEPARTMENT" || request.AgencyId != Guid.Empty || request.TicketId.HasValue || request.FailureCategory != null))
		{
			return null;
		}
		string department = WarehouseDepartmentCode(sourceDepartment);
		if (department == null)
		{
			return null;
		}
		bool flag;
		switch (request.DestinationType)
		{
		case null:
		case "AGENCY":
		case "GROUP":
		case "DEPARTMENT":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return null;
		}
		bool flag2 = actor.Role == "GroupAdministrator";
		bool flag3 = flag2;
		if (flag3)
		{
			bool flag4 = request.AgencyId == Guid.Empty;
			bool flag5 = flag4;
			if (!flag5)
			{
				string destinationType = request.DestinationType;
				flag = ((destinationType == null || destinationType == "AGENCY") ? true : false);
				flag5 = !flag;
			}
			bool flag6 = flag5;
			if (!flag6)
			{
				flag6 = !(await WarehouseRequestAgencyScope(actor, ct)).ContainsKey(request.AgencyId);
			}
			flag3 = flag6;
		}
		if (flag3)
		{
			return null;
		}
		List<WarehouseInventoryItem> inventory = await WarehouseAvailableInventory(actor, ct);
		foreach (WarehouseRequestLine requested in request.Lines.Where((WarehouseRequestLine line) => line.Kind != "RETURN"))
		{
			WarehouseInventoryItem warehouseInventoryItem = ((!string.IsNullOrWhiteSpace(requested.ProductId)) ? inventory.FirstOrDefault((WarehouseInventoryItem item) => item.ProductId.Equals(requested.ProductId.Trim(), StringComparison.OrdinalIgnoreCase)) : inventory.FirstOrDefault((WarehouseInventoryItem item) => item.Name.Equals(requested.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
			if (requested.Quantity < 1 || ((object)warehouseInventoryItem == null && !string.IsNullOrWhiteSpace(requested.ProductId)))
			{
				return null;
			}
		}
		if (request.DestinationType == "GROUP" && request.AgencyId == Guid.Empty)
		{
			if (!actor.Allowed || (!actor.Manager && sourceDepartment != actor.Department) || request.TicketId.HasValue || request.FailureCategory != null)
			{
				return null;
			}
			string text = (await WarehouseRequestGroups(actor, ct)).FirstOrDefault((string item) => string.Equals(item, request.AgencyGroup?.Trim(), StringComparison.OrdinalIgnoreCase));
			if (text == null)
			{
				return null;
			}
			return request with
			{
				AgencyCode = "GRUPO",
				AgencyName = "Grupo " + text,
				AgencyGroup = text,
				InstallerId = "",
				InstallerName = "",
				TicketNumber = null,
				Lines = request.Lines.Select((WarehouseRequestLine line) =>
				{
					WarehouseInventoryItem warehouseInventoryItem2 = inventory.FirstOrDefault((WarehouseInventoryItem item) => string.IsNullOrWhiteSpace(line.ProductId) ? item.Name.Equals(line.Name.Trim(), StringComparison.OrdinalIgnoreCase) : item.ProductId.Equals(line.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
					return line with
					{
						Kind = "EQUIPMENT",
						ProductId = warehouseInventoryItem2?.ProductId,
						Name = (warehouseInventoryItem2?.Name ?? line.Name.Trim()),
						Serial = null
					};
				}).ToList()
			};
		}
		if (request.AgencyId == Guid.Empty)
		{
			if (request.DestinationType == "AGENCY" || !actor.Allowed || (!actor.Manager && sourceDepartment != actor.Department) || request.TicketId.HasValue)
			{
				return null;
			}
			return request with
			{
				DestinationType = "DEPARTMENT",
				AgencyCode = "USO INTERNO",
				AgencyName = "Departamento de " + sourceDepartment,
				AgencyGroup = "Grupo Tejeda",
				InstallerId = "",
				InstallerName = "",
				TicketNumber = null,
				Lines = request.Lines.Select((WarehouseRequestLine line) =>
				{
					WarehouseInventoryItem warehouseInventoryItem2 = inventory.FirstOrDefault((WarehouseInventoryItem item) => string.IsNullOrWhiteSpace(line.ProductId) ? item.Name.Equals(line.Name.Trim(), StringComparison.OrdinalIgnoreCase) : item.ProductId.Equals(line.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
					return line with
					{
						Kind = "EQUIPMENT",
						ProductId = warehouseInventoryItem2?.ProductId,
						Name = (warehouseInventoryItem2?.Name ?? line.Name.Trim()),
						Serial = null
					};
				}).ToList()
			};
		}
		WarehouseRequestInfo result;
		await using (SqlConnection connection = await Open(ct))
		{
			WarehouseRequestInfo warehouseRequestInfo3;
			await using (SqlCommand command = new SqlCommand("SELECT codigo,terminal,grupo FROM dbo.agencies WHERE id=@agency", connection))
			{
				command.Parameters.Add("@agency", SqlDbType.Char, 36).Value = request.AgencyId.ToString();
				WarehouseRequestInfo warehouseRequestInfo;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					WarehouseRequestInfo warehouseRequestInfo2;
					if (!(await reader.ReadAsync(ct)))
					{
						warehouseRequestInfo = null;
					}
					else
					{
						string agencyCode = (reader.IsDBNull(0) ? "" : reader.GetString(0));
						string agencyName = (reader.IsDBNull(1) ? "" : reader.GetString(1));
						string agencyGroup = (reader.IsDBNull(2) ? "" : reader.GetString(2));
						await reader.DisposeAsync();
						if (request.DestinationType == "GROUP" && !(await WarehouseRequestGroups(actor, ct)).Any((string group) => string.Equals(group, agencyGroup.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(group, request.AgencyGroup?.Trim(), StringComparison.OrdinalIgnoreCase)))
						{
							warehouseRequestInfo = null;
						}
						else
						{
							long? ticketNumber = null;
							Guid? ticketId = request.TicketId;
							if (ticketId.HasValue)
							{
								Guid valueOrDefault = ticketId.GetValueOrDefault();
								await using SqlCommand ticket = new SqlCommand("SELECT ticket_number FROM dbo.support_tickets WHERE id=@ticket AND agency_id=@agency AND assigned_department=@department AND status NOT IN('RESOLVED','CLOSED','CANCELLED')", connection);
								ticket.Parameters.AddWithValue("@ticket", valueOrDefault);
								ticket.Parameters.Add("@agency", SqlDbType.Char, 36).Value = request.AgencyId.ToString();
								ticket.Parameters.AddWithValue("@department", department);
								object obj = await ticket.ExecuteScalarAsync(ct);
								if (obj == null)
								{
									warehouseRequestInfo2 = null;
									goto IL_0d64;
								}
								ticketNumber = Convert.ToInt64(obj);
							}
							warehouseRequestInfo = request with
							{
								AgencyCode = agencyCode,
								AgencyName = agencyName,
								AgencyGroup = agencyGroup,
								InstallerId = "",
								InstallerName = "",
								TicketNumber = ticketNumber,
								Lines = request.Lines.Select((WarehouseRequestLine line) =>
								{
									WarehouseInventoryItem warehouseInventoryItem2 = ((!string.IsNullOrWhiteSpace(line.ProductId)) ? inventory.FirstOrDefault((WarehouseInventoryItem item) => item.ProductId.Equals(line.ProductId.Trim(), StringComparison.OrdinalIgnoreCase)) : inventory.FirstOrDefault((WarehouseInventoryItem item) => item.Name.Equals(line.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
									return line with
									{
										Kind = "EQUIPMENT",
										ProductId = warehouseInventoryItem2?.ProductId,
										Name = (warehouseInventoryItem2?.Name ?? line.Name.Trim()),
										Serial = null
									};
								}).ToList()
							};
						}
					}
					goto end_IL_0926;
					IL_0d64:
					warehouseRequestInfo = warehouseRequestInfo2;
					end_IL_0926:;
				}
				warehouseRequestInfo3 = warehouseRequestInfo;
			}
			result = warehouseRequestInfo3;
		}
		return result;
	}

	private async Task<Dictionary<Guid, string>> WarehouseRequestAgencyScope(DocumentActor actor, CancellationToken ct)
	{
		Dictionary<Guid, string> result2;
		await using (SqlConnection connection = await Open(ct))
		{
			string cmdText = ((actor.Role == "GroupAdministrator") ? "SELECT id,grupo FROM dbo.agencies a WHERE EXISTS(SELECT 1 FROM dbo.admin_user_groups g WHERE g.user_id=@user AND g.group_name=a.grupo)" : "SELECT id,grupo FROM dbo.agencies");
			Dictionary<Guid, string> dictionary2;
			await using (SqlCommand command = new SqlCommand(cmdText, connection))
			{
				command.Parameters.AddWithValue("@user", actor.Id);
				Dictionary<Guid, string> rows = new Dictionary<Guid, string>();
				Dictionary<Guid, string> dictionary;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						if (Guid.TryParse(reader.GetString(0), out var result))
						{
							rows[result] = (reader.IsDBNull(1) ? "" : reader.GetString(1));
						}
					}
					dictionary = rows;
				}
				dictionary2 = dictionary;
			}
			result2 = dictionary2;
		}
		return result2;
	}

	public async Task<List<string>> WarehouseRequestGroups(DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Allowed || actor.WarehouseRequestsBlocked || actor.Role == "GroupAdministrator")
		{
			return new List<string>();
		}
		List<string> result;
		await using (SqlConnection connection = await Open(ct))
		{
			string cmdText = "SELECT DISTINCT LTRIM(RTRIM(grupo)) AS name FROM dbo.agencies WHERE NULLIF(LTRIM(RTRIM(grupo)),N'') IS NOT NULL ORDER BY name";
			List<string> list2;
			await using (SqlCommand command = new SqlCommand(cmdText, connection))
			{
				command.Parameters.AddWithValue("@user", actor.Id);
				List<string> groups = new List<string>();
				List<string> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						groups.Add(reader.GetString(0));
					}
					list = groups;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	private static string? WarehouseWithdrawalRequestNumber(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || raw.Length > 2048)
		{
			return null;
		}
		string input = MaintenanceTraceCode.Normalize(raw);
		Match match = Regex.Match(input, "^(?:REAL-MNT\\|)?SOL-ALM-(\\d{8})-?(\\d{6})-([A-Z0-9]{4,32})$", RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return null;
		}
		return "SOLALM" + match.Groups[1].Value + match.Groups[2].Value + match.Groups[3].Value;
	}

	private static string? WarehouseQrToken(string raw)
	{
		raw = raw.Trim().Trim('\'', '"');
		if (raw.Length > 2048)
		{
			return null;
		}
		for (int i = 0; i < 2; i++)
		{
			try
			{
				string text = Uri.UnescapeDataString(raw);
				if (text == raw)
				{
					break;
				}
				raw = text;
				continue;
			}
			catch (UriFormatException)
			{
				return null;
			}
		}
		raw = raw.Replace("\r", "").Replace("\n", "").Replace("\t", "");
		if (Uri.TryCreate(raw, UriKind.Absolute, out Uri result))
		{
			string text2 = "/api/institutional-documents/verify/";
			int num = result.AbsolutePath.IndexOf(text2, StringComparison.OrdinalIgnoreCase);
			if (num >= 0)
			{
				raw = result.AbsolutePath.Substring(num + text2.Length);
			}
		}
		if (raw.Length == 48 && raw.All(Uri.IsHexDigit))
		{
			return raw.ToLowerInvariant();
		}
		Match match = Regex.Match(raw, "(?:^|[^a-z0-9])api[^a-z0-9]+institutional[^a-z0-9]+documents[^a-z0-9]+verify[^a-z0-9]+(?<token>[a-f0-9]{48})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return null;
		}
		return match.Groups["token"].Value.ToLowerInvariant();
	}

	public async Task<List<WarehouseInventoryItem>> WarehouseAvailableInventory(DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Allowed)
		{
			return new List<WarehouseInventoryItem>();
		}
		List<WarehouseInventoryItem> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			await EnsureRepairedInventorySchema(connection, ct);
			string department = (actor.Manager ? "" : WarehouseDepartmentCode(actor.Department));
			if (!actor.Manager && department == null)
			{
				result = new List<WarehouseInventoryItem>();
			}
			else
			{
				List<WarehouseInventoryItem> list;
				await using (SqlCommand command = new SqlCommand("SELECT p.product_id,p.description,\r\n  p.official_stock+\r\n  COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at)),0)-\r\n  COALESCE((SELECT SUM(a.quantity) FROM dbo.warehouse_equipment_allocations a WHERE (a.product_id=p.product_id OR (a.product_id IS NULL AND LOWER(a.equipment_name)=LOWER(p.description))) AND a.status='PENDING_PICKUP'),0) available,\r\n  p.warehouse,p.unit_id,p.division,p.product_group,CONVERT(varchar(10),p.last_purchase_date,23),p.tracks_serial\r\nFROM dbo.warehouse_products p WHERE p.is_active=1 ORDER BY p.description,p.product_id;", connection))
				{
					List<WarehouseInventoryItem> items = new List<WarehouseInventoryItem>();
					await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							string text = reader.GetString(6);
							bool boolean = reader.GetBoolean(8);
							items.Add(new WarehouseInventoryItem(department ?? "", reader.GetString(0), reader.GetString(1), Math.Max(0, reader.GetInt32(2)), new List<string>(), CatalogConsumable(text, boolean), reader.GetString(3), reader.GetString(4), reader.GetString(5), text, reader.IsDBNull(7) ? null : reader.GetString(7), boolean));
						}
					}
					foreach (WarehouseInventoryItem item in items.Where((WarehouseInventoryItem warehouseInventoryItem) => warehouseInventoryItem.TracksSerial))
					{
						await using SqlCommand serials = new SqlCommand("SELECT DISTINCT UPPER(LTRIM(RTRIM(m.serial_number))) FROM dbo.maintenance_movements m\r\nJOIN dbo.warehouse_products p ON p.product_id=@product\r\nWHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (LOWER(m.equipment_type)=LOWER(p.description) OR m.component_type=p.product_id)\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr WHERE rr.movement_id=m.id)\r\n  AND m.movement_type IN('ENTRY','DAMAGED_RETURN') AND m.serial_number IS NOT NULL AND LTRIM(RTRIM(m.serial_number))<>''\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_movements exit_m WHERE UPPER(LTRIM(RTRIM(exit_m.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND exit_m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') AND exit_m.occurred_at>=m.occurred_at)\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_equipment_allocations a WHERE a.serial_number=UPPER(LTRIM(RTRIM(m.serial_number))) AND a.status='PENDING_PICKUP');", connection);
						serials.Parameters.AddWithValue("@product", item.ProductId);
						await using SqlDataReader reader = await serials.ExecuteReaderAsync(ct);
						while (await reader.ReadAsync(ct))
						{
							item.Serials.Add(reader.GetString(0));
						}
					}
					list = items;
				}
				result = list;
			}
		}
		return result;
	}

	public async Task<List<WarehouseAllocationItem>> WarehouseAllocations(DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Allowed)
		{
			return new List<WarehouseAllocationItem>();
		}
		List<WarehouseAllocationItem> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			List<WarehouseAllocationItem> list2;
			await using (SqlCommand command = new SqlCommand("SELECT a.id,a.document_id,a.line_index,a.department,a.equipment_name,a.serial_number,a.quantity,a.status,a.delivery_number,a.delivered_by,a.received_by,a.withdrawn_at,f.metadata\r\nFROM dbo.warehouse_equipment_allocations a JOIN dbo.institutional_files f ON f.id=a.document_id\r\nWHERE (@manager=1 OR (JSON_VALUE(f.metadata,'$.status')='approved' AND (f.owner_id=@owner OR f.department=@department)))\r\nORDER BY a.id DESC;", connection))
			{
				command.Parameters.AddWithValue("@manager", actor.Manager);
				command.Parameters.AddWithValue("@owner", actor.Id);
				command.Parameters.AddWithValue("@department", actor.Department);
				List<WarehouseAllocationItem> rows = new List<WarehouseAllocationItem>();
				List<WarehouseAllocationItem> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(12), InstitutionalJson);
						WarehouseRequestInfo warehouseRequestInfo = institutionalFile?.WarehouseRequest;
						if ((object)warehouseRequestInfo != null && !institutionalFile.Trashed)
						{
							rows.Add(new WarehouseAllocationItem(reader.GetInt64(0), reader.GetInt64(1), warehouseRequestInfo.Number, institutionalFile.Department, warehouseRequestInfo.AgencyCode, warehouseRequestInfo.AgencyName, warehouseRequestInfo.AgencyGroup, reader.GetString(4), reader.GetString(5), reader.GetInt32(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? ((DateTime?)null) : new DateTime?(reader.GetDateTime(11))));
						}
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	public async Task<object> WarehouseRequestFrequency(long id, DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Allowed || actor.WarehouseRequestsBlocked)
		{
			return Array.Empty<object>();
		}
		object result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			WarehouseRequestInfo target = null;
			string targetDepartment = null;
			await using (SqlCommand command = new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id", connection))
			{
				command.Parameters.AddWithValue("@id", id);
				if (await command.ExecuteScalarAsync(ct) is string json)
				{
					InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
					target = institutionalFile?.WarehouseRequest;
					targetDepartment = institutionalFile?.Department;
				}
			}
			if ((object)target == null)
			{
				result = Array.Empty<object>();
			}
			else
			{
				Dictionary<string, (int Deliveries, int Quantity)> counts = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
				await using (SqlCommand command = new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id<>@id AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL AND JSON_QUERY(metadata,'$.warehouseDelivery') IS NOT NULL", connection))
				{
					command.Parameters.AddWithValue("@id", id);
					await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
					while (await reader.ReadAsync(ct))
					{
						InstitutionalFile institutionalFile2 = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(0), InstitutionalJson);
						if (institutionalFile2?.WarehouseRequest?.AgencyId != target.AgencyId || (target.AgencyId == Guid.Empty && institutionalFile2.Department != targetDepartment) || (target.AgencyId == Guid.Empty && (institutionalFile2.WarehouseRequest.DestinationType == "GROUP" != (target.DestinationType == "GROUP") || (target.DestinationType == "GROUP" && !string.Equals(institutionalFile2.WarehouseRequest.AgencyGroup, target.AgencyGroup, StringComparison.OrdinalIgnoreCase)))))
						{
							continue;
						}
						foreach (WarehouseRequestLine item in institutionalFile2.WarehouseRequest.Lines.Where((WarehouseRequestLine line) => line.Kind != "RETURN"))
						{
							string key = ((!string.IsNullOrWhiteSpace(item.ProductId)) ? item.ProductId : item.Name.Trim());
							(int, int) valueOrDefault = counts.GetValueOrDefault(key);
							counts[key] = (valueOrDefault.Item1 + 1, valueOrDefault.Item2 + item.Quantity);
						}
					}
				}
				result = target.Lines.Select((WarehouseRequestLine line, int index) =>
				{
					string key2 = ((!string.IsNullOrWhiteSpace(line.ProductId)) ? line.ProductId : line.Name.Trim());
					(int, int) valueOrDefault2 = counts.GetValueOrDefault(key2);
					return new
					{
						lineIndex = index,
						productId = line.ProductId,
						name = line.Name,
						deliveryCount = valueOrDefault2.Item1,
						quantityDelivered = valueOrDefault2.Item2,
						warning = (valueOrDefault2.Item1 >= 3)
					};
				}).ToList();
			}
		}
		return result;
	}

	public async Task<object> WarehouseConsumptionAnalytics(DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Manager)
		{
			return new
			{
				requests = 0,
				units = 0,
				articles = Array.Empty<object>(),
				departments = Array.Empty<object>(),
				agencies = Array.Empty<object>(),
				groups = Array.Empty<object>(),
				terminals = Array.Empty<object>()
			};
		}
		DateTime now = DateTime.UtcNow;
		if (now - warehouseAnalyticsCachedUtc < TimeSpan.FromMinutes(2.0))
		{
			return warehouseAnalyticsCache;
		}
		await WarehouseAnalyticsLock.WaitAsync(ct);
		try
		{
			now = DateTime.UtcNow;
			if (now - warehouseAnalyticsCachedUtc < TimeSpan.FromMinutes(2.0))
			{
				return warehouseAnalyticsCache;
			}
			object result;
			await using (SqlConnection connection = await Open(ct))
			{
				await EnsureInstitutionalSchema(connection, ct);
				Dictionary<long, (string Department, string AgencyId, string AgencyCode, string AgencyName, string AgencyGroup, List<(string Key, string Label, int Quantity)> Lines)> requests = new Dictionary<long, (string, string, string, string, string, List<(string, string, int)>)>();
				await using (SqlCommand command = new SqlCommand("SELECT f.id,f.department,\r\n       JSON_VALUE(f.metadata,'$.warehouseRequest.agencyId'),\r\n       JSON_VALUE(f.metadata,'$.warehouseRequest.agencyCode'),\r\n       JSON_VALUE(f.metadata,'$.warehouseRequest.agencyName'),\r\n       JSON_VALUE(f.metadata,'$.warehouseRequest.agencyGroup'),\r\n       COALESCE(NULLIF(line.product_id,N''),line.name),line.name,line.quantity\r\nFROM dbo.institutional_files f\r\nCROSS APPLY OPENJSON(f.metadata,'$.warehouseRequest.lines') WITH(\r\n  kind nvarchar(20) '$.kind',\r\n  name nvarchar(240) '$.name',\r\n  quantity int '$.quantity',\r\n  product_id nvarchar(40) '$.productId'\r\n) line\r\nWHERE f.trashed=0 AND ISJSON(f.metadata)=1\r\n  AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL\r\n  AND line.kind<>'RETURN' AND line.quantity>0\r\nORDER BY f.id", connection))
				{
					await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
					while (await reader.ReadAsync(ct))
					{
						long @int = reader.GetInt64(0);
						if (!requests.TryGetValue(@int, out (string, string, string, string, string, List<(string, string, int)>) value))
						{
							value = (reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2), reader.IsDBNull(3) ? "" : reader.GetString(3), reader.IsDBNull(4) ? "" : reader.GetString(4), reader.IsDBNull(5) ? "" : reader.GetString(5), new List<(string, string, int)>());
						}
						value.Item6.Add((reader.IsDBNull(6) ? reader.GetString(7) : reader.GetString(6), reader.GetString(7), reader.GetInt32(8)));
						requests[@int] = value;
					}
				}
				Dictionary<string, (string, int, int)> dictionary = new Dictionary<string, (string, int, int)>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, (string, int, int)> dictionary2 = new Dictionary<string, (string, int, int)>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, (string, int, int)> dictionary3 = new Dictionary<string, (string, int, int)>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, (string, int, int)> dictionary4 = new Dictionary<string, (string, int, int)>(StringComparer.OrdinalIgnoreCase);
				Dictionary<string, (string, int, int)> dictionary5 = new Dictionary<string, (string, int, int)>(StringComparer.OrdinalIgnoreCase);
				int num = 0;
				foreach (var value2 in requests.Values)
				{
					int num2 = value2.Lines.Sum(((string Key, string Label, int Quantity) line) => line.Quantity);
					num += num2;
					Add(dictionary2, value2.Department, value2.Department, num2);
					Add(dictionary3, value2.AgencyId, value2.AgencyCode + " · " + value2.AgencyName, num2);
					Add(dictionary4, value2.AgencyGroup, value2.AgencyGroup, num2);
					Add(dictionary5, value2.AgencyName, value2.AgencyName, num2);
					foreach (var item in value2.Lines)
					{
						Add(dictionary, item.Key, item.Label, item.Quantity);
					}
				}
				warehouseAnalyticsCache = new
				{
					requests = requests.Count,
					units = num,
					articles = Rank(dictionary),
					departments = Rank(dictionary2),
					agencies = Rank(dictionary3),
					groups = Rank(dictionary4),
					terminals = Rank(dictionary5)
				};
				warehouseAnalyticsCachedUtc = now;
				result = warehouseAnalyticsCache;
			}
			return result;
		}
		catch (SqlException ex) when (!ct.IsCancellationRequested)
		{
			Console.Error.WriteLine("Analítica de Almacén servida desde caché: " + ex.Message);
			warehouseAnalyticsCachedUtc = DateTime.UtcNow;
			return warehouseAnalyticsCache;
		}
		finally
		{
			WarehouseAnalyticsLock.Release();
		}
		static void Add(Dictionary<string, (string Label, int Requests, int Units)> bucket, string key, string label, int quantity)
		{
			(string, int, int) valueOrDefault = bucket.GetValueOrDefault(key);
			bucket[key] = (label, valueOrDefault.Item2 + 1, valueOrDefault.Item3 + quantity);
		}
		static object[] Rank(Dictionary<string, (string Label, int Requests, int Units)> rows)
		{
			return (from row in rows
				select new
				{
					key = row.Key,
					label = row.Value.Label,
					requests = row.Value.Requests,
					units = row.Value.Units
				} into row
				orderby row.units descending, row.requests descending, row.label
				select row).Take(20).Cast<object>().ToArray();
		}
	}

	public async Task<List<object>> WarehousePendingStock(string role, string? team, CancellationToken ct)
	{
		if (role != "Administrator" && team != "WAREHOUSE")
		{
			return new List<object>();
		}
		List<object> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			List<object> list2;
			await using (SqlCommand command = new SqlCommand("SELECT department,equipment_name,serial_number,quantity FROM dbo.warehouse_equipment_allocations WHERE status='PENDING_PICKUP'", connection))
			{
				List<object> rows = new List<object>();
				List<object> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						rows.Add(new
						{
							department = reader.GetString(0),
							equipmentType = reader.GetString(1),
							serialNumber = reader.GetString(2),
							quantity = reader.GetInt32(3)
						});
					}
					list = rows;
				}
				list2 = list;
			}
			result = list2;
		}
		return result;
	}

	private static async Task<string?> WarehouseProtectReservedStock(SqlConnection connection, SqlTransaction tx, string department, string equipment, string? serial, int quantity, CancellationToken ct)
	{
		await using (SqlCommand stockLock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", connection, tx))
		{
			await stockLock.ExecuteScalarAsync(ct);
		}
		string result;
		await using (SqlCommand command = new SqlCommand("SELECT COALESCE(SUM(quantity),0),COALESCE(SUM(CASE WHEN serial_number=@serial THEN 1 ELSE 0 END),0)\r\nFROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK)\r\nWHERE department=@department AND status='PENDING_PICKUP' AND (LOWER(equipment_name)=LOWER(@equipment) OR serial_number=@serial);", connection, tx))
		{
			command.Parameters.AddWithValue("@department", department);
			command.Parameters.AddWithValue("@equipment", equipment);
			command.Parameters.AddWithValue("@serial", ((object)serial) ?? ((object)DBNull.Value));
			int pending;
			string text;
			await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
			{
				await reader.ReadAsync(ct);
				pending = reader.GetInt32(0);
				if (reader.GetInt32(1) > 0)
				{
					text = "Este serial está reservado para una solicitud autorizada. Registra su retiro desde el QR del comprobante.";
					goto IL_03e6;
				}
			}
			text = null;
			if (pending == 0)
			{
				result = null;
			}
			else
			{
				await using (SqlCommand stock = new SqlCommand("SELECT COALESCE(SUM(CASE WHEN movement_type IN('ENTRY','DAMAGED_RETURN') THEN quantity WHEN movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -quantity ELSE 0 END),0) FROM dbo.maintenance_movements WHERE department=@department AND operational_area='WAREHOUSE' AND LOWER(equipment_type)=LOWER(@equipment)", connection, tx))
				{
					stock.Parameters.AddWithValue("@department", department);
					stock.Parameters.AddWithValue("@equipment", equipment);
					text = ((Convert.ToInt32(await stock.ExecuteScalarAsync(ct)) - pending >= quantity) ? null : "La salida utilizaría equipos pendientes de retiro. Selecciona existencias disponibles.");
				}
				result = text;
			}
			goto end_IL_01b6;
			IL_03e6:
			result = text;
			end_IL_01b6:;
		}
		return result;
	}

	private static async Task EnsureWarehouseEntryProduct(SqlConnection connection, SqlTransaction tx, string equipment, int quantity, DateTime occurred, CancellationToken ct, bool hasSerial = true)
	{
		await using SqlCommand cmd = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.warehouse_products WITH(UPDLOCK,HOLDLOCK) WHERE description=@equipment AND is_active=1)\r\nINSERT dbo.warehouse_products(product_id,description,is_product,warehouse,official_stock,unit_id,division,product_group,tracks_serial,catalog_version,imported_at,is_active)\r\nVALUES(@id,@equipment,1,N'PRINCIPAL',0,N'UND',N'Registro manual',N'Equipos',@serial,N'MANUAL',DATEADD(second,-1,@occurred),1);", connection, tx);
		cmd.Parameters.AddWithValue("@id", "MAN-" + Guid.NewGuid().ToString("N"));
		cmd.Parameters.AddWithValue("@equipment", equipment);
		cmd.Parameters.AddWithValue("@serial", hasSerial && quantity == 1);
		cmd.Parameters.AddWithValue("@occurred", occurred);
		await cmd.ExecuteNonQueryAsync(ct);
	}

	public async Task<string?> WarehouseSavePendingPurchase(long id, DocumentActor actor, WarehousePurchasePendingInput input, CancellationToken ct)
	{
		bool flag = actor.Team != "WAREHOUSE";
		bool flag2 = flag;
		if (flag2)
		{
			string role = actor.Role;
			bool flag3 = ((role == "Administrator" || role == "Warehouse") ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			return "Solo Almacén puede registrar artículos pendientes de compra.";
		}
		bool flag4 = input.Note == null;
		bool flag5 = flag4;
		if (!flag5)
		{
			int length = input.Note.Trim().Length;
			bool flag3 = ((length < 5 || length > 2000) ? true : false);
			flag5 = flag3;
		}
		if (flag5 || input.LineIndexes == null || input.LineIndexes.Count == 0 || input.Equipment == null || input.Equipment.Count > 10000)
		{
			return "Selecciona los artículos pendientes de compra e indica el motivo.";
		}
		List<WarehouseRequestLine> lines;
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			string text3;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", connection, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text = "Solicitud no encontrada.";
					}
					else
					{
						InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						lines = institutionalFile.WarehouseRequest?.Lines;
						if (lines == null || institutionalFile.Channel != "requirements" || institutionalFile.RecipientDepartment != "Almacén" || institutionalFile.Status != "approved" || institutionalFile.Trashed || institutionalFile.WarehouseAssigned.Count > 0 || (object)institutionalFile.WarehouseDelivery != null)
						{
							text = "Esta solicitud no admite cambios de preparación.";
						}
						else if (input.LineIndexes.Any((int index) => !ValidLine(index)) || input.Equipment.Any((WarehouseAssignedEquipment row) =>
						{
							if ((object)row != null && ValidLine(row.LineIndex) && row.Quantity >= 1 && row.Quantity <= 10000 && !string.IsNullOrWhiteSpace(row.Name) && row.Name.Length <= 240)
							{
								string serial = row.Serial;
								if (serial == null || serial.Length <= 120)
								{
									string? productId = row.ProductId;
									if (productId == null)
									{
										return false;
									}
									return productId.Length > 120;
								}
							}
							return true;
						}))
						{
							text = "Revisa los renglones y cantidades.";
						}
						else
						{
							foreach (IGrouping<int, WarehouseAssignedEquipment> item in from row in input.Equipment
								group row by row.LineIndex)
							{
								if (((IEnumerable<WarehouseAssignedEquipment>)item).Sum((Func<WarehouseAssignedEquipment, long>)((WarehouseAssignedEquipment row) => row.Quantity)) <= lines[item.Key].Quantity)
								{
									continue;
								}
								text = "La cantidad no puede superar lo solicitado.";
								goto end_IL_0336;
							}
							institutionalFile.WarehousePendingPurchaseLines = input.LineIndexes.Distinct().Order().ToList();
							institutionalFile.WarehouseAssignmentDraft = input.Equipment;
							institutionalFile.WarehousePurchaseNote = input.Note.Trim();
							institutionalFile.History.Add(new InstitutionalEvent("pending_purchase", actor.Name, actor.Login, DateTime.UtcNow, institutionalFile.Status, institutionalFile.ReceiptCode, institutionalFile.ReceiverLogin, institutionalFile.ReceivedUtc, null));
							string text2;
							await using (SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", connection, tx))
							{
								update.Parameters.AddWithValue("@id", id);
								update.Parameters.AddWithValue("@json", JsonSerializer.Serialize(institutionalFile, InstitutionalJson));
								await update.ExecuteNonQueryAsync(ct);
								await tx.CommitAsync(ct);
								text2 = null;
							}
							text = text2;
						}
					}
					end_IL_0336:;
				}
				text3 = text;
			}
			result = text3;
		}
		return result;
		bool ValidLine(int index)
		{
			if (index >= 0 && index < lines.Count)
			{
				return lines[index].Kind != "RETURN";
			}
			return false;
		}
	}

	public async Task<string?> WarehousePrepare(long id, DocumentActor actor, WarehouseAuthorizationInput input, CancellationToken ct)
	{
		bool flag = actor.Team != "WAREHOUSE";
		bool flag2 = flag;
		if (flag2)
		{
			string role = actor.Role;
			bool flag3 = ((role == "Administrator" || role == "Warehouse") ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			return "Solo Almacén puede preparar el despacho.";
		}
		bool flag4 = input.Note == null || input.Equipment == null || input.Equipment.Any((WarehouseAssignedEquipment x) => (object)x == null);
		bool flag5 = flag4;
		if (!flag5)
		{
			int length = input.Note.Trim().Length;
			bool flag3 = ((length < 5 || length > 2000) ? true : false);
			flag5 = flag3;
		}
		if (flag5 || input.Equipment.Count > 10000)
		{
			return "Indica un motivo y revisa los equipos asignados.";
		}
		List<WarehouseInventoryItem> catalog = await WarehouseAvailableInventory(actor, ct);
		await InstitutionalDetail(id, actor, ct);
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			string text5;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand documentCommand = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", connection, tx))
				{
					documentCommand.Parameters.AddWithValue("@id", id);
					if (!(await documentCommand.ExecuteScalarAsync(ct) is string json))
					{
						text = "Solicitud no encontrada.";
					}
					else
					{
						InstitutionalFile document = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						document.Id = id;
						WarehouseRequestInfo warehouseRequest = document.WarehouseRequest;
						if ((object)warehouseRequest == null || document.Channel != "requirements" || document.RecipientDepartment != "Almacén" || document.Status != "approved" || document.WarehouseAssigned.Count > 0 || (object)document.WarehouseDelivery != null || document.Trashed)
						{
							text = "Autoriza primero la solicitud antes de asignar equipos y seriales.";
						}
						else
						{
							string department = WarehouseDepartmentCode(document.Department);
							if (department == null)
							{
								text = "Departamento solicitante no válido.";
							}
							else
							{
								List<(WarehouseRequestLine line, int index)> expected = (from x in warehouseRequest.Lines.Select((WarehouseRequestLine line, int item2) => (line: line, index: item2))
									where x.line.Kind != "RETURN"
									select x).ToList();
								if (!input.Equipment.All((WarehouseAssignedEquipment x) => expected.Any(((WarehouseRequestLine line, int index) line) => line.index == x.LineIndex)) || input.Equipment.Select((WarehouseAssignedEquipment x) => x.LineIndex).Distinct().Count() != expected.Count)
								{
									text = "Asigna un equipo del inventario a cada renglón solicitado.";
								}
								else if (input.Equipment.Any((WarehouseAssignedEquipment x) =>
								{
									bool flag6 = x.Name == null;
									bool flag7 = flag6;
									if (!flag7)
									{
										int length2 = x.Name.Trim().Length;
										bool flag8 = ((length2 < 1 || length2 > 240) ? true : false);
										flag7 = flag8;
									}
									if (!flag7 && x.Quantity >= 1)
									{
										string serial = x.Serial;
										if (serial == null)
										{
											return false;
										}
										return serial.Trim().Length > 120;
									}
									return true;
								}))
								{
									text = "Revisa las cantidades y los seriales de los artículos.";
								}
								else
								{
									List<string> list = (from x in input.Equipment
										where !string.IsNullOrWhiteSpace(x.Serial)
										select x.Serial.Trim().ToUpperInvariant()).ToList();
									if (list.Distinct().Count() != list.Count)
									{
										text = "Cada equipo debe tener su propio serial válido y único.";
									}
									else
									{
										foreach (var item2 in expected)
										{
											WarehouseRequestLine item = item2.line;
											int index = item2.index;
											List<WarehouseAssignedEquipment> list2 = input.Equipment.Where((WarehouseAssignedEquipment x) => x.LineIndex == index).ToList();
											if (list2.Sum((WarehouseAssignedEquipment x) => x.Quantity) == item.Quantity)
											{
												foreach (WarehouseAssignedEquipment selected in list2)
												{
													WarehouseInventoryItem warehouseInventoryItem = catalog.FirstOrDefault((WarehouseInventoryItem warehouseInventoryItem2) => string.IsNullOrWhiteSpace(selected.ProductId) ? warehouseInventoryItem2.Name.Equals(selected.Name.Trim(), StringComparison.OrdinalIgnoreCase) : warehouseInventoryItem2.ProductId.Equals(selected.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
													if ((object)warehouseInventoryItem == null || !selected.Name.Trim().Equals(warehouseInventoryItem.Name, StringComparison.OrdinalIgnoreCase))
													{
														text = "Selecciona un artículo válido del inventario para entregar.";
													}
													else
													{
														if (!warehouseInventoryItem.TracksSerial || (selected.Quantity == 1 && !string.IsNullOrWhiteSpace(selected.Serial)))
														{
															continue;
														}
														text = "Registra o escanea un serial por cada unidad de " + warehouseInventoryItem.Name + ".";
													}
													goto end_IL_05ae;
												}
												continue;
											}
											text = "La cantidad asignada debe coincidir con la solicitud.";
											goto end_IL_05ae;
										}
										input = input with
										{
											Equipment = input.Equipment.Select((WarehouseAssignedEquipment warehouseAssignedEquipment) =>
											{
												WarehouseInventoryItem warehouseInventoryItem2 = catalog.First((WarehouseInventoryItem warehouseInventoryItem3) => string.IsNullOrWhiteSpace(warehouseAssignedEquipment.ProductId) ? warehouseInventoryItem3.Name.Equals(warehouseAssignedEquipment.Name.Trim(), StringComparison.OrdinalIgnoreCase) : warehouseInventoryItem3.ProductId.Equals(warehouseAssignedEquipment.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
												return warehouseAssignedEquipment with
												{
													ProductId = warehouseInventoryItem2.ProductId,
													Name = warehouseInventoryItem2.Name
												};
											}).ToList()
										};
										await using (SqlCommand lockStock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", connection, tx))
										{
											await lockStock.ExecuteScalarAsync(ct);
										}
										string text2;
										foreach (WarehouseAssignedEquipment equipment in input.Equipment.Where((WarehouseAssignedEquipment x) => !string.IsNullOrWhiteSpace(x.Serial)))
										{
											WarehouseInventoryItem product = catalog.First((WarehouseInventoryItem warehouseInventoryItem2) => string.IsNullOrWhiteSpace(equipment.ProductId) ? warehouseInventoryItem2.Name.Equals(equipment.Name.Trim(), StringComparison.OrdinalIgnoreCase) : warehouseInventoryItem2.ProductId.Equals(equipment.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
											if (!product.TracksSerial)
											{
												continue;
											}
											await using (SqlCommand lockStock = new SqlCommand("SELECT COUNT(*) FROM dbo.maintenance_movements m\r\nJOIN dbo.warehouse_products p ON p.product_id=@product\r\nWHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at\r\n  AND (LOWER(m.equipment_type)=LOWER(p.description) OR m.component_type=p.product_id)\r\n  AND m.movement_type IN('ENTRY','DAMAGED_RETURN')\r\n  AND UPPER(LTRIM(RTRIM(m.serial_number)))=@serial\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr WHERE rr.movement_id=m.id)\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_movements exit_m WHERE UPPER(LTRIM(RTRIM(exit_m.serial_number)))=@serial AND exit_m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') AND exit_m.occurred_at>=m.occurred_at)\r\n  AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_equipment_allocations a WITH(UPDLOCK,HOLDLOCK) WHERE a.serial_number=@serial AND a.status='PENDING_PICKUP');", connection, tx))
											{
												lockStock.Parameters.AddWithValue("@product", product.ProductId);
												lockStock.Parameters.AddWithValue("@serial", equipment.Serial.Trim().ToUpperInvariant());
												if (Convert.ToInt32(await lockStock.ExecuteScalarAsync(ct)) == 0)
												{
													text2 = $"El serial {equipment.Serial.Trim().ToUpperInvariant()} no está disponible en el inventario de {product.Name}.";
													goto IL_0ecf;
												}
											}
											text2 = null;
											continue;
											IL_0ecf:
											text = text2;
											goto end_IL_05ae;
										}
										string text3;
										foreach (IGrouping<string, WarehouseAssignedEquipment> grouped in input.Equipment.GroupBy((WarehouseAssignedEquipment x) => string.IsNullOrWhiteSpace(x.ProductId) ? x.Name.Trim() : x.ProductId.Trim(), StringComparer.OrdinalIgnoreCase))
										{
											await using (SqlCommand lockStock = new SqlCommand("SELECT p.official_stock+\r\n  COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at)),0)\r\nFROM dbo.warehouse_products p WHERE p.is_active=1 AND p.product_id=@product", connection, tx))
											{
												WarehouseInventoryItem product = catalog.First((WarehouseInventoryItem warehouseInventoryItem2) => warehouseInventoryItem2.ProductId.Equals(grouped.Key, StringComparison.OrdinalIgnoreCase) || warehouseInventoryItem2.Name.Equals(grouped.Key, StringComparison.OrdinalIgnoreCase));
												lockStock.Parameters.AddWithValue("@product", product.ProductId);
												int num = Convert.ToInt32(await lockStock.ExecuteScalarAsync(ct));
												await using (SqlCommand pending = new SqlCommand("SELECT COALESCE(SUM(quantity),0) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE (product_id=@product OR (product_id IS NULL AND LOWER(equipment_name)=LOWER(@equipment))) AND status='PENDING_PICKUP'", connection, tx))
												{
													pending.Parameters.AddWithValue("@product", product.ProductId);
													pending.Parameters.AddWithValue("@equipment", product.Name);
													int num2 = Convert.ToInt32(await pending.ExecuteScalarAsync(ct));
													if (num - num2 < grouped.Sum((WarehouseAssignedEquipment x) => x.Quantity))
													{
														text3 = "No hay existencias suficientes de " + product.Name + " en Inventario; la solicitud debe permanecer pendiente de entrega.";
														goto IL_12ac;
													}
												}
												text3 = null;
												goto end_IL_0fe9;
												IL_12ac:
												text2 = text3;
												goto IL_1384;
												end_IL_0fe9:;
											}
											text2 = null;
											continue;
											IL_1384:
											text = text2;
											goto end_IL_05ae;
										}
										foreach (WarehouseAssignedEquipment equipment2 in input.Equipment)
										{
											WarehouseInventoryItem product = catalog.First((WarehouseInventoryItem warehouseInventoryItem2) => string.IsNullOrWhiteSpace(equipment2.ProductId) ? warehouseInventoryItem2.Name.Equals(equipment2.Name.Trim(), StringComparison.OrdinalIgnoreCase) : warehouseInventoryItem2.ProductId.Equals(equipment2.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
											string text4 = equipment2.Serial?.Trim().ToUpperInvariant();
											text2 = ((!string.IsNullOrWhiteSpace(text4)) ? text4 : $"CONTROL-{id:D10}-{equipment2.LineIndex:D2}");
											if (!string.IsNullOrWhiteSpace(text4))
											{
												await using (SqlCommand lockStock = new SqlCommand("SELECT COUNT(*) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE serial_number=@serial AND status='PENDING_PICKUP'", connection, tx))
												{
													lockStock.Parameters.AddWithValue("@serial", text2);
													if (Convert.ToInt32(await lockStock.ExecuteScalarAsync(ct)) > 0)
													{
														text3 = "El serial " + text2 + " ya está pendiente de retiro.";
														goto IL_16a1;
													}
												}
												text3 = null;
											}
											await using (SqlCommand pending = new SqlCommand("INSERT INTO dbo.warehouse_equipment_allocations(document_id,line_index,department,product_id,equipment_name,serial_number,quantity) VALUES(@id,@line,@department,@product,@equipment,@serial,@quantity)", connection, tx))
											{
												pending.Parameters.AddWithValue("@id", id);
												pending.Parameters.AddWithValue("@line", equipment2.LineIndex);
												pending.Parameters.AddWithValue("@department", department);
												pending.Parameters.AddWithValue("@product", product.ProductId);
												pending.Parameters.AddWithValue("@equipment", product.Name);
												pending.Parameters.AddWithValue("@serial", text2);
												pending.Parameters.AddWithValue("@quantity", equipment2.Quantity);
												await pending.ExecuteNonQueryAsync(ct);
											}
											text2 = null;
											continue;
											IL_16a1:
											text = text3;
											goto end_IL_05ae;
										}
										DateTime utcNow = DateTime.UtcNow;
										document.WarehousePendingPurchaseLines = new List<int>();
										document.WarehouseAssignmentDraft = new List<WarehouseAssignedEquipment>();
										document.WarehousePurchaseNote = null;
										document.History.Add(new InstitutionalEvent("dispatch", actor.Name, actor.Login, utcNow, document.Status, document.ReceiptCode, document.ReceiverLogin, document.ReceivedUtc, null));
										document.WarehousePreparationNote = input.Note.Trim();
										document.WarehousePreparedBy = actor.Name;
										document.WarehousePreparedLogin = actor.Login;
										document.WarehousePreparedUtc = utcNow;
										document.WarehouseAssigned = input.Equipment.Select((WarehouseAssignedEquipment x) =>
										{
											WarehouseInventoryItem warehouseInventoryItem2 = catalog.First((WarehouseInventoryItem warehouseInventoryItem3) => string.IsNullOrWhiteSpace(x.ProductId) ? warehouseInventoryItem3.Name.Equals(x.Name.Trim(), StringComparison.OrdinalIgnoreCase) : warehouseInventoryItem3.ProductId.Equals(x.ProductId.Trim(), StringComparison.OrdinalIgnoreCase));
											return x with
											{
												ProductId = warehouseInventoryItem2.ProductId,
												Name = warehouseInventoryItem2.Name,
												Serial = (string.IsNullOrWhiteSpace(x.Serial) ? "CONTROL POR CANTIDAD" : x.Serial.Trim().ToUpperInvariant())
											};
										}).ToList();
										await using (SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", connection, tx))
										{
											update.Parameters.AddWithValue("@json", JsonSerializer.Serialize(document, InstitutionalJson));
											update.Parameters.AddWithValue("@id", id);
											await update.ExecuteNonQueryAsync(ct);
											await tx.CommitAsync(ct);
											text2 = null;
										}
										text = text2;
									}
								}
							}
						}
					}
					end_IL_05ae:;
				}
				text5 = text;
			}
			result = text5;
		}
		return result;
	}

	public async Task<string?> WarehouseApprovePrepared(long id, DocumentActor actor, WarehouseApprovalInput input, CancellationToken ct)
	{
		bool flag = !actor.Manager;
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = actor.Team != "WAREHOUSE";
			bool flag4 = flag3;
			if (flag4)
			{
				string role = actor.Role;
				bool flag5 = ((role == "Administrator" || role == "Warehouse") ? true : false);
				flag4 = !flag5;
			}
			flag2 = flag4;
		}
		if (flag2)
		{
			return "Solo la encargada autorizada de Almacén puede colocar el sello.";
		}
		bool flag6 = input.Note == null;
		bool flag7 = flag6;
		if (!flag7)
		{
			int length = input.Note.Trim().Length;
			bool flag5 = ((length < 5 || length > 2000) ? true : false);
			flag7 = flag5;
		}
		if (flag7)
		{
			return "Indica un motivo de autorización válido.";
		}
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			string text3;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand documentCommand = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", connection, tx))
				{
					documentCommand.Parameters.AddWithValue("@id", id);
					if (!(await documentCommand.ExecuteScalarAsync(ct) is string json))
					{
						text = "Solicitud no encontrada.";
					}
					else
					{
						InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						institutionalFile.NormalizeWarehouseReceiptState();
						institutionalFile.Id = id;
						if ((object)institutionalFile.WarehouseRequest == null || institutionalFile.Channel != "requirements" || institutionalFile.RecipientDepartment != "Almacén" || !institutionalFile.AwaitingWarehouseAuthorization() || institutionalFile.Trashed)
						{
							text = "La solicitud no está pendiente de autorización.";
						}
						else
						{
							DateTime utcNow = DateTime.UtcNow;
							institutionalFile.History.Add(new InstitutionalEvent("approve", actor.Name, actor.Login, utcNow, institutionalFile.Status, institutionalFile.ReceiptCode, institutionalFile.ReceiverLogin, institutionalFile.ReceivedUtc, null));
							institutionalFile.Status = "approved";
							institutionalFile.DecisionBy = actor.Name + " · " + actor.Login;
							institutionalFile.DecisionUtc = utcNow;
							institutionalFile.CorrectionNote = input.Note.Trim();
							string text2;
							await using (SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", connection, tx))
							{
								update.Parameters.AddWithValue("@json", JsonSerializer.Serialize(institutionalFile, InstitutionalJson));
								update.Parameters.AddWithValue("@id", id);
								await update.ExecuteNonQueryAsync(ct);
								await tx.CommitAsync(ct);
								text2 = null;
							}
							text = text2;
						}
					}
				}
				text3 = text;
			}
			result = text3;
		}
		return result;
	}

	public async Task<(string? Error, WarehouseDeliveryInfo? Delivery, long DocumentId)> WarehouseWithdraw(DocumentActor actor, WarehouseWithdrawalInput input, CancellationToken ct)
	{
		if (!actor.Manager)
		{
			return (Error: "Solo Almacén puede registrar el retiro.", Delivery: null, DocumentId: 0L);
		}
		string token = ((input.SealQr == null) ? null : WarehouseQrToken(input.SealQr));
		string requestNumber = WarehouseWithdrawalRequestNumber(input.SealQr);
		bool flag = input.ReceivedBy == null;
		bool flag2 = flag;
		if (!flag2)
		{
			int length = input.ReceivedBy.Trim().Length;
			bool flag3 = ((length < 3 || length > 160) ? true : false);
			flag2 = flag3;
		}
		if (flag2)
		{
			return (Error: "Indica el nombre de quien recibe (entre 3 y 160 caracteres).", Delivery: null, DocumentId: 0L);
		}
		if (token == null && requestNumber == null)
		{
			return (Error: "No se pudo leer el QR. Escanea completo el QR principal del formulario o el del sello autorizado.", Delivery: null, DocumentId: 0L);
		}
		(string?, WarehouseDeliveryInfo?, long) result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			await EnsureWarehouseWithdrawalSchema(connection, ct);
			(string?, WarehouseDeliveryInfo?, long) tuple5;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				(string?, WarehouseDeliveryInfo?, long) tuple2;
				await using (SqlCommand find = new SqlCommand("SELECT TOP(2) id,metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE (@token IS NOT NULL AND JSON_VALUE(metadata,'$.verificationToken')=@token) OR (@token IS NULL AND @number IS NOT NULL AND REPLACE(JSON_VALUE(metadata,'$.warehouseRequest.number'),'-','')=@number)", connection, tx))
				{
					find.Parameters.Add("@token", SqlDbType.NVarChar, 48).Value = ((object)token) ?? ((object)DBNull.Value);
					find.Parameters.Add("@number", SqlDbType.NVarChar, 100).Value = ((object)requestNumber) ?? ((object)DBNull.Value);
					long id;
					InstitutionalFile document;
					(string?, WarehouseDeliveryInfo?, long) tuple;
					await using (SqlDataReader reader = await find.ExecuteReaderAsync(ct))
					{
						if (!(await reader.ReadAsync(ct)))
						{
							tuple = ("El sello QR no corresponde a una solicitud registrada.", null, 0L);
							goto IL_07d4;
						}
						id = reader.GetInt64(0);
						document = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson);
						if (await reader.ReadAsync(ct))
						{
							tuple = ("El QR coincide con más de un formulario. Utiliza el QR único del sello autorizado.", null, 0L);
							goto IL_07d4;
						}
					}
					tuple = default;
					if (input.DocumentId.HasValue && input.DocumentId != id)
					{
						tuple2 = ("El QR escaneado no corresponde a esta solicitud. Escanea el comprobante de la tarjeta seleccionada.", null, id);
					}
					else if ((object)document.WarehouseRequest == null || document.RecipientDepartment != "Almacén" || document.Status != "approved" || !document.DecisionUtc.HasValue || document.Trashed)
					{
						tuple2 = ("La solicitud aún no está autorizada para retiro.", null, id);
					}
					else if ((object)document.WarehouseDelivery != null)
					{
						tuple2 = ("Este comprobante ya fue retirado; no se duplicó la entrega.", document.WarehouseDelivery, id);
					}
					else
					{
						await using (SqlCommand stockLock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", connection, tx))
						{
							await stockLock.ExecuteScalarAsync(ct);
						}
						await using (SqlCommand rows = new SqlCommand("SELECT id,line_index,product_id,equipment_name,serial_number,quantity FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE document_id=@id AND status='PENDING_PICKUP' ORDER BY id", connection, tx))
						{
							rows.Parameters.AddWithValue("@id", id);
							List<(long Id, int Line, string? ProductId, string Name, string Serial, int Quantity)> allocations = new List<(long, int, string, string, string, int)>();
							await using (SqlDataReader reader = await rows.ExecuteReaderAsync(ct))
							{
								while (await reader.ReadAsync(ct))
								{
									allocations.Add((reader.GetInt64(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetInt32(5)));
								}
							}
							if (allocations.Count == 0)
							{
								tuple = ("Esta solicitud no tiene equipos pendientes de retiro.", null, id);
							}
							else
							{
								WarehouseRequestInfo request = document.WarehouseRequest;
								string department = WarehouseDepartmentCode(document.Department);
								DateTime now = DateTime.UtcNow;
								WarehouseDeliveryInfo delivery = new WarehouseDeliveryInfo($"ENT-ALM-{now:yyyyMMdd}-{id:D6}", actor.Name, actor.Login, input.ReceivedBy.Trim(), now);
								(string?, WarehouseDeliveryInfo?, long id) tuple4;
								foreach (var allocation in allocations)
								{
									string value = $"ENT-ALM-{Guid.NewGuid():N}";
									await using (SqlCommand stockLock = new SqlCommand("INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at)\r\nVALUES(NEWID(),@agency,@ticket,@department,@installer,@installerName,'NEW_DELIVERY',@equipment,@product,'Retiro por QR de solicitud autorizada',@serial,@quantity,@notes,@actorId,@actorName,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@delivered,@received,@destination,NULL,@at)", connection, tx))
									{
										stockLock.Parameters.Add("@agency", SqlDbType.Char, 36).Value = ((request.AgencyId == Guid.Empty) ? ((IConvertible)DBNull.Value) : ((IConvertible)request.AgencyId.ToString()));
										stockLock.Parameters.AddWithValue("@ticket", Db(request.TicketId));
										stockLock.Parameters.AddWithValue("@department", department);
										stockLock.Parameters.AddWithValue("@installer", Db(string.IsNullOrWhiteSpace(request.InstallerId) ? null : request.InstallerId));
										stockLock.Parameters.AddWithValue("@installerName", delivery.ReceivedBy);
										stockLock.Parameters.AddWithValue("@equipment", allocation.Name);
										stockLock.Parameters.AddWithValue("@product", Db(allocation.ProductId));
										stockLock.Parameters.AddWithValue("@serial", allocation.Serial);
										stockLock.Parameters.AddWithValue("@quantity", allocation.Quantity);
										stockLock.Parameters.AddWithValue("@notes", request.Number + " · " + delivery.Number + " · QR de sello autorizado");
										stockLock.Parameters.AddWithValue("@actorId", actor.Id);
										stockLock.Parameters.AddWithValue("@actorName", actor.Name);
										stockLock.Parameters.AddWithValue("@number", value);
										stockLock.Parameters.AddWithValue("@delivered", actor.Name);
										stockLock.Parameters.AddWithValue("@received", delivery.ReceivedBy);
										string text = $"{document.Department} · {request.AgencyCode} · {request.AgencyName}";
										stockLock.Parameters.AddWithValue("@destination", (text.Length > 200) ? text.Substring(0, 200) : text);
										stockLock.Parameters.AddWithValue("@at", now);
										await stockLock.ExecuteNonQueryAsync(ct);
										(string?, WarehouseDeliveryInfo?, long id) tuple3;
										await using (SqlCommand allocated = new SqlCommand("UPDATE dbo.warehouse_equipment_allocations SET status='WITHDRAWN',delivery_number=@number,delivered_by=@delivered,received_by=@received,withdrawn_at=@at WHERE id=@id AND status='PENDING_PICKUP'", connection, tx))
										{
											allocated.Parameters.AddWithValue("@number", delivery.Number);
											allocated.Parameters.AddWithValue("@delivered", actor.Name);
											allocated.Parameters.AddWithValue("@received", delivery.ReceivedBy);
											allocated.Parameters.AddWithValue("@at", now);
											allocated.Parameters.AddWithValue("@id", allocation.Id);
											if (await allocated.ExecuteNonQueryAsync(ct) != 1)
											{
												tuple3 = ("El retiro cambió; inténtalo de nuevo.", null, id: id);
												goto IL_1422;
											}
										}
										tuple3 = default;
										goto end_IL_0e81;
										IL_1422:
										tuple4 = tuple3;
										goto IL_14ff;
										end_IL_0e81:;
									}
									tuple4 = default;
									continue;
									IL_14ff:
									tuple = tuple4;
									goto end_IL_0ab3;
								}
								document.WarehouseDelivery = delivery;
								document.History.Add(new InstitutionalEvent("withdraw", actor.Name, actor.Login, now, document.Status, document.ReceiptCode, document.ReceiverLogin, document.ReceivedUtc, null));
								await using (SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", connection, tx))
								{
									update.Parameters.AddWithValue("@json", JsonSerializer.Serialize(document, InstitutionalJson));
									update.Parameters.AddWithValue("@id", id);
									await update.ExecuteNonQueryAsync(ct);
									await tx.CommitAsync(ct);
									tuple4 = (null, delivery, id: id);
								}
								tuple = tuple4;
							}
							end_IL_0ab3:;
						}
						tuple2 = tuple;
					}
					goto end_IL_04f8;
					IL_07d4:
					tuple2 = tuple;
					end_IL_04f8:;
				}
				tuple5 = tuple2;
			}
			result = tuple5;
		}
		return result;
	}

	public async Task<List<object>> WarehouseNotifications(DocumentActor actor, CancellationToken ct)
	{
		string role = actor.Role;
		bool flag = ((role == "Administrator" || role == "Warehouse") ? true : false);
		if (!flag && actor.Team != "WAREHOUSE")
		{
			return new List<object>();
		}
		List<object> result2;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			await EnsureNotificationReceiptSchema(connection, ct);
			List<object> list2;
			await using (SqlCommand command = new SqlCommand("SELECT TOP(100) f.id,JSON_VALUE(f.metadata,'$.title'),f.department,\n  JSON_VALUE(f.metadata,'$.ownerDisplayName'),JSON_VALUE(f.metadata,'$.warehouseRequest.number'),\n  JSON_VALUE(f.metadata,'$.createdUtc'),JSON_VALUE(f.metadata,'$.status'),r.read_at\nFROM dbo.institutional_files f\nLEFT JOIN dbo.support_notification_receipts r ON r.user_id=@user AND r.notification_key=CONCAT('WAREHOUSE_REQUEST:',f.id)\nWHERE COALESCE(JSON_VALUE(f.metadata,'$.trashed'),'false')<>'true' AND f.area='communications' AND f.channel='requirements'\n  AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Almacén'\n  AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL\n  AND JSON_VALUE(f.metadata,'$.status') NOT IN('draft','active')\n  AND r.dismissed_at IS NULL\nORDER BY f.id DESC;", connection))
			{
				command.Parameters.AddWithValue("@user", actor.Id);
				List<object> result = new List<object>();
				SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				List<object> list;
				try
				{
					while (await reader.ReadAsync(ct))
					{
						long @int = reader.GetInt64(0);
						result.Add(new
						{
							kind = "TICKET",
							id = @int.ToString(),
							ticketNumber = @int,
							ticketType = "WAREHOUSE",
							codigo = Text(4),
							terminal = Text(2),
							grupo = "Almacén",
							assignedDepartment = "WAREHOUSE",
							subject = Text(1),
							resolution = (string)null,
							status = Text(6),
							updatedAt = Text(5),
							eventType = "CREATED",
							message = "Nueva solicitud de " + Text(2) + " · " + Text(3),
							actorName = Text(3),
							notificationId = @int.ToString(),
							isRead = !reader.IsDBNull(7)
						});
					}
					list = result;
				}
				finally
				{
					if (reader != null)
					{
						await reader.DisposeAsync();
					}
				}
				list2 = list;
				string Text(int i)
				{
					if (!reader.IsDBNull(i))
					{
						return reader.GetString(i);
					}
					return "";
				}
			}
			result2 = list2;
		}
		return result2;
	}

	public async Task<string?> WarehouseRemoveProduct(DocumentActor actor, WarehouseProductRemoval input, CancellationToken ct)
	{
		if (!actor.CanDeleteInventory)
		{
			return "No tienes permiso para eliminar artículos.";
		}
		if (string.IsNullOrWhiteSpace(input.ProductId) || input.ProductId.Length > 40 || input.ExpectedQuantity < 0 || string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5 || input.Reason.Length > 500)
		{
			return "Indica el artículo y un motivo de 5 a 500 caracteres.";
		}
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureMaintenanceSchema(connection, ct);
			await EnsureWarehouseFulfillmentSchema(connection, ct);
			await using (SqlCommand schema = new SqlCommand("IF OBJECT_ID(N'dbo.warehouse_product_removals',N'U') IS NULL\nCREATE TABLE dbo.warehouse_product_removals(\n  id bigint IDENTITY PRIMARY KEY,product_id nvarchar(40) NOT NULL,\n  description nvarchar(240) NOT NULL,available_quantity int NOT NULL,\n  actor_id nvarchar(80) NOT NULL,actor_login nvarchar(160) NOT NULL,\n  reason nvarchar(500) NOT NULL,removed_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());", connection))
			{
				await schema.ExecuteNonQueryAsync(ct);
			}
			string text3;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand schema = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.admin_users WITH(HOLDLOCK) WHERE id=@id AND LOWER(email)=LOWER(@login) AND is_active=1 AND (role='WAREHOUSE' OR support_team='WAREHOUSE')", connection, tx))
				{
					schema.Parameters.AddWithValue("@id", actor.Id);
					schema.Parameters.AddWithValue("@login", actor.Login);
					if (Convert.ToInt64(await schema.ExecuteScalarAsync(ct)) != 1)
					{
						text = "La cuenta ya no tiene acceso a esta operación.";
						goto IL_064d;
					}
				}
				text = null;
				string name;
				int available;
				await using (SqlCommand schema = new SqlCommand("SELECT p.description,p.official_stock+\n  COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END)\n    FROM dbo.maintenance_movements m WITH(HOLDLOCK) WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id)),0)\nFROM dbo.warehouse_products p WITH(UPDLOCK,HOLDLOCK) WHERE p.product_id=@product AND p.is_active=1;", connection, tx))
				{
					schema.Parameters.AddWithValue("@product", input.ProductId.Trim());
					string text2;
					await using (SqlDataReader reader = await schema.ExecuteReaderAsync(ct))
					{
						if (!(await reader.ReadAsync(ct)))
						{
							text2 = "El artículo ya no está activo. Actualiza el inventario.";
							goto IL_08c2;
						}
						name = reader.GetString(0);
						available = Math.Max(0, reader.GetInt32(1));
					}
					goto end_IL_06b2;
					IL_08c2:
					text = text2;
					goto IL_09a1;
					end_IL_06b2:;
				}
				text = null;
				await using (SqlCommand schema = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE status='PENDING_PICKUP' AND (product_id=@product OR (product_id IS NULL AND LOWER(equipment_name)=LOWER(@name)))", connection, tx))
				{
					schema.Parameters.AddWithValue("@product", input.ProductId.Trim());
					schema.Parameters.AddWithValue("@name", name);
					if (Convert.ToInt64(await schema.ExecuteScalarAsync(ct)) > 0)
					{
						text = "Este artículo tiene unidades reservadas pendientes de retiro. No se puede eliminar.";
						goto IL_0b7c;
					}
				}
				if (available != input.ExpectedQuantity)
				{
					text3 = "Las existencias cambiaron. Actualiza el inventario antes de eliminar.";
				}
				else
				{
					await using (SqlCommand schema = new SqlCommand("INSERT dbo.warehouse_product_removals(product_id,description,available_quantity,actor_id,actor_login,reason)\n  VALUES(@product,@name,@quantity,@actor,@login,@reason);\nUPDATE dbo.warehouse_products SET is_active=0 WHERE product_id=@product AND is_active=1;", connection, tx))
					{
						schema.Parameters.AddWithValue("@product", input.ProductId.Trim());
						schema.Parameters.AddWithValue("@name", name);
						schema.Parameters.AddWithValue("@quantity", available);
						schema.Parameters.AddWithValue("@actor", actor.Id);
						schema.Parameters.AddWithValue("@login", actor.Login);
						schema.Parameters.AddWithValue("@reason", input.Reason.Trim());
						await schema.ExecuteNonQueryAsync(ct);
					}
					await tx.CommitAsync(ct);
					text3 = null;
				}
				goto end_IL_049b;
				IL_09a1:
				text3 = text;
				goto end_IL_049b;
				IL_064d:
				text3 = text;
				goto end_IL_049b;
				IL_0b7c:
				text3 = text;
				end_IL_049b:;
			}
			result = text3;
		}
		return result;
	}

	internal static async Task CleanupCompletedWarehouseRequests(SqlConnection connection, CancellationToken ct)
	{
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		await using SqlCommand command = new SqlCommand("DECLARE @lock int;\nEXEC @lock=sys.sp_getapplock @Resource='WarehouseRequestCleanup',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 THROW 50000,'No se pudo preparar la limpieza de solicitudes',1;\nIF NOT EXISTS(SELECT 1 FROM dbo.application_one_time_actions WITH(UPDLOCK,HOLDLOCK) WHERE action_key=@key)\nBEGIN\n  IF OBJECT_ID('dbo.warehouse_request_cleanup_backup','U') IS NULL\n    CREATE TABLE dbo.warehouse_request_cleanup_backup(\n      cleanup_key varchar(100) NOT NULL,document_id bigint NOT NULL,metadata nvarchar(max) NOT NULL,\n      file_data varbinary(max) NOT NULL,allocations_json nvarchar(max) NULL,\n      archived_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),PRIMARY KEY(cleanup_key,document_id));\n  INSERT dbo.warehouse_request_cleanup_backup(cleanup_key,document_id,metadata,file_data)\n    SELECT @key,id,metadata,file_data FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK)\n    WHERE area='communications' AND channel='requirements'\n      AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'\n      AND COALESCE(JSON_VALUE(metadata,'$.trashed'),'false')<>'true'\n      AND (COALESCE(JSON_VALUE(metadata,'$.status'),'') NOT IN('pending','received','correction_requested')\n           OR JSON_QUERY(metadata,'$.warehouseDelivery') IS NOT NULL);\n  IF OBJECT_ID('dbo.warehouse_equipment_allocations','U') IS NOT NULL\n  BEGIN\n    EXEC sys.sp_executesql N'\n      UPDATE b SET allocations_json=(SELECT a.* FROM dbo.warehouse_equipment_allocations a WHERE a.document_id=b.document_id FOR JSON PATH)\n        FROM dbo.warehouse_request_cleanup_backup b WHERE b.cleanup_key=@key;',N'@key varchar(100)',@key;\n    IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.warehouse_equipment_allocations') AND name='CK_warehouse_allocation_status')\n      ALTER TABLE dbo.warehouse_equipment_allocations DROP CONSTRAINT CK_warehouse_allocation_status;\n    ALTER TABLE dbo.warehouse_equipment_allocations WITH CHECK ADD CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'));\n    EXEC sys.sp_executesql N'\n      UPDATE a SET status=''CANCELLED'' FROM dbo.warehouse_equipment_allocations a\n        JOIN dbo.warehouse_request_cleanup_backup b ON b.document_id=a.document_id\n        WHERE b.cleanup_key=@key AND a.status=''PENDING_PICKUP'';',N'@key varchar(100)',@key;\n  END;\n  UPDATE f SET metadata=JSON_MODIFY(f.metadata,'$.trashed',CAST(1 AS bit))\n    FROM dbo.institutional_files f JOIN dbo.warehouse_request_cleanup_backup b ON b.document_id=f.id WHERE b.cleanup_key=@key;\n  INSERT dbo.application_one_time_actions(action_key) VALUES(@key);\nEND;", connection, tx);
		command.Parameters.AddWithValue("@key", "V418_20260928_KEEP_PENDING_WAREHOUSE_REQUESTS");
		await command.ExecuteNonQueryAsync(ct);
		await tx.CommitAsync(ct);
	}

	public async Task<List<object>> WarehouseTrace(string code, DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Allowed || string.IsNullOrWhiteSpace(code) || code.Length > 2048)
		{
			return new List<object>();
		}
		string token = WarehouseQrToken(code);
		string text = MaintenanceTraceCode.Normalize(code);
		string number = (text.StartsWith("REAL-MNT|", StringComparison.Ordinal) ? text.Substring(9) : text);
		string compactNumber = Regex.Replace(number, "^(SOL-ALM-\\d{8})-(\\d{6}-)", "$1$2");
		List<object> result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			List<object> list;
			await using (SqlCommand command = new SqlCommand("SELECT TOP 1 id FROM dbo.institutional_files WHERE area='communications' AND channel='requirements'\n  AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'\n  AND ((@token<>'' AND JSON_VALUE(metadata,'$.verificationToken')=@token)\n    OR JSON_VALUE(metadata,'$.warehouseRequest.number') IN(@number,@compact)\n    OR JSON_VALUE(metadata,'$.originalFileName') IN(@number+'.pdf',@compact+'.pdf')) ORDER BY id DESC;", connection))
			{
				command.Parameters.AddWithValue("@token", token ?? "");
				command.Parameters.AddWithValue("@number", number);
				command.Parameters.AddWithValue("@compact", compactNumber);
				object obj = await command.ExecuteScalarAsync(ct);
				InstitutionalFile d;
				string ticketStatus;
				string ticketDepartment;
				List<WarehouseTraceEvent> events;
				string text2;
				string status;
				if (obj == null)
				{
					list = new List<object>();
				}
				else
				{
					d = await InstitutionalDetail(Convert.ToInt64(obj), actor, ct);
					if (d != null && !d.Trashed && (object)d.WarehouseRequest != null)
					{
						ticketStatus = null;
						ticketDepartment = null;
						Guid? ticketId = d.WarehouseRequest.TicketId;
						if (ticketId.HasValue)
						{
							Guid valueOrDefault = ticketId.GetValueOrDefault();
							await using SqlCommand ticket = new SqlCommand("SELECT status,assigned_department FROM dbo.support_tickets WHERE id=@ticket", connection);
							ticket.Parameters.AddWithValue("@ticket", valueOrDefault);
							await using SqlDataReader ticketReader = await ticket.ExecuteReaderAsync(ct);
							if (await ticketReader.ReadAsync(ct))
							{
								ticketStatus = ticketReader.GetString(0);
								ticketDepartment = ticketReader.GetString(1);
							}
						}
						events = new List<WarehouseTraceEvent>();
						Add("CREATED", "SENT", "Solicitud registrada", d.OwnerDisplayName, d.OwnerLogin, d.CreatedUtc);
						long? ticketNumber = d.WarehouseRequest.TicketNumber;
						if (ticketNumber.HasValue)
						{
							long valueOrDefault2 = ticketNumber.GetValueOrDefault();
							Add("TICKET_CREATED", ticketStatus ?? "OPEN", $"Ticket #{valueOrDefault2} · {d.WarehouseRequest.FailureDescription ?? "Falla registrada"}", d.OwnerDisplayName, d.OwnerLogin, d.CreatedUtc);
						}
						Add("RECEIVED_WAREHOUSE", "RECEIVED", "Solicitud recibida en Almacén", d.ReceiverDisplayName, d.ReceiverLogin, d.ReceivedUtc);
						status = d.Status;
						if ((status == "approved" || status == "rejected") ? true : false)
						{
							Add((d.Status == "approved") ? "APPROVED" : "REJECTED", (d.Status == "approved") ? "APPROVED" : "REJECTED", "Decisión registrada", d.DecisionBy, null, d.DecisionUtc);
						}
						Add("DISPATCHED", "DISPATCH", "Equipo y serial asignados", d.WarehousePreparedBy, d.WarehousePreparedLogin, d.WarehousePreparedUtc);
						WarehouseDeliveryInfo warehouseDelivery = d.WarehouseDelivery;
						if ((object)warehouseDelivery != null)
						{
							Add("WITHDRAWN", "DELIVERED", "Retirado por " + warehouseDelivery.ReceivedBy, warehouseDelivery.DeliveredBy, warehouseDelivery.DeliveredLogin, warehouseDelivery.At);
						}
						WarehouseInstallationInfo warehouseInstallation = d.WarehouseInstallation;
						if ((object)warehouseInstallation != null)
						{
							Add("INSTALLED", "INSTALLED", warehouseInstallation.Resolution, warehouseInstallation.ResolvedBy, null, warehouseInstallation.ResolvedAt);
						}
						if (d.Status == "cancelled")
						{
							Add("CANCELLED", "CANCELLED", d.CorrectionNote ?? "Solicitud anulada", d.DecisionBy, null, d.DecisionUtc);
						}
						if (d.Status == "cancelled")
						{
							text2 = "CANCELLED";
						}
						else if ((object)d.WarehouseInstallation != null)
						{
							text2 = "INSTALLED";
						}
						else if ((object)d.WarehouseDelivery != null)
						{
							text2 = "DELIVERED";
						}
						else if (d.Status == "rejected")
						{
							text2 = "REJECTED";
						}
						else if (d.WarehousePreparedUtc.HasValue)
						{
							text2 = "DISPATCH";
						}
						else if (d.Status == "approved")
						{
							text2 = "APPROVED";
						}
						else
						{
							text2 = (d.ReceivedUtc.HasValue ? "RECEIVED" : "SENT");
						}
						if (text2 == null)
						{
							goto IL_0c74;
						}
						int length = text2.Length;
						if (length != 8)
						{
							if (length != 9)
							{
								goto IL_0c74;
							}
							char c = text2[0];
							if (c != 'C')
							{
								if (c != 'D')
								{
									if (c != 'I' || !(text2 == "INSTALLED"))
									{
										goto IL_0c74;
									}
									status = "Instalado y comprobado";
								}
								else
								{
									if (!(text2 == "DELIVERED"))
									{
										goto IL_0c74;
									}
									status = "Equipo retirado y entregado";
								}
							}
							else
							{
								if (!(text2 == "CANCELLED"))
								{
									goto IL_0c74;
								}
								status = "Solicitud anulada";
							}
						}
						else
						{
							char c = text2[2];
							if ((uint)c <= 74u)
							{
								if (c != 'C')
								{
									if (c != 'J' || !(text2 == "REJECTED"))
									{
										goto IL_0c74;
									}
									status = "Solicitud rechazada";
								}
								else
								{
									if (!(text2 == "RECEIVED"))
									{
										goto IL_0c74;
									}
									status = "Recibida · pendiente de decisión";
								}
							}
							else if (c != 'P')
							{
								if (c != 'S' || !(text2 == "DISPATCH"))
								{
									goto IL_0c74;
								}
								status = "Equipo asignado · pendiente de retiro";
							}
							else
							{
								if (!(text2 == "APPROVED"))
								{
									goto IL_0c74;
								}
								status = "Autorizada · pendiente de asignación";
							}
						}
						goto IL_0c7b;
					}
					list = new List<object>();
				}
				goto end_IL_021d;
				IL_0c7b:
				string statusText = status;
				list = new List<object>(1)
				{
					new
					{
						id = d.Id.ToString(),
						documentNumber = d.WarehouseRequest.Number,
						equipmentType = string.Join(" · ", d.WarehouseRequest.Lines.Select((WarehouseRequestLine l) => l.Name)),
						serialNumber = string.Join(", ", d.WarehouseAssigned.Select((WarehouseAssignedEquipment a) => a.Serial)),
						department = d.Department,
						movementType = "REQUEST",
						operationalArea = "WAREHOUSE",
						status = text2,
						statusText = statusText,
						sentAt = d.CreatedUtc,
						senderName = d.OwnerDisplayName,
						receivedByName = d.ReceiverDisplayName,
						events = events,
						requiresInstallation = d.WarehouseRequest.TicketId.HasValue,
						ticketNumber = d.WarehouseRequest.TicketNumber,
						ticketStatus = ticketStatus,
						ticketDepartment = ticketDepartment,
						failureCategory = d.WarehouseRequest.FailureCategory
					}
				};
				goto end_IL_021d;
				IL_0c74:
				status = "Pendiente de recepción";
				goto IL_0c7b;
				end_IL_021d:
				void Add(string action, string status2, string note, string? name, string? login, DateTime? time)
				{
					if (time.HasValue)
					{
						DateTime valueOrDefault3 = time.GetValueOrDefault();
						events.Add(new WarehouseTraceEvent($"{d.Id}-{action}", action, status2, note, name ?? "", login ?? "", valueOrDefault3));
					}
				}
			}
			result = list;
		}
		return result;
	}

	public async Task<string> DepartmentOperationalAudit(string scope, DateTime from, DateTime to, int offset, CancellationToken ct)
	{
		SqlConnection c = await Open(ct);
		string result;
		try
		{
			List<string> queries = new List<string>();
			if (await Exists("maintenance_movements"))
			{
				queries.Add("SELECT CONCAT('movement:',m.id) id,m.created_at at,m.created_by_name actor,m.created_by_user_id login,m.operational_area module,m.movement_type action,m.document_number reference,CONCAT(m.equipment_type,N' · ',m.serial_number,N' · ',m.failure_cause,N' · ',m.notes) detail\r\nFROM dbo.maintenance_movements m WHERE @scope='ALL' OR (@scope='WAREHOUSE' AND m.operational_area='WAREHOUSE') OR (@scope IN('WORKSHOP','TECHNICAL_FAILURE') AND m.operational_area='WORKSHOP') OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND m.department=@scope)");
			}
			if (await Exists("maintenance_workshop_events"))
			{
				queries.Add("SELECT CONCAT('work:',e.id),e.occurred_at,e.actor_name,e.actor_login,'WORKSHOP',e.action,m.document_number,CONCAT(e.status,N' · ',e.note)\r\nFROM dbo.maintenance_workshop_events e JOIN dbo.maintenance_movements m ON m.id=e.source_id\r\nWHERE @scope IN('ALL','WORKSHOP','TECHNICAL_FAILURE') OR (@scope='WAREHOUSE' AND e.action IN('DELIVER','WAREHOUSE_RECEIVE')) OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND m.department=@scope)");
			}
			if (await Exists("workshop_available_inventory"))
			{
				queries.Add("SELECT CONCAT('stock:',id),created_at,created_by_name,created_by,'WORKSHOP','AVAILABLE_ENTRY',serial,CONCAT(equipment,N' · ',notes) FROM dbo.workshop_available_inventory WHERE @scope IN('ALL','WORKSHOP','TECHNICAL_FAILURE')");
			}
			if (await Exists("support_ticket_history"))
			{
				queries.Add("SELECT CONCAT('ticket:',h.id),h.created_at,h.actor_name,COALESCE(u.email,h.actor_user_id),'SUPPORT',h.action,CONCAT('#',t.ticket_number),CONCAT(t.subject,N' · ',h.comment,N' · Ruta: ',COALESCE(h.from_team,h.from_department,N'Origen'),N' → ',COALESCE(h.to_team,h.to_department,t.assigned_team,t.assigned_department,N'Sin destino'))\r\nFROM dbo.support_ticket_history h JOIN dbo.support_tickets t ON t.id=h.ticket_id LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=h.actor_user_id\r\nWHERE @scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND COALESCE(h.to_department,t.assigned_department)=@scope)\r\n OR (@scope='CALL_CENTER' AND (\r\n   h.from_team='CALL_CENTER' OR h.to_team='CALL_CENTER' OR t.assigned_team='CALL_CENTER'\r\n   OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team='CALL_CENTER')))\r\n OR (@scope='TECHNICAL_FAILURE' AND (\r\n   h.from_team IN('TECHNICAL_FAILURE','TECHNICIANS') OR h.to_team IN('TECHNICAL_FAILURE','TECHNICIANS') OR t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS')\r\n   OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team IN('TECHNICAL_FAILURE','TECHNICIANS'))))\r\n OR (@scope='ADMINISTRATIVE' AND EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND (creator.role='ADMINISTRATIVE' OR creator.email IN('emma.mendoza','emma.mendoza@grupotejeda.local'))))\r\n OR (@scope='WAREHOUSE' AND EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND (creator.role='WAREHOUSE' OR creator.support_team='WAREHOUSE')))");
			}
			if (await Exists("workshop_stock_exits"))
			{
				queries.Add("SELECT CONCAT('stock-exit:',stock_id),created_at,actor_name,actor_id,'WORKSHOP','STOCK_EXIT',CONVERT(nvarchar(80),ticket_id),CONCAT(equipment,N' · ',serial)\r\nFROM dbo.workshop_stock_exits WHERE @scope IN('ALL','WORKSHOP','TECHNICAL_FAILURE','TECHNOLOGY')");
			}
			if (await Exists("automatic_findings"))
			{
				queries.Add("SELECT CONCAT('finding:',id),resolved_at,resolved_by_name,resolved_by_username,'FINDING',status,title,CONCAT(diagnosis,N' · ',resolution,N' · ',verification)\r\nFROM dbo.automatic_findings WHERE resolved_at IS NOT NULL AND (@scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND assigned_department=@scope) OR (@scope='TECHNICAL_FAILURE' AND assigned_department='TECHNOLOGY'))");
			}
			if (await Exists("automatic_finding_notifications"))
			{
				queries.Add("SELECT CONCAT('finding-event:',MIN(CONVERT(varchar(36),n.id))),n.created_at,n.actor_name,'','FINDING',n.event_type,f.title,n.message\r\nFROM dbo.automatic_finding_notifications n JOIN dbo.automatic_findings f ON f.id=n.finding_id\r\nWHERE @scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND f.assigned_department=@scope) OR (@scope='TECHNICAL_FAILURE' AND f.assigned_department='TECHNOLOGY')\r\nGROUP BY n.finding_id,n.created_at,n.actor_name,n.event_type,f.title,n.message");
			}
			if (await Exists("agency_transition_progress"))
			{
				queries.Add("SELECT CONCAT('transition:',id),created_at,created_by_name,created_by_user_id,'TRANSITION',progress_status,CONVERT(nvarchar(80),project_id),note FROM dbo.agency_transition_progress WHERE @scope='ALL' OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND department=@scope)");
			}
			if (await Exists("agency_profiles") && await Exists("agencies"))
			{
				queries.Add("SELECT CONCAT('field-audit:',p.id),p.submitted_at,COALESCE(JSON_VALUE(p.submitted_by_context,'$.displayName'),u.display_name),COALESCE(JSON_VALUE(p.submitted_by_context,'$.login'),u.email),'FIELD_AUDIT','PROFILE_CREATED',CONCAT(a.codigo,N' · ',a.terminal),CONCAT(N'Levantamiento de campo completado · ',p.municipio,N', ',p.provincia) FROM dbo.agency_profiles p JOIN dbo.agencies a ON a.id=p.agency_id LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId') WHERE @scope IN('ALL','SUPERVISION')");
			}
			if (await Exists("audit_log"))
			{
				queries.Add("SELECT CONCAT('operation-error:',l.id),l.created_at,COALESCE(u.display_name,l.actor_email),l.actor_email,'OPERATION_ERROR',l.action,JSON_VALUE(l.after_data,'$.reference'),JSON_VALUE(l.after_data,'$.detail') FROM dbo.audit_log l LEFT JOIN dbo.admin_users u ON LOWER(u.email)=LOWER(l.actor_email) WHERE l.entity_type='operational_error' AND (@scope='ALL' OR JSON_VALUE(l.after_data,'$.department')=@scope)");
			}
			if (await Exists("maintenance_document_signatures"))
			{
				queries.Add("SELECT CONCAT('signature:',s.movement_id,':',s.signature_role),s.signed_at,s.signer_name,s.signer_login,m.operational_area,CONCAT('SIGN_',s.signature_role),m.document_number,N'Firma registrada'\r\nFROM dbo.maintenance_document_signatures s JOIN dbo.maintenance_movements m ON m.id=s.movement_id WHERE @scope='ALL' OR (@scope='WAREHOUSE' AND m.operational_area='WAREHOUSE') OR (@scope IN('TECHNICAL_FAILURE','WORKSHOP') AND m.operational_area='WORKSHOP') OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') AND m.department=@scope)");
			}
			if (await Exists("institutional_files"))
			{
				queries.Add("SELECT CONCAT('document:',f.id),TRY_CONVERT(datetime2,JSON_VALUE(f.metadata,'$.createdUtc')),JSON_VALUE(f.metadata,'$.ownerDisplayName'),JSON_VALUE(f.metadata,'$.ownerLogin'),'DOCUMENT','CREATED',JSON_VALUE(f.metadata,'$.title'),JSON_VALUE(f.metadata,'$.description') FROM dbo.institutional_files f WHERE (@scope='ALL' OR (@scope='ADMINISTRATIVE' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Administración' AND JSON_QUERY(f.metadata,'$.administrativeRequest') IS NOT NULL) OR (@scope='WAREHOUSE' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Almacén' AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL) OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','SUPERVISION','ADMINISTRATIVE') AND f.department=@department))");
				queries.Add("SELECT CONCAT('document-event:',f.id,':',j.[key]),TRY_CONVERT(datetime2,JSON_VALUE(j.value,'$.at')),JSON_VALUE(j.value,'$.actor'),JSON_VALUE(j.value,'$.login'),'DOCUMENT',JSON_VALUE(j.value,'$.action'),JSON_VALUE(f.metadata,'$.title'),CONCAT(N'Estado anterior: ',JSON_VALUE(j.value,'$.previousStatus')) FROM dbo.institutional_files f CROSS APPLY OPENJSON(f.metadata,'$.history') j WHERE (@scope='ALL' OR (@scope='ADMINISTRATIVE' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Administración' AND JSON_QUERY(f.metadata,'$.administrativeRequest') IS NOT NULL) OR (@scope='WAREHOUSE' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Almacén' AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL) OR (@scope IN('ADMINISTRATIVE','COMMUNICATION','COMMERCIALIZATION','TREASURY','ACCOUNTING','GENERAL_AUDIT','VAULT','CASHIER','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES','SUPERVISION','ADMINISTRATIVE') AND f.department=@department))");
			}
			if (queries.Count == 0)
			{
				result = "[]";
			}
			else
			{
				IEnumerable<string> normalized = queries.Select((string q) => "SELECT id COLLATE DATABASE_DEFAULT id,at,actor COLLATE DATABASE_DEFAULT actor,login COLLATE DATABASE_DEFAULT login,module COLLATE DATABASE_DEFAULT module,action COLLATE DATABASE_DEFAULT action,reference COLLATE DATABASE_DEFAULT reference,detail COLLATE DATABASE_DEFAULT detail FROM (" + q + ") source(id,at,actor,login,module,action,reference,detail)");
				string text = ((await Exists("admin_users")) ? "EXISTS(SELECT 1 FROM dbo.admin_users au WHERE (REPLACE(LOWER(LTRIM(RTRIM(au.email))),'@grupotejeda.local','')=REPLACE(LOWER(LTRIM(RTRIM(activity.login))),'@grupotejeda.local','') OR CONVERT(nvarchar(80),au.id)=activity.login) AND (au.role IN('ADMINISTRATIVE','ADMINISTRATOR') OR (au.role='DEPARTMENT' AND au.support_team='ADMINISTRATIVE')) AND COALESCE(au.support_team,'') NOT IN('WAREHOUSE','WORKSHOP'))" : "1=0");
				string cmdText = "WITH activity AS (" + string.Join(" UNION ALL ", normalized) + ") SELECT id,at,actor,login,module,action,reference,detail FROM activity WHERE at>=@from AND at<@to AND (@scope<>'ADMINISTRATIVE' OR (module NOT IN('WAREHOUSE','WORKSHOP') AND " + text + ")) ORDER BY at DESC,id DESC OFFSET @offset ROWS FETCH NEXT 201 ROWS ONLY FOR JSON PATH";
				string text3;
				await using (SqlCommand cmd = new SqlCommand(cmdText, c))
				{
					cmd.Parameters.AddWithValue("@scope", scope);
					cmd.Parameters.AddWithValue("@department", DepartmentAccounts.Names.GetValueOrDefault(scope, ""));
					cmd.Parameters.AddWithValue("@from", from);
					cmd.Parameters.AddWithValue("@to", to);
					cmd.Parameters.AddWithValue("@offset", offset);
					StringBuilder json = new StringBuilder();
					string text2;
					await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
					{
						while (await reader.ReadAsync(ct))
						{
							json.Append(reader.GetString(0));
						}
						text2 = ((json.Length == 0) ? "[]" : json.ToString());
					}
					text3 = text2;
				}
				result = text3;
			}
		}
		finally
		{
			if (c != null)
			{
				await c.DisposeAsync();
			}
		}
		return result;
		async Task<bool> Exists(string table)
		{
			bool result2;
			await using (SqlCommand check = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@table,N'U') IS NULL THEN 0 ELSE 1 END", c))
			{
				check.Parameters.AddWithValue("@table", "dbo." + table);
				result2 = Convert.ToInt32(await check.ExecuteScalarAsync(ct)) == 1;
			}
			return result2;
		}
	}

	public async Task<string> DepartmentSupport(string scope, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			string text2;
			await using (SqlCommand cmd = new SqlCommand("SELECT t.id,t.ticket_number ticketNumber,t.subject,t.description,t.category,t.priority,t.status,t.ticket_type ticketType,\n t.assigned_department assignedDepartment,t.assigned_team assignedTeam,t.created_by_name createdByName,\n COALESCE(NULLIF(t.resolved_by_name,N''),resolver.display_name) resolvedByName,\n technician.display_name assignedTechnicianName,t.created_at createdAt,t.updated_at updatedAt,\n COALESCE(a.codigo,'') codigo,COALESCE(a.terminal,'') terminal,COALESCE(a.grupo,'') grupo,\n JSON_QUERY((SELECT h.id,h.action,h.actor_name actorName,h.comment,h.from_department fromDepartment,\n   h.to_department toDepartment,h.from_team fromTeam,h.to_team toTeam,h.created_at createdAt\n   FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id ORDER BY h.created_at,h.id FOR JSON PATH)) history\nFROM dbo.support_tickets t\nLEFT JOIN dbo.agencies a ON a.id=t.agency_id\nLEFT JOIN dbo.admin_users technician ON CONVERT(nvarchar(80),technician.id)=t.assigned_technician_id\nLEFT JOIN dbo.admin_users resolver ON CONVERT(nvarchar(80),resolver.id)=t.resolved_by_user_id\nWHERE (@scope NOT IN('WAREHOUSE','WORKSHOP','CALL_CENTER','TECHNICAL_FAILURE') AND t.assigned_department=@scope)\n OR (@scope='CALL_CENTER' AND (\n   t.assigned_team='CALL_CENTER'\n   OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team='CALL_CENTER')\n   OR EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id AND (h.from_team='CALL_CENTER' OR h.to_team='CALL_CENTER'))))\n OR (@scope='TECHNICAL_FAILURE' AND (\n   t.assigned_team IN('TECHNICAL_FAILURE','TECHNICIANS')\n   OR EXISTS(SELECT 1 FROM dbo.admin_users creator WHERE CONVERT(nvarchar(80),creator.id)=t.created_by_user_id AND creator.support_team IN('TECHNICAL_FAILURE','TECHNICIANS'))\n   OR EXISTS(SELECT 1 FROM dbo.support_ticket_history h WHERE h.ticket_id=t.id AND (h.from_team IN('TECHNICAL_FAILURE','TECHNICIANS') OR h.to_team IN('TECHNICAL_FAILURE','TECHNICIANS')))))\n OR (@scope IN('WAREHOUSE','WORKSHOP') AND (t.assigned_department=@scope OR EXISTS(SELECT 1 FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=t.created_by_user_id AND (u.support_team=@scope OR u.role=@scope))))\nORDER BY t.updated_at DESC FOR JSON PATH", c))
			{
				cmd.Parameters.AddWithValue("@scope", scope);
				StringBuilder json = new StringBuilder();
				string text;
				await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
				{
					while (await r.ReadAsync(ct))
					{
						json.Append(r.GetString(0));
					}
					text = ((json.Length == 0) ? "[]" : json.ToString());
				}
				text2 = text;
			}
			result = text2;
		}
		return result;
	}

	public async Task<string> DepartmentSupportMembers(string scope, CancellationToken ct)
	{
		return await OperationalDirectoryMembers(scope, ct);
	}

	private async Task AlignEnedraineWithoutSupportReference(List<AdminUserRow> users, Func<AdminUserRow, AdminUserRow> effective, CancellationToken ct)
	{
		if (namedCallCenterCorrectionReady)
		{
			return;
		}
		List<AdminUserRow> targets = users.Where((AdminUserRow u) => EnedraineCallCenterProfile.IsTarget(u.Email)).ToList();
		if (targets.Count != 1)
		{
			return;
		}
		Dictionary<string, bool> selected = (await UserAccessPresets(ct)).SingleOrDefault((UserAccessPreset p) => p.Department == "CALL_CENTER" && p.Profile == "SUPPORT")?.Permissions ?? EnedraineCallCenterProfile.DefaultPermissions();
		Dictionary<string, bool> permissions = SupportProfilePolicy.ForSave(selected, null);
		Dictionary<string, bool> reference = effective(targets[0] with
		{
			Role = "TECHNOLOGY",
			SupportTeam = "CALL_CENTER",
			Permissions = permissions
		}).EffectivePermissions;
		await using SqlConnection connection = await Open(ct);
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		try
		{
			await using (SqlCommand guard = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='unified-support-profile',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @result<0 THROW 50001,'No se pudo bloquear la actualización de perfiles.',1;", connection, tx))
			{
				await guard.ExecuteNonQueryAsync(ct);
			}
			await AlignEnedraineCallCenter(connection, tx, reference, ct);
			await tx.CommitAsync(ct);
			namedCallCenterCorrectionReady = true;
		}
		catch
		{
			await tx.RollbackAsync(ct);
			throw;
		}
	}

	internal static async Task<Guid?> AlignEnedraineCallCenter(SqlConnection connection, SqlTransaction transaction, Dictionary<string, bool> reference, CancellationToken ct)
	{
		List<(Guid Id, string Role, string? Team, string? Raw)> matches = new List<(Guid, string, string, string)>();
		await using (SqlCommand query = new SqlCommand("SELECT id,role,support_team,permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE LOWER(LTRIM(RTRIM(email))) IN(@email,N'enedraine.elisabeth')", connection, transaction))
		{
			query.Parameters.AddWithValue("@email", "enedraine.elisabeth@grupotejeda.local");
			await using SqlDataReader reader = await query.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				matches.Add((Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
			}
		}
		if (matches.Count == 0)
		{
			return null;
		}
		if (matches.Count != 1)
		{
			throw new InvalidOperationException("Hay más de una cuenta de Enedraine Elisabeth; no se modificó su perfil.");
		}
		(Guid Id, string Role, string? Team, string? Raw) user = matches[0];
		await using (SqlCommand query = new SqlCommand("IF OBJECT_ID(N'dbo.call_center_profile_corrections',N'U') IS NULL\r\n    CREATE TABLE dbo.call_center_profile_corrections(\r\n        migration_key varchar(80) NOT NULL PRIMARY KEY,user_id nvarchar(80) NOT NULL,\r\n        previous_role varchar(40) NOT NULL,previous_team varchar(40) NULL,\r\n        previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", connection, transaction))
		{
			await query.ExecuteNonQueryAsync(ct);
		}
		Guid? result;
		await using (SqlCommand query = new SqlCommand("SELECT COUNT(*) FROM dbo.call_center_profile_corrections WHERE migration_key='V439_ENEDRAINE_CALL_CENTER'", connection, transaction))
		{
			if (Convert.ToInt32(await query.ExecuteScalarAsync(ct)) > 0)
			{
				result = null;
				return result;
			}
		}
		Dictionary<string, bool> original = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.Raw ?? "{}") ?? new Dictionary<string, bool>();
		Dictionary<string, bool> value = CallCenterSupportProfile.Normalize(original);
		await using (SqlCommand update = new SqlCommand("INSERT dbo.call_center_profile_corrections(migration_key,user_id,previous_role,previous_team,previous_permissions)\r\nVALUES('V439_ENEDRAINE_CALL_CENTER',@id,@role,@team,@previous);\r\nUPDATE dbo.admin_users SET role='TECHNOLOGY',support_team='CALL_CENTER',permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction))
		{
			update.Parameters.AddWithValue("@id", user.Id.ToString());
			update.Parameters.AddWithValue("@role", user.Role);
			update.Parameters.AddWithValue("@team", ((object)user.Team) ?? ((object)DBNull.Value));
			update.Parameters.AddWithValue("@previous", ((object)user.Raw) ?? ((object)DBNull.Value));
			update.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(value));
			await update.ExecuteNonQueryAsync(ct);
			result = user.Id;
		}
		return result;
	}

	public async Task<ExecutiveReportResult> ExecutiveReport(string period, CancellationToken ct)
	{
		period = "global";
		DateTime from = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		DateTime to = DateTime.UtcNow;
		ExecutiveReportResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			List<ExecutiveCaseRow> tickets = new List<ExecutiveCaseRow>();
			List<ExecutiveCaseRow> findings = new List<ExecutiveCaseRow>();
			List<ExecutiveFindingPersonRow> findingPeople = new List<ExecutiveFindingPersonRow>();
			List<ExecutiveWarehouseRow> warehouse = new List<ExecutiveWarehouseRow>();
			List<ExecutiveWorkshopRow> workshop = new List<ExecutiveWorkshopRow>();
			List<ExecutiveAuditGroup> auditGroups = new List<ExecutiveAuditGroup>();
			List<ExecutiveAuditAgency> agencies = new List<ExecutiveAuditAgency>();
			await using (SqlCommand command = new SqlCommand("SELECT ISNULL(NULLIF(assigned_department,''),'SIN_DEPARTAMENTO'),ISNULL(NULLIF(assigned_team,''),'GENERAL'),status,COUNT(*)\nFROM dbo.support_tickets\nWHERE created_at<=@to\nGROUP BY assigned_department,assigned_team,status ORDER BY assigned_department,assigned_team,status;", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					tickets.Add(new ExecutiveCaseRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
				}
			}
			await using (SqlCommand command = new SqlCommand("SELECT ISNULL(NULLIF(assigned_department,''),'SIN_DEPARTAMENTO'),'HALLAZGOS',status,COUNT(*)\nFROM dbo.automatic_findings\nWHERE created_at<=@to\nGROUP BY assigned_department,status ORDER BY assigned_department,status;", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					findings.Add(new ExecutiveCaseRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
				}
			}
			await using (SqlCommand command = new SqlCommand("SELECT role_name,person_name,department,status,SUM(total) total\nFROM (\n  SELECT 'SUPERVISOR' role_name,\n         COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.displayName'),''),NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),N'Sin identificar') person_name,\n         af.assigned_department department,af.status,COUNT(*) total\n  FROM dbo.automatic_findings af INNER JOIN dbo.agency_profiles p ON p.id=af.profile_id\n  WHERE af.created_at<=@to\n  GROUP BY COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.displayName'),''),NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),N'Sin identificar'),af.assigned_department,af.status\n  UNION ALL\n  SELECT CASE WHEN ISNULL(u.support_team,'')='TECHNICIANS' OR ISNULL(u.role,'') IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') THEN 'TECNICO' ELSE 'SOPORTE' END,\n         COALESCE(NULLIF(af.resolved_by_name,''),NULLIF(u.display_name,''),NULLIF(u.email,''),NULLIF(af.assigned_technician_id,''),N'Sin asignar'),\n         af.assigned_department,af.status,COUNT(*)\n  FROM dbo.automatic_findings af LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=af.assigned_technician_id\n  WHERE af.created_at<=@to AND (NULLIF(af.assigned_technician_id,'') IS NOT NULL OR NULLIF(af.resolved_by_name,'') IS NOT NULL)\n  GROUP BY CASE WHEN ISNULL(u.support_team,'')='TECHNICIANS' OR ISNULL(u.role,'') IN('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES') THEN 'TECNICO' ELSE 'SOPORTE' END,\n           COALESCE(NULLIF(af.resolved_by_name,''),NULLIF(u.display_name,''),NULLIF(u.email,''),NULLIF(af.assigned_technician_id,''),N'Sin asignar'),af.assigned_department,af.status\n) detail\nGROUP BY role_name,person_name,department,status\nORDER BY role_name,person_name,department,status;", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					findingPeople.Add(new ExecutiveFindingPersonRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4)));
				}
			}
			await using (SqlCommand command = new SqlCommand("SELECT ISNULL(NULLIF(department,''),N'Sin departamento'),\n       ISNULL(NULLIF(JSON_VALUE(metadata,'$.status'),''),'pending'),COUNT(*)\nFROM dbo.institutional_files\nWHERE area='communications' AND channel='requirements'\n  AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL\n  AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'\n  AND COALESCE(TRY_CONVERT(datetime2,JSON_VALUE(metadata,'$.createdUtc')),TRY_CONVERT(datetime2,JSON_VALUE(metadata,'$.sentUtc')),'2000-01-01')<=@to\nGROUP BY department,JSON_VALUE(metadata,'$.status') ORDER BY department,JSON_VALUE(metadata,'$.status');", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					warehouse.Add(new ExecutiveWarehouseRow(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
				}
			}
			await using (SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.maintenance_movements',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.maintenance_workshop_cases',N'U') IS NOT NULL\nBEGIN\n  SELECT m.department,COALESCE(c.status,'SENT'),COUNT(*) equipment,\n         SUM(1+COALESCE(activity.total,0)) movements\n  FROM dbo.maintenance_movements m\n  LEFT JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id\n  OUTER APPLY(SELECT COUNT(*) total FROM dbo.maintenance_workshop_events e WHERE e.source_id=m.id AND e.occurred_at<=@to) activity\n  WHERE m.created_at<=@to AND (m.movement_type='TRANSFER_TO_WORKSHOP' OR\n        (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' AND NOT EXISTS(\n          SELECT 1 FROM dbo.maintenance_workshop_events child WHERE child.movement_id=m.id)))\n  GROUP BY m.department,COALESCE(c.status,'SENT')\n  ORDER BY m.department,COALESCE(c.status,'SENT');\nEND", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					workshop.Add(new ExecutiveWorkshopRow(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), Convert.ToInt32(reader.GetValue(3))));
				}
			}
			await using (SqlCommand command = new SqlCommand("SELECT a.grupo,COUNT(*),\n       SUM(CASE WHEN a.status='COMPLETED' AND p.id IS NOT NULL THEN 1 ELSE 0 END),\n       SUM(CASE WHEN a.status='PENDING' OR (a.status='COMPLETED' AND p.id IS NULL) THEN 1 ELSE 0 END),\n       SUM(CASE WHEN a.status='REVIEW_REQUIRED' THEN 1 ELSE 0 END),\n       SUM(CASE WHEN a.status='COMPLETED' AND p.id IS NOT NULL THEN 1 ELSE 0 END)\nFROM dbo.agencies a LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id AND p.submitted_at<=@to\nWHERE a.is_active=1 GROUP BY a.grupo ORDER BY a.grupo;", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					auditGroups.Add(new ExecutiveAuditGroup(reader.GetString(0), reader.GetInt32(1), Convert.ToInt32(reader.GetValue(2)), Convert.ToInt32(reader.GetValue(3)), Convert.ToInt32(reader.GetValue(4)), Convert.ToInt32(reader.GetValue(5))));
				}
			}
			await using (SqlCommand command = new SqlCommand("SELECT a.codigo,a.terminal,a.grupo,CASE WHEN a.status='COMPLETED' AND p.id IS NULL THEN 'PENDING' ELSE a.status END,p.submitted_at,\n       COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),N'Sin identificar')\nFROM dbo.agencies a LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id AND p.submitted_at<=@to\nWHERE a.is_active=1 ORDER BY a.grupo,a.status,a.terminal;", connection))
			{
				command.Parameters.AddWithValue("@to", to);
				await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					agencies.Add(new ExecutiveAuditAgency(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? ((DateTime?)null) : new DateTime?(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc)), reader.IsDBNull(5) ? null : reader.GetString(5)));
				}
			}
			result = new ExecutiveReportResult(period, from, to, DateTime.UtcNow, tickets, findings, findingPeople, warehouse, workshop, auditGroups, agencies, new List<IncidentTicketReportRow>());
		}
		return result;
	}

	internal static async Task EnsureGledysAdministrativeProfile(SqlConnection connection, CancellationToken ct)
	{
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(ct));
		await using (SqlCommand schema = new SqlCommand("DECLARE @lock int;\nEXEC @lock=sys.sp_getapplock @Resource='GLEDYS_ADMINISTRATIVE_20261020',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;\nIF @lock<0 THROW 50001,'No se pudo preparar el perfil administrativo.',1;\nIF OBJECT_ID(N'dbo.administrative_profile_updates',N'U') IS NULL\n  CREATE TABLE dbo.administrative_profile_updates(update_key varchar(100) PRIMARY KEY,user_id nvarchar(80) NOT NULL,previous_role varchar(40),previous_team varchar(40),previous_permissions nvarchar(max),applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", connection, tx))
		{
			await schema.ExecuteNonQueryAsync(ct);
		}
		await using SqlCommand read = new SqlCommand("SELECT CONVERT(nvarchar(80),id),role,support_team,permissions_json\nFROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK)\nWHERE LOWER(LTRIM(RTRIM(email))) IN('gledys.tejeda','gledys.tejeda@grupotejeda.local')\n  AND NOT EXISTS(SELECT 1 FROM dbo.administrative_profile_updates WHERE update_key='GLEDYS_ADMINISTRATIVE_20261020');", connection, tx);
		string id;
		string role;
		string team;
		string previous;
		await using (SqlDataReader reader = await read.ExecuteReaderAsync(ct))
		{
			if (await reader.ReadAsync(ct))
			{
				id = reader.GetString(0);
				role = reader.GetString(1);
				team = (reader.IsDBNull(2) ? null : reader.GetString(2));
				previous = (reader.IsDBNull(3) ? null : reader.GetString(3));
				if (await reader.ReadAsync(ct))
				{
					throw new InvalidOperationException("Hay más de una cuenta de gledys.tejeda; no se modificó ningún perfil.");
				}
				goto end_IL_0340;
			}
			await reader.DisposeAsync();
			await tx.CommitAsync(ct);
			goto end_IL_02a6;
			end_IL_0340:;
		}
		Dictionary<string, JsonElement> dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(previous ?? "{}") ?? new Dictionary<string, JsonElement>();
		string[] array = new string[3] { "canViewDashboard", "canViewFieldAudits", "canViewAllSupportDepartments" };
		foreach (string key in array)
		{
			dictionary.TryAdd(key, JsonSerializer.SerializeToElement(value: false));
		}
		string[] array2 = new string[20]
		{
			"canManageDepartment", "canCreateTickets", "canAssignTickets", "canResolveTickets", "canViewSupportDashboard", "canViewTicketNotifications", "canViewSupportFindings", "canUseSupportChat", "canShareSupportConversations", "canViewDocuments",
			"canUploadDocuments", "canManageDocumentFolders", "canShareDocuments", "canDeleteDocuments", "canReceiveDocuments", "canSignDocuments", "canDecideDocuments", "canManageDocumentTemplates", "canManageAdministrativeAssets", "canRequestWarehouse"
		};
		foreach (string key2 in array2)
		{
			dictionary[key2] = JsonSerializer.SerializeToElement(value: true);
		}
		await using (SqlCommand save = new SqlCommand("INSERT dbo.administrative_profile_updates(update_key,user_id,previous_role,previous_team,previous_permissions)\nVALUES('GLEDYS_ADMINISTRATIVE_20261020',@id,@role,@team,@previous);\nUPDATE dbo.admin_users SET role='ADMINISTRATIVE',support_team=NULL,permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;", connection, tx))
		{
			save.Parameters.AddWithValue("@id", id);
			save.Parameters.AddWithValue("@role", role);
			save.Parameters.AddWithValue("@team", ((object)team) ?? ((object)DBNull.Value));
			save.Parameters.AddWithValue("@previous", ((object)previous) ?? ((object)DBNull.Value));
			save.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(dictionary));
			await save.ExecuteNonQueryAsync(ct);
			await tx.CommitAsync(ct);
		}
		end_IL_02a6:;
	}

	private static async Task EnsureInstitutionalSchema(SqlConnection c, CancellationToken ct)
	{
		if (institutionalSchemaReady)
		{
			return;
		}
		await InstitutionalSchemaLock.WaitAsync(ct);
		try
		{
			if (!institutionalSchemaReady)
			{
				await using SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL\r\nBEGIN\r\n  CREATE TABLE dbo.institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80) NOT NULL,department nvarchar(80) NOT NULL,area varchar(20) NOT NULL,channel varchar(20) NOT NULL,metadata nvarchar(max) NOT NULL CHECK(ISJSON(metadata)=1),file_data varbinary(max) NOT NULL);\r\n  CREATE INDEX IX_institutional_files_scope ON dbo.institutional_files(area,channel,owner_id);\r\nEND;\r\nIF OBJECT_ID(N'dbo.institutional_editable_templates',N'U') IS NULL\r\n  CREATE TABLE dbo.institutional_editable_templates(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80) NOT NULL,department nvarchar(80) NOT NULL,name nvarchar(160) NOT NULL,content nvarchar(max) NOT NULL,updated_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\r\nIF OBJECT_ID(N'dbo.application_one_time_actions',N'U') IS NULL\r\n  CREATE TABLE dbo.application_one_time_actions(action_key varchar(100) NOT NULL PRIMARY KEY,executed_utc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());\r\n-- Retire the old destructive cleanup; the current cleanup preserves pending requests and backups.\r\nIF NOT EXISTS(SELECT 1 FROM dbo.application_one_time_actions WHERE action_key='V403_CLEAR_WAREHOUSE_REQUESTS')\r\n  INSERT INTO dbo.application_one_time_actions(action_key) VALUES('V403_CLEAR_WAREHOUSE_REQUESTS');", c);
				await cmd.ExecuteNonQueryAsync(ct);
				await CleanupCompletedWarehouseRequests(c, ct);
				institutionalSchemaReady = true;
			}
		}
		finally
		{
			InstitutionalSchemaLock.Release();
		}
	}

	private static async Task ApplyTechnicalFailureWarehouseAccess(SqlConnection connection, IEnumerable<InstitutionalFile> files, DocumentActor actor, CancellationToken ct)
	{
		if (!actor.CanReadTechnicalFailureWarehouseTeam)
		{
			return;
		}
		HashSet<string> owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		await using SqlCommand command = new SqlCommand("SELECT CONVERT(nvarchar(80),id) FROM dbo.admin_users WHERE role='TECHNOLOGY' AND support_team='TECHNICAL_FAILURE' AND is_active=1", connection);
		await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
		while (await reader.ReadAsync(ct))
		{
			owners.Add(reader.GetString(0));
		}
		foreach (InstitutionalFile file in files)
		{
			file.TeamWarehouseReaderAllowed = file.Area == "communications" && file.Channel == "requirements" && (object)file.WarehouseRequest != null && file.RecipientDepartment == "Almacén" && owners.Contains(file.OwnerUserName);
		}
	}

	public async Task<List<InstitutionalFile>> InstitutionalList(DocumentActor a, string area, string channel, CancellationToken ct, bool hydrate = true)
	{
		if (!a.Allowed)
		{
			return new List<InstitutionalFile>();
		}
		List<InstitutionalFile> result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			await EnsureAdministrativeFolderSchema(c, ct);
			if (area == "communications")
			{
				if (a.Role == "Administrator")
				{
					goto IL_02d4;
				}
				if (a.Role == "Administrative")
				{
					string[]? array = AdministrativeProfiles.Forms(a.Login);
					if (array != null && Enumerable.Contains(array, "FUEL_LOG"))
					{
						goto IL_02d4;
					}
				}
			}
			goto IL_02db;
			IL_02d4:
			await EnsureSandraFuelImport(c, ct);
			goto IL_02db;
			IL_02db:
			List<InstitutionalFile> list2;
			await using (SqlCommand cmd = new SqlCommand("SELECT id,metadata FROM dbo.institutional_files f WHERE area=@area AND (@area='files' OR @channel='all' OR channel=@channel) AND (owner_id=@user OR @manager=1 OR (@incidentReader=1 AND area='communications' AND channel='requirements' AND JSON_VALUE(metadata,'$.administrativeRequest.formType')='INCIDENT_REPORT') OR (area='files' AND (department=@dept OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.folderId')) IS NOT NULL)) OR (area='communications' AND channel='requirements' AND department=@dept AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén') OR (@teamReader=1 AND area='communications' AND channel='requirements' AND JSON_QUERY(metadata,'$.warehouseRequest') IS NOT NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén' AND EXISTS(SELECT 1 FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=f.owner_id AND u.role='TECHNOLOGY' AND u.support_team='TECHNICAL_FAILURE' AND u.is_active=1)) OR JSON_VALUE(metadata,'$.recipientUserName')=@user OR (JSON_VALUE(metadata,'$.recipientUserName') IS NULL AND JSON_VALUE(metadata,'$.recipientDepartment')=@dept) OR EXISTS(SELECT 1 FROM OPENJSON(metadata,'$.sharedWith') WITH(recipientUserName nvarchar(80),recipientDepartment nvarchar(80)) s WHERE s.recipientUserName=@user OR (s.recipientUserName IS NULL AND s.recipientDepartment=@dept))) ORDER BY id DESC", c))
			{
				cmd.Parameters.AddWithValue("@area", area);
				cmd.Parameters.AddWithValue("@channel", channel);
				cmd.Parameters.AddWithValue("@user", a.Id);
				cmd.Parameters.AddWithValue("@manager", a.Manager);
				cmd.Parameters.AddWithValue("@incidentReader", AdministrativeProfiles.CanReadIncidentReports(a));
				cmd.Parameters.AddWithValue("@dept", a.Department);
				cmd.Parameters.AddWithValue("@teamReader", a.CanReadTechnicalFailureWarehouseTeam);
				List<InstitutionalFile> list = new List<InstitutionalFile>();
				await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
				{
					while (await r.ReadAsync(ct))
					{
						InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(r.GetString(1), InstitutionalJson);
						institutionalFile.Id = r.GetInt64(0);
						list.Add(institutionalFile);
					}
				}
				await ApplyAdministrativeFolderAccess(c, list, a, ct);
				await ApplyTechnicalFailureWarehouseAccess(c, list, a, ct);
				List<InstitutionalFile> accessible = (from d in list
					where d.Accessible(a)
					select d.For(a)).ToList();
				if (hydrate)
				{
					foreach (InstitutionalFile item in accessible)
					{
						await HydrateInstitutionalFile(item, ct);
					}
				}
				list2 = accessible;
			}
			result = list2;
		}
		return result;
	}

	public async Task<InstitutionalFile?> InstitutionalDetail(long id, DocumentActor a, CancellationToken ct)
	{
		InstitutionalFile result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			InstitutionalFile institutionalFile;
			await using (SqlCommand cmd = new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id", c))
			{
				cmd.Parameters.AddWithValue("@id", id);
				if (!(await cmd.ExecuteScalarAsync(ct) is string json))
				{
					institutionalFile = null;
				}
				else
				{
					InstitutionalFile d = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
					d.Id = id;
					await ApplyAdministrativeFolderAccess(c, new _003C_003Ez__ReadOnlySingleElementList<InstitutionalFile>(d), a, ct);
					await ApplyTechnicalFailureWarehouseAccess(c, new _003C_003Ez__ReadOnlySingleElementList<InstitutionalFile>(d), a, ct);
					if (!d.Accessible(a))
					{
						institutionalFile = null;
					}
					else
					{
						d = await WarehouseRecoverLegacyRequest(c, d, json, ct);
						InstitutionalFile institutionalFile2 = ((!d.Accessible(a)) ? null : (await HydrateInstitutionalFile(d.For(a), ct)));
						institutionalFile = institutionalFile2;
					}
				}
			}
			result = institutionalFile;
		}
		return result;
	}

	public async Task<(InstitutionalFile Document, byte[] Data)?> InstitutionalBytes(long id, DocumentActor a, CancellationToken ct)
	{
		(InstitutionalFile Document, byte[] Data)? result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			(InstitutionalFile Document, byte[] Data)? tuple3;
			await using (SqlCommand cmd = new SqlCommand("SELECT metadata,file_data FROM dbo.institutional_files WHERE id=@id", c))
			{
				cmd.Parameters.AddWithValue("@id", id);
				(InstitutionalFile Document, byte[] Data)? tuple;
				await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
				{
					if (!(await r.ReadAsync(ct)))
					{
						tuple = null;
					}
					else
					{
						InstitutionalFile d = JsonSerializer.Deserialize<InstitutionalFile>(r.GetString(0), InstitutionalJson);
						d.Id = id;
						byte[] bytes = (byte[])r[1];
						await r.DisposeAsync();
						await ApplyAdministrativeFolderAccess(c, new _003C_003Ez__ReadOnlySingleElementList<InstitutionalFile>(d), a, ct);
						await ApplyTechnicalFailureWarehouseAccess(c, new _003C_003Ez__ReadOnlySingleElementList<InstitutionalFile>(d), a, ct);
						(InstitutionalFile, byte[])? tuple2 = ((!d.Accessible(a)) ? (((InstitutionalFile, byte[])?)null) : new (InstitutionalFile, byte[])?((await HydrateInstitutionalFile(d.For(a), ct), await StorageFiles.Read(bytes, ct))));
						tuple = tuple2;
					}
				}
				tuple3 = tuple;
			}
			result = tuple3;
		}
		return result;
	}

	public async Task<(InstitutionalFile Document, byte[] Data)?> InstitutionalOpenBytes(long id, CancellationToken ct)
	{
		(InstitutionalFile Document, byte[] Data)? result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			(InstitutionalFile Document, byte[] Data)? tuple2;
			await using (SqlCommand cmd = new SqlCommand("SELECT metadata,file_data FROM dbo.institutional_files WHERE id=@id", c))
			{
				cmd.Parameters.AddWithValue("@id", id);
				(InstitutionalFile Document, byte[] Data)? tuple;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					if (!(await reader.ReadAsync(ct)))
					{
						tuple = null;
					}
					else
					{
						InstitutionalFile document = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(0), InstitutionalJson);
						document.Id = id;
						byte[] stored = (byte[])reader[1];
						await reader.DisposeAsync();
						InstitutionalFile item = document;
						tuple = (item, await StorageFiles.Read(stored, ct));
					}
				}
				tuple2 = tuple;
			}
			result = tuple2;
		}
		return result;
	}

	public async Task<(byte[] Data, string Mime, string FileName)?> InstitutionalWarehouseEvidence(long documentId, Guid evidenceId, DocumentActor actor, CancellationToken ct)
	{
		(byte[] Data, string Mime, string FileName)? result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			(byte[] Data, string Mime, string FileName)? tuple;
			await using (SqlCommand documentCommand = new SqlCommand("SELECT metadata FROM dbo.institutional_files WHERE id=@id", connection))
			{
				documentCommand.Parameters.AddWithValue("@id", documentId);
				if (!(await documentCommand.ExecuteScalarAsync(ct) is string json))
				{
					tuple = null;
				}
				else
				{
					InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
					if (institutionalFile == null)
					{
						tuple = null;
					}
					else
					{
						institutionalFile.Id = documentId;
						if (!institutionalFile.Accessible(actor) || (object)institutionalFile.WarehouseInstallation == null || institutionalFile.WarehouseInstallation.Evidence.All((WarehouseInstallationEvidence item) => item.Id != evidenceId))
						{
							tuple = null;
						}
						else
						{
							(byte[] Data, string Mime, string FileName)? tuple4;
							await using (SqlCommand evidence = new SqlCommand("SELECT photo_data,content_type,file_name FROM dbo.support_ticket_evidence WHERE id=@evidence AND ticket_id=@ticket", connection))
							{
								evidence.Parameters.AddWithValue("@evidence", evidenceId);
								evidence.Parameters.AddWithValue("@ticket", institutionalFile.WarehouseInstallation.TicketId);
								(byte[] Data, string Mime, string FileName)? tuple3;
								await using (SqlDataReader reader = await evidence.ExecuteReaderAsync(ct))
								{
									(byte[], string, string)? tuple2 = ((!(await reader.ReadAsync(ct))) ? (((byte[], string, string)?)null) : new (byte[], string, string)?((await StorageFiles.Read((byte[])reader[0], ct), reader.GetString(1), reader.GetString(2))));
									tuple3 = tuple2;
								}
								tuple4 = tuple3;
							}
							tuple = tuple4;
						}
					}
				}
			}
			result = tuple;
		}
		return result;
	}

	public async Task<long> InstitutionalUpload(InstitutionalFile d, byte[] data, CancellationToken ct)
	{
		bool flag = (object)d.WarehouseRequest != null && !d.ReceivedUtc.HasValue && (object)d.WarehouseDelivery == null;
		bool flag2 = flag;
		if (flag2)
		{
			string status = d.Status;
			bool flag3 = ((status == "approved" || status == "rejected") ? true : false);
			flag2 = !flag3;
		}
		if (flag2)
		{
			d.Status = "pending";
			DateTime? sentUtc = d.SentUtc;
			sentUtc.GetValueOrDefault();
			if (!sentUtc.HasValue)
			{
				DateTime utcNow = DateTime.UtcNow;
				d.SentUtc = utcNow;
			}
		}
		if ((object)d.AdministrativeRequest != null && d.Area == "communications" && d.Channel == "requirements")
		{
			return await InstitutionalAdministrativeUpload(d, data, ct);
		}
		if ((object)d.WarehouseRequest != null && d.Area == "communications" && d.Channel == "requirements")
		{
			return await InstitutionalWarehouseUpload(d, data, ct);
		}
		long result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			await EnsureAdministrativeFolderSchema(c, ct);
			long num2;
			await using (SqlTransaction uploadTx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				if (d.Area == "files")
				{
					Guid? folderId = d.FolderId;
					if (folderId.HasValue)
					{
						Guid target = folderId.GetValueOrDefault();
						DocumentActor actor = new DocumentActor(d.OwnerUserName, "Administrative", "", d.OwnerDisplayName, d.OwnerLogin, d.Department);
						List<AdministrativeFolder> list = await LoadAdministrativeFolders(c, d.Department, ct, uploadTx);
						AdministrativeFolder administrativeFolder = list.Find((AdministrativeFolder f) => f.Id == target);
						if ((object)administrativeFolder == null || !FolderWritable(administrativeFolder, list, actor))
						{
							throw new InvalidOperationException("Selecciona una carpeta propia de tu departamento.");
						}
					}
				}
				long num;
				await using (SqlCommand cmd = new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,@area,@channel,@json,@bytes)", c, uploadTx))
				{
					cmd.Parameters.AddWithValue("@owner", d.OwnerUserName);
					cmd.Parameters.AddWithValue("@dept", d.Department);
					cmd.Parameters.AddWithValue("@area", d.Area);
					cmd.Parameters.AddWithValue("@channel", d.Channel);
					SqlParameterCollection parameters = cmd.Parameters;
					parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(d, InstitutionalJson), upload: true, ct));
					parameters = cmd.Parameters;
					parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
					long inserted = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
					await uploadTx.CommitAsync(ct);
					num = inserted;
				}
				num2 = num;
			}
			result = num2;
		}
		return result;
	}

	public async Task<InstitutionalFile?> InstitutionalWarehouseSubmission(string number, DocumentActor actor, CancellationToken ct)
	{
		InstitutionalFile result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			InstitutionalFile institutionalFile3;
			await using (SqlCommand cmd = new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND area='communications' AND channel='requirements' AND JSON_VALUE(metadata,'$.warehouseRequest.number')=@number ORDER BY id", c))
			{
				cmd.Parameters.AddWithValue("@owner", actor.Id);
				cmd.Parameters.AddWithValue("@number", number);
				InstitutionalFile institutionalFile;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					if (!(await reader.ReadAsync(ct)))
					{
						institutionalFile = null;
					}
					else
					{
						InstitutionalFile institutionalFile2 = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson);
						if (institutionalFile2 == null || !institutionalFile2.Accessible(actor))
						{
							institutionalFile = null;
						}
						else
						{
							institutionalFile2.Id = reader.GetInt64(0);
							institutionalFile = await HydrateInstitutionalFile(institutionalFile2.For(actor), ct);
						}
					}
				}
				institutionalFile3 = institutionalFile;
			}
			result = institutionalFile3;
		}
		return result;
	}

	private async Task<long> InstitutionalWarehouseUpload(InstitutionalFile d, byte[] data, CancellationToken ct)
	{
		long result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			if (d.WarehouseRequest?.FailureCategory != null)
			{
				await EnsureTicketWorkflowSchema(c, ct);
			}
			long num3;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(ct)))
			{
				string text = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(d.OwnerUserName + "|" + d.WarehouseRequest.Number)));
				await using (SqlCommand gate = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @result<0 THROW 50001,'No fue posible confirmar el envio. Reintenta con el mismo formulario.',1;", c, tx))
				{
					gate.Parameters.AddWithValue("@key", "warehouse-submission:" + text);
					await gate.ExecuteNonQueryAsync(ct);
				}
				long num2;
				await using (SqlCommand gate = new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND area='communications' AND channel='requirements' AND JSON_VALUE(metadata,'$.warehouseRequest.number')=@number ORDER BY id", c, tx))
				{
					gate.Parameters.AddWithValue("@owner", d.OwnerUserName);
					gate.Parameters.AddWithValue("@number", d.WarehouseRequest.Number);
					long num;
					await using (SqlDataReader reader = await gate.ExecuteReaderAsync(ct))
					{
						if (await reader.ReadAsync(ct))
						{
							long id = reader.GetInt64(0);
							InstitutionalFile old = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson);
							WarehouseRequestInfo value = ((old.WarehouseRequest?.FailureCategory != null) ? old.WarehouseRequest with
							{
								TicketId = null,
								TicketNumber = null,
								FailureTeam = d.WarehouseRequest.FailureTeam
							} : old.WarehouseRequest);
							if (old.Department != d.Department || old.Description != d.Description || JsonSerializer.Serialize(value, InstitutionalJson) != JsonSerializer.Serialize(d.WarehouseRequest, InstitutionalJson))
							{
								throw new WarehouseSubmissionConflictException();
							}
							await reader.DisposeAsync();
							await tx.CommitAsync(ct);
							d.Id = id;
							d.WarehouseRequest = old.WarehouseRequest;
							num = id;
							goto IL_0926;
						}
					}
					goto end_IL_0511;
					IL_0926:
					num2 = num;
					goto IL_09fe;
					end_IL_0511:;
				}
				WarehouseRequestInfo request = d.WarehouseRequest;
				if ((object)request != null && request.FailureCategory != null)
				{
					bool flag = request.AgencyId == Guid.Empty || request.TicketId.HasValue;
					bool flag2 = flag;
					if (!flag2)
					{
						string failureDepartment = request.FailureDepartment;
						bool flag3 = ((failureDepartment == "TECHNOLOGY" || failureDepartment == "GENERAL_SERVICES") ? true : false);
						flag2 = !flag3;
					}
					if (flag2 || string.IsNullOrWhiteSpace(request.FailureDescription))
					{
						throw new WarehouseFailureValidationException();
					}
					string categoryName;
					await using (SqlCommand gate = new SqlCommand("SELECT TOP(1)c.name FROM dbo.support_categories c JOIN dbo.support_departments dept ON dept.code=c.department_code WHERE c.department_code=@department AND c.code=@category AND c.is_active=1 AND dept.is_active=1", c, tx))
					{
						gate.Parameters.AddWithValue("@department", request.FailureDepartment);
						gate.Parameters.AddWithValue("@category", request.FailureCategory);
						categoryName = ((await gate.ExecuteScalarAsync(ct)) as string) ?? throw new WarehouseFailureValidationException();
					}
					Guid ticketId = Guid.NewGuid();
					string text2 = categoryName + " · " + request.AgencyName;
					if (text2.Length > 180)
					{
						text2 = text2.Substring(0, 180);
					}
					string value2 = string.Join("; ", request.Lines.Select((WarehouseRequestLine line) => $"{line.Name} x{line.Quantity}"));
					string text3 = $"Solicitud a Almacén {request.Number}. Falla: {request.FailureDescription.Trim()}\nEquipos: {value2}";
					if (text3.Length > 2000)
					{
						text3 = text3.Substring(0, 2000);
					}
					await using (SqlCommand gate = new SqlCommand("INSERT INTO dbo.support_tickets(id,agency_id,category,assigned_department,assigned_team,priority,status,subject,description,created_by_user_id,created_by_name,created_by_username,ticket_type,assigned_technician_id)\r\nOUTPUT INSERTED.ticket_number\r\nVALUES(@id,@agency,@category,@department,@team,@priority,'OPEN',@subject,@description,@userId,@name,@login,@ticketType,NULL)", c, tx))
					{
						gate.Parameters.AddWithValue("@id", ticketId);
						gate.Parameters.AddWithValue("@agency", request.AgencyId.ToString());
						gate.Parameters.AddWithValue("@category", request.FailureCategory);
						gate.Parameters.AddWithValue("@department", request.FailureDepartment);
						gate.Parameters.AddWithValue("@team", (request.FailureTeam != null) ? ((IConvertible)request.FailureTeam) : ((IConvertible)DBNull.Value));
						gate.Parameters.AddWithValue("@priority", (request.FailureDepartment == "TECHNOLOGY" && request.FailureCategory == "CPU_FAILURE") ? "CRITICAL" : request.Priority);
						gate.Parameters.AddWithValue("@subject", text2);
						gate.Parameters.AddWithValue("@description", text3);
						gate.Parameters.AddWithValue("@userId", d.OwnerUserName);
						gate.Parameters.AddWithValue("@name", d.OwnerDisplayName);
						gate.Parameters.AddWithValue("@login", d.OwnerLogin);
						gate.Parameters.AddWithValue("@ticketType", "INTERNAL");
						long value3 = Convert.ToInt64(await gate.ExecuteScalarAsync(ct));
						d.WarehouseRequest = request with
						{
							TicketId = ticketId,
							TicketNumber = value3
						};
					}
					await AddTicketHistory(c, tx, ticketId, "CREATED", null, request.FailureDepartment, null, request.FailureTeam, d.OwnerUserName, d.OwnerDisplayName, "Creado desde solicitud a Almacén " + request.Number, ct);
					await AddTicketNotifications(c, tx, ticketId, "CREATED", null, request.FailureDepartment, request.AgencyGroup, d.OwnerUserName, d.OwnerDisplayName, "Se abrió un ticket de falla desde la solicitud a Almacén " + request.Number + ".", notifySupervisors: true, ct);
				}
				await using (SqlCommand insert = new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,@area,@channel,@json,@bytes)", c, tx))
				{
					insert.Parameters.AddWithValue("@owner", d.OwnerUserName);
					insert.Parameters.AddWithValue("@dept", d.Department);
					insert.Parameters.AddWithValue("@area", d.Area);
					insert.Parameters.AddWithValue("@channel", d.Channel);
					SqlParameterCollection parameters = insert.Parameters;
					parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(d, InstitutionalJson), upload: true, ct));
					parameters = insert.Parameters;
					parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
					object obj = await insert.ExecuteScalarAsync(ct);
					if (obj == null || Convert.ToInt64(obj) <= 0)
					{
						throw new InvalidOperationException("No se obtuvo el identificador de la solicitud.");
					}
					d.Id = Convert.ToInt64(obj);
					await tx.CommitAsync(ct);
					num2 = d.Id;
				}
				num3 = num2;
				goto end_IL_0323;
				IL_09fe:
				num3 = num2;
				end_IL_0323:;
			}
			result = num3;
		}
		return result;
	}

	public async Task<long> InstitutionalArchiveTemplatePdf(DocumentActor a, long templateId, InstitutionalFile d, byte[] data, CancellationToken ct)
	{
		long result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			long num3;
			await using (SqlCommand find = new SqlCommand("SELECT TOP(1) id,metadata FROM dbo.institutional_files WHERE owner_id=@owner AND area='communications' AND channel='requests' AND JSON_VALUE(metadata,'$.templateId')=@template ORDER BY id DESC", c))
			{
				find.Parameters.AddWithValue("@owner", a.Id);
				find.Parameters.AddWithValue("@template", templateId.ToString());
				long num2;
				await using (SqlDataReader reader = await find.ExecuteReaderAsync(ct))
				{
					long? existing = null;
					DateTime? created = null;
					if (await reader.ReadAsync(ct))
					{
						existing = reader.GetInt64(0);
						try
						{
							created = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson)?.CreatedUtc;
						}
						catch (JsonException)
						{
						}
					}
					await reader.DisposeAsync();
					d.TemplateId = templateId;
					d.Id = existing.GetValueOrDefault();
					d.CreatedUtc = created ?? DateTime.UtcNow;
					long num;
					if (existing.HasValue)
					{
						long id = existing.GetValueOrDefault();
						await using (SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id AND owner_id=@owner", c))
						{
							update.Parameters.AddWithValue("@id", id);
							update.Parameters.AddWithValue("@owner", a.Id);
							SqlParameterCollection parameters = update.Parameters;
							parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(d, InstitutionalJson), upload: true, ct));
							parameters = update.Parameters;
							parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
							await update.ExecuteNonQueryAsync(ct);
							num = id;
						}
						num2 = num;
					}
					else
					{
						await using (SqlCommand insert = new SqlCommand("INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) OUTPUT INSERTED.id VALUES(@owner,@dept,'communications','requests',@json,@bytes)", c))
						{
							insert.Parameters.AddWithValue("@owner", a.Id);
							insert.Parameters.AddWithValue("@dept", a.Department);
							SqlParameterCollection parameters = insert.Parameters;
							parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(d, InstitutionalJson), upload: true, ct));
							parameters = insert.Parameters;
							parameters.AddWithValue("@bytes", await StorageFiles.Store(data, "institutional-document", ct));
							num = Convert.ToInt64(await insert.ExecuteScalarAsync(ct));
						}
						num2 = num;
					}
				}
				num3 = num2;
			}
			result = num3;
		}
		return result;
	}

	public async Task<string?> DecideIncidentTicket(long id, DocumentActor actor, IncidentTicketDecisionInput input, CancellationToken ct)
	{
		if (!AdministrativeProfiles.CanDecideIncidentTickets(actor)) return "Solo Lucianny, Yessica o el administrador principal pueden decidir estos tickets.";
		if (input.Status is not ("APPROVED" or "REJECTED")) return "Selecciona Aprobar o Rechazar.";
		if (input.Note?.Trim().Length > 2000) return "El motivo admite hasta 2,000 caracteres.";
		if (input.Status == "APPROVED" && (input.Amount is null || input.Amount <= 0 || input.Amount > 1000000000m)) return "Indica un monto de compensación mayor que cero.";
		if (input.Status == "REJECTED" && (string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length < 5)) return "Indica el motivo del rechazo (mínimo 5 caracteres).";

		await using SqlConnection connection = await Open(ct);
		await EnsureInstitutionalSchema(connection, ct);
		await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
		await using SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", connection, transaction);
		read.Parameters.AddWithValue("@id", id);
		if (await read.ExecuteScalarAsync(ct) is not string json) return "Documento no disponible.";
		InstitutionalFile document = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson)!;
		document.Id = id;
		if (!document.Accessible(actor)) return "No tienes acceso a este informe.";
		if (document.Trashed || document.AdministrativeRequest?.FormType != "INCIDENT_REPORT" || document.AdministrativeRequest.FormData is not { ValueKind: JsonValueKind.Object } formData) return "El informe de incidentes no es válido.";
		if (document.ReceivedUtc is null || document.Status is not ("received" or "approved" or "rejected")) return "Primero recibe y sella el informe antes de decidir sus tickets.";

		JsonObject? root = JsonNode.Parse(formData.GetRawText())?.AsObject();
		JsonArray? rows = root?["rows"]?.AsArray();
		if (rows is null || input.TicketIndex < 0 || input.TicketIndex >= rows.Count || rows[input.TicketIndex] is not JsonObject row) return "El ticket seleccionado no existe.";
		string? ticketCode = row["ticketCode"]?.GetValue<string>()?.Trim();
		if (string.IsNullOrWhiteSpace(ticketCode)) return "El ticket seleccionado no tiene número.";
		string? previous = row["decisionStatus"]?.GetValue<string>()?.ToUpperInvariant();
		if (previous is "APPROVED" or "REJECTED" && previous != input.Status) return "Este ticket ya tiene una decisión y no puede cambiarse por la opción contraria.";

		DateTime now = DateTime.UtcNow;
		string previousDocumentStatus = document.Status;
		row["decisionStatus"] = input.Status;
		row["decisionNote"] = input.Note?.Trim() ?? "";
		row["decisionBy"] = actor.Name;
		row["decisionLogin"] = actor.Login;
		row["decisionUtc"] = now;
		if (input.Status == "APPROVED") row["approvedAmount"] = input.Amount!.Value; else row.Remove("approvedAmount");
		document.AdministrativeRequest = document.AdministrativeRequest with { FormData = JsonSerializer.SerializeToElement(root, InstitutionalJson) };
		List<JsonObject> active = rows.OfType<JsonObject>().Where(item => !string.IsNullOrWhiteSpace(item["ticketCode"]?.GetValue<string>())).ToList();
		List<string?> decisions = active.Select(item => item["decisionStatus"]?.GetValue<string>()?.ToUpperInvariant()).ToList();
		bool completed = active.Count > 0 && decisions.All(status => status is "APPROVED" or "REJECTED");
		document.Status = completed ? (decisions.Any(status => status == "APPROVED") ? "approved" : "rejected") : "received";
		document.DecisionBy = completed ? actor.Name + " · " + actor.Login : null;
		document.DecisionUtc = completed ? now : null;
		document.CorrectionNote = input.Note?.Trim();
		document.AdministrativeRevision++;
		document.History.Add(new InstitutionalEvent(input.Status == "APPROVED" ? "incident-ticket-approved" : "incident-ticket-rejected", actor.Name, actor.Login, now, previousDocumentStatus, document.ReceiptCode, document.ReceiverLogin, document.ReceivedUtc, null, Note: $"Ticket {ticketCode}: {input.Note?.Trim()}"));
		await using SqlCommand update = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", connection, transaction);
		update.Parameters.AddWithValue("@id", id);
		update.Parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(document, InstitutionalJson), upload: true, ct));
		await update.ExecuteNonQueryAsync(ct);
		await transaction.CommitAsync(ct);
		return null;
	}

	public async Task<string?> InstitutionalChange(long id, DocumentActor a, string action, InstitutionalAction b, CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			await EnsureAdministrativeFolderSchema(c, ct);
			string text4;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", c, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text = "Documento no disponible.";
					}
					else
					{
						InstitutionalFile d = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						d.Id = id;
						await ApplyAdministrativeFolderAccess(c, new _003C_003Ez__ReadOnlySingleElementList<InstitutionalFile>(d), a, ct, tx);
						if (!d.Accessible(a))
						{
							text = "No tienes acceso a este documento.";
						}
						else
						{
							d.For(a);
							if (AdministrativeFuel.BlocksAction(d, action))
							{
								text = "Los registros diarios de gasolina se guardan directamente y no requieren autorización ni recepción.";
							}
							else if (d.AdministrativeRequest?.FormType == "INCIDENT_REPORT" && (action == "approve" || action == "reject" || action == "correction"))
							{
								text = "Decide cada ticket por separado desde las opciones del informe.";
							}
							else
							{
								bool flag = d.AdministrativeRequest?.FormType == "GAS_LOG" && d.OwnerUserName == a.Id;
								bool flag2 = flag;
								if (flag2)
								{
									string text2 = action;
									bool flag3 = ((text2 == "approve" || text2 == "reject") ? true : false);
									flag2 = flag3;
								}
								if (flag2)
								{
									text = "La relación de gas debe ser autorizada por otra persona con permiso.";
								}
								else
								{
									d.History.Add(new InstitutionalEvent(action, a.Name, a.Login, DateTime.UtcNow, d.Status, d.ReceiptCode, d.ReceiverLogin, d.ReceivedUtc, (action == "resend") ? d.ReceiptSignature : null));
									string text2 = action;
									bool flag3 = ((text2 == "trash" || text2 == "purge") ? true : false);
									if (flag3 && d.WarehouseAssigned.Count > 0 && (object)d.WarehouseDelivery == null)
									{
										text = "La solicitud tiene equipos pendientes de retiro. Completa la entrega antes de enviarla a la papelera.";
									}
									else
									{
										text2 = action;
										flag3 = ((text2 == "trash" || text2 == "purge") ? true : false);
										if (flag3 && (object)d.AdministrativeRequest != null && d.AdministrativeRequest.FormType != "INCIDENT_REPORT" && d.Status == "approved" && (object)d.AdministrativeDelivery == null)
										{
											text = "Completa la entrega administrativa antes de enviar la solicitud a la papelera.";
										}
										else
										{
											switch (action)
											{
											case "trash":
											case "restore":
											case "purge":
												if (!d.IsOwner && !d.CanManageFiles)
												{
													text = "Solo el propietario o los colaboradores de la carpeta administran su papelera.";
												}
												else
												{
													if (!(action == "purge") || d.Trashed)
													{
														d.Trashed = action != "restore";
														break;
													}
													text = "Envía primero el archivo a la papelera.";
												}
												goto end_IL_0301;
											default:
												if (!d.Trashed)
												{
													switch (action)
													{
													case "cancel":
														if ((object)d.WarehouseRequest == null || d.RecipientDepartment != "Almacén" || (!d.IsOwner && !a.Manager))
														{
															text = "Solo el solicitante, Almacén o Administración puede anular esta solicitud.";
														}
														else
														{
															bool flag5 = (object)d.WarehouseDelivery != null;
															bool flag6 = flag5;
															if (!flag6)
															{
																switch (d.Status)
																{
																case "pending":
																case "received":
																case "correction_requested":
																case "approved":
																	flag3 = true;
																	break;
																default:
																	flag3 = false;
																	break;
																}
																flag6 = !flag3;
															}
															if (flag6)
															{
																text = "Solo puedes anular solicitudes activas que todavía no se han entregado.";
															}
															else
															{
																bool flag7 = string.IsNullOrWhiteSpace(b.Note);
																bool flag8 = flag7;
																if (!flag8)
																{
																	int length = b.Note.Trim().Length;
																	flag3 = ((length < 5 || length > 2000) ? true : false);
																	flag8 = flag3;
																}
																if (!flag8)
																{
																	await using (SqlCommand release = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.warehouse_equipment_allocations') AND name='CK_warehouse_allocation_status' AND definition NOT LIKE '%CANCELLED%')\r\nBEGIN\r\n  ALTER TABLE dbo.warehouse_equipment_allocations DROP CONSTRAINT CK_warehouse_allocation_status;\r\n  ALTER TABLE dbo.warehouse_equipment_allocations WITH CHECK ADD CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'));\r\nEND;\r\nIF OBJECT_ID(N'dbo.warehouse_equipment_allocations',N'U') IS NOT NULL UPDATE dbo.warehouse_equipment_allocations SET status='CANCELLED' WHERE document_id=@id AND status='PENDING_PICKUP';", c, tx))
																	{
																		release.Parameters.AddWithValue("@id", id);
																		await release.ExecuteNonQueryAsync(ct);
																		d.Status = "cancelled";
																		d.CorrectionNote = b.Note.Trim();
																		d.DecisionBy = a.Name + " · " + a.Login;
																		d.DecisionUtc = DateTime.UtcNow;
																	}
																	break;
																}
																text = "Indica el motivo de anulación (5 a 2,000 caracteres).";
															}
														}
														goto end_IL_0301;
													case "edit":
														if (d.CanEdit && !(d.Status == "cancelled"))
														{
															d.Title = b.Title.Trim();
															d.Description = b.Description?.Trim() ?? "";
															break;
														}
														text = "No puedes modificar este documento.";
														goto end_IL_0301;
													case "share":
													case "unshare":
														if (!d.IsOwner)
														{
															text = "Solo el propietario puede compartir o revocar accesos.";
														}
														else
														{
															if (!(d.Area == "files") || !d.FolderId.HasValue)
															{
																if (action == "unshare")
																{
																	d.SharedWith.RemoveAll((DocumentAccess s) => s.Id == b.ShareId);
																	break;
																}
																d.SharedWith.RemoveAll((DocumentAccess s) => s.RecipientUserName == b.RecipientUserName && s.RecipientDepartment == b.RecipientDepartment);
																d.SharedWith.Add(new DocumentAccess(Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), d.SharedWith.Select((DocumentAccess s) => s.Id).DefaultIfEmpty(0L).Max() + 1), b.RecipientUserName, b.RecipientDepartment, b.CanEdit, DateTime.UtcNow));
																break;
															}
															text = "Los archivos de una carpeta heredan sus destinatarios. Administra los accesos desde la carpeta principal.";
														}
														goto end_IL_0301;
													case "move":
														if (d.CanMove && d.ReceivedUtc.HasValue)
														{
															d.SealX = b.SealX;
															d.SealY = b.SealY;
															break;
														}
														text = "Solo quien recibió puede mover su sello.";
														goto end_IL_0301;
													case "sign":
														if (d.CanMove && d.ReceivedUtc.HasValue)
														{
															d.ReceiptSignature = b.SignatureData;
															break;
														}
														text = "Primero recibe el documento desde tu cuenta.";
														goto end_IL_0301;
													case "resend":
													{
														bool flag9 = !d.IsOwner;
														bool flag10 = flag9;
														if (!flag10)
														{
															text2 = d.Status;
															flag3 = ((text2 == "rejected" || text2 == "correction_requested") ? true : false);
															flag10 = !flag3;
														}
														if (!flag10)
														{
															if (d.AwaitingWarehouseAuthorization())
															{
																d.Status = "received";
																d.SentUtc = DateTime.UtcNow;
																break;
															}
															d.Status = "pending";
															d.SentUtc = DateTime.UtcNow;
															d.ReceivedUtc = null;
															d.ReceiptSignature = null;
															d.ReceivedByUserName = null;
															d.ReceiverDisplayName = null;
															d.ReceiverLogin = null;
															d.ReceiptCode = null;
															d.VerificationToken = null;
															break;
														}
														text = "No admite reenvío.";
														goto end_IL_0301;
													}
													case "send":
														if (d.IsOwner && !(d.Area != "communications") && !(d.Status != "draft") && !string.IsNullOrWhiteSpace(b.RecipientDepartment))
														{
															d.RecipientDepartment = b.RecipientDepartment.Trim();
															d.RecipientUserName = (string.IsNullOrWhiteSpace(b.RecipientUserName) ? null : b.RecipientUserName.Trim());
															d.Status = "pending";
															d.SentUtc = DateTime.UtcNow;
															break;
														}
														text = "Selecciona un departamento para enviar el borrador.";
														goto end_IL_0301;
													default:
													if (d.Area != "communications" || (!d.IsRecipient && !(d.AdministrativeRequest?.FormType == "INCIDENT_REPORT" && AdministrativeProfiles.CanManageIncidentReports(a))))
														{
															text = "Solo el destinatario puede responder.";
														}
														else if (action == "approve")
														{
															if ((object)d.WarehouseRequest != null)
															{
																text = "Selecciona los equipos y seriales desde Inventario antes de autorizar.";
															}
															else
															{
																if (!(d.Channel != "requirements") && (a.Manager || (a.Role == "Administrative" && (object)d.AdministrativeRequest != null && AdministrativeProfiles.CanRead(a, d.AdministrativeRequest))) && !(d.Status != "received"))
																{
																	d.Status = "approved";
																	d.DecisionBy = a.Name + " · " + a.Login;
																	d.DecisionUtc = DateTime.UtcNow;
																	d.CorrectionNote = b.Note;
																	break;
																}
																text = "Primero marca el requerimiento como recibido antes de autorizarlo.";
															}
														}
														else if (action == "receive")
														{
															bool hasValue = d.ReceivedUtc.HasValue;
															bool flag4 = hasValue;
															if (!flag4)
															{
																switch (d.Status)
																{
																case "pending":
																case "correction_requested":
																case "approved":
																	flag3 = true;
																	break;
																default:
																	flag3 = false;
																	break;
																}
																flag4 = !flag3;
															}
															if (!flag4)
															{
																InstitutionalFile institutionalFile = d;
																string status;
																if (d.Channel == "requests" && d.RecipientDepartment == "Almacén")
																{
																	status = "approved";
																}
																else
																{
																	status = ((d.Status == "approved") ? "approved" : "received");
																}
																institutionalFile.Status = status;
																d.DecisionBy = ((d.Status == "approved") ? (a.Name + " · " + a.Login) : d.DecisionBy);
																d.DecisionUtc = ((d.Status == "approved") ? new DateTime?(DateTime.UtcNow) : d.DecisionUtc);
																d.CorrectionNote = ((d.Status == "approved") ? "Recepción autorizada automáticamente por Almacén General." : d.CorrectionNote);
																d.ReceivedUtc = DateTime.UtcNow;
																d.ReceivedByUserName = a.Id;
																d.ReceiverDisplayName = a.Name;
																d.ReceiverLogin = a.Login;
																d.ReceiptSignature = b.SignatureData;
																d.ReceiptCode = $"REAL-REC-{DateTime.UtcNow:yyyyMMdd}-{id:D6}";
																d.VerificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
																d.SealX = b.SealX;
																d.SealY = b.SealY;
																break;
															}
															text = "La recepción ya fue registrada o no está pendiente.";
														}
														else
														{
															text2 = action;
															if ((text2 == "correction" || text2 == "reject") ? true : false)
															{
																if (d.Channel == "requirements" && d.Status != "received")
																{
																	text = "Almacén debe marcar primero el requerimiento como recibido antes de decidir.";
																}
																else
																{
																	switch (d.Status)
																	{
																	case "pending":
																	case "correction_requested":
																	case "received":
																		flag3 = true;
																		break;
																	default:
																		flag3 = false;
																		break;
																	}
																	if (flag3)
																	{
																		if ((object)d.WarehouseRequest != null && d.WarehouseAssigned.Count > 0)
																		{
																			await using SqlCommand release = new SqlCommand("DELETE FROM dbo.warehouse_equipment_allocations WHERE document_id=@id AND status='PENDING_PICKUP'", c, tx);
																			release.Parameters.AddWithValue("@id", id);
																			await release.ExecuteNonQueryAsync(ct);
																			d.WarehouseAssigned = new List<WarehouseAssignedEquipment>();
																			d.WarehousePreparationNote = null;
																			d.WarehousePreparedBy = null;
																			d.WarehousePreparedLogin = null;
																			d.WarehousePreparedUtc = null;
																		}
																		d.Status = ((action == "reject") ? "rejected" : "correction_requested");
																		d.CorrectionNote = b.Note;
																		d.DecisionBy = a.Name + " · " + a.Login;
																		d.DecisionUtc = DateTime.UtcNow;
																		break;
																	}
																	text = "La comunicación ya tiene una decisión.";
																}
															}
															else
															{
																text = "Acción desconocida.";
															}
														}
														goto end_IL_0301;
													}
													break;
												}
												text = "Restaura el documento antes de modificarlo.";
												goto end_IL_0301;
											}
											string text3;
											await using (SqlCommand save = new SqlCommand((action == "purge") ? "DELETE FROM dbo.institutional_files WHERE id=@id" : "UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", c, tx))
											{
												save.Parameters.AddWithValue("@id", id);
												SqlParameterCollection parameters = save.Parameters;
												parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(d, InstitutionalJson), upload: true, ct));
												await save.ExecuteNonQueryAsync(ct);
												await tx.CommitAsync(ct);
												text3 = null;
											}
											text = text3;
										}
									}
								}
							}
						}
					}
					end_IL_0301:;
				}
				text4 = text;
			}
			result = text4;
		}
		return result;
	}

	private static async Task LinkWarehouseTicketResolution(SqlConnection connection, SqlTransaction transaction, Guid ticketId, string actorId, string actorName, CancellationToken ct)
	{
		await using (SqlCommand exists = new SqlCommand("SELECT CASE WHEN OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL THEN 0 ELSE 1 END", connection, transaction))
		{
			if (Convert.ToInt32(await exists.ExecuteScalarAsync(ct)) == 0)
			{
				return;
			}
		}
		long ticketNumber;
		string status;
		string resolution;
		DateTime resolvedAt;
		string agencyCode;
		string agencyName;
		await using (SqlCommand exists = new SqlCommand("SELECT t.ticket_number,t.status,ISNULL(t.resolution,''),COALESCE(t.resolved_at,SYSUTCDATETIME()),a.codigo,a.terminal FROM dbo.support_tickets t JOIN dbo.agencies a ON a.id=t.agency_id WHERE t.id=@ticket", connection, transaction))
		{
			exists.Parameters.AddWithValue("@ticket", ticketId);
			await using (SqlDataReader reader = await exists.ExecuteReaderAsync(ct))
			{
				if (!(await reader.ReadAsync(ct)))
				{
					goto end_IL_0298;
				}
				ticketNumber = reader.GetInt64(0);
				status = reader.GetString(1);
				resolution = reader.GetString(2);
				resolvedAt = reader.GetDateTime(3);
				agencyCode = reader.GetString(4);
				agencyName = reader.GetString(5);
				goto end_IL_01f1;
				end_IL_0298:;
			}
			return;
			end_IL_01f1:;
		}
		List<WarehouseInstallationEvidence> evidence = new List<WarehouseInstallationEvidence>();
		await using (SqlCommand exists = new SqlCommand("SELECT id,file_name,content_type,captured_at FROM dbo.support_ticket_evidence WHERE ticket_id=@ticket ORDER BY captured_at,id", connection, transaction))
		{
			exists.Parameters.AddWithValue("@ticket", ticketId);
			await using SqlDataReader reader = await exists.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				evidence.Add(new WarehouseInstallationEvidence(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
			}
		}
		List<(long Id, InstitutionalFile Document)> linked = new List<(long, InstitutionalFile)>();
		await using (SqlCommand exists = new SqlCommand("SELECT id,metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.warehouseRequest.ticketId'))=@ticket", connection, transaction))
		{
			exists.Parameters.AddWithValue("@ticket", ticketId);
			await using SqlDataReader reader = await exists.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson);
				if (institutionalFile != null)
				{
					institutionalFile.Id = reader.GetInt64(0);
					linked.Add((institutionalFile.Id, institutionalFile));
				}
			}
		}
		foreach (var item3 in linked)
		{
			long item = item3.Id;
			InstitutionalFile item2 = item3.Document;
			item2.WarehouseInstallation = new WarehouseInstallationInfo(ticketId, ticketNumber, status, resolution, actorName, resolvedAt, agencyCode, agencyName, evidence);
			item2.History.Add(new InstitutionalEvent("installed", actorName, actorId, resolvedAt, item2.Status, item2.ReceiptCode, item2.ReceiverLogin, item2.ReceivedUtc, null));
			await using SqlCommand exists = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", connection, transaction);
			exists.Parameters.AddWithValue("@json", JsonSerializer.Serialize(item2, InstitutionalJson));
			exists.Parameters.AddWithValue("@id", item);
			await exists.ExecuteNonQueryAsync(ct);
		}
	}

	public async Task<object> InstitutionalOverview(DocumentActor a, string area, string channel, CancellationToken ct)
	{
		List<InstitutionalFile> docs = (await InstitutionalList(a, area, channel, ct, hydrate: false)).Where((InstitutionalFile d) => !d.Trashed).ToList();
		return new
		{
			total = docs.Count,
			mine = Enumerable.Count(docs, (InstitutionalFile d) => d.IsOwner),
			shared = Enumerable.Count(docs, (InstitutionalFile d) => !d.IsOwner && d.Shared(a)),
			pending = Enumerable.Count(docs, (InstitutionalFile d) =>
			{
				string status = d.Status;
				return ((status == "pending" || status == "correction_requested") ? true : false) && !d.AwaitingWarehouseAuthorization();
			}),
			received = Enumerable.Count(docs, (InstitutionalFile d) => d.Status == "received" || d.AwaitingWarehouseAuthorization()),
			approved = Enumerable.Count(docs, (InstitutionalFile d) => d.Status == "approved"),
			sent = Enumerable.Count(docs, (InstitutionalFile d) => d.IsOwner && d.SentUtc.HasValue),
			rejected = Enumerable.Count(docs, (InstitutionalFile d) => d.Status == "rejected"),
			departments = InstitutionalDepartments.Select((string name) =>
			{
				List<InstitutionalFile> list = docs.Where((InstitutionalFile d) => d.Department == name).ToList();
				return new
				{
					name = name,
					count = list.Count,
					pending = Enumerable.Count(list, (InstitutionalFile d) =>
					{
						string status = d.Status;
						return ((status == "pending" || status == "correction_requested") ? true : false) && !d.AwaitingWarehouseAuthorization();
					}),
					received = Enumerable.Count(list, (InstitutionalFile d) => d.Status == "received" || d.AwaitingWarehouseAuthorization()),
					approved = Enumerable.Count(list, (InstitutionalFile d) => d.Status == "approved"),
					rejected = Enumerable.Count(list, (InstitutionalFile d) => d.Status == "rejected")
				};
			})
		};
	}

	public async Task<bool> InstitutionalRecipient(string? user, string? department, CancellationToken ct)
	{
		if (!Enumerable.Contains(InstitutionalDepartments, department))
		{
			return false;
		}
		if (user == null)
		{
			return true;
		}
		using JsonDocument jsonDocument = JsonDocument.Parse(await InstitutionalUsers(ct));
		return jsonDocument.RootElement.EnumerateArray().Any((JsonElement x) => x.GetProperty("userName").GetString() == user && x.GetProperty("department").GetString() == department);
	}

	public async Task<List<InstitutionalTemplate>> InstitutionalTemplates(DocumentActor a, CancellationToken ct)
	{
		List<InstitutionalTemplate> result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			List<InstitutionalTemplate> list4;
			await using (SqlCommand cmd = new SqlCommand("SELECT id,name,content,department,owner_id,updated_at FROM dbo.institutional_editable_templates WHERE owner_id=@user OR department=@dept ORDER BY updated_at DESC", c))
			{
				cmd.Parameters.AddWithValue("@user", a.Id);
				cmd.Parameters.AddWithValue("@dept", a.Department);
				List<InstitutionalTemplate> list = new List<InstitutionalTemplate>();
				List<InstitutionalTemplate> list3;
				await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
				{
					while (await r.ReadAsync(ct))
					{
						List<InstitutionalTemplate> list2 = list;
						long @int = r.GetInt64(0);
						string name = r.GetString(1);
						list2.Add(new InstitutionalTemplate(@int, name, await StorageFiles.TransformJson(r.GetString(2), upload: false, ct), r.GetString(3), r.GetString(4), IsSystem: false, DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc)));
					}
					list3 = list;
				}
				list4 = list3;
			}
			result = list4;
		}
		return result;
	}

	public async Task<long?> InstitutionalSaveTemplate(DocumentActor a, long? id, string name, string content, CancellationToken ct)
	{
		long? result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			long? num;
			await using (SqlCommand cmd = new SqlCommand((id.HasValue && id.GetValueOrDefault() > 0) ? "UPDATE dbo.institutional_editable_templates SET name=@name,content=@content,updated_at=SYSUTCDATETIME() OUTPUT INSERTED.id WHERE id=@id AND owner_id=@user" : "INSERT INTO dbo.institutional_editable_templates(owner_id,department,name,content) OUTPUT INSERTED.id VALUES(@user,@dept,@name,@content)", c))
			{
				cmd.Parameters.AddWithValue("@id", id.GetValueOrDefault());
				cmd.Parameters.AddWithValue("@user", a.Id);
				cmd.Parameters.AddWithValue("@dept", a.Department);
				cmd.Parameters.AddWithValue("@name", name);
				SqlParameterCollection parameters = cmd.Parameters;
				parameters.AddWithValue("@content", await StorageFiles.TransformJson(content, upload: true, ct));
				object obj = await cmd.ExecuteScalarAsync(ct);
				num = ((obj == null) ? ((long?)null) : new long?(Convert.ToInt64(obj)));
			}
			result = num;
		}
		return result;
	}

	public async Task<bool> InstitutionalDeleteTemplate(DocumentActor a, long id, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			bool flag;
			await using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.institutional_editable_templates WHERE id=@id AND owner_id=@user", c))
			{
				cmd.Parameters.AddWithValue("@id", id);
				cmd.Parameters.AddWithValue("@user", a.Id);
				flag = await cmd.ExecuteNonQueryAsync(ct) == 1;
			}
			result = flag;
		}
		return result;
	}

	private static async Task<InstitutionalFile> HydrateInstitutionalFile(InstitutionalFile document, CancellationToken ct)
	{
		document.ReceiptSignature = await StorageFiles.ReadDataUrl(document.ReceiptSignature, ct);
		for (int i = 0; i < document.History.Count; i++)
		{
			List<InstitutionalEvent> history = document.History;
			int index = i;
			InstitutionalEvent institutionalEvent = document.History[i] with { };
			InstitutionalEvent institutionalEvent2 = institutionalEvent;
			institutionalEvent2 = institutionalEvent2 with { PreviousReceiptSignature = await StorageFiles.ReadDataUrl(document.History[i].PreviousReceiptSignature, ct) };
			history[index] = institutionalEvent;
		}
		JsonElement? jsonElement = document.AdministrativeRequest?.FormData;
		if (jsonElement.HasValue)
		{
			string json = await StorageFiles.TransformJson(jsonElement.GetValueOrDefault().GetRawText(), upload: false, ct);
			document.AdministrativeRequest = document.AdministrativeRequest with
			{
				FormData = JsonSerializer.Deserialize<JsonElement>(json)
			};
		}
		return document;
	}

	private static async Task<List<InstitutionalUser>> ReadInstitutionalUsers(SqlConnection c, CancellationToken ct, SqlTransaction? tx = null)
	{
		List<InstitutionalUser> users = new List<InstitutionalUser>();
		List<InstitutionalUser> result;
		await using (SqlCommand command = new SqlCommand("SELECT CONVERT(nvarchar(80),id),display_name,email,role,support_team FROM dbo.admin_users WHERE is_active=1 ORDER BY display_name", c, tx))
		{
			List<InstitutionalUser> list;
			await using (SqlDataReader r = await command.ExecuteReaderAsync(ct))
			{
				while (await r.ReadAsync(ct))
				{
					users.Add(new InstitutionalUser(r.GetString(0), r.GetString(1), r.GetString(2), DepartmentAccounts.AccountDepartment(r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4))));
				}
				list = users;
			}
			result = list;
		}
		return result;
	}

	public async Task<string> InstitutionalUsers(CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			result = JsonSerializer.Serialize(await ReadInstitutionalUsers(c, ct), InstitutionalJson);
		}
		return result;
	}

	public async Task<List<InstitutionalUser>> AdministrativeFolderUsers(DocumentActor actor, CancellationToken ct)
	{
		List<InstitutionalUser> result;
		await using (SqlConnection c = await Open(ct))
		{
			result = (await ReadInstitutionalUsers(c, ct)).Where((InstitutionalUser u) => !string.Equals(u.UserName, actor.Id, StringComparison.OrdinalIgnoreCase)).ToList();
		}
		return result;
	}

	public async Task<bool> InternetTicketAgencyMatches(Guid ticketId, Guid agencyId, CancellationToken ct)
	{
		bool result;
		await using (SqlConnection c = await Open(ct))
		{
			bool flag;
			await using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.support_tickets WHERE id=@ticket AND agency_id=@agency AND ticket_type='INTERNAL' AND assigned_department='TECHNOLOGY' AND status NOT IN('RESOLVED','CLOSED','CANCELLED')", c))
			{
				cmd.Parameters.AddWithValue("@ticket", ticketId);
				cmd.Parameters.AddWithValue("@agency", agencyId.ToString());
				flag = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
			}
			result = flag;
		}
		return result;
	}

	private static async Task EnsureLegacyAccountRecovery(SqlConnection connection, CancellationToken ct)
	{
		byte[] adminSalt = RandomNumberGenerator.GetBytes(16);
		byte[] adminHash = PasswordHash("123456", adminSalt, 210000);
		byte[] waskarSalt = RandomNumberGenerator.GetBytes(16);
		byte[] waskarHash = PasswordHash("123456", waskarSalt, 210000);
		await using SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(ct));
		try
		{
			await using SqlCommand command = new SqlCommand("DECLARE @lock int;\nEXEC @lock=sys.sp_getapplock @Resource='V429_ACCOUNT_ACCESS_RECOVERY',@LockMode='Exclusive',@LockOwner='Transaction';\nIF @lock<0 THROW 51028,'No se pudo preparar la recuperación de acceso.',1;\n\nIF OBJECT_ID(N'dbo.account_access_recovery_migrations',N'U') IS NULL\n    CREATE TABLE dbo.account_access_recovery_migrations\n    (\n        migration_key varchar(80) NOT NULL PRIMARY KEY,\n        account_email nvarchar(320) NOT NULL,\n        applied_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()\n    );\n\n-- Preserve the principal account if it exists; create it only when absent.\nIF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE LOWER(email)=N'admin@local.com')\n    INSERT INTO dbo.admin_users\n      (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,must_change_password,permissions_json,support_team)\n    VALUES(CONVERT(char(36),NEWID()),N'admin@local.com',N'Administrador de recuperación','ADMINISTRATOR',\n           @adminSalt,@adminHash,210000,1,1,NULL,NULL);\n\n-- Reset only this named account once. Its identity, role, permissions,\n-- groups, and profile stay intact; later password changes are preserved.\nIF NOT EXISTS(SELECT 1 FROM dbo.account_access_recovery_migrations WHERE migration_key='V429_ADMIN_LOCAL_TEMPORARY_PASSWORD')\nBEGIN\n    UPDATE dbo.admin_users\n       SET password_salt=@adminSalt,password_hash=@adminHash,password_iterations=210000,\n           must_change_password=1,updated_at=SYSUTCDATETIME()\n     WHERE LOWER(email)=N'admin@local.com' AND is_active=1;\n    IF @@ROWCOUNT=1\n        INSERT INTO dbo.account_access_recovery_migrations(migration_key,account_email)\n        VALUES('V429_ADMIN_LOCAL_TEMPORARY_PASSWORD',N'admin@local.com');\nEND;\n\n-- Older supervisors who never logged in retain their stored passwords,\n-- while their first-access 123456 path is restored.\nIF NOT EXISTS(SELECT 1 FROM dbo.account_access_recovery_migrations WHERE migration_key='V428_LEGACY_FIRST_LOGIN_FLAGS')\nBEGIN\n    UPDATE dbo.admin_users\n       SET must_change_password=1,updated_at=SYSUTCDATETIME()\n     WHERE role='GROUP_ADMIN' AND is_active=1 AND last_login_at IS NULL AND must_change_password=0;\n    INSERT INTO dbo.account_access_recovery_migrations(migration_key,account_email)\n    VALUES('V428_LEGACY_FIRST_LOGIN_FLAGS',N'legacy-group-supervisors');\nEND;\n\n-- The named active account is recovered exactly once. No role, groups,\n-- permissions, or other users' passwords are changed.\nIF NOT EXISTS(SELECT 1 FROM dbo.account_access_recovery_migrations WHERE migration_key='V428_WASKAR_TEMPORARY_PASSWORD')\nBEGIN\n    UPDATE dbo.admin_users\n       SET password_salt=@waskarSalt,password_hash=@waskarHash,password_iterations=210000,\n           must_change_password=1,updated_at=SYSUTCDATETIME()\n     WHERE LOWER(email)=N'waskar.sepulveda@grupotejeda.local' AND is_active=1;\n    IF @@ROWCOUNT=1\n        INSERT INTO dbo.account_access_recovery_migrations(migration_key,account_email)\n        VALUES('V428_WASKAR_TEMPORARY_PASSWORD',N'waskar.sepulveda@grupotejeda.local');\nEND;", connection, transaction);
			command.Parameters.AddWithValue("@adminSalt", adminSalt);
			command.Parameters.AddWithValue("@adminHash", adminHash);
			command.Parameters.AddWithValue("@waskarSalt", waskarSalt);
			command.Parameters.AddWithValue("@waskarHash", waskarHash);
			await command.ExecuteNonQueryAsync(ct);
			await transaction.CommitAsync(ct);
		}
		catch
		{
			await transaction.RollbackAsync(ct);
			throw;
		}
	}

	private static byte[] RecoveryHash(string code)
	{
		return SHA256.HashData(Encoding.UTF8.GetBytes(code.Replace("-", "").Replace(" ", "").Trim()
			.ToUpperInvariant()));
	}

	private static async Task EnsureRecoverySchema(SqlConnection c, CancellationToken ct)
	{
		if (recoverySchemaReady)
		{
			return;
		}
		await RecoverySchemaLock.WaitAsync(ct);
		try
		{
			if (!recoverySchemaReady)
			{
				await using SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.account_recovery',N'U') IS NULL\nCREATE TABLE dbo.account_recovery(\n    user_id nvarchar(80) NOT NULL PRIMARY KEY,\n    code_hash varbinary(32) NULL,\n    session_version nvarchar(36) NOT NULL DEFAULT N'',\n    generated_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),\n    used_at datetime2(3) NULL\n);", c);
				await cmd.ExecuteNonQueryAsync(ct);
				recoverySchemaReady = true;
			}
		}
		finally
		{
			RecoverySchemaLock.Release();
		}
	}

	public Task<string?> GenerateRecoveryCode(string userId, string password, CancellationToken ct)
	{
		return GenerateRecoveryCodeCore(userId, password, byLogin: false, ct);
	}

	public Task<string?> GenerateRecoveryCodeFromLogin(string userName, string password, CancellationToken ct)
	{
		return GenerateRecoveryCodeCore(userName, password, byLogin: true, ct);
	}

	private async Task<string?> GenerateRecoveryCodeCore(string identifier, string password, bool byLogin, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 320 || string.IsNullOrEmpty(password) || password.Length > 128)
		{
			return null;
		}
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureRecoverySchema(c, ct);
			string text4;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text = (byLogin ? "email=@identifier" : "id=@identifier");
				string userId;
				string text3;
				await using (SqlCommand read = new SqlCommand("SELECT password_salt,password_hash,password_iterations,CONVERT(nvarchar(80),id) FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE " + text + " AND is_active=1 AND must_change_password=0", c, tx))
				{
					read.Parameters.AddWithValue("@identifier", identifier);
					string text2;
					await using (SqlDataReader r = await read.ExecuteReaderAsync(ct))
					{
						if (!(await r.ReadAsync(ct)) || !CryptographicOperations.FixedTimeEquals(PasswordHash(password, (byte[])r[0], r.GetInt32(2)), (byte[])r[1]))
						{
							text2 = null;
							goto IL_04ee;
						}
						userId = r.GetString(3);
					}
					goto end_IL_02bd;
					IL_04ee:
					text3 = text2;
					goto IL_05cc;
					end_IL_02bd:;
				}
				string raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
				string code = string.Join("-", from i in Enumerable.Range(0, 8)
					select raw.Substring(i * 4, 4));
				await using (SqlCommand read = new SqlCommand("UPDATE dbo.account_recovery SET code_hash=@hash,generated_at=SYSUTCDATETIME(),used_at=NULL WHERE user_id=@id;\nIF @@ROWCOUNT=0 INSERT dbo.account_recovery(user_id,code_hash) VALUES(@id,@hash);", c, tx))
				{
					read.Parameters.AddWithValue("@id", userId);
					read.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = RecoveryHash(code);
					await read.ExecuteNonQueryAsync(ct);
				}
				await tx.CommitAsync(ct);
				text4 = code;
				goto end_IL_025e;
				IL_05cc:
				text4 = text3;
				end_IL_025e:;
			}
			result = text4;
		}
		return result;
	}

	public async Task<bool> RecoverPassword(string userName, string code, string newPassword, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(userName) || userName.Length > 160 || string.IsNullOrWhiteSpace(code) || code.Length > 80 || newPassword.Length < 8 || newPassword.Length > 128 || newPassword == "123456")
		{
			return false;
		}
		bool result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureRecoverySchema(c, ct);
			bool flag2;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				bool flag;
				string userId;
				await using (SqlCommand user = new SqlCommand("SELECT CONVERT(nvarchar(80),id) FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE email=@email AND is_active=1", c, tx))
				{
					user.Parameters.AddWithValue("@email", userName);
					if (!(await user.ExecuteScalarAsync(ct) is string text))
					{
						flag = false;
						goto IL_0415;
					}
					userId = text;
				}
				await using (SqlCommand user = new SqlCommand("SELECT code_hash FROM dbo.account_recovery WITH(UPDLOCK,HOLDLOCK) WHERE user_id=@id AND code_hash IS NOT NULL", c, tx))
				{
					user.Parameters.AddWithValue("@id", userId);
					if (!(await user.ExecuteScalarAsync(ct) is byte[] array) || !CryptographicOperations.FixedTimeEquals(array, RecoveryHash(code)))
					{
						flag = false;
						goto IL_05dd;
					}
				}
				byte[] bytes = RandomNumberGenerator.GetBytes(16);
				byte[] value = PasswordHash(newPassword, bytes, 210000);
				await using (SqlCommand user = new SqlCommand("UPDATE dbo.admin_users SET password_salt=@salt,password_hash=@hash,password_iterations=210000,must_change_password=0,updated_at=SYSUTCDATETIME() WHERE id=@id AND is_active=1;\nUPDATE dbo.account_recovery SET code_hash=NULL,used_at=SYSUTCDATETIME(),session_version=@version WHERE user_id=@id;\nIF OBJECT_ID(N'dbo.persistent_login_tokens',N'U') IS NOT NULL\n    UPDATE dbo.persistent_login_tokens SET revoked_at=SYSUTCDATETIME() WHERE user_id=@id AND revoked_at IS NULL;", c, tx))
				{
					user.Parameters.AddWithValue("@id", userId);
					user.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = bytes;
					user.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = value;
					user.Parameters.AddWithValue("@version", Guid.NewGuid().ToString());
					await user.ExecuteNonQueryAsync(ct);
				}
				await tx.CommitAsync(ct);
				flag2 = true;
				goto end_IL_0282;
				IL_0415:
				flag2 = flag;
				goto end_IL_0282;
				IL_05dd:
				flag2 = flag;
				end_IL_0282:;
			}
			result = flag2;
		}
		return result;
	}

	internal static async Task EnsureSandraFuelImport(SqlConnection c, CancellationToken ct)
	{
		await using (SqlCommand done = new SqlCommand("IF OBJECT_ID(N'dbo.admin_users',N'U') IS NULL SELECT 1; ELSE SELECT COUNT(*) FROM dbo.application_one_time_actions WHERE action_key=@key", c))
		{
			done.Parameters.AddWithValue("@key", "SANDRA_FUEL_EXCEL_20261020");
			if (Convert.ToInt32(await done.ExecuteScalarAsync(ct)) > 0)
			{
				return;
			}
		}
		await using SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(ct));
		await using (SqlCommand done = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 50001,'No se pudo importar combustible.',1; SELECT COUNT(*) FROM dbo.application_one_time_actions WHERE action_key=@key;", c, tx))
		{
			done.Parameters.AddWithValue("@key", "SANDRA_FUEL_EXCEL_20261020");
			if (Convert.ToInt32(await done.ExecuteScalarAsync(ct)) > 0)
			{
				await tx.CommitAsync(ct);
				return;
			}
		}
		string id;
		string login;
		string name;
		await using (SqlCommand done = new SqlCommand("SELECT CONVERT(nvarchar(80),id),email,display_name FROM dbo.admin_users WHERE LOWER(LTRIM(RTRIM(email))) IN ('sandra.ramirez','sandra.ramirez@grupotejeda.local')", c, tx))
		{
			await using SqlDataReader r = await done.ExecuteReaderAsync(ct);
			if (!(await r.ReadAsync(ct)))
			{
				await r.DisposeAsync();
				await tx.CommitAsync(ct);
				return;
			}
			id = r.GetString(0);
			login = r.GetString(1);
			name = r.GetString(2);
			if (await r.ReadAsync(ct))
			{
				throw new InvalidOperationException("Hay más de una cuenta de Sandra Ramírez; no se importó combustible.");
			}
		}
		await using Stream stream = typeof(Database).Assembly.GetManifestResourceStream("SandraFuel20261020.json") ?? throw new InvalidOperationException("Falta el registro de combustible.");
		using JsonDocument source = await JsonDocument.ParseAsync(stream, default, ct);
		foreach (JsonElement form in source.RootElement.GetProperty("forms").EnumerateArray())
		{
			string number = form.GetProperty("number").GetString();
			await using SqlCommand done = new SqlCommand("SELECT COUNT(*) FROM dbo.institutional_files WHERE JSON_VALUE(metadata,'$.administrativeRequest.number')=@number", c, tx);
			done.Parameters.AddWithValue("@number", number);
			if (Convert.ToInt32(await done.ExecuteScalarAsync(ct)) <= 0)
			{
				byte[] array = Convert.FromBase64String(form.GetProperty("pdfBase64").GetString());
				string text = form.GetProperty("date").GetString();
				DateTime utcNow = DateTime.UtcNow;
				JsonElement value = JsonSerializer.SerializeToElement(new
				{
					importKey = "SANDRA_FUEL_EXCEL_20261020",
					sourceFile = source.RootElement.GetProperty("sourceFile").GetString(),
					sourceSha256 = source.RootElement.GetProperty("sourceSha256").GetString(),
					sheet = "Hoja1",
					fuelDate = text,
					fuelPeriod = text.Substring(0, 7),
					sections = form.GetProperty("sections"),
					sourceTotal = form.GetProperty("sourceTotal"),
					calculatedTotal = form.GetProperty("calculatedTotal"),
					rowCount = form.GetProperty("rowCount"),
					recordedAmounts = form.GetProperty("recordedAmounts"),
					sourceCells = form.GetProperty("sourceCells"),
					notes = "Registro diario de gasolina importado del Excel. Las celdas vacías se conservan."
				});
				InstitutionalFile institutionalFile = new InstitutionalFile();
				institutionalFile.Area = "communications";
				institutionalFile.Channel = "requirements";
				institutionalFile.Title = "Gasolina · gasto diario · " + text;
				institutionalFile.Description = "Registro importado · " + text + " · " + form.GetProperty("rowCount").GetInt32() + " filas de vehículos";
				institutionalFile.OriginalFileName = number + ".pdf";
				institutionalFile.ContentType = "application/pdf";
				institutionalFile.Size = array.Length;
				institutionalFile.OwnerUserName = id;
				institutionalFile.OwnerLogin = login;
				institutionalFile.OwnerDisplayName = name;
				institutionalFile.Department = "Administración";
				institutionalFile.RecipientDepartment = "Administración";
				institutionalFile.Status = "registered";
				institutionalFile.CreatedUtc = utcNow;
				institutionalFile.SentUtc = utcNow;
				institutionalFile.AdministrativeRequest = new AdministrativeRequestInfo(number, "OTRO", "EMPLEADO", "Control de combustible", null, "Importación de registros existentes", null, "FUEL_LOG", value);
				institutionalFile.History = new List<InstitutionalEvent>(1)
				{
					new InstitutionalEvent("import", "Importación de registros existentes", "system:import", utcNow, "", null, null, null, null)
				};
				InstitutionalFile value2 = institutionalFile;
				await using SqlCommand insert = new SqlCommand("INSERT dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES(@owner,N'Administración','communications','requirements',@json,@bytes)", c, tx);
				insert.Parameters.AddWithValue("@owner", id);
				insert.Parameters.AddWithValue("@json", JsonSerializer.Serialize(value2, InstitutionalJson));
				SqlParameterCollection parameters = insert.Parameters;
				parameters.AddWithValue("@bytes", await StorageFiles.Store(array, "generated-document", ct));
				await insert.ExecuteNonQueryAsync(ct);
			}
		}
		await using SqlCommand marker = new SqlCommand("INSERT dbo.application_one_time_actions(action_key) VALUES(@key)", c, tx);
		marker.Parameters.AddWithValue("@key", "SANDRA_FUEL_EXCEL_20261020");
		await marker.ExecuteNonQueryAsync(ct);
		await tx.CommitAsync(ct);
	}

	public async Task<List<StorageDocumentRow>> StorageDocuments(StorageUploadQueue queue, CancellationToken ct)
	{
		List<StorageDocumentRow> result;
		await using (SqlConnection connection = await Open(ct))
		{
			result = await StorageDocumentInventory.Read(connection, queue, ct);
		}
		return result;
	}

	public async Task<StorageMigrationReport> CopyLegacyFiles(CancellationToken ct, Action<string> progress)
	{
		StorageMigrationReport result;
		await using (SqlConnection connection = await Open(ct))
		{
			result = await StorageMigrationRunner.Run(connection, apply: true, ct, progress);
		}
		return result;
	}

	public async Task<StorageMigrationRunner.ReleaseStatus> ReleaseStorageCopies(CancellationToken ct)
	{
		StorageMigrationRunner.ReleaseStatus result;
		await using (SqlConnection connection = await Open(ct))
		{
			result = await StorageMigrationRunner.ReleaseVerified(connection, ct);
		}
		return result;
	}

	public async Task<(int Pending, int Verified)> FinalizeStorageCopies(CancellationToken ct)
	{
		(int Pending, int Verified) result;
		await using (SqlConnection connection = await Open(ct))
		{
			result = await StorageMigrationRunner.FinalizePending(connection, ct);
		}
		return result;
	}

	public static void InvalidateSupportStandard()
	{
		supportStandard = null;
	}

	public async Task EnsureUnifiedSupportProfile(Func<AdminUserRow, AdminUserRow> effective, CancellationToken ct)
	{
		if (supportStandard != null)
		{
			return;
		}
		await SupportStandardLock.WaitAsync(ct);
		try
		{
			if (supportStandard != null)
			{
				return;
			}
			List<AdminUserRow> users = await AdminUsers(ct);
			List<AdminUserRow> list = users.Where((AdminUserRow u) => u.Role == "GENERAL_SERVICES" && Enumerable.Contains(new string[2] { "ross.mariel", "ross.mariel@grupotejeda.local" }, u.Email.Trim().ToLowerInvariant())).ToList();
			if (list.Count != 1)
			{
				await AlignEnedraineWithoutSupportReference(users, effective, ct);
				return;
			}
			AdminUserRow source = list[0];
			Dictionary<string, bool> reference = effective(source).EffectivePermissions;
			await using SqlConnection connection = await Open(ct);
			await EnsureUserAccessPresetSchema(connection, ct);
			await using SqlTransaction transaction = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
			try
			{
				await using (SqlCommand guard = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource='unified-support-profile',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @result<0 THROW 50001,'No se pudo bloquear la actualización de perfiles.',1;", connection, transaction))
				{
					await guard.ExecuteNonQueryAsync(ct);
				}
				await using (SqlCommand guard = new SqlCommand("SELECT permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", connection, transaction))
				{
					guard.Parameters.AddWithValue("@id", source.Id.ToString());
					Dictionary<string, bool> dictionary = ReadPermissions((await guard.ExecuteScalarAsync(ct)) as string);
					if (dictionary.Count != (source.Permissions?.Count ?? 0) || dictionary.Any((KeyValuePair<string, bool> p) => source.Permissions?.GetValueOrDefault(p.Key) != p.Value))
					{
						throw new InvalidOperationException("Los permisos de Ross Mariel cambiaron; vuelve a intentarlo.");
					}
				}
				await using (SqlCommand guard = new SqlCommand("IF OBJECT_ID(N'dbo.support_permission_backups',N'U') IS NULL\r\nCREATE TABLE dbo.support_permission_backups(id uniqueidentifier PRIMARY KEY,user_id nvarchar(80) NOT NULL,permissions_json nvarchar(max) NULL,reference_user_id nvarchar(80) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", connection, transaction))
				{
					await guard.ExecuteNonQueryAsync(ct);
				}
				Guid? alignedCallCenterUser = await AlignEnedraineCallCenter(connection, transaction, reference, ct);
				foreach (AdminUserRow user in users.Where((AdminUserRow u) =>
				{
					if (u.Id != source.Id)
					{
						Guid id = u.Id;
						Guid? guid = alignedCallCenterUser;
						if (id != guid)
						{
							return SupportProfilePolicy.IsSupport(u.Role, u.SupportTeam, u.Permissions);
						}
					}
					return false;
				}))
				{
					if (user.Permissions != null && user.Permissions.GetValueOrDefault("usesCustomizedPermissions")) continue;
					Dictionary<string, bool> updated = (CallCenterSupportProfile.Applies(user.Role, user.SupportTeam, user.Permissions) ? CallCenterSupportProfile.Normalize(user.Permissions) : SupportProfilePolicy.Standard(user.Permissions, reference));
					if (user.Permissions != null && updated.Count == user.Permissions.Count && updated.All((KeyValuePair<string, bool> p) => user.Permissions.TryGetValue(p.Key, out var value) && value == p.Value))
					{
						continue;
					}
					await using SqlCommand guard = new SqlCommand("SELECT permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", connection, transaction);
					guard.Parameters.AddWithValue("@id", user.Id.ToString());
					string text = (await guard.ExecuteScalarAsync(ct)) as string;
					Dictionary<string, bool> dictionary2 = ReadPermissions(text) ?? new Dictionary<string, bool>();
					if (dictionary2.Count != (user.Permissions?.Count ?? 0) || dictionary2.Any((KeyValuePair<string, bool> p) => user.Permissions?.GetValueOrDefault(p.Key) != p.Value))
					{
						throw new InvalidOperationException("Los permisos cambiaron durante la actualización; vuelve a intentarlo.");
					}
					await using SqlCommand command = new SqlCommand("DECLARE @previous nvarchar(max)=(SELECT permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK) WHERE id=@id);\r\nIF ISNULL(@previous,N'')<>ISNULL(@expected,N'') THROW 50001,'Los permisos cambiaron durante la actualización; vuelve a intentarlo.',1;\r\nINSERT dbo.support_permission_backups(id,user_id,permissions_json,reference_user_id) VALUES(NEWID(),@id,@previous,@source);\r\nUPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE id=@id;", connection, transaction);
					command.Parameters.AddWithValue("@id", user.Id.ToString());
					command.Parameters.AddWithValue("@source", source.Id.ToString());
					command.Parameters.AddWithValue("@expected", ((object)text) ?? ((object)DBNull.Value));
					command.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(updated));
					await command.ExecuteNonQueryAsync(ct);
				}
				foreach (string item in DepartmentAccounts.Names.Keys.Where((string code) =>
				{
					bool flag = ((code == "WAREHOUSE" || code == "WORKSHOP") ? true : false);
					return !flag;
				}))
				{
					await using SqlCommand command = new SqlCommand("UPDATE dbo.user_access_presets SET permissions_json=@permissions,updated_by=@source,updated_at=SYSUTCDATETIME() WHERE department=@department AND profile='SUPPORT';\r\nIF @@ROWCOUNT=0 INSERT dbo.user_access_presets(department,profile,permissions_json,updated_by,updated_at) VALUES(@department,'SUPPORT',@permissions,@source,SYSUTCDATETIME());", connection, transaction);
					command.Parameters.AddWithValue("@department", item);
					command.Parameters.AddWithValue("@source", source.Id.ToString());
					command.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize((item == "CALL_CENTER") ? CallCenterSupportProfile.Permissions() : reference.Where((KeyValuePair<string, bool> p) => p.Key.StartsWith("can")).ToDictionary((KeyValuePair<string, bool> p) => p.Key, (KeyValuePair<string, bool> p) => p.Value)));
					await command.ExecuteNonQueryAsync(ct);
				}
				await transaction.CommitAsync(ct);
				supportStandard = new Dictionary<string, bool>(reference);
			}
			catch
			{
				await transaction.RollbackAsync(ct);
				throw;
			}
		}
		finally
		{
			SupportStandardLock.Release();
		}
	}

	internal static async Task RestoreWarehouseAuthorizers(SqlConnection connection, CancellationToken ct)
	{
		await using SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		try
		{
			await using (SqlCommand schema = new SqlCommand("DECLARE @lock int; EXEC @lock=sys.sp_getapplock @Resource='warehouse-authorizers-v441',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;\nIF @lock<0 THROW 50001,'No se pudo restaurar la autorización de Almacén.',1;\nIF OBJECT_ID(N'dbo.warehouse_authorization_recovery',N'U') IS NULL\n    CREATE TABLE dbo.warehouse_authorization_recovery(user_id nvarchar(80) PRIMARY KEY,previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());", connection, tx))
			{
				await schema.ExecuteNonQueryAsync(ct);
			}
			List<(string Id, string? Raw)> users = new List<(string, string)>();
			await using (SqlCommand schema = new SqlCommand("SELECT CONVERT(nvarchar(80),id),permissions_json FROM dbo.admin_users WITH(UPDLOCK,HOLDLOCK)\nWHERE (role='WAREHOUSE' OR (role='TECHNOLOGY' AND support_team='WAREHOUSE'))\n  AND LOWER(LTRIM(RTRIM(email))) IN('ana.rodriguez','ana.rodriguez@grupotejeda.local','diorly.abreu','diorly.abreu@grupotejeda.local')\n  AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_authorization_recovery r WHERE r.user_id=CONVERT(nvarchar(80),admin_users.id));", connection, tx))
			{
				await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
				while (await reader.ReadAsync(ct))
				{
					users.Add((reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
				}
			}
			foreach (var item in users)
			{
				Dictionary<string, bool> dictionary = JsonSerializer.Deserialize<Dictionary<string, bool>>(item.Raw ?? "{}") ?? new Dictionary<string, bool>();
				if (!dictionary.GetValueOrDefault("profile:WAREHOUSE_REQUESTER"))
				{
					dictionary["canAuthorizeWarehouseRequests"] = true;
					dictionary["canDecideDocuments"] = true;
					await using SqlCommand schema = new SqlCommand("INSERT dbo.warehouse_authorization_recovery(user_id,previous_permissions) VALUES(@id,@previous);\nUPDATE dbo.admin_users SET permissions_json=@permissions,updated_at=SYSUTCDATETIME() WHERE CONVERT(nvarchar(80),id)=@id;", connection, tx);
					schema.Parameters.AddWithValue("@id", item.Id);
					schema.Parameters.AddWithValue("@previous", ((object)item.Raw) ?? ((object)DBNull.Value));
					schema.Parameters.AddWithValue("@permissions", JsonSerializer.Serialize(dictionary));
					await schema.ExecuteNonQueryAsync(ct);
				}
			}
			await tx.CommitAsync(ct);
		}
		catch
		{
			await tx.RollbackAsync(ct);
			throw;
		}
	}

	public async Task<List<WarehouseQueueItem>> InstitutionalWarehouseQueue(DocumentActor actor, CancellationToken ct)
	{
		if (!actor.Allowed)
		{
			return new List<WarehouseQueueItem>();
		}
		List<WarehouseQueueItem> result2;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureInstitutionalSchema(connection, ct);
			List<WarehouseQueueItem> list2;
			await using (SqlCommand command = new SqlCommand("SELECT id,JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(metadata,'$.receiptSignature',NULL),'$.history',NULL),'$.administrativeRequest.formData',NULL)\nFROM dbo.institutional_files\nWHERE area='communications' AND channel='requirements'\n  AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'\n  AND COALESCE(JSON_VALUE(metadata,'$.trashed'),'false')<>'true'\nORDER BY id DESC", connection))
			{
				List<WarehouseQueueItem> result = new List<WarehouseQueueItem>();
				List<WarehouseQueueItem> list;
				await using (SqlDataReader reader = await command.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						InstitutionalFile institutionalFile = JsonSerializer.Deserialize<InstitutionalFile>(reader.GetString(1), InstitutionalJson);
						if (institutionalFile != null && !institutionalFile.Trashed && institutionalFile.Accessible(actor))
						{
							institutionalFile.Id = reader.GetInt64(0);
							institutionalFile.NormalizeWarehouseReceiptState();
							result.Add(new WarehouseQueueItem(institutionalFile.Id, institutionalFile.Title, institutionalFile.OriginalFileName, institutionalFile.Department, "Almacén", institutionalFile.OwnerUserName, institutionalFile.OwnerDisplayName, institutionalFile.Status, institutionalFile.CreatedUtc, institutionalFile.DecisionUtc, institutionalFile.DecisionBy));
						}
					}
					list = result;
				}
				list2 = list;
			}
			result2 = list2;
		}
		return result2;
	}

	private static async Task EnsureRepairedInventorySchema(SqlConnection c, CancellationToken ct)
	{
		await RepairedInventorySchemaLock.WaitAsync(ct);
		try
		{
			await using SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.warehouse_repaired_receipts',N'U') IS NULL CREATE TABLE dbo.warehouse_repaired_receipts(movement_id uniqueidentifier PRIMARY KEY,source_id uniqueidentifier NOT NULL);\nIF OBJECT_ID(N'dbo.maintenance_workshop_events',N'U') IS NOT NULL\n EXEC(N'INSERT dbo.warehouse_repaired_receipts(movement_id,source_id) SELECT e.movement_id,MIN(CONVERT(varchar(36),e.source_id)) FROM dbo.maintenance_workshop_events e JOIN dbo.maintenance_movements m ON m.id=e.movement_id WHERE e.action IN(''DELIVER'',''WAREHOUSE_RECEIVE'') AND m.operational_area=''WAREHOUSE'' AND m.movement_type=''ENTRY'' AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts r WHERE r.movement_id=e.movement_id) GROUP BY e.movement_id');", c);
			await cmd.ExecuteNonQueryAsync(ct);
		}
		finally
		{
			RepairedInventorySchemaLock.Release();
		}
	}

	public async Task<string> WarehouseRepairedInventory(CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureMaintenanceSchema(c, ct);
			await EnsureRepairedInventorySchema(c, ct);
			string text2;
			await using (SqlCommand cmd = new SqlCommand("WITH received AS(SELECT m.*,r.source_id,ROW_NUMBER() OVER(PARTITION BY m.department,UPPER(LTRIM(RTRIM(m.serial_number))) ORDER BY m.occurred_at DESC,m.created_at DESC,m.id DESC) rn FROM dbo.warehouse_repaired_receipts r JOIN dbo.maintenance_movements m ON m.id=r.movement_id)\nSELECT m.id,m.source_id sourceId,m.department,m.equipment_type equipment,m.serial_number serial,m.document_number documentNumber,m.qr_token qrToken,m.received_by_name receivedBy,m.occurred_at receivedAt,\n CASE WHEN m.quantity-COALESCE(x.used,0)>0 THEN m.quantity-COALESCE(x.used,0) ELSE 0 END available\nFROM received m OUTER APPLY(SELECT SUM(e.quantity) used FROM dbo.maintenance_movements e WHERE e.operational_area='WAREHOUSE' AND e.department=m.department AND UPPER(LTRIM(RTRIM(e.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND e.movement_type IN('EXIT','NEW_DELIVERY','TRANSFER_TO_WORKSHOP','DISCHARGE') AND e.occurred_at>=m.occurred_at) x\nWHERE m.rn=1 ORDER BY m.occurred_at DESC FOR JSON PATH;", c))
			{
				StringBuilder json = new StringBuilder();
				string text;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						json.Append(reader.GetString(0));
					}
					text = ((json.Length == 0) ? "[]" : json.ToString());
				}
				text2 = text;
			}
			result = text2;
		}
		return result;
	}

	public async Task<string?> WarehouseEditRequest(long id, DocumentActor actor, WarehouseRequestEdit input, byte[] pdf, CancellationToken ct)
	{
		if (!WarehouseRequestEditing.Editor(actor))
		{
			return "Solo Diorly Abreu y Ana Rodríguez pueden editar solicitudes de Almacén.";
		}
		int? num = input.Note?.Trim().Length;
		int num2;
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			if (valueOrDefault >= 5 && valueOrDefault <= 2000)
			{
				num2 = ((input.Lines == null) ? 1 : 0);
				goto IL_00d1;
			}
		}
		num2 = 1;
		goto IL_00d1;
		IL_00d1:
		bool flag = (byte)num2 != 0;
		bool flag2 = flag;
		if (!flag2)
		{
			int valueOrDefault = input.Lines.Count;
			bool flag3 = ((valueOrDefault < 1 || valueOrDefault > 30) ? true : false);
			flag2 = flag3;
		}
		if (flag2 || input.Lines.Any((WarehouseRequestLine x) =>
		{
			bool flag4 = (object)x == null || string.IsNullOrWhiteSpace(x.ProductId);
			bool flag5 = flag4;
			if (!flag5)
			{
				int quantity = x.Quantity;
				bool flag6 = ((quantity < 1 || quantity > 10000) ? true : false);
				flag5 = flag6;
			}
			if (!flag5)
			{
				string? note = x.Note;
				if (note == null)
				{
					return false;
				}
				return note.Length > 500;
			}
			return true;
		}) || input.Lines.Sum((WarehouseRequestLine x) => x.Quantity) > 10000)
		{
			return "Selecciona artículos del inventario, cantidades válidas y el motivo del ajuste.";
		}
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureMaintenanceSchema(c, ct);
			await EnsureInstitutionalSchema(c, ct);
			await EnsureWarehouseFulfillmentSchema(c, ct);
			await EnsureRepairedInventorySchema(c, ct);
			string text3;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", c, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text = "Solicitud no encontrada.";
					}
					else
					{
						InstitutionalFile d = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						d.Id = id;
						if (!WarehouseRequestEditing.CanEdit(d, actor))
						{
							text = "La solicitud ya está preparada, entregada o no admite cambios.";
						}
						else if (d.WarehouseRevision != input.Revision)
						{
							text = "La solicitud cambió. Vuelve a abrirla antes de editar.";
						}
						else
						{
							await using (SqlCommand stockLock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", c, tx))
							{
								await stockLock.ExecuteScalarAsync(ct);
							}
							List<WarehouseRequestLine> lines = new List<WarehouseRequestLine>();
							foreach (IGrouping<string, WarehouseRequestLine> group in input.Lines.GroupBy((WarehouseRequestLine x) => x.ProductId.Trim(), StringComparer.OrdinalIgnoreCase))
							{
								(string Name, int Available)? stock = await WarehouseProductBalance(c, tx, group.Key, ct);
								if (!stock.HasValue || stock.Value.Available < group.Sum((WarehouseRequestLine x) => x.Quantity))
								{
									text = "Uno de los artículos no existe o no tiene suficientes unidades disponibles. Actualiza el inventario.";
								}
								else
								{
									if (!group.Any((WarehouseRequestLine x) => x.Name != stock.Value.Name))
									{
										continue;
									}
									text = "El nombre del artículo cambió. Actualiza el inventario.";
								}
								goto end_IL_04d8;
							}
							foreach (WarehouseRequestLine line in input.Lines)
							{
								lines.Add(line with
								{
									Kind = "EQUIPMENT",
									ProductId = line.ProductId.Trim(),
									Serial = null
								});
							}
							d.History.Add(new InstitutionalEvent("edit_warehouse", actor.Name, actor.Login, DateTime.UtcNow, d.Status, d.ReceiptCode, d.ReceiverLogin, d.ReceivedUtc, d.ReceiptSignature, null, d.WarehouseRequest, input.Note.Trim()));
							d.WarehouseRequest = d.WarehouseRequest with
							{
								Lines = lines
							};
							d.WarehouseRevision++;
							d.Status = "pending";
							d.ReceivedUtc = null;
							d.ReceivedByUserName = null;
							d.ReceiverDisplayName = null;
							d.ReceiverLogin = null;
							d.ReceiptCode = null;
							d.VerificationToken = null;
							d.ReceiptSignature = null;
							d.DecisionBy = null;
							d.DecisionUtc = null;
							d.CorrectionNote = null;
							d.WarehouseAssignmentDraft = new List<WarehouseAssignedEquipment>();
							d.WarehousePendingPurchaseLines = new List<int>();
							d.WarehousePurchaseNote = null;
							d.Size = pdf.Length;
							string text2;
							await using (SqlCommand save = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json,file_data=@bytes WHERE id=@id", c, tx))
							{
								save.Parameters.AddWithValue("@id", id);
								SqlParameterCollection parameters = save.Parameters;
								parameters.AddWithValue("@json", await StorageFiles.TransformJson(JsonSerializer.Serialize(d, InstitutionalJson), upload: true, ct));
								parameters = save.Parameters;
								parameters.AddWithValue("@bytes", await StorageFiles.Store(pdf, "institutional-document", ct));
								await save.ExecuteNonQueryAsync(ct);
								await tx.CommitAsync(ct);
								text2 = null;
							}
							text = text2;
						}
					}
					end_IL_04d8:;
				}
				text3 = text;
			}
			result = text3;
		}
		return result;
	}

	private static async Task<(string Name, int Available)?> WarehouseProductBalance(SqlConnection c, SqlTransaction tx, string product, CancellationToken ct)
	{
		(string Name, int Available)? result;
		await using (SqlCommand cmd = new SqlCommand("SELECT p.description,p.official_stock+\nCOALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END) FROM dbo.maintenance_movements m WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id) AND NOT EXISTS(SELECT 1 FROM dbo.warehouse_repaired_receipts rr JOIN dbo.maintenance_movements repaired ON repaired.id=rr.movement_id WHERE repaired.department=m.department AND UPPER(LTRIM(RTRIM(repaired.serial_number)))=UPPER(LTRIM(RTRIM(m.serial_number))) AND repaired.occurred_at<=m.occurred_at)),0)-\nCOALESCE((SELECT SUM(a.quantity) FROM dbo.warehouse_equipment_allocations a WITH(UPDLOCK,HOLDLOCK) WHERE (a.product_id=p.product_id OR (a.product_id IS NULL AND LOWER(a.equipment_name)=LOWER(p.description))) AND a.status='PENDING_PICKUP'),0)\nFROM dbo.warehouse_products p WITH(HOLDLOCK) WHERE p.is_active=1 AND p.product_id=@product", c, tx))
		{
			cmd.Parameters.AddWithValue("@product", product);
			(string Name, int Available)? tuple;
			await using (SqlDataReader r = await cmd.ExecuteReaderAsync(ct))
			{
				tuple = ((await r.ReadAsync(ct)) ? new (string, int)?((r.GetString(0), r.GetInt32(1))) : (((string, int)?)null));
			}
			result = tuple;
		}
		return result;
	}

	private static async Task EnsureWarehouseWithdrawalSchema(SqlConnection connection, CancellationToken ct)
	{
		await using SqlCommand command = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_quantity' AND definition NOT LIKE '%10000%')\nBEGIN\n    BEGIN TRANSACTION;\n    ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_quantity;\n    ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_quantity CHECK(quantity BETWEEN 1 AND 10000);\n    COMMIT TRANSACTION;\nEND;", connection);
		await command.ExecuteNonQueryAsync(ct);
	}

	public async Task<string?> WeekendLoanReport(long id, DocumentActor actor, WeekendLoanReport? input, bool confirm, CancellationToken ct)
	{
		if (confirm ? (!actor.Manager) : (!WeekendLoans.Requester(actor)))
		{
			return "No tienes acceso a esta operación del préstamo.";
		}
		if (!confirm)
		{
			if ((object)input != null)
			{
				int? num = input.Note?.Trim().Length;
				if (num.HasValue)
				{
					int valueOrDefault = num.GetValueOrDefault();
					if (valueOrDefault >= 5 && valueOrDefault <= 2000 && input.Uses != null && input.Uses.Count <= 10000 && !input.Uses.Any((WeekendLoanUse x) =>
					{
						bool flag = (object)x == null;
						bool flag2 = flag;
						if (!flag2)
						{
							int quantity = x.Quantity;
							bool flag3 = ((quantity < 1 || quantity > 10000) ? true : false);
							flag2 = flag3;
						}
						return flag2 || x.AssignedIndex < 0 || x.AgencyId == Guid.Empty;
					}))
					{
						goto IL_0158;
					}
				}
			}
			return "Revisa las cantidades utilizadas, las agencias y la nota de devolución.";
		}
		goto IL_0158;
		IL_0158:
		List<TicketAgencyRow> list = ((!confirm) ? (await TicketAgencies(actor.Id, actor.Role, ct)) : null);
		List<TicketAgencyRow> agencies = list;
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureInstitutionalSchema(c, ct);
			await EnsureMaintenanceSchema(c, ct);
			await EnsureWarehouseFulfillmentSchema(c, ct);
			string text3;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand read = new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", c, tx))
				{
					read.Parameters.AddWithValue("@id", id);
					InstitutionalFile d;
					if (!(await read.ExecuteScalarAsync(ct) is string json))
					{
						text = "Préstamo no encontrado.";
					}
					else
					{
						d = JsonSerializer.Deserialize<InstitutionalFile>(json, InstitutionalJson);
						d.Id = id;
						if (!d.Accessible(actor) || d.Trashed || (object)d.WarehouseRequest?.WeekendLoan == null || (object)d.WarehouseDelivery == null || d.WarehouseAssigned.Count == 0)
						{
							text = "Primero debe registrarse la entrega del préstamo.";
						}
						else if (!confirm)
						{
							if (d.OwnerUserName != actor.Id)
							{
								text = "Solo el solicitante puede declarar el uso del préstamo.";
							}
							else if ((object)d.WeekendLoanSettlement != null)
							{
								text = "Este préstamo ya tiene una declaración. Actualiza la pantalla.";
							}
							else
							{
								if (!input.Uses.Any((WeekendLoanUse x) => x.AssignedIndex >= d.WarehouseAssigned.Count || !agencies.Any((TicketAgencyRow a) => a.Id == x.AgencyId)) && !(from x in input.Uses
									group x by x.AssignedIndex).Any((IGrouping<int, WeekendLoanUse> g) => g.Sum((WeekendLoanUse x) => x.Quantity) > d.WarehouseAssigned[g.Key].Quantity))
								{
									List<WeekendLoanUse> uses = input.Uses.Select((WeekendLoanUse x) =>
									{
										TicketAgencyRow ticketAgencyRow = agencies.First((TicketAgencyRow a) => a.Id == x.AgencyId);
										return x with
										{
											AgencyLabel = $"{ticketAgencyRow.Codigo} · {ticketAgencyRow.Terminal} · {ticketAgencyRow.Grupo}"
										};
									}).ToList();
									d.WeekendLoanSettlement = new WeekendLoanSettlement(uses, input.Note.Trim(), actor.Name, DateTime.UtcNow);
									goto IL_0d62;
								}
								text = "El uso no puede superar las unidades entregadas y requiere una agencia válida.";
							}
						}
						else
						{
							WeekendLoanSettlement settlement = d.WeekendLoanSettlement;
							if ((object)settlement == null)
							{
								text = "Vittini debe declarar primero los equipos usados y los sobrantes.";
							}
							else
							{
								if (!settlement.ConfirmedAt.HasValue)
								{
									await using (SqlCommand stockLock = new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.maintenance_movements WITH(TABLOCKX,HOLDLOCK)", c, tx))
									{
										await stockLock.ExecuteScalarAsync(ct);
									}
									int i;
									for (i = 0; i < d.WarehouseAssigned.Count; i++)
									{
										WarehouseAssignedEquipment warehouseAssignedEquipment = d.WarehouseAssigned[i];
										int num2 = warehouseAssignedEquipment.Quantity - settlement.Uses.Where((WeekendLoanUse x) => x.AssignedIndex == i).Sum((WeekendLoanUse x) => x.Quantity);
										if (num2 > 0)
										{
											string value = $"DEV-PRE-{id}-{i}";
											await using SqlCommand stockLock = new SqlCommand("INSERT dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,signature_data,occurred_at)\nVALUES(NEWID(),NULL,NULL,@department,NULL,@owner,'ENTRY',@equipment,@product,N'Devolución de sobrantes de préstamo',@serial,@quantity,@note,@actorId,@actorName,'WAREHOUSE',@number,CONCAT('REAL-MNT|',@number),@owner,@actorName,N'Almacén central',NULL,SYSUTCDATETIME())", c, tx);
											stockLock.Parameters.AddWithValue("@department", WarehouseDepartmentCode(d.Department) ?? "TECHNOLOGY");
											stockLock.Parameters.AddWithValue("@owner", d.OwnerDisplayName);
											stockLock.Parameters.AddWithValue("@equipment", warehouseAssignedEquipment.Name);
											stockLock.Parameters.AddWithValue("@product", ((object)warehouseAssignedEquipment.ProductId) ?? ((object)DBNull.Value));
											stockLock.Parameters.AddWithValue("@serial", (warehouseAssignedEquipment.Serial == "CONTROL POR CANTIDAD") ? "" : warehouseAssignedEquipment.Serial);
											stockLock.Parameters.AddWithValue("@quantity", num2);
											stockLock.Parameters.AddWithValue("@note", "Préstamo " + d.WarehouseRequest.Number + ". " + settlement.Note);
											stockLock.Parameters.AddWithValue("@actorId", actor.Id);
											stockLock.Parameters.AddWithValue("@actorName", actor.Name);
											stockLock.Parameters.AddWithValue("@number", value);
											await stockLock.ExecuteNonQueryAsync(ct);
										}
									}
									d.WeekendLoanSettlement = settlement with
									{
										ConfirmedBy = actor.Name,
										ConfirmedAt = DateTime.UtcNow
									};
									goto IL_0d62;
								}
								text = null;
							}
						}
					}
					goto end_IL_04de;
					IL_0d62:
					d.History.Add(new InstitutionalEvent(confirm ? "loan_return_confirmed" : "loan_usage_reported", actor.Name, actor.Login, DateTime.UtcNow, d.Status, d.ReceiptCode, d.ReceiverLogin, d.ReceivedUtc, null));
					string text2;
					await using (SqlCommand save = new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id", c, tx))
					{
						save.Parameters.AddWithValue("@id", id);
						save.Parameters.AddWithValue("@json", JsonSerializer.Serialize(d, InstitutionalJson));
						await save.ExecuteNonQueryAsync(ct);
						await tx.CommitAsync(ct);
						text2 = null;
					}
					text = text2;
					end_IL_04de:;
				}
				text3 = text;
			}
			result = text3;
		}
		return result;
	}

	public async Task<string> WorkshopAvailableInventory(CancellationToken ct)
	{
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureWorkshopSchema(c, ct);
			await EnsureWorkshopStockExitSchema(c, ct);
			string text2;
			await using (SqlCommand cmd = new SqlCommand("SELECT * FROM (\n SELECT id,equipment,serial,notes,created_at createdAt,created_by_name createdBy,CAST('DIRECT' AS varchar(20)) origin FROM dbo.workshop_available_inventory\n UNION ALL\n SELECT m.id,m.equipment_type,m.serial_number,m.notes,w.updated_at,m.created_by_name,'REPAIRED' FROM dbo.maintenance_workshop_cases w JOIN dbo.maintenance_movements m ON m.id=w.source_id WHERE w.status='INVENTORY'\n) inventory WHERE NOT EXISTS(SELECT 1 FROM dbo.workshop_stock_exits x WHERE x.stock_id=inventory.id) ORDER BY createdAt DESC FOR JSON PATH", c))
			{
				StringBuilder json = new StringBuilder();
				string text;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						json.Append(reader.GetString(0));
					}
					text = ((json.Length == 0) ? "[]" : json.ToString());
				}
				text2 = text;
			}
			result = text2;
		}
		return result;
	}

	public async Task<string?> CreateWorkshopAvailableInventory(WorkshopAvailableIntake body, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanOperate)
		{
			return "No tienes permiso para registrar inventario de Taller.";
		}
		bool flag = body.BatchId == Guid.Empty || string.IsNullOrWhiteSpace(body.Equipment) || body.Equipment.Length > 100 || (body.Notes?.Length ?? 0) > 2000 || body.Serials == null;
		bool flag2 = flag;
		if (!flag2)
		{
			int num = body.Serials.Length;
			bool flag3 = ((num < 1 || num > 100) ? true : false);
			flag2 = flag3;
		}
		if (flag2 || body.Serials.Any((string s) => string.IsNullOrWhiteSpace(s) || s.Trim().Length > 120))
		{
			return "Completa equipo y un serial por unidad (máximo 100).";
		}
		string[] serials = body.Serials.Select((string s) => s.Trim().ToUpperInvariant()).ToArray();
		if (serials.Distinct().Count() != serials.Length)
		{
			return "Los seriales no pueden repetirse.";
		}
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureWorkshopSchema(c, ct);
			string text2;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand cmd = new SqlCommand("DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='WorkshopIntake',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 THROW 50000,'No se pudo bloquear el registro.',1;\nIF EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch)\nBEGIN SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch AND payload_hash=@hash) THEN N'' ELSE N'Este registro ya existe con otros datos.' END; RETURN; END;\nIF EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.workshop_available_inventory i ON UPPER(i.serial)=s.value)\n OR EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.maintenance_movements m ON UPPER(LTRIM(RTRIM(m.serial_number)))=s.value LEFT JOIN dbo.maintenance_workshop_cases w ON w.source_id=m.id WHERE m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' AND COALESCE(w.status,'SENT') NOT IN('DELIVERED','DISCHARGED'))\nBEGIN SELECT N'Un serial ya pertenece al inventario o a una orden de Taller.'; RETURN; END;\nINSERT dbo.workshop_intake_batches(batch_id,payload_hash) VALUES(@batch,@hash);\nINSERT dbo.workshop_available_inventory(id,batch_id,equipment,serial,notes,created_by,created_by_name)\nSELECT NEWID(),@batch,@equipment,value,@notes,@user,@name FROM OPENJSON(@serials);\nSELECT N'';", c, tx))
				{
					cmd.Parameters.AddWithValue("@batch", body.BatchId);
					cmd.Parameters.AddWithValue("@hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("STOCK:" + JsonSerializer.Serialize(body with
					{
						Serials = serials
					})))));
					cmd.Parameters.AddWithValue("@serials", JsonSerializer.Serialize(serials));
					cmd.Parameters.AddWithValue("@equipment", body.Equipment.Trim());
					cmd.Parameters.AddWithValue("@notes", Db(body.Notes));
					cmd.Parameters.AddWithValue("@user", actor.Id);
					cmd.Parameters.AddWithValue("@name", actor.Name);
					string error = (string)(await cmd.ExecuteScalarAsync(ct));
					if (!string.IsNullOrEmpty(error))
					{
						await tx.RollbackAsync(ct);
						text = error;
					}
					else
					{
						await tx.CommitAsync(ct);
						text = null;
					}
				}
				text2 = text;
			}
			result = text2;
		}
		return result;
	}

	internal static JsonElement FiscalizedStockSource()
	{
		return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "workshop-fiscalized-20260930.json")));
	}

	public async Task<FiscalizedImportResult> ImportFiscalizedWorkshopStock(bool includeWithoutSerial, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanOperate)
		{
			return new FiscalizedImportResult(0, 0, "No tienes permiso para cargar el inventario de Taller.");
		}
		List<FiscalizedStockItem> items = FiscalizedStockSource().GetProperty("items").Deserialize<List<FiscalizedStockItem>>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
		if (includeWithoutSerial)
		{
			items.Add(new FiscalizedStockItem("MAQUINA TOUCH 2CONNET MAQUINA", "INT-TAL-FISC-20260930-0068", "Código interno, no es un serial de fabricante. Original: SIN SERIAL. Inventario de equpos fiscalizados.xlsm · Hoja1, fila 68."));
		}
		int count = items.Count;
		bool flag = ((count < 1 || count > 1000) ? true : false);
		if (flag || items.Any((FiscalizedStockItem i) => string.IsNullOrWhiteSpace(i.Serial) || i.Serial.Length > 120 || string.IsNullOrWhiteSpace(i.Equipment) || i.Equipment.Length > 100 || i.Notes.Length > 2000) || items.Select((FiscalizedStockItem i) => i.Serial.Trim().ToUpperInvariant()).Distinct().Count() != items.Count)
		{
			throw new InvalidOperationException("El inventario fiscalizado no supera la validación.");
		}
		FiscalizedImportResult result2;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureWorkshopSchema(c, ct);
			FiscalizedImportResult fiscalizedImportResult2;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				FiscalizedImportResult fiscalizedImportResult;
				await using (SqlCommand cmd = new SqlCommand("DECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='WorkshopIntake',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 THROW 50000,'No se pudo bloquear el inventario.',1;\nDECLARE @items TABLE(serial nvarchar(120),equipment nvarchar(100),notes nvarchar(2000));\nINSERT @items SELECT UPPER(LTRIM(RTRIM(serial))),equipment,notes FROM OPENJSON(@json) WITH(serial nvarchar(120),equipment nvarchar(100),notes nvarchar(2000));\nIF EXISTS(SELECT 1 FROM @items i JOIN dbo.maintenance_movements m ON UPPER(LTRIM(RTRIM(m.serial_number)))=i.serial LEFT JOIN dbo.maintenance_workshop_cases w ON w.source_id=m.id WHERE m.operational_area='WORKSHOP' AND m.movement_type IN('ENTRY','TRANSFER_TO_WORKSHOP') AND COALESCE(w.status,'SENT') NOT IN('DELIVERED','DISCHARGED'))\nBEGIN SELECT 0 added,0 existing,N'Un serial del archivo ya está en una orden de Taller. No se agregó ningún equipo; revisa el inventario antes de continuar.' error; RETURN; END;\nDECLARE @existing int=(SELECT COUNT(*) FROM @items i JOIN dbo.workshop_available_inventory s ON UPPER(LTRIM(RTRIM(s.serial)))=i.serial);\nINSERT dbo.workshop_available_inventory(id,batch_id,equipment,serial,notes,created_by,created_by_name)\nSELECT NEWID(),@batch,i.equipment,i.serial,i.notes,@user,@actor FROM @items i WHERE NOT EXISTS(SELECT 1 FROM dbo.workshop_available_inventory s WHERE UPPER(LTRIM(RTRIM(s.serial)))=i.serial);\nSELECT @@ROWCOUNT added,@existing existing,CAST(NULL AS nvarchar(2000)) error;", c, tx))
				{
					cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(items, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
					cmd.Parameters.AddWithValue("@batch", Guid.Parse("b7e7a306-0930-4418-a68f-3cbfd30f3ae2"));
					cmd.Parameters.AddWithValue("@user", actor.Id);
					cmd.Parameters.AddWithValue("@actor", actor.Name);
					FiscalizedImportResult result;
					await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
					{
						await reader.ReadAsync(ct);
						result = new FiscalizedImportResult(reader.GetInt32(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2));
					}
					if (result.Error != null)
					{
						await tx.RollbackAsync(ct);
					}
					else
					{
						await tx.CommitAsync(ct);
					}
					fiscalizedImportResult = result;
				}
				fiscalizedImportResult2 = fiscalizedImportResult;
			}
			result2 = fiscalizedImportResult2;
		}
		return result2;
	}

	public async Task<string?> CreateWorkshopIntake(WorkshopIntake body, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanOperate)
		{
			return "Solo Vittini, Michael y el administrador principal pueden registrar equipos en Taller.";
		}
		bool flag = body.BatchId == Guid.Empty;
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3;
			switch (body.Department)
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
			flag2 = !flag3;
		}
		bool flag4 = flag2 || string.IsNullOrWhiteSpace(body.Equipment) || body.Equipment.Length > 100 || string.IsNullOrWhiteSpace(body.Failure) || body.Failure.Length > 200 || (body.Notes?.Length ?? 0) > 2000 || body.Serials == null;
		bool flag5 = flag4;
		if (!flag5)
		{
			int num = body.Serials.Length;
			bool flag3 = ((num < 1 || num > 100) ? true : false);
			flag5 = flag3;
		}
		if (flag5 || body.Serials.Any((string s) => string.IsNullOrWhiteSpace(s) || s.Trim().Length > 120))
		{
			return "Completa equipo, motivo y un serial por unidad (máximo 100).";
		}
		string[] serials = body.Serials.Select((string s) => s.Trim().ToUpperInvariant()).ToArray();
		if (serials.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serials.Length)
		{
			return "Los seriales no pueden repetirse.";
		}
		await PrepareWorkshopRenewal(actor, ct);
		string result;
		await using (SqlConnection c = await Open(ct))
		{
			await EnsureWorkshopSchema(c, ct);
			string text2;
			await using (SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				string text;
				await using (SqlCommand cmd = new SqlCommand("DECLARE @lock int;\nEXEC @lock=sys.sp_getapplock @Resource='WorkshopIntake',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 THROW 50000,'No se pudo bloquear la entrada de Taller.',1;\nIF EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch)\nBEGIN\n SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.workshop_intake_batches WHERE batch_id=@batch AND payload_hash=@hash) THEN N'' ELSE N'La entrada ya existe con datos diferentes.' END; RETURN;\nEND;\nIF @agency IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.agencies WHERE id=@agency) BEGIN SELECT N'Agencia no disponible.'; RETURN; END;\nIF EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.workshop_available_inventory i ON UPPER(i.serial)=s.value)\n  BEGIN SELECT N'Un serial está registrado en el inventario disponible de Taller.'; RETURN; END;\nIF EXISTS(SELECT 1 FROM OPENJSON(@serials) s JOIN dbo.maintenance_movements m ON UPPER(LTRIM(RTRIM(m.serial_number)))=s.value\n LEFT JOIN dbo.maintenance_workshop_cases wc ON wc.source_id=m.id\n  WHERE (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY')) AND COALESCE(wc.status,'SENT') NOT IN('DELIVERED','DISCHARGED')\n    AND NOT EXISTS(SELECT 1 FROM dbo.workshop_reset_log r WHERE r.reset_key='V418_20260929_WORKSHOP_PERMANENT_RESET' AND m.operational_area='WAREHOUSE' AND m.created_at<=r.completed_at)\n   AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events e WHERE e.movement_id=m.id))\nBEGIN SELECT N'Uno de los seriales ya está activo en Taller.'; RETURN; END;\nINSERT dbo.workshop_intake_batches(batch_id,payload_hash) VALUES(@batch,@hash);\nINSERT dbo.maintenance_movements(id,agency_id,department,technician_name,movement_type,equipment_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,destination_name,occurred_at)\nSELECT NEWID(),@agency,@department,@name,'ENTRY',@equipment,@failure,s.value,1,@notes,@user,@name,'WORKSHOP',\n CONCAT('TAL-',REPLACE(CONVERT(varchar(36),@batch),'-',''),'-',s.[key]),CONCAT('REAL-MNT|TAL-',REPLACE(CONVERT(varchar(36),@batch),'-',''),'-',s.[key]),N'Taller',SYSUTCDATETIME()\nFROM OPENJSON(@serials) s;\nSELECT N'';", c, tx))
				{
					cmd.Parameters.AddWithValue("@batch", body.BatchId);
					cmd.Parameters.AddWithValue("@hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body with
					{
						Serials = serials
					})))));
					cmd.Parameters.AddWithValue("@serials", JsonSerializer.Serialize(serials));
					cmd.Parameters.AddWithValue("@agency", (!body.AgencyId.HasValue) ? ((IConvertible)DBNull.Value) : ((IConvertible)body.AgencyId.Value.ToString()));
					cmd.Parameters.AddWithValue("@department", body.Department);
					cmd.Parameters.AddWithValue("@equipment", body.Equipment.Trim());
					cmd.Parameters.AddWithValue("@failure", body.Failure.Trim());
					cmd.Parameters.AddWithValue("@notes", Db(body.Notes));
					cmd.Parameters.AddWithValue("@user", actor.Id);
					cmd.Parameters.AddWithValue("@name", actor.Name);
					string error = (string)(await cmd.ExecuteScalarAsync(ct));
					if (!string.IsNullOrEmpty(error))
					{
						await tx.RollbackAsync(ct);
						text = error;
					}
					else
					{
						await tx.CommitAsync(ct);
						text = null;
					}
				}
				text2 = text;
			}
			result = text2;
		}
		return result;
	}

	public async Task PrepareWorkshopRenewal(WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanOperate)
		{
			return;
		}
		await using SqlConnection c = await Open(ct);
		await EnsureWorkshopSchema(c, ct);
		await using SqlTransaction tx = (SqlTransaction)(await c.BeginTransactionAsync(IsolationLevel.Serializable, ct));
		await using SqlCommand cmd = new SqlCommand("DECLARE @lock int;\nEXEC @lock=sys.sp_getapplock @Resource='WorkshopPermanentResetV418',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 THROW 50000,'No se pudo preparar la limpieza de Taller.',1;\nIF NOT EXISTS(SELECT 1 FROM dbo.workshop_reset_log WHERE reset_key='V418_20260929_WORKSHOP_PERMANENT_RESET')\nBEGIN\n SELECT id INTO #workshop_delete FROM dbo.maintenance_movements WITH(UPDLOCK,HOLDLOCK) WHERE operational_area='WORKSHOP';\n DECLARE @deleted int=(SELECT COUNT(*) FROM #workshop_delete),@cutoff datetime2(3)=SYSUTCDATETIME();\n DELETE FROM dbo.maintenance_workshop_events;\n DELETE FROM dbo.maintenance_workshop_cases;\n IF OBJECT_ID('dbo.institutional_files','U') IS NOT NULL\n  DELETE f FROM dbo.institutional_files f WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(f.metadata,'$.workshopMovementId')) IN(SELECT id FROM #workshop_delete)\n   OR TRY_CONVERT(uniqueidentifier,JSON_VALUE(f.metadata,'$.maintenanceMovementId')) IN(SELECT id FROM #workshop_delete)\n   OR (f.area='communications' AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Taller');\n IF OBJECT_ID('dbo.maintenance_document_signatures','U') IS NOT NULL DELETE s FROM dbo.maintenance_document_signatures s JOIN #workshop_delete t ON t.id=s.movement_id;\n IF OBJECT_ID('dbo.maintenance_document_marks','U') IS NOT NULL DELETE s FROM dbo.maintenance_document_marks s JOIN #workshop_delete t ON t.id=s.movement_id;\n DELETE m FROM dbo.maintenance_movements m JOIN #workshop_delete t ON t.id=m.id WHERE m.operational_area='WORKSHOP';\n INSERT dbo.workshop_reset_log(reset_key,completed_at,deleted_movements,actor_login) VALUES('V418_20260929_WORKSHOP_PERMANENT_RESET',@cutoff,@deleted,@actor);\nEND;", c, tx);
		cmd.Parameters.AddWithValue("@actor", actor.Login);
		await cmd.ExecuteNonQueryAsync(ct);
		await tx.CommitAsync(ct);
	}

	private static async Task EnsureWorkshopStockExitSchema(SqlConnection connection, CancellationToken ct)
	{
		await using SqlCommand cmd = new SqlCommand("BEGIN TRANSACTION;\nDECLARE @lock int;EXEC @lock=sys.sp_getapplock @Resource='WorkshopStockExitSchema',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 BEGIN ROLLBACK;THROW 50001,N'No se pudo preparar el registro de salidas. Inténtalo nuevamente.',1;END;\nIF OBJECT_ID(N'dbo.workshop_stock_exits',N'U') IS NULL\nCREATE TABLE dbo.workshop_stock_exits(stock_id uniqueidentifier NOT NULL PRIMARY KEY,ticket_id uniqueidentifier NOT NULL,\n equipment nvarchar(100) NOT NULL,serial nvarchar(120) NOT NULL,origin varchar(20) NOT NULL,\n actor_id nvarchar(80) NOT NULL,actor_name nvarchar(160) NOT NULL,created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());\nCOMMIT;", connection);
		await cmd.ExecuteNonQueryAsync(ct);
	}

	private static async Task RecordWorkshopStockExit(SqlConnection c, SqlTransaction tx, Guid stockId, Guid ticketId, string userId, string name, CancellationToken ct)
	{
		await using SqlCommand cmd = new SqlCommand("DECLARE @resource nvarchar(255)=CONCAT('WorkshopStock:',@stock),@lock int;\nEXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\nIF @lock<0 THROW 50001,N'El equipo está siendo actualizado. Inténtalo nuevamente.',1;\nIF EXISTS(SELECT 1 FROM dbo.workshop_stock_exits WITH(UPDLOCK,HOLDLOCK) WHERE stock_id=@stock)\n THROW 50001,N'Este equipo ya tiene una salida registrada. Actualiza el inventario.',1;\nINSERT dbo.workshop_stock_exits(stock_id,ticket_id,equipment,serial,origin,actor_id,actor_name)\nSELECT id,@ticket,equipment,serial,'DIRECT',@user,@name FROM dbo.workshop_available_inventory WITH(UPDLOCK,HOLDLOCK) WHERE id=@stock;\nIF @@ROWCOUNT=0\nBEGIN\n INSERT dbo.workshop_stock_exits(stock_id,ticket_id,equipment,serial,origin,actor_id,actor_name)\n SELECT m.id,@ticket,m.equipment_type,m.serial_number,'REPAIRED',@user,@name\n FROM dbo.maintenance_workshop_cases w WITH(UPDLOCK,HOLDLOCK) JOIN dbo.maintenance_movements m ON m.id=w.source_id\n WHERE w.source_id=@stock AND w.status='INVENTORY';\n IF @@ROWCOUNT=0 THROW 50001,N'El equipo ya no está disponible en Taller.',1;\nEND;\nSELECT equipment,serial FROM dbo.workshop_stock_exits WHERE stock_id=@stock;", c, tx);
		cmd.Parameters.AddWithValue("@stock", stockId);
		cmd.Parameters.AddWithValue("@ticket", ticketId);
		cmd.Parameters.AddWithValue("@user", userId);
		cmd.Parameters.AddWithValue("@name", name);
		string equipment;
		string serial;
		await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
		{
			await reader.ReadAsync(ct);
			equipment = reader.GetString(0);
			serial = reader.GetString(1);
		}
		await AddTicketHistory(c, tx, ticketId, "WORKSHOP_STOCK_OUT", null, null, null, null, userId, name, "Equipo entregado por Taller: " + equipment + "\nSerial del equipo reemplazado: " + serial, ct);
	}

	public async Task<string> WorkshopTicketReplacementSerial(Guid ticketId, CancellationToken ct)
	{
		string result2;
		await using (SqlConnection c = await Open(ct))
		{
			string text2;
			await using (SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.workshop_stock_exits',N'U') IS NOT NULL SELECT serial FROM dbo.workshop_stock_exits WHERE ticket_id=@ticket", c))
			{
				cmd.Parameters.AddWithValue("@ticket", ticketId);
				List<string> result = new List<string>();
				string text;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						result.Add(reader.GetString(0));
					}
					text = string.Join(", ", result.Distinct());
				}
				text2 = text;
			}
			result2 = text2;
		}
		return result2;
	}

	public async Task<TicketActionResult> ReceiveWorkshopInWarehouse(Guid id, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanReceiveWarehouse)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Almacén o el administrador principal puede confirmar esta recepción.");
		}
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureWorkshopSchema(connection, ct);
			await EnsureRepairedInventorySchema(connection, ct);
			await EnsureWarehouseWithdrawalSchema(connection, ct);
			TicketActionResult ticketActionResult2;
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct)))
			{
				TicketActionResult ticketActionResult;
				await using (SqlCommand cmd = new SqlCommand("DECLARE @status varchar(20);\nSELECT @status=status FROM dbo.maintenance_workshop_cases WITH(UPDLOCK,HOLDLOCK) WHERE source_id=@id;\nIF @status IS NULL OR @status<>'DELIVERED' OR NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events WHERE source_id=@id AND action='DELIVER' AND movement_id IS NULL)\n OR EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events WHERE source_id=@id AND action='WAREHOUSE_RECEIVE')\nBEGIN SELECT 0; RETURN; END;\nDECLARE @now datetime2(3)=SYSUTCDATETIME(),@entry uniqueidentifier=NEWID(),@document varchar(40)=CONCAT('ALM-',REPLACE(CONVERT(varchar(36),NEWID()),'-',''));\nINSERT dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_name,movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,occurred_at)\nSELECT @entry,m.agency_id,m.ticket_id,m.department,@name,'ENTRY',m.equipment_type,m.component_type,N'Recibido en Almacén desde Taller',COALESCE(c.replacement_serial,m.serial_number),m.quantity,N'Recepción confirmada mediante QR de Taller',@user,@name,'WAREHOUSE',@document,CONCAT('REAL-MNT|',@document),d.actor_name,@name,N'Almacén',@now\nFROM dbo.maintenance_movements m JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id\nCROSS APPLY(SELECT TOP(1) actor_name FROM dbo.maintenance_workshop_events WHERE source_id=m.id AND action='DELIVER' ORDER BY occurred_at DESC) d WHERE m.id=@id;\nINSERT dbo.maintenance_movements(id,department,technician_name,movement_type,equipment_type,failure_cause,serial_number,quantity,notes,created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,occurred_at)\nSELECT NEWID(),m.department,@name,'EXIT',m.equipment_type,N'Recepción en Almacén confirmada',m.serial_number,m.quantity,N'Salida de Taller confirmada al recibir Almacén',@user,@name,'WORKSHOP',CONCAT(@document,'-SAL'),CONCAT('REAL-MNT|',@document,'-SAL'),d.actor_name,@name,N'Almacén',@now\nFROM dbo.maintenance_movements m CROSS APPLY(SELECT TOP(1) actor_name FROM dbo.maintenance_workshop_events WHERE source_id=m.id AND action='DELIVER' ORDER BY occurred_at DESC) d WHERE m.id=@id;\nINSERT dbo.maintenance_workshop_events(id,source_id,action,status,note,actor_user_id,actor_name,actor_login,occurred_at,movement_id)\nVALUES(NEWID(),@id,'WAREHOUSE_RECEIVE','DELIVERED',N'Equipo recibido e incorporado al inventario de Almacén',@user,@name,@login,@now,@entry);\nINSERT dbo.warehouse_repaired_receipts(movement_id,source_id) VALUES(@entry,@id);\nUPDATE dbo.maintenance_workshop_cases SET updated_at=@now WHERE source_id=@id;\nSELECT 1;", connection, tx))
				{
					cmd.Parameters.AddWithValue("@id", id);
					cmd.Parameters.AddWithValue("@user", actor.Id);
					cmd.Parameters.AddWithValue("@name", actor.Name);
					cmd.Parameters.AddWithValue("@login", actor.Login);
					if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) != 1)
					{
						await tx.RollbackAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: false, "CONFLICT", "Este equipo ya fue recibido o no está pendiente de recibir en Almacén.");
					}
					else
					{
						await tx.CommitAsync(ct);
						ticketActionResult = new TicketActionResult(Ok: true, "OK", null);
					}
				}
				ticketActionResult2 = ticketActionResult;
			}
			result = ticketActionResult2;
		}
		return result;
	}

	private static async Task EnsureWorkshopSchema(SqlConnection connection, CancellationToken ct)
	{
		await EnsureMaintenanceSchema(connection, ct);
		if (workshopSchemaReady)
		{
			return;
		}
		await WorkshopSchemaLock.WaitAsync(ct);
		try
		{
			if (!workshopSchemaReady)
			{
				await using SqlCommand command = new SqlCommand("IF OBJECT_ID(N'dbo.workshop_intake_batches',N'U') IS NULL CREATE TABLE dbo.workshop_intake_batches(batch_id uniqueidentifier PRIMARY KEY,payload_hash varchar(64) NOT NULL);\r\nIF OBJECT_ID(N'dbo.workshop_available_inventory',N'U') IS NULL CREATE TABLE dbo.workshop_available_inventory(id uniqueidentifier NOT NULL PRIMARY KEY,batch_id uniqueidentifier NOT NULL,equipment nvarchar(100) NOT NULL,serial nvarchar(120) NOT NULL UNIQUE,notes nvarchar(2000) NULL,created_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),created_by nvarchar(80) NOT NULL,created_by_name nvarchar(160) NOT NULL);\r\nIF OBJECT_ID(N'dbo.workshop_reset_log',N'U') IS NULL CREATE TABLE dbo.workshop_reset_log(reset_key varchar(100) PRIMARY KEY,completed_at datetime2(3) NOT NULL,deleted_movements int NOT NULL,actor_login nvarchar(256) NOT NULL);\r\nIF OBJECT_ID(N'dbo.maintenance_workshop_cases',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_workshop_cases(\r\n  source_id uniqueidentifier NOT NULL PRIMARY KEY REFERENCES dbo.maintenance_movements(id),\r\n  status varchar(20) NOT NULL CHECK(status IN('RECEIVED','REPAIR','REPLACEMENT','DELIVERED')),\r\n  return_name nvarchar(160) NOT NULL,\r\n  sender_user_id nvarchar(80) NULL,\r\n  received_by_user_id nvarchar(80) NOT NULL,\r\n  received_by_name nvarchar(160) NOT NULL,\r\n  received_at datetime2(3) NOT NULL,\r\n  replacement_serial nvarchar(120) NULL,\r\n  updated_at datetime2(3) NOT NULL\r\n);\r\nIF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_workshop_cases') AND name=N'CK_workshop_case_status_v418_inventory')\r\nBEGIN\r\n  DECLARE @dropStatus nvarchar(max)=N'';\r\n  SELECT @dropStatus=@dropStatus+N'ALTER TABLE dbo.maintenance_workshop_cases DROP CONSTRAINT '+QUOTENAME(name)+N';'\r\n  FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_workshop_cases') AND definition LIKE '%status%' AND definition LIKE '%RECEIVED%' AND definition LIKE '%DELIVERED%';\r\n  EXEC sp_executesql @dropStatus;\r\n  ALTER TABLE dbo.maintenance_workshop_cases ADD CONSTRAINT CK_workshop_case_status_v418_inventory CHECK(status IN('RECEIVED','REPAIR','REPLACEMENT','PART_REPLACEMENT','DISCHARGED','INVENTORY','DELIVERED'));\r\nEND;\r\nIF OBJECT_ID(N'dbo.maintenance_workshop_events',N'U') IS NULL\r\nCREATE TABLE dbo.maintenance_workshop_events(\r\n  id uniqueidentifier NOT NULL PRIMARY KEY,\r\n  source_id uniqueidentifier NOT NULL REFERENCES dbo.maintenance_workshop_cases(source_id),\r\n  action varchar(20) NOT NULL,\r\n  status varchar(20) NOT NULL,\r\n  note nvarchar(2000) NULL,\r\n  actor_user_id nvarchar(80) NOT NULL,\r\n  actor_name nvarchar(160) NOT NULL,\r\n  actor_login nvarchar(256) NOT NULL,\r\n  occurred_at datetime2(3) NOT NULL,\r\n  movement_id uniqueidentifier NULL REFERENCES dbo.maintenance_movements(id),\r\n  replacement_serial nvarchar(120) NULL\r\n);\r\nIF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.maintenance_workshop_events') AND name='IX_workshop_source')\r\n  CREATE INDEX IX_workshop_source ON dbo.maintenance_workshop_events(source_id,occurred_at);", connection);
				await command.ExecuteNonQueryAsync(ct);
				workshopSchemaReady = true;
			}
		}
		finally
		{
			WorkshopSchemaLock.Release();
		}
	}

	private static void WorkshopParams(SqlCommand cmd, WorkshopActor actor)
	{
		cmd.Parameters.AddWithValue("@operator", actor.CanOperate ? 1 : 0);
		cmd.Parameters.AddWithValue("@admin", (actor.Role == "Administrator") ? 1 : 0);
		cmd.Parameters.AddWithValue("@warehouse", actor.IsWarehouse ? 1 : 0);
		cmd.Parameters.AddWithValue("@technologyManager", actor.IsTechnologyManager ? 1 : 0);
		cmd.Parameters.AddWithValue("@user", actor.Id);
		cmd.Parameters.AddWithValue("@team", actor.Team ?? "");
		cmd.Parameters.AddWithValue("@department", actor.Department);
	}

	private async Task<string> WorkshopSnapshotRaw(WorkshopActor actor, string? code, CancellationToken ct)
	{
		code = (string.IsNullOrWhiteSpace(code) ? null : WorkshopCode.Normalize(code));
		string result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureWorkshopSchema(connection, ct);
			string cmdText = "SELECT m.id,m.document_number documentNumber,m.qr_token qrToken,m.equipment_type equipmentType,\r\n  m.serial_number serialNumber,m.quantity,m.department,m.notes,m.component_type componentType,m.agency_id agencyId,m.ticket_id ticketId,COALESCE(a.codigo,'INTERNO') agencyCode,COALESCE(a.terminal,N'Taller') agencyName,m.delivered_by_name deliveredByName,\r\n  m.created_by_name senderName,m.created_at sentAt,m.failure_cause failureCause,\r\n  COALESCE(c.status,'SENT') status,COALESCE(c.return_name,m.delivered_by_name) returnName,\r\n  c.received_by_name receivedByName,c.received_at receivedAt,c.replacement_serial replacementSerial,\r\n  CAST(CASE WHEN @operator=1 THEN 1 ELSE 0 END AS bit) canOperate,\r\n  CAST(CASE WHEN c.status='DELIVERED' AND EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events d WHERE d.source_id=m.id AND d.action='DELIVER' AND d.movement_id IS NULL) AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events r WHERE r.source_id=m.id AND r.action='WAREHOUSE_RECEIVE') THEN 1 ELSE 0 END AS bit) pendingWarehouse,\r\n  CAST(CASE WHEN (@admin=1 OR @warehouse=1) AND c.status='DELIVERED' AND EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events d WHERE d.source_id=m.id AND d.action='DELIVER' AND d.movement_id IS NULL) AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events r WHERE r.source_id=m.id AND r.action='WAREHOUSE_RECEIVE') THEN 1 ELSE 0 END AS bit) canReceiveWarehouse,\r\n  JSON_QUERY(COALESCE((SELECT e.id,e.action,e.status,e.note,e.actor_name actorName,e.actor_login actorLogin,\r\n    e.occurred_at occurredAt,e.replacement_serial replacementSerial,e.movement_id movementId\r\n    FROM dbo.maintenance_workshop_events e WHERE e.source_id=m.id ORDER BY e.occurred_at,e.id FOR JSON PATH),'[]')) events\r\nFROM dbo.maintenance_movements m\r\nLEFT JOIN dbo.agencies a ON a.id=m.agency_id\r\nLEFT JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id\r\nWHERE (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events child WHERE child.movement_id=m.id))) AND (@admin=1 OR @operator=1 OR m.created_by_user_id=@user OR m.technician_user_id=@user\r\n  OR (@warehouse=1 AND (m.operational_area='WAREHOUSE' OR c.status='DELIVERED'))\r\n  OR (@technologyManager=1 AND m.department='TECHNOLOGY')\r\n  OR m.department=@department\r\n  OR (@team='WORKSHOP' AND m.department=@department))\r\n AND NOT EXISTS(SELECT 1 FROM dbo.workshop_reset_log reset WHERE reset.reset_key='V418_20260929_WORKSHOP_PERMANENT_RESET' AND m.operational_area='WAREHOUSE' AND m.created_at<=reset.completed_at)\r\n AND (@code IS NULL OR m.qr_token=@code OR m.document_number=@code OR CONVERT(varchar(36),m.id)=@code\r\n   OR EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events e JOIN dbo.maintenance_movements child ON child.id=e.movement_id\r\n     WHERE e.source_id=m.id AND (child.qr_token=@code OR child.document_number=@code OR CONCAT(child.qr_token,'-SAL')=@code OR CONCAT(child.document_number,'-SAL')=@code)))\r\nORDER BY m.created_at DESC FOR JSON PATH;";
			string text2;
			await using (SqlCommand cmd = new SqlCommand(cmdText, connection))
			{
				WorkshopParams(cmd, actor);
				cmd.Parameters.AddWithValue("@code", Db(code));
				StringBuilder json = new StringBuilder();
				string text;
				await using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct))
				{
					while (await reader.ReadAsync(ct))
					{
						if (!reader.IsDBNull(0))
						{
							json.Append(reader.GetString(0));
						}
					}
					text = ((json.Length == 0) ? "[]" : json.ToString());
				}
				text2 = text;
			}
			result = text2;
		}
		return result;
	}

	public async Task<TicketActionResult> WorkshopTransition(Guid id, WorkshopAction input, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanOperate)
		{
			return new TicketActionResult(Ok: false, "FORBIDDEN", "Solo Taller puede avanzar el equipo; el remitente puede consultar su seguimiento.");
		}
		SqlTransaction tx;
		TicketActionResult result;
		await using (SqlConnection connection = await Open(ct))
		{
			await EnsureWorkshopSchema(connection, ct);
			tx = (SqlTransaction)(await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct));
			TicketActionResult ticketActionResult2;
			try
			{
				string status = null;
				string returnName = null;
				string sourceSerial = null;
				bool exists = false;
				bool manualEntry = false;
				TicketActionResult ticketActionResult;
				await using (SqlCommand outgoing = new SqlCommand("DECLARE @resource nvarchar(255)=CONCAT('WorkshopStock:',@id),@lock int;\r\nEXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;\r\nIF @lock<0 THROW 50000,N'El equipo está siendo actualizado. Inténtalo nuevamente.',1;\r\nIF OBJECT_ID(N'dbo.workshop_stock_exits',N'U') IS NOT NULL SELECT COUNT(*) FROM dbo.workshop_stock_exits WHERE stock_id=@id;", connection, tx))
				{
					outgoing.Parameters.AddWithValue("@id", id);
					if (Convert.ToInt32(await outgoing.ExecuteScalarAsync(ct)) > 0)
					{
						ticketActionResult = await Fail("CONFLICT", "Este equipo ya salió de Taller mediante un ticket.");
						goto IL_0543;
					}
				}
				ticketActionResult = null;
				await using (SqlCommand outgoing = new SqlCommand("SELECT COALESCE(c.status,'SENT'),COALESCE(c.return_name,NULLIF(m.delivered_by_name,N''),m.created_by_name),m.serial_number,CAST(CASE WHEN m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' THEN 1 ELSE 0 END AS bit)\r\nFROM dbo.maintenance_movements m WITH(UPDLOCK,HOLDLOCK)\r\nLEFT JOIN dbo.maintenance_workshop_cases c WITH(UPDLOCK,HOLDLOCK) ON c.source_id=m.id\r\nWHERE m.id=@id AND (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY' AND NOT EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events child WHERE child.movement_id=m.id)))\r\n    AND @operator=1\r\n    AND NOT EXISTS(SELECT 1 FROM dbo.workshop_reset_log reset WHERE reset.reset_key='V418_20260929_WORKSHOP_PERMANENT_RESET' AND m.operational_area='WAREHOUSE' AND m.created_at<=reset.completed_at)", connection, tx))
				{
					outgoing.Parameters.AddWithValue("@id", id);
					WorkshopParams(outgoing, actor);
					await using SqlDataReader reader = await outgoing.ExecuteReaderAsync(ct);
					if (await reader.ReadAsync(ct))
					{
						exists = true;
						status = reader.GetString(0);
						returnName = (reader.IsDBNull(1) ? null : reader.GetString(1));
						sourceSerial = (reader.IsDBNull(2) ? null : reader.GetString(2));
						manualEntry = reader.GetBoolean(3);
					}
				}
				if (!exists)
				{
					ticketActionResult2 = await Fail("FORBIDDEN", "El envío no existe o no pertenece a tu departamento.");
				}
				else if (input.ExpectedStatus != status)
				{
					ticketActionResult2 = await Fail("CONFLICT", "El equipo cambió de estado. Actualiza la tabla antes de continuar.");
				}
				else
				{
					string text;
					switch (input.Action)
					{
					case "RECEIVE":
						if (status == "SENT")
						{
							text = "RECEIVED";
							break;
						}
						goto default;
					case "REPAIR":
						if (status == "RECEIVED")
						{
							text = "REPAIR";
							break;
						}
						goto default;
					case "REPLACE":
						if (status == "RECEIVED")
						{
							text = "REPLACEMENT";
							break;
						}
						goto default;
					case "PART":
					{
						string text2 = status;
						if ((text2 == "RECEIVED" || text2 == "REPAIR") ? true : false)
						{
							text = "PART_REPLACEMENT";
							break;
						}
						goto default;
					}
					case "DISCHARGE":
					{
						bool flag;
						switch (status)
						{
						case "RECEIVED":
						case "REPAIR":
						case "REPLACEMENT":
						case "PART_REPLACEMENT":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (flag)
						{
							text = "DISCHARGED";
							break;
						}
						goto default;
					}
					case "WORK":
					{
						bool flag;
						switch (status)
						{
						case "REPAIR":
						case "REPLACEMENT":
						case "PART_REPLACEMENT":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (flag)
						{
							text = status;
							break;
						}
						goto default;
					}
					case "STOCK":
					{
						string text2 = status;
						if ((text2 == "REPAIR" || text2 == "PART_REPLACEMENT") ? true : false)
						{
							text = "INVENTORY";
							break;
						}
						goto default;
					}
					case "DELIVER":
					{
						bool flag;
						switch (status)
						{
						case "REPAIR":
						case "REPLACEMENT":
						case "PART_REPLACEMENT":
						case "INVENTORY":
							flag = true;
							break;
						default:
							flag = false;
							break;
						}
						if (flag)
						{
							text = "DELIVERED";
							break;
						}
						goto default;
					}
					default:
						text = null;
						break;
					}
					string next = text;
					if (next == null)
					{
						ticketActionResult2 = await Fail("CONFLICT", "Esta acción no corresponde al estado actual del equipo.");
					}
					else if (string.IsNullOrWhiteSpace(returnName))
					{
						ticketActionResult2 = await Fail("VALIDATION", "El envío original no identifica quién entregó el equipo. Debe corregirse ese dato antes de recibirlo.");
					}
					else
					{
						string note = input.Note?.Trim();
						if ((input.Action == "WORK" || input.Action == "DISCHARGE") && (note?.Length ?? 0) < 5)
						{
							ticketActionResult2 = await Fail("VALIDATION", "Describe el diagnóstico, trabajo realizado o resultado (mínimo 5 caracteres).");
						}
						else
						{
							string replacement = input.ReplacementSerial?.Trim().ToUpperInvariant();
							if (!string.IsNullOrEmpty(replacement) && (status != "REPLACEMENT" || input.Action != "WORK"))
							{
								ticketActionResult2 = await Fail("VALIDATION", "El serial sustituto se registra en la bitácora de reemplazo.");
							}
							else if (!string.IsNullOrEmpty(replacement) && string.Equals(replacement, sourceSerial?.Trim(), StringComparison.OrdinalIgnoreCase))
							{
								ticketActionResult2 = await Fail("VALIDATION", "El equipo sustituto debe tener un serial distinto al original.");
							}
							else
							{
								text = input.Action;
								if ((text == "DELIVER" || text == "STOCK") ? true : false)
								{
									await using SqlCommand outgoing = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.maintenance_workshop_events WHERE source_id=@id AND action='WORK' AND (status=@workStatus OR @workStatus='INVENTORY'))\r\n  AND (@replacement=0 OR EXISTS(SELECT 1 FROM dbo.maintenance_workshop_cases WHERE source_id=@id AND NULLIF(replacement_serial,'') IS NOT NULL))\r\nTHEN 1 ELSE 0 END", connection, tx);
									outgoing.Parameters.AddWithValue("@workStatus", status);
									outgoing.Parameters.AddWithValue("@id", id);
									outgoing.Parameters.AddWithValue("@replacement", (status == "REPLACEMENT") ? 1 : 0);
									if (Convert.ToInt32(await outgoing.ExecuteScalarAsync(ct)) != 1)
									{
										ticketActionResult = await Fail("VALIDATION", "Registra primero el trabajo realizado y, si es reemplazo, el serial del equipo sustituto.");
										goto IL_12e8;
									}
								}
								string text3 = input.Action switch
								{
									"RECEIVE" => (!manualEntry) ? "ENTRY" : null, 
									"REPAIR" => "REPAIR", 
									"REPLACE" => "REPLACEMENT", 
									"PART" => "COMPONENT_REPLACEMENT", 
									"DISCHARGE" => "DISCHARGE", 
									_ => null, 
								};
								Guid guid = Guid.NewGuid();
								Guid? guid2 = ((text3 == null) ? ((Guid?)null) : new Guid?(Guid.NewGuid()));
								string value = "TAL-" + Guid.NewGuid().ToString("N");
								await using (SqlCommand outgoing = new SqlCommand("DECLARE @now datetime2(3)=SYSUTCDATETIME();\r\nIF @action='RECEIVE'\r\n  INSERT INTO dbo.maintenance_workshop_cases(source_id,status,return_name,sender_user_id,received_by_user_id,received_by_name,received_at,updated_at)\r\n  SELECT id,@next,COALESCE(NULLIF(delivered_by_name,N''),created_by_name),created_by_user_id,@user,@actor,@now,@now FROM dbo.maintenance_movements WHERE id=@id;\r\nELSE UPDATE dbo.maintenance_workshop_cases SET status=@next,updated_at=@now,\r\n  replacement_serial=COALESCE(NULLIF(@replacementSerial,''),replacement_serial) WHERE source_id=@id;\r\nIF @movement IS NOT NULL\r\nBEGIN\r\n  INSERT INTO dbo.maintenance_movements(id,agency_id,ticket_id,department,technician_user_id,technician_name,\r\n   movement_type,equipment_type,component_type,failure_cause,serial_number,quantity,notes,\r\n   created_by_user_id,created_by_name,operational_area,document_number,qr_token,delivered_by_name,received_by_name,destination_name,occurred_at)\r\n  SELECT @movementId,m.agency_id,m.ticket_id,m.department,m.technician_user_id,@actor,\r\n   @movement,m.equipment_type,m.component_type,@cause,\r\n   CASE WHEN @action='DELIVER' THEN COALESCE(c.replacement_serial,m.serial_number) ELSE m.serial_number END,\r\n   m.quantity,@note,@user,@actor,CASE WHEN @action='DELIVER' THEN 'WAREHOUSE' ELSE 'WORKSHOP' END,@document,CONCAT('REAL-MNT|',@document),\r\n   CASE WHEN @action='RECEIVE' THEN c.return_name ELSE @actor END,\r\n   CASE WHEN @action='DELIVER' THEN N'Almacén · pendiente de recibir' ELSE @actor END,\r\n   CASE WHEN @action='DELIVER' THEN N'Almacén' ELSE N'Taller' END,@now\r\n  FROM dbo.maintenance_movements m JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id WHERE m.id=@id;\r\nEND;\r\nINSERT INTO dbo.maintenance_workshop_events(id,source_id,action,status,note,actor_user_id,actor_name,actor_login,occurred_at,movement_id,replacement_serial)\r\n VALUES(@eventId,@id,@action,@next,@note,@user,@actor,@login,@now,@movementId,NULLIF(@replacementSerial,''));", connection, tx))
								{
									outgoing.Parameters.AddWithValue("@id", id);
									outgoing.Parameters.AddWithValue("@action", input.Action);
									outgoing.Parameters.AddWithValue("@next", next);
									outgoing.Parameters.AddWithValue("@replacementSerial", Db(replacement));
									outgoing.Parameters.AddWithValue("@movement", Db(text3));
									outgoing.Parameters.AddWithValue("@movementId", Db(guid2));
									outgoing.Parameters.AddWithValue("@eventId", guid);
									outgoing.Parameters.AddWithValue("@note", Db(note));
									outgoing.Parameters.AddWithValue("@user", actor.Id);
									outgoing.Parameters.AddWithValue("@actor", actor.Name);
									outgoing.Parameters.AddWithValue("@login", actor.Login);
									outgoing.Parameters.AddWithValue("@document", value);
									SqlParameterCollection parameters = outgoing.Parameters;
									parameters.AddWithValue("@cause", input.Action switch
									{
										"RECEIVE" => (object)"Recepción automática de envío a Taller", 
										"REPAIR" => "Equipo en reparación", 
										"REPLACE" => "Equipo en reemplazo", 
										"PART" => "Reemplazo de una pieza", 
										"DISCHARGE" => "Equipo sin reparación", 
										"DELIVER" => "Equipo reparado enviado de Taller a Almacén", 
										_ => "Bitácora de trabajo", 
									});
									await outgoing.ExecuteNonQueryAsync(ct);
								}
								await tx.CommitAsync(ct);
								ticketActionResult2 = new TicketActionResult(Ok: true, "OK", null);
							}
						}
					}
				}
				goto end_IL_0316;
				IL_0543:
				ticketActionResult2 = ticketActionResult;
				goto end_IL_0316;
				IL_12e8:
				ticketActionResult2 = ticketActionResult;
				end_IL_0316:;
			}
			catch
			{
				await tx.RollbackAsync(ct);
				throw;
			}
			finally
			{
				if (tx != null)
				{
					await tx.DisposeAsync();
				}
			}
			result = ticketActionResult2;
		}
		return result;
		async Task<TicketActionResult> Fail(string code, string error)
		{
			await tx.RollbackAsync(ct);
			return new TicketActionResult(Ok: false, code, error);
		}
	}

	public async Task<bool> NotifyWorkshopDeliveryReady(Guid id, WorkshopActor actor, CancellationToken ct)
	{
		_ = 16;
		try
		{
			bool result;
			await using (SqlConnection connection = await Open(ct))
			{
				await EnsureWorkshopSchema(connection, ct);
				await EnsureWorkshopDeliveryNotificationSchema(connection, ct);
				string target = null;
				string returnName = null;
				string equipment = null;
				string serial = null;
				string document = null;
				string movementDocument = null;
				Guid? movementId = null;
				DateTime? deliveredAt = null;
				bool flag2;
				await using (SqlCommand read = new SqlCommand("SELECT TOP(1) NULLIF(c.sender_user_id,N''),c.return_name,m.equipment_type,COALESCE(c.replacement_serial,m.serial_number),m.document_number,e.movement_id,child.document_number,e.occurred_at\r\nFROM dbo.maintenance_movements m\r\nJOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id\r\nJOIN dbo.maintenance_workshop_events e ON e.source_id=m.id AND e.action='DELIVER'\r\nLEFT JOIN dbo.maintenance_movements child ON child.id=e.movement_id\r\nWHERE m.id=@id\r\nORDER BY e.occurred_at DESC,e.id DESC;", connection))
				{
					read.Parameters.AddWithValue("@id", id);
					bool flag;
					await using (SqlDataReader r = await read.ExecuteReaderAsync(ct))
					{
						if (!(await r.ReadAsync(ct)))
						{
							flag = false;
							goto IL_0589;
						}
						target = (r.IsDBNull(0) ? null : r.GetString(0));
						returnName = (r.IsDBNull(1) ? null : r.GetString(1));
						equipment = (r.IsDBNull(2) ? "Equipo" : r.GetString(2));
						serial = (r.IsDBNull(3) ? "Sin serial" : r.GetString(3));
						document = (r.IsDBNull(4) ? "Formulario Taller" : r.GetString(4));
						movementId = (r.IsDBNull(5) ? ((Guid?)null) : new Guid?(r.GetGuid(5)));
						movementDocument = (r.IsDBNull(6) ? document : r.GetString(6));
						deliveredAt = (r.IsDBNull(7) ? DateTime.UtcNow : r.GetDateTime(7));
					}
					goto end_IL_026c;
					IL_0589:
					flag2 = flag;
					goto IL_0660;
					end_IL_026c:;
				}
				if (string.IsNullOrWhiteSpace(target) || !movementId.HasValue)
				{
					result = false;
				}
				else
				{
					Guid conversationId = Guid.NewGuid();
					Guid? existing = null;
					string category = "DIRECT:" + actor.Id;
					await using (SqlCommand read = new SqlCommand("SELECT TOP(1) id FROM dbo.support_conversations WHERE supervisor_user_id=@target AND category=@category AND deleted_at IS NULL ORDER BY last_message_at DESC", connection))
					{
						read.Parameters.AddWithValue("@target", target);
						read.Parameters.AddWithValue("@category", category);
						if (await read.ExecuteScalarAsync(ct) is Guid value)
						{
							existing = value;
						}
					}
					if (!existing.HasValue)
					{
						await using SqlCommand read = new SqlCommand("INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,status,requested_agent) VALUES(@id,@target,@returnName,@target,N'WORKSHOP',@category,'IN_PROGRESS',0);", connection);
						read.Parameters.AddWithValue("@id", conversationId);
						read.Parameters.AddWithValue("@target", target);
						read.Parameters.AddWithValue("@returnName", Db(returnName ?? target));
						read.Parameters.AddWithValue("@category", category);
						await read.ExecuteNonQueryAsync(ct);
					}
					else
					{
						conversationId = existing.Value;
					}
					string pdfUrl = $"/api/maintenance/movements/{movementId}/document";
					string message = $"El equipo reparado fue enviado de Taller a Almacén. Equipo: {equipment}. Serial: {serial}. Comprobante PDF + QR: {movementDocument}. Abre el comprobante aquí: {pdfUrl}";
					await using (SqlCommand read = new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_user_id,sender_name,sender_role,message,is_bot)\r\nVALUES(@conversation,@sender,@senderName,@role,@message,0);\r\nUPDATE dbo.support_conversations SET status='IN_PROGRESS',updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@conversation;", connection))
					{
						read.Parameters.AddWithValue("@conversation", conversationId);
						read.Parameters.AddWithValue("@sender", actor.Id);
						read.Parameters.AddWithValue("@senderName", "Taller - " + actor.Name);
						read.Parameters.AddWithValue("@role", actor.Role);
						read.Parameters.AddWithValue("@message", (message.Length > 2000) ? message.Substring(0, 2000) : message);
						await read.ExecuteNonQueryAsync(ct);
					}
					string title = "Equipo reparado enviado a Almacén - " + movementDocument;
					string originalFileName = "comprobante-taller-" + SafeFileName(movementDocument ?? document ?? "taller") + ".pdf";
					string body = string.Join("\n", "Remitente: " + (returnName ?? target), "Equipo: " + equipment, "Serial: " + serial, "Formulario: " + movementDocument, $"Fecha: {deliveredAt:yyyy-MM-dd HH:mm} UTC", "Enlace del comprobante: " + pdfUrl);
					byte[] array = WorkshopReceiptPdf(title, body);
					string value2 = JsonSerializer.Serialize(new
					{
						area = "communications",
						channel = "requests",
						title = title,
						description = message,
						originalFileName = originalFileName,
						contentType = "application/pdf",
						size = array.Length,
						ownerUserName = actor.Id,
						ownerDisplayName = "Taller - " + actor.Name,
						ownerLogin = actor.Login,
						department = "Taller",
						recipientUserName = target,
						recipientDepartment = (string)null,
						status = "received",
						trashed = false,
						createdUtc = DateTime.UtcNow,
						sentUtc = DateTime.UtcNow,
						receivedUtc = DateTime.UtcNow,
						receivedByUserName = target,
						receiverDisplayName = (returnName ?? target),
						receiverLogin = (string)null,
						receiptCode = $"REAL-TAL-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}",
						verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
						sealX = 65,
						sealY = 72,
						workshopMovementId = movementId.Value.ToString(),
						sharedWith = Array.Empty<object>(),
						history = new[]
						{
							new
							{
								action = "workshop-delivery",
								actor = actor.Name,
								login = actor.Login,
								at = DateTime.UtcNow,
								previousStatus = "DELIVERED",
								receiptCode = (string)null,
								receiverLogin = (string)null,
								receivedUtc = (DateTime?)null,
								previousReceiptSignature = (string)null
							}
						}
					}, new JsonSerializerOptions(JsonSerializerDefaults.Web));
					await using (SqlCommand read = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.institutional_files WHERE area='communications' AND channel='requests' AND JSON_VALUE(metadata,'$.workshopMovementId')=@movement) INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES(@owner,N'Taller','communications','requests',@json,@bytes);", connection))
					{
						read.Parameters.AddWithValue("@owner", actor.Id);
						read.Parameters.AddWithValue("@json", value2);
						SqlParameterCollection parameters = read.Parameters;
						parameters.AddWithValue("@bytes", await StorageFiles.Store(array, "generated-document", ct));
						read.Parameters.AddWithValue("@movement", movementId.Value.ToString());
						await read.ExecuteNonQueryAsync(ct);
					}
					result = true;
				}
				goto end_IL_011f;
				IL_0660:
				result = flag2;
				end_IL_011f:;
			}
			return result;
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			Console.Error.WriteLine("Workshop delivery notification: " + ex.Message);
			return false;
		}
	}

	public async Task<bool> NotifyMaintenanceReceiptCreated(string documentNumber, WorkshopActor actor, CancellationToken ct)
	{
		_ = 15;
		try
		{
			bool result;
			await using (SqlConnection connection = await Open(ct))
			{
				await EnsureWorkshopDeliveryNotificationSchema(connection, ct);
				Guid movementId;
				string target;
				string recipientName;
				string equipment;
				string serial;
				string movement;
				string department;
				string qr;
				DateTime createdAt;
				bool flag2;
				await using (SqlCommand read = new SqlCommand("SELECT TOP(1) m.id,m.technician_user_id,COALESCE(NULLIF(u.display_name,N''),NULLIF(m.received_by_name,N''),m.technician_user_id),m.equipment_type,m.serial_number,m.movement_type,m.department,m.qr_token,m.created_at\r\nFROM dbo.maintenance_movements m LEFT JOIN dbo.admin_users u ON CONVERT(nvarchar(80),u.id)=m.technician_user_id\r\nWHERE m.document_number=@document AND NULLIF(m.technician_user_id,N'') IS NOT NULL ORDER BY m.created_at DESC;", connection))
				{
					read.Parameters.AddWithValue("@document", documentNumber.Trim().ToUpperInvariant());
					bool flag;
					await using (SqlDataReader r = await read.ExecuteReaderAsync(ct))
					{
						if (!(await r.ReadAsync(ct)))
						{
							flag = false;
							goto IL_043d;
						}
						movementId = r.GetGuid(0);
						target = r.GetString(1);
						recipientName = r.GetString(2);
						equipment = r.GetString(3);
						serial = r.GetString(4);
						movement = r.GetString(5);
						department = r.GetString(6);
						qr = r.GetString(7);
						createdAt = r.GetDateTime(8);
					}
					goto end_IL_01b9;
					IL_043d:
					flag2 = flag;
					goto IL_0514;
					end_IL_01b9:;
				}
				string category = "DIRECT:" + actor.Id;
				object existing = null;
				await using (SqlCommand read = new SqlCommand("SELECT TOP(1) id FROM dbo.support_conversations WHERE supervisor_user_id=@target AND category=@category AND deleted_at IS NULL ORDER BY last_message_at DESC", connection))
				{
					read.Parameters.AddWithValue("@target", target);
					read.Parameters.AddWithValue("@category", category);
					existing = await read.ExecuteScalarAsync(ct);
				}
				Guid conversationId;
				if (existing is Guid guid)
				{
					conversationId = guid;
				}
				else
				{
					conversationId = Guid.NewGuid();
					await using SqlCommand read = new SqlCommand("INSERT INTO dbo.support_conversations(id,supervisor_user_id,supervisor_name,supervisor_username,assigned_department,category,status,requested_agent) VALUES(@id,@target,@name,@target,@department,@category,'IN_PROGRESS',0);", connection);
					read.Parameters.AddWithValue("@id", conversationId);
					read.Parameters.AddWithValue("@target", target);
					read.Parameters.AddWithValue("@name", recipientName);
					read.Parameters.AddWithValue("@department", department);
					read.Parameters.AddWithValue("@category", category);
					await read.ExecuteNonQueryAsync(ct);
				}
				string pdfUrl = $"/api/maintenance/movements/{movementId}/document";
				string trackingUrl = "/?equipment=" + Uri.EscapeDataString(qr);
				string message = $"Se registró un comprobante para ti. Formulario: {documentNumber}. Equipo: {equipment}. Serial: {serial}. Consulta el PDF: {pdfUrl}. Seguimiento QR: {trackingUrl}";
				await using (SqlCommand read = new SqlCommand("INSERT INTO dbo.support_messages(conversation_id,sender_user_id,sender_name,sender_role,message,is_bot) VALUES(@conversation,@sender,@name,@role,@message,0); UPDATE dbo.support_conversations SET status='IN_PROGRESS',updated_at=SYSUTCDATETIME(),last_message_at=SYSUTCDATETIME() WHERE id=@conversation;", connection))
				{
					read.Parameters.AddWithValue("@conversation", conversationId);
					read.Parameters.AddWithValue("@sender", actor.Id);
					read.Parameters.AddWithValue("@name", actor.Name);
					read.Parameters.AddWithValue("@role", actor.Role);
					read.Parameters.AddWithValue("@message", message);
					await read.ExecuteNonQueryAsync(ct);
				}
				string title = "Comprobante de entrega - " + documentNumber;
				string originalFileName = "comprobante-" + SafeFileName(documentNumber) + ".pdf";
				byte[] array = WorkshopReceiptPdf(title, string.Join("\n", "Recibe: " + recipientName, "Entrega: " + actor.Name, "Movimiento: " + movement, "Departamento: " + department, "Equipo: " + equipment, "Serial: " + serial, $"Fecha: {createdAt:yyyy-MM-dd HH:mm} UTC", "Formulario PDF: " + pdfUrl, "Seguimiento QR: " + trackingUrl));
				string value = JsonSerializer.Serialize(new
				{
					area = "communications",
					channel = "requests",
					title = title,
					description = message,
					originalFileName = originalFileName,
					contentType = "application/pdf",
					size = array.Length,
					ownerUserName = actor.Id,
					ownerDisplayName = actor.Name,
					ownerLogin = actor.Login,
					department = department,
					recipientUserName = target,
					recipientDepartment = department,
					status = "received",
					trashed = false,
					createdUtc = DateTime.UtcNow,
					sentUtc = DateTime.UtcNow,
					receivedUtc = DateTime.UtcNow,
					receivedByUserName = target,
					receiverDisplayName = recipientName,
					receiverLogin = (string)null,
					receiptCode = qr,
					verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
					sealX = 65,
					sealY = 72,
					maintenanceMovementId = movementId.ToString(),
					sharedWith = Array.Empty<object>(),
					history = new[]
					{
						new
						{
							action = "maintenance-receipt-created",
							actor = actor.Name,
							login = actor.Login,
							at = DateTime.UtcNow
						}
					}
				}, new JsonSerializerOptions(JsonSerializerDefaults.Web));
				await using (SqlCommand read = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.institutional_files WHERE JSON_VALUE(metadata,'$.maintenanceMovementId')=@movement) INSERT INTO dbo.institutional_files(owner_id,department,area,channel,metadata,file_data) VALUES(@owner,@department,'communications','requests',@json,@bytes);", connection))
				{
					read.Parameters.AddWithValue("@movement", movementId.ToString());
					read.Parameters.AddWithValue("@owner", actor.Id);
					read.Parameters.AddWithValue("@department", department);
					read.Parameters.AddWithValue("@json", value);
					SqlParameterCollection parameters = read.Parameters;
					parameters.AddWithValue("@bytes", await StorageFiles.Store(array, "generated-document", ct));
					await read.ExecuteNonQueryAsync(ct);
				}
				result = true;
				goto end_IL_0117;
				IL_0514:
				result = flag2;
				end_IL_0117:;
			}
			return result;
		}
		catch (Exception ex) when (!ct.IsCancellationRequested)
		{
			Console.Error.WriteLine("Maintenance receipt notification: " + ex.Message);
			return false;
		}
	}

	private static async Task EnsureWorkshopDeliveryNotificationSchema(SqlConnection connection, CancellationToken ct)
	{
		await using SqlCommand cmd = new SqlCommand("IF OBJECT_ID(N'dbo.support_conversations',N'U') IS NULL\r\nCREATE TABLE dbo.support_conversations(id uniqueidentifier NOT NULL PRIMARY KEY,supervisor_user_id nvarchar(80) NOT NULL,supervisor_name nvarchar(160) NOT NULL,supervisor_username nvarchar(180) NOT NULL,assigned_department varchar(40) NOT NULL,category varchar(60) NULL,status varchar(25) NOT NULL DEFAULT('WAITING_SUPPORT'),requested_agent bit NOT NULL DEFAULT(0),created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),updated_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),last_message_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),deleted_at datetime2(3) NULL,is_restricted bit NOT NULL DEFAULT(0),is_blocked bit NOT NULL DEFAULT(0));\r\nIF COL_LENGTH(N'dbo.support_conversations',N'deleted_at') IS NULL ALTER TABLE dbo.support_conversations ADD deleted_at datetime2(3) NULL;\r\nIF COL_LENGTH(N'dbo.support_conversations',N'is_restricted') IS NULL ALTER TABLE dbo.support_conversations ADD is_restricted bit NOT NULL DEFAULT(0);\r\nIF COL_LENGTH(N'dbo.support_conversations',N'is_blocked') IS NULL ALTER TABLE dbo.support_conversations ADD is_blocked bit NOT NULL DEFAULT(0);\r\nIF OBJECT_ID(N'dbo.support_messages',N'U') IS NULL\r\nCREATE TABLE dbo.support_messages(id uniqueidentifier NOT NULL DEFAULT(NEWID()) PRIMARY KEY,conversation_id uniqueidentifier NOT NULL,sender_user_id nvarchar(80) NULL,sender_name nvarchar(160) NOT NULL,sender_role varchar(40) NOT NULL,message nvarchar(2000) NOT NULL,is_bot bit NOT NULL DEFAULT(0),created_at datetime2(3) NOT NULL DEFAULT(SYSUTCDATETIME()),deleted_at datetime2(3) NULL);\r\nIF COL_LENGTH(N'dbo.support_messages',N'deleted_at') IS NULL ALTER TABLE dbo.support_messages ADD deleted_at datetime2(3) NULL;\r\nIF OBJECT_ID(N'dbo.institutional_files',N'U') IS NULL\r\nCREATE TABLE dbo.institutional_files(id bigint IDENTITY PRIMARY KEY,owner_id nvarchar(80) NOT NULL,department nvarchar(80) NOT NULL,area varchar(20) NOT NULL,channel varchar(20) NOT NULL,metadata nvarchar(max) NOT NULL CHECK(ISJSON(metadata)=1),file_data varbinary(max) NOT NULL);", connection);
		await cmd.ExecuteNonQueryAsync(ct);
	}

	private static string SafeFileName(string value)
	{
		return Regex.Replace(value, "[^A-Za-z0-9._-]+", "-").Trim('-');
	}

	private static byte[] WorkshopReceiptPdf(string title, string body)
	{
		string[] array = (title + "\n\n" + body).Split('\n').Take(24).Select(PdfText)
			.ToArray();
		StringBuilder stringBuilder = new StringBuilder("BT /F1 13 Tf 50 780 Td ");
		string[] array2 = array;
		foreach (string value in array2)
		{
			stringBuilder.Append('(').Append(value).Append(") Tj 0 -20 Td ");
		}
		stringBuilder.Append("ET");
		byte[] bytes = Encoding.ASCII.GetBytes(stringBuilder.ToString());
		List<string> list = new List<string>
		{
			"<< /Type /Catalog /Pages 2 0 R >>",
			"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
			"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
			$"<< /Length {bytes.Length} >>\nstream\n{stringBuilder}\nendstream",
			"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
		};
		using (MemoryStream memoryStream = new MemoryStream())
		{
			using StreamWriter streamWriter = new StreamWriter(memoryStream, Encoding.ASCII, 1024, leaveOpen: true)
			{
				NewLine = "\n"
			};
			streamWriter.Write("%PDF-1.4\n");
			List<long> list2 = new List<long> { 0L };
			for (int j = 0; j < list.Count; j++)
			{
				list2.Add(memoryStream.Position);
				streamWriter.Write($"{j + 1} 0 obj\n{list[j]}\nendobj\n");
				streamWriter.Flush();
			}
			long position = memoryStream.Position;
			streamWriter.Write($"xref\n0 {list.Count + 1}\n0000000000 65535 f \n");
			foreach (long item in list2.Skip(1))
			{
				streamWriter.Write($"{item:0000000000} 00000 n \n");
			}
			streamWriter.Write($"trailer << /Size {list.Count + 1} /Root 1 0 R >>\nstartxref\n{position}\n%%EOF\n");
			streamWriter.Flush();
			return memoryStream.ToArray();
		}
		static string PdfText(string text)
		{
			return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")
				.Replace("\r", "");
		}
	}
}
