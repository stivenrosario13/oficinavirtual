using System;

internal sealed record LicenseDecision(bool AllowsUse, string Status, string Code, string Message, DateTimeOffset? ExpiresAt, DateTimeOffset CheckedAt, bool UsingGracePeriod, bool SignatureVerified)
{
	public static LicenseDecision Development()
	{
		return new LicenseDecision(AllowsUse: true, "DISABLED", "LICENSE_DISABLED", "Control de licencia desactivado en esta instalación.", null, DateTimeOffset.UtcNow, UsingGracePeriod: false, SignatureVerified: false);
	}

	public static LicenseDecision ConfigurationError(string message)
	{
		return new LicenseDecision(AllowsUse: false, "CONFIGURATION_ERROR", "LICENSE_CONFIG", message, null, DateTimeOffset.UtcNow, UsingGracePeriod: false, SignatureVerified: false);
	}
}
