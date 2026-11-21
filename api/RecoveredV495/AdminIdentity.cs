using System.Collections.Generic;

internal record AdminIdentity(string Id, string Email, string DisplayName, string Role, bool MustChangePassword, string? AvatarUrl, Dictionary<string, bool> Permissions, string? SupportTeam)
{
	public string RecoveryVersion { get; init; } = "";
}
