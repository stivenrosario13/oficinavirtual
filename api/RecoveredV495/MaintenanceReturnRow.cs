using System;

internal record MaintenanceReturnRow(Guid Id, string ReturnNumber, Guid? SupplierId, string? SupplierName, Guid? PurchaseOrderId, string Department, string ProductName, string SerialNumber, int Quantity, string Reason, string Status, string CreatedByName, DateTime CreatedAt, string? Notes);
