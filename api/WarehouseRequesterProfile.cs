using System.Security.Claims;
using System.Text.Json;

static class WarehouseRequesterProfile
{
    public const string Marker="profile:WAREHOUSE_REQUESTER";
    public static bool IsRequester(ClaimsPrincipal user){try{var saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.FindFirstValue("permissions_json")??"{}");return saved?.GetValueOrDefault(Marker)==true&&saved.GetValueOrDefault("usesCustomizedPermissions")!=true;}catch(JsonException){return false;}}
    public static void Apply(Dictionary<string,bool> effective,Dictionary<string,bool>? saved)
    {
        if(saved?.GetValueOrDefault(Marker)!=true||saved.GetValueOrDefault("usesCustomizedPermissions"))return;
        var allowed=new[]{"canView","canConfigureProfile","canRequestWarehouse","canViewDocuments"};
        foreach(var key in effective.Keys.ToArray())effective[key]=allowed.Contains(key);
        foreach(var key in allowed)effective[key]=true;
        effective[Marker]=true;effective[ExplicitUserPermissions.Marker]=true;
    }
    public static bool ApiAllowed(string path,string method)
    {
        path=path.TrimEnd('/').ToLowerInvariant();
        if(!path.StartsWith("/api/"))return true;
        var read=method is "GET" or "HEAD";
        if(read&&new[]{"/api/admin/session","/api/license/status","/api/version","/api/health","/api/warehouse/fulfillment/inventory","/api/warehouse/fulfillment/track","/api/tickets/agencies","/api/support/catalog","/api/institutional-documents/warehouse-groups","/api/live/stream"}.Contains(path))return true;
        if(method=="POST"&&new[]{"/api/admin/logout","/api/admin/session/restore","/api/institutional-documents"}.Contains(path))return true;
        if(path=="/api/account/avatar"&&method=="POST")return true;
        if(method=="POST"&&path is "/api/account/recovery-code" or "/api/account/password/recover")return true;
        if(read&&path.StartsWith("/api/account/avatar/")&&Guid.TryParse(path[20..],out _))return true;
        if(path=="/api/account"&&method is "GET" or "PUT"||path=="/api/account/password"&&method=="PUT")return true;
        if(read&&path=="/api/institutional-documents")return true;
        if(read&&path.StartsWith("/api/institutional-documents/warehouse-submission/"))return true;
        var parts=path.Split('/',StringSplitOptions.RemoveEmptyEntries);
        return read&&parts.Length is 3 or 4&&parts[0]=="api"&&parts[1]=="institutional-documents"&&long.TryParse(parts[2],out _)&&(parts.Length==3||parts[3] is "preview" or "file" or "download");
    }
}
