using System;

internal record MaintenancePurchaseOrderRow(Guid Id, string OrderNumber, Guid SupplierId, string SupplierName, Guid? RequisitionId, string Department, string ProductName, int QuantityOrdered, int QuantityReceived, decimal UnitCost, string Status, DateTime? ExpectedAt, string CreatedByName, DateTime CreatedAt, DateTime UpdatedAt, string? Notes);
