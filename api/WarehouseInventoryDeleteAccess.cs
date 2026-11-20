static class WarehouseInventoryDeleteAccess
{
    public static bool Allowed(string role,string team,string login) =>
        (role=="Warehouse"||team=="WAREHOUSE") &&
        (string.Equals(login.Trim(),"diorly.abreu@grupotejeda.local",StringComparison.OrdinalIgnoreCase)||
         string.Equals(login.Trim(),"bryan.arias@grupotejeda.local",StringComparison.OrdinalIgnoreCase));
}
