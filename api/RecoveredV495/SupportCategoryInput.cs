using System.Collections.Generic;

internal record SupportCategoryInput(string? DepartmentCode, string? Code, string? Name, string? Description, string? DefaultPriority, bool RequiresDetail, string? DetailLabel, List<string>? Options, int DisplayOrder, bool IsActive);
