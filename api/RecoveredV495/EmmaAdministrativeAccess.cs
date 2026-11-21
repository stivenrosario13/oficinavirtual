using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

internal static class EmmaAdministrativeAccess
{
	public static bool IsEmma(string? login)
	{
		return AdministrativeProfiles.Login(login ?? "") == "emma.mendoza";
	}

	public static bool Allowed(ClaimsPrincipal user)
	{
		if (user.IsInRole("Administrative"))
		{
			return IsEmma(user.Identity?.Name);
		}
		return false;
	}

	public static AdminIdentity Scope(AdminIdentity account)
	{
		if (!IsEmma(account.Email))
		{
			return account;
		}
		return account with
		{
			Role = "Administrative",
			SupportTeam = null,
			Permissions = new Dictionary<string, bool>
			{
				["canView"] = true,
				["canConfigureProfile"] = true,
				["canCreateTickets"] = true
			}
		};
	}

	public static bool ApiAllowed(ClaimsPrincipal user, string path, string method)
	{
		if (Allowed(user))
		{
			if (!(method == "GET") || !new string[4] { "/api/support/catalog", "/api/tickets", "/api/tickets/agencies", "/api/department-audit" }.Contains(path, StringComparer.OrdinalIgnoreCase))
			{
				if (method == "POST")
				{
					return path.Equals("/api/tickets", StringComparison.OrdinalIgnoreCase);
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
