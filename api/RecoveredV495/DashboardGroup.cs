using System;

internal record DashboardGroup(
    string Grupo,
    int Total,
    int Completed,
    int Pending,
    int Percent,
    DateTime? LastUpdated,
    int AuditCount = 0,
    int CompletedRounds = 0,
    int RoundsTotal = 1);
