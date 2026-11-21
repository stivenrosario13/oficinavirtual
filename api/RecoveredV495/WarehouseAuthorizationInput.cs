using System.Collections.Generic;

internal record WarehouseAuthorizationInput(string Note, List<WarehouseAssignedEquipment> Equipment);
