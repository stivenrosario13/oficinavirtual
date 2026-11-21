using System.Collections.Generic;

internal record SupportDepartmentRow(string Code, string Name, string? Description, string AccentColor, int DisplayOrder, bool IsActive, List<SupportCategoryRow> Categories);
