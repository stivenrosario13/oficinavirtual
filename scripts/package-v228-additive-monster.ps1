$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\V228_RELEASE_NEW_ASSEMBLY"
$frontend = Join-Path $root "api\wwwroot"
$backendZip = Join-Path $root "artifacts\V228_01_BACKEND_NUEVO_SIN_BORRAR.zip"
$frontendZip = Join-Path $root "artifacts\V228_02_INTERFAZ_CORREGIDA_SIN_BORRAR.zip"
$activationZip = Join-Path $root "artifacts\V228_03_ACTIVAR_SIN_BORRAR.zip"
$assembly = "RegistroAgencias.SqlServer.V228"
$backendFiles = @("$assembly.exe", "$assembly.dll", "$assembly.deps.json", "$assembly.runtimeconfig.json")

foreach ($name in $backendFiles) {
  if (-not (Test-Path -LiteralPath (Join-Path $publish $name))) { throw "Falta $name en la publicación V228." }
}
foreach ($target in @($backendZip, $frontendZip, $activationZip)) {
  if (Test-Path -LiteralPath $target) { throw "El artefacto ya existe y no se sobrescribirá: $target" }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Archive([string]$path, [scriptblock]$fill) {
  $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew)
  try {
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try { & $fill $archive } finally { $archive.Dispose() }
  } finally { $stream.Dispose() }
}

function Add-Text($archive, [string]$name, [string]$value) {
  $entry = $archive.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
  $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
  try { $writer.Write($value) } finally { $writer.Dispose() }
}

New-Archive $backendZip {
  param($archive)
  foreach ($name in $backendFiles) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $publish $name), $name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  Add-Text $archive "app_offline.v228.nuevo" "Actualización V228 en curso. El servicio volverá en pocos minutos."
  Add-Text $archive "LEEME_V228_PASO_1.txt" @"
V228 · PASO 1 · BACKEND NUEVO SIN BORRAR VERSIONES ANTERIORES

Extrae este ZIP directamente dentro de /wwwroot. Los archivos V228 son nuevos.
No borres ni reemplaces las DLL V192, V193, V227 ni ninguna versión anterior.
Después renombra app_offline.v228.nuevo a app_offline.htm y continúa con el paso 2.
"@
}

New-Archive $frontendZip {
  param($archive)
  Get-ChildItem -LiteralPath $frontend -File -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($frontend.Length).TrimStart("\").Replace("\", "/")
    if ($relative -notin @("favicon.svg", "loto-real-logo-transparent.png", "loto-real-logo.svg")) {
      foreach ($entryName in @($relative, "wwwroot/$relative")) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
      }
    }
  }
}

$webConfig = Get-Content -LiteralPath (Join-Path $root "api\web.config") -Raw
New-Archive $activationZip {
  param($archive)
  Add-Text $archive "web.config.v228" $webConfig
  Add-Text $archive "LEEME_V228_PASO_3.txt" @"
V228 · PASO 3 · ACTIVACIÓN SIN ELIMINAR ARCHIVOS

1. Conserva los index.html, release-guard.js y version-web.json actuales, tanto
   en la raíz como dentro de wwwroot, renombrándolos con el sufijo .antes-v228.
2. Extrae V228_02_INTERFAZ_CORREGIDA_SIN_BORRAR.zip dentro de /wwwroot.
3. Renombra web.config a web.config.antes-v228.
4. Renombra web.config.v228 a web.config.
5. Renombra app_offline.htm a app_offline.v228.usado. No lo elimines.
6. Reinicia el sitio y abre /api/version?v=228.
7. Debe indicar release V228 y assembly RegistroAgencias.SqlServer.V228.
"@
}

Get-Item -LiteralPath $backendZip, $frontendZip, $activationZip | Select-Object FullName, Length
