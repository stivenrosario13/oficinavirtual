using System;

internal record InternetRequestInfo(Guid AgencyId, string AgencyCode, string AgencyName, string AgencyGroup, string ServiceKind, string ServiceNumber, string Company, int ModemQuantity, string ItemKind, string ChangeReason, Guid? TicketId = null, long? TicketNumber = null);
