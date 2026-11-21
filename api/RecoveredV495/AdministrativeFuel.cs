using System.Text.Json;

internal static class AdministrativeFuel
{
	public static bool IsSandra(string login)
	{
		return AdministrativeProfiles.Login(login) == "sandra.ramirez";
	}

	public static bool IsSulay(string login)
	{
		return AdministrativeProfiles.Login(login) == "whilmaly.sulay";
	}

	public static void Normalize(InstitutionalFile document)
	{
		AdministrativeRequestInfo administrativeRequest = document.AdministrativeRequest;
		if ((object)administrativeRequest != null && administrativeRequest.FormType == "FUEL_LOG" && IsSulay(document.OwnerLogin))
		{
			document.AdministrativeRequest = administrativeRequest with
			{
				FormType = "GAS_LOG"
			};
		}
		if (document.AdministrativeRegistrationOnly)
		{
			document.Status = "registered";
			JsonElement? jsonElement = document.AdministrativeRequest?.FormData;
			if (jsonElement.HasValue && jsonElement.GetValueOrDefault().TryGetProperty("importKey", out var _))
			{
				document.Title = document.Title.Replace("Relación de combustible", "Gasolina · gasto diario");
				document.Description = document.Description.Replace(" · Pendiente de revisión", "");
			}
		}
	}

	public static bool BlocksAction(InstitutionalFile document, string action)
	{
		bool administrativeRegistrationOnly = document.AdministrativeRegistrationOnly;
		bool flag = administrativeRegistrationOnly;
		if (flag)
		{
			bool flag2;
			switch (action)
			{
			case "approve":
			case "receive":
			case "reject":
			case "cancel":
			case "resend":
			case "send":
			case "sign":
			case "move":
			case "correction":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		return flag;
	}
}
