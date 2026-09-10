using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

#if MSSQL_LEGACY
sealed class Database
{
    readonly List<(string Value, string Mode)> connections = [];
    readonly string? configurationError;

    public Database(IConfiguration configuration)
    {
        var raw = FirstValue(
            configuration.GetConnectionString("DefaultConnection"),
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
            configuration["MSSQL_CONNECTION_STRING"],
            Environment.GetEnvironmentVariable("MSSQL_CONNECTION_STRING"));

        if (string.IsNullOrWhiteSpace(raw))
        {
            configurationError = "No se encontró la conexión. Agrega ConnectionStrings__DefaultConnection en MonsterASP y reinicia el sitio.";
            return;
        }

        try
        {
            raw = raw.Trim();
            foreach (var prefix in new[] { "ConnectionStrings__DefaultConnection=", "MSSQL_CONNECTION_STRING=" })
            {
                if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw[prefix.Length..].Trim();
                    break;
                }
            }

            if (raw.Length >= 2 && ((raw[0] == '"' && raw[^1] == '"') || (raw[0] == '\'' && raw[^1] == '\'')))
                raw = raw[1..^1];

            var configured = new SqlConnectionStringBuilder(raw);
            if (string.IsNullOrWhiteSpace(configured.DataSource) ||
                string.IsNullOrWhiteSpace(configured.InitialCatalog) ||
                string.IsNullOrWhiteSpace(configured.UserID))
            {
                configurationError = "La conexión de SQL Server está incompleta: debe contener Server, Database y User Id.";
                return;
            }

            if (configured.ConnectTimeout == 0) configured.ConnectTimeout = 30;
            var internalHost = configured.DataSource.Replace(
                ".public.databaseasp.net",
                ".databaseasp.net",
                StringComparison.OrdinalIgnoreCase);

            if (!string.Equals(internalHost, configured.DataSource, StringComparison.OrdinalIgnoreCase))
            {
                var internalConnection = new SqlConnectionStringBuilder(configured.ConnectionString)
                {
                    DataSource = internalHost
                };
                connections.Add((internalConnection.ConnectionString, "monster-internal"));
            }

            connections.Add((
                configured.ConnectionString,
                configured.DataSource.Contains(".public.databaseasp.net", StringComparison.OrdinalIgnoreCase)
                    ? "monster-public"
                    : "configured"));

            connections = connections.DistinctBy(item => item.Value, StringComparer.Ordinal).ToList();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"SQL Server connection configuration: {error.Message}");
            configurationError = "El formato de la conexión de SQL Server no es válido. Copia la cadena completa desde el panel de MonsterASP.";
        }
    }

    static string? FirstValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    static object Db(object? value) => value ?? DBNull.Value;

    async Task<SqlConnection> Open(CancellationToken ct)
    {
        if (configurationError is not null)
            throw new InvalidOperationException("MSSQL_CONFIG:" + configurationError);

        Exception? last = null;
        foreach (var candidate in connections)
        {
            var connection = new SqlConnection(candidate.Value);
            try
            {
                await connection.OpenAsync(ct);
                return connection;
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                last = error;
                Console.Error.WriteLine($"SQL Server {candidate.Mode}: {error.Message}");
                await connection.DisposeAsync();
            }
        }

        throw last ?? new InvalidOperationException("MSSQL_CONFIG:No existe una conexión de SQL Server utilizable.");
    }

    static byte[] PasswordHash(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    static string ClaimRole(string databaseRole) =>
        databaseRole == "ADMINISTRATOR" ? "Administrator" : "Viewer";

    public async Task<AdminIdentity?> AuthenticateUser(string email, string password, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        string id, storedEmail, name, role;
        byte[] salt, hash;
        int iterations;

        const string sql = """
            SELECT TOP (1) id,email,display_name,role,password_salt,password_hash,password_iterations
            FROM dbo.admin_users
            WHERE email=@email AND is_active=1;
            """;
        await using (var command = new SqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@email", email);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            id = reader.GetString(0);
            storedEmail = reader.GetString(1);
            name = reader.GetString(2);
            role = reader.GetString(3);
            salt = (byte[])reader[4];
            hash = (byte[])reader[5];
            iterations = reader.GetInt32(6);
        }

        var candidate = PasswordHash(password, salt, iterations);
        if (candidate.Length != hash.Length || !CryptographicOperations.FixedTimeEquals(candidate, hash)) return null;

        await using (var update = new SqlCommand(
            "UPDATE dbo.admin_users SET last_login_at=SYSUTCDATETIME(),updated_at=SYSUTCDATETIME() WHERE id=@id;",
            connection))
        {
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(ct);
        }

        return new(id, storedEmail, name, ClaimRole(role));
    }

    public async Task<List<AdminUserRow>> AdminUsers(CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand(
            "SELECT id,email,display_name,role,is_active,last_login_at,created_at FROM dbo.admin_users ORDER BY role,email;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var users = new List<AdminUserRow>();
        while (await reader.ReadAsync(ct)) users.Add(ReadAdminUser(reader));
        return users;
    }

    async Task<AdminUserRow?> AdminUser(Guid id, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand(
            "SELECT id,email,display_name,role,is_active,last_login_at,created_at FROM dbo.admin_users WHERE id=@id;",
            connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadAdminUser(reader) : null;
    }

    static AdminUserRow ReadAdminUser(SqlDataReader reader) => new(
        Guid.Parse(reader.GetString(0)),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetBoolean(4),
        reader.IsDBNull(5) ? null : reader.GetDateTime(5),
        reader.GetDateTime(6));

    public async Task<AdminUserRow> CreateAdminUser(AdminUserCreate body, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = PasswordHash(body.Password!, salt, 210000);
        await using var connection = await Open(ct);
        const string sql = """
            INSERT INTO dbo.admin_users
              (id,email,display_name,role,password_salt,password_hash,password_iterations,is_active)
            VALUES(@id,@email,@name,@role,@salt,@hash,210000,1);
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@email", body.Email!.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("@name", body.DisplayName!.Trim());
        command.Parameters.AddWithValue("@role", body.Role);
        command.Parameters.AddWithValue("@salt", salt);
        command.Parameters.AddWithValue("@hash", hash);
        await command.ExecuteNonQueryAsync(ct);
        return (await AdminUser(id, ct))!;
    }

    public async Task<AdminUserRow?> UpdateAdminUser(Guid id, AdminUserUpdate body, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        var hasPassword = !string.IsNullOrEmpty(body.Password);
        var sql = hasPassword
            ? "UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,is_active=@active,password_salt=@salt,password_hash=@hash,password_iterations=210000,updated_at=SYSUTCDATETIME() WHERE id=@id;"
            : "UPDATE dbo.admin_users SET email=@email,display_name=@name,role=@role,is_active=@active,updated_at=SYSUTCDATETIME() WHERE id=@id;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@email", body.Email!.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("@name", body.DisplayName!.Trim());
        command.Parameters.AddWithValue("@role", body.Role);
        command.Parameters.AddWithValue("@active", body.IsActive);
        if (hasPassword)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            command.Parameters.AddWithValue("@salt", salt);
            command.Parameters.AddWithValue("@hash", PasswordHash(body.Password!, salt, 210000));
        }

        if (await command.ExecuteNonQueryAsync(ct) == 0) return null;
        return await AdminUser(id, ct);
    }

    public async Task<DatabaseHealth> Health(CancellationToken ct)
    {
        if (configurationError is not null) return new(false, "MSSQL_CONFIG", configurationError, null);
        try
        {
            await using var connection = await Open(ct);
            await using (var command = new SqlCommand("SELECT 1;", connection))
                await command.ExecuteScalarAsync(ct);

            const string schemaSql = """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='agencies';
                """;
            await using (var schema = new SqlCommand(schemaSql, connection))
            {
                if (Convert.ToInt32(await schema.ExecuteScalarAsync(ct)) == 0)
                    return new(false, "MSSQL_SCHEMA", "SQL Server conectó, pero falta dbo.agencies. Ejecuta 01_EJECUTAR_EN_SQL_SERVER.sql.", ConnectionMode(connection.ConnectionString));
            }

            return new(true, "OK", "Conexión de SQL Server activa.", ConnectionMode(connection.ConnectionString));
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            Console.Error.WriteLine(error);
            var diagnostic = DescribeConnectionError(error);
            return new(false, diagnostic.Code, diagnostic.Message, null);
        }
    }

    static string ConnectionMode(string value)
    {
        var builder = new SqlConnectionStringBuilder(value);
        return builder.DataSource.Contains(".public.databaseasp.net", StringComparison.OrdinalIgnoreCase)
            ? "monster-public"
            : builder.DataSource.EndsWith(".databaseasp.net", StringComparison.OrdinalIgnoreCase)
                ? "monster-internal"
                : "configured";
    }

    public static DatabaseDiagnostic DescribeConnectionError(Exception error)
    {
        if (error is InvalidOperationException invalid && invalid.Message.StartsWith("MSSQL_CONFIG:", StringComparison.Ordinal))
            return new("MSSQL_CONFIG", invalid.Message["MSSQL_CONFIG:".Length..]);
        if (error is TimeoutException)
            return new("MSSQL_TIMEOUT", "SQL Server tardó demasiado en responder. Verifica el servidor y reinicia el sitio.");
        if (error is SqlException sql)
        {
            if (sql.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                sql.Message.Contains("certificado", StringComparison.OrdinalIgnoreCase))
                return new("MSSQL_TLS", "SQL Server rechazó el certificado TLS. Usa Encrypt=True;TrustServerCertificate=True en la cadena de MonsterASP.");

            return sql.Number switch
            {
                18456 => new("MSSQL_AUTH", "SQL Server rechazó el usuario o la contraseña. Copia nuevamente la cadena desde MonsterASP."),
                4060 => new("MSSQL_DATABASE", "SQL Server no pudo abrir la base indicada. Verifica Database y los permisos del usuario."),
                208 => new("MSSQL_SCHEMA", "SQL Server conectó, pero falta una tabla. Ejecuta los archivos T-SQL de configuración en orden."),
                -2 or 40 or 53 or 258 or 11001 => new("MSSQL_HOST", "No se pudo alcanzar SQL Server. En MonsterASP se probará primero el host interno y después el host público configurado."),
                _ => new($"MSSQL_{sql.Number}", "SQL Server rechazó la conexión o la consulta. Revisa el registro de la aplicación en MonsterASP.")
            };
        }

        return new("MSSQL_CONNECTION", "No fue posible abrir la conexión con SQL Server. Revisa las variables del sitio y reinícialo.");
    }

    public async Task<List<GroupRow>> Groups(CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT grupo,COUNT(*) AS pending
            FROM dbo.agencies
            WHERE status='PENDING'
            GROUP BY grupo
            ORDER BY grupo;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var groups = new List<GroupRow>();
        while (await reader.ReadAsync(ct)) groups.Add(new(reader.GetString(0), reader.GetInt32(1)));
        return groups;
    }

    public async Task<List<AgencyRow>> Agencies(string group, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT id,codigo,terminal,expected_latitude,expected_longitude
            FROM dbo.agencies
            WHERE status='PENDING' AND grupo=@group
            ORDER BY terminal;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@group", group);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var agencies = new List<AgencyRow>();
        while (await reader.ReadAsync(ct))
            agencies.Add(new(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4)));
        return agencies;
    }

    public async Task<DashboardRow> Dashboard(CancellationToken ct)
    {
        await using var connection = await Open(ct);
        var groups = new List<DashboardGroup>();
        var total = 0;
        var completed = 0;
        var pending = 0;
        var review = 0;
        const string groupsSql = """
            SELECT a.grupo,
                   COUNT(*) AS total,
                   SUM(CASE WHEN a.status='COMPLETED' THEN 1 ELSE 0 END) AS completed,
                   SUM(CASE WHEN a.status='PENDING' THEN 1 ELSE 0 END) AS pending,
                   SUM(CASE WHEN a.status='REVIEW_REQUIRED' THEN 1 ELSE 0 END) AS review_count,
                   MAX(p.submitted_at) AS last_updated
            FROM dbo.agencies a
            LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id
            GROUP BY a.grupo
            ORDER BY a.grupo;
            """;
        await using (var command = new SqlCommand(groupsSql, connection))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var groupTotal = reader.GetInt32(1);
                var groupCompleted = Convert.ToInt32(reader.GetValue(2));
                var groupPending = Convert.ToInt32(reader.GetValue(3));
                var groupReview = Convert.ToInt32(reader.GetValue(4));
                groups.Add(new(
                    reader.GetString(0),
                    groupTotal,
                    groupCompleted,
                    groupPending,
                    groupTotal == 0 ? 0 : (int)Math.Round(groupCompleted * 100d / groupTotal),
                    reader.IsDBNull(5) ? null : reader.GetDateTime(5)));
                total += groupTotal;
                completed += groupCompleted;
                pending += groupPending;
                review += groupReview;
            }
        }

        var audits = new List<CompletedAudit>();
        const string auditsSql = """
            SELECT TOP (200) a.codigo,a.terminal,a.grupo,p.municipio,p.provincia,
                   p.tipo_establecimiento,p.latitude,p.longitude,p.accuracy_meters,
                   p.submitted_at,p.id,p.observations,p.employee_name,
                   COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),
                            (SELECT TOP (1) NULLIF(u.email,'') FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')),
                            (SELECT TOP (1) NULLIF(l.actor_email,'') FROM dbo.audit_log l WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED' ORDER BY l.created_at),
                            (SELECT TOP (1) NULLIF(u.email,'')
                             FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id
                             WHERE ug.group_name=a.grupo AND u.role='GROUP_ADMIN' AND u.is_active=1
                             ORDER BY u.display_name,u.email))
            FROM dbo.agencies a
            INNER JOIN dbo.agency_profiles p ON p.agency_id=a.id
            ORDER BY p.submitted_at DESC;
            """;
        await using (var command = new SqlCommand(auditsSql, connection))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                audits.Add(new(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), reader.GetString(5),
                    reader.GetDouble(6), reader.GetDouble(7), reader.GetDouble(8),
                    DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc), Guid.Parse(reader.GetString(10)),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetString(13)));
        }

        var percent = total == 0 ? 0 : (int)Math.Round(completed * 100d / total);
        return new(total, completed, pending, review, percent, groups.Count, groups.Count(group => group.Percent == 100), groups, audits);
    }

    public async Task<AuditDetailRow?> AuditDetail(Guid id, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        string codigo, terminal, grupo, municipio, provincia, type, employeeName, direccion, sector;
        string? employeeCode, observations, submittedByLogin;
        double latitude, longitude, accuracy;
        DateTime submittedAt;
        System.Text.Json.JsonElement answers;
        const string detailSql = """
            SELECT a.codigo,a.terminal,a.grupo,p.municipio,p.provincia,p.tipo_establecimiento,
                   p.latitude,p.longitude,p.accuracy_meters,p.submitted_at,p.observations,
                   p.employee_name,p.employee_code,p.direccion,p.sector,p.structural_answers,
                   COALESCE(NULLIF(JSON_VALUE(p.submitted_by_context,'$.login'),''),
                            (SELECT TOP (1) NULLIF(u.email,'') FROM dbo.admin_users u WHERE CONVERT(nvarchar(80),u.id)=JSON_VALUE(p.submitted_by_context,'$.userId')),
                            (SELECT TOP (1) NULLIF(l.actor_email,'') FROM dbo.audit_log l WHERE l.entity_type='agency_profile' AND l.entity_id=p.id AND l.action='PROFILE_CREATED' ORDER BY l.created_at),
                            (SELECT TOP (1) NULLIF(u.email,'')
                             FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id
                             WHERE ug.group_name=a.grupo AND u.role='GROUP_ADMIN' AND u.is_active=1
                             ORDER BY u.display_name,u.email))
            FROM dbo.agencies a
            INNER JOIN dbo.agency_profiles p ON p.agency_id=a.id
            WHERE p.id=@id;
            """;
        await using (var command = new SqlCommand(detailSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            codigo = reader.GetString(0);
            terminal = reader.GetString(1);
            grupo = reader.GetString(2);
            municipio = reader.GetString(3);
            provincia = reader.GetString(4);
            type = reader.GetString(5);
            latitude = reader.GetDouble(6);
            longitude = reader.GetDouble(7);
            accuracy = reader.GetDouble(8);
            submittedAt = DateTime.SpecifyKind(reader.GetDateTime(9), DateTimeKind.Utc);
            observations = reader.IsDBNull(10) ? null : reader.GetString(10);
            employeeName = reader.IsDBNull(11) ? "Sin nombre registrado" : reader.GetString(11);
            employeeCode = reader.IsDBNull(12) ? null : reader.GetString(12);
            direccion = reader.GetString(13);
            sector = reader.GetString(14);
            using var document = System.Text.Json.JsonDocument.Parse(reader.IsDBNull(15) ? "{}" : reader.GetString(15));
            answers = document.RootElement.Clone();
            submittedByLogin = reader.IsDBNull(16) ? null : reader.GetString(16);
        }

        var photos = new List<AuditPhotoRow>();
        const string photosSql = """
            SELECT photo_type,latitude,longitude,accuracy_meters,captured_at
            FROM dbo.audit_photos
            WHERE profile_id=@id
            ORDER BY CASE photo_type WHEN 'frontal' THEN 1 WHEN 'operativa' THEN 2 WHEN 'entorno' THEN 3 ELSE 4 END;
            """;
        await using (var command = new SqlCommand(photosSql, connection))
        {
            command.Parameters.AddWithValue("@id", id.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var photoType = reader.GetString(0);
                photos.Add(new(
                    photoType,
                    $"/api/images/{id}/{photoType}",
                    reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3), reader.GetDateTime(4)));
            }
        }

        return new(codigo, terminal, grupo, municipio, provincia, type, latitude, longitude, accuracy,
            submittedAt, id, observations, employeeName, employeeCode, direccion, sector, answers, submittedByLogin, photos);
    }

    public async Task<AuditDetailRow?> UpdateAudit(Guid id, AuditUpdate body, string actor, CancellationToken ct)
    {
        var before = await AuditDetail(id, ct);
        if (before is null) return null;

        await using var connection = await Open(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            string? agencyId;
            await using (var find = new SqlCommand(
                "SELECT agency_id FROM dbo.agency_profiles WITH (UPDLOCK,ROWLOCK) WHERE id=@id;",
                connection,
                transaction))
            {
                find.Parameters.AddWithValue("@id", id.ToString());
                agencyId = (string?)await find.ExecuteScalarAsync(ct);
            }

            if (agencyId is null)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            const string agencySql = """
                UPDATE dbo.agencies
                SET codigo=@codigo,terminal=@terminal,grupo=@grupo,status='COMPLETED',updated_at=SYSUTCDATETIME()
                WHERE id=@agency;
                """;
            await using (var agency = new SqlCommand(agencySql, connection, transaction))
            {
                agency.Parameters.AddWithValue("@codigo", body.Codigo!.Trim());
                agency.Parameters.AddWithValue("@terminal", body.Terminal!.Trim());
                agency.Parameters.AddWithValue("@grupo", body.Grupo!.Trim());
                agency.Parameters.AddWithValue("@agency", agencyId);
                await agency.ExecuteNonQueryAsync(ct);
            }

            const string profileSql = """
                UPDATE dbo.agency_profiles
                SET employee_name=@employeeName,employee_code=@employeeCode,direccion=@direccion,
                    sector=@sector,municipio=@municipio,provincia=@provincia,latitude=@latitude,
                    longitude=@longitude,accuracy_meters=@accuracy,structural_answers=@answers,
                    observations=@observations,updated_at=SYSUTCDATETIME()
                WHERE id=@id;
                """;
            await using (var profile = new SqlCommand(profileSql, connection, transaction))
            {
                profile.Parameters.AddWithValue("@employeeName", body.EmployeeName!.Trim());
                profile.Parameters.AddWithValue("@employeeCode", Db(string.IsNullOrWhiteSpace(body.EmployeeCode) ? null : body.EmployeeCode.Trim()));
                profile.Parameters.AddWithValue("@direccion", body.Direccion!.Trim());
                profile.Parameters.AddWithValue("@sector", body.Sector!.Trim());
                profile.Parameters.AddWithValue("@municipio", body.Municipio!.Trim());
                profile.Parameters.AddWithValue("@provincia", body.Provincia!.Trim());
                profile.Parameters.AddWithValue("@latitude", body.Latitude);
                profile.Parameters.AddWithValue("@longitude", body.Longitude);
                profile.Parameters.AddWithValue("@accuracy", body.Accuracy);
                profile.Parameters.AddWithValue("@answers", System.Text.Json.JsonSerializer.Serialize(body.Answers));
                profile.Parameters.AddWithValue("@observations", Db(string.IsNullOrWhiteSpace(body.Observations) ? null : body.Observations.Trim()));
                profile.Parameters.AddWithValue("@id", id.ToString());
                await profile.ExecuteNonQueryAsync(ct);
            }

            const string logSql = """
                INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,after_data,actor_email)
                VALUES('agency_profile',@id,'PROFILE_CORRECTED',@before,@after,@actor);
                """;
            await using (var log = new SqlCommand(logSql, connection, transaction))
            {
                log.Parameters.AddWithValue("@id", id.ToString());
                log.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(before));
                log.Parameters.AddWithValue("@after", System.Text.Json.JsonSerializer.Serialize(body));
                log.Parameters.AddWithValue("@actor", actor);
                await log.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        return await AuditDetail(id, ct);
    }

    public async Task<bool> DeleteAudit(Guid id, string actor, CancellationToken ct)
    {
        var before = await AuditDetail(id, ct);
        if (before is null) return false;

        await using var connection = await Open(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            string? agencyId;
            await using (var find = new SqlCommand(
                "SELECT agency_id FROM dbo.agency_profiles WITH (UPDLOCK,ROWLOCK) WHERE id=@id;",
                connection,
                transaction))
            {
                find.Parameters.AddWithValue("@id", id.ToString());
                agencyId = (string?)await find.ExecuteScalarAsync(ct);
            }

            if (agencyId is null)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            await using (var delete = new SqlCommand("DELETE FROM dbo.agency_profiles WHERE id=@id;", connection, transaction))
            {
                delete.Parameters.AddWithValue("@id", id.ToString());
                await delete.ExecuteNonQueryAsync(ct);
            }

            await using (var reopen = new SqlCommand(
                "UPDATE dbo.agencies SET status='PENDING',updated_at=SYSUTCDATETIME() WHERE id=@agency;",
                connection,
                transaction))
            {
                reopen.Parameters.AddWithValue("@agency", agencyId);
                await reopen.ExecuteNonQueryAsync(ct);
            }

            const string logSql = """
                INSERT INTO dbo.audit_log(entity_type,entity_id,action,before_data,actor_email)
                VALUES('agency_profile',@id,'PROFILE_DELETED_AND_AGENCY_REOPENED',@before,@actor);
                """;
            await using (var log = new SqlCommand(logSql, connection, transaction))
            {
                log.Parameters.AddWithValue("@id", id.ToString());
                log.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(before));
                log.Parameters.AddWithValue("@actor", actor);
                await log.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Guid> Submit(Guid agency, Submit body, string? submittedByUserId, string? submittedByLogin, string? submittedByName, string? ip, string? agent, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var canonicalLogin = submittedByLogin?.Trim().ToLowerInvariant();
            var canonicalName = submittedByName?.Trim();
            if (!string.IsNullOrWhiteSpace(submittedByUserId))
            {
                const string submitterSql = "SELECT TOP (1) email,display_name FROM dbo.admin_users WHERE CONVERT(nvarchar(80),id)=@userId AND is_active=1;";
                await using var submitter = new SqlCommand(submitterSql, connection, transaction);
                submitter.Parameters.AddWithValue("@userId", submittedByUserId);
                await using var submitterReader = await submitter.ExecuteReaderAsync(ct);
                if (await submitterReader.ReadAsync(ct))
                {
                    canonicalLogin = submitterReader.GetString(0).Trim().ToLowerInvariant();
                    canonicalName = submitterReader.GetString(1).Trim();
                }
            }
            string? status = null;
            double? expectedLatitude = null;
            double? expectedLongitude = null;
            const string checkSql = """
                SELECT status,expected_latitude,expected_longitude
                FROM dbo.agencies WITH (UPDLOCK,ROWLOCK)
                WHERE id=@id;
                """;
            await using (var check = new SqlCommand(checkSql, connection, transaction))
            {
                check.Parameters.AddWithValue("@id", agency.ToString());
                await using var reader = await check.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    status = reader.GetString(0);
                    expectedLatitude = reader.IsDBNull(1) ? null : reader.GetDouble(1);
                    expectedLongitude = reader.IsDBNull(2) ? null : reader.GetDouble(2);
                }
            }

            if (status != "PENDING") throw new AgencyConflict();

            var id = Guid.NewGuid();
            var geo = body.Geolocation!;
            double? distance = expectedLatitude.HasValue && expectedLongitude.HasValue
                ? Haversine(geo.Latitude, geo.Longitude, expectedLatitude.Value, expectedLongitude.Value)
                : null;
            var afterData = System.Text.Json.JsonSerializer.Serialize(new
            {
                id,
                agencyId = agency,
                completionPercent = 100
            });

            const string insertSql = """
                INSERT INTO dbo.agency_profiles
                  (id,agency_id,employee_name,employee_code,direccion,sector,municipio,provincia,
                   tipo_establecimiento,tipo_establecimiento_otro,latitude,longitude,accuracy_meters,
                   distance_from_agency_meters,location_captured_at,location_source,structural_answers,
                   observations,completion_percent,submitted_by_context)
                VALUES
                  (@id,@agency,@employeeName,@employeeCode,@direccion,@sector,@municipio,@provincia,
                   @tipo,@otro,@lat,@lng,@accuracy,@distance,@captured,@source,@answers,@observations,100,@context);

                UPDATE dbo.agencies
                SET status='COMPLETED',updated_at=SYSUTCDATETIME()
                WHERE id=@agency;

                INSERT INTO dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
                VALUES('agency_profile',@id,'PROFILE_CREATED',@after,@login);
                """;
            await using var insert = new SqlCommand(insertSql, connection, transaction);
            insert.Parameters.AddWithValue("@id", id.ToString());
            insert.Parameters.AddWithValue("@agency", agency.ToString());
            insert.Parameters.AddWithValue("@employeeName", body.EmployeeName!.Trim());
            insert.Parameters.AddWithValue("@employeeCode", Db(string.IsNullOrWhiteSpace(body.EmployeeCode) ? null : body.EmployeeCode.Trim()));
            insert.Parameters.AddWithValue("@direccion", body.Direccion!.Trim());
            insert.Parameters.AddWithValue("@sector", body.Sector!.Trim());
            insert.Parameters.AddWithValue("@municipio", body.Municipio!.Trim());
            insert.Parameters.AddWithValue("@provincia", body.Provincia!.Trim());
            insert.Parameters.AddWithValue("@tipo", body.TipoEstablecimiento!);
            insert.Parameters.AddWithValue("@otro", DBNull.Value);
            insert.Parameters.AddWithValue("@lat", geo.Latitude);
            insert.Parameters.AddWithValue("@lng", geo.Longitude);
            insert.Parameters.AddWithValue("@accuracy", geo.AccuracyMeters);
            insert.Parameters.AddWithValue("@distance", Db(distance));
            insert.Parameters.AddWithValue("@captured", geo.CapturedAt!.Value.UtcDateTime);
            insert.Parameters.AddWithValue("@source", geo.Source!);
            insert.Parameters.AddWithValue("@answers", System.Text.Json.JsonSerializer.Serialize(body.Answers));
            insert.Parameters.AddWithValue("@observations", Db(string.IsNullOrWhiteSpace(body.Observations) ? null : body.Observations.Trim()));
            insert.Parameters.AddWithValue("@context", System.Text.Json.JsonSerializer.Serialize(new
            {
                userId = submittedByUserId,
                login = canonicalLogin,
                displayName = canonicalName,
                ip,
                userAgent = agent,
                submittedAt = DateTimeOffset.UtcNow
            }));
            insert.Parameters.AddWithValue("@after", afterData);
            insert.Parameters.AddWithValue("@login", Db(canonicalLogin));
            await insert.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return id;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return Math.Round(radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)), 1);
    }

    public async Task<bool> PhotoMatchesProfile(Guid id, double lat, double lng, double maximumMeters, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        await using var command = new SqlCommand(
            "SELECT latitude,longitude FROM dbo.agency_profiles WHERE id=@id;",
            connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) &&
               Haversine(lat, lng, reader.GetDouble(0), reader.GetDouble(1)) <= maximumMeters;
    }

    public async Task AttachPhoto(Guid id, string type, byte[] data, string mime, double lat, double lng,
        double accuracy, DateTime captured, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            IF EXISTS (SELECT 1 FROM dbo.audit_photos WHERE profile_id=@id AND photo_type=@type)
            BEGIN
                UPDATE dbo.audit_photos
                SET photo_data=@data,content_type=@mime,latitude=@lat,longitude=@lng,
                    accuracy_meters=@accuracy,captured_at=@captured,created_at=SYSUTCDATETIME()
                WHERE profile_id=@id AND photo_type=@type;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.audit_photos
                  (id,profile_id,photo_type,photo_data,content_type,latitude,longitude,accuracy_meters,captured_at)
                VALUES(@photoId,@id,@type,@data,@mime,@lat,@lng,@accuracy,@captured);
            END;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@photoId", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@data", data);
        command.Parameters.AddWithValue("@mime", mime);
        command.Parameters.AddWithValue("@lat", lat);
        command.Parameters.AddWithValue("@lng", lng);
        command.Parameters.AddWithValue("@accuracy", accuracy);
        command.Parameters.AddWithValue("@captured", captured);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<(byte[] Data, string Mime)?> Photo(Guid id, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT TOP (1) photo_data,content_type
            FROM dbo.audit_photos
            WHERE profile_id=@id
            ORDER BY CASE photo_type WHEN 'frontal' THEN 1 WHEN 'operativa' THEN 2 WHEN 'entorno' THEN 3 ELSE 4 END;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ((byte[])reader[0], reader.GetString(1));
    }

    public async Task<(byte[] Data, string Mime)?> Photo(Guid id, string type, CancellationToken ct)
    {
        await using var connection = await Open(ct);
        const string sql = """
            SELECT TOP (1) photo_data,content_type
            FROM dbo.audit_photos
            WHERE profile_id=@id AND photo_type=@type;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@type", type);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ((byte[])reader[0], reader.GetString(1));
    }
}
#endif
