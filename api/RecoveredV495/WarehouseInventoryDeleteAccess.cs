using System;

internal static class WarehouseInventoryDeleteAccess
{
	public static bool Allowed(string role, string team, string login)
	{
		if (role == "Warehouse" || team == "WAREHOUSE")
		{
			if (!string.Equals(login.Trim(), "diorly.abreu@grupotejeda.local", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(login.Trim(), "bryan.arias@grupotejeda.local", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return false;
	}
}
