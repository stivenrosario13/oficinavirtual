using System;
using System.Collections.Generic;

internal record ExecutiveReportResult(string Period, DateTime FromUtc, DateTime ToUtc, DateTime GeneratedUtc, List<ExecutiveCaseRow> Tickets, List<ExecutiveCaseRow> Findings, List<ExecutiveFindingPersonRow> FindingPeople, List<ExecutiveWarehouseRow> Warehouse, List<ExecutiveWorkshopRow> Workshop, List<ExecutiveAuditGroup> AuditGroups, List<ExecutiveAuditAgency> Agencies, List<IncidentTicketReportRow> IncidentTickets);
