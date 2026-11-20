using Microsoft.Data.SqlClient;

record WarehouseTraceEvent(string Id,string Action,string Status,string Note,string ActorName,string ActorLogin,DateTime OccurredAt);
partial class Database
{
    public async Task<List<object>> WarehouseTrace(string code,DocumentActor actor,CancellationToken ct)
    {
        if(!actor.Allowed||string.IsNullOrWhiteSpace(code)||code.Length>2048)return [];
        var token=WarehouseQrToken(code);
        var normalized=MaintenanceTraceCode.Normalize(code);
        var number=normalized.StartsWith("REAL-MNT|",StringComparison.Ordinal)?normalized[9..]:normalized;
        var compactNumber=System.Text.RegularExpressions.Regex.Replace(number,@"^(SOL-ALM-\d{8})-(\d{6}-)","$1$2");
        await using var connection=await Open(ct);await EnsureInstitutionalSchema(connection,ct);
        await using var command=new SqlCommand("""
          SELECT TOP 1 id FROM dbo.institutional_files WHERE area='communications' AND channel='requirements'
            AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'
            AND ((@token<>'' AND JSON_VALUE(metadata,'$.verificationToken')=@token)
              OR JSON_VALUE(metadata,'$.warehouseRequest.number') IN(@number,@compact)
              OR JSON_VALUE(metadata,'$.originalFileName') IN(@number+'.pdf',@compact+'.pdf')) ORDER BY id DESC;
          """,connection);
        command.Parameters.AddWithValue("@token",token??"");command.Parameters.AddWithValue("@number",number);command.Parameters.AddWithValue("@compact",compactNumber);
        var value=await command.ExecuteScalarAsync(ct);if(value is null)return [];
        var d=await InstitutionalDetail(Convert.ToInt64(value),actor,ct);
        if(d is null||d.Trashed||d.WarehouseRequest is null)return [];
        string? ticketStatus=null,ticketDepartment=null;
        if(d.WarehouseRequest.TicketId is Guid ticketId)
        {
            await using var ticket=new SqlCommand("SELECT status,assigned_department FROM dbo.support_tickets WHERE id=@ticket",connection);
            ticket.Parameters.AddWithValue("@ticket",ticketId);
            await using var ticketReader=await ticket.ExecuteReaderAsync(ct);
            if(await ticketReader.ReadAsync(ct)){ticketStatus=ticketReader.GetString(0);ticketDepartment=ticketReader.GetString(1);}
        }
        var events=new List<WarehouseTraceEvent>();
        void Add(string action,string status,string note,string? name,string? login,DateTime? time){if(time is DateTime at)events.Add(new($"{d.Id}-{action}",action,status,note,name??"",login??"",at));}
        Add("CREATED","SENT","Solicitud registrada",d.OwnerDisplayName,d.OwnerLogin,d.CreatedUtc);
        if(d.WarehouseRequest.TicketNumber is long linkedTicketNumber)Add("TICKET_CREATED",ticketStatus??"OPEN",$"Ticket #{linkedTicketNumber} · {d.WarehouseRequest.FailureDescription??"Falla registrada"}",d.OwnerDisplayName,d.OwnerLogin,d.CreatedUtc);
        Add("RECEIVED_WAREHOUSE","RECEIVED","Solicitud recibida en Almacén",d.ReceiverDisplayName,d.ReceiverLogin,d.ReceivedUtc);
        if(d.Status is "approved" or "rejected")Add(d.Status=="approved"?"APPROVED":"REJECTED",d.Status=="approved"?"APPROVED":"REJECTED","Decisión registrada",d.DecisionBy,null,d.DecisionUtc);
        Add("DISPATCHED","DISPATCH","Equipo y serial asignados",d.WarehousePreparedBy,d.WarehousePreparedLogin,d.WarehousePreparedUtc);
        if(d.WarehouseDelivery is {} delivery)Add("WITHDRAWN","DELIVERED","Retirado por "+delivery.ReceivedBy,delivery.DeliveredBy,delivery.DeliveredLogin,delivery.At);
        if(d.WarehouseInstallation is {} installed)Add("INSTALLED","INSTALLED",installed.Resolution,installed.ResolvedBy,null,installed.ResolvedAt);
        if(d.Status=="cancelled")Add("CANCELLED","CANCELLED",d.CorrectionNote??"Solicitud anulada",d.DecisionBy,null,d.DecisionUtc);
        var status=d.Status=="cancelled"?"CANCELLED":d.WarehouseInstallation is not null?"INSTALLED":d.WarehouseDelivery is not null?"DELIVERED":d.Status=="rejected"?"REJECTED":d.WarehousePreparedUtc is not null?"DISPATCH":d.Status=="approved"?"APPROVED":d.ReceivedUtc is not null?"RECEIVED":"SENT";
        var statusText=status switch{"CANCELLED"=>"Solicitud anulada","INSTALLED"=>"Instalado y comprobado","DELIVERED"=>"Equipo retirado y entregado","REJECTED"=>"Solicitud rechazada","DISPATCH"=>"Equipo asignado · pendiente de retiro","APPROVED"=>"Autorizada · pendiente de asignación","RECEIVED"=>"Recibida · pendiente de decisión",_=>"Pendiente de recepción"};
        return [new{id=d.Id.ToString(),documentNumber=d.WarehouseRequest.Number,equipmentType=string.Join(" · ",d.WarehouseRequest.Lines.Select(l=>l.Name)),serialNumber=string.Join(", ",d.WarehouseAssigned.Select(a=>a.Serial)),department=d.Department,movementType="REQUEST",operationalArea="WAREHOUSE",status,statusText,sentAt=d.CreatedUtc,senderName=d.OwnerDisplayName,receivedByName=d.ReceiverDisplayName,events,requiresInstallation=d.WarehouseRequest.TicketId is not null,ticketNumber=d.WarehouseRequest.TicketNumber,ticketStatus,ticketDepartment,failureCategory=d.WarehouseRequest.FailureCategory}];
    }
}
