using System.Security.Claims;

static class TemporaryPasswordGate
{
    public static async Task Invoke(HttpContext context,Func<Task> next)
    {
        var path=context.Request.Path.Value??"";
        // License status is installation-wide and must remain readable before
        // the account completes its mandatory first-password change.
        var allowed=path is "/api/license/status" or "/api/version" or "/api/admin/session" or "/api/admin/logout" or "/api/account/password" or "/api/account/avatar"||path.StartsWith("/api/account/avatar/",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/api/push/",StringComparison.OrdinalIgnoreCase);
        if(context.User.Identity?.IsAuthenticated==true&&context.User.FindFirstValue("must_change_password")=="true"&&path.StartsWith("/api/",StringComparison.OrdinalIgnoreCase)&&!allowed){
            context.Response.StatusCode=403;
            await context.Response.WriteAsJsonAsync(new{error="Debes cambiar tu contraseña temporal antes de continuar.",code="PASSWORD_CHANGE_REQUIRED"});
            return;
        }
        await next();
    }
}
