$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V192_PUBLISH_TEMP"
$public = Join-Path $root "api\wwwroot"
$fullZip = Join-Path $artifacts "V192_CATEGORIAS_DEPARTAMENTALES_COMPLETO.zip"
$visualZip = Join-Path $artifacts "V192_CATEGORIAS_DEPARTAMENTALES_SOLO_INTERFAZ.zip"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Add-Tree($archive, [string]$source, [string]$prefix) {
  Get-ChildItem -LiteralPath $source -Recurse -File | Where-Object { $_.Name -ne "web.config" } | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    $entryName = if ($prefix) { "$prefix/$relative" } else { $relative }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
}

foreach ($zipPath in @($fullZip,$visualZip)) {
  if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
}

$archive = [System.IO.Compression.ZipFile]::Open($fullZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  Add-Tree $archive $publish ""
  Add-Tree $archive $public ""
  # web.config se agrega al final: IIS cambia a V192 después de recibir la DLL nueva.
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $publish "web.config"), "web.config", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally { $archive.Dispose() }

$archive = [System.IO.Compression.ZipFile]::Open($visualZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  Add-Tree $archive $public ""
  Add-Tree $archive $public "wwwroot"
}
finally { $archive.Dispose() }

Write-Host "V192 creada en las dos rutas publicas posibles." -ForegroundColor Green
