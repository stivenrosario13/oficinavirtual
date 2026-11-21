using System.Text.Json;

internal record AdministrativeRequestInfo(string Number, string AssetType, string TargetType, string TargetName, string? AgencyCode, string? Reason, InternetRequestInfo? Internet = null, string? FormType = null, JsonElement? FormData = null);
