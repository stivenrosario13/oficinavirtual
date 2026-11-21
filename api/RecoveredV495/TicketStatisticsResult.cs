using System;
using System.Collections.Generic;

internal record TicketStatisticsResult(DateTime From, DateTime To, int Active, int Resolved, List<TicketTechnicianStatistic> ByTechnician, List<TicketCategoryStatistic> ByCategory, List<TicketDepartmentStatistic> ByDepartment, List<TicketHistoryPoint> History);
