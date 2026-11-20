$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V462_COMPLETA_PRIVADA_MONSTERASP_*.zip'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V462'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release-ne'V462'-or$version.apiRelease-ne'V462'){throw 'Versión incorrecta'}
  if($version.warehouseProfessionalWorkspace-ne'sidebar-daily-control-sticky-status-dark-editor-v462'){throw 'Falta marcador del espacio profesional de Almacén'}
  if($version.supervisorWarehouseDestinations-ne'agency-or-multiple-agencies-only-v461'){throw 'Falta la restricción anterior de supervisores'}
  $config=Read-Entry ($prefix+'web.config');if($config-notmatch'RegistroAgencias.SqlServer.V462.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $main=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/index-*.js')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$main.Contains('Control diario de Almacén')){throw 'Falta navegación del control diario'}
  $styles=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/maintenanceCommunications-*.css')-or$_.FullName-like($prefix+'wwwroot/assets/InstitutionalDocumentDetail-*.css')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$styles.Contains('position:sticky')-or!$styles.Contains('warehouse-request-editor')){throw 'Faltan estilos profesionales de Almacén'}
  if(!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V462.dll')-or!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V462.exe')){throw 'Falta API V462'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;WarehouseProfessionalWorkspace='verified';Release='V462'}|ConvertTo-Json
