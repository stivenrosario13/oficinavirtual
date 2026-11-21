using System;

internal record MaintenanceReturnCreate(Guid? SupplierId, Guid? PurchaseOrderId, string? Department, string? ProductName, string? SerialNumber, int Quantity, string? Reason, string? Notes);
