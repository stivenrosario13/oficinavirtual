using Microsoft.Data.SqlClient;
using System.Text.Json;
record InstitutionalUser(string UserName,string FullName,string Email,string Department);
partial class Database
{
    static async Task<List<InstitutionalUser>> ReadInstitutionalUsers(SqlConnection c,CancellationToken ct,SqlTransaction? tx=null)
    {
        var users=new List<InstitutionalUser>();
        await using var command=new SqlCommand("SELECT CONVERT(nvarchar(80),id),display_name,email,role,support_team FROM dbo.admin_users WHERE is_active=1 ORDER BY display_name",c,tx);
        await using var r=await command.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))users.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),DepartmentAccounts.AccountDepartment(r.GetString(3),r.IsDBNull(4)?null:r.GetString(4))));
        return users;
    }
    public async Task<string> InstitutionalUsers(CancellationToken ct){await using var c=await Open(ct);return JsonSerializer.Serialize(await ReadInstitutionalUsers(c,ct),InstitutionalJson);}
    public async Task<List<InstitutionalUser>> AdministrativeFolderUsers(DocumentActor actor,CancellationToken ct){await using var c=await Open(ct);return (await ReadInstitutionalUsers(c,ct)).Where(u=>!string.Equals(u.UserName,actor.Id,StringComparison.OrdinalIgnoreCase)).ToList();}
}
