using System.Collections.Generic;
using System.Linq;

internal static class SupportProfilePolicy
{
	public static bool IsSupport(string role, string? team, Dictionary<string, bool>? permissions)
	{
		bool flag;
		switch (role)
		{
		case "TECHNOLOGY":
		case "GENERAL_SERVICES":
		case "HUMAN_RESOURCES":
		case "DEPARTMENT":
		case "WAREHOUSE":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return false;
		}
		bool flag2 = role == "WAREHOUSE";
		bool flag3 = flag2;
		if (!flag3)
		{
			flag = ((team == "WAREHOUSE" || team == "WORKSHOP") ? true : false);
			flag3 = flag;
		}
		if (flag3)
		{
			return false;
		}
		if (permissions != null && permissions.GetValueOrDefault("profile:SUPPORT"))
		{
			return true;
		}
		if (permissions != null && permissions.Any((KeyValuePair<string, bool> p) => p.Key.StartsWith("profile:") && p.Value))
		{
			return false;
		}
		bool flag4 = role == "WAREHOUSE";
		bool flag5 = flag4;
		if (!flag5)
		{
			switch (team)
			{
			case "WAREHOUSE":
			case "WORKSHOP":
			case "TECHNICIANS":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			flag5 = flag;
		}
		if (flag5)
		{
			return false;
		}
		if (permissions != null && permissions.GetValueOrDefault("canManageDepartment"))
		{
			return false;
		}
		if (role == "TECHNOLOGY" && team == "CALL_CENTER")
		{
			return true;
		}
		return permissions?.GetValueOrDefault("canAssignTickets") ?? false;
	}

	public static Dictionary<string, bool> Standard(Dictionary<string, bool>? original, Dictionary<string, bool> reference)
	{
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>(original ?? new Dictionary<string, bool>());
		string[] array = dictionary.Keys.Where((string k) => k.StartsWith("can") || k.StartsWith("profile:")).ToArray();
		foreach (string key in array)
		{
			dictionary.Remove(key);
		}
		foreach (KeyValuePair<string, bool> item in reference.Where((KeyValuePair<string, bool> p) => p.Key.StartsWith("can")))
		{
			dictionary[item.Key] = item.Value;
		}
		dictionary["profile:SUPPORT"] = true;
		dictionary["usesExplicitPermissions"] = true;
		return dictionary;
	}

	public static Dictionary<string, bool> ForSave(Dictionary<string, bool>? selected, Dictionary<string, bool>? reference)
	{
		Dictionary<string, bool> result = new Dictionary<string, bool>(selected ?? reference ?? new Dictionary<string, bool>());
		result["profile:SUPPORT"] = true;
		result[ExplicitUserPermissions.Marker] = true;
		result["usesCustomizedPermissions"] = true;
		return result;
	}
}
