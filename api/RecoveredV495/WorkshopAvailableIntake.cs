using System;

internal record WorkshopAvailableIntake(Guid BatchId, string Equipment, string[] Serials, string? Notes = null);
