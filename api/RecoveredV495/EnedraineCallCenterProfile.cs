using System;
using System.Collections.Generic;

internal static class EnedraineCallCenterProfile
{
	public const string Email = "enedraine.elisabeth@grupotejeda.local";

	public static bool IsTarget(string email)
	{
		if (!email.Trim().Equals("enedraine.elisabeth@grupotejeda.local", StringComparison.OrdinalIgnoreCase))
		{
			return email.Trim().Equals("enedraine.elisabeth", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public static Dictionary<string, bool> DefaultPermissions()
	{
		Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
		string[] array = new string[18]
		{
			"canManage", "canManageUsers", "canManageDepartment", "canViewFieldAudits", "canManageAudits", "canManageAgencies", "canViewDashboard", "canAudit", "canExportAudits", "canViewAllSupportDepartments",
			"canConfigureSupport", "canModerateSupportChat", "canDeleteSupportConversations", "canDeleteSupportTickets", "canCancelFindingWithoutEvidence", "canCancelFindingWithoutReason", "canAuthorizeWarehouseRequests", "canLoadWarehouseInventory"
		};
		foreach (string key in array)
		{
			dictionary[key] = false;
		}
		string[] array2 = new string[13]
		{
			"canView", "canConfigureProfile", "canCreateTickets", "canAssignTickets", "canResolveTickets", "canViewSupportDashboard", "canViewSupportFindings", "canCancelFindingWithEvidence", "canCancelFindingWithReason", "canUseSupportChat",
			"canManageSupportChat", "canShareSupportConversations", "canViewTicketNotifications"
		};
		foreach (string key2 in array2)
		{
			dictionary[key2] = true;
		}
		dictionary["usesExplicitPermissions"] = true;
		dictionary["profile:SUPPORT"] = true;
		return dictionary;
	}
}
