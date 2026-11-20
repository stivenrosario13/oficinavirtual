$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V464_COMPLETA_PRIVADA_MONSTERASP_*.zip'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V464'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release-ne'V464'-or$version.apiRelease-ne'V464'){throw 'Versión incorrecta'}
  if($version.administrativeFileActions-ne'compact-explorer-context-menu-only-v464'){throw 'Falta marcador V464'}
  if($version.administrativeFileExplorer-ne'tree-only-folders-file-type-icons-v463'){throw 'Falta explorador V463'}
  $config=Read-Entry ($prefix+'web.config');if($config-notmatch'RegistroAgencias.SqlServer.V464.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $scripts=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/AdministrativeWorkspace-*.js')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$scripts.Contains('Clic derecho para ver las acciones')-or!$scripts.Contains('Administrar acceso')-or!$scripts.Contains('Eliminar definitivamente')){throw 'Faltan acciones contextuales'}
  $styles=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/administrativeWorkspace-*.css')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$styles.Contains('minmax(118px,150px)')-or!$styles.Contains('adw-folder-actions')){throw 'Faltan estilos compactos'}
  if(!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V464.dll')-or!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V464.exe')){throw 'Falta API V464'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;CompactExplorer='verified';Release='V464'}|ConvertTo-Json
