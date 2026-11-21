using System;

internal record WarehouseAllocationItem(long Id, long DocumentId, string RequestNumber, string Department, string AgencyCode, string AgencyName, string AgencyGroup, string Equipment, string Serial, int Quantity, string Status, string? DeliveryNumber, string? DeliveredBy, string? ReceivedBy, DateTime? WithdrawnUtc);
