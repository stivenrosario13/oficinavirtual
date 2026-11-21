using System;

internal record StorageQueueStatus(int Pending, int Complete, int Review, long StagedBytes, string State, string Message, DateTime? NextAttempt, long DailyAttemptedBytes, long DailyLimitBytes, int UploadsPerMinute, int MaxPdfPages, object[] Issues)
{
	public DateTime? VerificationBlockedUntil { get; init; }
}
