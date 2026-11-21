using System;

internal record WeekendLoanUse(int AssignedIndex, int Quantity, Guid AgencyId, string? AgencyLabel = null);
