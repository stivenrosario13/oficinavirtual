using System;

internal record MaintenanceMovementCreate(Guid? AgencyId, Guid? TicketId, string? TechnicianUserId, string? TechnicianName, string? MovementType, string? EquipmentType, string? ComponentType, string? FailureCause, string? SerialNumber, int Quantity, string? Notes, string? Department, string? OperationalArea, string? DocumentNumber, string? QrToken, string? DeliveredByName, string? ReceivedByName, string? DestinationName, string? SignatureData, DateTime? OccurredAt);
