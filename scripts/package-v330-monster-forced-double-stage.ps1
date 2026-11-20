$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v330_dual_$stamp"
$stage = Join-Path $root "outputs/_stage_v330_dual_$stamp"
$activation = Join-Path $root "outputs/_activate_v330_dual_$stamp"
$out = Join-Path $root "outputs/V330_MONSTER_DOBLE_RUTA_$stamp"
$filesZip = Join-Path $out "01_V330_ARCHIVOS_DOBLE_RUTA_$stamp.zip"
$activateZip = Join-Path $out "02_V330_ACTIVAR_AMBOS_WEB_CONFIG_$stamp.zip"

New-Item -ItemType Directory -Path $publish,$stage,$activation,$out -Force | Out-Null
Set-Location $root
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }

$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {
  $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or
  $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'
}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }

# La instalación V329 de Monster conserva dos posibles raíces ejecutables.
# Se cargan primero todos los archivos en ambas; ningún web.config viaja aquí.
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' |
  Copy-Item -Destination $stage -Recurse -Force
$inner = Join-Path $stage 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' |
  Copy-Item -Destination $inner -Recurse -Force

# Los dos archivos de activación se aíslan en un segundo ZIP mínimo.
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $activation 'web.config') -Force
New-Item -ItemType Directory -Path (Join-Path $activation 'wwwroot') -Force | Out-Null
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $activation 'wwwroot/web.config') -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function New-FlatZip([string]$source,[string]$destination) {
  $archive=[IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Create)
  try {
    foreach($file in Get-ChildItem $source -File -Recurse | Sort-Object FullName) {
      $name=$file.FullName.Substring($source.Length).TrimStart([char[]]'\/').Replace('\','/')
      [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
    }
  } finally {$archive.Dispose()}
}
New-FlatZip $stage $filesZip
New-FlatZip $activation $activateZip

$files=[IO.Compression.ZipFile]::OpenRead($filesZip)
$activate=[IO.Compression.ZipFile]::OpenRead($activateZip)
try {
  $fileNames=@($files.Entries.FullName)
  $activationNames=@($activate.Entries.FullName)
  foreach($required in @(
    'RegistroAgencias.SqlServer.V330.exe','RegistroAgencias.SqlServer.V330.dll',
    'wwwroot/index.html','wwwroot/version-web.json',
    'wwwroot/RegistroAgencias.SqlServer.V330.exe','wwwroot/RegistroAgencias.SqlServer.V330.dll',
    'wwwroot/wwwroot/index.html','wwwroot/wwwroot/version-web.json'
  )) {if($required -notin $fileNames){throw "Falta $required"}}
  if($fileNames -contains 'web.config' -or $fileNames -contains 'wwwroot/web.config') {throw 'El ZIP de archivos no debe activar el sitio.'}
  foreach($required in @('web.config','wwwroot/web.config')) {if($required -notin $activationNames){throw "Falta $required"}}
  if($activationNames.Count -ne 2){throw 'El ZIP de activación debe contener únicamente dos web.config.'}
  if($fileNames -contains 'app_offline.htm' -or $fileNames -contains 'wwwroot/app_offline.htm'){throw 'No se permite app_offline.htm.'}
} finally {$files.Dispose();$activate.Dispose()}

$readme=@'
V330 · CORRECCIÓN PARA INSTALACIÓN MONSTER DE DOBLE RUTA

La respuesta /api/version en V329 demuestra que el web.config activo no fue
reemplazado. Este paquete cubre las dos raíces que conserva esa instalación.

1. Sitúate en la carpeta superior que contiene la carpeta wwwroot.
2. Extrae 01_V330_ARCHIVOS_DOBLE_RUTA_*.zip y espera a que termine por completo.
3. Confirma que existen RegistroAgencias.SqlServer.V330.exe tanto en esta carpeta
   como dentro de wwwroot.
4. Extrae 02_V330_ACTIVAR_AMBOS_WEB_CONFIG_*.zip en la misma carpeta y acepta
   reemplazar ambos web.config.
5. Consulta /api/version?release=V330. Debe responder release V330 y assembly
   RegistroAgencias.SqlServer.V330.

No contiene app_offline.htm y no requiere detener manualmente el sitio. Cambiar
web.config produce el reciclado automático normal de ASP.NET Core.
'@
Set-Content (Join-Path $out 'LEEME_V330_DOBLE_RUTA.txt') $readme -Encoding UTF8
$sha=[System.Security.Cryptography.SHA256]::Create()
try {
  $hashes=@($filesZip,$activateZip)|ForEach-Object {
    $stream=[IO.File]::OpenRead($_)
    try {$hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')}
    finally {$stream.Dispose()}
    "$hash  $([IO.Path]::GetFileName($_))"
  }
} finally {$sha.Dispose()}
Set-Content (Join-Path $out 'SHA256.txt') $hashes -Encoding ASCII
Write-Host 'Paquete V330 de doble ruta y activación final verificado.' -ForegroundColor Green
Get-Item $filesZip,$activateZip | Select-Object FullName,Length
