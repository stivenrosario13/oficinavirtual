using System.Data;
using Microsoft.Data.SqlClient;

partial class Database
{
    internal const string WarehouseCleanupKey="V418_20260928_KEEP_PENDING_WAREHOUSE_REQUESTS";
    internal static async Task CleanupCompletedWarehouseRequests(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var command=new SqlCommand("""
          DECLARE @lock int;
          EXEC @lock=sys.sp_getapplock @Resource='WarehouseRequestCleanup',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
          IF @lock<0 THROW 50000,'No se pudo preparar la limpieza de solicitudes',1;
          IF NOT EXISTS(SELECT 1 FROM dbo.application_one_time_actions WITH(UPDLOCK,HOLDLOCK) WHERE action_key=@key)
          BEGIN
            IF OBJECT_ID('dbo.warehouse_request_cleanup_backup','U') IS NULL
              CREATE TABLE dbo.warehouse_request_cleanup_backup(
                cleanup_key varchar(100) NOT NULL,document_id bigint NOT NULL,metadata nvarchar(max) NOT NULL,
                file_data varbinary(max) NOT NULL,allocations_json nvarchar(max) NULL,
                archived_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),PRIMARY KEY(cleanup_key,document_id));
            INSERT dbo.warehouse_request_cleanup_backup(cleanup_key,document_id,metadata,file_data)
              SELECT @key,id,metadata,file_data FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK)
              WHERE area='communications' AND channel='requirements'
                AND JSON_VALUE(metadata,'$.recipientDepartment')=N'Almacén'
                AND COALESCE(JSON_VALUE(metadata,'$.trashed'),'false')<>'true'
                AND (COALESCE(JSON_VALUE(metadata,'$.status'),'') NOT IN('pending','received','correction_requested')
                     OR JSON_QUERY(metadata,'$.warehouseDelivery') IS NOT NULL);
            IF OBJECT_ID('dbo.warehouse_equipment_allocations','U') IS NOT NULL
            BEGIN
              EXEC sys.sp_executesql N'
                UPDATE b SET allocations_json=(SELECT a.* FROM dbo.warehouse_equipment_allocations a WHERE a.document_id=b.document_id FOR JSON PATH)
                  FROM dbo.warehouse_request_cleanup_backup b WHERE b.cleanup_key=@key;',N'@key varchar(100)',@key;
              IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.warehouse_equipment_allocations') AND name='CK_warehouse_allocation_status')
                ALTER TABLE dbo.warehouse_equipment_allocations DROP CONSTRAINT CK_warehouse_allocation_status;
              ALTER TABLE dbo.warehouse_equipment_allocations WITH CHECK ADD CONSTRAINT CK_warehouse_allocation_status CHECK(status IN('PENDING_PICKUP','WITHDRAWN','CANCELLED'));
              EXEC sys.sp_executesql N'
                UPDATE a SET status=''CANCELLED'' FROM dbo.warehouse_equipment_allocations a
                  JOIN dbo.warehouse_request_cleanup_backup b ON b.document_id=a.document_id
                  WHERE b.cleanup_key=@key AND a.status=''PENDING_PICKUP'';',N'@key varchar(100)',@key;
            END;
            UPDATE f SET metadata=JSON_MODIFY(f.metadata,'$.trashed',CAST(1 AS bit))
              FROM dbo.institutional_files f JOIN dbo.warehouse_request_cleanup_backup b ON b.document_id=f.id WHERE b.cleanup_key=@key;
            INSERT dbo.application_one_time_actions(action_key) VALUES(@key);
          END;
          """,connection,tx);
        command.Parameters.AddWithValue("@key",WarehouseCleanupKey);
        await command.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
}
