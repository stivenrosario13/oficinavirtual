using System;

internal record WorkshopIntake(Guid BatchId, string Department, string Equipment, string Failure, string[] Serials, Guid? AgencyId = null, string? Notes = null);
