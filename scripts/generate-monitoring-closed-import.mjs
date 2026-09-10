import fs from "node:fs/promises";

const root = "C:/Users/pc/Desktop/registro-agencias";
const source = "C:/Users/pc/Downloads/Monitoreo Terminales (11).xls";
const html = await fs.readFile(source, "utf8");
const activeSql = await fs.readFile(`${root}/database-mssql/005_import_agencies_ltk.sql`, "utf8");
const historicalSql = await fs.readFile(`${root}/database-mssql/010_import_closed_agencies.sql`, "utf8");
const clean = (value) => String(value ?? "").replace(/<[^>]+>/g, " ").replace(/&nbsp;/gi, " ").replace(/\s+/g, " ").trim();
const normalize = (value) => clean(value).normalize("NFD").replace(/\p{Diacritic}/gu, "").toUpperCase();
const sqlText = (value) => `N'${value.replaceAll("'", "''")}'`;

const catalogRows = [...`${activeSql}\n${historicalSql}`.matchAll(/N'LTK:([^']+)'\s*,N'([^']*)'\s*,N'([^']*)'\s*,N'([^']*)'\s*,N'([^']*)'/g)]
  .map((match) => ({ id: match[1], codigo: match[2], terminal: match[3], grupo: match[4], region: match[5] }));
const byId = new Map(catalogRows.map((row) => [row.id, row]));
const regionByGroup = new Map(catalogRows.map((row) => [normalize(row.grupo), row.region]));
const activeIds = new Set([...activeSql.matchAll(/N'LTK:([^']+)'/g)].map((match) => match[1]));
const historicalIds = new Set([...historicalSql.matchAll(/N'LTK:([^']+)'/g)].map((match) => match[1]));

let currentGroup = "";
const monitoring = [];
const rowMatches = [...html.matchAll(/<tr(?:\s[^>]*)?>([\s\S]*?)<\/tr>/gi)];
for (const row of rowMatches) {
  const cells = [...row[1].matchAll(/<t[dh][^>]*>([\s\S]*?)<\/t[dh]>/gi)].map((match) => clean(match[1]));
  if (!cells.length) continue;
  const groupMatch = cells[0].match(/^Grupo:\s*(.+)$/i);
  if (groupMatch) {
    currentGroup = clean(groupMatch[1]);
    continue;
  }
  const agencyMatch = cells[0].match(/^([^|]+?)\s*\|\s*(.+)$/);
  if (!agencyMatch) continue;
  const identity = clean(agencyMatch[1]).match(/^([^\s]+)\s*-\s*([^\s]+)$/);
  if (!identity || !currentGroup) continue;
  const id = clean(identity[1]);
  const codigo = clean(identity[2]);
  const terminal = clean(agencyMatch[2]);
  const existing = byId.get(id);
  monitoring.push({
    id,
    codigo,
    terminal,
    grupo: currentGroup,
    region: existing?.region ?? regionByGroup.get(normalize(currentGroup)) ?? "Sin región",
  });
}

const unique = [...new Map(monitoring.map((row) => [row.id, row])).values()];
if (unique.length !== 120) throw new Error(`Se esperaban 120 terminales cerradas y se encontraron ${unique.length}.`);
const alreadyClosed = unique.filter((row) => historicalIds.has(row.id)).length;
const previouslyActive = unique.filter((row) => activeIds.has(row.id)).length;
const newClosed = unique.length - alreadyClosed - previouslyActive;

const lines = [
  "-- Importación de terminales cerradas desde Monitoreo Terminales (11).xls",
  `-- Total: ${unique.length} | Ya cerradas: ${alreadyClosed} | Antes activas: ${previouslyActive} | Nuevas: ${newClosed}`,
  "SET NOCOUNT ON;",
  "SET XACT_ABORT ON;",
  "IF OBJECT_ID(N'dbo.agencies',N'U') IS NULL THROW 50001,N'Falta dbo.agencies.',1;",
  "IF COL_LENGTH(N'dbo.agencies',N'operational_status') IS NULL",
  "    EXEC sys.sp_executesql N'ALTER TABLE dbo.agencies ADD operational_status varchar(20) NOT NULL CONSTRAINT DF_agencies_operational_status DEFAULT(''ACTIVE'') WITH VALUES;';",
  "BEGIN TRANSACTION;",
  "CREATE TABLE #MonitoringClosed(source_key nvarchar(200) NOT NULL PRIMARY KEY,codigo nvarchar(100) NOT NULL,terminal nvarchar(300) NOT NULL,grupo nvarchar(150) NOT NULL,region nvarchar(120) NOT NULL);",
  "INSERT INTO #MonitoringClosed(source_key,codigo,terminal,grupo,region) VALUES",
];
unique.forEach((row, index) => {
  const values = [sqlText(`LTK:${row.id}`), sqlText(row.codigo), sqlText(row.terminal), sqlText(row.grupo), sqlText(row.region)].join(",");
  lines.push(`(${values})${index === unique.length - 1 ? ";" : ","}`);
});
lines.push(
  "EXEC sys.sp_executesql N'",
  "MERGE dbo.agencies WITH(HOLDLOCK) AS target",
  "USING #MonitoringClosed AS source ON target.source_key=source.source_key",
  "WHEN MATCHED THEN UPDATE SET codigo=source.codigo,terminal=source.terminal,grupo=source.grupo,region=source.region,operational_status=''CLOSED'',is_active=0,updated_at=SYSUTCDATETIME()",
  "WHEN NOT MATCHED THEN INSERT(id,source_key,codigo,terminal,grupo,region,status,is_active,operational_status)",
  "VALUES(CONVERT(char(36),NEWID()),source.source_key,source.codigo,source.terminal,source.grupo,source.region,''PENDING'',0,''CLOSED'');",
  "';",
  "DECLARE @closedLoaded int=(SELECT COUNT(*) FROM dbo.agencies a INNER JOIN #MonitoringClosed m ON m.source_key=a.source_key WHERE a.operational_status='CLOSED' AND a.is_active=0);",
  "DROP TABLE #MonitoringClosed;",
  "COMMIT TRANSACTION;",
  `SELECT CASE WHEN @closedLoaded=${unique.length} THEN N'OK' ELSE N'REVISAR' END AS validacion,@closedLoaded AS agencias_cerradas_cargadas;`,
  "SELECT operational_status,is_active,COUNT(*) AS agencias FROM dbo.agencies WHERE source_key LIKE N'LTK:%' GROUP BY operational_status,is_active ORDER BY operational_status,is_active;",
);

await fs.writeFile(`${root}/database-mssql/011_import_closed_monitoring.sql`, `${lines.join("\r\n")}\r\n`, "utf8");
await fs.writeFile(`${root}/scratch/monitoring-xls/summary.json`, JSON.stringify({
  monitoring: unique.length,
  alreadyClosed,
  previouslyActive,
  newClosed,
  groups: new Set(unique.map((row) => row.grupo)).size,
}, null, 2), "utf8");
console.log(JSON.stringify({ monitoring: unique.length, alreadyClosed, previouslyActive, newClosed, groups: new Set(unique.map((row) => row.grupo)).size }));
