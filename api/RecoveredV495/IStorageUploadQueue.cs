using System.Threading;
using System.Threading.Tasks;

internal interface IStorageUploadQueue
{
	Task<StorageReference> Enqueue(byte[] bytes, string mime, string kind, CancellationToken ct);

	Task<byte[]> Read(StorageReference reference, CancellationToken ct);

	Task<StorageReference?> Resolve(StorageReference reference, CancellationToken ct);
}
