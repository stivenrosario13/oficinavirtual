import fs from "node:fs/promises";

const root = "C:/Users/pc/Desktop/registro-agencias";
const raw = JSON.parse(await fs.readFile(`${root}/scratch/spreadsheet_import/output/values.json`, "utf8"))[0].values;
const activeSql = await fs.readFile(`${root}/database-mssql/005_import_agencies_ltk.sql`, "utf8");
const activeRows = [...activeSql.matchAll(/N'LTK:([^']+)'\s*,N'([^']*)'\s*,N'([^']*)'\s*,N'([^']*)'\s*,N'([^']*)'/g)]
  .map((match) => ({ id: match[1], codigo: match[2], terminal: match[3], grupo: match[4], region: match[5] }));
const activeIds = new Set(activeRows.map((row) => row.id));
const regionByGroup = new Map(activeRows.map((row) => [row.grupo, row.region]));
const clean = (value) => String(value ?? "").trim().replace(/\s+/g, " ");
const sqlText = (value) => `N'${value.replaceAll("'", "''")}'`;
const closed = [];

for (let index = 10; index < raw.length; index += 1) {
  const terminalCell = clean(raw[index]?.[0]);
  const grupo = clean(raw[index]?.[1]);
  if (!terminalCell.startsWith("(") || !grupo) continue;
  const match = terminalCell.match(/^\(([^)]+)\)\s*([^\s|¦/?-]+)\s*(.*)$/);
  if (!match) continue;
  const id = clean(match[1]);
  if (activeIds.has(id)) continue;
  const codigo = clean(match[2]);
  const terminal = clean(match[3]).replace(/^[|¦/?-]+\s*/, "").replace(/\s*\(\d+\)\s*$/, "").trim() || codigo;
  closed.push({ id, codigo, terminal, grupo, region: regionByGroup.get(grupo) ?? "Sin región" });
}

if (activeRows.length !== 3122 || closed.length !== 197) {
  throw new Error(`Conteo inesperado: ${activeRows.length} activas y ${closed.length} cerradas.`);
}

const lines = [
  "-- Agencias cerradas Grupo Tejeda - SQL Server 2025",
  "-- Resultado esperado: 3122 activas + 197 cerradas = 3319 registros LTK.",
  "SET NOCOUNT ON;",
  "SET XACT_ABORT ON;",
  "IF OBJECT_ID(N'dbo.agencies',N'U') IS NULL THROW 50001,N'Falta dbo.agencies.',1;",
  "IF COL_LENGTH(N'dbo.agencies',N'operational_status') IS NULL",
  "    EXEC sys.sp_executesql N'ALTER TABLE dbo.agencies ADD operational_status varchar(20) NOT NULL CONSTRAINT DF_agencies_operational_status DEFAULT(''ACTIVE'') WITH VALUES;';",
  "BEGIN TRANSACTION;",
  "CREATE TABLE #ClosedAgencies(source_key nvarchar(200) NOT NULL PRIMARY KEY,codigo nvarchar(100) NOT NULL,terminal nvarchar(300) NOT NULL,grupo nvarchar(150) NOT NULL,region nvarchar(120) NOT NULL);",
];

for (let start = 0; start < closed.length; start += 500) {
  const batch = closed.slice(start, start + 500);
  lines.push("INSERT INTO #ClosedAgencies(source_key,codigo,terminal,grupo,region) VALUES");
  batch.forEach((row, index) => {
    const values = [sqlText(`LTK:${row.id}`), sqlText(row.codigo), sqlText(row.terminal), sqlText(row.grupo), sqlText(row.region)].join(",");
    lines.push(`(${values})${index === batch.length - 1 ? ";" : ","}`);
  });
}

lines.push(
  "EXEC sys.sp_executesql N'",
  "MERGE dbo.agencies WITH(HOLDLOCK) AS target",
  "USING #ClosedAgencies AS source ON target.source_key=source.source_key",
  "WHEN MATCHED THEN UPDATE SET codigo=source.codigo,terminal=source.terminal,grupo=source.grupo,region=source.region,operational_status=''CLOSED'',is_active=0,updated_at=SYSUTCDATETIME()",
  "WHEN NOT MATCHED THEN INSERT(id,source_key,codigo,terminal,grupo,region,status,is_active,operational_status)",
  "VALUES(CONVERT(char(36),NEWID()),source.source_key,source.codigo,source.terminal,source.grupo,source.region,''PENDING'',0,''CLOSED'');",
  "';",
  "DROP TABLE #ClosedAgencies;",
  "COMMIT TRANSACTION;",
  "SELECT operational_status,COUNT(*) AS agencias FROM dbo.agencies WHERE source_key LIKE N'LTK:%' GROUP BY operational_status ORDER BY operational_status;",
  "SELECT CASE WHEN SUM(CASE WHEN operational_status='ACTIVE' AND is_active=1 THEN 1 ELSE 0 END)=3122 AND SUM(CASE WHEN operational_status='CLOSED' AND is_active=0 THEN 1 ELSE 0 END)=197 THEN N'OK' ELSE N'REVISAR' END AS validacion,COUNT(*) AS total_ltk FROM dbo.agencies WHERE source_key LIKE N'LTK:%';",
);

await fs.writeFile(`${root}/database-mssql/010_import_closed_agencies.sql`, `${lines.join("\r\n")}\r\n`, "utf8");
console.log(JSON.stringify({ active: activeRows.length, closed: closed.length, total: activeRows.length + closed.length }));
