using System;
using System.Collections.Generic;

internal record WeekendLoanSettlement(List<WeekendLoanUse> Uses, string Note, string ReportedBy, DateTime ReportedAt, string? ConfirmedBy = null, DateTime? ConfirmedAt = null);
