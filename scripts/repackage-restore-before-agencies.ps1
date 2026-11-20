$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Repackage the exact, previous release; do not incorporate later workspace changes.
$source=Join-Path $root 'outputs/_publish_v418_correccion_solicitudes_sql_catalogo_20261009150329'
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$zip=Join-Path $root "outputs/MONSTER_RESTAURAR_APLICACION_09_OCT_$stamp.zip"
$files=@(Get-ChildItem -LiteralPath $source -Recurse -File | Sort-Object FullName)
if(!$files.Count){throw 'Missing verified release'}
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\'}){throw 'Private files in release'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try {
 # Plain relative file entries, one application root, activation file last.
 foreach($file in @($files | Where-Object Name -ne 'web.config')+@($files | Where-Object Name -eq 'web.config')){
  $relative=$file.FullName.Substring($source.Length+1).Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
 }
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 $names=@($archive.Entries.FullName)
 foreach($required in @('Microsoft.SqlServer.Server.dll','RegistroAgencias.SqlServer.V418.exe','RegistroAgencias.SqlServer.V418.dll','RegistroAgencias.SqlServer.V418.deps.json','RegistroAgencias.SqlServer.V418.runtimeconfig.json','web.config','wwwroot/index.html','wwwroot/version-web.json')){
  if($required -notin $names){throw "Missing $required"}
 }
 if($names | Where-Object {$_ -match '^/|\\|^wwwroot/wwwroot/|^wwwroot/.*\.dll$|/$'}){throw 'Invalid or duplicate layout'}
 if(($names | Select-Object -Unique).Count -ne $files.Count){throw 'Entry count mismatch'}
 foreach($file in $files){
  $relative=$file.FullName.Substring($source.Length+1).Replace('\','/')
  $stream=$archive.GetEntry($relative).Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$hash=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
  if($hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash){throw "Incorrect file: $relative"}
 }
 Write-Output "VERIFIED: $($files.Count) files; one root; every SHA-256 matches the previous release."
}finally{$archive.Dispose()}
Get-Item -LiteralPath $zip | Select-Object FullName,Length
