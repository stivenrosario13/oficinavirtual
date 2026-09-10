$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V219_RELEASE_FULL\RegistroAgencias.SqlServer.V193.dll"
$target = Join-Path $root "artifacts\V219_CAMARA_GPS_RAPIDA_CON_DLL.zip"

if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL publicada V219." }
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($target, [System.IO.Compression.ZipArchiveMode]::Create)

try {
  Get-ChildItem -LiteralPath $frontend -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($frontend.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    foreach ($entryName in @($relative, "wwwroot/$relative")) {
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
  }
  foreach ($entryName in @("RegistroAgencias.SqlServer.V193.dll", "wwwroot/RegistroAgencias.SqlServer.V193.dll")) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$backendDll,$entryName,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  $readme = $archive.CreateEntry("LEEME_PRIMERO.txt")
  $writer = [System.IO.StreamWriter]::new($readme.Open())
  try {
    $writer.Write(@"
ACTUALIZACION V219 - MISMO FORMATO DE V216, V217 Y V218

Mejoras de velocidad:
- GPS y optimización de la fotografía trabajan al mismo tiempo.
- Reutiliza una ubicación reciente del teléfono durante 60 segundos.
- Reduce automáticamente fotos grandes a 1600 px y JPEG optimizado.
- Tiempo máximo de espera del GPS reducido de 25 a 12 segundos.
- Conserva coordenadas, precisión, fecha y evidencia visual legible.

INSTALACION
1. Detén el sitio con app_offline.htm y espera 10 segundos.
2. Extrae TODO el ZIP en la carpeta de web.config y confirma reemplazos.
3. Elimina app_offline.htm y reinicia el sitio.
4. Abre /api/version y confirma exactamente "release":"V219".

No requiere ejecutar SQL ni reemplaza web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
