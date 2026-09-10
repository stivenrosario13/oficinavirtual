IF OBJECT_ID(N'dbo.agencies',N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.agencies',N'directory_direccion') IS NULL ALTER TABLE dbo.agencies ADD directory_direccion nvarchar(300) NULL;
    IF COL_LENGTH(N'dbo.agencies',N'directory_sector') IS NULL ALTER TABLE dbo.agencies ADD directory_sector nvarchar(120) NULL;
    IF COL_LENGTH(N'dbo.agencies',N'directory_municipio') IS NULL ALTER TABLE dbo.agencies ADD directory_municipio nvarchar(120) NULL;
    IF COL_LENGTH(N'dbo.agencies',N'directory_provincia') IS NULL ALTER TABLE dbo.agencies ADD directory_provincia nvarchar(120) NULL;
END;
