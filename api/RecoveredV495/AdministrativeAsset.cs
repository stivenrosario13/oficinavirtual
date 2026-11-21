using System;

internal record AdministrativeAsset(Guid Id, string Kind, string Label, string Identifier, string Status, string? HolderType, string? HolderName, string? AgencyCode, string? Notes, DateTime CreatedUtc, DateTime UpdatedUtc, long? RequestId);
