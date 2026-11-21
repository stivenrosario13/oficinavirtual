using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class StorageUploadWorker(StorageUploadQueue queue, ILogger<StorageUploadWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Yield();
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await queue.ProcessOne(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex2)
			{
				logger.LogError("No se procesó la cola de almacenamiento ({Type}).", ex2.GetType().Name);
				try
				{
					await Task.Delay(TimeSpan.FromMinutes(1.0), stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(2.0), stoppingToken);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}
}
