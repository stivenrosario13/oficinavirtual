using System.Collections.Generic;

internal record WarehousePurchasePendingInput(string Note, List<int> LineIndexes, List<WarehouseAssignedEquipment> Equipment);
