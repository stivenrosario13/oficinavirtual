using System.Security.Claims;
using System.Text.Json;

static class WarehousePermission
{
    public static bool Has(ClaimsPrincipal user,string key)
    {
        if(user.Identity?.IsAuthenticated!=true)return false;
        var raw=user.FindFirst("permissions_json")?.Value;
        if(!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var json=JsonDocument.Parse(raw);
                if(json.RootElement.ValueKind!=JsonValueKind.Object)return false;
                if(json.RootElement.TryGetProperty(key,out var value))return value.ValueKind==JsonValueKind.True;
            }
            catch(JsonException){return false;}
        }
        // Match the session's administrator defaults for legacy cookies.
        // Explicit false always takes precedence over a role default.
        return user.IsInRole("Administrator")&&key is "canAuthorizeWarehouseRequests" or "canLoadWarehouseInventory";
    }
}
