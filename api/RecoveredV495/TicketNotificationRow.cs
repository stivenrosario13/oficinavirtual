using System;

internal record TicketNotificationRow(Guid Id, long TicketNumber, string Codigo, string Terminal, string Grupo, string AssignedDepartment, string Subject, string? Resolution, string Status, DateTime UpdatedAt, string EventType, string? Message, string? ActorName, Guid NotificationId, string TicketType, bool IsRead)
{
	public TicketNotificationRow(Guid id, long ticketNumber, string codigo, string terminal, string grupo, string assignedDepartment, string subject, string? resolution, string status, DateTime updatedAt)
		: this(id, ticketNumber, codigo, terminal, grupo, assignedDepartment, subject, resolution, status, updatedAt, ((status == "RESOLVED" || status == "CLOSED") ? true : false) ? status : "CREATED", null, null, id, "SUPPORT", IsRead: false)
	{
	}
}
