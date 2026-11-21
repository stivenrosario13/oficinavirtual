internal static class AdministrativeTicketAccess
{
	public static bool ApiAllowed(string path, string method, bool manager = false)
	{
		bool flag = manager && DepartmentAccounts.ApiAllowed(path, method);
		bool flag2 = flag;
		if (!flag2)
		{
			bool flag3 = method == "GET";
			bool flag4 = flag3;
			if (flag4)
			{
				bool flag5 = ((path == "/api/support/catalog" || path == "/api/tickets/agencies") ? true : false);
				flag4 = flag5;
			}
			flag2 = flag4;
		}
		if (!flag2)
		{
			if (method == "POST")
			{
				return path == "/api/tickets";
			}
			return false;
		}
		return true;
	}
}
