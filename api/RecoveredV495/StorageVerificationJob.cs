using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

internal sealed class StorageVerificationJob(IWebHostEnvironment environment, StorageUploadQueue queue) : BackgroundService
{
	private readonly SemaphoreSlim signal = new SemaphoreSlim(0, 1);

	private readonly object sync = new object();

	private string state = "unknown";

	private string message = "Todavía no se ha probado desde este servidor.";

	private StorageVerificationReport? report;

	public object Status()
	{
		lock (sync)
		{
			return new { state, message, report };
		}
	}

	public bool Start()
	{
		bool flag;
		lock (sync)
		{
			string text = state;
			flag = ((text == "queued" || text == "running") ? true : false);
			if (flag)
			{
				flag = false;
			}
			else
			{
				state = "queued";
				message = "Prueba solicitada";
				report = null;
				signal.Release();
				flag = true;
			}
		}
		return flag;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Yield();
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await signal.WaitAsync(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			lock (sync)
			{
				state = "running";
				message = "Comprobando almacenamiento";
			}
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
			timeout.CancelAfter(TimeSpan.FromMinutes(5.0));
			try
			{
				lock (sync)
				{
					message = "Esperando la transferencia activa para comprobar imagen y PDF";
				}
				using (await queue.BeginVerification(timeout.Token))
				{
					StorageVerificationReport storageVerificationReport = await StorageVerification.Run(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), timeout.Token, (string text) =>
					{
						lock (sync)
						{
							message = text;
						}
					}, queue.ReserveVerificationUpload, queue.RecordVerificationError);
					queue.CompleteVerification(storageVerificationReport.Passed);
					lock (sync)
					{
						report = storageVerificationReport;
						state = (storageVerificationReport.Passed ? "passed" : "failed");
						message = (storageVerificationReport.Passed ? "Subida y descarga de imagen y PDF verificadas desde este servidor." : "El almacenamiento todavía no está operativo. Consulta las comprobaciones.");
					}
				}
			}
			catch (OperationCanceledException)
			{
				lock (sync)
				{
					state = "failed";
					message = "Prueba interrumpida o sin respuesta dentro de cinco minutos. Vuelve a intentarlo.";
				}
			}
			catch (Exception)
			{
				lock (sync)
				{
					state = "failed";
					message = "No se pudo completar la prueba de almacenamiento.";
				}
			}
		}
	}
}
