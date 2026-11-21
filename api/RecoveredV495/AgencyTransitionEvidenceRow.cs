using System;

internal record AgencyTransitionEvidenceRow(Guid Id, string FileName, string ContentType, DateTime CapturedAt, string Url, double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null, DateTime? LocationCapturedAt = null, string? UploadedByName = null);
