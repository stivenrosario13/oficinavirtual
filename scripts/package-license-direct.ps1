$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sourceZip = Join-Path $root "artifacts\SERVIDOR_CENTRAL_LICENCIAS_MONSTERASP.zip"
$directZip = Join-Path $root "artifacts\SERVIDOR_LICENCIAS_EXTRAER_DENTRO_DE_WWWROOT.zip"
if (-not (Test-Path $sourceZip)) { throw "No existe el paquete publicado del servidor." }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $directZip) { Remove-Item -LiteralPath $directZip -Force }
$sourceArchive = [System.IO.Compression.ZipFile]::OpenRead($sourceZip)
$archive = [System.IO.Compression.ZipFile]::Open($directZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  $sourceArchive.Entries | Where-Object { $_.FullName.StartsWith("wwwroot/") -and $_.Name } | ForEach-Object {
    $entryName=$_.FullName.Substring("wwwroot/".Length)
    $entry=$archive.CreateEntry($entryName,[System.IO.Compression.CompressionLevel]::Optimal)
    $input=$_.Open(); $output=$entry.Open()
    try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
  }
} finally { $archive.Dispose(); $sourceArchive.Dispose() }
Write-Host $directZip
