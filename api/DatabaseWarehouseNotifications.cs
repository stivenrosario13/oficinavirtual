using Microsoft.Data.SqlClient;

partial class Database
{
    public async Task<List<object>> WarehouseNotifications(DocumentActor actor,CancellationToken ct)
    {
        if(actor.Role is not ("Administrator" or "Warehouse")&&actor.Team!="WAREHOUSE")return [];
        await using var connection=await Open(ct);
        await EnsureInstitutionalSchema(connection,ct);
        await EnsureNotificationReceiptSchema(connection,ct);
        await using var command=new SqlCommand("""
          SELECT TOP(100) f.id,JSON_VALUE(f.metadata,'$.title'),f.department,
            JSON_VALUE(f.metadata,'$.ownerDisplayName'),JSON_VALUE(f.metadata,'$.warehouseRequest.number'),
            JSON_VALUE(f.metadata,'$.createdUtc'),JSON_VALUE(f.metadata,'$.status'),r.read_at
          FROM dbo.institutional_files f
          LEFT JOIN dbo.support_notification_receipts r ON r.user_id=@user AND r.notification_key=CONCAT('WAREHOUSE_REQUEST:',f.id)
          WHERE COALESCE(JSON_VALUE(f.metadata,'$.trashed'),'false')<>'true' AND f.area='communications' AND f.channel='requirements'
            AND JSON_VALUE(f.metadata,'$.recipientDepartment')=N'Almacén'
            AND JSON_QUERY(f.metadata,'$.warehouseRequest') IS NOT NULL
            AND JSON_VALUE(f.metadata,'$.status') NOT IN('draft','active')
            AND r.dismissed_at IS NULL
          ORDER BY f.id DESC;
          """,connection);
        command.Parameters.AddWithValue("@user",actor.Id);
        var result=new List<object>();await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)){
            string Text(int i)=>reader.IsDBNull(i)?"":reader.GetString(i);
            var id=reader.GetInt64(0);
            result.Add(new{kind="TICKET",id=id.ToString(),ticketNumber=id,ticketType="WAREHOUSE",codigo=Text(4),terminal=Text(2),grupo="Almacén",assignedDepartment="WAREHOUSE",subject=Text(1),resolution=(string?)null,status=Text(6),updatedAt=Text(5),eventType="CREATED",message=$"Nueva solicitud de {Text(2)} · {Text(3)}",actorName=Text(3),notificationId=id.ToString(),isRead=!reader.IsDBNull(7)});
        }
        return result;
    }
}
