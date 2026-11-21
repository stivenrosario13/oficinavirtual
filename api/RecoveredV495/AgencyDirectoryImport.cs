using System.Collections.Generic;

internal record AgencyDirectoryImport(string? FileName, List<AgencyDirectoryImportRow>? Agencies);
