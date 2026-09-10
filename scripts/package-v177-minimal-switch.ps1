$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V192_PUBLISH_TEMP"
$zipPath = Join-Path $artifacts "V192_01_BACKEND_PRIMERO_MONSTA.zip"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path -LiteralPath $zipPath) {
  Remove-Item -LiteralPath $zipPath -Force
}

$files = @(
  "RegistroAgencias.SqlServer.V192.dll",
  "RegistroAgencias.SqlServer.V192.deps.json",
  "RegistroAgencias.SqlServer.V192.runtimeconfig.json",
  "RegistroAgencias.SqlServer.V192.exe"
)

$archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($file in $files) {
    $source = Join-Path $publish $file
    if (-not (Test-Path -LiteralPath $source)) {
      throw "No existe el archivo de publicación requerido: $source"
    }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $file, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }

  # Se escribe al final para que Monsta no active V192 antes de terminar la DLL nueva.
  $webConfig = Join-Path $publish "web.config"
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $webConfig, "web.config", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally {
  $archive.Dispose()
}

Write-Host "Paquete minimo V192 creado: $zipPath" -ForegroundColor Green
