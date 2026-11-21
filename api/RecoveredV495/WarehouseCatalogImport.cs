using System.Collections.Generic;

internal record WarehouseCatalogImport(string Version, string? SourceFile, List<WarehouseCatalogProduct> Products);
