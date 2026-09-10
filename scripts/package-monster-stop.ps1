$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "deploy\app_offline.htm"
$zip = "C:\Users\pc\Downloads\PASO_1_DETENER_APLICACION_MONSTER.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive,
        $source,
        "app_offline.htm",
        [System.IO.Compression.CompressionLevel]::Optimal
    ) | Out-Null
}
finally { $archive.Dispose() }
Get-Item -LiteralPath $zip | Select-Object FullName, Length
