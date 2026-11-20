$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_v425_workshop_receipt_$stamp"
$zip=Join-Path $root "outputs/ACTUALIZACION_V425_RECEPCION_TALLER_$stamp.zip"
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Error publicando API'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'outputs/INSTRUCCIONES_V425_RECEPCION_TALLER.txt') -Destination (Join-Path $publish 'INSTRUCCIONES_V425_RECEPCION_TALLER.txt') -Force
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\'}){throw 'Archivos privados en paquete'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($file in $files){
  $name=$file.FullName.Substring($publish.Length+1).Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
 }
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 foreach($file in $files){
  $name=$file.FullName.Substring($publish.Length+1).Replace('\','/')
  $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
  if($actual -ne (Get-FileHash $file.FullName -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}
 }
 if($archive.GetEntry('wwwroot/BouncyCastle.Crypto.dll')){throw 'El paquete contiene una DLL duplicada bajo wwwroot'}
 foreach($name in @('web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js')){
  $reader=[IO.StreamReader]::new($archive.GetEntry($name).Open());try{$content=$reader.ReadToEnd()}finally{$reader.Dispose()}
  if($content -notmatch 'V425'){throw "Version incorrecta: $name"}
 }
 Write-Host "VERIFICADO V425: $($archive.Entries.Count) archivos, una sola ruta, hashes identicos"
}finally{$archive.Dispose()}
Get-Item $zip | Select-Object FullName,Length
