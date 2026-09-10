$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V226_RELEASE_V192_COMPAT\RegistroAgencias.SqlServer.V192.dll"
$backendZip = Join-Path $root "artifacts\V226_01_DLL_V192_SUBIR_COMO_ARCHIVO_NUEVO.zip"
$frontendZip = Join-Path $root "artifacts\V226_02_INTERFAZ_DENTRO_WWWROOT_SIN_DLL.zip"
if (-not (Test-Path -LiteralPath $backendDll)) { throw "No se encontró la DLL V226 compatible con V192." }
foreach($target in @($backendZip,$frontendZip)){if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target -Force}}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive=[System.IO.Compression.ZipFile]::Open($backendZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$backendDll,"RegistroAgencias.SqlServer.V192.dll.upload",[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null
  $readme=$archive.CreateEntry("PASO_1_LEEME.txt");$writer=[System.IO.StreamWriter]::new($readme.Open())
  try{$writer.Write(@"
V226 · PASO 1 · DLL V192 SIN REEMPLAZO FTP

1. En /wwwroot crea app_offline.htm y espera 15 segundos.
2. Extrae este ZIP directamente dentro de /wwwroot.
3. Se creará RegistroAgencias.SqlServer.V192.dll.upload como archivo NUEVO.
4. Si existe RegistroAgencias.SqlServer.V192.dll, renómbralo temporalmente a
   RegistroAgencias.SqlServer.V192.dll.bak.
5. Renombra RegistroAgencias.SqlServer.V192.dll.upload a
   RegistroAgencias.SqlServer.V192.dll.
6. Todavía NO elimines app_offline.htm. Continúa con el ZIP del PASO 2.
"@)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}

$archive=[System.IO.Compression.ZipFile]::Open($frontendZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  Get-ChildItem -LiteralPath $frontend -Recurse -File | ForEach-Object {
    $relative=$_.FullName.Substring($frontend.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null
  }
  $readme=$archive.CreateEntry("PASO_2_LEEME.txt");$writer=[System.IO.StreamWriter]::new($readme.Open())
  try{$writer.Write(@"
V226 · PASO 2 · INTERFAZ SIN DLL

1. Extrae este ZIP directamente dentro de /wwwroot y reemplaza los archivos.
2. Confirma que RegistroAgencias.SqlServer.V192.dll ya tiene el nombre final.
3. Elimina app_offline.htm y reinicia el sitio desde Monster.
4. Abre /api/version?v=226 evitando caché.
5. Debe mostrar "release":"V226" y "assembly":"RegistroAgencias.SqlServer.V192".

Este ZIP no contiene DLL, web.config ni una carpeta wwwroot duplicada.
"@)}finally{$writer.Dispose()}
}finally{$archive.Dispose()}

Write-Output $backendZip
Write-Output $frontendZip
