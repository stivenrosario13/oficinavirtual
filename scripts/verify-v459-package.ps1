$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V459_COMPLETA_PRIVADA_MONSTERASP_*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V459'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json'))|ConvertFrom-Json
  if($version.release -ne 'V459' -or $version.apiRelease -ne 'V459'){throw 'Versión incorrecta'}
  if($version.warehouseEditing -ne 'diorly-ana-direct-button-search-inventory-v459'){throw 'Falta edición de Almacén V459'}
  if($version.warehouseWeekendTheme -ne 'institutional-dark-white-text-v459'){throw 'Falta tema oscuro V459'}
  if($version.auditPhotoGps -ne 'transient-recovery-official-accuracy-same-audit-fallback-v458'){throw 'Falta corrección GPS anterior'}
  $config=Read-Entry ($prefix+'web.config');if($config -notmatch 'RegistroAgencias.SqlServer.V459.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config;$credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']");if(!$credential-or[string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta almacenamiento privado'}
  $scripts=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/*.js')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  foreach($marker in @('Editar formulario','Buscar artículo','Nombre, código, grupo o almacén')){if(!$scripts.Contains($marker)){throw "Falta función compilada: $marker"}}
  $styles=($archive.Entries|Where-Object {$_.FullName-like($prefix+'wwwroot/assets/*.css')}|ForEach-Object {Read-Entry $_.FullName})-join"`n"
  if(!$styles.Contains('warehouse-pdf-management>.weekend-loan-section')){throw 'Falta estilo institucional del préstamo'}
 }
 if($archive.Entries.FullName|Where-Object {$_-match'(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;WarehouseEditing='verified';Theme='verified';Release='V459'}|ConvertTo-Json
