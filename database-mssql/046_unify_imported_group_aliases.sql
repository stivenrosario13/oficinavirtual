-- Corrección de las cinco variantes confirmadas en el sitio. Conserva IDs y auditorías.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @aliases TABLE(alias nvarchar(150) PRIMARY KEY,canonical nvarchar(150));
INSERT @aliases VALUES
(N'003 | ALBERTO PROPIAS',N'3-ALBERTO PROPIAS'),
(N'ALBERTO PROPIAS',N'3-ALBERTO PROPIAS'),
(N'130 | LUMISMART BAVARO | PUNTA CANA',N'130-LUMISMART BAVARO | PUNTA CANA'),
(N'194 | LUIS TEJEDA .D.N',N'LUIS TEJEDA .D.N'),
(N'210 | MIGUEL RODRIGUEZ ( LA FAMOSA)',N'210-MIGUEL RODRIGUEZ ( LA FAMOSA)'),
(N'253 | SOCIEDAD EDUARD MORA',N'SOCIEDAD EDUARD MORA');

-- Un alias con un supervisor diferente exige revisar sus permisos antes de consolidar.
IF EXISTS(SELECT 1 FROM @aliases m JOIN dbo.admin_user_groups old ON old.group_name=m.alias
    WHERE NOT EXISTS(SELECT 1 FROM dbo.admin_user_groups current_assignment
        WHERE current_assignment.user_id=old.user_id AND current_assignment.group_name=m.canonical))
BEGIN
    ROLLBACK;
    THROW 50046,N'Hay supervisores asignados únicamente a un alias. Revisa las asignaciones antes de consolidar; no se modificó ningún registro.',1;
END;
IF EXISTS(SELECT 1 FROM @aliases m WHERE EXISTS(SELECT 1 FROM dbo.agencies a WHERE a.grupo=m.alias)
    AND NOT EXISTS(SELECT 1 FROM dbo.agencies a WHERE a.grupo=m.canonical))
BEGIN
    ROLLBACK;
    THROW 50047,N'Falta un grupo de destino. No se modificó ningún registro.',1;
END;

DECLARE @changes TABLE(id char(36),codigo nvarchar(100),old_group nvarchar(150),new_group nvarchar(150));
UPDATE a WITH(UPDLOCK,HOLDLOCK) SET grupo=m.canonical,updated_at=SYSUTCDATETIME()
OUTPUT inserted.id,inserted.codigo,deleted.grupo,inserted.grupo INTO @changes
FROM dbo.agencies a JOIN @aliases m ON a.grupo=m.alias;

INSERT dbo.audit_log(entity_type,entity_id,action,after_data,actor_email)
SELECT 'agency',c.id,'AGENCY_GROUP_ALIAS_NORMALIZED',
    (SELECT c.codigo,c.old_group,c.new_group FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),'system:group-alias-repair'
FROM @changes c;

-- Conservar catálogo y asignaciones históricas; ocultar las variantes vacías.
UPDATE g SET is_active=0,agency_count=0,updated_at=SYSUTCDATETIME()
FROM dbo.agency_groups g JOIN @aliases m ON g.grupo=m.alias;
UPDATE g SET agency_count=(SELECT COUNT(*) FROM dbo.agencies a WHERE a.grupo=g.grupo AND a.is_active=1),updated_at=SYSUTCDATETIME()
FROM dbo.agency_groups g WHERE EXISTS(SELECT 1 FROM @aliases m WHERE m.canonical=g.grupo);
COMMIT;
SELECT old_group,new_group,COUNT(*) AS agencias_reubicadas FROM @changes GROUP BY old_group,new_group;
-- No se borran agencias: posibles códigos duplicados requieren comprobar sus expedientes.
