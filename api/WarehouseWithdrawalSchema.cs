using Microsoft.Data.SqlClient;
partial class Database
{
    // Match the already-supported request/allocation quantity limit.
    static async Task EnsureWarehouseWithdrawalSchema(SqlConnection connection,CancellationToken ct)
    {
        const string sql="""
        IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.maintenance_movements') AND name=N'CK_maintenance_quantity' AND definition NOT LIKE '%10000%')
        BEGIN
            BEGIN TRANSACTION;
            ALTER TABLE dbo.maintenance_movements DROP CONSTRAINT CK_maintenance_quantity;
            ALTER TABLE dbo.maintenance_movements WITH CHECK ADD CONSTRAINT CK_maintenance_quantity CHECK(quantity BETWEEN 1 AND 10000);
            COMMIT TRANSACTION;
        END;
        """;
        await using var command=new SqlCommand(sql,connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
