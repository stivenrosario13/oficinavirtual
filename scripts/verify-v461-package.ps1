$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V461_COMPLETA_PRIVADA_MONSTERASP_*.zip'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V461'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release-ne'V461'-or$version.apiRelease-ne'V461'){throw 'Versión incorrecta'}
  if($version.supervisorWarehouseDestinations-ne'agency-or-multiple-agencies-only-v461'){throw 'Falta restricción de destinos'}
  if($version.vittiniWarehouseVisibility-ne'active-technical-failure-team-requests-v460'){throw 'Falta acceso anterior de Vittini'}
  $config=Read-Entry ($prefix+'web.config');if($config-notmatch'RegistroAgencias.SqlServer.V461.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $scripts=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/WarehouseRequestComposer-*.js')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$scripts.Contains('Para una agencia')-or!$scripts.Contains('Para varias agencias')){throw 'Faltan destinos permitidos'}
  if(!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V461.dll')-or!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V461.exe')){throw 'Falta API V461'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;SupervisorDestinations='verified';Release='V461'}|ConvertTo-Json
