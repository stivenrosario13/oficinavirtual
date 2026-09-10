$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$staging = Join-Path $root "artifacts\FRONTEND_V122_ASSETS_TEMP"
$destination = Join-Path $root "artifacts\01_SUBIR_V124_TARJETAS_Y_MANTENIMIENTO.zip"
$activation = Join-Path $root "artifacts\02_RENOMBRAR_COMO_INDEX_HTML_V124.txt"

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
$assetRoot = Join-Path $staging "wwwroot\assets"
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $source "assets\index-BSX3o6yx.js") -Destination $assetRoot
Copy-Item -LiteralPath (Join-Path $source "assets\index-BJYY6xIy.css") -Destination $assetRoot
Copy-Item -LiteralPath (Join-Path $source "assets\index.es-CmFKOMe2.js") -Destination $assetRoot
Copy-Item -LiteralPath (Join-Path $source "assets\jspdf.es.min-BZAFP1Cf.js") -Destination $assetRoot
Copy-Item -LiteralPath (Join-Path $source "index.html") -Destination $activation -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
$archive = [System.IO.Compression.ZipFile]::Open($destination, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $staging -Recurse -File | ForEach-Object {
        $entryName = $_.FullName.Substring($staging.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $_.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Remove-Item -LiteralPath $staging -Recurse -Force
Write-Host $destination
Write-Host $activation
