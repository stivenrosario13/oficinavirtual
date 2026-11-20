-- Corrección de las cinco variantes confirmadas en el sitio. Conserva IDs y auditorías.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
DECLARE @aliases TABLE(alias nvarchar(150) PRIMARY KEY,canonical nvarchar(150));
INSERT @aliases VALUES
(N'003 | ALBERTO PROPIAS',N'3-ALBERTO PROPIAS'),
(N'ALBERTO PROPIAS',N'3-ALBERTO PROPIAS'),
(N'130 | LUMISMART BAVARO | PUNTA CANA',N'130-LUMISMART BAVARO | PUNTA CANA'),
(N'194 | LUIS TEJEDA .D.N',N'LUIS TEJEDA .D.N'),
(N'210 | MIGUEL RODRIGUEZ ( LA FAMOSA)',N'210-MIGUEL RODRIGUEZ ( LA FAMOSA)'),
(N'253 | SOCIEDAD EDUARD MORA',N'SOCIEDAD EDUARD MORA');

IF EXISTS(SELECT 1 FROM @aliases m WHERE EXISTS(SELECT 1 FROM dbo.agencies a WHERE a.grupo=m.alias)
    AND NOT EXISTS(SELECT 1 FROM dbo.agencies a WHERE a.grupo=m.canonical))
BEGIN
    ROLLBACK;
    THROW 50047,N'Falta un grupo de destino. No se modificó ningún registro.',1;
END;

-- Los responsables de las variantes pasan a ser responsables del grupo unificado.
-- Por ello podrán consultar todas las agencias de ese grupo, no solo las del alias.
INSERT dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
SELECT 'admin_user',ug.user_id,'GROUP_ASSIGNMENT_ALIAS_NORMALIZED',
    (SELECT ug.group_name AS old_group,m.canonical AS new_group FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
    'system:group-alias-repair'
FROM dbo.admin_user_groups ug WITH(UPDLOCK,HOLDLOCK) JOIN @aliases m ON m.alias=ug.group_name;

INSERT dbo.admin_user_groups(user_id,group_name,region,assigned_at)
SELECT ug.user_id,m.canonical,MAX(ug.region),MIN(ug.assigned_at)
FROM dbo.admin_user_groups ug WITH(UPDLOCK,HOLDLOCK) JOIN @aliases m ON m.alias=ug.group_name
WHERE NOT EXISTS(SELECT 1 FROM dbo.admin_user_groups target WITH(UPDLOCK,HOLDLOCK)
    WHERE target.user_id=ug.user_id AND target.group_name=m.canonical)
GROUP BY ug.user_id,m.canonical;
DELETE ug FROM dbo.admin_user_groups ug JOIN @aliases m ON m.alias=ug.group_name;

DECLARE @changes TABLE(id char(36),codigo nvarchar(100),old_group nvarchar(150),new_group nvarchar(150));
UPDATE a WITH(UPDLOCK,HOLDLOCK) SET grupo=m.canonical,updated_at=SYSUTCDATETIME()
OUTPUT inserted.id,inserted.codigo,deleted.grupo,inserted.grupo INTO @changes
FROM dbo.agencies a JOIN @aliases m ON a.grupo=m.alias;

INSERT dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
SELECT 'agency',c.id,'AGENCY_GROUP_ALIAS_NORMALIZED',
    (SELECT c.codigo,c.old_group,c.new_group FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),'system:group-alias-repair'
FROM @changes c;

-- Ocultar variantes vacías en el catálogo; los cambios de asignación quedan en audit_log.
UPDATE g SET is_active=0,agency_count=0,updated_at=SYSUTCDATETIME()
FROM dbo.agency_groups g JOIN @aliases m ON g.grupo=m.alias;
UPDATE g SET agency_count=(SELECT COUNT(*) FROM dbo.agencies a WHERE a.grupo=g.grupo AND a.is_active=1),updated_at=SYSUTCDATETIME()
FROM dbo.agency_groups g WHERE EXISTS(SELECT 1 FROM @aliases m WHERE m.canonical=g.grupo);
COMMIT;
SELECT old_group,new_group,COUNT(*) AS agencias_reubicadas FROM @changes GROUP BY old_group,new_group;
-- No se borran agencias: posibles códigos duplicados requieren comprobar sus expedientes.

END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
