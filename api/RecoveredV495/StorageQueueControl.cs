using System;
using System.Text.Json.Nodes;

internal record StorageQueueControl
{
	public DateTime NextAttempt { get; set; }

	public DateTime NextUpload { get; set; }

	public DateTime ProviderRetryUntil { get; set; }

	public DateTime UsageCheckedAt { get; set; }

	public DateTime UtcDay { get; set; }

	public long AttemptedBytes { get; set; }

	public bool Blocked { get; set; }

	public string? Message { get; set; }

	public int MaxPending { get; set; } = 100;

	public int MaxImageBytes { get; set; } = 26214400;

	public int MaxPdfBytes { get; set; } = 52428800;

	public int MaxPdfPages { get; set; } = 200;

	public JsonObject? ProviderUsage { get; set; }
}
