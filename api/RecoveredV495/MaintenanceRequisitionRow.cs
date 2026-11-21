using System;

internal record MaintenanceRequisitionRow(Guid Id, string RequisitionNumber, string Department, string ProductName, int QuantityRequested, int QuantityFulfilled, string Priority, string Status, string RequestedByName, DateTime CreatedAt, DateTime UpdatedAt, string? Notes);
