using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal sealed class StorageDocumentUsage(Database database, StorageUploadQueue queue)
{
	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private List<StorageDocumentRow>? rows;

	private DateTime? checkedAt;

	private DateTime nextRead;

	private string? error;

	public async Task<object> Read(CancellationToken ct)
	{
		await gate.WaitAsync(ct);
		try
		{
			if (DateTime.UtcNow >= nextRead)
			{
				nextRead = DateTime.UtcNow.AddSeconds(30.0);
				try
				{
					rows = await database.StorageDocuments(queue, ct);
					checkedAt = DateTime.UtcNow;
					error = null;
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception)
				{
					error = "No se pudo actualizar el detalle de solicitudes y formularios. Se conserva la última lectura disponible.";
				}
			}
			return new { rows, checkedAt, error };
		}
		finally
		{
			gate.Release();
		}
	}
}
