using System.Collections.Generic;

internal record WarehouseRequestEdit(int Revision, string Note, List<WarehouseRequestLine> Lines);
