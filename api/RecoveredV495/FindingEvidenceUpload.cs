using System;

internal record FindingEvidenceUpload(string FileName, string ContentType, byte[] Data, double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null, DateTime? LocationCapturedAt = null);
