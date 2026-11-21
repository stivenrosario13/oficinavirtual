using System;
using System.Collections.Generic;

internal record AgencyTransitionStageRow(Guid Id, string Department, int StageOrder, string Status, string? Notes, DateTime? StartedAt, DateTime? CompletedAt, string? CompletedByName, DateTime UpdatedAt, List<AgencyTransitionProgressRow> Progress);
