$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\monster-v90-hotfix"
$zip = "C:\Users\pc\Downloads\HOTFIX_CHAT_500_MONSTERASP_V90.zip"
$files = @(
    "web.config",
    "RegistroAgencias.SqlServer.V90.dll",
    "RegistroAgencias.SqlServer.V90.deps.json",
    "RegistroAgencias.SqlServer.V90.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V90.staticwebassets.endpoints.json"
)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zip) { throw "El paquete ya existe: $zip" }
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            (Join-Path $source $name),
            $name,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally { $archive.Dispose() }
Write-Output $zip
