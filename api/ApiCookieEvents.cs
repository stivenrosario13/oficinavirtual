using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

static class ApiCookieEvents
{
    public static Task AccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode=StatusCodes.Status403Forbidden;
        context.Response.Headers.CacheControl="no-store";
        return context.Response.WriteAsJsonAsync(new{error="Tu cuenta no tiene permiso para esta operación.",code="ACCESS_DENIED"});
    }
}
