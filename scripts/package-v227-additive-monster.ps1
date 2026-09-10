$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\V227_RELEASE_NEW_ASSEMBLY"
$dist = Join-Path $root "api\wwwroot"
$backendZip = Join-Path $root "artifacts\V227_01_BACKEND_NUEVO_SIN_BORRAR.zip"
$frontendZip = Join-Path $root "artifacts\V227_02_INTERFAZ_RAPIDA_SIN_BORRAR.zip"
$activationZip = Join-Path $root "artifacts\V227_03_ACTIVAR_SIN_BORRAR.zip"

$requiredBackend = @(
  "RegistroAgencias.SqlServer.V227.exe",
  "RegistroAgencias.SqlServer.V227.dll",
  "RegistroAgencias.SqlServer.V227.deps.json",
  "RegistroAgencias.SqlServer.V227.runtimeconfig.json"
)
foreach ($name in $requiredBackend) {
  if (-not (Test-Path -LiteralPath (Join-Path $publish $name))) { throw "Falta $name en la publicación V227." }
}
if (-not (Test-Path -LiteralPath (Join-Path $dist "index.html"))) { throw "Primero ejecuta npm run build." }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-Archive([string]$path, [scriptblock]$fill) {
  if (Test-Path -LiteralPath $path) { throw "El artefacto ya existe y no se sobrescribirá: $path" }
  $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew)
  try {
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try { & $fill $archive } finally { $archive.Dispose() }
  } finally { $stream.Dispose() }
}

function Add-TextEntry($archive, [string]$name, [string]$content) {
  $entry = $archive.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
  $writer = [System.IO.StreamWriter]::new($entry.Open(), [System.Text.UTF8Encoding]::new($false))
  try { $writer.Write($content) } finally { $writer.Dispose() }
}

New-Archive $backendZip {
  param($archive)
  foreach ($name in $requiredBackend) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $publish $name), $name, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  Add-TextEntry $archive "app_offline.v227.nuevo" "Actualización V227 en curso. El servicio volverá en pocos minutos."
  Add-TextEntry $archive "LEEME_V227_PASO_1.txt" @"
V227 · PASO 1 · BACKEND NUEVO, SIN BORRAR NI REEMPLAZAR BINARIOS ANTERIORES

Extrae este ZIP directamente dentro de /wwwroot.
Los cuatro archivos RegistroAgencias.SqlServer.V227 son nombres nuevos.
No borres, no reemplaces y no renombres ninguna DLL V192, V193 ni de otra versión.
No actives todavía app_offline.v227.nuevo.
"@
}

New-Archive $frontendZip {
  param($archive)
  Get-ChildItem -LiteralPath $dist -File -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($dist.Length).TrimStart("\").Replace("\", "/")
    if ($relative -ne "favicon.svg" -and $relative -ne "loto-real-logo-transparent.png" -and $relative -ne "loto-real-logo.svg") {
      foreach ($entryName in @($relative, "wwwroot/$relative")) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
      }
    }
  }
}

$webConfig = Get-Content -LiteralPath (Join-Path $root "api\web.config") -Raw
New-Archive $activationZip {
  param($archive)
  Add-TextEntry $archive "web.config.v227" $webConfig
  Add-TextEntry $archive "LEEME_V227_PASO_3.txt" @"
V227 · PASO 3 · ACTIVACIÓN SIN ELIMINAR ARCHIVOS

1. Renombra app_offline.v227.nuevo a app_offline.htm.
2. Conserva la interfaz anterior renombrando, si existen:
   index.html a index.html.antes-v227
   release-guard.js a release-guard.js.antes-v227
   version-web.json a version-web.json.antes-v227
   wwwroot/index.html a wwwroot/index.html.antes-v227
   wwwroot/release-guard.js a wwwroot/release-guard.js.antes-v227
   wwwroot/version-web.json a wwwroot/version-web.json.antes-v227
3. Extrae V227_02_INTERFAZ_RAPIDA_SIN_BORRAR.zip dentro de /wwwroot.
   El ZIP incluye la interfaz tanto en la raíz como en la carpeta wwwroot para
   cubrir las dos configuraciones usadas por Monster, sin eliminar la anterior.
4. Conserva la configuración actual renombrando web.config a web.config.antes-v227.
5. Renombra web.config.v227 a web.config.
6. Renombra app_offline.htm a app_offline.v227.usado. No lo elimines.
7. Verifica /api/version. Debe indicar release V227 y assembly RegistroAgencias.SqlServer.V227.

Los binarios y recursos anteriores permanecen guardados. No se elimina ningún archivo.
"@
}

Get-Item -LiteralPath $backendZip, $frontendZip, $activationZip | Select-Object FullName, Length
