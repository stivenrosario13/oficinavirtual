using System;

internal sealed class WarehouseSubmissionConflictException : Exception
{
	public WarehouseSubmissionConflictException()
		: base("Este número de solicitud ya se guardó con otros datos. Consulta la solicitud existente antes de crear una nueva.")
	{
	}
}
