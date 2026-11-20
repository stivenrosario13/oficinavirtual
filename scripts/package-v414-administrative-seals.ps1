$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Join-Path $root 'outputs/ACTUALIZACION_COMPLETA_V413_FORMULARIO_ADMINISTRATIVO_FIJO_20260924184530.zip'
$frontend=Join-Path $root 'api/wwwroot'
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$zip=Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V414_SELLOS_SIN_SUPERPONER_$stamp.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$previous=[IO.Compression.ZipFile]::OpenRead($source)
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
 foreach($entry in $previous.Entries){
  # Retain the complete verified API, twice; replace only the two frontend trees.
  if($entry.FullName.EndsWith('/')){continue}
  if($entry.FullName.StartsWith('wwwroot/') -and ($entry.FullName.StartsWith('wwwroot/wwwroot/') -or !$previous.GetEntry($entry.FullName.Substring(8)))){continue}
  $target=$archive.CreateEntry($entry.FullName,[IO.Compression.CompressionLevel]::Optimal)
  $inputStream=$entry.Open();$outputStream=$target.Open()
  try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
 }
 foreach($file in Get-ChildItem $frontend -Recurse -File){
  $relative=$file.FullName.Substring($frontend.Length+1).Replace('\','/')
  foreach($prefix in @('wwwroot/','wwwroot/wwwroot/')){
   [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$prefix+$relative,[IO.Compression.CompressionLevel]::Optimal)
  }
 }
}finally{$archive.Dispose();$previous.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 function EntryHash($entry){
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{return [Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
 }
 foreach($entry in $archive.Entries){
  if($entry.FullName -match '^wwwroot/' -and $entry.FullName -notmatch '^wwwroot/(assets|vendor|fonts|images)/'){continue}
  $copy=$archive.GetEntry('wwwroot/'+$entry.FullName)
  if(!$copy -or (EntryHash $entry) -ne (EntryHash $copy)){throw "Copia incorrecta: $($entry.FullName)"}
 }
 foreach($file in Get-ChildItem $frontend -Recurse -File){
  $relative=$file.FullName.Substring($frontend.Length+1).Replace('\','/')
  foreach($prefix in @('wwwroot/','wwwroot/wwwroot/')){
   if((EntryHash $archive.GetEntry($prefix+$relative)) -ne (Get-FileHash $file.FullName).Hash){throw "Frontend incorrecto: $relative"}
  }
 }
 foreach($prefix in @('wwwroot/','wwwroot/wwwroot/')){
  $reader=[IO.StreamReader]::new($archive.GetEntry($prefix+'version-web.json').Open())
  try{$metadata=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
  if($metadata.release -ne 'V414'){throw 'Versión incorrecta'}
 }
 Write-Host "VERIFICADO: $($archive.Entries.Count) archivos, doble ruta, frontend V414 y API V411 conservada"
}finally{$archive.Dispose()}
Write-Host $zip
