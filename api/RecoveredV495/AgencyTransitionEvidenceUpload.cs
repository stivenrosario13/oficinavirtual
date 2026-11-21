using System;

internal record AgencyTransitionEvidenceUpload(string FileName, string ContentType, byte[] Data, double? Latitude = null, double? Longitude = null, double? AccuracyMeters = null, DateTime? LocationCapturedAt = null);
