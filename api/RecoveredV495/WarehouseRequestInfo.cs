using System;
using System.Collections.Generic;

internal record WarehouseRequestInfo(string Number, Guid AgencyId, string AgencyCode, string AgencyName, string AgencyGroup, string InstallerId, string InstallerName, string Priority, List<WarehouseRequestLine> Lines, Guid? TicketId = null, long? TicketNumber = null, string? DestinationType = null, string? FailureDepartment = null, string? FailureCategory = null, string? FailureDescription = null, string? FailureTeam = null, WeekendLoanPeriod? WeekendLoan = null);
