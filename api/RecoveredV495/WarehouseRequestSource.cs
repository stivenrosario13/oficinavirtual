internal static class WarehouseRequestSource
{
	public static DocumentActor? Resolve(DocumentActor actor, string requested)
	{
		if (requested == "")
		{
			return actor;
		}
		if (!DepartmentAccounts.Names.ContainsValue(requested))
		{
			return null;
		}
		if (actor.Manager || requested == actor.Department)
		{
			return actor with
			{
				AssignedDepartment = requested
			};
		}
		if (actor.Role == "Technology" && actor.Team != "WAREHOUSE")
		{
			string text = actor.Team switch
			{
				"WORKSHOP" => "Taller", 
				"CALL_CENTER" => "Call Center", 
				"TECHNICAL_FAILURE" => "Avería Técnica", 
				_ => null, 
			};
			if (requested == "Tecnología" || requested == text)
			{
				return actor with
				{
					AssignedDepartment = requested
				};
			}
		}
		return null;
	}

	public static WarehouseRequestInfo? RouteFailure(WarehouseRequestInfo request, DocumentActor actor, string sourceDepartment)
	{
		if (request.FailureCategory == null)
		{
			return request;
		}
		string failureDepartment = request.FailureDepartment;
		if ((!(failureDepartment == "TECHNOLOGY") && !(failureDepartment == "GENERAL_SERVICES")) || 1 == 0)
		{
			return null;
		}
		string text2;
		if (failureDepartment == "TECHNOLOGY")
		{
			string team = actor.Team;
			string text;
			if (team == "CALL_CENTER")
			{
				text = "CALL_CENTER";
			}
			else
			{
				text = ((!(team == "TECHNICAL_FAILURE")) ? "TECHNICAL_FAILURE" : "TECHNICAL_FAILURE");
			}
			text2 = text;
		}
		else
		{
			text2 = null;
		}
		string failureTeam = text2;
		return request with
		{
			FailureTeam = failureTeam
		};
	}
}
