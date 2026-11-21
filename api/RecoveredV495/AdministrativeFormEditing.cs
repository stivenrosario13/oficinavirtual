internal static class AdministrativeFormEditing
{
	public static bool CanEdit(InstitutionalFile document, DocumentActor actor)
	{
		bool flag = document.Accessible(actor) && !document.Trashed && document.Area == "communications" && document.RecipientDepartment == "Administración" && (object)document.AdministrativeRequest != null && (document.AdministrativeRequest.AssetType != "INTERNET" || actor.Role == "Administrator" || (actor.Role == "Technology" && actor.Team == "TECHNICAL_FAILURE"));
		bool flag2 = flag;
		if (flag2)
		{
			bool administrativeRegistrationOnly = document.AdministrativeRegistrationOnly;
			bool flag3 = administrativeRegistrationOnly;
			if (!flag3)
			{
				bool flag4 = (object)document.AdministrativeDelivery == null;
				bool flag5 = flag4;
				if (flag5)
				{
					bool flag6;
					switch (document.Status)
					{
					case "pending":
					case "received":
					case "correction_requested":
					case "rejected":
						flag6 = true;
						break;
					default:
						flag6 = false;
						break;
					}
					flag5 = flag6;
				}
				flag3 = flag5;
			}
			flag2 = flag3;
		}
		if (flag2)
		{
			if (!(document.OwnerUserName == actor.Id))
			{
				if (actor.Department == "Administración")
				{
					if (!actor.Manager)
					{
						return actor.Role == "Administrative";
					}
					return true;
				}
				return false;
			}
			return true;
		}
		return false;
	}
}
