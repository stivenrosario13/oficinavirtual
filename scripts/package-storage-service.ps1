param([switch]$SkipFrontend,[ValidatePattern('^[A-Z0-9_]+$')][string]$UpdateName='ACTUALIZACION_ALMACENAMIENTO_LIBERAR_SQL')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_storage_$stamp"
$zip=Join-Path $root "outputs/${UpdateName}_$stamp.zip"

if(!$SkipFrontend){& npm run build;if($LASTEXITCODE -ne 0){throw 'Error compilando interfaz'}}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained -p:AssemblyName=RegistroAgencias.SqlServer.V448.Storage
if($LASTEXITCODE -ne 0){throw 'Error publicando API'}
$webConfig=[IO.File]::ReadAllText((Join-Path $root 'api/web.config')).Replace('RegistroAgencias.SqlServer.V448.exe','RegistroAgencias.SqlServer.V448.Storage.exe')
[IO.File]::WriteAllText((Join-Path $publish 'web.config'),$webConfig)
Copy-Item -LiteralPath (Join-Path $root 'docs/storage-service.md') -Destination (Join-Path $publish 'LEEME_STORAGE.md')
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object { $_.Name -ne 'web.config' })
# Keep the active server configuration, including its private STORAGE_KEY.
# This update uses the same executable name and must never erase that setting.
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName.Contains('\App_Data\') -or $_.FullName.Contains('\uploads\') -or $_.FullName.Contains('\logs\')}){throw 'Archivo privado inesperado en paquete'}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($prefix in @('','wwwroot/')){foreach($file in $files){
    $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
  }}
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
  $names=@($archive.Entries.FullName)
  if(($names | Select-Object -Unique).Count -ne $names.Count){throw 'Entradas duplicadas'}
  if($names | Where-Object {$_ -match '(^|/)web.config$'}){throw 'El paquete no debe sobrescribir la configuración privada del servidor'}
  foreach($prefix in @('','wwwroot/')){foreach($required in @('RegistroAgencias.SqlServer.V448.Storage.dll','RegistroAgencias.SqlServer.V448.Storage.exe','wwwroot/index.html','LEEME_STORAGE.md')){
    if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}
  }}
}finally{$archive.Dispose()}
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item -LiteralPath $zip).Length;SHA256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash}|ConvertTo-Json
