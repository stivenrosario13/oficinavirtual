using System;

internal record StorageQueueItem(string Id, string Mime, string Kind, string Sha256, int Size, DateTime CreatedAt)
{
	public StorageOrigin? Origin { get; set; }

	public string State { get; set; } = "pending";

	public int Attempts { get; set; }

	public DateTime NextAttempt { get; set; }

	public string? RemoteId { get; set; }

	public StorageReference? Remote { get; set; }

	public string? ErrorCode { get; set; }

	public string? Message { get; set; }
}
