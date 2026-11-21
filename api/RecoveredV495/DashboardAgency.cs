using System;

internal record DashboardAgency(string Codigo, string Terminal, string Grupo, string Status, int Percent, DateTime? LastUpdated, int AuditCount = 0);
