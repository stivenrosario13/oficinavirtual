-- Datos de demostración opcionales. Ejecuta después de 001_schema.sql.
INSERT IGNORE INTO agencies(id,source_key,codigo,terminal,grupo,expected_latitude,expected_longitude,status) VALUES
('10000000-0000-4000-8000-000000000001','DEMO-DN-001','L-1025','REAL SOÑADORA Bella Vista','Grupo Distrito Capital',18.44850,-69.94310,'PENDING'),
('10000000-0000-4000-8000-000000000002','DEMO-DN-002','DN-002','Agencia 27 de Febrero','Grupo Distrito Capital',18.46620,-69.92700,'PENDING'),
('20000000-0000-4000-8000-000000000001','DEMO-SDE-001','L-5044','REAL SOÑADORA San Pedro Central','Grupo Este Sinergias',18.45200,-69.30080,'PENDING'),
('20000000-0000-4000-8000-000000000002','DEMO-SDE-002','SDE-002','Agencia Los Mina','Grupo Este Sinergias',18.49700,-69.88300,'PENDING'),
('30000000-0000-4000-8000-000000000001','DEMO-STI-001','L-4012','REAL SOÑADORA Santiago Gurabo','Grupo Cibao Norte',19.48120,-70.66200,'PENDING'),
('30000000-0000-4000-8000-000000000002','DEMO-STI-002','STI-002','Agencia Los Jardines','Grupo Cibao Norte',19.46800,-70.69000,'PENDING');
