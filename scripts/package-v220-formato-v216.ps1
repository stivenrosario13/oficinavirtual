$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V220_RELEASE_FULL\RegistroAgencias.SqlServer.V193.dll"
$target = Join-Path $root "artifacts\V220_MOVIL_SIN_SCROLLS_INTERNOS_CON_DLL.zip"

if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL publicada V220." }
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
ACTUALIZACION V220 - MISMO FORMATO DE V216 A V219

Correcciones móviles:
- Un solo desplazamiento vertical para el expediente completo.
- Sin scroll interno en fotografías, respuestas ni observaciones.
- Sin barras de desplazamiento visibles dentro de las tarjetas.
- Directorio de usuarios sin scroll horizontal.
- Buscador y acciones PDF, Imprimir y Agregar usuario adaptados al ancho móvil.
- Conserva la carga rápida de fotografías con GPS de V219.

INSTALACION
1. Detén el sitio con app_offline.htm y espera 10 segundos.
2. Extrae TODO el ZIP en la carpeta de web.config y confirma reemplazos.
3. Elimina app_offline.htm y reinicia el sitio.
4. Abre /api/version y confirma exactamente "release":"V220".

No requiere ejecutar SQL ni reemplaza web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
