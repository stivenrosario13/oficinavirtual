-- Administradores por regiones y permisos por grupo - SQL Server 2025
-- Fuente: Grupos y administradores por regiones.xlsx
-- Las contraseñas temporales están fuera de wwwroot y deben cambiarse al primer ingreso.
-- Con el listado vigente: 141 de 161 grupos tienen una asignación exacta; los otros 20 quedan visibles al administrador principal.

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Permite volver a ejecutar el archivo en la misma ventana después de un error.
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
DROP TABLE IF EXISTS #Assignments;
DROP TABLE IF EXISTS #ImportedUsers;

IF COL_LENGTH(N'dbo.admin_users',N'region') IS NULL ALTER TABLE dbo.admin_users ADD region nvarchar(120) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'contact') IS NULL ALTER TABLE dbo.admin_users ADD contact nvarchar(80) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'avatar_data') IS NULL ALTER TABLE dbo.admin_users ADD avatar_data varbinary(max) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'avatar_content_type') IS NULL ALTER TABLE dbo.admin_users ADD avatar_content_type varchar(100) NULL;
IF COL_LENGTH(N'dbo.admin_users',N'must_change_password') IS NULL ALTER TABLE dbo.admin_users ADD must_change_password bit NOT NULL CONSTRAINT DF_admin_users_must_change DEFAULT(0) WITH VALUES;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.admin_users') AND name=N'CK_admin_users_role') ALTER TABLE dbo.admin_users DROP CONSTRAINT CK_admin_users_role;
ALTER TABLE dbo.admin_users WITH CHECK ADD CONSTRAINT CK_admin_users_role CHECK(role IN ('ADMINISTRATOR','VIEWER','GROUP_ADMIN','FISCALIZADOR','TECHNOLOGY','GENERAL_SERVICES','HUMAN_RESOURCES'));

IF OBJECT_ID(N'dbo.admin_user_groups',N'U') IS NULL
BEGIN
  CREATE TABLE dbo.admin_user_groups(
    user_id char(36) NOT NULL,group_name nvarchar(150) NOT NULL,region nvarchar(120) NULL,
    assigned_at datetime2(3) NOT NULL CONSTRAINT DF_admin_user_groups_assigned DEFAULT(SYSUTCDATETIME()),
    CONSTRAINT PK_admin_user_groups PRIMARY KEY(user_id,group_name),
    CONSTRAINT FK_admin_user_groups_user FOREIGN KEY(user_id) REFERENCES dbo.admin_users(id) ON DELETE CASCADE
  );
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.admin_user_groups') AND name=N'IX_admin_user_groups_group') CREATE INDEX IX_admin_user_groups_group ON dbo.admin_user_groups(group_name,user_id);

BEGIN TRANSACTION;
CREATE TABLE #ImportedUsers(id char(36),email nvarchar(320),display_name nvarchar(160),region nvarchar(120),contact nvarchar(80),password_salt binary(16),password_hash binary(32));
INSERT INTO #ImportedUsers(id,email,display_name,region,contact,password_salt,password_hash) VALUES
('a0a3a1d2-0791-4340-acab-4e6c08888043',N'amarilys.reyes@grupotejeda.local',N'Amarilys Reyes',N'Región Este',N'(829) 559-6628',0x6042ee325b7fc14cba7996fe8f252450,0x1cb2e4074c7cd17d8f8ed23e493767a185dfe1e1000b31ae7a307e228634e0d0),
('15056168-e850-49ba-b12a-926cb884f812',N'ana.cedeno@grupotejeda.local',N'Ana Cedeño',N'Región Este',N'(829) 985-6567',0x9698c0921955dde96504fe7b8a2f710c,0x7a08e4f8442b4fb147c282b224ffbd3bd9493f1ae1ab350c9dc8fe314ef66908),
('160a288a-2f38-409b-8fc9-d30a821fd97b',N'anrys.rodriguez@grupotejeda.local',N'Anrys Rodriguez',N'Gran Santo Domingo',N'(809) 308-4287',0x67265239cc31148f07f44c95af4ea2e1,0xa3302acb3441545484e462536b00ec4093965d989bb822b5288016c27ea430e6),
('debc4197-61cd-4a57-8543-a4769b6238bb',N'antony.guzman@grupotejeda.local',N'Antony Guzman',N'Grupos Administrativos',N'(829) 591-9403',0xc5d1e7a46866b5477fa9c9b676101210,0x10f9101058dc39d462f937dddeccca1dc4b82961b8f6017f78b499bfb9add4d7),
('4ee6110b-5e93-4a8f-9fbc-e003e140ade2',N'ariel.garcia@grupotejeda.local',N'Ariel Garcia',N'Grupos Administrativos',N'(809) 780-4202',0xdc892767349c112c3a42d20cfdae4654,0x179372f50b93f2d1b68111309d81ad9f8b7ee8397a359403d6075400c293e62f),
('867a3715-9f14-46f9-b64d-ee38c976aac7',N'benito@grupotejeda.local',N'Benito',N'Región Sur',N'(829) 851-9990',0x9ef7511b12c326a2d1ed0bc59e29c752,0x44c22acb9abb3d8e3f75930348168cdc97c93f3c9b3dc91f06ff808652c550af),
('dcc21cc8-9f07-46ca-bb37-49b9d4bcaeb1',N'benjamin.a.mercedes@grupotejeda.local',N'Benjamín A Mercedes',N'Gran Santo Domingo',N'(829) 560-0490',0xb7850133ada77c3a3202974e0b53bd4d,0x1ede1d7f0ab3f5d0c79428a6dcc668f0757c4f6582ec4806cbc08263e6b44c3f),
('5c676c66-e90b-48e3-b3f1-570cd00b3bbb',N'carlos.brisas@grupotejeda.local',N'Carlos Brisas',N'Gran Santo Domingo',N'(829) 868-2252',0xfac8c6d38c956b7d0c1a6941bc569625,0x76b376d16d2ee3fa11114854165e280cd34218a1fd0be458b244593f38ed766a),
('91dbafb1-2664-4f2d-874b-ff2de4d38c43',N'carolina.rodriguez@grupotejeda.local',N'Carolina Rodriguez',N'Gran Santo Domingo',N'(809) 853-0667',0x6e4a85af4348da8c93a382cbcbff0035,0x8368dd589b0fe2acac6f34cc2dc8eb8babcf5bc1b93272c4c8ea86dfda60270d),
('f3a5f068-2529-4bd3-a4da-f69a13f08fad',N'cocha@grupotejeda.local',N'Cocha',N'Región Sur',N'(829) 277-7959',0x4b46f804112eecfc697ebaf422afef7b,0x39083d05a04026407b4314ecb52593aba0eec6709d2c51e8af75337cf41d6295),
('4c87c30a-888a-4a4f-bb0e-fc48344aa2e6',N'cornelio.fortuna@grupotejeda.local',N'Cornelio Fortuna',N'Gran Santo Domingo',N'(829) 522-8233',0xdbfeac532792148ca060a0772b382b8f,0xaaee27628e6d0b5a9f262541db7b75ec444a019670b9c3712f4f6872e2ee592e),
('4fc64cd5-dae7-4fc6-998c-478cb48d2e20',N'cristina.montero@grupotejeda.local',N'Cristina Montero',N'Región Este',N'(829) 540-2154',0xabe015209f2239a20549049e3156257f,0xb74446fd291e1ac04b4a33c1351a4fb82528254880e39f475b610012420cdcdd),
('1d33e9e8-e95c-4085-9c3b-83a0d4223ea0',N'darlyn.trump@grupotejeda.local',N'Darlyn Trump',N'Región Norte y Noreste',N'(829) 858-5098',0xeb602f4ff0488569a8743414e4545df3,0x30fee504fd5aba44f2946985fcf9fbd13a0dc115e1404bc274aa6d95e57b018a),
('97992f62-2124-40c7-9b20-f56769ad2cc5',N'dauryn@grupotejeda.local',N'Dauryn',N'Gran Santo Domingo',N'(809) 969-2681',0xa89feed2ab94e824aaabc5fb2f32ee18,0x28327e3c799c770b909923da8491ab83668ca11f6b1d64218680be518ed4b94a),
('14849d3c-40c9-4935-a52e-ccb07bd19b42',N'david.tavares@grupotejeda.local',N'David Tavares',N'Gran Santo Domingo',N'(829) 346-0295',0x6f7dc6f1c87139c1cd045596e1476be5,0x17e8421223b357d94785a690ad1bc74fb0d6ba330e9babe871c4d628bb2d8caa),
('48cf5e21-d699-4f73-a3a5-71264b163095',N'divanna.jimenez@grupotejeda.local',N'Divanna Jimenez',N'Gran Santo Domingo',N'(809) 886-2810',0xf9e4636dbf3c8ed1e221169854fc8274,0x8648a831c25da14db624c047b79b5e57c2b7a2dbf1a6c47f2ea0cddb6d403fc8),
('390aec16-295b-4395-b0e7-ede8b66aa92b',N'dominga@grupotejeda.local',N'Dominga',N'Gran Santo Domingo',N'(829) 631-3303',0x684597ad25b4210ca2bc09a5dcf99a9b,0x6882fc2d4ec98beaf31b33a30f92b29d48ccc91a5aae0b944ace5fb669fb3538),
('d4f5f190-cddd-4dc1-9751-c955f7280826',N'domingo.mameyes@grupotejeda.local',N'Domingo Mameyes',N'Gran Santo Domingo',N'(809) 603-8367',0x0a4dc000fe5e23f43c84ebc61b001b78,0x547bd1db0a9c3d514728948c45612df52915350b4a051ff9adce6393eb23bf8a),
('e51e3143-aa45-4f37-964a-c0cf3bc4f4cc',N'dulce.maria@grupotejeda.local',N'Dulce Maria',N'Región Norte y Noreste',N'(849) 208-3509',0x9b8f64f7700a85292bf6b29517c32c2f,0xb9642557c769b5c1eb5c9e0265677d79e49be831da2af1ccc27082ab57d56693),
('4867c06a-e7fd-4d12-8807-176f2d416754',N'eduardo.paredes@grupotejeda.local',N'Eduardo Paredes',N'Región Norte y Noreste',N'(829) 795-3222',0x19852ccad60842653265641585aa8ae6,0xd71bdddf662c47958e0c6a690733e6b62890da3532adc0563621e5c91310c622),
('f172da32-1a4b-448f-9ba8-672943150237',N'enerio@grupotejeda.local',N'Enerio',N'Gran Santo Domingo',N'(809) 837-7271',0xa77d2f3259b3a9a65c99b12703192148,0xc21580fa0b8955c99752594eba4a2e1af88eb88b0a91fcebfeeb323802ca367f),
('b0f1e538-2a0f-4918-b7fd-e046e88b631f',N'fatima@grupotejeda.local',N'Fatima',N'Región Este',N'(829) 514-2404',0x687eb12fc7c1932b97bdeee0b53e869f,0x875f722815d164c53001e9c53762e46870082d8191f761179e5c8f218c05042f),
('555a72f7-6253-4e58-a2b2-205aa5732ef2',N'felix.toribio@grupotejeda.local',N'Felix Toribio',N'Gran Santo Domingo',N'(809) 852-5144',0x2de120aa028c269874dee9e9cb3145a8,0xb671c86373898143d6ce5177f872533b0610a8d9d4a4f453d3626dd50d301834),
('b11e4b6b-10c1-4f58-abfa-ad1c30b3d481',N'flor.maria@grupotejeda.local',N'Flor Maria',N'Región Sur',N'(849) 852-2267',0x0c13527e847607a9833de89b566196d7,0xe40343befbdce742d599af60aca5035894437f1fb48801327b408659e2591cac),
('bf7bb034-79e2-4b19-85f6-bd5786b2841c',N'hedwing.nunez@grupotejeda.local',N'Hedwing Nuñez',N'Región Norte y Noreste',N'(829) 927-5006',0x725e2b3746eb6948adbeb58ae01e8954,0x06334648876b9e3d400d1bced6562fb0ec9ce002f4e8aa98ef28d4693ec903e8),
('1dec8443-216e-4fcb-af1e-417569c94e2a',N'henry.santos@grupotejeda.local',N'Henry Santos',N'Gran Santo Domingo',N'(829) 886-3784',0x71a7a1ac2a6073d95fe7a99651dad8ca,0x79449b0a95681441f018cc384bd1a508d65d10cac3660310025e0f280d4bbd1a),
('a756ad5c-1064-4022-b8a1-32c4420bd04f',N'hijo@grupotejeda.local',N'Hijo',N'Gran Santo Domingo',N'(849) 344-5632',0xf60732fabc29aed0c40d5a771a0b2326,0x599961d9da44958401477cba8f47e2b24fc5e95cfbe112cbc9605431c984192a),
('d981e926-c85a-442e-beed-4b8927b0814e',N'iris.alcantara@grupotejeda.local',N'Iris Alcantara',N'Región Sur',N'(829) 509-2489',0x91c27b05943abdaf645fe8daeb30fc56,0xe74f6686b7f5240d53dc5ad0bcde22aab35e89fff727a1947da10eba202809eb),
('98228a27-4f7e-4e50-8920-6f4314a71338',N'ivelise.martinez@grupotejeda.local',N'Ivelise Martinez',N'Gran Santo Domingo · Región Norte y Noreste',N'(809) 910-6707',0x693b7637716ce6b89e2644e521a64aaf,0xb33887a5ac28aa8358f764e5746381b4676118782d33080145e96141b64b1972),
('107c36a2-670f-488a-b5ac-8d15ed23f331',N'jeidi.castillo@grupotejeda.local',N'Jeidi Castillo',N'Región Sur',N'(829) 301-5856',0xe337763937bfee21ab25c4ce2964b9d5,0x2d63cb96006f0b4edaf0e8a921fc93431e85bdaed419e60e194935ba63c90558),
('13c55ee3-4fca-43e9-97da-75106a008b77',N'jhordy.arias@grupotejeda.local',N'Jhordy Arias',N'Gran Santo Domingo',N'(809) 299-4169',0x6f8f7884409f734417b3665a95845efd,0xb715b39d35ed1612f78752b4354fd0a9019bb216ba46b97d30cec3e063527013),
('308ba5db-b9d8-4dfc-9bc3-799551be285b',N'jhoscarlo.suriel@grupotejeda.local',N'Jhoscarlo Suriel',N'Región Norte y Noreste',N'(809) 729-2553',0x8bb44310821209c55eafc4e9989ac469,0xbaecf3b377009466d88822201e961522dfb09e67e8754e520111d79bc785bb87),
('65f66d48-d3d8-4e9e-a5c8-3e31ca4263c0',N'jinet.roman@grupotejeda.local',N'Jinet Roman',N'Región Norte y Noreste',N'(809) 882-4487',0x7390e038efcebf395897efa7c500949f,0xe1fc30a2bc8868181242ba9541876af0706d4584bf0275e2175bbde80071658f),
('72f2519b-a5e3-45d0-80da-b11a10b1ab63',N'joel.vizcaino@grupotejeda.local',N'Joel Vizcaino',N'Gran Santo Domingo',N'(809) 222-5393',0xd31c7115517fe5ff0bf0aa1a94bd890b,0x562ffbb37d042dc3c27538ebd60a10b3cc00ed67f5e6ac734b05501f7b95db23),
('823d1c81-5e5b-4308-9d88-45eaa61b1173',N'jose.batista.sandy@grupotejeda.local',N'José Batista (Sandy)',N'Gran Santo Domingo',N'(809) 629-2363',0xa01d8dc56970f029f7cb94cfda43b0f0,0xe54d33d2dd5d8f0f126f6b86d95bb676a3cfa1835164dcfa78be074f1cadb344),
('ccf525bb-65b1-43ec-8731-86c2eb721cb3',N'jose.mesa@grupotejeda.local',N'Jose mesa',N'Gran Santo Domingo',N'(809) 439-6938',0x9305f40cbb253fee17f975c68aeb6f24,0xb1898f60a8850de5268fe6408598df9c12db4a71dc0ff32afe8005b6f9b7b94b),
('7ed2653e-30f0-4f7c-b4fd-567ca164ffbd',N'jose.ramon@grupotejeda.local',N'Jose Ramon',N'Gran Santo Domingo',N'(809) 804-2311',0x6ead2b23cdfadd93a0c4da98199219b4,0x4d9a6a2d3dd549e81403fbcef286eacb77b463f066971a33afe379f8d247bbee),
('e2d63a9d-811c-431a-af17-6882753e43f6',N'julio.rosario@grupotejeda.local',N'Julio Rosario',N'Gran Santo Domingo',N'(809) 994-8993',0x5539d3adcc000b2707887c92c3f071c2,0x768f5ff35cf740b8ec2d1f078dd2091b4a3675dc73148c36c6a666eb90acec0f),
('a73436bc-8584-4a3f-ba30-ba5da3e3c2dd',N'junior.bono@grupotejeda.local',N'Junior Bono',N'Gran Santo Domingo',N'(829) 801-2509',0x374137d5b95b1b2431e4886811da3bfb,0xd2600113cd22938f1ded94ffe7c758463077ccfb07cbcd7a05a2e033c396bf9a),
('fdee4b28-ffe4-499d-8c5f-94bf452546de',N'junior.melendez@grupotejeda.local',N'Junior Meléndez',N'Gran Santo Domingo',N'(829) 707-3851',0x5e736a6509efd5a653c508d58d992d02,0x3c13698afa5d8f623948393be036077a9e637081d5119dd2a4b3b924a2302f70),
('0d253a90-b7be-4357-8919-3cfd5e898127',N'junior.simon@grupotejeda.local',N'Junior Simon',N'Gran Santo Domingo',N'(829) 305-2219',0x6853fcb34c57072953088e7997dd4b36,0xdf4bee583d83c01c5966d52e124989ced14dee54170d085fe55436c8f6ded552),
('bfd9dee0-fffc-403b-a448-41dd11b35412',N'kiko@grupotejeda.local',N'Kiko',N'Región Este',N'(829) 279-9289',0xe92c78364f7e6760cc1ad80a9327c55a,0xcc05c53d43ea6e1e89117b21031ca000ac0fffa2c5490f496037d9fb083936c9),
('f6534dba-9514-4d78-8cda-68d1b942b04e',N'leolidia.jimenez@grupotejeda.local',N'Leolidia Jimenez',N'Gran Santo Domingo',N'(809) 708-6715',0x6f2b646e6ffd107269918a31de9429c0,0xf7eb95263c6208ecd1e729c7af819c2f72bd8a9120583487c6ac4d519120ac25),
('086a2834-5b81-4fc6-be4e-defbdefd1a05',N'levi.marte@grupotejeda.local',N'Levi Marte',N'Gran Santo Domingo',N'(829) 722-4900',0xd176df9afccb6ffaf99a337e27525ba1,0xf165b42c044fd18c10f0702b5f6151fe642a61406b3c8601706abaa268cbc242),
('a44c7fb6-876b-4bd5-a2dc-893b039f67bb',N'lucy.cordero@grupotejeda.local',N'Lucy Cordero',N'Región Sur',N'(849) 878-1869',0xf1bf01bc13835a5081b3b5100d83ff84,0xf25ba4bfb107d359aa0ed50f4a1f4de5a92adbc6b62d0fe2973327604725b81e),
('4ea2cdb0-8cc3-49b2-8ffb-303037bef46c',N'luis.henriquez@grupotejeda.local',N'Luis henriquez',N'Gran Santo Domingo',N'(829) 589-8090',0x27f3c0959e6cb038e606458ca2c4f0ed,0xf74a00c412790d8d800ec60110551162e353ba2d2b03c6873812db04767a1cc0),
('a3028b9d-4531-4e6c-b2a9-258cc21470fc',N'luis.manuel@grupotejeda.local',N'Luis Manuel',N'Región Sur · Barahona',NULL,0xf8d5cacf8a4204b53646e95e2e5f26b6,0x427a0d258167aba2444c1b84118a2eee99ba9000ab345f729494058ac66ec750),
('fd4f0a04-f695-4746-9e8a-685c30ac8048',N'mario.mejia@grupotejeda.local',N'Mario Mejia',N'Región Este',N'(829) 675-3550',0xef0c32691ebc325dad0cd73b231afd17,0xf5267df55fa77526fb3321ef0cc738bf2665aaf073d21f05cd4946538d4dc9f5),
('8ba73af2-e91f-46d6-943e-900d826f6ff7',N'maryelin.herrera@grupotejeda.local',N'Maryelin Herrera',N'Región Este',N'(829) 556-0768',0x3fdf29a0ba3e8e7b288857770ed6ce3c,0x4b3d96a3bf2b210d73d267093ac36f3c21b44e810f5d40ce7433945e6d287943),
('8e528253-fba3-4c27-a6e5-eb31201b4a08',N'maximo.de.jesus@grupotejeda.local',N'Maximo de Jesus',N'Gran Santo Domingo',N'(829) 562-4269',0x46ee11846f09ffe1a9fcb348022bcbcc,0x8c7df6a721f509c09d68bc68fb77e621f46c88a451ce3e5a093f21856f31db90),
('01934863-63b8-4104-a4c4-c9cb7d03d6c6',N'mechi@grupotejeda.local',N'Mechi',N'Gran Santo Domingo',N'(809) 640-9229',0x4baa54ce865cc5f4eba4721e1d72c1df,0xa1b226599d9ef6bc598865fdf3d864ac10b38bacc66f3f88de62196060c96b46),
('87ca6310-fc57-4c61-8d3a-a2e6c7a741c3',N'naomy@grupotejeda.local',N'Naomy',N'Región Sur · Barahona',N'(809) 917-3977',0xd6db5afcb2e6ba929522ac6d2010affa,0xd3483f48057928afbc9e6543bd85cb10ec8055fc3fc5c279d8b03361feaa0cfa),
('17f034ec-82d7-4fd1-9b74-c945b2e0f8c0',N'nelson.breton@grupotejeda.local',N'Nelson bretón',N'Gran Santo Domingo',N'(829) 838-6328',0x4683cdd7d1f4989e0febd3869c21e81d,0xd27f99b8bdf64b16c4c45b7d9187883ec40ab07a86c005664a318a179d4c754d),
('2b51509a-5d1e-46e6-aee8-9b3501c7932c',N'nelson.perez@grupotejeda.local',N'Nelson Perez',N'Región Sur',N'(829) 361-0562',0x49f7e3df8be79f8c6df804a98680978a,0x7d97cca2115307caf139b267b9c2d3b8300c427efacdb12e7aef49a48aab5a46),
('d1679687-4447-4db7-a9e2-0ad6611d4af9',N'pamela.alvarez@grupotejeda.local',N'Pamela Alvarez',N'Gran Santo Domingo',N'(809) 438-1762',0xa6b26021f79a84a46b6c5ae63af3107a,0xc716601e9fed9c3eff83b09dbe8a388997c3e91f572fc9a78f988011690ff4d4),
('3e4ea166-4bec-427b-88c3-e785cfb74609',N'pamela@grupotejeda.local',N'Pamela',N'Región Sur',N'(809) 903-9952',0x60ba55f7179561d58d7ca1d95c5190cd,0x5e402c1fe9cbc73ea131f029b2f4c44ca895c19a5a4a1fc132f192007e9b551e),
('20e77b88-d063-4968-8aaa-5444c4aa6499',N'pedro.toribio@grupotejeda.local',N'Pedro Toribio',N'Gran Santo Domingo',N'(829) 702-3483',0x55f04c16a523a02254391006166496dd,0x9df8ead0676187cce785fd9f764b36a493a4e3405c26ac82df063464f468148a),
('07f741e8-24c7-4cb4-899a-724d1be7f830',N'peje@grupotejeda.local',N'Peje',N'Gran Santo Domingo',N'(809) 850-8822',0x808b10028695f6a4a40469df9ab9894d,0xb72eabf14df98163ede23629f114b24543dd9e4705bf38e86daa24f2d6554e70),
('edc9dd5a-2dee-4077-8c63-a83cbdecb636',N'piter.ravix@grupotejeda.local',N'Piter Ravix',N'Gran Santo Domingo',N'(809) 697-8909',0xe7d7785eee00d530c667b27e409125a3,0x3e459e5a7cdf04765b2761f0730c315462732f77d341713ed516d10ed7bf1338),
('641fe400-8526-4a98-b50d-9783894b74ea',N'robert.medina@grupotejeda.local',N'Robert Medina',N'Región Sur',N'(829) 341-3106',0xdbd994a8a3df51e816f7ba2594636b6c,0x32eba6e6a94767124c065080fefd3c08b9584f32f41ef276dd3ce31bd2856c8c),
('65f28534-339f-46a6-9301-5999763c89f5',N'robinson.luciano@grupotejeda.local',N'Robinson Luciano',N'Gran Santo Domingo',N'(809) 860-8472',0x623d01790664d2bb47f126ec98b32c74,0x1d628207b5edbd406ba36d4d2ed0d1047de5ef5ec95f3df255dc1612dda8fa89),
('f31f4573-9cde-4dc0-9d9a-02e1482b905e',N'ruth.maira@grupotejeda.local',N'Ruth Maira',N'Región Norte y Noreste',N'(849) 853-3998',0x068210f788209e005f58f242ac62c9df,0x9a443057c30dad8257f20d180c5fef7c05c56b2796a6d26954862e4c20c1b3b3),
('72b1cced-1faf-407a-b3ae-110be792902b',N'ruth@grupotejeda.local',N'Ruth',N'Gran Santo Domingo',N'(809) 497-9140',0x1444b4f4d768f03b4947b7637f74f46b,0x92c0dab82470ca9bff0dd8c61cb86d1abbc5d264ff796d646055a4755f1d12d1),
('7fa45902-adef-45ad-8c91-bf9b855ab62a',N'santa.mota@grupotejeda.local',N'Santa Mota',N'Región Este',N'(849) 642-9111',0x9c0918a233e6fad4af5c33deeda3def5,0x66844c3ba7b70248d33ca6b278511c63cbf759834d9ccef1c7294ffed8aa808a),
('4f2e3460-0fe7-4301-a7b2-b5dae867314c',N'santos.martinez@grupotejeda.local',N'Santos Martinez',N'Gran Santo Domingo',N'(849) 220-1567',0xba1aca7369ccffd762dc9b61e39b3a08,0x8851948da61bf335cc429d3c18be49ac93011b8d6247f701d9648de0b31755d3),
('c00ec047-268e-44a6-86c4-7e562829563c',N'treisy.baez@grupotejeda.local',N'Treisy Báez',N'Región Sur · Barahona',N'(809) 761-4317',0xbb162bd3129444f50860af2333f79f8b,0x33a89d9fdfb420cea331fd1f60a070ae70e261f5bed845f4088b4f247fa16cf1),
('3eddb672-dff1-4b65-aaf6-a1e029105db2',N'waskar.sepulveda@grupotejeda.local',N'Waskar Sepulveda',N'Gran Santo Domingo',N'(829) 714-2071',0x97b89897772fd8c084e992a55466ff40,0x519e648394f63e299440c35a0e5fcc65e155ac380f246111d7a37f51ba77f4f4),
('6df7d9b8-5aa2-41e0-9709-de806b162e69',N'wileilin.caraballo@grupotejeda.local',N'Wileilin Caraballo',N'Gran Santo Domingo',N'(829) 727-0234',0x56cfec159b7b56501b341f14a77282a7,0xa7069fe48c3c488c3bce6526891fb27821627fd5296d3d29d4d3578afe101acb),
('0feb7a32-4c78-4d34-b866-f3fac9ad4472',N'wilmer@grupotejeda.local',N'Wilmer',N'Grupos Administrativos',N'(829) 579-8620',0x4f1b82205178c0b30088447f8bc071ba,0xa0111a862f0329d3cd71a797939244d30bd38f8a94ba5b5f1d4654c0fb3483bf),
('8935ab04-0ac6-405b-bde8-38b734d91c63',N'xiomara@grupotejeda.local',N'Xiomara',N'Gran Santo Domingo',N'(829) 814-7048',0x75fadd8ff4710a563081db26fb332889,0x9e287c3f8b877e7e04c097384fd5b3fc34bc61fa1c335b784ebc055313e7857a),
('81685541-c969-4ce4-8855-de1af2a4c780',N'yanilka.torres@grupotejeda.local',N'Yanilka Torres',N'Región Norte y Noreste',N'(829) 222-5313',0xdb38d89f1d6f95214744f329e84bd297,0x253ba2a7571f692d3f9ee955891341316278f6d12ab86a46bce878a94ba9a235),
('313e7b5d-d8de-4852-a79d-3f9099201c2d',N'yanirys@grupotejeda.local',N'Yanirys',N'Región Norte y Noreste',N'(849) 268-3452',0x36577271443634244ef285ae5455fe74,0x978d7d93d30d8833630416c89bde7ec7a590f8b5d503685c55f984f44b772159),
('bb27df16-e1f0-4ddb-b6e8-2524e8194874',N'yany.7062@grupotejeda.local',N'Yany',N'Gran Santo Domingo',N'(809) 405-7062',0x4bbef1114c61431596da2be7a859815d,0x3c81a3019297fe82cd87333f0ef421581639c397699924e0745471ebec9b793d),
('3f98d683-0418-4e20-a3dd-a4a3123b8f27',N'yany@grupotejeda.local',N'Yany',N'Región Este',N'(809) 269-0328',0x2982687663b7f9dd00150cf73ba91d55,0x7e22deb67efa518e667fd0bd23107f8ce5b19236d347f643ce9018c9a65407b7);

UPDATE target SET display_name=source.display_name,role='GROUP_ADMIN',region=source.region,
    contact=source.contact,is_active=1,updated_at=SYSUTCDATETIME()
FROM dbo.admin_users target
INNER JOIN #ImportedUsers source ON target.id=source.id OR target.email=source.email;

INSERT INTO dbo.admin_users(id,email,display_name,role,password_salt,password_hash,password_iterations,is_active,region,contact,must_change_password)
SELECT source.id,source.email,source.display_name,'GROUP_ADMIN',source.password_salt,source.password_hash,210000,1,source.region,source.contact,1
FROM #ImportedUsers source
WHERE NOT EXISTS(SELECT 1 FROM dbo.admin_users target WHERE target.id=source.id OR target.email=source.email);

CREATE TABLE #Assignments(email nvarchar(320),group_name nvarchar(150),region nvarchar(120));
INSERT INTO #Assignments(email,group_name,region) VALUES
(N'amarilys.reyes@grupotejeda.local',N'2-AMARILYS | HIGUEY',N'Región Este'),
(N'amarilys.reyes@grupotejeda.local',N'HIGUEY DEPORTIVAS',N'Región Este'),
(N'ana.cedeno@grupotejeda.local',N'132-LUMISMART BAYAHIBE BENERITO',N'Región Este'),
(N'anrys.rodriguez@grupotejeda.local',N'ARISMENDI LA FORTUNA',N'Gran Santo Domingo'),
(N'antony.guzman@grupotejeda.local',N'BRYAN B',N'Grupos Administrativos'),
(N'antony.guzman@grupotejeda.local',N'BRYAN | LUIS ALBERTO',N'Grupos Administrativos'),
(N'ariel.garcia@grupotejeda.local',N'135-SOCIEDAD DARIO | ALBERTO D',N'Grupos Administrativos'),
(N'benito@grupotejeda.local',N'57-EL CERCADO BENITO',N'Región Sur'),
(N'benjamin.a.mercedes@grupotejeda.local',N'197-TEJEDA PIMENTEL| SDE,',N'Gran Santo Domingo'),
(N'benjamin.a.mercedes@grupotejeda.local',N'3-ALBERTO PROPIAS',N'Gran Santo Domingo'),
(N'benjamin.a.mercedes@grupotejeda.local',N'ALBERTO SPORT',N'Gran Santo Domingo'),
(N'carlos.brisas@grupotejeda.local',N'63-CARLOS BRIZAS',N'Gran Santo Domingo'),
(N'carolina.rodriguez@grupotejeda.local',N'13-RICHARD MANGO M',N'Gran Santo Domingo'),
(N'carolina.rodriguez@grupotejeda.local',N'28-RICHARD JUMBO',N'Gran Santo Domingo'),
(N'cocha@grupotejeda.local',N'TIERRA NUEVA JIMANI COCHA',N'Región Sur'),
(N'cornelio.fortuna@grupotejeda.local',N'127-FORTUNA F',N'Gran Santo Domingo'),
(N'cristina.montero@grupotejeda.local',N'130-LUMISMART BAVARO | PUNTA CANA',N'Región Este'),
(N'cristina.montero@grupotejeda.local',N'BAVARO B',N'Región Este'),
(N'cristina.montero@grupotejeda.local',N'LOTTO REAL PUNTA CANA',N'Región Este'),
(N'darlyn.trump@grupotejeda.local',N'17-MONTE PLATA',N'Región Norte y Noreste'),
(N'dauryn@grupotejeda.local',N'58-FERNANDO',N'Gran Santo Domingo'),
(N'david.tavares@grupotejeda.local',N'DAVID | ASOCIADO',N'Gran Santo Domingo'),
(N'divanna.jimenez@grupotejeda.local',N'4-DIVANNA BOCA CHICA',N'Gran Santo Domingo'),
(N'dominga@grupotejeda.local',N'5-LUIS ALBERTO/ CAPOTILLO PROPIA',N'Gran Santo Domingo'),
(N'dominga@grupotejeda.local',N'96-DOMINGA PROPIA',N'Gran Santo Domingo'),
(N'domingo.mameyes@grupotejeda.local',N'41-DOMINGO MAMEYES',N'Gran Santo Domingo'),
(N'dulce.maria@grupotejeda.local',N'LOTTO REAL SANCHEZ',N'Región Norte y Noreste'),
(N'dulce.maria@grupotejeda.local',N'SANCHEZ',N'Región Norte y Noreste'),
(N'eduardo.paredes@grupotejeda.local',N'196-KINITO | SAMANA RIFERO',N'Región Norte y Noreste'),
(N'enerio@grupotejeda.local',N'85-DR.ENERIO E',N'Gran Santo Domingo'),
(N'fatima@grupotejeda.local',N'147-YUMA',N'Región Este'),
(N'felix.toribio@grupotejeda.local',N'112-CARREFOUR',N'Gran Santo Domingo'),
(N'felix.toribio@grupotejeda.local',N'16-JOSELITO | HS | ALBERTO',N'Gran Santo Domingo'),
(N'felix.toribio@grupotejeda.local',N'39-SOCIEDAD MERCADAL GLEDYS, NANCY, ALBERTO',N'Gran Santo Domingo'),
(N'felix.toribio@grupotejeda.local',N'AEROPUERTO | AILA | HIGUERO',N'Gran Santo Domingo'),
(N'felix.toribio@grupotejeda.local',N'METRO M',N'Gran Santo Domingo'),
(N'flor.maria@grupotejeda.local',N'29-LAS MATAS FLOR MARIA',N'Región Sur'),
(N'hedwing.nunez@grupotejeda.local',N'HEDWING | SAMANA RIFERO',N'Región Norte y Noreste'),
(N'henry.santos@grupotejeda.local',N'LUIS TEJEDA .D.N',N'Gran Santo Domingo'),
(N'hijo@grupotejeda.local',N'45-EL PRINCIPE P',N'Gran Santo Domingo'),
(N'iris.alcantara@grupotejeda.local',N'66-BUJIA | BARAHONA',N'Región Sur'),
(N'ivelise.martinez@grupotejeda.local',N'144-LUMISMART KM YAMASA',N'Gran Santo Domingo · Región Norte y Noreste'),
(N'ivelise.martinez@grupotejeda.local',N'SOCIEDAD SANDRA YAMASA',N'Gran Santo Domingo · Región Norte y Noreste'),
(N'ivelise.martinez@grupotejeda.local',N'VILLA MELLA V',N'Gran Santo Domingo · Región Norte y Noreste'),
(N'jeidi.castillo@grupotejeda.local',N'138-BANI',N'Región Sur'),
(N'jhordy.arias@grupotejeda.local',N'201-GRUPO - J.A.T',N'Gran Santo Domingo'),
(N'jhordy.arias@grupotejeda.local',N'JHORDY SPORT',N'Gran Santo Domingo'),
(N'jhoscarlo.suriel@grupotejeda.local',N'150-SURIEL | GRUPO 1',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'151-SURIEL | GRUPO 2',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'152-SURIEL | GRUPO 3',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'153-SURIEL | GRUPO 4',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'154-SURIEL | GRUPO 5',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'155-SURIEL | GRUPO 6',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'161-SURIEL | GRUPO 13',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'184-SURIEL | GRUPO 11',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'185-SURIEL | GRUPO 16 SANTO DOMINGO',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'186-SURIEL | GRUPO 17 CARMEN M .',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'192-SURIEL | GRUPO 18',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'198-SURIEL | GRUPO 19. H.',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'206-GRUPO 20 EL CONVENTO SURIEL',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'211-SURIEL | GRUPO 7 CORALITOS',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'LOTTO REAL CONSTANZA',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'LOTTO REAL JARABACOA',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'SURIEL BISONO | GRUPO 14',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'SURIEL GRUPO 22 - SANTO DOMINGO',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'SURIEL | GRUPO 21 Y.',N'Región Norte y Noreste'),
(N'jhoscarlo.suriel@grupotejeda.local',N'SURIEL | GRUPO 8 CONSTANZA',N'Región Norte y Noreste'),
(N'jinet.roman@grupotejeda.local',N'164-MAO VALVERDE',N'Región Norte y Noreste'),
(N'joel.vizcaino@grupotejeda.local',N'019 - OZAMA',N'Gran Santo Domingo'),
(N'joel.vizcaino@grupotejeda.local',N'JOEL | PROPIAS',N'Gran Santo Domingo'),
(N'jose.batista.sandy@grupotejeda.local',N'145-LUMISMART CAPITAL',N'Gran Santo Domingo'),
(N'jose.batista.sandy@grupotejeda.local',N'204-GRUPO EDUARD GUZMAN',N'Gran Santo Domingo'),
(N'jose.batista.sandy@grupotejeda.local',N'205-PAPO ESTRELLA',N'Gran Santo Domingo'),
(N'jose.mesa@grupotejeda.local',N'53-JOSE MESA J',N'Gran Santo Domingo'),
(N'jose.ramon@grupotejeda.local',N'26-JOSE RAMON | SABANA P.',N'Gran Santo Domingo'),
(N'julio.rosario@grupotejeda.local',N'NEY',N'Gran Santo Domingo'),
(N'junior.bono@grupotejeda.local',N'72-EDDY JUNIOR',N'Gran Santo Domingo'),
(N'junior.melendez@grupotejeda.local',N'210-MIGUEL RODRIGUEZ ( LA FAMOSA)',N'Gran Santo Domingo'),
(N'junior.simon@grupotejeda.local',N'015 - Grupo Simon',N'Gran Santo Domingo'),
(N'kiko@grupotejeda.local',N'209-KIKO ROMANA',N'Región Este'),
(N'leolidia.jimenez@grupotejeda.local',N'195-JYL SOCIEDAD',N'Gran Santo Domingo'),
(N'levi.marte@grupotejeda.local',N'203-JHONATAN SILVESTRE HIJO / SOCIEDAD',N'Gran Santo Domingo'),
(N'levi.marte@grupotejeda.local',N'43-COMPADRE JHONNY C',N'Gran Santo Domingo'),
(N'levi.marte@grupotejeda.local',N'SONADORA HS',N'Gran Santo Domingo'),
(N'lucy.cordero@grupotejeda.local',N'171-GRUPO AZUA',N'Región Sur'),
(N'luis.manuel@grupotejeda.local',N'181-LOS BATEYES / BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'64-BARAHONA CIENAGA | BAHORUCO',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'75-BARAHONA | PARAISO',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'76-BARAHONA | POLO',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'78-BARAHONA | NEIBA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'80-BARAHONA MELLA | DUVERGE',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'81-BARAHONA | PEÑON',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'84-BARAHONA CENTRO CIUDAD | BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'AGENCIAS SPORT',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'BARAHONA LOS RIOS | VILLA JARAGUA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'BARAHONA SPORT',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'BARAHONA | BOCA DE CACHON TIERRA NUEVA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'BARAHONA | ENRIQUILLO',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'BARAHONA | GALVAN',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'BARAHONA | INDEPENDENCIA & DESCUBIERTA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'EL LIMON JIMANI | BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'FONDO NEGRO / CANOA | BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'JIMANI / BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'NEIBA 1',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'SOCIEDAD ANTONIO RODRIGUEZ BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'SOCIEDAD RICARDO SANCHEZ BARAHONA',N'Región Sur · Barahona'),
(N'luis.manuel@grupotejeda.local',N'SOCIEDAD YEISON GARCIA BARAHONA',N'Región Sur · Barahona'),
(N'mario.mejia@grupotejeda.local',N'0207 - Mario Romana',N'Región Este'),
(N'maryelin.herrera@grupotejeda.local',N'027 -ROMANA',N'Región Este'),
(N'maryelin.herrera@grupotejeda.local',N'LOTTO REAL ROMANA',N'Región Este'),
(N'maryelin.herrera@grupotejeda.local',N'LUIS | RIFERO ROMANA',N'Región Este'),
(N'maryelin.herrera@grupotejeda.local',N'ROMANA | FELO',N'Región Este'),
(N'maximo.de.jesus@grupotejeda.local',N'89-EL TIO MAXIMO',N'Gran Santo Domingo'),
(N'mechi@grupotejeda.local',N'48-MECHI INVIVIENDA',N'Gran Santo Domingo'),
(N'naomy@grupotejeda.local',N'77-BARAHONA VICENTE NOBLES | TAMAYO',N'Región Sur · Barahona'),
(N'nelson.breton@grupotejeda.local',N'50-NELSON BRETON',N'Gran Santo Domingo'),
(N'nelson.perez@grupotejeda.local',N'18-NELSON PEDERNALES',N'Región Sur'),
(N'nelson.perez@grupotejeda.local',N'ALBERTO PEDERNALES',N'Región Sur'),
(N'pamela.alvarez@grupotejeda.local',N'92-GALLO G',N'Gran Santo Domingo'),
(N'pedro.toribio@grupotejeda.local',N'LOS MINA PEDRITO',N'Gran Santo Domingo'),
(N'peje@grupotejeda.local',N'62-PEJE | CAPITAL',N'Gran Santo Domingo'),
(N'peje@grupotejeda.local',N'PEJE | CASTILLO',N'Gran Santo Domingo'),
(N'piter.ravix@grupotejeda.local',N'59-RIFEROS_FM_SOCIEDAD',N'Gran Santo Domingo'),
(N'piter.ravix@grupotejeda.local',N'7-FRANKLIN REYES | SOCIEDAD TEJEDA',N'Gran Santo Domingo'),
(N'robert.medina@grupotejeda.local',N'54-HONDO VALLE MACHO FM',N'Región Sur'),
(N'robert.medina@grupotejeda.local',N'HONDO VALLE MACHO ARRENDADAS',N'Región Sur'),
(N'robinson.luciano@grupotejeda.local',N'165-VALIENTE SDE',N'Gran Santo Domingo'),
(N'ruth.maira@grupotejeda.local',N'LOTTO REAL SAMANA',N'Región Norte y Noreste'),
(N'ruth.maira@grupotejeda.local',N'SAMANA',N'Región Norte y Noreste'),
(N'ruth.maira@grupotejeda.local',N'SOCIEDAD COLOMBO/ SAMANA',N'Región Norte y Noreste'),
(N'ruth@grupotejeda.local',N'74-DAVID | PROPIAS',N'Gran Santo Domingo'),
(N'santa.mota@grupotejeda.local',N'179-MICHES M',N'Región Este'),
(N'santos.martinez@grupotejeda.local',N'GRUPO SANTO',N'Gran Santo Domingo'),
(N'treisy.baez@grupotejeda.local',N'105-BARAHONA | CABRAL',N'Región Sur · Barahona'),
(N'treisy.baez@grupotejeda.local',N'69-BARAHONA CABRAL | PACHI',N'Región Sur · Barahona'),
(N'waskar.sepulveda@grupotejeda.local',N'146-WASCAR SP',N'Gran Santo Domingo'),
(N'wileilin.caraballo@grupotejeda.local',N'137-JAVIER TEJEDA',N'Gran Santo Domingo'),
(N'wilmer@grupotejeda.local',N'118-SOCIEDAD ARIAS TEJEDA',N'Grupos Administrativos'),
(N'xiomara@grupotejeda.local',N'61-XIOMARA X',N'Gran Santo Domingo'),
(N'yanilka.torres@grupotejeda.local',N'182- SAJOMA PROPIO',N'Región Norte y Noreste'),
(N'yanilka.torres@grupotejeda.local',N'SANTIAGO | GRUPO 7',N'Región Norte y Noreste'),
(N'yanilka.torres@grupotejeda.local',N'SANTIAGO | GRUPO 8',N'Región Norte y Noreste'),
(N'yanilka.torres@grupotejeda.local',N'SANTIAGO | GRUPO 9 & 12',N'Región Norte y Noreste'),
(N'yanirys@grupotejeda.local',N'143-BAYAGUANA SOCIEDAD FM RICHARD',N'Región Norte y Noreste'),
(N'yanirys@grupotejeda.local',N'BAYAGUANA SPORT',N'Región Norte y Noreste'),
(N'yany.7062@grupotejeda.local',N'35-FRANKLIN MAMEYES',N'Gran Santo Domingo'),
(N'yany@grupotejeda.local',N'126-NISIBON N',N'Región Este');

DELETE ug FROM dbo.admin_user_groups ug INNER JOIN dbo.admin_users u ON u.id=ug.user_id INNER JOIN #ImportedUsers i ON i.id=u.id OR i.email=u.email;
INSERT INTO dbo.admin_user_groups(user_id,group_name,region)
SELECT DISTINCT u.id,a.group_name,a.region
FROM #Assignments a
INNER JOIN #ImportedUsers i ON i.email=a.email
INNER JOIN dbo.admin_users u ON u.id=i.id OR u.email=i.email
INNER JOIN (SELECT DISTINCT grupo FROM dbo.agencies WHERE is_active=1) g ON g.grupo=a.group_name;
DROP TABLE #Assignments;
DROP TABLE #ImportedUsers;
COMMIT TRANSACTION;

SELECT COUNT(*) AS administradores_de_grupo FROM dbo.admin_users WHERE role='GROUP_ADMIN' AND is_active=1;
SELECT COUNT(*) AS asignaciones_activas FROM dbo.admin_user_groups;
SELECT a.grupo FROM (SELECT DISTINCT grupo FROM dbo.agencies WHERE is_active=1) a LEFT JOIN dbo.admin_user_groups ug ON ug.group_name=a.grupo WHERE ug.user_id IS NULL ORDER BY a.grupo;
