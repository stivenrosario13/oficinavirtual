using System;

internal static class FieldAuditGps
{
	public const double MatchRadiusMeters = 75.0;

	public static bool Valid(double latitude, double longitude, double accuracy)
	{
		if (double.IsFinite(latitude) && latitude >= -90.0 && latitude <= 90.0 && double.IsFinite(longitude) && longitude >= -180.0 && longitude <= 180.0 && (latitude != 0.0 || longitude != 0.0) && double.IsFinite(accuracy))
		{
			return accuracy > 0.0;
		}
		return false;
	}

	public static bool ValidCapture(double latitude, double longitude, double accuracy, DateTimeOffset? captured, DateTimeOffset now)
	{
		if (Valid(latitude, longitude, accuracy) && captured.HasValue)
		{
			return captured.Value <= now.AddMinutes(2.0);
		}
		return false;
	}

	public static bool Matches(double latitude, double longitude, double accuracy, double referenceLatitude, double referenceLongitude, double referenceAccuracy, double minimumRadiusMeters = MatchRadiusMeters)
	{
		if (!Valid(latitude, longitude, accuracy) || !Valid(referenceLatitude, referenceLongitude, referenceAccuracy))
		{
			return false;
		}
		double num = Math.PI / 180.0;
		double num2 = (referenceLatitude - latitude) * num;
		double num3 = (referenceLongitude - longitude) * num;
		double num4 = Math.Pow(Math.Sin(num2 / 2.0), 2.0) + Math.Cos(latitude * num) * Math.Cos(referenceLatitude * num) * Math.Pow(Math.Sin(num3 / 2.0), 2.0);
		double num5 = 12742000.0 * Math.Atan2(Math.Sqrt(num4), Math.Sqrt(Math.Max(0.0, 1.0 - num4)));
		return num5 <= Math.Max(minimumRadiusMeters, accuracy + referenceAccuracy);
	}
}
