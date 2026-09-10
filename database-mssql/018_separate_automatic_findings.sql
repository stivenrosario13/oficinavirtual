SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.automatic_findings',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.automatic_findings (
        id uniqueidentifier NOT NULL CONSTRAINT DF_automatic_findings_id DEFAULT(NEWID()),
        profile_id char(36) NOT NULL,
        agency_id char(36) NOT NULL,
        finding_key varchar(40) NOT NULL,
        title nvarchar(180) NOT NULL,
        detail nvarchar(1000) NOT NULL,
        assigned_department varchar(25) NOT NULL,
        priority varchar(15) NOT NULL,
        status varchar(20) NOT NULL CONSTRAINT DF_automatic_findings_status DEFAULT('OPEN'),
        resolution nvarchar(2000) NULL,
        created_at datetime2(3) NOT NULL CONSTRAINT DF_automatic_findings_created DEFAULT(SYSUTCDATETIME()),
        updated_at datetime2(3) NOT NULL CONSTRAINT DF_automatic_findings_updated DEFAULT(SYSUTCDATETIME()),
        closed_at datetime2(3) NULL,
        CONSTRAINT PK_automatic_findings PRIMARY KEY(id),
        CONSTRAINT UQ_automatic_findings_profile_key UNIQUE(profile_id,finding_key),
        CONSTRAINT FK_automatic_findings_profile FOREIGN KEY(profile_id) REFERENCES dbo.agency_profiles(id) ON DELETE CASCADE,
        CONSTRAINT FK_automatic_findings_agency FOREIGN KEY(agency_id) REFERENCES dbo.agencies(id),
        CONSTRAINT CK_automatic_findings_department CHECK(assigned_department IN ('TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES')),
        CONSTRAINT CK_automatic_findings_priority CHECK(priority IN ('LOW','MEDIUM','HIGH','CRITICAL')),
        CONSTRAINT CK_automatic_findings_status CHECK(status IN ('OPEN','IN_PROGRESS','RESOLVED','CLOSED'))
    );
END;

EXEC sys.sp_executesql N'
INSERT INTO dbo.automatic_findings(id,profile_id,agency_id,finding_key,title,detail,assigned_department,priority)
SELECT NEWID(),p.id,p.agency_id,f.finding_key,f.title,f.detail,f.department,f.priority
FROM dbo.agency_profiles p
CROSS APPLY (VALUES
 (''NO_INVERTER'',''Agencia sin inversor'',''La auditoría confirmó que la agencia no tiene inversor.'',''GENERAL_SERVICES'',''HIGH'',COALESCE(JSON_VALUE(p.structural_answers,''$.inverterPresent''),JSON_VALUE(p.structural_answers,''$.InverterPresent'')),''no''),
 (''NO_BATTERY'',''Agencia sin batería'',''La auditoría confirmó que la agencia no tiene batería.'',''GENERAL_SERVICES'',''HIGH'',COALESCE(JSON_VALUE(p.structural_answers,''$.batteryPresent''),JSON_VALUE(p.structural_answers,''$.BatteryPresent'')),''no''),
 (''PRINTER_NO_MAINTENANCE'',''Impresora sin mantenimiento'',''La impresora no recibió mantenimiento y es indispensable para imprimir tickets.'',''TECHNOLOGY'',''CRITICAL'',COALESCE(JSON_VALUE(p.structural_answers,''$.printerMaintained''),JSON_VALUE(p.structural_answers,''$.PrinterMaintained'')),''no''),
 (''NOT_PAINTED'',''Agencia no pintada'',''La agencia no cumple con la pintura requerida.'',''GENERAL_SERVICES'',''LOW'',COALESCE(JSON_VALUE(p.structural_answers,''$.painted''),JSON_VALUE(p.structural_answers,''$.Painted'')),''no''),
 (''NO_RAZA_STICKER'',''Falta sticker de Raza'',''No fue colocado el sticker de Raza.'',''GENERAL_SERVICES'',''LOW'',COALESCE(JSON_VALUE(p.structural_answers,''$.razaSticker''),JSON_VALUE(p.structural_answers,''$.RazaSticker'')),''no''),
 (''NO_REAL_STICKER'',''Falta sticker de Real'',''No fue colocado el sticker de Real.'',''GENERAL_SERVICES'',''LOW'',COALESCE(JSON_VALUE(p.structural_answers,''$.realSticker''),JSON_VALUE(p.structural_answers,''$.RealSticker'')),''no''),
 (''LOTEKA_NOT_REMOVED'',''Publicidad de Loteka pendiente'',''No fue retirada toda la publicidad de Loteka.'',''GENERAL_SERVICES'',''MEDIUM'',COALESCE(JSON_VALUE(p.structural_answers,''$.lotekaRemoved''),JSON_VALUE(p.structural_answers,''$.LotekaRemoved'')),''no''),
 (''DAMAGE_REPORTED'',''Avería sin clasificación histórica'',''La auditoría reportó una avería, pero el formulario anterior no exigía especificar el tipo.'',''TECHNOLOGY'',''HIGH'',COALESCE(JSON_VALUE(p.structural_answers,''$.damageFound''),JSON_VALUE(p.structural_answers,''$.DamageFound'')),''yes'')
) f(finding_key,title,detail,department,priority,answer,trigger_answer)
WHERE f.answer=f.trigger_answer
AND NOT EXISTS(SELECT 1 FROM dbo.automatic_findings af WHERE af.profile_id=p.id AND af.finding_key=f.finding_key);

DELETE FROM dbo.support_tickets WHERE is_automatic=1;';

COMMIT TRANSACTION;
EXEC sys.sp_executesql N'SELECT finding_key,priority,COUNT(*) AS cantidad FROM dbo.automatic_findings GROUP BY finding_key,priority ORDER BY priority,finding_key;';
