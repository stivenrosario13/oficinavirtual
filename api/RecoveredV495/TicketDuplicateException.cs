using System;

internal sealed class TicketDuplicateException : Exception
{
	public Guid TicketId { get; }

	public long TicketNumber { get; }

	public string Status { get; }

	public string Subject { get; }

	public TicketDuplicateException(Guid ticketId, long ticketNumber, string status, string subject)
		: base($"Ticket duplicado #{ticketNumber}")
	{
		TicketId = ticketId;
		TicketNumber = ticketNumber;
		Status = status;
		Subject = subject;
	}
}
