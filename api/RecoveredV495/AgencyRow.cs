using System;

internal record AgencyRow(Guid Id, string Codigo, string Terminal, double? ExpectedLatitude, double? ExpectedLongitude, int AuditCount = 0, DateTime? LastAuditedAt = null, string Status = "PENDING");
