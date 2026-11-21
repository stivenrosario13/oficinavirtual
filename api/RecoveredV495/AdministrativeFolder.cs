using System;
using System.Collections.Generic;

internal record AdministrativeFolder(Guid Id, string Name, Guid? ParentId, string Department, bool IsShared, string CreatedBy, DateTime CreatedAt)
{
	public List<string> SharedUserIds { get; init; } = new List<string>();

	public bool CanManage { get; init; }

	public bool CanWrite { get; init; }
}
