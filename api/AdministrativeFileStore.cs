using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

record AdministrativeFolder(Guid Id, string Name, Guid? ParentId, string Department, bool IsShared, string CreatedBy, DateTime CreatedAt) { public List<string> SharedUserIds {get;init;}=[]; public bool CanManage {get;init;} public bool CanWrite {get;init;} }

partial class Database
{
    static readonly SemaphoreSlim AdministrativeFolderSchemaLock = new(1, 1);
    static bool administrativeFolderSchemaReady;
    static bool SameFolderUser(string? left,string? right)=>string.Equals(left?.Trim(),right?.Trim(),StringComparison.OrdinalIgnoreCase);

    static async Task EnsureAdministrativeFolderSchema(SqlConnection connection, CancellationToken ct)
    {
        if (administrativeFolderSchemaReady) return;
        await AdministrativeFolderSchemaLock.WaitAsync(ct);
        try
        {
            if (administrativeFolderSchemaReady) return;
            // DDL is intentionally dynamic: SQL Server compiles a batch before it
            // executes ALTER TABLE, so a newly added column cannot be referenced by
            // later statements in that same static batch.
            await using var command = new SqlCommand("""
                IF OBJECT_ID(N'dbo.administrative_document_folders', N'U') IS NULL
                BEGIN
                    EXEC(N'CREATE TABLE dbo.administrative_document_folders (
                        id uniqueidentifier NOT NULL PRIMARY KEY,
                        name nvarchar(120) NOT NULL,
                        parent_id uniqueidentifier NULL REFERENCES dbo.administrative_document_folders(id),
                        department nvarchar(80) NOT NULL,
                        is_shared bit NOT NULL DEFAULT 0,
                        created_by nvarchar(80) NOT NULL,
                        created_at datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
                    );
                    CREATE INDEX IX_administrative_document_folders_department_parent
                    ON dbo.administrative_document_folders(department,parent_id,name);');
                END
                ELSE
                BEGIN
                    IF COL_LENGTH(N'dbo.administrative_document_folders', N'department') IS NULL
                        EXEC(N'ALTER TABLE dbo.administrative_document_folders ADD department nvarchar(80) NULL;');

                    IF COL_LENGTH(N'dbo.administrative_document_folders', N'is_shared') IS NULL
                        EXEC(N'ALTER TABLE dbo.administrative_document_folders ADD is_shared bit NOT NULL CONSTRAINT DF_administrative_document_folders_is_shared DEFAULT 0;');

                    IF EXISTS (
                        SELECT 1 FROM sys.columns
                        WHERE object_id=OBJECT_ID(N'dbo.administrative_document_folders')
                          AND name=N'department' AND is_nullable=1
                    )
                    BEGIN
                        EXEC(N'UPDATE dbo.administrative_document_folders SET department=N''Administración'' WHERE department IS NULL;');
                        EXEC(N'ALTER TABLE dbo.administrative_document_folders ALTER COLUMN department nvarchar(80) NOT NULL;');
                    END;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE object_id=OBJECT_ID(N'dbo.administrative_document_folders')
                          AND name=N'IX_administrative_document_folders_department_parent'
                    )
                        EXEC(N'CREATE INDEX IX_administrative_document_folders_department_parent ON dbo.administrative_document_folders(department,parent_id,name);');
                END;
                IF COL_LENGTH(N'dbo.administrative_document_folders',N'shared_user_ids') IS NULL
                    EXEC(N'ALTER TABLE dbo.administrative_document_folders ADD shared_user_ids nvarchar(max) NOT NULL CONSTRAINT DF_folder_shared_users DEFAULT N''[]'' CHECK(ISJSON(shared_user_ids)=1);');
                """, connection);
            await command.ExecuteNonQueryAsync(ct);
            administrativeFolderSchemaReady = true;
        }
        finally { AdministrativeFolderSchemaLock.Release(); }
    }

    static async Task<List<AdministrativeFolder>> LoadAdministrativeFolders(SqlConnection c,string? department,CancellationToken ct,SqlTransaction? tx=null)
    {
        var rows=new List<AdministrativeFolder>();
        await using var cmd=new SqlCommand("SELECT id,name,parent_id,department,is_shared,created_by,created_at,shared_user_ids FROM dbo.administrative_document_folders "+(tx is null?"":"WITH(UPDLOCK,HOLDLOCK) ")+(department is null?"":"WHERE department=@dept ")+"ORDER BY name",c,tx);
        if(department is not null)cmd.Parameters.AddWithValue("@dept",department);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))rows.Add(new(r.GetGuid(0),r.GetString(1),r.IsDBNull(2)?null:r.GetGuid(2),r.GetString(3),r.GetBoolean(4),r.GetString(5),DateTime.SpecifyKind(r.GetDateTime(6),DateTimeKind.Utc)){SharedUserIds=JsonSerializer.Deserialize<List<string>>(r.GetString(7))??[]});
        return rows;
    }
    static AdministrativeFolder? FolderRoot(Guid id,List<AdministrativeFolder> folders)
    {
        var seen=new HashSet<Guid>();var folder=folders.Find(f=>f.Id==id);
        while(folder is not null&&seen.Add(folder.Id)){
            if(folder.ParentId is not Guid parent)return folder;
            folder=folders.Find(f=>f.Id==parent);
        }
        return null;
    }
    static bool FolderReadable(AdministrativeFolder folder,List<AdministrativeFolder> folders,DocumentActor actor)
    {
        var root=FolderRoot(folder.Id,folders);
        return actor.Allowed&&root is not null&&(SameFolderUser(root.CreatedBy,actor.Id)||root.SharedUserIds.Any(id=>SameFolderUser(id,actor.Id)));
    }
    static bool FolderWritable(AdministrativeFolder folder,List<AdministrativeFolder> folders,DocumentActor actor)=>FolderReadable(folder,folders,actor);
    public async Task<List<AdministrativeFolder>> AdministrativeFolders(DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        var rows=await LoadAdministrativeFolders(c,null,ct);
        return rows.Where(f=>FolderReadable(f,rows,actor)).Select(f=>f with {IsShared=FolderRoot(f.Id,rows)!.SharedUserIds.Count>0,SharedUserIds=[..FolderRoot(f.Id,rows)!.SharedUserIds],CanManage=SameFolderUser(FolderRoot(f.Id,rows)?.CreatedBy,actor.Id),CanWrite=FolderWritable(f,rows,actor)}).ToList();
    }
    static async Task ApplyAdministrativeFolderAccess(SqlConnection c,IEnumerable<InstitutionalFile> files,DocumentActor actor,CancellationToken ct,SqlTransaction? tx=null)
    {
        var scoped=files.Where(f=>f.Area=="files"&&f.FolderId is not null).ToList();if(scoped.Count==0)return;
        if(tx is null)await EnsureAdministrativeFolderSchema(c,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);
        foreach(var file in scoped){
            var folder=folders.Find(f=>f.Id==file.FolderId);
            // The folder id is the authoritative access boundary. Older files can
            // retain the uploader's former department in metadata after a move;
            // that stale label must not block an authorized collaborator.
            file.FolderReadAllowed=folder is not null&&FolderReadable(folder,folders,actor);
            file.FolderWriteAllowed=folder is not null&&FolderWritable(folder,folders,actor);
        }
    }
    public async Task<bool> AdministrativeFolderWritable(Guid id,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct);var folder=folders.Find(f=>f.Id==id);
        return folder is not null&&FolderWritable(folder,folders,actor);
    }
    public async Task<string?> AdministrativeFolderDepartment(Guid id,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct);var folder=folders.Find(f=>f.Id==id);
        return folder is not null&&FolderWritable(folder,folders,actor)?folder.Department:null;
    }
    public async Task<AdministrativeFolder?> CreateAdministrativeFolder(string name,Guid? parentId,DocumentActor actor,List<string>? userIds,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);
        var users=(userIds??[]).Where(id=>!SameFolderUser(id,actor.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var targetDepartment=actor.Department;
        if(parentId is Guid parent){var folder=folders.Find(f=>f.Id==parent);if(folder is null||!FolderWritable(folder,folders,actor))return null;users=[..FolderRoot(parent,folders)!.SharedUserIds];targetDepartment=folder.Department;}
        // Existing access is inherited from the root. An inactive participant
        // must not prevent the remaining collaborators from creating children.
        if(parentId is null&&!await ValidFolderRecipients(c,tx,users,ct))return null;
        var id=Guid.NewGuid();await using var cmd=new SqlCommand("INSERT dbo.administrative_document_folders(id,name,parent_id,department,is_shared,created_by,shared_user_ids) VALUES(@id,@name,@parent,@dept,@shared,@actor,@users)",c,tx);
        cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@parent",(object?)parentId??DBNull.Value);cmd.Parameters.AddWithValue("@dept",targetDepartment);cmd.Parameters.AddWithValue("@shared",users.Count>0);cmd.Parameters.AddWithValue("@actor",actor.Id);cmd.Parameters.AddWithValue("@users",JsonSerializer.Serialize(users));await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
        return new(id,name,parentId,targetDepartment,users.Count>0,actor.Id,DateTime.UtcNow){SharedUserIds=users,CanManage=parentId is null,CanWrite=true};
    }
    static async Task<bool> ValidFolderRecipients(SqlConnection c,SqlTransaction tx,List<string> ids,CancellationToken ct)
    {
        if(ids.Count>20)return false;if(ids.Count==0)return true;
        var users=await ReadInstitutionalUsers(c,ct,tx);
        return ids.All(id=>users.Any(u=>SameFolderUser(u.UserName,id)));
    }
    public async Task<string?> SetAdministrativeFolderRecipients(Guid id,List<string> ids,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);var folder=folders.Find(f=>f.Id==id);
        if(folder is null||!FolderReadable(folder,folders,actor)||!SameFolderUser(folder.CreatedBy,actor.Id))return "Solo quien creó la carpeta puede administrar sus accesos.";
        if(folder.ParentId is not null)return "Los accesos se administran desde la carpeta principal y se heredan en sus subcarpetas.";
        ids=ids.Where(user=>!SameFolderUser(user,actor.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(!await ValidFolderRecipients(c,tx,ids,ct))return "Selecciona hasta 20 usuarios activos.";
        await using var cmd=new SqlCommand("UPDATE dbo.administrative_document_folders SET shared_user_ids=@users,is_shared=@shared WHERE id=@id",c,tx);
        cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@users",JsonSerializer.Serialize(ids));cmd.Parameters.AddWithValue("@shared",ids.Count>0);await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
    public async Task<bool> RenameAdministrativeFolder(Guid id,string name,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);var folder=folders.Find(f=>f.Id==id);
        if(folder is null||!FolderWritable(folder,folders,actor))return false;
        await using var cmd=new SqlCommand("UPDATE dbo.administrative_document_folders SET name=@name WHERE id=@id AND department=@dept",c,tx);cmd.Parameters.AddWithValue("@name",name);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@dept",folder.Department);cmd.Parameters.AddWithValue("@actor",actor.Id);var renamed=await cmd.ExecuteNonQueryAsync(ct)==1;await tx.CommitAsync(ct);return renamed;
    }
    public async Task<string?> MoveAdministrativeFolder(Guid id,Guid? parentId,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);var folder=folders.Find(f=>f.Id==id);
        if(folder is null||!FolderWritable(folder,folders,actor))return "Solo quien creó la carpeta puede moverla.";
        var parent=folders.Find(f=>f.Id==parentId);
        if(parentId is not null&&(parent is null||!FolderWritable(parent,folders,actor)))return "Selecciona una carpeta propia del departamento.";
        var sourceRoot=FolderRoot(id,folders)!;
        var destinationRoot=parentId is Guid destination?FolderRoot(destination,folders):null;
        if(sourceRoot.Id!=destinationRoot?.Id&&!SameFolderUser(sourceRoot.CreatedBy,actor.Id))return "Solo el creador puede cambiar los accesos al mover una carpeta fuera de su carpeta principal.";
        var seen=new HashSet<Guid>{id};while(parent is not null){if(!seen.Add(parent.Id))return "Una carpeta no puede moverse dentro de sí misma.";parent=folders.Find(f=>f.Id==parent.ParentId);}
        // Detaching a subfolder retains the recipients it inherited before the move.
        var users=FolderRoot(id,folders)!.SharedUserIds;
        await using var cmd=new SqlCommand("UPDATE dbo.administrative_document_folders SET parent_id=@parent,shared_user_ids=@users,is_shared=@shared WHERE id=@id",c,tx);
        cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@parent",(object?)parentId??DBNull.Value);cmd.Parameters.AddWithValue("@users",JsonSerializer.Serialize(users));cmd.Parameters.AddWithValue("@shared",users.Count>0);await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
    public async Task<bool> DeleteAdministrativeFolder(Guid id,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureInstitutionalSchema(c,ct);await EnsureAdministrativeFolderSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);var folder=folders.Find(f=>f.Id==id);
        if(folder is null||!FolderWritable(folder,folders,actor))return false;
        await using var cmd=new SqlCommand("DELETE FROM dbo.administrative_document_folders WHERE id=@id AND department=@dept AND NOT EXISTS(SELECT 1 FROM dbo.administrative_document_folders WHERE parent_id=@id) AND NOT EXISTS(SELECT 1 FROM dbo.institutional_files WHERE TRY_CONVERT(uniqueidentifier,JSON_VALUE(metadata,'$.folderId'))=@id)",c,tx);
        cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@dept",folder.Department);cmd.Parameters.AddWithValue("@actor",actor.Id);var result=await cmd.ExecuteNonQueryAsync(ct)==1;await tx.CommitAsync(ct);return result;
    }
    public async Task<string?> MoveAdministrativeFile(long id,Guid? folderId,string department,DocumentActor actor,CancellationToken ct)
    {
        await using var c=await Open(ct);await EnsureAdministrativeFolderSchema(c,ct);await EnsureInstitutionalSchema(c,ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var folders=await LoadAdministrativeFolders(c,null,ct,tx);
        if(folderId is Guid target){var folder=folders.Find(f=>f.Id==target);if(folder is null||!FolderWritable(folder,folders,actor))return "Selecciona una carpeta compartida contigo.";department=folder.Department;}
        else if(!string.Equals(department,actor.Department,StringComparison.OrdinalIgnoreCase))return "Selecciona tu propio departamento.";
        await using var read=new SqlCommand("SELECT metadata FROM dbo.institutional_files WITH(UPDLOCK,HOLDLOCK) WHERE id=@id AND area='files'",c,tx);
        read.Parameters.AddWithValue("@id",id);var json=await read.ExecuteScalarAsync(ct) as string;if(json is null)return "El archivo no está disponible.";
        var file=JsonSerializer.Deserialize<InstitutionalFile>(json,InstitutionalJson)!;
        await ApplyAdministrativeFolderAccess(c,[file],actor,ct,tx);if(file.Trashed||!file.Accessible(actor)||(!file.FolderWriteAllowed&&!SameFolderUser(file.OwnerUserName,actor.Id)))return "El archivo no está disponible.";
        file.FolderId=folderId;
        // Old automatic cross-department shares must not survive leaving a folder.
        file.SharedWith.RemoveAll(access=>access.Id<0);
        await using var save=new SqlCommand("UPDATE dbo.institutional_files SET metadata=@json WHERE id=@id",c,tx);save.Parameters.AddWithValue("@id",id);save.Parameters.AddWithValue("@json",JsonSerializer.Serialize(file,InstitutionalJson));await save.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return null;
    }
}
