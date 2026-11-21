using System;

internal record WarehouseQueueItem(long Id, string Title, string OriginalFileName, string Department, string RecipientDepartment, string OwnerUserName, string OwnerDisplayName, string Status, DateTime CreatedUtc, DateTime? DecisionUtc, string? DecisionBy);
