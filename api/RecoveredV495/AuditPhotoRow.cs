using System;

internal record AuditPhotoRow(string PhotoType, string Url, double Latitude, double Longitude, double Accuracy, DateTime CapturedAt);
