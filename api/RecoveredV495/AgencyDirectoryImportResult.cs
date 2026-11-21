using System.Collections.Generic;

internal record AgencyDirectoryImportResult(int Total, int Created, int Updated, int Skipped, List<string> Duplicates);
