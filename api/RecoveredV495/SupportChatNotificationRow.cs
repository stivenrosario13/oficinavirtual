using System;

internal record SupportChatNotificationRow(Guid Id, Guid ConversationId, string AssignedDepartment, string SupervisorName, string SenderName, string SenderRole, string Message, DateTime CreatedAt, bool IsRead);
