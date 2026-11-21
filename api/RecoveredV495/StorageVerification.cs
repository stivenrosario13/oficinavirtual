using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal static class StorageVerification
{
	public static async Task<StorageVerificationReport> Run(string publicRoot, CancellationToken ct, Action<string>? progress = null, Func<int, CancellationToken, Task>? beforeUpload = null, Action<StorageServiceException>? onStorageError = null)
	{
		List<StorageCheck> checks = new List<StorageCheck>();
		await Check("Conexión y credencial", async () =>
		{
			if (!StorageFiles.Enabled)
			{
				throw new StorageServiceException("STORAGE_DISABLED", "El almacenamiento externo está desactivado.");
			}
			await StorageFiles.Required().WhoAmI(ct);
		});
		(string Name, string Path)[] array = new (string, string)[2]
		{
			("Imagen: subida y descarga", "loto-real-logo-transparent.png"),
			("PDF: subida y descarga", "documents/plantilla-conduce-institucional.pdf")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(string Name, string Path) sample = array[num];
			if (!checks[0].Passed)
			{
				checks.Add(new StorageCheck(sample.Name, Passed: false, "Pendiente: primero debe funcionar la conexión.", "STORAGE_NOT_TESTED"));
				continue;
			}
			await Check(sample.Name, async () =>
			{
				byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(publicRoot, sample.Path), ct);
				if (beforeUpload != null)
				{
					await beforeUpload(bytes.Length, ct);
				}
				byte[] bytes2 = await StorageFiles.Store(bytes, "integration-verification", ct);
				if (!StorageFiles.IsReference(bytes2))
				{
					throw new StorageServiceException("STORAGE_REFERENCE", "El archivo no produjo una referencia externa.");
				}
				await StorageFiles.Read(bytes2, ct);
			});
		}
		return new StorageVerificationReport(DateTime.UtcNow, checks.ToArray());
		async Task Check(string name, Func<Task> action)
		{
			progress?.Invoke(name);
			try
			{
				await action();
				checks.Add(new StorageCheck(name, Passed: true, "Correcto"));
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				throw;
			}
			catch (StorageServiceException ex2)
			{
				onStorageError?.Invoke(ex2);
				checks.Add(new StorageCheck(name, Passed: false, ex2.Message, ex2.Code));
			}
			catch (Exception)
			{
				checks.Add(new StorageCheck(name, Passed: false, "No se completó la comprobación desde este servidor.", "STORAGE_CHECK_FAILED"));
			}
		}
	}
}
