using System;
using System.Collections.Generic;

internal record AdministrativeFolderInput(string? Name, Guid? ParentId, List<string>? UserIds = null);
