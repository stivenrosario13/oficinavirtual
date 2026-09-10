$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V222_RELEASE_V192_COMPAT\RegistroAgencias.SqlServer.V192.dll"
$target = Join-Path $root "artifacts\V222_EVIDENCIAS_HISTORIAL_COMPATIBLE_V192.zip"
if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL V222 compilada con el nombre V192." }
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[System.IO.Compression.ZipFile]::Open($target,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $frontend -Recurse -File | ForEach-Object {
    $relative=$_.FullName.Substring($frontend.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    foreach($entryName in @($relative,"wwwroot/$relative")){[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
  }
  foreach($entryName in @("RegistroAgencias.SqlServer.V192.dll","wwwroot/RegistroAgencias.SqlServer.V192.dll")){[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$backendDll,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
  $readme=$archive.CreateEntry("LEEME_PRIMERO.txt");$writer=[System.IO.StreamWriter]::new($readme.Open())
  try{$writer.Write(@"
V222 - VISOR DE EVIDENCIAS DEL HISTORIAL - COMPATIBLE CON MONSTER V192

Incluye el botón Ver evidencias dentro de cada registro de trabajo. El visor
muestra las fotografías, quién las subió, fecha, GPS y enlace a la ubicación.

PASOS OBLIGATORIOS
1. En la misma carpeta de web.config crea app_offline.htm.
2. Espera 15 segundos para que Windows libere RegistroAgencias.SqlServer.V192.dll.
3. Extrae TODO este ZIP en esa carpeta y confirma el reemplazo de la DLL V192.
4. Comprueba que la fecha y el tamaño de RegistroAgencias.SqlServer.V192.dll cambiaron.
5. Elimina app_offline.htm y reinicia el sitio desde Monster.
6. Abre /api/version?v=222 evitando caché.
7. Debe mostrar "release":"V222" y "assembly":"RegistroAgencias.SqlServer.V192".

No requiere ejecutar SQL y no reemplaza web.config.
"@)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
Write-Output $target
