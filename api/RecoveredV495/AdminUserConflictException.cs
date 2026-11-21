using System;

internal sealed class AdminUserConflictException : Exception
{
	public AdminUserConflictException(Guid id)
		: base($"Cuenta activa duplicada: {id}")
	{
	}
}
