using System.Text.Json.Nodes;

record StorageUsageSnapshot(JsonObject? Provider, DateTime? CheckedAt, string? Error, DateTime? NextCheckAt);

// Independent from upload backoff: reading usage never uploads or resumes files.
// Share recent results and provider Retry-After across simultaneous panel requests.
sealed class StorageUsageMonitor
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Func<DateTime> now;
    JsonObject? provider;
    DateTime? checkedAt;
    DateTime nextCheck;
    string? error;
    public StorageUsageMonitor() : this(() => DateTime.UtcNow) { }
    internal StorageUsageMonitor(Func<DateTime> clock) => now = clock;
    public async Task<StorageUsageSnapshot> Read(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (now() >= nextCheck)
            {
                nextCheck = now().AddSeconds(30);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                try
                {
                    provider = await StorageFiles.Required().Usage(timeout.Token);
                    checkedAt = now(); error = null;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (StorageServiceException e)
                {
                    error = e.Message;
                    if (e.RetryAfter is TimeSpan delay && now().Add(delay) > nextCheck) nextCheck = now().Add(delay);
                }
                catch (Exception) { error = "No se pudo consultar el uso de la API. Se conserva la última lectura disponible."; }
            }
            return new(provider?.DeepClone().AsObject(), checkedAt, error, nextCheck);
        }
        finally { gate.Release(); }
    }
}
