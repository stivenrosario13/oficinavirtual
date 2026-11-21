using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;

internal static class OperationalPermissions
{
	public static readonly string[] Keys = new string[25]
	{
		"canViewDocuments", "canUploadDocuments", "canManageDocumentFolders", "canShareDocuments", "canDeleteDocuments", "canReceiveDocuments", "canSignDocuments", "canDecideDocuments", "canManageDocumentTemplates", "canRequestWarehouse",
		"canViewWarehouseInventory", "canCreateWarehouseArticles", "canPrepareWarehouseRequests", "canWithdrawWarehouseRequests", "canCancelWarehouseRequests", "canViewWarehouseReports", "canViewWorkshop", "canOperateWorkshop", "canImportWorkshopInventory", "canViewMaintenance", "canCreateMaintenanceMovements",
		"canManageProcurement", "canViewAgencyTransitions", "canManageAgencyTransitions", "canManageAdministrativeAssets"
	};
	private static readonly string[] IncidentDocumentPermissions = new string[4] { "canViewDocuments", "canReceiveDocuments", "canSignDocuments", "canDecideDocuments" };

	private static bool IncidentOperator(string login, string? displayName = null)
	{
		return AdministrativeProfiles.IsIncidentOperator(login, displayName);
	}

	public static Dictionary<string, bool> Defaults(string role, string? team)
	{
		bool admin = role == "Administrator";
		bool warehouse = role == "Warehouse" || team == "WAREHOUSE";
		bool flag = admin | warehouse;
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3;
			switch (role)
			{
			case "Department":
			case "Technology":
			case "Administrative":
			case "HumanResources":
			case "GroupAdministrator":
			case "Fiscalizador":
			case "Viewer":
			case "GeneralServices":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag2 = flag3;
		}
		bool documents = flag2;
		bool flag4 = admin | warehouse;
		bool flag5 = flag4;
		if (!flag5)
		{
			bool flag3;
			switch (role)
			{
			case "Technology":
			case "GeneralServices":
			case "HumanResources":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag5 = flag3;
		}
		bool maintenance = flag5;
		bool flag6 = admin;
		bool flag7 = flag6;
		if (!flag7)
		{
			bool flag8 = role == "Technology";
			bool flag9 = flag8;
			if (flag9)
			{
				bool flag3 = ((team == "WORKSHOP" || team == "TECHNICAL_FAILURE") ? true : false);
				flag9 = flag3;
			}
			flag7 = flag9;
		}
		bool workshop = flag7;
		return Keys.Append("canDeleteWarehouseInventory").ToDictionary((string key) => key, (string key) =>
		{
			switch (key)
			{
			case "canDeleteWarehouseInventory":
				return warehouse;
			case "canCreateWarehouseArticles":
				return admin | warehouse;
			case "canRequestWarehouse":
				return documents;
			case "canCancelWarehouseRequests":
			case "canViewWarehouseInventory":
			case "canViewWarehouseReports":
				return documents;
			case "canPrepareWarehouseRequests":
			case "canWithdrawWarehouseRequests":
				return admin | warehouse;
			case "canViewWorkshop":
				return maintenance;
			case "canImportWorkshopInventory":
			case "canOperateWorkshop":
				return workshop;
			case "canViewMaintenance":
			case "canCreateMaintenanceMovements":
			case "canManageProcurement":
				return maintenance;
			case "canManageAgencyTransitions":
			case "canViewAgencyTransitions":
			{
				bool flag10 = admin;
				bool flag11 = flag10;
				if (!flag11)
				{
					bool flag12;
					switch (role)
					{
					case "Technology":
					case "GeneralServices":
					case "HumanResources":
						flag12 = true;
						break;
					default:
						flag12 = false;
						break;
					}
					flag11 = flag12;
				}
				return flag11;
			}
			case "canManageAdministrativeAssets":
				return admin || role == "Administrative" || role == "Department";
			default:
				return documents;
			}
		});
	}

	public static void Apply(Dictionary<string, bool> effective, Dictionary<string, bool>? saved, string role, string? team, string login = "")
	{
		foreach (var (key, flag2) in Defaults(role, team))
		{
			effective[key] = flag2 && (saved?.GetValueOrDefault(key, defaultValue: true) ?? true);
		}
		effective["canRequestWarehouse"] = Defaults(role, team)["canRequestWarehouse"];
		effective["canShareDocuments"] = Defaults(role, team)["canShareDocuments"];
		if (role == "Warehouse" || team == "WAREHOUSE")
		{
			string[] array = new string[3] { "canViewWorkshop", "canViewMaintenance", "canWithdrawWarehouseRequests" };
			foreach (string key2 in array)
			{
				effective[key2] = true;
			}
		}
		if (saved == null || !saved.ContainsKey("canDeleteWarehouseInventory"))
		{
			effective["canDeleteWarehouseInventory"] = WarehouseInventoryDeleteAccess.Allowed(role, team ?? "", login);
		}
		if (saved == null || !saved.ContainsKey("canCreateWarehouseArticles"))
		{
			effective["canCreateWarehouseArticles"] = role == "Administrator" || WarehouseArticleCreationAccess.Named(login);
		}
		if (saved == null || !saved.ContainsKey("canOperateWorkshop"))
		{
			effective["canOperateWorkshop"] = role == "Administrator" || (role == "Technology" && team == "TECHNICAL_FAILURE" && Enumerable.Contains(new string[4] { "rosmarlin.vittini", "rosmarlin.vittini@grupotejeda.local", "michael.miguel", "michael.miguel@grupotejeda.local" }, login.Trim().ToLowerInvariant()));
		}
		if (IncidentOperator(login))
		{
			foreach (string key3 in IncidentDocumentPermissions) effective[key3] = true;
		}
	}

	public static bool Allowed(ClaimsPrincipal user, string key)
	{
		IIdentity? identity = user.Identity;
		if (identity == null || !identity.IsAuthenticated)
		{
			return false;
		}
		if (IncidentOperator(identity.Name ?? "", user.FindFirstValue("display_name")) && IncidentDocumentPermissions.Contains(key)) return true;
		Dictionary<string, bool>? explicitPermissions;
		try
		{
			explicitPermissions = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.FindFirstValue("permissions_json") ?? "{}");
		}
		catch (JsonException)
		{
			return false;
		}
		if (explicitPermissions != null && explicitPermissions.GetValueOrDefault(ExplicitUserPermissions.Marker) && explicitPermissions.TryGetValue(key, out bool explicitValue)) return explicitValue;
		Dictionary<string, bool> dictionary = Defaults(user.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", user.FindFirstValue("support_team"));
		if (!dictionary.GetValueOrDefault(key))
		{
			return false;
		}
		if (key == "canCreateWarehouseArticles")
		{
			try
			{
				Dictionary<string, bool>? saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.FindFirstValue("permissions_json") ?? "{}");
				if (saved != null && saved.TryGetValue(key, out bool granted)) return granted;
				return user.IsInRole("Administrator") || WarehouseArticleCreationAccess.Named(user.Identity?.Name);
			}
			catch (JsonException)
			{
				return false;
			}
		}
		bool flag = ((key == "canRequestWarehouse" || key == "canShareDocuments") ? true : false);
		bool flag2 = flag;
		bool flag3 = flag2;
		if (!flag3)
		{
			bool flag4;
			switch (key)
			{
			case "canViewWorkshop":
			case "canViewMaintenance":
			case "canWithdrawWarehouseRequests":
				flag4 = true;
				break;
			default:
				flag4 = false;
				break;
			}
			flag3 = flag4 && (user.IsInRole("Warehouse") || user.FindFirstValue("support_team") == "WAREHOUSE");
		}
		if (flag3)
		{
			return true;
		}
		try
		{
			Dictionary<string, bool> dictionary2 = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.FindFirstValue("permissions_json") ?? "{}");
			return dictionary2 == null || dictionary2.GetValueOrDefault(key, defaultValue: true);
		}
		catch (JsonException)
		{
			return false;
		}
	}

	public static string? Required(string path, string method)
	{
		path = path.TrimEnd('/').ToLowerInvariant();
		bool flag = ((method == "GET" || method == "HEAD") ? true : false);
		bool flag2 = flag;
		string text = path.Split('/').Last();
		if (path == "/api/warehouse/fulfillment/inventory")
		{
			return "canRequestWarehouse";
		}
		if (path.StartsWith("/api/warehouse/fulfillment/"))
		{
			switch (text)
			{
			case "remove":
				return "canDeleteWarehouseInventory";
			case "prepare":
			case "pending-purchase":
				return "canPrepareWarehouseRequests";
			case "withdraw":
				return "canWithdrawWarehouseRequests";
			case "analytics":
			case "frequency":
			case "track":
				return "canViewWarehouseReports";
			default:
				return null;
			}
		}
		if (path == "/api/maintenance/department-requests")
		{
			return "canRequestWarehouse";
		}
		if (method == "POST" && path.StartsWith("/api/workshop/") && path.EndsWith("/warehouse-receipt"))
		{
			return null;
		}
		if (path.StartsWith("/api/workshop"))
		{
			if (!flag2)
			{
				if (!path.EndsWith("/fiscalized"))
				{
					return "canOperateWorkshop";
				}
				return "canImportWorkshopInventory";
			}
			return "canViewWorkshop";
		}
		if (path.StartsWith("/api/agency-transitions"))
		{
			if (!flag2)
			{
				return "canManageAgencyTransitions";
			}
			return "canViewAgencyTransitions";
		}
		if (path.StartsWith("/api/maintenance/"))
		{
			if (!flag2)
			{
				if (!path.EndsWith("/document"))
				{
					if (!path.Contains("/movements"))
					{
						return "canManageProcurement";
					}
					return "canCreateMaintenanceMovements";
				}
				return null;
			}
			return "canViewMaintenance";
		}
		if (path.StartsWith("/api/administrative-assets"))
		{
			return "canManageAdministrativeAssets";
		}
		if (path.StartsWith("/api/administrative-files"))
		{
			if (!flag2)
			{
				if (!(text == "shared"))
				{
					return "canManageDocumentFolders";
				}
				return "canShareDocuments";
			}
			return "canViewDocuments";
		}
		if (path.StartsWith("/api/institutional-documents"))
		{
			if (flag2 && path.StartsWith("/api/institutional-documents/warehouse-submission/"))
			{
				return "canRequestWarehouse";
			}
			if (flag2 && path == "/api/institutional-documents/warehouse-groups")
			{
				return "canRequestWarehouse";
			}
			if (flag2)
			{
				return "canViewDocuments";
			}
			if (path == "/api/institutional-documents" && method == "POST")
			{
				return null;
			}
			if (path.Contains("/templates"))
			{
				return "canManageDocumentTemplates";
			}
			if (path.EndsWith("/incident-tickets/decision")) return "canDecideDocuments";
			switch (text)
			{
			case "share":
			case "unshare":
				return "canShareDocuments";
			case "trash":
			case "purge":
			case "restore":
				return "canDeleteDocuments";
			case "receive":
				return "canReceiveDocuments";
			case "move":
			case "sign":
				return "canSignDocuments";
			case "approve":
			case "reject":
			case "correction":
				return "canDecideDocuments";
			case "cancel":
				return "canCancelWarehouseRequests";
			default:
				return "canUploadDocuments";
			}
		}
		return null;
	}
}
