$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V463_COMPLETA_PRIVADA_MONSTERASP_*.zip'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V463'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release-ne'V463'-or$version.apiRelease-ne'V463'){throw 'Versión incorrecta'}
  if($version.administrativeFileExplorer-ne'tree-only-folders-file-type-icons-v463'){throw 'Falta marcador del explorador V463'}
  if($version.warehouseProfessionalWorkspace-ne'sidebar-daily-control-sticky-status-dark-editor-v462'){throw 'Falta mejora anterior de Almacén'}
  $config=Read-Entry ($prefix+'web.config');if($config-notmatch'RegistroAgencias.SqlServer.V463.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $scripts=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/AdministrativeWorkspace-*.js')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$scripts.Contains('adw-tree-toggle')-or!$scripts.Contains('administrativeFileVisual')-or!$scripts.Contains('excel')-or!$scripts.Contains('word')){throw 'Falta árbol moderno o tipos de archivo'}
  $styles=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/administrativeWorkspace-*.css')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$styles.Contains('adw-tree-row')-or!$styles.Contains('adw-file.file-pdf')){throw 'Faltan estilos del explorador'}
  if(!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V463.dll')-or!$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V463.exe')){throw 'Falta API V463'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;AdministrativeFileExplorer='verified';Release='V463'}|ConvertTo-Json
