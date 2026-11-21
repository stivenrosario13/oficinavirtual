using System.Collections.Generic;

internal record SupportContactRow(string Id, string DisplayName, string Username, string Role, string? Contact, string? AvatarUrl, List<string> Groups);
