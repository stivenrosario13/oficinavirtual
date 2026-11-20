using Microsoft.Data.SqlClient;

partial class Database
{
 // One-time, narrowly targeted profile assignment. Later permission edits win.
 internal static async Task EnsureBryanManagerProfile(SqlConnection connection,CancellationToken ct)
 {
  const string sql="""
   SET XACT_ABORT ON;
   BEGIN TRANSACTION;
   DECLARE @lock int;
   EXEC @lock=sys.sp_getapplock @Resource=N'V418_BRYAN_MANAGER_20260930',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
   IF @lock<0 THROW 50001,'No se pudo bloquear la actualización del perfil de gerente.',1;
   IF OBJECT_ID(N'dbo.user_access_profile_updates',N'U') IS NULL
    CREATE TABLE dbo.user_access_profile_updates(update_key varchar(100) NOT NULL PRIMARY KEY,user_id nvarchar(80) NOT NULL,previous_permissions nvarchar(max) NULL,applied_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
   IF NOT EXISTS(SELECT 1 FROM dbo.user_access_profile_updates WHERE update_key='V418_BRYAN_MANAGER_20260930')
   BEGIN
    DECLARE @id nvarchar(80),@previous nvarchar(max);
    SELECT @id=CONVERT(nvarchar(80),id),@previous=permissions_json FROM dbo.admin_users
     WHERE LOWER(email)=N'bryan.arias@grupotejeda.local' AND (role='WAREHOUSE' OR (role='TECHNOLOGY' AND support_team='WAREHOUSE'));
    IF @id IS NOT NULL
    BEGIN
     UPDATE dbo.admin_users SET permissions_json=JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(CASE WHEN ISJSON(permissions_json)=1 THEN permissions_json ELSE N'{}' END,
      '$.canManageDepartment',CAST(1 AS bit)),'$.canViewFieldAudits',CAST(1 AS bit)),'$.canViewDashboard',CAST(1 AS bit)),'$.canViewSupportDashboard',CAST(1 AS bit)),updated_at=SYSUTCDATETIME()
      WHERE CONVERT(nvarchar(80),id)=@id;
     INSERT dbo.user_access_profile_updates(update_key,user_id,previous_permissions) VALUES('V418_BRYAN_MANAGER_20260930',@id,@previous);
    END
   END
   COMMIT;
   """;
  await using var command=new SqlCommand(sql,connection);await command.ExecuteNonQueryAsync(ct);
 }
}
