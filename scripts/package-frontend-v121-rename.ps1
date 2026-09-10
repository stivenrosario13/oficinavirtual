$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$staging = Join-Path $root "artifacts\FRONTEND_V121_RENAME_TEMP"
$destination = Join-Path $root "artifacts\SUBIR_FRONTEND_V121_SIN_REEMPLAZAR_INDEX.zip"

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
$staticRoot = Join-Path $staging "wwwroot"
$assetRoot = Join-Path $staticRoot "assets"
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $source "index.html") -Destination (Join-Path $staticRoot "index-nuevo-v121.html")
Copy-Item -LiteralPath (Join-Path $source "assets\index-DVe9hTb9.js") -Destination $assetRoot
Copy-Item -LiteralPath (Join-Path $source "assets\index.es-BesyT4Zh.js") -Destination $assetRoot
Copy-Item -LiteralPath (Join-Path $source "assets\jspdf.es.min-WWqAajNZ.js") -Destination $assetRoot

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $destination, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $staging -Recurse -Force
Write-Host $destination
