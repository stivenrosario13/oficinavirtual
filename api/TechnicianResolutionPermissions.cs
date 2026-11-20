static class TechnicianResolutionPermissions
{
    public const string Marker="profile:TECHNICIAN";
    public static bool IsTechnician(string role,string? team,IReadOnlyDictionary<string,bool>? permissions)
    {
        role=role.ToUpperInvariant();team=team?.ToUpperInvariant();
        if(role is not ("TECHNOLOGY" or "GENERAL_SERVICES" or "GENERALSERVICES" or "HUMAN_RESOURCES" or "HUMANRESOURCES" or "DEPARTMENT"))return false;
        if(permissions?.GetValueOrDefault("profile:WAREHOUSE_REQUESTER")==true||team=="WAREHOUSE")return false;
        if(permissions?.GetValueOrDefault(Marker)==true)return true;
        if(permissions?.Any(item=>item.Key.StartsWith("profile:")&&item.Value)==true||permissions?.GetValueOrDefault("canManageDepartment")==true)return false;
        if(role=="TECHNOLOGY")return team=="TECHNICIANS";
        if(role is "GENERAL_SERVICES" or "GENERALSERVICES" or "HUMAN_RESOURCES" or "HUMANRESOURCES")
            return team=="WORKSHOP"||permissions?.TryGetValue("canAssignTickets",out var assign)==true&&!assign;
        return false;
    }
    // Resolution is a required capability of the technician profile. The ticket
    // endpoint still verifies assignment, department, status and evidence.
    public static void Apply(Dictionary<string,bool> effective,IReadOnlyDictionary<string,bool>? saved,string role,string? team)
    {
        // Keep explicit profile identity across authentication and claim refresh;
        // otherwise a restricted support account could resemble a legacy technician.
        if(saved is not null)foreach(var item in saved.Where(item=>item.Key.StartsWith("profile:")&&item.Value))effective[item.Key]=true;
        if(saved?.GetValueOrDefault("usesCustomizedPermissions")==true)return;
        if(!IsTechnician(role,team,saved))return;
        effective["canResolveTickets"]=true;
        effective["canViewSupportDashboard"]=true;
        effective[Marker]=true; // Preserve recognition in existing signed-in sessions.
    }
}
