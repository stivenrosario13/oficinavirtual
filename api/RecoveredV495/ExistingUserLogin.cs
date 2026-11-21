using System;

internal static class ExistingUserLogin
{
	public static bool Unchanged(string? requested, string? stored)
	{
		if (!string.IsNullOrWhiteSpace(stored))
		{
			return string.Equals(requested?.Trim(), stored.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
