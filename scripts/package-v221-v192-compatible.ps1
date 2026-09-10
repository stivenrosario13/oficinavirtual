$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V221_RELEASE_V192_COMPAT\RegistroAgencias.SqlServer.V192.dll"
$target = Join-Path $root "artifacts\V221_COMPATIBLE_CON_MONSTER_V192.zip"
if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL V192 compatible." }
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
V221 COMPATIBLE CON EL PROCESO ACTIVO V192 DE MONSTER

Este ZIP usa exactamente RegistroAgencias.SqlServer.V192.dll porque /api/version
confirmó que Monster ejecuta el ensamblado V192. No contiene la DLL V193 y no
requiere cambiar web.config.

PASOS OBLIGATORIOS
1. En la misma carpeta de web.config crea app_offline.htm.
2. Espera 15 segundos para que Windows libere RegistroAgencias.SqlServer.V192.dll.
3. Extrae TODO este ZIP en esa carpeta y confirma el reemplazo de la DLL V192.
4. Verifica que la fecha y tamaño de RegistroAgencias.SqlServer.V192.dll cambiaron.
5. Elimina app_offline.htm y reinicia el sitio desde Monster.
6. Abre /api/version con ?v=221 para evitar caché.
7. Debe mostrar "release":"V221" aunque "assembly" continúe diciendo V192.

Incluye V221 sin scroll horizontal, V220 sin scrolls internos y V219 con cámara
GPS rápida. No requiere SQL ni reemplaza web.config.
"@)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
Write-Output $target
