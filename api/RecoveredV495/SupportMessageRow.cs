using System;

internal record SupportMessageRow(Guid Id, Guid ConversationId, string? SenderUserId, string SenderName, string SenderRole, string? AvatarUrl, string Message, bool IsBot, DateTime CreatedAt, string? VoiceNoteUrl, bool IsDeleted);
