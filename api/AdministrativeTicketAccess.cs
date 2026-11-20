static class AdministrativeTicketAccess
{
    public static bool ApiAllowed(string path,string method,bool manager=false) =>
        manager&&DepartmentAccounts.ApiAllowed(path,method)
        ||
        method=="GET"&&(path is "/api/support/catalog" or "/api/tickets/agencies")
        || method=="POST"&&path=="/api/tickets";
}
