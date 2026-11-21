using System;

internal record DocumentAccess(long Id, string? RecipientUserName, string? RecipientDepartment, bool CanEdit, DateTime SharedUtc);
