using System;
using System.Collections.Generic;

internal record AdministrativeDeliveryInfo(string Number, string DeliveredBy, string DeliveredLogin, string ReceivedBy, DateTime At, Guid? AssetId, string? AssetIdentifier, List<string>? AssetIdentifiers = null);
