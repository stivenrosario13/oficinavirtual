using System;
using System.Collections.Generic;

internal record AdminUserRow(Guid Id, string Email, string DisplayName, string Role, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt, string? Region, string? Contact, string? AvatarUrl, bool MustChangePassword, List<string> Groups, int AgencyCount, Dictionary<string, bool> Permissions, string? SupportTeam)
{
	public Dictionary<string, bool>? EffectivePermissions { get; init; }
}
