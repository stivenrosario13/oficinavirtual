record StorageCheck(string Name, bool Passed, string Message, string? Code = null);
record StorageVerificationReport(DateTime CheckedAt, IReadOnlyList<StorageCheck> Checks)
{
    public bool Passed => Checks.Count == 3 && Checks.All(x => x.Passed);
}

static class StorageVerification
{
    // Exercise the same upload/reference/download path used by application files.
    // Fixed sample content and kind keep repeat checks idempotent.
    public static async Task<StorageVerificationReport> Run(string publicRoot, CancellationToken ct, Action<string>? progress = null,Func<int,CancellationToken,Task>? beforeUpload=null,Action<StorageServiceException>? onStorageError=null)
    {
        var checks = new List<StorageCheck>();
        async Task Check(string name, Func<Task> action)
        {
            progress?.Invoke(name);
            try { await action(); checks.Add(new(name, true, "Correcto")); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (StorageServiceException e) { onStorageError?.Invoke(e);checks.Add(new(name, false, e.Message, e.Code)); }
            catch (Exception) { checks.Add(new(name, false, "No se completó la comprobación desde este servidor.", "STORAGE_CHECK_FAILED")); }
        }
        await Check("Conexión y credencial", async () => {
            if (!StorageFiles.Enabled) throw new StorageServiceException("STORAGE_DISABLED", "El almacenamiento externo está desactivado.");
            await StorageFiles.Required().WhoAmI(ct);
        });
        foreach (var sample in new[] { (Name: "Imagen: subida y descarga", Path: "loto-real-logo-transparent.png"), (Name: "PDF: subida y descarga", Path: "documents/plantilla-conduce-institucional.pdf") })
        {
            if (!checks[0].Passed) { checks.Add(new(sample.Name, false, "Pendiente: primero debe funcionar la conexión.", "STORAGE_NOT_TESTED")); continue; }
            await Check(sample.Name, async () => {
                var bytes = await File.ReadAllBytesAsync(System.IO.Path.Combine(publicRoot, sample.Path), ct);
                if(beforeUpload is not null)await beforeUpload(bytes.Length,ct);
                var stored = await StorageFiles.Store(bytes, "integration-verification", ct);
                if (!StorageFiles.IsReference(stored)) throw new StorageServiceException("STORAGE_REFERENCE", "El archivo no produjo una referencia externa.");
                await StorageFiles.Read(stored, ct);
            });
        }
        return new(DateTime.UtcNow, checks.ToArray());
    }
}
