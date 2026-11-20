using Microsoft.Data.SqlClient;
var source=File.ReadAllText("api/DatabaseAgencyLocations.cs");
var start=source.IndexOf("const string sql=\"\"\"",StringComparison.Ordinal)+"const string sql=\"\"\"".Length;
var sql=source[start..source.IndexOf("\"\"\";",start,StringComparison.Ordinal)];
// Session-local temp tables avoid creating database files or touching application data.
string Temporary(string text){foreach(var table in new[]{"agencies","agency_profiles","admin_user_groups"})text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:dbo\.)?"+table+@"\b","#"+table);return text;}
sql=Temporary(sql);
async Task Exec(SqlConnection c,string text){await using var cmd=new SqlCommand(Temporary(text),c);await cmd.ExecuteNonQueryAsync();}
{
 await using var c=new SqlConnection("Server=localhost;Database=tempdb;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");await c.OpenAsync();
 await Exec(c,"""
 CREATE TABLE agencies(id nvarchar(80),codigo nvarchar(80),terminal nvarchar(100),grupo nvarchar(100),directory_direccion nvarchar(100),directory_sector nvarchar(100),directory_municipio nvarchar(100),directory_provincia nvarchar(100),expected_latitude float,expected_longitude float,is_active bit);
 CREATE TABLE agency_profiles(id nvarchar(80),agency_id nvarchar(80),direccion nvarchar(100),sector nvarchar(100),municipio nvarchar(100),provincia nvarchar(100),latitude float,longitude float,submitted_at datetime2);
 CREATE TABLE admin_user_groups(user_id nvarchar(80),group_name nvarchar(100));
 INSERT agencies(id,codigo,terminal,grupo,expected_latitude,expected_longitude,is_active) VALUES('legacy-1','1','Primera','A',18,-69,1),('legacy-2','2','Segunda','B',19,-70,1),('inactive','3','Inactiva','A',18,-69,0),('missing','4','Sin GPS','A',NULL,NULL,1);
 INSERT agency_profiles(id,agency_id,latitude,longitude,submitted_at) VALUES('old','legacy-1',17,-68,'2025-01-01'),('new','legacy-1',18.5,-69.5,'2026-01-01'),('invalid','legacy-2',0,0,'2026-01-01');
 INSERT admin_user_groups VALUES('supervisor','A');
 """);
 async Task<List<(string Id,double? Lat,double? Lng)>> Read(bool scoped,string user){await using var cmd=new SqlCommand(sql,c);cmd.Parameters.AddWithValue("@scoped",scoped);cmd.Parameters.AddWithValue("@userId",user);await using var r=await cmd.ExecuteReaderAsync();var rows=new List<(string,double?,double?)>();while(await r.ReadAsync())rows.Add((r.GetString(0),r.IsDBNull(8)?null:r.GetDouble(8),r.IsDBNull(9)?null:r.GetDouble(9)));return rows;}
 var all=await Read(false,"admin");
 if(all.Count!=3||!all.Contains(("legacy-1",18.5,-69.5))||!all.Contains(("legacy-2",19,-70))||!all.Contains(("missing",null,null)))throw new Exception("Global locations, legacy IDs, latest profile, missing location or coordinate fallback failed");
 var scoped=await Read(true,"supervisor");if(scoped.Count!=2||scoped.Any(row=>row.Id=="legacy-2"))throw new Exception("Supervisor scope failed");
 if((await Read(true,"unassigned")).Count!=0)throw new Exception("Unassigned supervisor scope failed");
 Console.WriteLine("PASS: active agencies, legacy IDs, unique latest profile, paired coordinate fallback, missing GPS, supervisor scope and unassigned scope.");
}
