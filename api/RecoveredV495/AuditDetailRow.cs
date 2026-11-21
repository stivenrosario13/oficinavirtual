using System;
using System.Collections.Generic;
using System.Text.Json;

internal record AuditDetailRow(string Codigo, string Terminal, string Grupo, string Municipio, string Provincia, string Type, double Latitude, double Longitude, double Accuracy, DateTime SubmittedAt, Guid ProfileId, string? Observations, string EmployeeName, string? EmployeeCode, string Direccion, string Sector, JsonElement Answers, string? SubmittedByLogin, List<AuditPhotoRow> Photos);
