-- Evidencias condicionales de inversor y batería - SQL Server 2025
-- Ejecutar después de 08_CREAR_ADMINISTRADORES_POR_GRUPO.sql.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.audit_photos',N'U') IS NULL
    THROW 50001,N'Falta dbo.audit_photos. Ejecuta primero 01_EJECUTAR_EN_SQL_SERVER.sql.',1;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.audit_photos') AND name=N'CK_photos_type')
    ALTER TABLE dbo.audit_photos DROP CONSTRAINT CK_photos_type;

ALTER TABLE dbo.audit_photos WITH CHECK
ADD CONSTRAINT CK_photos_type
CHECK (photo_type IN ('frontal','operativa','entorno','inversor','bateria'));

SELECT N'OK' AS resultado,N'Las fotos de inversor y batería están habilitadas.' AS detalle;
