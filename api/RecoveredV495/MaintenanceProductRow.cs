using System;

internal record MaintenanceProductRow(Guid Id, string Department, string ScanCode, string ProductName, string? ComponentType, DateTime UpdatedAt);
