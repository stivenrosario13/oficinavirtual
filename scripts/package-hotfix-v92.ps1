$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\monster-v92-hotfix"
$zip = Join-Path $root "artifacts\HOTFIX_PERFIL_CHAT_500_MONSTERASP_V92.zip"
$files = @(
    "web.config",
    "RegistroAgencias.SqlServer.V92.dll",
    "RegistroAgencias.SqlServer.V92.deps.json",
    "RegistroAgencias.SqlServer.V92.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V92.staticwebassets.endpoints.json"
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($name in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) {
        throw "Falta el archivo requerido: $name"
    }
}
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
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
