SET NOCOUNT ON;
IF COL_LENGTH('dbo.admin_users','permissions_json') IS NULL
    ALTER TABLE dbo.admin_users ADD permissions_json nvarchar(max) NULL;

IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='jordy.arias@grupotejeda.local')
    UPDATE dbo.admin_users SET email='jordy.arias@grupotejeda.local' WHERE email='yordy.arias@grupotejeda.local';
UPDATE dbo.admin_users SET display_name=N'Jordy Arias'
WHERE email='jordy.arias@grupotejeda.local' OR LOWER(display_name)=N'yordy arias';

IF NOT EXISTS(SELECT 1 FROM dbo.admin_users WHERE email='michael.tejeda@grupotejeda.local')
    UPDATE dbo.admin_users SET email='michael.tejeda@grupotejeda.local' WHERE email='maicol.tejeda@grupotejeda.local';
UPDATE dbo.admin_users SET display_name=N'Michael Tejeda'
WHERE email='michael.tejeda@grupotejeda.local' OR LOWER(display_name)=N'maicol tejeda';
