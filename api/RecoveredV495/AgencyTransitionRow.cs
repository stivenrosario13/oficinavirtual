using System;
using System.Collections.Generic;

internal record AgencyTransitionRow(Guid Id, Guid AgencyId, string Codigo, string Terminal, string Grupo, string ProjectType, string Status, string CurrentStage, string? Notes, string CreatedByName, DateTime CreatedAt, DateTime UpdatedAt, DateTime? CompletedAt, List<AgencyTransitionStageRow> Stages);
