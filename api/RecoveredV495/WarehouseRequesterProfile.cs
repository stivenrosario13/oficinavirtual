using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

internal static class WarehouseRequesterProfile
{
	public const string Marker = "profile:WAREHOUSE_REQUESTER";

	public static bool IsRequester(ClaimsPrincipal user)
	{
		try
		{
			Dictionary<string, bool>? saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.FindFirstValue("permissions_json") ?? "{}");
			return saved != null && saved.GetValueOrDefault("profile:WAREHOUSE_REQUESTER") && !saved.GetValueOrDefault("usesCustomizedPermissions");
		}
		catch (JsonException)
		{
			return false;
		}
	}

	public static void Apply(Dictionary<string, bool> effective, Dictionary<string, bool>? saved)
	{
		if (saved != null && saved.GetValueOrDefault("profile:WAREHOUSE_REQUESTER") && !saved.GetValueOrDefault("usesCustomizedPermissions"))
		{
			string[] array = new string[4] { "canView", "canConfigureProfile", "canRequestWarehouse", "canViewDocuments" };
			string[] array2 = effective.Keys.ToArray();
			foreach (string text in array2)
			{
				effective[text] = Enumerable.Contains(array, text);
			}
			string[] array3 = array;
			foreach (string key in array3)
			{
				effective[key] = true;
			}
			effective["profile:WAREHOUSE_REQUESTER"] = true;
			effective["usesExplicitPermissions"] = true;
		}
	}

	public static bool ApiAllowed(string path, string method)
	{
		path = path.TrimEnd('/').ToLowerInvariant();
		if (!path.StartsWith("/api/"))
		{
			return true;
		}
		bool flag = ((method == "GET" || method == "HEAD") ? true : false);
		bool flag2 = flag;
		if (flag2 && Enumerable.Contains(new string[10] { "/api/admin/session", "/api/license/status", "/api/version", "/api/health", "/api/warehouse/fulfillment/inventory", "/api/warehouse/fulfillment/track", "/api/tickets/agencies", "/api/support/catalog", "/api/institutional-documents/warehouse-groups", "/api/live/stream" }, path))
		{
			return true;
		}
		if (method == "POST" && Enumerable.Contains(new string[3] { "/api/admin/logout", "/api/admin/session/restore", "/api/institutional-documents" }, path))
		{
			return true;
		}
		if (path == "/api/account/avatar" && method == "POST")
		{
			return true;
		}
		bool flag3 = method == "POST";
		bool flag4 = flag3;
		if (flag4)
		{
			flag = ((path == "/api/account/recovery-code" || path == "/api/account/password/recover") ? true : false);
			flag4 = flag;
		}
		if (flag4)
		{
			return true;
		}
		if (flag2 && path.StartsWith("/api/account/avatar/") && Guid.TryParse(path.Substring(20), out var _))
		{
			return true;
		}
		bool flag5 = path == "/api/account";
		bool flag6 = flag5;
		if (flag6)
		{
			flag = ((method == "GET" || method == "PUT") ? true : false);
			flag6 = flag;
		}
		if (flag6 || (path == "/api/account/password" && method == "PUT"))
		{
			return true;
		}
		if (flag2 && path == "/api/institutional-documents")
		{
			return true;
		}
		if (flag2 && path.StartsWith("/api/institutional-documents/warehouse-submission/"))
		{
			return true;
		}
		string[] array = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
		bool flag7 = flag2;
		bool flag8 = flag7;
		if (flag8)
		{
			int num = array.Length;
			flag = (uint)(num - 3) <= 1u;
			flag8 = flag;
		}
		bool flag9 = flag8 && array[0] == "api" && array[1] == "institutional-documents" && long.TryParse(array[2], out var _);
		bool flag10 = flag9;
		if (flag10)
		{
			bool flag11 = array.Length == 3;
			bool flag12 = flag11;
			if (!flag12)
			{
				switch (array[3])
				{
				case "preview":
				case "file":
				case "download":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				flag12 = flag;
			}
			flag10 = flag12;
		}
		return flag10;
	}
}
