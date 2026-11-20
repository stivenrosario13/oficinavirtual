$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$name='RegistroAgencias.SqlServer.Solicitudes20261010'
$publish=Join-Path $root "outputs/_publish_solicitudes_activacion_$stamp"
$zip=Join-Path $root "outputs/MONSTER_SOLICITUDES_ACTIVACION_$stamp.zip"
& dotnet publish api/RegistroAgencias.Api.csproj -c Release --no-self-contained -p:AssemblyName=$name -o $publish
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
# Keep the exact frontend that is currently deployed; this fix is server-side.
$baseline=Join-Path $root 'outputs/_publish_v418_correccion_solicitudes_sql_catalogo_20261009150329/wwwroot'
$static=Join-Path $publish 'wwwroot'
if(Test-Path $static){
 $resolved=[IO.Path]::GetFullPath($static)
 if(!$resolved.StartsWith([IO.Path]::GetFullPath($publish)+[IO.Path]::DirectorySeparatorChar)){throw 'Unsafe static path'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
Copy-Item -LiteralPath $baseline -Destination $static -Recurse
$config=[IO.File]::ReadAllText((Join-Path $root 'api/web.config')).Replace('RegistroAgencias.SqlServer.V418.exe',"$name.exe")
[IO.File]::WriteAllText((Join-Path $publish 'web.config'),$config)
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files|Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\'}){throw 'Private files in package'}
$dll=[IO.File]::ReadAllBytes((Join-Path $publish "$name.dll"))
if(![Text.Encoding]::Unicode.GetString($dll).Contains('source-and-403-json-20261010')){throw 'Missing compiled fix marker'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($file in @($files|Where-Object Name -ne 'web.config')+@($files|Where-Object Name -eq 'web.config')){
  $relative=$file.FullName.Substring($publish.Length+1).Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
 }
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 foreach($file in $files){
  $relative=$file.FullName.Substring($publish.Length+1).Replace('\','/')
  $stream=$archive.GetEntry($relative).Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$hash=[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}
  if($hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw "Invalid ZIP file: $relative"}
 }
 if($archive.Entries[-1].FullName -ne 'web.config'){throw 'Activation file must be last'}
 if($archive.Entries.FullName|Where-Object {$_ -match '^wwwroot/wwwroot/|^/|\\|/$'}){throw 'Invalid ZIP layout'}
 foreach($required in @("$name.exe","$name.dll","$name.deps.json","$name.runtimeconfig.json",'Microsoft.SqlServer.Server.dll','wwwroot/index.html')){
  if(!$archive.GetEntry($required)){throw "Missing dependency: $required"}
 }
}finally{$archive.Dispose()}
Write-Output "VERIFIED: $($files.Count) files; unique executable; web.config activates the new binary; all ZIP hashes match."
Get-Item $zip|Select-Object FullName,Length
