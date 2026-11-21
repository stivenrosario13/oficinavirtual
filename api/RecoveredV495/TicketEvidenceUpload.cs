using System;

internal record TicketEvidenceUpload(string FileName, string ContentType, byte[] Data, double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null, DateTime? LocationCapturedAt = null);
