$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\ENTREGA_MONSTER\02_DENTRO_DE_WWWROOT"
$zip = Join-Path $root "artifacts\HOTFIX_MONSTERASP_V90_A_V91.zip"
$files = @(
    "web.config",
    "RegistroAgencias.SqlServer.V91.dll",
    "RegistroAgencias.SqlServer.V91.deps.json",
    "RegistroAgencias.SqlServer.V91.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V91.staticwebassets.endpoints.json"
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($name in $files) {
    $path = Join-Path $source $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Falta un archivo de publicación requerido: $path"
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
