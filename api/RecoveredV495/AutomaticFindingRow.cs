using System;
using System.Collections.Generic;

internal record AutomaticFindingRow(Guid Id, Guid ProfileId, string Codigo, string Terminal, string Grupo, DateTime SubmittedAt, string FindingKey, string Title, string Detail, string Department, string Priority, string Status, string? Diagnosis, string? Resolution, string? Verification, string? Recommendations, DateTime? ResolvedAt, int? ResolutionMinutes, string? ResolvedByName, List<FindingEvidenceRow> Evidence, string? AssignedTechnicianId, string? AssignedTechnicianName, string? Direccion, string? Sector, string? Municipio, string? Provincia, double? Latitude, double? Longitude, string SupervisorName, string SupervisorUserId, string SupervisorLogin, string? AuditObservation, string? DamageAnswer);
