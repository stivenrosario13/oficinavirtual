using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

internal static class DepartmentAccounts
{
	public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
	{
		["TECHNOLOGY"] = "Tecnología",
		["GENERAL_SERVICES"] = "Servicios Generales",
		["HUMAN_RESOURCES"] = "Recursos Humanos",
		["ADMINISTRATIVE"] = "Administración",
		["WAREHOUSE"] = "Almacén",
		["WORKSHOP"] = "Taller",
		["COMMUNICATION"] = "Comunicación",
		["COMMERCIALIZATION"] = "Comercialización",
		["TREASURY"] = "Tesorería",
		["ACCOUNTING"] = "Contabilidad",
		["GENERAL_AUDIT"] = "Auditoría General",
		["VAULT"] = "Bóveda",
		["CASHIER"] = "Caja",
		["CALL_CENTER"] = "Call Center",
		["TECHNICAL_FAILURE"] = "Avería Técnica",
		["TECHNICIANS"] = "Técnicos",
		["SUPERVISION"] = "Supervisión"
	};

	public static bool Valid(string? code)
	{
		if (code != null)
		{
			return Names.ContainsKey(code);
		}
		return false;
	}

	public static string AccountDepartment(string role, string? team)
	{
		switch (role.ToUpperInvariant())
		{
		case "DEPARTMENT":
			return Names.GetValueOrDefault(team ?? "", "Sin departamento");
		case "WAREHOUSE":
			return "Almacén";
		case "TECHNOLOGY":
			return Names.GetValueOrDefault(team ?? "", "Tecnología");
		case "GENERALSERVICES":
		case "GENERAL_SERVICES":
			return (team == "WORKSHOP") ? "Taller" : "Servicios Generales";
		case "HUMAN_RESOURCES":
		case "HUMANRESOURCES":
			return "Recursos Humanos";
		case "GROUPADMINISTRATOR":
		case "GROUP_ADMIN":
			return "Supervisión";
		default:
			return "Administración";
		}
	}

	public static bool LegacyStandalone(string? code)
	{
		switch (code)
		{
		case "COMMUNICATION":
		case "GENERAL_AUDIT":
		case "ADMINISTRATIVE":
		case "COMMERCIALIZATION":
		case "TREASURY":
		case "ACCOUNTING":
		case "VAULT":
		case "CASHIER":
		case "SUPERVISION":
			return true;
		default:
			return false;
		}
	}

	public static bool Standalone(string? code)
	{
		return Valid(code);
	}

	public static string SupportScopeRole(ClaimsPrincipal user, string path, bool administrativeManager = false)
	{
		string text = user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "Viewer";
		if (((text == "Administrative") & administrativeManager) && (path.StartsWith("/api/tickets", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/support/chat", StringComparison.OrdinalIgnoreCase)))
		{
			return "Department:ADMINISTRATIVE";
		}
		if (text != "Department" || (!path.StartsWith("/api/tickets", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("/api/support/chat", StringComparison.OrdinalIgnoreCase)))
		{
			return text;
		}
		string text2 = user.FindFirstValue("support_team");
		return "Department:" + (Valid(text2) ? text2 : "INVALID");
	}

	public static bool IsSupportScope(string role)
	{
		return role.StartsWith("Department:", StringComparison.Ordinal);
	}

	public static string SupportDepartment(string role)
	{
		if (!IsSupportScope(role) || !Valid(role.Substring(11)))
		{
			return "__DENIED__";
		}
		return role.Substring(11);
	}

	public static bool ApiAllowed(string path, string method)
	{
		if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (Enumerable.Contains(new string[7] { "/api/admin/login", "/api/admin/logout", "/api/admin/session", "/api/admin/session/restore", "/api/license/status", "/api/version", "/api/health" }, path))
		{
			return true;
		}
		if (method == "GET" && Enumerable.Contains(new string[5] { "/api/department-support", "/api/department-audit", "/api/manager-team-members", "/api/support/catalog", "/api/live/stream" }, path))
		{
			return true;
		}
		if (method == "GET" && Enumerable.Contains(new string[4] { "/api/warehouse/fulfillment/inventory", "/api/warehouse/fulfillment/track", "/api/warehouse/fulfillment/notifications", "/api/warehouse/fulfillment/allocations" }, path))
		{
			return true;
		}
		bool flag = path == "/api/push/public-key" && method == "GET";
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = path == "/api/push/subscriptions";
			bool flag4 = flag3;
			if (flag4)
			{
				bool flag5 = ((method == "POST" || method == "DELETE") ? true : false);
				flag4 = flag5;
			}
			flag2 = flag4;
		}
		if (flag2)
		{
			return true;
		}
		bool flag6 = path == "/api/tickets" || path.StartsWith("/api/tickets/", StringComparison.OrdinalIgnoreCase);
		bool flag7 = flag6;
		if (flag7)
		{
			bool flag5;
			switch (method)
			{
			case "GET":
			case "POST":
			case "PUT":
				flag5 = true;
				break;
			default:
				flag5 = false;
				break;
			}
			flag7 = flag5;
		}
		if (flag7)
		{
			return true;
		}
		bool flag8 = path.StartsWith("/api/support/chat/", StringComparison.OrdinalIgnoreCase);
		bool flag9 = flag8;
		if (flag9)
		{
			bool flag5;
			switch (method)
			{
			case "GET":
			case "POST":
			case "PUT":
				flag5 = true;
				break;
			default:
				flag5 = false;
				break;
			}
			flag9 = flag5;
		}
		if (flag9)
		{
			return true;
		}
		if (method == "DELETE" && path.StartsWith("/api/support/chat/messages/", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(path.Substring("/api/support/chat/messages/".Length), out var _))
		{
			return true;
		}
		return new string[4] { "/api/account", "/api/administrative-files", "/api/administrative-assets", "/api/institutional-documents" }.Any((string p) => path == p || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
	}
}
