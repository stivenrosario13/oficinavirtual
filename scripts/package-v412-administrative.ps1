$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publish=Join-Path $root 'outputs/_publish_v411_20260924174541'
$frontend=Join-Path $root 'api/wwwroot'
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$zip=Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V412_SOLICITUDES_ADMINISTRATIVAS_$stamp.zip"
# Frontend-only fix: retain the verified V411 API and its matching IIS configuration.
# Read both trees directly to avoid making another full staging copy.
$files=@(Get-ChildItem $publish -Recurse -File | Where-Object {!$_.FullName.StartsWith($publish+'\wwwroot\')} | ForEach-Object {
 [pscustomobject]@{Path=$_.FullName;Name=$_.FullName.Substring($publish.Length+1).Replace('\','/')}
})
$files+=@(Get-ChildItem $frontend -Recurse -File | ForEach-Object {
 [pscustomobject]@{Path=$_.FullName;Name='wwwroot/'+$_.FullName.Substring($frontend.Length+1).Replace('\','/')}
})
if($files | Where-Object {$_.Name -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm|App_Data|uploads|logs|node_modules)(/|$)'}){throw 'Archivos privados en paquete'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
 $directories=[Collections.Generic.HashSet[string]]::new()
 foreach($prefix in @('','wwwroot/')){foreach($file in $files){
  $name=$prefix+$file.Name
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.Path,$name,[IO.Compression.CompressionLevel]::Optimal)
  while($name.Contains('/')){$name=$name.Substring(0,$name.LastIndexOf('/'));[void]$directories.Add($name+'/')}
 }}
 foreach($directory in $directories){[void]$archive.CreateEntry($directory)}
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 foreach($file in $files){foreach($prefix in @('','wwwroot/')){
  $name=$prefix+$file.Name
  $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
  if($actual -ne (Get-FileHash $file.Path -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}
 }}
 foreach($prefix in @('','wwwroot/')){
  foreach($name in @('wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js')){
   $reader=[IO.StreamReader]::new($archive.GetEntry($prefix+$name).Open());try{$content=$reader.ReadToEnd()}finally{$reader.Dispose()}
   if($content -notmatch 'V412'){throw "Version incorrecta: $prefix$name"}
  }
  $reader=[IO.StreamReader]::new($archive.GetEntry($prefix+'web.config').Open());try{$config=$reader.ReadToEnd()}finally{$reader.Dispose()}
  if($config -notmatch 'RegistroAgencias.SqlServer.V411.exe' -or !$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V411.exe')){throw 'API e IIS incompatibles'}
 }
 Write-Host "VERIFICADO V412 web / V411 API: $($archive.Entries.Count) entradas, doble ruta con hashes identicos"
}finally{$archive.Dispose()}
Get-Item $zip | Select-Object FullName,Length
