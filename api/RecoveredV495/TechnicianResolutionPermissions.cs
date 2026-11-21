using System.Collections.Generic;
using System.Linq;

internal static class TechnicianResolutionPermissions
{
	public const string Marker = "profile:TECHNICIAN";

	public static bool IsTechnician(string role, string? team, IReadOnlyDictionary<string, bool>? permissions)
	{
		role = role.ToUpperInvariant();
		team = team?.ToUpperInvariant();
		bool flag;
		switch (role)
		{
		case "TECHNOLOGY":
		case "GENERAL_SERVICES":
		case "GENERALSERVICES":
		case "HUMAN_RESOURCES":
		case "HUMANRESOURCES":
		case "DEPARTMENT":
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
		if ((permissions != null && permissions.GetValueOrDefault("profile:WAREHOUSE_REQUESTER")) || team == "WAREHOUSE")
		{
			return false;
		}
		if (permissions != null && permissions.GetValueOrDefault("profile:TECHNICIAN"))
		{
			return true;
		}
		if ((permissions != null && permissions.Any((KeyValuePair<string, bool> item) => item.Key.StartsWith("profile:") && item.Value)) || (permissions != null && permissions.GetValueOrDefault("canManageDepartment")))
		{
			return false;
		}
		switch (role)
		{
		case "TECHNOLOGY":
			return team == "TECHNICIANS";
		case "GENERAL_SERVICES":
		case "GENERALSERVICES":
		case "HUMAN_RESOURCES":
		case "HUMANRESOURCES":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			if (!(team == "WORKSHOP"))
			{
				if (permissions != null && permissions.TryGetValue("canAssignTickets", out var value))
				{
					return !value;
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static void Apply(Dictionary<string, bool> effective, IReadOnlyDictionary<string, bool>? saved, string role, string? team)
	{
		if (saved != null)
		{
			foreach (KeyValuePair<string, bool> item in saved.Where((KeyValuePair<string, bool> item) => item.Key.StartsWith("profile:") && item.Value))
			{
				effective[item.Key] = true;
			}
		}
		if (saved != null && saved.GetValueOrDefault("usesCustomizedPermissions")) return;
		if (IsTechnician(role, team, saved))
		{
			effective["canResolveTickets"] = true;
			effective["canViewSupportDashboard"] = true;
			effective["profile:TECHNICIAN"] = true;
		}
	}
}
