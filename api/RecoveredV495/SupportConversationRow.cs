using System;
using System.Collections.Generic;

internal record SupportConversationRow(Guid Id, string SupervisorUserId, string SupervisorName, string SupervisorUsername, string? SupervisorContact, string? SupervisorAvatarUrl, List<string> Groups, string AssignedDepartment, string? Category, string Status, bool RequestedAgent, DateTime CreatedAt, DateTime UpdatedAt, DateTime LastMessageAt, string LastMessage, bool IsMuted, bool IsRestricted, bool IsBlocked);
