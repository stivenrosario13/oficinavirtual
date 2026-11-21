using System.Linq;

internal record DocumentActor(string Id, string Role, string Team, string Name, string Login, string? AssignedDepartment = null, bool? InventoryDelete = null, bool WarehouseRequestOnly = false, bool WarehouseAuthorize = false)
{
	public string Department => AssignedDepartment ?? DepartmentAccounts.AccountDepartment(Role, Team);

	public bool Manager
	{
		get
		{
			string role = Role;
			if ((!(role == "Administrator") && !(role == "Warehouse")) || 1 == 0)
			{
				return Team == "WAREHOUSE";
			}
			return true;
		}
	}

	public bool WarehouseRequestsBlocked => !Allowed;

	public bool Allowed
	{
		get
		{
			switch (Role)
			{
			case "Department":
			case "Technology":
			case "Administrative":
			case "HumanResources":
			case "Administrator":
			case "GroupAdministrator":
			case "Fiscalizador":
			case "Viewer":
			case "GeneralServices":
			case "Warehouse":
				return true;
			default:
				return false;
			}
		}
	}

	public bool CanDeleteInventory
	{
		get
		{
			if (Role == "Warehouse" || Team == "WAREHOUSE")
			{
				return InventoryDelete ?? Enumerable.Contains(new string[2] { "diorly.abreu@grupotejeda.local", "bryan.arias@grupotejeda.local" }, Login.Trim().ToLowerInvariant());
			}
			return false;
		}
	}

	public bool CanReadTechnicalFailureWarehouseTeam
	{
		get
		{
			if (Role == "Technology" && Team == "TECHNICAL_FAILURE")
			{
				return AdministrativeProfiles.Login(Login) == "rosmarlin.vittini";
			}
			return false;
		}
	}
}
