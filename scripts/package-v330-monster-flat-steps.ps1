$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_v330_flat_$stamp"
$out=Join-Path $root "outputs/V330_MONSTER_PASOS_PLANOS_$stamp"
New-Item -ItemType Directory -Path $publish,$out -Force|Out-Null
Set-Location $root
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE-ne 0){throw 'Falló dotnet publish.'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function New-ZipFromFiles([string]$destination,[System.IO.FileInfo[]]$files){
 $archive=[IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Create)
 try{foreach($file in $files){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$file.Name,[IO.Compression.CompressionLevel]::Optimal)}}finally{$archive.Dispose()}
}

$backendNames=@(
 'RegistroAgencias.SqlServer.V330.exe','RegistroAgencias.SqlServer.V330.dll',
 'RegistroAgencias.SqlServer.V330.deps.json','RegistroAgencias.SqlServer.V330.runtimeconfig.json',
 'RegistroAgencias.SqlServer.V330.staticwebassets.endpoints.json'
)
$backendFiles=@($backendNames|ForEach-Object{Get-Item (Join-Path $publish $_)})
$webFiles=@(Get-ChildItem (Join-Path $publish 'wwwroot') -File)
$assetFiles=@(Get-ChildItem (Join-Path $publish 'wwwroot/assets') -File)
$activationFiles=@(Get-Item (Join-Path $publish 'web.config'))

$backendZip=Join-Path $out "01_RAIZ_BACKEND_V330_$stamp.zip"
$assetsZip=Join-Path $out "02_DENTRO_ASSETS_V330_$stamp.zip"
$webZip=Join-Path $out "03_DENTRO_WWWROOT_V330_$stamp.zip"
$activationZip=Join-Path $out "04_ACTIVAR_WEB_CONFIG_V330_$stamp.zip"
New-ZipFromFiles $backendZip $backendFiles
New-ZipFromFiles $assetsZip $assetFiles
New-ZipFromFiles $webZip $webFiles
New-ZipFromFiles $activationZip $activationFiles

foreach($zipPath in @($backendZip,$assetsZip,$webZip,$activationZip)){
 $archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
 try{
  if(!$archive.Entries.Count){throw "ZIP vacío: $zipPath"}
  if($archive.Entries.FullName|Where-Object{$_-match '[/\\]'}){throw "El ZIP no es plano: $zipPath"}
  if($archive.Entries.FullName|Where-Object{$_-match 'BouncyCastle|app_offline|V329'}){throw "El ZIP contiene archivos prohibidos: $zipPath"}
 }finally{$archive.Dispose()}
}

$readme=@'
V330 · MONSTER · CARGA PLANA SIN BOUNCYCASTLE

Todos los ZIP son planos. Monster no tendrá que crear rutas internas ni subir
BouncyCastle.Crypto.dll, que ya existe en producción y provocaba el error FTP.

En la carpeta /wwwroot mostrada en el panel:
1. Extrae 01_RAIZ_BACKEND_V330_*.zip.
2. Abre la carpeta assets y extrae allí 02_DENTRO_ASSETS_V330_*.zip.
3. Regresa a /wwwroot y extrae 03_DENTRO_WWWROOT_V330_*.zip.
4. En /wwwroot, extrae al final 04_ACTIVAR_WEB_CONFIG_V330_*.zip y reemplaza web.config.
5. Consulta /api/version?release=V330. Debe responder V330.

No borres BouncyCastle.Crypto.dll, App_Data, uploads ni archivos V329. No se usa
app_offline.htm y no hay que detener manualmente la aplicación.
'@
Set-Content (Join-Path $out 'LEEME_V330_PASOS_PLANOS.txt') $readme -Encoding UTF8
Write-Host 'Paquetes planos V330 verificados sin BouncyCastle ni rutas internas.' -ForegroundColor Green
Get-ChildItem $out|Select-Object Name,Length
