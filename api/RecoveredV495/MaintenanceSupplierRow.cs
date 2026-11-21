using System;

internal record MaintenanceSupplierRow(Guid Id, string Name, string? TaxId, string? ContactName, string? Phone, string? Email, bool IsActive, DateTime CreatedAt);
