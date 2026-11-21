using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

internal static class ApiCookieEvents
{
	public static Task AccessDenied(RedirectContext<CookieAuthenticationOptions> context)
	{
		context.Response.StatusCode = 403;
		context.Response.Headers.CacheControl = "no-store";
		return context.Response.WriteAsJsonAsync(new
		{
			error = "Tu cuenta no tiene permiso para esta operación.",
			code = "ACCESS_DENIED"
		});
	}
}
