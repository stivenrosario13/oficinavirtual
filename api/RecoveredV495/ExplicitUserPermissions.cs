using System.Collections.Generic;
using System.Linq;

internal static class ExplicitUserPermissions
{
	public const string Marker = "usesExplicitPermissions";

	public static void Apply(Dictionary<string, bool> effective, Dictionary<string, bool>? saved, string role)
	{
		if (role == "Department")
		{
			string[] array = new string[2] { "profile:ADMINISTRATIVE", "profile:DEPARTMENT_ADMINISTRATIVE" };
			foreach (string key in array)
			{
				if (saved != null && saved.GetValueOrDefault(key))
				{
					effective[key] = true;
				}
			}
		}
		if (saved == null || !saved.GetValueOrDefault("usesExplicitPermissions"))
		{
			return;
		}
		string[] array2 = effective.Keys.ToArray();
		foreach (string text in array2)
		{
			if (text.StartsWith("can") && saved.TryGetValue(text, out var value)) effective[text] = value;
		}
		if (role == "Administrator")
		{
			effective["canManageUsers"] = true;
		}
		effective["canManage"] = effective.GetValueOrDefault("canManageUsers");
		effective["usesExplicitPermissions"] = true;
	}
}
