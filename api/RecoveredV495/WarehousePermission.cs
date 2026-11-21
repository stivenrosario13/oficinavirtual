using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;

internal static class WarehousePermission
{
	public static bool Has(ClaimsPrincipal user, string key)
	{
		IIdentity? identity = user.Identity;
		if (identity == null || !identity.IsAuthenticated)
		{
			return false;
		}
		string text = user.FindFirst("permissions_json")?.Value;
		if (!string.IsNullOrWhiteSpace(text))
		{
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(text);
				if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
				{
					return false;
				}
				if (jsonDocument.RootElement.TryGetProperty(key, out var value))
				{
					return value.ValueKind == JsonValueKind.True;
				}
			}
			catch (JsonException)
			{
				return false;
			}
		}
		bool flag = user.IsInRole("Administrator");
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3 = ((key == "canAuthorizeWarehouseRequests" || key == "canLoadWarehouseInventory") ? true : false);
			flag2 = flag3;
		}
		return flag2;
	}
}
