using System;

internal record TicketCreate(Guid? AgencyId, string? Category, string? AssignedDepartment, string? Priority, string? Subject, string? Description, string? TicketType, string? AssignedTechnicianId, string? AssignedTeam, bool? AllowDuplicate, Guid? WorkshopStockId = null);
