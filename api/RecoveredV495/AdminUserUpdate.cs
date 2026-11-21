using System.Collections.Generic;

internal record AdminUserUpdate(string? Email, string? DisplayName, string? Role, bool IsActive, string? Password, string? Region, string? Contact, List<string>? Groups, Dictionary<string, bool>? Permissions, string? SupportTeam);
