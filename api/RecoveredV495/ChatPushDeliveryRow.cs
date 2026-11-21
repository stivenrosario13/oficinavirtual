using System;

public sealed record ChatPushDeliveryRow(Guid MessageId, string Endpoint, string P256dh, string Auth, Guid ConversationId, string AssignedDepartment, string SenderName, string Message, DateTime CreatedAt);
