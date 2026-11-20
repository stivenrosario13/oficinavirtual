using System.Data;
using Microsoft.Data.SqlClient;

record WarehouseProductRemoval(string ProductId,int ExpectedQuantity,string Reason);

partial class Database
{
    public async Task<string?> WarehouseRemoveProduct(DocumentActor actor,WarehouseProductRemoval input,CancellationToken ct)
    {
        if(!actor.CanDeleteInventory)return "No tienes permiso para eliminar artículos.";
        if(string.IsNullOrWhiteSpace(input.ProductId)||input.ProductId.Length>40||input.ExpectedQuantity<0||string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Trim().Length<5||input.Reason.Length>500)return "Indica el artículo y un motivo de 5 a 500 caracteres.";
        await using var connection=await Open(ct);
        await EnsureMaintenanceSchema(connection,ct);await EnsureWarehouseFulfillmentSchema(connection,ct);
        await using(var schema=new SqlCommand("""
          IF OBJECT_ID(N'dbo.warehouse_product_removals',N'U') IS NULL
          CREATE TABLE dbo.warehouse_product_removals(
            id bigint IDENTITY PRIMARY KEY,product_id nvarchar(40) NOT NULL,
            description nvarchar(240) NOT NULL,available_quantity int NOT NULL,
            actor_id nvarchar(80) NOT NULL,actor_login nvarchar(160) NOT NULL,
            reason nvarchar(500) NOT NULL,removed_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME());
          """,connection))await schema.ExecuteNonQueryAsync(ct);
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using(var user=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.admin_users WITH(HOLDLOCK) WHERE id=@id AND LOWER(email)=LOWER(@login) AND is_active=1 AND (role='WAREHOUSE' OR support_team='WAREHOUSE')",connection,tx))
        {
            user.Parameters.AddWithValue("@id",actor.Id);user.Parameters.AddWithValue("@login",actor.Login);
            if(Convert.ToInt64(await user.ExecuteScalarAsync(ct))!=1)return "La cuenta ya no tiene acceso a esta operación.";
        }
        string name;int available;
        await using(var product=new SqlCommand("""
          SELECT p.description,p.official_stock+
            COALESCE((SELECT SUM(CASE WHEN m.movement_type IN('ENTRY','DAMAGED_RETURN') THEN m.quantity WHEN m.movement_type IN('NEW_DELIVERY','EXIT','TRANSFER_TO_WORKSHOP','DISCHARGE') THEN -m.quantity ELSE 0 END)
              FROM dbo.maintenance_movements m WITH(HOLDLOCK) WHERE m.operational_area='WAREHOUSE' AND m.occurred_at>=p.imported_at AND (m.equipment_type=p.description OR m.component_type=p.product_id)),0)
          FROM dbo.warehouse_products p WITH(UPDLOCK,HOLDLOCK) WHERE p.product_id=@product AND p.is_active=1;
          """,connection,tx))
        {
            product.Parameters.AddWithValue("@product",input.ProductId.Trim());
            await using var reader=await product.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))return "El artículo ya no está activo. Actualiza el inventario.";
            name=reader.GetString(0);available=Math.Max(0,reader.GetInt32(1));
        }
        await using(var pending=new SqlCommand("SELECT COUNT_BIG(*) FROM dbo.warehouse_equipment_allocations WITH(UPDLOCK,HOLDLOCK) WHERE status='PENDING_PICKUP' AND (product_id=@product OR (product_id IS NULL AND LOWER(equipment_name)=LOWER(@name)))",connection,tx))
        {
            pending.Parameters.AddWithValue("@product",input.ProductId.Trim());pending.Parameters.AddWithValue("@name",name);
            if(Convert.ToInt64(await pending.ExecuteScalarAsync(ct))>0)return "Este artículo tiene unidades reservadas pendientes de retiro. No se puede eliminar.";
        }
        if(available!=input.ExpectedQuantity)return "Las existencias cambiaron. Actualiza el inventario antes de eliminar.";
        await using(var update=new SqlCommand("""
          INSERT dbo.warehouse_product_removals(product_id,description,available_quantity,actor_id,actor_login,reason)
            VALUES(@product,@name,@quantity,@actor,@login,@reason);
          UPDATE dbo.warehouse_products SET is_active=0 WHERE product_id=@product AND is_active=1;
          """,connection,tx))
        {
            update.Parameters.AddWithValue("@product",input.ProductId.Trim());update.Parameters.AddWithValue("@name",name);update.Parameters.AddWithValue("@quantity",available);
            update.Parameters.AddWithValue("@actor",actor.Id);update.Parameters.AddWithValue("@login",actor.Login);update.Parameters.AddWithValue("@reason",input.Reason.Trim());
            await update.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);return null;
    }
}
