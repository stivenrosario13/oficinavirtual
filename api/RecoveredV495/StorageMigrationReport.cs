using System.Collections.Generic;

internal record StorageMigrationReport(int Candidates, int Copied, int Existing, int Skipped, int Failed, List<object> Details)
{
	public bool OriginalsPreserved => true;

	public string? StopReason { get; init; }

	public int Queued { get; init; }

	public bool NeedsRescan { get; init; }
}
