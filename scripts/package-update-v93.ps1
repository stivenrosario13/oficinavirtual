$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\monster-v93-update"
$zip = Join-Path $root "artifacts\ACTUALIZACION_FILTROS_CASOS_CHAT_MONSTERASP_V93.zip"
$rootFiles = @(
    "web.config",
    "RegistroAgencias.SqlServer.V93.dll",
    "RegistroAgencias.SqlServer.V93.deps.json",
    "RegistroAgencias.SqlServer.V93.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V93.staticwebassets.endpoints.json"
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($name in $rootFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) {
        throw "Falta el archivo requerido: $name"
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $source "wwwroot\index.html"))) {
    throw "Falta el frontend publicado."
}
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $rootFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $source $name),$name,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $webRoot = Join-Path $source "wwwroot"
    Get-ChildItem -LiteralPath $webRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Write-Output $zip
