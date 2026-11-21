using System;
using System.Globalization;
using System.Linq;
using System.Text;

internal static class AdministrativeProfiles
{
	public static bool ApiAllowed(string path)
	{
		if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (new string[8] { "/api/admin/login", "/api/admin/logout", "/api/admin/session", "/api/admin/session/restore", "/api/version", "/api/health", "/api/license/status", "/api/live/stream" }.Contains(path, StringComparer.OrdinalIgnoreCase))
		{
			return true;
		}
		return new string[6] { "/api/account", "/api/institutional-documents", "/api/administrative-files", "/api/administrative-assets", "/api/warehouse/fulfillment", "/api/incident-ticket-report" }.Any((string prefix) => path.Equals(prefix, StringComparison.OrdinalIgnoreCase) || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
	}

	public static string Login(string login)
	{
		string text = login.Trim().ToLowerInvariant();
		int num = Math.Max(text.LastIndexOf('\\'), text.LastIndexOf('/'));
		if (num >= 0) text = text.Substring(num + 1);
		int num2 = text.IndexOf('@');
		return num2 >= 0 ? text.Substring(0, num2) : text;
	}

	private static string IdentityName(string value)
	{
		string text = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in text) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)) stringBuilder.Append(c);
		return stringBuilder.ToString();
	}

	public static bool IsIncidentOperator(string login, string? displayName = null)
	{
		string text = Login(login);
		string text2 = IdentityName(displayName ?? "");
		return text is "yesica.mercedes" or "yessica.mercedes" or "lucianny.gonzalez" or "lucianny.gonzales" || text2 is "yesicamercedes" or "yessicamercedes" or "luciannygonzalez" or "luciannygonzales";
	}

	public static bool CanManageIncidentReports(DocumentActor actor)
	{
		return actor.Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) || IsIncidentOperator(actor.Login, actor.Name);
	}

	public static string[]? Forms(string login)
	{
		switch (Login(login))
		{
		case "gledys.tejeda":
			return new string[17]
			{
				"SIM", "MARBETE", "DEVICE", "PAYMENT", "FLEET", "MODEM", "CONTRACT_REQUEST", "CONTRACT_RENEWAL", "RENTAL_CONTRACT", "RENT_RECEIPT",
				"INCREASE_LETTER", "FUEL_LOG", "GAS_LOG", "GENERAL", "LABOR_BENEFITS_REQUEST", "BLANK_CHECKS_DELIVERY", "SIGNED_BENEFITS_CHECKS_DELIVERY"
			};
		case "emma.mendoza":
			return new string[5] { "SIM", "MARBETE", "DEVICE", "PAYMENT", "FLEET" };
		case "derihan.alisha":
			return new string[2] { "DEVICE", "MODEM" };
		case "whilmaly.sulay":
			return new string[10] { "CONTRACT_REQUEST", "CONTRACT_RENEWAL", "RENTAL_CONTRACT", "RENT_RECEIPT", "INCREASE_LETTER", "GAS_LOG", "GENERAL", "LABOR_BENEFITS_REQUEST", "BLANK_CHECKS_DELIVERY", "SIGNED_BENEFITS_CHECKS_DELIVERY" };
		case "sandra.ramirez":
			return new string[1] { "FUEL_LOG" };
		case "diana.ventura":
		case "emely.figueroa":
			return new string[1] { "CONTRACT_REQUEST" };
		case "yessica.mercedes":
		case "yesica.mercedes":
		case "lucianny.gonzalez":
		case "lucianny.gonzales":
			return new string[1] { "INCIDENT_REPORT" };
		default:
			return null;
		}
	}

	public static bool CanRead(DocumentActor actor, AdministrativeRequestInfo request)
	{
		if (request.FormType?.Equals("INCIDENT_REPORT", StringComparison.OrdinalIgnoreCase) == true && CanManageIncidentReports(actor)) return true;
		if ((request.FormType is "LABOR_BENEFITS_REQUEST" or "BLANK_CHECKS_DELIVERY" or "SIGNED_BENEFITS_CHECKS_DELIVERY") && (actor.Department is "Administración" or "Recursos Humanos")) return true;
		string[] array = Forms(actor.Login);
		if (array == null)
		{
			return actor.Role != "Administrative";
		}
		string text = request.FormType?.ToUpperInvariant();
		if ((text == null || text == "GENERAL") ? true : false)
		{
			string assetType = request.AssetType;
			string text2;
			if (assetType == "FLOTA")
			{
				text2 = "FLEET";
			}
			else
			{
				text2 = ((!(assetType == "MODEM")) ? (text ?? "GENERAL") : "MODEM");
			}
			text = text2;
		}
		return Enumerable.Contains(array, text);
	}

	public static bool CanReadIncidentReports(DocumentActor actor)
	{
		return CanManageIncidentReports(actor);
	}

	public static bool CanDecideIncidentTickets(DocumentActor actor)
	{
		return actor.Role != "GroupAdministrator" && actor.Role != "GROUP_ADMINISTRATOR" && CanManageIncidentReports(actor);
	}
}
