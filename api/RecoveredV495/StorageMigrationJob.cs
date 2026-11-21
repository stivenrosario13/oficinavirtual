using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

internal sealed class StorageMigrationJob : BackgroundService
{
	private record JobState(string State, string Message, StorageMigrationReport? Report, DateTime? StartedAt, DateTime? FinishedAt, DateTime NextScan, long StagedBytesAtPause = 0L, StorageMigrationRunner.ReleaseStatus? Release = null);

	private readonly Database database;

	private readonly IConfiguration config;

	private readonly StorageUploadQueue queue;

	private readonly string statePath;

	private readonly SemaphoreSlim signal = new SemaphoreSlim(0, 1);

	private readonly object sync = new object();

	private JobState state = new JobState("idle", "Sin iniciar", null, null, null, default, 0L);

	public StorageMigrationJob(Database database, IConfiguration config, StorageUploadQueue queue)
	{
		this.database = database;
		this.config = config;
		this.queue = queue;
		statePath = Path.Combine(queue.Root, "migration.json");
		if (File.Exists(statePath))
		{
			try
			{
				state = StorageUploadQueue.Load<JobState>(statePath);
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
			{
				state = new JobState("idle", "Estado de migración no disponible; el sitio continuará activo", null, null, null, default, 0L);
			}
		}
	}

	private void Persist()
	{
		try
		{
			StorageUploadQueue.Save(statePath, state);
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
			// El estado de una tarea de mantenimiento nunca debe detener el portal.
		}
	}

	public object Status()
	{
		lock (sync)
		{
			return new
			{
				enabled = StorageFiles.Enabled,
				configured = (StorageFiles.Service != null),
				state = state.State,
				message = state.Message,
				startedAt = state.StartedAt,
				finishedAt = state.FinishedAt,
				report = state.Report,
				release = state.Release
			};
		}
	}

	public bool Start()
	{
		bool flag;
		lock (sync)
		{
			string text = state.State;
			flag = ((text == "queued" || text == "running") ? true : false);
			if (flag)
			{
				flag = false;
			}
			else
			{
				if (!StorageFiles.Enabled)
				{
					throw new StorageServiceException("STORAGE_DISABLED", "Activa STORAGE_ENABLED antes de copiar los archivos.");
				}
				state = new JobState("queued", "Preparando cola; originales intactos", null, DateTime.UtcNow, null, default, 0L);
				Persist();
				if (signal.CurrentCount == 0)
				{
					signal.Release();
				}
				flag = true;
			}
		}
		return flag;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Yield();
		if (StorageFiles.Enabled && !string.Equals(config["STORAGE_MIGRATE_EXISTING"], "false", StringComparison.OrdinalIgnoreCase) && state.State == "idle")
		{
			Start();
		}
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await signal.WaitAsync(TimeSpan.FromSeconds(10.0), stoppingToken);
				JobState snapshot;
				lock (sync)
				{
					snapshot = state;
				}
				if (!StorageFiles.Enabled)
				{
					continue;
				}
				bool flag;
				switch (snapshot.State)
				{
				case "queued":
				case "running":
				case "waiting":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if ((!flag && (!(snapshot.State == "complete") || (object)snapshot.Release != null)) || snapshot.NextScan > DateTime.UtcNow)
				{
					continue;
				}
				if (!(snapshot.Report?.NeedsRescan ?? false))
				{
					goto IL_03cc;
				}
				await database.FinalizeStorageCopies(stoppingToken);
				StorageMigrationRunner.ReleaseStatus releaseStatus = await database.ReleaseStorageCopies(stoppingToken);
				lock (sync)
				{
					state = state with
					{
						Release = releaseStatus,
						NextScan = releaseStatus.NextAttempt.GetValueOrDefault()
					};
					Persist();
				}
				if (releaseStatus.Message == null)
				{
					StorageQueueStatus storageQueueStatus = queue.Status();
					if (storageQueueStatus.Pending <= 0 || snapshot.StagedBytesAtPause - storageQueueStatus.StagedBytes >= Math.Min(52428800L, snapshot.StagedBytesAtPause))
					{
						goto IL_03cc;
					}
				}
				continue;
				IL_03cc:
				FileStream fileStream;
				try
				{
					fileStream = new FileStream(Path.Combine(queue.Root, "migration.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				}
				catch (IOException)
				{
					continue;
				}
				using (fileStream)
				{
					string text = snapshot.State;
					flag = ((text == "queued" || text == "running") ? true : false);
					if (flag || (snapshot.Report?.NeedsRescan ?? false))
					{
						lock (sync)
						{
							state = state with
							{
								State = "running",
								Message = "Preparando los archivos en la cola privada"
							};
							Persist();
						}
						StorageMigrationReport storageMigrationReport = await database.CopyLegacyFiles(stoppingToken, (string message) =>
						{
							lock (sync)
							{
								state = state with
								{
									Message = message
								};
							}
						});
						lock (sync)
						{
							state = state with
							{
								Report = storageMigrationReport,
								State = ((storageMigrationReport.Queued > 0 || storageMigrationReport.NeedsRescan) ? "waiting" : ((storageMigrationReport.Failed > 0) ? "partial" : "complete")),
								Message = (storageMigrationReport.NeedsRescan ? "Cola llena: se continuará cuando se libere espacio" : ((storageMigrationReport.Queued > 0) ? "Cola preparada; esperando transferencias verificadas" : ((storageMigrationReport.Failed > 0) ? "Hay archivos que requieren revisión" : "Transferencias verificadas; comprobando liberación de SQL"))),
								NextScan = (storageMigrationReport.NeedsRescan ? DateTime.UtcNow.AddMinutes(1.0) : default(DateTime)),
								StagedBytesAtPause = (storageMigrationReport.NeedsRescan ? queue.Status().StagedBytes : 0),
								FinishedAt = ((storageMigrationReport.Queued == 0 && !storageMigrationReport.NeedsRescan) ? new DateTime?(DateTime.UtcNow) : ((DateTime?)null))
							};
							Persist();
						}
					}
					else
					{
						(int, int) tuple = await database.FinalizeStorageCopies(stoppingToken);
						lock (sync)
						{
							StorageMigrationReport? obj = state.Report ?? new StorageMigrationReport(0, 0, 0, 0, 0, new List<object>());
							StorageMigrationReport storageMigrationReport2 = obj with
							{
								Queued = tuple.Item1,
								Copied = Math.Max(0, tuple.Item2 - (state.Report?.Existing ?? 0))
							};
							state = state with
							{
								Report = storageMigrationReport2,
								State = ((tuple.Item1 > 0) ? "waiting" : ((storageMigrationReport2.Failed > 0) ? "partial" : "complete")),
								Message = ((tuple.Item1 > 0) ? "Transferencia automática: los pendientes se conservan en cola" : "Transferencias verificadas; comprobando liberación de SQL"),
								FinishedAt = ((tuple.Item1 == 0) ? new DateTime?(DateTime.UtcNow) : ((DateTime?)null))
							};
							Persist();
						}
					}
					StorageMigrationRunner.ReleaseStatus releaseStatus2 = await database.ReleaseStorageCopies(stoppingToken);
					lock (sync)
					{
						state = state with
						{
							Release = releaseStatus2
						};
						if (releaseStatus2.Pending > 0)
						{
							state = state with
							{
								State = "waiting",
								NextScan = releaseStatus2.NextAttempt.GetValueOrDefault(),
								Message = (releaseStatus2.Message ?? "Verificando descargas y liberando los originales de SQL")
							};
						}
						else if (state.State == "complete")
						{
							state = state with
							{
								Message = "Traslado finalizado: los archivos verificados se abren desde la API"
							};
						}
						Persist();
					}
				}
				snapshot = null;
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (SqlException)
			{
				lock (sync)
				{
					state = state with
					{
						NextScan = DateTime.UtcNow.AddMinutes(1.0),
						Message = "Conexión SQL pendiente: se reintentará en un minuto"
					};
					Persist();
				}
			}
			catch (Exception ex4)
			{
				lock (sync)
				{
					state = state with
					{
						State = "failed",
						Message = ((ex4 is StorageServiceException ex5) ? ex5.Message : ("No se pudo continuar la cola de migración (" + ex4.GetType().Name + ")")),
						FinishedAt = DateTime.UtcNow
					};
					Persist();
				}
			}
		}
	}
}
