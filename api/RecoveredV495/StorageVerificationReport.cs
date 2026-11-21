using System;
using System.Collections.Generic;
using System.Linq;

internal record StorageVerificationReport(DateTime CheckedAt, IReadOnlyList<StorageCheck> Checks)
{
	public bool Passed
	{
		get
		{
			if (Checks.Count == 3)
			{
				return Checks.All((StorageCheck x) => x.Passed);
			}
			return false;
		}
	}
}
