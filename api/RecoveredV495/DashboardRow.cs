using System.Collections.Generic;

internal record DashboardRow(int Total, int Completed, int Pending, int Review, int Percent, int GroupsTotal, int GroupsReady, int AuditsTotal, List<DashboardGroup> Groups, List<DashboardAgency> Agencies, List<DashboardAdministrator> Administrators, List<CompletedAudit> Audits);
