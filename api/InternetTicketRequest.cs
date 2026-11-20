using Microsoft.Data.SqlClient;
partial class Database
{
    // Called only after the ordinary ticket reader has verified this actor's scope.
    public async Task<bool> InternetTicketAgencyMatches(Guid ticketId,Guid agencyId,CancellationToken ct)
    {
        await using var c=await Open(ct);
        await using var cmd=new SqlCommand("SELECT COUNT(*) FROM dbo.support_tickets WHERE id=@ticket AND agency_id=@agency AND ticket_type='INTERNAL' AND assigned_department='TECHNOLOGY' AND status NOT IN('RESOLVED','CLOSED','CANCELLED')",c);
        cmd.Parameters.AddWithValue("@ticket",ticketId);cmd.Parameters.AddWithValue("@agency",agencyId.ToString());return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==1;
    }
}
