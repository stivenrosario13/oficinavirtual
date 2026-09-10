SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.support_tickets',N'source_profile_id') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets ADD source_profile_id char(36) NULL;';

IF COL_LENGTH(N'dbo.support_tickets',N'is_automatic') IS NULL
    EXEC sys.sp_executesql N'ALTER TABLE dbo.support_tickets ADD is_automatic bit NOT NULL CONSTRAINT DF_support_tickets_automatic DEFAULT(0) WITH VALUES;';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.support_tickets') AND name=N'UX_support_tickets_profile_department')
    EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_support_tickets_profile_department
        ON dbo.support_tickets(source_profile_id,assigned_department)
        WHERE source_profile_id IS NOT NULL;';

EXEC sys.sp_executesql N'
INSERT INTO dbo.support_tickets
 (id,agency_id,category,assigned_department,priority,subject,description,created_by_user_id,created_by_name,created_by_username,source_profile_id,is_automatic)
SELECT NEWID(),p.agency_id,''INFRASTRUCTURE'',''GENERAL_SERVICES'',''MEDIUM'',
       CONCAT(''Hallazgos automáticos de auditoría - '',a.terminal),
       CONCAT(
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.painted''),JSON_VALUE(p.structural_answers,''$.Painted''))=''no'' THEN ''- La agencia no fue pintada. '' ELSE '''' END,
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.razaSticker''),JSON_VALUE(p.structural_answers,''$.RazaSticker''))=''no'' THEN ''- Falta el sticker de Raza. '' ELSE '''' END,
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.realSticker''),JSON_VALUE(p.structural_answers,''$.RealSticker''))=''no'' THEN ''- Falta el sticker de Real. '' ELSE '''' END,
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.lotekaRemoved''),JSON_VALUE(p.structural_answers,''$.LotekaRemoved''))=''no'' THEN ''- No fue retirada toda la publicidad de Loteka.'' ELSE '''' END),
       ''SYSTEM'',''Sistema automático'',''sistema.auditoria'',p.id,1
FROM dbo.agency_profiles p INNER JOIN dbo.agencies a ON a.id=p.agency_id
WHERE (COALESCE(JSON_VALUE(p.structural_answers,''$.painted''),JSON_VALUE(p.structural_answers,''$.Painted''))=''no''
    OR COALESCE(JSON_VALUE(p.structural_answers,''$.razaSticker''),JSON_VALUE(p.structural_answers,''$.RazaSticker''))=''no''
    OR COALESCE(JSON_VALUE(p.structural_answers,''$.realSticker''),JSON_VALUE(p.structural_answers,''$.RealSticker''))=''no''
    OR COALESCE(JSON_VALUE(p.structural_answers,''$.lotekaRemoved''),JSON_VALUE(p.structural_answers,''$.LotekaRemoved''))=''no'')
  AND NOT EXISTS(SELECT 1 FROM dbo.support_tickets t WHERE t.source_profile_id=p.id AND t.assigned_department=''GENERAL_SERVICES'');

INSERT INTO dbo.support_tickets
 (id,agency_id,category,assigned_department,priority,subject,description,created_by_user_id,created_by_name,created_by_username,source_profile_id,is_automatic)
SELECT NEWID(),p.agency_id,''EQUIPMENT'',''TECHNOLOGY'',
       CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.damageFound''),JSON_VALUE(p.structural_answers,''$.DamageFound''))=''yes'' THEN ''HIGH'' ELSE ''MEDIUM'' END,
       CONCAT(''Hallazgos automáticos de auditoría - '',a.terminal),
       CONCAT(
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.damageFound''),JSON_VALUE(p.structural_answers,''$.DamageFound''))=''yes'' THEN ''- Se reportó una avería. '' ELSE '''' END,
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.printerMaintained''),JSON_VALUE(p.structural_answers,''$.PrinterMaintained''))=''no'' THEN ''- La impresora no recibió mantenimiento. '' ELSE '''' END,
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.inverterPresent''),JSON_VALUE(p.structural_answers,''$.InverterPresent''))=''no'' THEN ''- La agencia no tiene inversor. '' ELSE '''' END,
         CASE WHEN COALESCE(JSON_VALUE(p.structural_answers,''$.batteryPresent''),JSON_VALUE(p.structural_answers,''$.BatteryPresent''))=''no'' THEN ''- La agencia no tiene batería.'' ELSE '''' END),
       ''SYSTEM'',''Sistema automático'',''sistema.auditoria'',p.id,1
FROM dbo.agency_profiles p INNER JOIN dbo.agencies a ON a.id=p.agency_id
WHERE (COALESCE(JSON_VALUE(p.structural_answers,''$.damageFound''),JSON_VALUE(p.structural_answers,''$.DamageFound''))=''yes''
    OR COALESCE(JSON_VALUE(p.structural_answers,''$.printerMaintained''),JSON_VALUE(p.structural_answers,''$.PrinterMaintained''))=''no''
    OR COALESCE(JSON_VALUE(p.structural_answers,''$.inverterPresent''),JSON_VALUE(p.structural_answers,''$.InverterPresent''))=''no''
    OR COALESCE(JSON_VALUE(p.structural_answers,''$.batteryPresent''),JSON_VALUE(p.structural_answers,''$.BatteryPresent''))=''no'')
  AND NOT EXISTS(SELECT 1 FROM dbo.support_tickets t WHERE t.source_profile_id=p.id AND t.assigned_department=''TECHNOLOGY'');';

COMMIT TRANSACTION;
EXEC sys.sp_executesql N'SELECT COUNT(*) AS tickets_automaticos FROM dbo.support_tickets WHERE is_automatic=1;';
