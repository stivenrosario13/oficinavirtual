$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$zip = Join-Path $root "artifacts\CORRECCION_PDF_GERENCIA_SIN_ARROBA.zip"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (-not (Test-Path -LiteralPath (Join-Path $source "index.html"))) { throw "Falta compilar el frontend." }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($zip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
        $relative = "wwwroot/" + $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Write-Output $zip
