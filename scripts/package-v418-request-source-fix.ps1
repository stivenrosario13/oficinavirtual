$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$build=Join-Path $root 'api/bin/Release/net8.0'
$baseline=Join-Path $root 'outputs/_publish_v418_correccion_solicitudes_sql_catalogo_20261009150329'
$assembly='RegistroAgencias.SqlServer.V418'
$current=Get-Content (Join-Path $build "$assembly.deps.json") -Raw | ConvertFrom-Json
$previous=Get-Content (Join-Path $baseline "$assembly.deps.json") -Raw | ConvertFrom-Json
if(Compare-Object @($current.libraries.PSObject.Properties.Name) @($previous.libraries.PSObject.Properties.Name)){throw 'Dependencies changed; a DLL-only patch is not sufficient'}
$zip=Join-Path $root ('outputs/MONSTER_CORRECCION_SOLICITUDES_'+(Get-Date -Format 'yyyyMMddHHmmss')+'.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($extension in @('dll','pdb')){
  $name="$assembly.$extension"
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $build $name),$name,[IO.Compression.CompressionLevel]::Optimal)
 }
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 if($archive.Entries.Count -ne 2){throw 'Unexpected package contents'}
 foreach($entry in $archive.Entries){
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$hash=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
  if($hash -ne (Get-FileHash (Join-Path $build $entry.FullName)).Hash){throw "Hash mismatch: $($entry.FullName)"}
 }
}finally{$archive.Dispose()}
Write-Output 'VERIFIED: two compiled files; unchanged dependencies; SHA-256 verified.'
Get-Item $zip | Select-Object FullName,Length
