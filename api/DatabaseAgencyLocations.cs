using Microsoft.Data.SqlClient;

record AgencyLocationRow(string Id,string Codigo,string Terminal,string Grupo,string? Direccion,string? Sector,string? Municipio,string? Provincia,double? Latitude,double? Longitude);

sealed partial class Database
{
    public async Task<List<AgencyLocationRow>> AgencyLocations(string userId,string role,CancellationToken ct)
    {
        await using var connection=await Open(ct);
        await EnsureAgencyDirectorySchema(connection,ct);
        const string sql="""
            SELECT a.id,a.codigo,a.terminal,a.grupo,
                   COALESCE(p.direccion,a.directory_direccion),COALESCE(p.sector,a.directory_sector),
                   COALESCE(p.municipio,a.directory_municipio),COALESCE(p.provincia,a.directory_provincia),
                   CASE WHEN p.latitude BETWEEN -90 AND 90 AND p.longitude BETWEEN -180 AND 180 AND (p.latitude<>0 OR p.longitude<>0) THEN p.latitude ELSE a.expected_latitude END,
                   CASE WHEN p.latitude BETWEEN -90 AND 90 AND p.longitude BETWEEN -180 AND 180 AND (p.latitude<>0 OR p.longitude<>0) THEN p.longitude ELSE a.expected_longitude END
            FROM dbo.agencies a
            OUTER APPLY (SELECT TOP(1) p.* FROM dbo.agency_profiles p WHERE p.agency_id=a.id ORDER BY p.submitted_at DESC,p.id DESC) p
            WHERE a.is_active=1 AND (@scoped=0 OR EXISTS(
                SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo))
            ORDER BY a.grupo,a.terminal,a.id;
            """;
        await using var command=new SqlCommand(sql,connection);
        AddScope(command,userId,role);
        await using var reader=await command.ExecuteReaderAsync(ct);
        var rows=new List<AgencyLocationRow>();
        while(await reader.ReadAsync(ct))
        {
            string? Text(int i)=>reader.IsDBNull(i)?null:reader.GetString(i);
            rows.Add(new(Text(0)!,Text(1)??"",Text(2)??"",Text(3)??"",Text(4),Text(5),Text(6),Text(7),reader.IsDBNull(8)?null:Convert.ToDouble(reader.GetValue(8)),reader.IsDBNull(9)?null:Convert.ToDouble(reader.GetValue(9))));
        }
        return rows;
    }
}
