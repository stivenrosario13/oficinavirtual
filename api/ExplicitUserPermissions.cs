static class ExplicitUserPermissions
{
    public const string Marker="usesExplicitPermissions";
    // Opt-in only: existing accounts keep their current effective permissions.
    // Once the administrator customizes an account, both grants and denials are authoritative.
    public static void Apply(Dictionary<string,bool> effective,Dictionary<string,bool>? saved,string role)
    {
        // Profile identity selects the workspace; it grants no additional API permissions.
        if(role=="Department")
            foreach(var key in new[]{"profile:ADMINISTRATIVE","profile:DEPARTMENT_ADMINISTRATIVE"})
                if(saved?.GetValueOrDefault(key)==true)effective[key]=true;
        if(saved?.GetValueOrDefault(Marker)!=true)return;
        foreach(var key in effective.Keys.ToArray())
            if(saved.TryGetValue(key,out var enabled)&&key.StartsWith("can"))effective[key]=enabled;
        if(role=="Administrator")effective["canManageUsers"]=true;
        effective["canManage"]=effective.GetValueOrDefault("canManageUsers");
        effective[Marker]=true;
    }
}
