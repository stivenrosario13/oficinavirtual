$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "license-server\LicenseServer.csproj"
$artifacts = Join-Path $root "artifacts\LICENSE_SERVER"
$published = Join-Path $artifacts "wwwroot"
$zip = Join-Path $root "artifacts\SERVIDOR_CENTRAL_LICENCIAS_MONSTERASP.zip"
$directZip = Join-Path $root "artifacts\SERVIDOR_LICENCIAS_EXTRAER_DENTRO_DE_WWWROOT.zip"
if (Test-Path $artifacts) { Remove-Item -LiteralPath $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $published -Force | Out-Null
& dotnet publish $project -c Release -o $published --no-self-contained --no-restore
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación del servidor de licencias." }
Copy-Item -LiteralPath (Join-Path $root "license-server\database\001_license_server.sql") -Destination (Join-Path $artifacts "01_EJECUTAR_EN_SQL_SERVER.sql")
Copy-Item -LiteralPath (Join-Path $root "license-server\INSTRUCCIONES_MONSTER.md") -Destination $artifacts
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($zip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $artifacts -Recurse -File | ForEach-Object {
    $entryName=$_.FullName.Substring($artifacts.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null
  }
} finally { $archive.Dispose() }
Write-Host "Servidor central listo: $zip" -ForegroundColor Green
if (Test-Path $directZip) { Remove-Item -LiteralPath $directZip -Force }
$directArchive = [System.IO.Compression.ZipFile]::Open($directZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $published -Recurse -File | ForEach-Object {
    $entryName=$_.FullName.Substring($published.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($directArchive,$_.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null
  }
} finally { $directArchive.Dispose() }
Write-Host "Contenido directo de wwwroot: $directZip" -ForegroundColor Green
