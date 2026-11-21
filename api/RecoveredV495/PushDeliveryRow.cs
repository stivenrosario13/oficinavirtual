using System;

public sealed record PushDeliveryRow(Guid NotificationId, string Endpoint, string P256dh, string Auth, long TicketNumber, string Subject, string? Message, Guid TicketId, string AssignedDepartment, string TicketType, string TicketStatus, DateTime CreatedAt);
