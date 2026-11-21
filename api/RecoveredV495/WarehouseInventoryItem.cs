using System.Collections.Generic;

internal record WarehouseInventoryItem(string Department, string ProductId, string Name, int Available, List<string> Serials, bool IsConsumable, string Warehouse, string UnitId, string Division, string Group, string? LastPurchaseDate, bool TracksSerial);
