using System;
using System.Collections.Generic;

internal record AccountContactProfileRow(Guid Id, string Email, string DisplayName, string Role, string? Contact, string? AvatarUrl, List<string> Groups);
