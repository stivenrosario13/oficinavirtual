using System.Collections.Generic;

internal record AdminUserCreate(string? Email, string? DisplayName, string? Role, string? Password, string? Region, string? Contact, List<string>? Groups, Dictionary<string, bool>? Permissions, string? SupportTeam);
