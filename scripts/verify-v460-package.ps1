$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V460_COMPLETA_PRIVADA_MONSTERASP_*.zip'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V460'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release-ne'V460'-or$version.apiRelease-ne'V460'){throw 'Versión incorrecta'}
  if($version.vittiniWarehouseVisibility-ne'active-technical-failure-team-requests-v460'){throw 'Falta alcance de Vittini'}
  if($version.warehouseEditing-ne'diorly-ana-direct-button-search-inventory-v459'-or$version.auditPhotoGps-ne'transient-recovery-official-accuracy-same-audit-fallback-v458'){throw 'Faltan cambios anteriores'}
  $config=Read-Entry ($prefix+'web.config');if($config-notmatch'RegistroAgencias.SqlServer.V460.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $html=Read-Entry ($prefix+'wwwroot/index.html');if($html-notmatch'app-release" content="V460'){throw 'HTML anterior'}
  foreach($match in [regex]::Matches($html,'(?:src|href)="(/assets/[^"]+)"')){if(!$archive.GetEntry($prefix+'wwwroot'+$match.Groups[1].Value)){throw 'Falta recurso web'}}
  if(!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V460.dll')-or!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V460.exe')){throw 'Falta API V460'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;VittiniTeamVisibility='verified';Release='V460'}|ConvertTo-Json
