using System;
using System.Collections.Generic;

internal record AdministrativeDeliveryInput(Guid? AssetId, string ReceivedBy, List<Guid>? AssetIds = null, string? SealQr = null);
