using System;

internal sealed class WarehouseFailureValidationException : Exception
{
	public WarehouseFailureValidationException()
		: base("La falla seleccionada ya no está disponible en el catálogo de soporte. Actualiza el formulario y vuelve a intentarlo.")
	{
	}
}
