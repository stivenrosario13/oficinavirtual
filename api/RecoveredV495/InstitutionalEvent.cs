using System;

internal record InstitutionalEvent(string Action, string Actor, string Login, DateTime At, string PreviousStatus, string? ReceiptCode, string? ReceiverLogin, DateTime? ReceivedUtc, string? PreviousReceiptSignature, AdministrativeRequestInfo? PreviousAdministrativeRequest = null, WarehouseRequestInfo? PreviousWarehouseRequest = null, string? Note = null);
