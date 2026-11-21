using System;

internal record InstitutionalTemplate(long Id, string Name, string Content, string Department, string OwnerUserName, bool IsSystem, DateTime UpdatedUtc);
