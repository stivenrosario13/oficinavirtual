$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V465_COMPLETA_PRIVADA_MONSTERASP_*.zip'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V465'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release-ne'V465'-or$version.apiRelease-ne'V465'){throw 'Versión incorrecta'}
  if($version.administrativeFileActions-ne'drag-shared-folders-global-search-dark-tree-v465'){throw 'Falta explorador V465'}
  if($version.voiceOperations-ne'spanish-ticket-warehouse-guided-creation-v465'){throw 'Falta asistente por voz V465'}
  $config=Read-Entry ($prefix+'web.config');if($config-notmatch'RegistroAgencias.SqlServer.V465.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $scripts=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/*.js')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  foreach($marker in @('IA por voz','Crear solicitud hablando','Buscar en todas las carpetas y archivos','Carpetas compartidas')){if(!$scripts.Contains($marker)){throw "Falta marcador: $marker"}}
  $styles=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/*.css')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$styles.Contains('voice-operation-assistant')-or!$styles.Contains('drag-target')){throw 'Faltan estilos V465'}
  if(!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V465.dll')-or!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V465.exe')){throw 'Falta API V465'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;Explorer='verified';VoiceAssistant='verified';Release='V465'}|ConvertTo-Json
