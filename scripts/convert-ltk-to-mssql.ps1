$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root "database\005_import_agencies_ltk.sql"
$destinationPath = Join-Path $root "database-mssql\005_import_agencies_ltk.sql"
$records = [System.Collections.Generic.List[object]]::new()
$pattern = "^\(UUID\(\),'((?:''|[^'])*)','((?:''|[^'])*)','((?:''|[^'])*)','((?:''|[^'])*)','PENDING'\)[,;]?$"

foreach ($line in [System.IO.File]::ReadLines($sourcePath)) {
    $match = [regex]::Match($line.Trim(), $pattern)
    if ($match.Success) {
        $records.Add([pscustomobject]@{
            SourceKey = $match.Groups[1].Value
            Codigo = $match.Groups[2].Value
            Terminal = $match.Groups[3].Value
            Grupo = $match.Groups[4].Value
        })
    }
}

if ($records.Count -ne 3319) { throw "Se esperaban 3319 agencias y se encontraron $($records.Count)." }
$groups = $records | Select-Object -ExpandProperty Grupo -Unique
if ($groups.Count -ne 169) { throw "Se esperaban 169 grupos y se encontraron $($groups.Count)." }

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("-- Importacion Grupo Tejeda - SQL Server 2025 / MonsterASP.NET")
$lines.Add("-- Fuente: LTKResultadoBrutoDetalladoApi (2) (1).xlsx")
$lines.Add("-- Registros unicos: 3319 | Grupos: 169")
$lines.Add("")
$lines.Add("SET NOCOUNT ON;")
$lines.Add("SET XACT_ABORT ON;")
$lines.Add("BEGIN TRANSACTION;")
$lines.Add("")
$lines.Add("DELETE a")
$lines.Add("FROM dbo.agencies a")
$lines.Add("LEFT JOIN dbo.agency_profiles p ON p.agency_id=a.id")
$lines.Add("WHERE a.source_key LIKE N'DEMO-%' AND p.id IS NULL;")
$lines.Add("")
$lines.Add("CREATE TABLE #LTKImport (")
$lines.Add("    source_key nvarchar(200) NOT NULL PRIMARY KEY,")
$lines.Add("    codigo nvarchar(100) NOT NULL,")
$lines.Add("    terminal nvarchar(200) NOT NULL,")
$lines.Add("    grupo nvarchar(150) NOT NULL")
$lines.Add(");")

$batchSize = 500
for ($start = 0; $start -lt $records.Count; $start += $batchSize) {
    $end = [Math]::Min($start + $batchSize, $records.Count)
    $lines.Add("")
    $lines.Add("INSERT INTO #LTKImport(source_key,codigo,terminal,grupo) VALUES")
    for ($index = $start; $index -lt $end; $index++) {
        $record = $records[$index]
        $suffix = if ($index -eq $end - 1) { ";" } else { "," }
        $lines.Add("(N'$($record.SourceKey)',N'$($record.Codigo)',N'$($record.Terminal)',N'$($record.Grupo)')$suffix")
    }
}

$lines.Add("")
$lines.Add("UPDATE target")
$lines.Add("SET target.codigo=source.codigo,")
$lines.Add("    target.terminal=source.terminal,")
$lines.Add("    target.grupo=source.grupo,")
$lines.Add("    target.updated_at=SYSUTCDATETIME()")
$lines.Add("FROM dbo.agencies target")
$lines.Add("INNER JOIN #LTKImport source ON source.source_key=target.source_key;")
$lines.Add("")
$lines.Add("INSERT INTO dbo.agencies(id,source_key,codigo,terminal,grupo,status)")
$lines.Add("SELECT CONVERT(char(36),NEWID()),source.source_key,source.codigo,source.terminal,source.grupo,'PENDING'")
$lines.Add("FROM #LTKImport source")
$lines.Add("WHERE NOT EXISTS (SELECT 1 FROM dbo.agencies target WHERE target.source_key=source.source_key);")
$lines.Add("")
$lines.Add("DROP TABLE #LTKImport;")
$lines.Add("COMMIT TRANSACTION;")
$lines.Add("")
$lines.Add("SELECT grupo,COUNT(*) AS agencias FROM dbo.agencies GROUP BY grupo ORDER BY grupo;")
$lines.Add("SELECT COUNT(*) AS total_agencias FROM dbo.agencies;")

[System.IO.File]::WriteAllLines($destinationPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generado: $destinationPath ($($records.Count) agencias, $($groups.Count) grupos)"
