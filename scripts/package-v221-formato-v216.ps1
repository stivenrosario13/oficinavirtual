$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V221_RELEASE_FULL\RegistroAgencias.SqlServer.V193.dll"
$target = Join-Path $root "artifacts\V221_TARJETAS_CENTRADAS_SIN_SCROLL_CON_DLL.zip"
if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL publicada V221." }
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($target,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $frontend -Recurse -File | ForEach-Object {
    $relative=$_.FullName.Substring($frontend.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    foreach($entryName in @($relative,"wwwroot/$relative")){[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
  }
  foreach($entryName in @("RegistroAgencias.SqlServer.V193.dll","wwwroot/RegistroAgencias.SqlServer.V193.dll")){[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$backendDll,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
  $readme=$archive.CreateEntry("LEEME_PRIMERO.txt");$writer=[System.IO.StreamWriter]::new($readme.Open())
  try{$writer.Write(@"
ACTUALIZACION V221 - MISMO FORMATO DE V216 A V220

- Avance por grupos y Auditorías completadas centradas al 50% del ancho.
- Filtros Todos, Completados, En progreso y Sin iniciar en cuadrícula 2 x 2.
- Sin carrusel ni desplazamiento horizontal en esta sección.
- Conserva V220 sin scrolls internos y V219 con carga GPS rápida.

Detén el sitio con app_offline.htm, espera 10 segundos, extrae todo junto a
web.config, confirma reemplazos y reinicia. /api/version debe mostrar V221.
No requiere SQL ni reemplaza web.config.
"@)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
Write-Output $target
