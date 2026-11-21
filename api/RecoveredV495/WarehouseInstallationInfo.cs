using System;
using System.Collections.Generic;

internal record WarehouseInstallationInfo(Guid TicketId, long TicketNumber, string Status, string Resolution, string ResolvedBy, DateTime ResolvedAt, string AgencyCode, string AgencyName, List<WarehouseInstallationEvidence> Evidence);
