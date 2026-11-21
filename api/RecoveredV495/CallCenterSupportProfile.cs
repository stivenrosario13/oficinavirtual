using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal static class CallCenterSupportProfile
{
	public static Dictionary<string, bool> Permissions()
	{
		using Stream utf8Json = typeof(CallCenterSupportProfile).Assembly.GetManifestResourceStream("CallCenterSupportProfile.json");
		return JsonSerializer.Deserialize<Dictionary<string, bool>>(utf8Json);
	}

	public static bool Applies(string role, string? team, IReadOnlyDictionary<string, bool>? saved)
	{
		if (!role.Equals("TECHNOLOGY", StringComparison.OrdinalIgnoreCase) || team != "CALL_CENTER")
		{
			return false;
		}
		if (saved != null && saved.GetValueOrDefault("usesCustomizedPermissions")) return false;
		if (saved != null && saved.GetValueOrDefault("profile:SUPPORT"))
		{
			return true;
		}
		if (saved == null || !saved.Any((KeyValuePair<string, bool> p) => p.Key.StartsWith("profile:") && p.Value))
		{
			if (saved == null)
			{
				return true;
			}
			return !saved.GetValueOrDefault("canManageDepartment");
		}
		return false;
	}

	public static Dictionary<string, bool> Normalize(Dictionary<string, bool>? original)
	{
		return SupportProfilePolicy.Standard(original, Permissions());
	}

	public static void Apply(Dictionary<string, bool> effective, IReadOnlyDictionary<string, bool>? saved, string role, string? team)
	{
		if (!Applies(role, team, saved))
		{
			return;
		}
		foreach (KeyValuePair<string, bool> item in Permissions())
		{
			effective[item.Key] = item.Value;
		}
	}
}
