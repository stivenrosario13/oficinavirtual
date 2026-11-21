using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

internal sealed class StorageUsageMonitor
{
	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private readonly Func<DateTime> now;

	private JsonObject? provider;

	private DateTime? checkedAt;

	private DateTime nextCheck;

	private string? error;

	public StorageUsageMonitor()
		: this(() => DateTime.UtcNow)
	{
	}

	internal StorageUsageMonitor(Func<DateTime> clock)
	{
		now = clock;
	}

	public async Task<StorageUsageSnapshot> Read(CancellationToken ct)
	{
		await gate.WaitAsync(ct);
		try
		{
			if (now() >= nextCheck)
			{
				nextCheck = now().AddSeconds(30.0);
				using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
				timeout.CancelAfter(TimeSpan.FromSeconds(15.0));
				try
				{
					provider = await StorageFiles.Required().Usage(timeout.Token);
					checkedAt = now();
					error = null;
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					throw;
				}
				catch (StorageServiceException ex2)
				{
					error = ex2.Message;
					TimeSpan? retryAfter = ex2.RetryAfter;
					if (retryAfter.HasValue)
					{
						TimeSpan valueOrDefault = retryAfter.GetValueOrDefault();
						if (now().Add(valueOrDefault) > nextCheck)
						{
							nextCheck = now().Add(valueOrDefault);
						}
					}
				}
				catch (Exception)
				{
					error = "No se pudo consultar el uso de la API. Se conserva la última lectura disponible.";
				}
			}
			return new StorageUsageSnapshot(provider?.DeepClone().AsObject(), checkedAt, error, nextCheck);
		}
		finally
		{
			gate.Release();
		}
	}
}
