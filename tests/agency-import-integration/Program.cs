using Microsoft.Data.SqlClient;

// Run the production importer against an isolated, disposable SQL Server database.
var name="Codex_AgencyImport_QA_"+Guid.NewGuid().ToString("N");
var connectionString=$"Server=localhost;Database={name};Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
await using var master=new SqlConnection("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5");
await master.OpenAsync();var created=false;
void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
async Task Exec(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
async Task<int> Count(SqlConnection c,string sql){await using var cmd=new SqlCommand(sql,c);return Convert.ToInt32(await cmd.ExecuteScalarAsync());}
AgencyDirectoryImportRow Row(int number,string code,string terminal,string group)=>new(number,code,terminal,group,null,"Imported address",null,null,null,19,-70);
try
{
    await Exec(master,$"CREATE DATABASE [{name}]");created=true;
    await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
    await Exec(connection,"""
        CREATE TABLE agencies(id char(36) PRIMARY KEY,source_key nvarchar(200) UNIQUE,codigo nvarchar(100),terminal nvarchar(300),grupo nvarchar(150),region nvarchar(120),
            directory_direccion nvarchar(300),directory_sector nvarchar(120),directory_municipio nvarchar(120),directory_provincia nvarchar(120),expected_latitude float,expected_longitude float,
            is_active bit,status varchar(30),created_at datetime2,updated_at datetime2);
        CREATE TABLE agency_profiles(id char(36) PRIMARY KEY,agency_id char(36) REFERENCES agencies(id),direccion nvarchar(300),latitude float,longitude float,evidence varbinary(max));
        CREATE TABLE audit_log(entity_type varchar(50),entity_id nvarchar(100),action varchar(100),after_data nvarchar(max),actor_email nvarchar(100));
        INSERT agencies(id,source_key,codigo,terminal,grupo,is_active,status,created_at,updated_at)
            VALUES('11111111-1111-1111-1111-111111111111','LTK:101','101','Terminal original','Grupo Uno',1,'COMPLETED',GETUTCDATE(),GETUTCDATE());
        INSERT agency_profiles VALUES('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','Field address',18.5,-69.9,0x010203);
        """);
    var db=new Database(connectionString);
    var rows=new[]{Row(2," 101 ","Terminal original"," grupo\u00a0  UNO "),Row(3,"102","New terminal","GRUPO UNO")};
    var first=await db.ImportAgencyDirectory(rows,"QA.xlsx","qa",default);
    Check(first.Created==1&&first.Updated==1&&first.Skipped==0,"Existing code updates in place; only the new agency is inserted");
    Check(await Count(connection,"SELECT COUNT(DISTINCT grupo COLLATE Latin1_General_100_BIN2) FROM agencies")==1,"Case and Excel whitespace reuse the existing group");
    var repeat=await db.ImportAgencyDirectory(rows,"QA.xlsx","qa",default);
    Check(repeat.Created==0&&repeat.Updated==2&&await Count(connection,"SELECT COUNT(*) FROM agencies")==2,"Re-upload is idempotent");
    Check(await Count(connection,"SELECT COUNT(*) FROM agencies a JOIN agency_profiles p ON p.agency_id=a.id WHERE a.codigo='101' AND a.status='COMPLETED' AND p.direccion='Field address' AND p.latitude=18.5 AND p.longitude=-69.9 AND p.evidence=0x010203")==1,"Original ID, completed status, field location and evidence are preserved");
    var moved=await db.ImportAgencyDirectory([Row(2,"101","Terminal renamed","Grupo Dos")],"QA.xlsx","qa",default);
    Check(moved.Updated==1&&moved.Created==0&&await Count(connection,"SELECT COUNT(*) FROM agencies WHERE id='11111111-1111-1111-1111-111111111111' AND grupo='Grupo Dos'")==1,"Group/name changes preserve agency identity");
    var conflict=await db.ImportAgencyDirectory([Row(2,"999","Terminal renamed","Other group")],"QA.xlsx","qa",default);
    Check(conflict.Skipped==1&&conflict.Created==0&&conflict.Duplicates.Count==1,"Same terminal with different code and group is reported, not duplicated");
    var batch=await db.ImportAgencyDirectory([Row(2,"201","Duplicate terminal","New group"),Row(3,"202","duplicate TERMINAL","Other group")],"QA.xlsx","qa",default);
    Check(batch.Skipped==2&&batch.Created==0,"Duplicate terminals in one batch are skipped before insertion");
    var duplicateCode=await db.ImportAgencyDirectory([Row(2,"AB1","First name","G"),Row(3,"ab1","Second name","G")],"QA.xlsx","qa",default);
    Check(duplicateCode.Skipped==2&&duplicateCode.Created==0,"Database importer also blocks repeated codes without relying on request validation");
    await Exec(connection,"INSERT agencies(id,source_key,codigo,terminal,grupo,is_active,status) VALUES(NEWID(),'LEGACY:101','101','Historical duplicate','Old group',1,'COMPLETED')");
    var ambiguous=await db.ImportAgencyDirectory([Row(2,"101","Replacement","Another group")],"QA.xlsx","qa",default);
    Check(ambiguous.Skipped==1&&ambiguous.Created==0&&ambiguous.Updated==0,"Pre-existing duplicate codes require review; no arbitrary history is selected");
    var next=await db.ImportAgencyDirectory([Row(2,"301","Unique new terminal","Brand New"),Row(3,"302","Other new terminal","brand new")],"QA.xlsx","qa",default);
    Check(next.Created==2&&await Count(connection,"SELECT COUNT(DISTINCT grupo COLLATE Latin1_General_100_BIN2) FROM agencies WHERE codigo IN ('301','302')")==1,"New group is shared by agencies in the same batch");
    var nextChunk=await db.ImportAgencyDirectory([Row(2,"303","Third new terminal"," BRAND   NEW ")],"QA.xlsx","qa",default);
    Check(nextChunk.Created==1&&await Count(connection,"SELECT COUNT(DISTINCT grupo COLLATE Latin1_General_100_BIN2) FROM agencies WHERE codigo IN ('301','302','303')")==1,"Later chunks reuse the group created by an earlier chunk");
    Check(await Count(connection,"SELECT COUNT(*) FROM agency_profiles WHERE evidence=0x010203 AND direccion='Field address'")==1,"Audit evidence survives all import scenarios");
    await Exec(connection,"UPDATE agencies SET grupo='3-ALBERTO PROPIAS' WHERE codigo IN ('301','302','303'); UPDATE agencies SET grupo='003 | ALBERTO PROPIAS' WHERE codigo='102'; UPDATE agencies SET grupo='ALBERTO PROPIAS' WHERE id='11111111-1111-1111-1111-111111111111'");
    var screenshotRows=new[]{Row(2,"301","Unique new terminal","003 | ALBERTO PROPIAS"),Row(3,"302","Other new terminal","ALBERTO PROPIAS"),Row(4,"303","Third new terminal","3-ALBERTO PROPIAS")};
    var screenshot=await db.ImportAgencyDirectory(screenshotRows,"QA.xlsx","qa",default);
    Check(screenshot.Created==0&&screenshot.Updated==3&&screenshot.Skipped==0,"Screenshot variants reuse existing agencies, with no new records");
    Check(await Count(connection,"SELECT COUNT(*) FROM agencies WHERE codigo IN ('301','302','303') AND grupo='3-ALBERTO PROPIAS'")==3,"003 pipe, 3 dash and bare group name reuse the established group");
    var screenshotAgain=await db.ImportAgencyDirectory(screenshotRows,"QA.xlsx","qa",default);
    Check(screenshotAgain.Created==0&&screenshotAgain.Updated==3,"Repeated screenshot import stays idempotent");
    var conflictingGroup=await db.ImportAgencyDirectory([Row(2,"401","Distinct terminal","4-ALBERTO PROPIAS")],"QA.xlsx","qa",default);
    Check(conflictingGroup.Created==0&&conflictingGroup.Skipped==1,"Different numeric group codes are not silently merged");
    var newVariants=await db.ImportAgencyDirectory([Row(2,"501","First fresh terminal","005 | NUEVO GRUPO"),Row(3,"502","Second fresh terminal","5-NUEVO GRUPO"),Row(4,"503","Third fresh terminal","NUEVO GRUPO")],"QA.xlsx","qa",default);
    Check(newVariants.Created==3&&await Count(connection,"SELECT COUNT(DISTINCT grupo COLLATE Latin1_General_100_BIN2) FROM agencies WHERE codigo IN ('501','502','503')")==1,"New group prefix variants are canonicalized within one batch");
    Check(await Count(connection,"SELECT COUNT(*) FROM agency_profiles WHERE evidence=0x010203 AND direccion='Field address'")==1,"Screenshot reproduction leaves audit evidence intact");
    await Exec(connection,"""
        CREATE TABLE admin_user_groups(user_id char(36),group_name nvarchar(150),region nvarchar(120),assigned_at datetime2 DEFAULT SYSUTCDATETIME(),PRIMARY KEY(user_id,group_name));
        CREATE TABLE agency_groups(grupo nvarchar(150),agency_count int,is_active bit,updated_at datetime2);
        INSERT agency_groups VALUES('3-ALBERTO PROPIAS',3,1,GETUTCDATE()),('003 | ALBERTO PROPIAS',1,1,GETUTCDATE()),('ALBERTO PROPIAS',1,1,GETUTCDATE());
        """);
    var repair=File.ReadAllText("database-mssql/046_unify_imported_group_aliases.sql");
    var totalBefore=await Count(connection,"SELECT COUNT(*) FROM agencies");
    await Exec(connection,repair);
    Check(await Count(connection,"SELECT COUNT(*) FROM agencies WHERE grupo IN ('003 | ALBERTO PROPIAS','ALBERTO PROPIAS')")==0,"Repair consolidates existing screenshot aliases");
    Check(await Count(connection,"SELECT COUNT(*) FROM agencies")==totalBefore&&await Count(connection,"SELECT COUNT(*) FROM agency_profiles WHERE evidence=0x010203 AND direccion='Field address'")==1,"Repair keeps every agency identity and field evidence");
    Check(await Count(connection,"SELECT COUNT(*) FROM audit_log WHERE action='AGENCY_GROUP_ALIAS_NORMALIZED'")==2,"Repair records both group changes for traceability");
    await Exec(connection,repair);
    Check(await Count(connection,"SELECT COUNT(*) FROM audit_log WHERE action='AGENCY_GROUP_ALIAS_NORMALIZED'")==2,"Repair can run twice without additional changes");
    await Exec(connection,"UPDATE agencies SET grupo='ALBERTO PROPIAS' WHERE codigo='102'; INSERT admin_user_groups(user_id,group_name) VALUES('33333333-3333-3333-3333-333333333333','ALBERTO PROPIAS')");
    var blocked=false;
    try{await Exec(connection,repair);}catch(SqlException error) when(error.Number==50046){blocked=true;}
    Check(blocked&&await Count(connection,"SELECT COUNT(*) FROM agencies WHERE codigo='102' AND grupo='ALBERTO PROPIAS'")==1,"Conflicting supervisor assignments stop repair without changing records");
    var completeRepair=File.ReadAllText("database-mssql/047_unify_groups_and_supervisor_assignments.sql");
    await Exec(connection,"INSERT admin_user_groups(user_id,group_name) VALUES('33333333-3333-3333-3333-333333333333','003 | ALBERTO PROPIAS'),('55555555-5555-5555-5555-555555555555','3-ALBERTO PROPIAS'),('66666666-6666-6666-6666-666666666666','Other unrelated group')");
    await Exec(connection,completeRepair);
    Check(await Count(connection,"SELECT COUNT(*) FROM admin_user_groups WHERE user_id='33333333-3333-3333-3333-333333333333' AND group_name='3-ALBERTO PROPIAS'")==1,"Both aliases transfer supervisor to a single canonical assignment");
    Check(await Count(connection,"SELECT COUNT(*) FROM admin_user_groups WHERE group_name IN ('ALBERTO PROPIAS','003 | ALBERTO PROPIAS')")==0,"Old assignment labels no longer appear");
    Check(await Count(connection,"SELECT COUNT(*) FROM admin_user_groups WHERE user_id='55555555-5555-5555-5555-555555555555' AND group_name='3-ALBERTO PROPIAS'")==1&&await Count(connection,"SELECT COUNT(*) FROM admin_user_groups WHERE group_name='Other unrelated group'")==1,"Existing canonical and unrelated supervisors are retained");
    Check(await Count(connection,"SELECT COUNT(*) FROM agencies WHERE grupo IN ('ALBERTO PROPIAS','003 | ALBERTO PROPIAS')")==0&&await Count(connection,"SELECT COUNT(*) FROM agencies")==totalBefore,"Complete repair consolidates agencies without deleting any");
    Check(await Count(connection,"SELECT COUNT(*) FROM agency_profiles WHERE evidence=0x010203 AND direccion='Field address'")==1,"Complete repair preserves field audit and evidence");
    var assignmentLogs=await Count(connection,"SELECT COUNT(*) FROM audit_log WHERE action='GROUP_ASSIGNMENT_ALIAS_NORMALIZED'");
    await Exec(connection,completeRepair);
    Check(assignmentLogs==2&&await Count(connection,"SELECT COUNT(*) FROM audit_log WHERE action='GROUP_ASSIGNMENT_ALIAS_NORMALIZED'")==2,"Complete repair records assignments and is idempotent");
    await Exec(connection,"""
        ALTER TABLE agency_profiles ADD municipio nvarchar(120),provincia nvarchar(120),tipo_establecimiento nvarchar(100),accuracy_meters float,submitted_at datetime2,observations nvarchar(max),employee_name nvarchar(160),submitted_by_context nvarchar(max);
        ALTER TABLE audit_log ADD created_at datetime2;
        CREATE TABLE admin_users(id char(36),email nvarchar(100),role varchar(30),is_active bit,display_name nvarchar(100));
        INSERT admin_user_groups(user_id,group_name) VALUES('44444444-4444-4444-4444-444444444444','3-ALBERTO PROPIAS');
        """);
    await Exec(connection,"""
        ;WITH n AS(SELECT TOP(205) ROW_NUMBER() OVER(ORDER BY (SELECT NULL)) AS n FROM sys.all_objects)
        INSERT agencies(id,source_key,codigo,terminal,grupo,is_active,status)
        SELECT NEWID(),CONCAT('AUDITQA:',n),CONCAT('AUDITQA:',n),'Audit history test','3-ALBERTO PROPIAS',1,'COMPLETED' FROM n;
        INSERT agency_profiles(id,agency_id,direccion,latitude,longitude,submitted_at,submitted_by_context)
        SELECT NEWID(),id,'Historical address',18,-69,GETUTCDATE(),'{"login":"qa"}' FROM agencies WHERE source_key LIKE 'AUDITQA:%';
        """);
    var source=File.ReadAllText("api/DatabaseSqlServerScoped.cs");
    var queryStart=source.IndexOf("var auditsSql = $\"\"\"",StringComparison.Ordinal)+"var auditsSql = $\"\"\"".Length;
    var query=source[queryStart..source.IndexOf("\"\"\";",queryStart,StringComparison.Ordinal)];
    async Task<int> AuditRows(string scope){await using var cmd=new SqlCommand(query.Replace("{scopeSql}",scope),connection);cmd.Parameters.AddWithValue("@userId","44444444-4444-4444-4444-444444444444");await using var reader=await cmd.ExecuteReaderAsync();var count=0;while(await reader.ReadAsync())count++;return count;}
    Check(await AuditRows("")==206,"Production audit query returns all 206 audits, including those beyond 200");
    Check(await AuditRows("AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo)")==206,"Supervisor query retains assigned-group scope");
    await Exec(connection,"DELETE FROM admin_user_groups WHERE user_id='44444444-4444-4444-4444-444444444444'");
    Check(await AuditRows("AND EXISTS(SELECT 1 FROM dbo.admin_user_groups ug WHERE ug.user_id=@userId AND ug.group_name=a.grupo)")==0,"Removing the audit limit does not expose unassigned groups");
}
finally
{
    SqlConnection.ClearAllPools();
    if(created)await Exec(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];");
}

sealed partial class Database(string connectionString)
{
    async Task<SqlConnection> Open(CancellationToken ct){var c=new SqlConnection(connectionString);await c.OpenAsync(ct);return c;}
    static Task EnsureAgencyDirectorySchema(SqlConnection c,CancellationToken ct)=>Task.CompletedTask;
    static object Db(object? value)=>value??DBNull.Value;
}
record AgencyDirectoryImportRow(int RowNumber,string? Codigo,string? Terminal,string? Grupo,string? Region,string? Direccion,string? Sector,string? Municipio,string? Provincia,double? Latitude,double? Longitude);
record AgencyDirectoryImportResult(int Total,int Created,int Updated,int Skipped,List<string> Duplicates);
