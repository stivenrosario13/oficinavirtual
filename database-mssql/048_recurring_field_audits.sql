SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.agency_profiles',N'U') IS NULL
        THROW 50001, 'No existe dbo.agency_profiles. Ejecuta primero el esquema principal.', 1;

    IF EXISTS (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.agency_profiles')
          AND name=N'UQ_profiles_agency' AND type=N'UQ'
    )
        ALTER TABLE dbo.agency_profiles DROP CONSTRAINT UQ_profiles_agency;

    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id=OBJECT_ID(N'dbo.agency_profiles')
          AND name=N'IX_agency_profiles_agency_submitted' AND is_unique=1
    )
        DROP INDEX IX_agency_profiles_agency_submitted ON dbo.agency_profiles;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id=OBJECT_ID(N'dbo.agency_profiles')
          AND name=N'IX_agency_profiles_agency_submitted'
    )
        CREATE INDEX IX_agency_profiles_agency_submitted
            ON dbo.agency_profiles(agency_id,submitted_at DESC,id DESC);

    IF EXISTS (
        SELECT 1
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
        INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE i.object_id=OBJECT_ID(N'dbo.agency_profiles')
          AND i.is_unique=1 AND i.is_primary_key=0 AND c.name=N'agency_id'
    )
        THROW 50002, 'Todavia existe un indice unico sobre agency_id.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT i.name AS index_name,i.is_unique,
       STRING_AGG(c.name,N', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS columns
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0
INNER JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.agency_profiles')
  AND i.name=N'IX_agency_profiles_agency_submitted'
GROUP BY i.name,i.is_unique;
GO
