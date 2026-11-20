$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_v447_double_route_$stamp"
$zip=Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V447_DOBLE_RUTA_$stamp.zip"

& npm run build
if($LASTEXITCODE -ne 0){throw 'Error compilando interfaz'}

& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Error publicando API'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'outputs/INSTRUCCIONES_V447_DOBLE_RUTA.txt') -Destination (Join-Path $publish 'INSTRUCCIONES_V447_DOBLE_RUTA.txt') -Force
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName.Contains('\App_Data\') -or $_.FullName.Contains('\uploads\') -or $_.FullName.Contains('\logs\') -or $_.FullName.Contains('\node_modules\')}){throw 'Archivos privados en paquete'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 $directories=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($prefix in @('','wwwroot/')){foreach($file in $files){
  $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
  $parent=$name
  while($parent.Contains('/')){$parent=$parent.Substring(0,$parent.LastIndexOf('/'));[void]$directories.Add($parent+'/')}
 }}
 foreach($directory in ($directories | Sort-Object)){[void]$archive.CreateEntry($directory)}
}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 $names=@($archive.Entries.FullName)
 if(($names | Select-Object -Unique).Count -ne $names.Count){throw 'Entradas duplicadas'}
 foreach($file in $files){foreach($prefix in @('','wwwroot/')){
  $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
  $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}
  finally{$stream.Dispose();$sha.Dispose()}
  if($actual -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}
 }}
 foreach($prefix in @('','wwwroot/')){foreach($name in @('web.config','BouncyCastle.Crypto.dll','RegistroAgencias.SqlServer.V447.dll','RegistroAgencias.SqlServer.V447.exe','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js','INSTRUCCIONES_V447_DOBLE_RUTA.txt')){
  if(!$archive.GetEntry($prefix+$name)){throw "Falta $prefix$name"}
 }}
 Write-Host "VERIFICADO V447 DOBLE RUTA: $($archive.Entries.Count) entradas; dos copias identicas"
}finally{$archive.Dispose()}
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item -LiteralPath $zip).Length;SHA256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash}|ConvertTo-Json
