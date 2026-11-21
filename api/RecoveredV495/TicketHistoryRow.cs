using System;

internal record TicketHistoryRow(Guid Id, string Action, string? FromDepartment, string? ToDepartment, string? FromTeam, string? ToTeam, string ActorName, string? Comment, DateTime CreatedAt);
