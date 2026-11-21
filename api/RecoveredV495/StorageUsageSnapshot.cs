using System;
using System.Text.Json.Nodes;

internal record StorageUsageSnapshot(JsonObject? Provider, DateTime? CheckedAt, string? Error, DateTime? NextCheckAt);
