using System;

internal sealed class StorageServiceException(string code, string message, int status = 503) : Exception(message)
{
	public string Code { get; } = code;

	public int Status { get; } = status;

	public bool Retryable { get; set; }

	public TimeSpan? RetryAfter { get; set; }
}
