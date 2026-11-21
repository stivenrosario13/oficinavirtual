using System.Collections.Generic;

internal record SupportTechnicianRow(string Id, string Name, string Department, string Role, List<string> Groups, bool IsAgencySupervisor, string? SupportTeam, bool IsTechnician)
{
	public SupportTechnicianRow(string id, string name, string department)
		: this(id, name, department, department, new List<string>(), IsAgencySupervisor: false, null, IsTechnician: true)
	{
	}
}
