using System;
using System.Collections.Generic;

internal record AgencyTransitionProgressRow(Guid Id, string Note, string ProgressStatus, string EntryType, string CreatedByName, DateTime CreatedAt, DateTime? UpdatedAt, List<AgencyTransitionEvidenceRow> Evidence);
