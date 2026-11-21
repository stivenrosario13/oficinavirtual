using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

internal static class StorageMigrationRunner
{
	public record ReleaseStatus(int Released, long BytesFreed, int Pending, int Changed, string? Message = null, DateTime? NextAttempt = null);

	private record Source(string Schema, string Table, string Column, string Type, string? Keys);

	public static async Task<StorageMigrationReport> Run(SqlConnection connection, bool apply, CancellationToken ct, Action<string>? log = null)
	{
		if (!apply)
		{
			return await RunCore(connection, apply: false, ct, log);
		}
		StorageMigrationReport result;
		await using (SqlCommand acquire = new SqlCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'registro-storage-migration-v1',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @result;", connection))
		{
			if (Convert.ToInt32(await acquire.ExecuteScalarAsync(ct)) < 0)
			{
				throw new StorageServiceException("STORAGE_MIGRATION_RUNNING", "Ya hay una copia de archivos en curso en otro proceso.", 409);
			}
			StorageMigrationReport storageMigrationReport;
			try
			{
				storageMigrationReport = await RunCore(connection, apply: true, ct, log);
			}
			finally
			{
				await using SqlCommand release = new SqlCommand("EXEC sys.sp_releaseapplock @Resource=N'registro-storage-migration-v1',@LockOwner='Session';", connection);
				await release.ExecuteNonQueryAsync(CancellationToken.None);
			}
			result = storageMigrationReport;
		}
		return result;
	}

	private static async Task<StorageMigrationReport> RunCore(SqlConnection connection, bool apply, CancellationToken ct, Action<string>? log)
	{
		if (apply)
		{
			if (!StorageFiles.Enabled)
			{
				throw new InvalidOperationException("Activa STORAGE_ENABLED antes de migrar.");
			}
			if (StorageFiles.Queue == null)
			{
				JsonObject jsonObject = await StorageFiles.Required().WhoAmI(ct);
				log?.Invoke("Servicio autenticado: " + jsonObject["tenant"]);
			}
			else
			{
				log?.Invoke("Preparando cola persistente; no se requiere subir durante el inventario.");
			}
			await using SqlCommand schema = new SqlCommand("IF OBJECT_ID(N'dbo.storage_migration_files',N'U') IS NULL\r\n  CREATE TABLE dbo.storage_migration_files(\r\n    migration_key char(64) NOT NULL PRIMARY KEY,\r\n    source_table nvarchar(260) NOT NULL, source_column nvarchar(128) NOT NULL,\r\n    source_key nvarchar(max) NOT NULL, source_sha256 char(64) NOT NULL,\r\n    reference_json nvarchar(max) NOT NULL,\r\n    verified_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());\r\nIF OBJECT_ID(N'dbo.storage_migration_pending',N'U') IS NULL\r\n  CREATE TABLE dbo.storage_migration_pending(\r\n    migration_key char(64) NOT NULL PRIMARY KEY,\r\n    source_table nvarchar(260) NOT NULL, source_column nvarchar(128) NOT NULL,\r\n    source_key nvarchar(max) NOT NULL, source_sha256 char(64) NOT NULL,\r\n    reference_json nvarchar(max) NOT NULL,\r\n    queued_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());", connection);
			await schema.ExecuteNonQueryAsync(ct);
		}
		List<Source> sources = new List<Source>();
		await using (SqlCommand schema = new SqlCommand("SELECT s.name,t.name,c.name,ty.name,\r\n  (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(kc.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal)\r\n   FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id\r\n   JOIN sys.columns kc ON kc.object_id=ic.object_id AND kc.column_id=ic.column_id\r\n   WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.key_ordinal>0) keys\r\nFROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id\r\nJOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id\r\nWHERE t.name<>'storage_migration_files' AND\r\n  ((ty.name IN('varbinary','image') AND c.name IN('file_data','photo_data','avatar_data'))\r\n   OR (ty.name IN('nvarchar','varchar') AND c.name IN('signature_data','metadata','content')))\r\nORDER BY s.name,t.name,c.name;", connection))
		{
			await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				sources.Add(new Source(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
			}
		}
		object obj = null;
		List<object> report = new List<object>();
		int copied = 0;
		int existing = 0;
		int skipped = 0;
		int failed = 0;
		int candidates = 0;
		int queued = 0;
		string type = default;
		foreach (Source source in sources)
		{
			if (source.Keys == null)
			{
				report.Add(new
				{
					source = source.Table,
					column = source.Column,
					error = "Tabla sin clave primaria; revisar manualmente"
				});
				failed++;
				continue;
			}
			string table = Q(source.Schema) + "." + Q(source.Table);
			string column = Q(source.Column);
			if (!apply)
			{
				type = source.Type;
				bool flag = ((type == "varbinary" || type == "image") ? true : false);
				bool binary = flag;
				string value = (binary ? ("CONVERT(varbinary(16),SUBSTRING(" + column + ",1,16))") : ("CASE WHEN CHARINDEX(N'data:'," + column + ")>0 THEN N'data:' ELSE N'' END"));
				SqlCommand schema2 = new SqlCommand($"SELECT {value} FROM {table} WHERE {column} IS NOT NULL AND DATALENGTH({column})>0", connection)
				{
					CommandTimeout = 120
				};
				Exception pendingException = null;
				int num = 0;
				try
				{
					await using (SqlDataReader reader = await schema2.ExecuteReaderAsync(ct))
					{
						int num2 = 0;
						while (await reader.ReadAsync(ct))
						{
							if (binary ? (StorageFiles.DetectMime((byte[])reader[0]) != null) : (reader.GetString(0) == "data:"))
							{
								num2++;
							}
							else
							{
								skipped++;
							}
						}
						candidates += num2;
						report.Add(new
						{
							source = table,
							column = source.Column,
							candidates = num2
						});
						log?.Invoke($"{table}.{source.Column}: {num2} registros con archivos.");
					}
					num = 1;
				}
				catch (Exception obj2)
				{
					pendingException = obj2;
				}
				if (schema2 != null)
				{
					await schema2.DisposeAsync();
				}
				Exception obj3 = pendingException;
				if (obj3 != null)
				{
					if (!(obj3 is Exception source2))
					{
						throw obj3;
					}
					ExceptionDispatchInfo.Capture(source2).Throw();
				}
				if (num == 1)
				{
					continue;
				}
				obj = null;
			}
			string keyExpression = "(SELECT " + source.Keys + " FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)";
			string cursor = "";
			int num3 = 0;
			while (!ct.IsCancellationRequested)
			{
				string rowKey;
				await using (SqlCommand schema = new SqlCommand($"SELECT TOP(1) {keyExpression} row_key,{column} FROM {table} WHERE {column} IS NOT NULL AND DATALENGTH({column})>0 AND {keyExpression} COLLATE Latin1_General_100_BIN2>@cursor COLLATE Latin1_General_100_BIN2 ORDER BY {keyExpression} COLLATE Latin1_General_100_BIN2", connection))
				{
					schema.CommandTimeout = 120;
					schema.Parameters.AddWithValue("@cursor", cursor);
					await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
					if (await reader.ReadAsync(ct))
					{
						rowKey = reader.GetString(0);
						obj = reader.GetValue(1);
						goto IL_0fce;
					}
				}
				break;
				IL_1edc:
				obj = null;
				rowKey = null;
				continue;
				IL_0fce:
				cursor = rowKey;
				byte[] original = ((obj is byte[] array) ? array : Encoding.UTF8.GetBytes((string)obj));
				string text = obj as string;
				if (obj is byte[] array2 && (StorageFiles.IsReference(array2) || StorageFiles.DetectMime(array2) == null))
				{
					skipped++;
					continue;
				}
				if (text != null && !text.Contains("data:", StringComparison.Ordinal))
				{
					skipped++;
					continue;
				}
				num3++;
				candidates++;
				if (!apply)
				{
					continue;
				}
				string hash = StorageFiles.Hash(original);
				string migrationKey = StorageFiles.Hash(Encoding.UTF8.GetBytes(table + "|" + source.Column + "|" + rowKey + "|" + hash));
				await using (SqlCommand schema = new SqlCommand("SELECT COUNT(*) FROM dbo.storage_migration_files WHERE migration_key=@key", connection))
				{
					schema.Parameters.AddWithValue("@key", migrationKey);
					if (Convert.ToInt32(await schema.ExecuteScalarAsync(ct)) > 0)
					{
						existing++;
						continue;
					}
				}
				try
				{
					using (StorageOrigin.Scope("legacy", "unknown"))
					{
						await using (SqlCommand schema = new SqlCommand("SELECT reference_json FROM dbo.storage_migration_pending WHERE migration_key=@key", connection))
						{
							schema.Parameters.AddWithValue("@key", migrationKey);
							string reference;
							if (await schema.ExecuteScalarAsync(ct) is string text2)
							{
								reference = text2;
							}
							else if (text != null)
							{
								reference = ((!text.StartsWith("data:", StringComparison.Ordinal)) ? (await StorageFiles.TransformJson(text, upload: true, ct)) : (await StorageFiles.StoreDataUrl(text, ct)));
							}
							else
							{
								Encoding uTF = Encoding.UTF8;
								reference = uTF.GetString(await StorageFiles.Store(original, "legacy-" + source.Table, ct));
							}
							if (!reference.Contains("REGISTRO-STORAGE-V1:", StringComparison.Ordinal))
							{
								skipped++;
								continue;
							}
							string resolved = await StorageFiles.ResolveQueuedReferences(reference, ct);
							if (resolved == null)
							{
								await using (SqlCommand enqueue = new SqlCommand("INSERT dbo.storage_migration_pending(migration_key,source_table,source_column,source_key,source_sha256,reference_json) SELECT @key,@table,@column,@source,@hash,@reference WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_pending WITH(UPDLOCK,HOLDLOCK) WHERE migration_key=@key)", connection))
								{
									enqueue.Parameters.AddWithValue("@key", migrationKey);
									enqueue.Parameters.AddWithValue("@table", table);
									enqueue.Parameters.AddWithValue("@column", source.Column);
									enqueue.Parameters.AddWithValue("@source", rowKey);
									enqueue.Parameters.AddWithValue("@hash", hash);
									enqueue.Parameters.AddWithValue("@reference", reference);
									await enqueue.ExecuteNonQueryAsync(ct);
									queued++;
									if (queued % 50 == 0)
									{
										log?.Invoke($"En cola: {queued}; verificados: {copied}; existentes: {existing}");
									}
								}
								continue;
							}
							reference = resolved;
							await using SqlCommand journal = new SqlCommand("INSERT dbo.storage_migration_files(migration_key,source_table,source_column,source_key,source_sha256,reference_json) SELECT @key,@table,@column,@source,@hash,@reference WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files WITH(UPDLOCK,HOLDLOCK) WHERE migration_key=@key)", connection);
							journal.Parameters.AddWithValue("@key", migrationKey);
							journal.Parameters.AddWithValue("@table", table);
							journal.Parameters.AddWithValue("@column", source.Column);
							journal.Parameters.AddWithValue("@source", rowKey);
							journal.Parameters.AddWithValue("@hash", hash);
							journal.Parameters.AddWithValue("@reference", reference);
							await journal.ExecuteNonQueryAsync(ct);
							copied++;
							if (copied % 10 == 0)
							{
								log?.Invoke($"Verificados: {copied}; existentes: {existing}; errores: {failed}");
							}
						}
						goto IL_1edc;
					}
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					throw;
				}
				catch (StorageServiceException ex2) when (((Func<bool>)delegate
				{
					// Could not convert BlockContainer to single expression
					type = ex2.Code;
					return (type == "STORAGE_QUEUE_FULL" || type == "STORAGE_QUEUE_BUSY") ? true : false;
				}).Invoke())
				{
					return new StorageMigrationReport(candidates, copied, existing, skipped, failed, report)
					{
						Queued = queued,
						NeedsRescan = true,
						StopReason = ex2.Message
					};
				}
				catch (Exception ex3) when ((ex3 is IOException || ex3 is UnauthorizedAccessException) ? true : false)
				{
					return new StorageMigrationReport(candidates, copied, existing, skipped, failed + 1, report)
					{
						Queued = queued,
						StopReason = "No se pudo conservar otro temporal. Revisa el espacio y los permisos de la carpeta privada; los originales siguen intactos."
					};
				}
				catch (Exception ex4)
				{
					failed++;
					string text3 = ((ex4 is StorageServiceException ex5) ? ex5.Code : ex4.GetType().Name);
					report.Add(new
					{
						source = table,
						column = source.Column,
						key = rowKey,
						error = text3
					});
					log?.Invoke($"Pendiente en {table}.{source.Column}: {text3}");
					if (ex4 is StorageServiceException { Status: 503 } ex6)
					{
						return new StorageMigrationReport(candidates, copied, existing, skipped, failed, report)
						{
							Queued = queued,
							StopReason = ex6.Message
						};
					}
					goto IL_1edc;
				}
			}
			log?.Invoke($"{table}.{source.Column}: {num3} registros con archivos.");
		}
		return new StorageMigrationReport(candidates, copied, existing, skipped, failed, report)
		{
			Queued = queued
		};
	}

	public static async Task<(int Pending, int Verified)> FinalizePending(SqlConnection connection, CancellationToken ct)
	{
		List<(string Key, string Reference)> rows = new List<(string, string)>();
		await using (SqlCommand select = new SqlCommand("SELECT p.migration_key,p.reference_json FROM dbo.storage_migration_pending p WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files f WHERE f.migration_key=p.migration_key)", connection))
		{
			await using SqlDataReader reader = await select.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				rows.Add((reader.GetString(0), reader.GetString(1)));
			}
		}
		foreach (var row in rows)
		{
			string text = await StorageFiles.ResolveQueuedReferences(row.Reference, ct);
			if (text != null)
			{
				await using SqlCommand select = new SqlCommand("INSERT dbo.storage_migration_files(migration_key,source_table,source_column,source_key,source_sha256,reference_json) SELECT migration_key,source_table,source_column,source_key,source_sha256,@reference FROM dbo.storage_migration_pending p WHERE migration_key=@key AND NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files WITH(UPDLOCK,HOLDLOCK) WHERE migration_key=@key)", connection);
				select.Parameters.AddWithValue("@key", row.Key);
				select.Parameters.AddWithValue("@reference", text);
				await select.ExecuteNonQueryAsync(ct);
			}
		}
		(int Pending, int Verified) result;
		await using (SqlCommand count = new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.storage_migration_pending p WHERE NOT EXISTS(SELECT 1 FROM dbo.storage_migration_files f WHERE f.migration_key=p.migration_key)),(SELECT COUNT(*) FROM dbo.storage_migration_files)", connection))
		{
			(int Pending, int Verified) tuple;
			await using (SqlDataReader counts = await count.ExecuteReaderAsync(ct))
			{
				await counts.ReadAsync(ct);
				tuple = (Pending: counts.GetInt32(0), Verified: counts.GetInt32(1));
			}
			result = tuple;
		}
		return result;
	}

	private static string Q(string name)
	{
		return "[" + name.Replace("]", "]]") + "]";
	}

	public static async Task<ReleaseStatus> ReleaseVerified(SqlConnection connection, CancellationToken ct)
	{
		ReleaseStatus result;
		await using (SqlCommand acquire = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'registro-storage-release-v2',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;", connection))
		{
			if (Convert.ToInt32(await acquire.ExecuteScalarAsync(ct)) < 0)
			{
				throw new StorageServiceException("STORAGE_MIGRATION_RUNNING", "La liberación ya está en curso en otro proceso.");
			}
			ReleaseStatus releaseStatus;
			try
			{
				releaseStatus = await ReleaseCore(connection, ct);
			}
			finally
			{
				await using SqlCommand release = new SqlCommand("EXEC sys.sp_releaseapplock @Resource=N'registro-storage-release-v2',@LockOwner='Session';", connection);
				await release.ExecuteNonQueryAsync(CancellationToken.None);
			}
			result = releaseStatus;
		}
		return result;
	}

	private static async Task<ReleaseStatus> ReleaseCore(SqlConnection connection, CancellationToken ct)
	{
		await using (SqlCommand schema = new SqlCommand("IF OBJECT_ID(N'dbo.storage_migration_files',N'U') IS NOT NULL\r\nAND COL_LENGTH('dbo.storage_migration_files','release_state') IS NULL\r\nBEGIN\r\n  ALTER TABLE dbo.storage_migration_files ADD release_state varchar(20) NULL,\r\n    released_at datetime2(3) NULL, released_bytes bigint NULL;\r\nEND", connection))
		{
			await schema.ExecuteNonQueryAsync(ct);
		}
		await using (SqlCommand schema = new SqlCommand("SELECT OBJECT_ID(N'dbo.storage_migration_files',N'U')", connection))
		{
			if ((await schema.ExecuteScalarAsync(ct)) is DBNull)
			{
				return new ReleaseStatus(0, 0L, 0, 0);
			}
		}
		ReleaseStatus result = null;
		List<(string Key, string Table, string Column, string Source, string Hash, string Reference)> rows = new List<(string, string, string, string, string, string)>();
		await using (SqlCommand schema = new SqlCommand("SELECT TOP(10) migration_key,source_table,source_column,source_key,source_sha256,reference_json FROM dbo.storage_migration_files WHERE release_state IS NULL ORDER BY verified_at,migration_key", connection))
		{
			await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
			while (await reader.ReadAsync(ct))
			{
				rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
			}
		}
		object obj = null;
		string message = null;
		DateTime? nextAttempt = null;
		foreach (var row in rows)
		{
			string table;
			string column;
			string type;
			string keys;
			await using (SqlCommand schema = new SqlCommand("SELECT QUOTENAME(s.name)+'.'+QUOTENAME(t.name),QUOTENAME(c.name),ty.name,\r\n  (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(kc.name)),',') WITHIN GROUP(ORDER BY ic.key_ordinal)\r\n   FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id\r\n   JOIN sys.columns kc ON kc.object_id=ic.object_id AND kc.column_id=ic.column_id\r\n   WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND ic.key_ordinal>0)\r\nFROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id\r\nJOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id\r\nWHERE t.object_id=OBJECT_ID(@table) AND c.name=@column\r\nAND ((ty.name IN('varbinary','image') AND c.name IN('file_data','photo_data','avatar_data'))\r\nOR (ty.name IN('nvarchar','varchar') AND c.name IN('signature_data','metadata','content')))", connection))
			{
				schema.Parameters.AddWithValue("@table", row.Table);
				schema.Parameters.AddWithValue("@column", row.Column);
				await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
				if (!(await reader.ReadAsync(ct)) || reader.IsDBNull(3))
				{
					throw new InvalidOperationException("Origen de migración no reconocido");
				}
				table = reader.GetString(0);
				column = reader.GetString(1);
				type = reader.GetString(2);
				keys = reader.GetString(3);
			}
			try
			{
				if (!row.Reference.Contains("REGISTRO-STORAGE-V1:", StringComparison.Ordinal) || row.Reference.Contains("queue:", StringComparison.Ordinal))
				{
					throw new StorageServiceException("STORAGE_VERIFY", "La copia todavía no tiene una referencia remota definitiva.");
				}
				if (!row.Reference.StartsWith("REGISTRO-STORAGE-V1:", StringComparison.Ordinal))
				{
					await StorageFiles.TransformJson(row.Reference, upload: false, ct);
				}
				else
				{
					await StorageFiles.Read(Encoding.UTF8.GetBytes(row.Reference), ct);
				}
			}
			catch (StorageServiceException ex)
			{
				message = ex.Message;
				nextAttempt = DateTime.UtcNow.Add((ex.RetryAfter > TimeSpan.FromMinutes(1.0)) ? ex.RetryAfter.Value : TimeSpan.FromMinutes(1.0));
				break;
			}
			string text = type;
			bool flag = ((text == "varbinary" || text == "image") ? true : false);
			bool binary = flag;
			string expression = "(SELECT " + keys + " FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2=@source COLLATE Latin1_General_100_BIN2";
			await using (SqlTransaction tx = (SqlTransaction)(await connection.BeginTransactionAsync(ct)))
			{
				long oldSize = 0L;
				await using (SqlCommand schema = new SqlCommand($"SELECT {column},CONVERT(bigint,DATALENGTH({column})) FROM {table} WITH(UPDLOCK,HOLDLOCK) WHERE {expression}", connection, tx))
				{
					schema.Parameters.AddWithValue("@source", row.Source);
					await using SqlDataReader reader = await schema.ExecuteReaderAsync(ct);
					if (await reader.ReadAsync(ct))
					{
						obj = reader.GetValue(0);
						oldSize = (reader.IsDBNull(1) ? 0 : reader.GetInt64(1));
					}
					else
					{
						obj = null;
					}
				}
				byte[] bytes;
				if (obj is byte[] array)
				{
					bytes = array;
				}
				else
				{
					bytes = ((obj is string s) ? Encoding.UTF8.GetBytes(s) : Array.Empty<byte>());
				}
				bool matches = StorageFiles.Hash(bytes) == row.Hash;
				bool same = Encoding.UTF8.GetString(bytes) == row.Reference;
				long freed = 0L;
				if (matches)
				{
					await using SqlCommand schema = new SqlCommand($"UPDATE {table} SET {column}=@reference WHERE {expression}", connection, tx);
					schema.Parameters.AddWithValue("@reference", binary ? ((object)Encoding.UTF8.GetBytes(row.Reference)) : ((object)row.Reference));
					schema.Parameters.AddWithValue("@source", row.Source);
					if (await schema.ExecuteNonQueryAsync(ct) != 1)
					{
						throw new InvalidOperationException("Origen no único");
					}
					freed = Math.Max(0L, oldSize - (binary ? Encoding.UTF8.GetByteCount(row.Reference) : ((type == "nvarchar") ? ((long)row.Reference.Length * 2L) : Encoding.UTF8.GetByteCount(row.Reference))));
				}
				await using (SqlCommand schema = new SqlCommand("UPDATE dbo.storage_migration_files SET release_state=@state,released_bytes=@bytes,released_at=SYSUTCDATETIME() WHERE migration_key=@key", connection, tx))
				{
					schema.Parameters.AddWithValue("@state", (matches | same) ? "released" : "changed");
					schema.Parameters.AddWithValue("@bytes", freed);
					schema.Parameters.AddWithValue("@key", row.Key);
					await schema.ExecuteNonQueryAsync(ct);
				}
				await tx.CommitAsync(ct);
			}
			table = null;
			column = null;
			keys = null;
			type = null;
			obj = null;
		}
		await using (SqlCommand totals = new SqlCommand("SELECT COUNT(CASE WHEN release_state='released' THEN 1 END),COALESCE(SUM(released_bytes),0),COUNT(CASE WHEN release_state IS NULL THEN 1 END),COUNT(CASE WHEN release_state='changed' THEN 1 END) FROM dbo.storage_migration_files", connection))
		{
			ReleaseStatus releaseStatus;
			await using (SqlDataReader result2 = await totals.ExecuteReaderAsync(ct))
			{
				await result2.ReadAsync(ct);
				releaseStatus = new ReleaseStatus(result2.GetInt32(0), result2.GetInt64(1), result2.GetInt32(2), result2.GetInt32(3), message, nextAttempt);
			}
			result = releaseStatus;
		}
		return result;
	}
}
