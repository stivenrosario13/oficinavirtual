using System;

internal record CompletedAudit(string Codigo, string Terminal, string Grupo, string Municipio, string Provincia, string Type, double Latitude, double Longitude, double Accuracy, DateTime SubmittedAt, Guid ProfileId, string? Observations, string? EmployeeName, string? SubmittedByLogin);
