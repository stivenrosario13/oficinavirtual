$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\monster-v89-portable"
$zip = "C:\Users\pc\Downloads\PROYECTO_COMPLETO_MONSTERASP_V89_PORTABLE_FINAL.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zip) { throw "El paquete final ya existe: $zip" }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($source.Length).TrimStart([char[]]@("\", "/")).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $_.FullName,
            $entry,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally { $archive.Dispose() }
Write-Output $zip
