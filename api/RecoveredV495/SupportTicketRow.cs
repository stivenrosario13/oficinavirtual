using System;
using System.Collections.Generic;

internal record SupportTicketRow(Guid Id, long TicketNumber, string Codigo, string Terminal, string Grupo, string Category, string AssignedDepartment, string Priority, string Status, string Subject, string Description, string? Resolution, string CreatedByName, string CreatedByUsername, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ClosedAt, bool IsAutomatic, string TicketType, string? AssignedTechnicianId, string? AssignedTechnicianName, string? Direccion, string? Sector, string? Municipio, string? Provincia, double? Latitude, double? Longitude, string? AssignedTeam, List<string> SharedWithDepartments, List<TicketHistoryRow> History, List<TicketEvidenceRow> Evidence, string? ResolvedByName)
{
	public bool IsAssignedToCurrentUser { get; init; }

	public bool IsCreatedByCurrentUser { get; init; }

	public SupportTicketRow(Guid id, long ticketNumber, string codigo, string terminal, string grupo, string category, string assignedDepartment, string priority, string status, string subject, string description, string? resolution, string createdByName, string createdByUsername, DateTime createdAt, DateTime updatedAt, DateTime? closedAt, bool isAutomatic, string ticketType, string? assignedTechnicianId, string? assignedTechnicianName, string? direccion, string? sector, string? municipio, string? provincia, double? latitude, double? longitude)
		: this(id, ticketNumber, codigo, terminal, grupo, category, assignedDepartment, priority, status, subject, description, resolution, createdByName, createdByUsername, createdAt, updatedAt, closedAt, isAutomatic, ticketType, assignedTechnicianId, assignedTechnicianName, direccion, sector, municipio, provincia, latitude, longitude, null, new List<string>(), new List<TicketHistoryRow>(), new List<TicketEvidenceRow>(), null)
	{
	}
}
