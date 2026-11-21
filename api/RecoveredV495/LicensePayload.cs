using System;

internal sealed record LicensePayload(string LicenseKey, string InstallationId, string Domain, string Status, string? Message, DateTimeOffset? ExpiresAt, DateTimeOffset CheckedAt);
