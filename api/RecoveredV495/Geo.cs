using System;

internal record Geo(double Latitude, double Longitude, double AccuracyMeters, DateTimeOffset? CapturedAt, string? Source);
