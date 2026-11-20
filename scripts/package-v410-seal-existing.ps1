$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$backend=Join-Path $root 'outputs/_publish_v410_20260924155110'
$web=Join-Path $root 'api/wwwroot'
$zip=Join-Path $root ('outputs/ACTUALIZACION_COMPLETA_V410_SELLO_DOS_LINEAS_'+(Get-Date -Format 'yyyyMMddHHmmss')+'.zip')
$items=@(Get-ChildItem $backend -File -Recurse | Where-Object {!$_.FullName.StartsWith($backend+'\wwwroot\')} | ForEach-Object {@{Source=$_.FullName;Name=$_.FullName.Substring($backend.Length+1).Replace('\','/')}})
$items+=@(Get-ChildItem $web -File -Recurse | ForEach-Object {@{Source=$_.FullName;Name='wwwroot/'+$_.FullName.Substring($web.Length+1).Replace('\','/')}})
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 $dirs=[Collections.Generic.HashSet[string]]::new()
 foreach($prefix in @('','wwwroot/')){foreach($item in $items){
  $name=$prefix+$item.Name
  if($name -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'){throw "Archivo privado: $name"}
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$item.Source,$name,[IO.Compression.CompressionLevel]::Optimal)
  while($name.Contains('/')){$name=$name.Substring(0,$name.LastIndexOf('/'));[void]$dirs.Add($name+'/')}
 }}
 foreach($dir in $dirs){[void]$archive.CreateEntry($dir)}
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 foreach($item in $items){$expected=(Get-FileHash $item.Source -Algorithm SHA256).Hash;foreach($prefix in @('','wwwroot/')){
  $entry=$archive.GetEntry($prefix+$item.Name);if(!$entry){throw 'Archivo faltante'}
  $sha=[Security.Cryptography.SHA256]::Create();$stream=$entry.Open()
  try{$actual=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$sha.Dispose();$stream.Dispose()}
  if($actual -ne $expected){throw 'Hash incorrecto'}
 }}
 foreach($prefix in @('','wwwroot/')){
  $reader=[IO.StreamReader]::new($archive.GetEntry($prefix+'web.config').Open());try{$config=$reader.ReadToEnd()}finally{$reader.Dispose()}
  if($config -notmatch 'RegistroAgencias.SqlServer.V410.exe'){throw 'Configuracion incorrecta'}
 }
 [pscustomobject]@{Zip=$zip;Bytes=(Get-Item $zip).Length;Entries=$archive.Entries.Count;VerifiedCopies=2}|ConvertTo-Json
}finally{$archive.Dispose()}
