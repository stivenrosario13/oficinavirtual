using System.Collections.Generic;

internal record MaintenanceProcurementSnapshot(List<MaintenanceSupplierRow> Suppliers, List<MaintenanceRequisitionRow> Requisitions, List<MaintenancePurchaseOrderRow> PurchaseOrders, List<MaintenanceReturnRow> Returns);
