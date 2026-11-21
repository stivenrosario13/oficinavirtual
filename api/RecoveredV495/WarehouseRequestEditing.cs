internal static class WarehouseRequestEditing
{
	public static bool Editor(DocumentActor actor)
	{
		bool flag = !actor.WarehouseRequestOnly;
		bool flag2 = flag;
		if (flag2)
		{
			string text = AdministrativeProfiles.Login(actor.Login);
			bool flag3 = ((text == "diorly.abreu" || text == "ana.rodriguez") ? true : false);
			flag2 = flag3;
		}
		return flag2;
	}

	public static bool CanEdit(InstitutionalFile d, DocumentActor actor)
	{
		bool flag = Editor(actor) && !d.Trashed && (object)d.WarehouseRequest != null && d.RecipientDepartment == "Almacén" && (object)d.WarehouseDelivery == null && d.WarehouseAssigned.Count == 0;
		bool flag2 = flag;
		if (flag2)
		{
			bool flag3;
			switch (d.Status)
			{
			case "pending":
			case "received":
			case "correction_requested":
			case "rejected":
			case "approved":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag2 = flag3;
		}
		return flag2;
	}
}
