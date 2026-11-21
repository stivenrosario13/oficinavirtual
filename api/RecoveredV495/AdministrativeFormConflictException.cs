using System;

internal sealed class AdministrativeFormConflictException : Exception
{
	public AdministrativeFormConflictException()
		: base("Este folio ya se guardó con otros datos. Abre el formulario existente para editarlo.")
	{
	}
}
