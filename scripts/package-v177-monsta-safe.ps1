$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V192_PUBLISH_TEMP"
$public = Join-Path $root "api\wwwroot"
$zipPath = Join-Path $artifacts "V192_CATEGORIAS_DEPARTAMENTALES_MONSTA_SEGURA.zip"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Add-Tree($archive, [string]$source, [string]$prefix) {
  Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    $entryName = if ($prefix) { "$prefix/$relative" } else { $relative }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
}

if (Test-Path -LiteralPath $zipPath) {
  Remove-Item -LiteralPath $zipPath -Force
}

$coreFiles = @(
  "RegistroAgencias.SqlServer.V192.dll",
  "RegistroAgencias.SqlServer.V192.deps.json",
  "RegistroAgencias.SqlServer.V192.runtimeconfig.json",
  "RegistroAgencias.SqlServer.V192.exe"
)

$archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($file in $coreFiles) {
    $source = Join-Path $publish $file
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $file, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }

  # Incluye la interfaz en ambas ubicaciones usadas por los alojamientos Monster/Monsta.
  Add-Tree $archive $public ""
  Add-Tree $archive $public "wwwroot"

  # Activa la nueva DLL solamente después de que backend e interfaz estén cargados.
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $publish "web.config"), "web.config", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}
finally {
  $archive.Dispose()
}

Write-Host "Paquete seguro V192 para Monsta creado: $zipPath" -ForegroundColor Green
