$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V223_RELEASE_V192_COMPAT\RegistroAgencias.SqlServer.V192.dll"
$target = Join-Path $root "artifacts\V223_REGISTROS_EXPANDIBLES_COMPATIBLE_V192.zip"
if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL V223 compilada con el nombre V192." }
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
V223 - REGISTROS EXPANDIBLES - COMPATIBLE CON MONSTER V192

Cada registro del Historial de avances aparece como una fila compacta. Toca
Ver detalles para abrir responsable, cámara, evidencias y acciones. Incluye
Expandir todos y Ocultar todos cuando una etapa tiene varios registros.

PASOS OBLIGATORIOS
1. En la misma carpeta de web.config crea app_offline.htm.
2. Espera 15 segundos para liberar RegistroAgencias.SqlServer.V192.dll.
3. Extrae TODO este ZIP en esa carpeta y confirma el reemplazo de la DLL V192.
4. Elimina app_offline.htm y reinicia el sitio desde Monster.
5. Abre /api/version?v=223 evitando caché.
6. Debe mostrar "release":"V223" y "assembly":"RegistroAgencias.SqlServer.V192".

No requiere ejecutar SQL y no reemplaza web.config.
"@)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
Write-Output $target
