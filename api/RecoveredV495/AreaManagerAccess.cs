using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;

internal static class AreaManagerAccess
{
	public const string Permission = "canManageDepartment";

	public static void ApplyViews(Dictionary<string, bool> values, IReadOnlyDictionary<string, bool>? custom)
	{
		if (values.GetValueOrDefault("canManageDepartment"))
		{
			string[] array = new string[3] { "canViewFieldAudits", "canViewDashboard", "canViewSupportDashboard" };
			foreach (string key in array)
			{
				values[key] = custom == null || !custom.TryGetValue(key, out var value) || value;
			}
		}
	}

	public static bool Default(string role, string? team, string login)
	{
		if (!(role == "Administrator") && ((!(role == "Warehouse") && !(team == "WAREHOUSE")) || !login.Equals("bryan.arias@grupotejeda.local", StringComparison.OrdinalIgnoreCase)) && (!(team == "CALL_CENTER") || (!login.Equals("yessica.mercedes@grupotejeda.local", StringComparison.OrdinalIgnoreCase) && !login.Equals("yessica.mercedes", StringComparison.OrdinalIgnoreCase))))
		{
			if (role == "Technology" && team == "TECHNICAL_FAILURE")
			{
				if (!login.Equals("rosmarlin.vittini@grupotejeda.local", StringComparison.OrdinalIgnoreCase))
				{
					return login.Equals("rosmarlin.vittini", StringComparison.OrdinalIgnoreCase);
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public static bool Allowed(ClaimsPrincipal user)
	{
		IIdentity? identity = user.Identity;
		if (identity == null || !identity.IsAuthenticated)
		{
			return false;
		}
		if (user.IsInRole("Administrator"))
		{
			return true;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(user.FindFirstValue("permissions_json") ?? "{}");
			if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
			{
				return false;
			}
			if (jsonDocument.RootElement.TryGetProperty("canManageDepartment", out var value))
			{
				return value.ValueKind == JsonValueKind.True;
			}
		}
		catch (JsonException)
		{
			return false;
		}
		return Default(user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", user.FindFirstValue("support_team"), user.Identity?.Name ?? "");
	}

	public static string? Scope(ClaimsPrincipal user)
	{
		if (!Allowed(user))
		{
			return null;
		}
		if (user.IsInRole("Administrator"))
		{
			return "ALL";
		}
		if (user.IsInRole("Department"))
		{
			if (!DepartmentAccounts.Standalone(user.FindFirstValue("support_team")))
			{
				return null;
			}
			return user.FindFirstValue("support_team");
		}
		string text = user.FindFirstValue("support_team");
		if (user.IsInRole("Warehouse") || text == "WAREHOUSE")
		{
			return "WAREHOUSE";
		}
		if (text == "WORKSHOP")
		{
			return "WORKSHOP";
		}
		if (user.IsInRole("Technology"))
		{
			if (!(text == "TECHNICAL_FAILURE"))
			{
				if (!(text == "CALL_CENTER"))
				{
					return "TECHNOLOGY";
				}
				return "CALL_CENTER";
			}
			return "TECHNICAL_FAILURE";
		}
		return user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") switch
		{
			"GeneralServices" => "GENERAL_SERVICES", 
			"HumanResources" => "HUMAN_RESOURCES", 
			"Administrative" => "ADMINISTRATIVE", 
			"GroupAdministrator" => "SUPERVISION", 
			_ => null, 
		};
	}

	public static string? RequestedScope(ClaimsPrincipal user, string? requested)
	{
		string text = Scope(user);
		if (text == null)
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(requested) || requested == "ALL")
		{
			return text;
		}
		if (requested == text)
		{
			return text;
		}
		if (text == "ALL" && DepartmentAccounts.Valid(requested))
		{
			return requested;
		}
		bool flag = text == "TECHNOLOGY";
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = ((requested == "CALL_CENTER" || requested == "TECHNICAL_FAILURE") ? true : false);
			flag2 = flag3;
		}
		if (!flag2)
		{
			return null;
		}
		return requested;
	}
}
