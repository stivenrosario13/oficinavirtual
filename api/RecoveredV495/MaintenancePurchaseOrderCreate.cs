using System;

internal record MaintenancePurchaseOrderCreate(Guid SupplierId, Guid? RequisitionId, string? Department, string? ProductName, int Quantity, decimal UnitCost, DateTime? ExpectedAt, string? Notes);
