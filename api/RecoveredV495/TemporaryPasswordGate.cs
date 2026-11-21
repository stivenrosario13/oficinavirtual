using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

internal static class TemporaryPasswordGate
{
	public static async Task Invoke(HttpContext context, Func<Task> next)
	{
		string text = context.Request.Path.Value ?? "";
		bool flag;
		switch (text)
		{
		case "/api/license/status":
		case "/api/version":
		case "/api/admin/session":
		case "/api/admin/logout":
		case "/api/account/password":
		case "/api/account/avatar":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		bool flag2 = flag || text.StartsWith("/api/account/avatar/", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/api/push/", StringComparison.OrdinalIgnoreCase);
		if (!(context.User.Identity?.IsAuthenticated ?? false) || !(context.User.FindFirstValue("must_change_password") == "true") || !text.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || flag2)
		{
			await next();
			return;
		}
		context.Response.StatusCode = 403;
		await context.Response.WriteAsJsonAsync(new
		{
			error = "Debes cambiar tu contraseña temporal antes de continuar.",
			code = "PASSWORD_CHANGE_REQUIRED"
		});
	}
}
