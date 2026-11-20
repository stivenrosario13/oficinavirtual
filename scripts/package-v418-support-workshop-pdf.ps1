$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_v418_soporte_taller_$stamp"
$zip=Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V418_SOPORTE_ANULACION_TALLER_PDF_$stamp.zip"
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Error publicando API'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
$files=@(Get-ChildItem $publish -Recurse -File)
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\'}){throw 'Archivos privados en paquete'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
 $directories=[Collections.Generic.HashSet[string]]::new()
 foreach($prefix in @('','wwwroot/')){foreach($file in $files){
  $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
  while($name.Contains('/')){$name=$name.Substring(0,$name.LastIndexOf('/'));[void]$directories.Add($name+'/')}
 }}
 foreach($directory in $directories){[void]$archive.CreateEntry($directory)}
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 foreach($file in $files){foreach($prefix in @('','wwwroot/')){
  $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
  $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
  if($actual -ne (Get-FileHash $file.FullName -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}
 }}
 foreach($prefix in @('','wwwroot/')){foreach($name in @('web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js')){
  $reader=[IO.StreamReader]::new($archive.GetEntry($prefix+$name).Open());try{$content=$reader.ReadToEnd()}finally{$reader.Dispose()}
  if($content -notmatch 'V418'){throw "Version incorrecta: $prefix$name"}
 }}
 Write-Host "VERIFICADO V418: $($archive.Entries.Count) entradas, dos copias identicas"
}finally{$archive.Dispose()}
Get-Item $zip | Select-Object FullName,Length
