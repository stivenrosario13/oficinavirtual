using System;

internal record WarehouseTraceEvent(string Id, string Action, string Status, string Note, string ActorName, string ActorLogin, DateTime OccurredAt);
