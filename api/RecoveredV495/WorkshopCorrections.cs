using System;
using System.Data;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

internal record WorkshopIntakeDeletion(string Reason);
internal record WorkshopIntakeUpdate(string Equipment, string Serial, string Failure, string? Notes);

internal sealed partial class Database
{
	private static readonly SemaphoreSlim WorkshopCorrectionSchemaLock = new SemaphoreSlim(1, 1);
	private static bool workshopCorrectionSchemaReady;

	private static async Task EnsureWorkshopCorrectionSchema(SqlConnection connection, CancellationToken ct)
	{
		if (workshopCorrectionSchemaReady) return;
		await WorkshopCorrectionSchemaLock.WaitAsync(ct);
		try
		{
			if (workshopCorrectionSchemaReady) return;
		await using SqlCommand schema = new SqlCommand("""
			IF OBJECT_ID(N'dbo.workshop_intake_deletions',N'U') IS NULL
			CREATE TABLE dbo.workshop_intake_deletions(source_id uniqueidentifier NOT NULL PRIMARY KEY,document_number nvarchar(100) NOT NULL,equipment nvarchar(100) NOT NULL,serial nvarchar(120) NULL,department varchar(40) NOT NULL,original_created_at datetime2(3) NOT NULL,original_created_by nvarchar(160) NOT NULL,reason nvarchar(500) NOT NULL,deleted_at datetime2(3) NOT NULL,deleted_by nvarchar(80) NOT NULL,deleted_by_name nvarchar(160) NOT NULL,deleted_by_login nvarchar(256) NOT NULL);
			IF OBJECT_ID(N'dbo.workshop_intake_edits',N'U') IS NULL
			CREATE TABLE dbo.workshop_intake_edits(id uniqueidentifier NOT NULL PRIMARY KEY,source_id uniqueidentifier NOT NULL,previous_data nvarchar(max) NOT NULL,new_data nvarchar(max) NOT NULL,edited_at datetime2(3) NOT NULL,edited_by nvarchar(80) NOT NULL,edited_by_name nvarchar(160) NOT NULL,edited_by_login nvarchar(256) NOT NULL);
			""", connection);
		await schema.ExecuteNonQueryAsync(ct);
			workshopCorrectionSchemaReady = true;
		}
		finally
		{
			WorkshopCorrectionSchemaLock.Release();
		}
	}

	public async Task<string> WorkshopSnapshot(WorkshopActor actor, string? code, CancellationToken ct)
	{
		await using SqlConnection connection = await Open(ct);
		await EnsureWorkshopCorrectionSchema(connection, ct);
		HashSet<string> deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		await using (SqlCommand read = new SqlCommand("SELECT CONVERT(varchar(36),source_id) FROM dbo.workshop_intake_deletions", connection))
		await using (SqlDataReader rows = await read.ExecuteReaderAsync(ct))
		{
			while (await rows.ReadAsync(ct)) deleted.Add(rows.GetString(0));
		}
		JsonArray list = JsonNode.Parse(await WorkshopSnapshotRaw(actor, code, ct))?.AsArray() ?? new JsonArray();
		for (int index = list.Count - 1; index >= 0; index--)
		{
			JsonObject? item = list[index] as JsonObject;
			string id = item?["id"]?.GetValue<string>() ?? "";
			if (deleted.Contains(id)) { list.RemoveAt(index); continue; }
			if (item != null) item["canDelete"] = actor.CanDeleteWorkshop;
		}
		return list.ToJsonString();
	}

	public async Task<string?> DeleteWorkshopIntake(Guid id, string reason, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanDeleteWorkshop) return "Solo el administrador principal y Vittini pueden eliminar equipos de Taller.";
		await using SqlConnection connection = await Open(ct);
		await EnsureWorkshopSchema(connection, ct);
		await EnsureWorkshopCorrectionSchema(connection, ct);
		await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
		await using SqlCommand cmd = new SqlCommand("""
			IF EXISTS(SELECT 1 FROM dbo.workshop_intake_deletions WITH(UPDLOCK,HOLDLOCK) WHERE source_id=@id)
			BEGIN SELECT N'Este equipo ya fue eliminado de Taller.'; RETURN; END;
			IF NOT EXISTS(SELECT 1 FROM dbo.maintenance_movements m WITH(UPDLOCK,HOLDLOCK) WHERE m.id=@id AND (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY')))
			BEGIN SELECT N'El equipo no pertenece a un proceso válido de Taller.'; RETURN; END;
			INSERT dbo.workshop_intake_deletions(source_id,document_number,equipment,serial,department,original_created_at,original_created_by,reason,deleted_at,deleted_by,deleted_by_name,deleted_by_login)
			SELECT id,document_number,equipment_type,serial_number,department,created_at,created_by_name,@reason,SYSUTCDATETIME(),@user,@name,@login FROM dbo.maintenance_movements WHERE id=@id;
			SELECT N'';
			""", connection, tx);
		cmd.Parameters.AddWithValue("@id", id);
		cmd.Parameters.AddWithValue("@reason", reason);
		cmd.Parameters.AddWithValue("@user", actor.Id);
		cmd.Parameters.AddWithValue("@name", actor.Name);
		cmd.Parameters.AddWithValue("@login", actor.Login);
		string? error = (string?)await cmd.ExecuteScalarAsync(ct);
		if (!string.IsNullOrEmpty(error)) { await tx.RollbackAsync(ct); return error; }
		await tx.CommitAsync(ct);
		return null;
	}

	public async Task<string?> UpdateWorkshopIntake(Guid id, WorkshopIntakeUpdate body, WorkshopActor actor, CancellationToken ct)
	{
		if (!actor.CanDeleteWorkshop) return "No tienes permiso para editar equipos de Taller.";
		string equipment = body.Equipment?.Trim() ?? "";
		string serial = body.Serial?.Trim().ToUpperInvariant() ?? "";
		string failure = body.Failure?.Trim() ?? "";
		string? notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim();
		if (equipment.Length is < 2 or > 100 || serial.Length is < 1 or > 120 || failure.Length is < 2 or > 200 || (notes?.Length ?? 0) > 2000)
			return "Completa equipo, serial y falla con valores válidos.";
		await using SqlConnection connection = await Open(ct);
		await EnsureWorkshopSchema(connection, ct);
		await EnsureWorkshopCorrectionSchema(connection, ct);
		await using SqlTransaction tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
		await using SqlCommand cmd = new SqlCommand("""
			IF NOT EXISTS(SELECT 1 FROM dbo.maintenance_movements WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND (movement_type='TRANSFER_TO_WORKSHOP' OR (operational_area='WORKSHOP' AND movement_type='ENTRY')) AND NOT EXISTS(SELECT 1 FROM dbo.workshop_intake_deletions d WHERE d.source_id=@id))
			BEGIN SELECT N'El equipo no pertenece a un proceso activo de Taller.'; RETURN; END;
			IF EXISTS(SELECT 1 FROM dbo.maintenance_movements m LEFT JOIN dbo.maintenance_workshop_cases c ON c.source_id=m.id WHERE m.id<>@id AND UPPER(LTRIM(RTRIM(m.serial_number)))=@serial AND (m.movement_type='TRANSFER_TO_WORKSHOP' OR (m.operational_area='WORKSHOP' AND m.movement_type='ENTRY')) AND COALESCE(c.status,'SENT') NOT IN('DELIVERED','DISCHARGED') AND NOT EXISTS(SELECT 1 FROM dbo.workshop_intake_deletions d WHERE d.source_id=m.id))
			BEGIN SELECT N'El serial ya está activo en otro registro de Taller.'; RETURN; END;
			DECLARE @previous nvarchar(max)=(SELECT equipment_type equipment,serial_number serial,failure_cause failure,notes FROM dbo.maintenance_movements WHERE id=@id FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
			UPDATE dbo.maintenance_movements SET equipment_type=@equipment,serial_number=@serial,failure_cause=@failure,notes=@notes WHERE id=@id;
			INSERT dbo.workshop_intake_edits(id,source_id,previous_data,new_data,edited_at,edited_by,edited_by_name,edited_by_login)
			VALUES(NEWID(),@id,@previous,@newData,SYSUTCDATETIME(),@user,@name,@login);
			SELECT N'';
			""", connection, tx);
		cmd.Parameters.AddWithValue("@id", id);
		cmd.Parameters.AddWithValue("@equipment", equipment);
		cmd.Parameters.AddWithValue("@serial", serial);
		cmd.Parameters.AddWithValue("@failure", failure);
		cmd.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@newData", JsonSerializer.Serialize(new { equipment, serial, failure, notes }));
		cmd.Parameters.AddWithValue("@user", actor.Id);
		cmd.Parameters.AddWithValue("@name", actor.Name);
		cmd.Parameters.AddWithValue("@login", actor.Login);
		string? error = (string?)await cmd.ExecuteScalarAsync(ct);
		if (!string.IsNullOrEmpty(error)) { await tx.RollbackAsync(ct); return error; }
		await tx.CommitAsync(ct);
		return null;
	}
}
