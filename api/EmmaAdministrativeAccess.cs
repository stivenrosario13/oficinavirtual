using System.Security.Claims;

// Effective account scope: keeps the stored password and all other accounts unchanged.
static class EmmaAdministrativeAccess
{
    public static bool IsEmma(string? login) => AdministrativeProfiles.Login(login ?? "") == "emma.mendoza";
    public static bool Allowed(ClaimsPrincipal user) => user.IsInRole("Administrative") && IsEmma(user.Identity?.Name);
    public static AdminIdentity Scope(AdminIdentity account) => IsEmma(account.Email)
        ? account with { Role = "Administrative", SupportTeam = null, Permissions = new() {
            ["canView"] = true, ["canConfigureProfile"] = true, ["canCreateTickets"] = true
        }} : account;
    public static bool ApiAllowed(ClaimsPrincipal user, string path, string method) => Allowed(user) &&
        ((method == "GET" && new[] { "/api/support/catalog", "/api/tickets", "/api/tickets/agencies", "/api/department-audit" }.Contains(path, StringComparer.OrdinalIgnoreCase)) ||
         (method == "POST" && path.Equals("/api/tickets", StringComparison.OrdinalIgnoreCase)));
}
