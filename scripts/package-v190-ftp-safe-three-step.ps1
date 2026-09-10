$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "V193_PUBLISH_TEMP"
$public = Join-Path $root "api\wwwroot"

$backendZip = Join-Path $artifacts "V193_01_DLL_PRIMERO_SIN_WEB_CONFIG.zip"
$switchZip = Join-Path $artifacts "V193_02_ACTIVAR_WEB_CONFIG.zip"
$frontendZip = Join-Path $artifacts "V193_03_INTERFAZ_DESPUES.zip"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Zip([string]$path) {
  if (Test-Path -LiteralPath $path) {
    Remove-Item -LiteralPath $path -Force
  }
  return [System.IO.Compression.ZipFile]::Open($path, [System.IO.Compression.ZipArchiveMode]::Create)
}

function Add-File($archive, [string]$source, [string]$entryName) {
  if (-not (Test-Path -LiteralPath $source)) {
    throw "No existe el archivo requerido: $source"
  }
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
    $archive,
    $source,
    $entryName,
    [System.IO.Compression.CompressionLevel]::Optimal
  ) | Out-Null
}

function Add-Tree($archive, [string]$source, [string]$prefix) {
  Get-ChildItem -LiteralPath $source -Recurse -File |
    Where-Object { $_.Name -ne "web.config" } |
    ForEach-Object {
      $relative = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      $entryName = if ($prefix) { "$prefix/$relative" } else { $relative }
      Add-File $archive $_.FullName $entryName
    }
}

# Paso 1: coloca la versión nueva sin modificar la aplicación que está activa.
$archive = New-Zip $backendZip
try {
  foreach ($file in @(
    "RegistroAgencias.SqlServer.V193.dll",
    "RegistroAgencias.SqlServer.V193.deps.json",
    "RegistroAgencias.SqlServer.V193.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V193.exe"
  )) {
    Add-File $archive (Join-Path $publish $file) $file
  }
}
finally {
  $archive.Dispose()
}

# Paso 2: activa V193 solamente cuando los cuatro archivos anteriores ya existen.
$archive = New-Zip $switchZip
try {
  Add-File $archive (Join-Path $publish "web.config") "web.config"
}
finally {
  $archive.Dispose()
}

# Paso 3: actualiza la interfaz sin volver a tocar la DLL ni web.config.
$archive = New-Zip $frontendZip
try {
  Add-Tree $archive $public ""
  Add-Tree $archive $public "wwwroot"
}
finally {
  $archive.Dispose()
}

Write-Host "Paquetes V193 separados para FTP creados correctamente." -ForegroundColor Green
