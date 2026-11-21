using System;

internal record WorkshopStockExitRequest(Guid AgencyId, string Department, string Category, string Description);
