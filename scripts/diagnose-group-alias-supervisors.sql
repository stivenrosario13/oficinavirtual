-- SOLO LECTURA: identifica los supervisores que activan el error 50046.
SET NOCOUNT ON;
DECLARE @aliases TABLE(alias nvarchar(150) PRIMARY KEY,canonical nvarchar(150));
INSERT @aliases VALUES
(N'003 | ALBERTO PROPIAS',N'3-ALBERTO PROPIAS'),
(N'ALBERTO PROPIAS',N'3-ALBERTO PROPIAS'),
(N'130 | LUMISMART BAVARO | PUNTA CANA',N'130-LUMISMART BAVARO | PUNTA CANA'),
(N'194 | LUIS TEJEDA .D.N',N'LUIS TEJEDA .D.N'),
(N'210 | MIGUEL RODRIGUEZ ( LA FAMOSA)',N'210-MIGUEL RODRIGUEZ ( LA FAMOSA)'),
(N'253 | SOCIEDAD EDUARD MORA',N'SOCIEDAD EDUARD MORA');

SELECT m.alias AS grupo_variante,m.canonical AS grupo_principal,
    old.user_id,u.email AS usuario,u.display_name AS nombre,u.role AS rol,u.is_active AS activo,
    (SELECT COUNT(*) FROM dbo.agencies a WHERE a.grupo=m.alias AND a.is_active=1) AS agencias_actuales_variante,
    (SELECT COUNT(*) FROM dbo.agencies a WHERE a.grupo=m.canonical AND a.is_active=1) AS agencias_grupo_principal
FROM @aliases m JOIN dbo.admin_user_groups old ON old.group_name=m.alias
LEFT JOIN dbo.admin_users u ON u.id=old.user_id
WHERE NOT EXISTS(SELECT 1 FROM dbo.admin_user_groups current_assignment
    WHERE current_assignment.user_id=old.user_id AND current_assignment.group_name=m.canonical)
ORDER BY m.canonical,u.display_name;

-- Responsables actuales de los grupos de destino, para comparar asignaciones.
SELECT DISTINCT m.canonical AS grupo_principal,u.id AS user_id,u.email AS usuario,
    u.display_name AS nombre,u.role AS rol,u.is_active AS activo
FROM @aliases m JOIN dbo.admin_user_groups ug ON ug.group_name=m.canonical
LEFT JOIN dbo.admin_users u ON u.id=ug.user_id
ORDER BY grupo_principal,nombre;
