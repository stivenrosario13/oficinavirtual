sealed class StorageVerificationJob(IWebHostEnvironment environment,StorageUploadQueue queue) : BackgroundService
{
    readonly SemaphoreSlim signal = new(0, 1);
    readonly object sync = new();
    string state = "unknown", message = "Todavía no se ha probado desde este servidor.";
    StorageVerificationReport? report;
    public object Status() { lock (sync) return new { state, message, report }; }
    public bool Start()
    {
        lock (sync)
        {
            if (state is "queued" or "running") return false;
            state = "queued"; message = "Prueba solicitada"; report = null;
            signal.Release(); return true;
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await signal.WaitAsync(stoppingToken); } catch (OperationCanceledException) { break; }
            lock (sync) { state = "running"; message = "Comprobando almacenamiento"; }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            try
            {
                lock(sync)message="Esperando la transferencia activa para comprobar imagen y PDF";
                using var lease=await queue.BeginVerification(timeout.Token);
                var result = await StorageVerification.Run(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), timeout.Token, text => { lock (sync) message = text; },queue.ReserveVerificationUpload,queue.RecordVerificationError);
                queue.CompleteVerification(result.Passed);
                lock (sync) { report = result; state = result.Passed ? "passed" : "failed"; message = result.Passed ? "Subida y descarga de imagen y PDF verificadas desde este servidor." : "El almacenamiento todavía no está operativo. Consulta las comprobaciones."; }
            }
            catch (OperationCanceledException) { lock (sync) { state = "failed"; message = "Prueba interrumpida o sin respuesta dentro de cinco minutos. Vuelve a intentarlo."; } }
            catch (Exception) { lock (sync) { state = "failed"; message = "No se pudo completar la prueba de almacenamiento."; } }
        }
    }
}
