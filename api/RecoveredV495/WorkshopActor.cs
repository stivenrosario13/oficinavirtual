using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

internal record WorkshopActor(string Id, string Role, string? Team, string Name, string Login, bool? Operate = null)
{
	public string Department => Role switch
	{
		"Technology" => "TECHNOLOGY", 
		"GeneralServices" => "GENERAL_SERVICES", 
		"HumanResources" => "HUMAN_RESOURCES", 
		_ => "", 
	};

	public bool IsTechnologyManager
	{
		get
		{
			if (Role == "Technology")
			{
				return Team == null;
			}
			return false;
		}
	}

	public bool IsNamedManager
	{
		get
		{
			if (Role == "Technology" && Team == "TECHNICAL_FAILURE")
			{
				return WorkshopManagerLogin(Login);
			}
			return false;
		}
	}

	public bool CanOperate
	{
		get
		{
			bool flag = Role == "Administrator";
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3 = Role == "Technology";
				bool flag4 = flag3;
				if (flag4)
				{
					string team = Team;
					bool flag5 = ((team == "WORKSHOP" || team == "TECHNICAL_FAILURE") ? true : false);
					flag4 = flag5;
				}
				flag2 = flag4;
			}
			if (flag2)
			{
				bool? operate = Operate;
				if (!operate.HasValue)
				{
					if (!(Role == "Administrator"))
					{
						return IsNamedManager;
					}
					return true;
				}
				return operate == true;
			}
			return false;
		}
	}

	public bool IsWarehouse
	{
		get
		{
			if (!(Role == "Warehouse"))
			{
				return Team == "WAREHOUSE";
			}
			return true;
		}
	}

	public bool CanReceiveWarehouse
	{
		get
		{
			if (!(Role == "Administrator"))
			{
				return IsWarehouse;
			}
			return true;
		}
	}

	public bool CanDeleteWorkshop
	{
		get
		{
			if (Role == "Administrator")
			{
				return true;
			}
			string login = Login.Trim().ToLowerInvariant();
			return login == "rosmarlin.vittini" || login == "rosmarlin.vittini@grupotejeda.local";
		}
	}

	public static bool WorkshopManagerLogin(string login)
	{
		switch (login.Trim().ToLowerInvariant())
		{
		case "rosmarlin.vittini":
		case "rosmarlin.vittini@grupotejeda.local":
		case "michael.miguel":
		case "michael.miguel@grupotejeda.local":
			return true;
		default:
			return false;
		}
	}

	public static WorkshopActor From(HttpContext c)
	{
		return new WorkshopActor(c.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ?? "", c.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "", c.User.FindFirstValue("support_team"), c.User.FindFirstValue("display_name") ?? "Usuario", c.User.Identity?.Name ?? "", ReadOperate(c.User));
	}

	private static bool? ReadOperate(ClaimsPrincipal user)
	{
		try
		{
			Dictionary<string, bool> dictionary = JsonSerializer.Deserialize<Dictionary<string, bool>>(user.FindFirstValue("permissions_json") ?? "{}");
			bool value;
			return (dictionary != null && dictionary.TryGetValue("canOperateWorkshop", out value)) ? new bool?(value) : ((bool?)null);
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
