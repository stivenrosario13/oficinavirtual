using System.Security.Claims;
using System.Text.Json;

static class OperationalPermissions
{
    public static readonly string[] Keys=["canViewDocuments","canUploadDocuments","canManageDocumentFolders","canShareDocuments","canDeleteDocuments","canReceiveDocuments","canSignDocuments","canDecideDocuments","canManageDocumentTemplates","canRequestWarehouse","canViewWarehouseInventory","canCreateWarehouseArticles","canPrepareWarehouseRequests","canWithdrawWarehouseRequests","canCancelWarehouseRequests","canViewWarehouseReports","canViewWorkshop","canOperateWorkshop","canImportWorkshopInventory","canViewMaintenance","canCreateMaintenanceMovements","canManageProcurement","canViewAgencyTransitions","canManageAgencyTransitions","canManageAdministrativeAssets"];
    static readonly string[] IncidentDocumentPermissions=["canViewDocuments","canReceiveDocuments","canSignDocuments","canDecideDocuments"];
    static bool IncidentOperator(string login,string? displayName=null)=>AdministrativeProfiles.IsIncidentOperator(login,displayName);

    public static Dictionary<string,bool> Defaults(string role,string? team)
    {
        var admin=role=="Administrator";var warehouse=role=="Warehouse"||team=="WAREHOUSE";
        var documents=admin||warehouse||role is "Department" or "Administrative" or "GroupAdministrator" or "Fiscalizador" or "Viewer" or "Technology" or "GeneralServices" or "HumanResources";
        var maintenance=admin||warehouse||role is "Technology" or "GeneralServices" or "HumanResources";
        var workshop=admin||role=="Technology"&&team is "WORKSHOP" or "TECHNICAL_FAILURE";
        return Keys.Append("canDeleteWarehouseInventory").ToDictionary(key=>key,key=>key switch {
            "canDeleteWarehouseInventory"=>warehouse,
            "canRequestWarehouse"=>documents,
            "canViewWarehouseInventory" or "canCancelWarehouseRequests" or "canViewWarehouseReports"=>documents,
            "canCreateWarehouseArticles"=>admin||warehouse,
            "canPrepareWarehouseRequests" or "canWithdrawWarehouseRequests"=>admin||warehouse,
            "canViewWorkshop"=>maintenance,
            "canOperateWorkshop" or "canImportWorkshopInventory"=>workshop,
            "canViewMaintenance" or "canCreateMaintenanceMovements" or "canManageProcurement"=>maintenance,
            "canViewAgencyTransitions" or "canManageAgencyTransitions"=>admin||role is "Technology" or "GeneralServices" or "HumanResources",
            "canManageAdministrativeAssets"=>admin||role=="Administrative"||role=="Department",
            _=>documents
        });
    }
    public static void Apply(Dictionary<string,bool> effective,Dictionary<string,bool>? saved,string role,string? team,string login="")
    {
        foreach(var (key,allowed) in Defaults(role,team))effective[key]=allowed&&(saved?.GetValueOrDefault(key,true)??true);
        // Requesting supplies is available to every operational account, including
        // accounts whose saved permissions predate the shared request workflow.
        effective["canRequestWarehouse"]=Defaults(role,team)["canRequestWarehouse"];
        // Every account that can use the document workspace may share its own
        // files and folders. Ownership checks remain enforced by the stores.
        effective["canShareDocuments"]=Defaults(role,team)["canShareDocuments"];
        // QR tracking and pickup belong to the warehouse role, including older
        // accounts with a saved false from before the shared QR workflow.
        if(role=="Warehouse"||team=="WAREHOUSE")
            foreach(var key in new[]{"canViewWorkshop","canViewMaintenance","canWithdrawWarehouseRequests"})effective[key]=true;
        if(saved?.ContainsKey("canDeleteWarehouseInventory")!=true)effective["canDeleteWarehouseInventory"]=WarehouseInventoryDeleteAccess.Allowed(role,team??"",login);
        if(saved?.ContainsKey("canCreateWarehouseArticles")!=true)effective["canCreateWarehouseArticles"]=role=="Administrator"||WarehouseArticleCreationAccess.Named(login);
        if(saved?.ContainsKey("canOperateWorkshop")!=true)effective["canOperateWorkshop"]=role=="Administrator"||role=="Technology"&&team=="TECHNICAL_FAILURE"&&new[]{"rosmarlin.vittini","rosmarlin.vittini@grupotejeda.local","michael.miguel","michael.miguel@grupotejeda.local"}.Contains(login.Trim().ToLowerInvariant());
        // Incident operators keep these four capabilities even when their account
        // has an older technical/call-center role or saved document permissions.
        if(IncidentOperator(login))foreach(var key in IncidentDocumentPermissions)effective[key]=true;
    }
    public static bool Allowed(ClaimsPrincipal user,string key)
    {
        if(user.Identity?.IsAuthenticated!=true)return false;
        if(IncidentOperator(user.Identity.Name??"",user.FindFirstValue("display_name"))&&IncidentDocumentPermissions.Contains(key))return true;
        Dictionary<string,bool>? explicitPermissions=null;
        try{explicitPermissions=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.FindFirstValue("permissions_json")??"{}");}
        catch(JsonException){return false;}
        if(explicitPermissions?.GetValueOrDefault(ExplicitUserPermissions.Marker)==true&&explicitPermissions.TryGetValue(key,out var explicitValue))return explicitValue;
        var defaults=Defaults(user.FindFirstValue(ClaimTypes.Role)??"",user.FindFirstValue("support_team"));
        if(!defaults.GetValueOrDefault(key))return false;
        if(key=="canCreateWarehouseArticles"){
            try{
                var saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.FindFirstValue("permissions_json")??"{}");
                if(saved?.TryGetValue(key,out var granted)==true)return granted;
                return user.IsInRole("Administrator")||WarehouseArticleCreationAccess.Named(user.Identity.Name??"");
            }catch(JsonException){return false;}
        }
        if(key is "canRequestWarehouse" or "canShareDocuments"||((key is "canViewWorkshop" or "canViewMaintenance" or "canWithdrawWarehouseRequests")&&(user.IsInRole("Warehouse")||user.FindFirstValue("support_team")=="WAREHOUSE")))return true;
        try{var saved=JsonSerializer.Deserialize<Dictionary<string,bool>>(user.FindFirstValue("permissions_json")??"{}");return saved?.GetValueOrDefault(key,true)??true;}
        catch(JsonException){return false;}
    }
    public static string? Required(string path,string method)
    {
        path=path.TrimEnd('/').ToLowerInvariant();var read=method is "GET" or "HEAD";var action=path.Split('/').Last();
        if(path=="/api/warehouse/fulfillment/inventory")return "canRequestWarehouse";
        if(path.StartsWith("/api/warehouse/fulfillment/"))return action switch{
            "remove"=>"canDeleteWarehouseInventory",
            "prepare" or "pending-purchase"=>"canPrepareWarehouseRequests",
            "withdraw"=>"canWithdrawWarehouseRequests",
            "analytics" or "frequency" or "track"=>"canViewWarehouseReports",_=>null};
        if(path=="/api/maintenance/department-requests")return "canRequestWarehouse";
        // The receipt is a warehouse action. Workshop's own endpoint checks the
        // authenticated warehouse role and the pending delivery state.
        if(method=="POST"&&path.StartsWith("/api/workshop/")&&path.EndsWith("/warehouse-receipt"))return null;
        if(path.StartsWith("/api/workshop"))return read?"canViewWorkshop":path.EndsWith("/fiscalized")?"canImportWorkshopInventory":"canOperateWorkshop";
        if(path.StartsWith("/api/agency-transitions"))return read?"canViewAgencyTransitions":"canManageAgencyTransitions";
        if(path.StartsWith("/api/maintenance/"))return read?"canViewMaintenance":path.EndsWith("/document")?null:path.Contains("/movements")?"canCreateMaintenanceMovements":"canManageProcurement";
        if(path.StartsWith("/api/administrative-assets"))return "canManageAdministrativeAssets";
        if(path.StartsWith("/api/administrative-files"))return read?"canViewDocuments":action=="shared"?"canShareDocuments":"canManageDocumentFolders";
        if(path.StartsWith("/api/institutional-documents")){
            // Confirmation is restricted by the store to the authenticated owner.
            // A requester need not have general document browsing permission.
            if(read&&path.StartsWith("/api/institutional-documents/warehouse-submission/"))return "canRequestWarehouse";
            if(read&&path=="/api/institutional-documents/warehouse-groups")return "canRequestWarehouse";
            if(read)return "canViewDocuments";
            // The multipart endpoint selects the permission after reading the form's purpose.
            if(path=="/api/institutional-documents"&&method=="POST")return null;
            if(path.Contains("/templates"))return "canManageDocumentTemplates";
            if(path.EndsWith("/incident-tickets/decision"))return "canDecideDocuments";
            return action switch{
                "share" or "unshare"=>"canShareDocuments",
                "trash" or "purge" or "restore"=>"canDeleteDocuments",
                "receive"=>"canReceiveDocuments",
                "sign" or "move"=>"canSignDocuments",
                "approve" or "reject" or "correction"=>"canDecideDocuments",
                "cancel"=>"canCancelWarehouseRequests",
                _=>"canUploadDocuments"
            };
        }
        return null;
    }
}
