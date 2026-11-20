$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V458_COMPLETA_PRIVADA_MONSTERASP_*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if(!$zip){throw 'No se encontró el paquete V458'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){
 $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
 $reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}
}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json')) | ConvertFrom-Json
  if($version.release -ne 'V458' -or $version.apiRelease -ne 'V458'){throw 'Versión web incorrecta'}
  if($version.auditPhotoGps -ne 'transient-recovery-official-accuracy-same-audit-fallback-v458'){throw 'Falta la corrección GPS V458'}
  if($version.modemForm -ne 'self-contained-layout-code-linked-agency-v457' -or $version.supervisorWaskar -ne 'active-group-audit-evidence-normal-login-verified-v456'){throw 'Faltan funciones anteriores'}
  $config=Read-Entry ($prefix+'web.config')
  if($config -notmatch 'RegistroAgencias.SqlServer.V458.exe'){throw 'Ejecutable incorrecto'}
  $xml=[xml]$config
  $credential=$xml.SelectSingleNode("//environmentVariable[@name='STORAGE_KEY']")
  if(!$credential -or [string]::IsNullOrWhiteSpace($credential.GetAttribute('value'))){throw 'Falta la configuración privada de almacenamiento'}
  $html=Read-Entry ($prefix+'wwwroot/index.html')
  if($html -notmatch 'app-release" content="V458'){throw 'HTML anterior a V458'}
  foreach($match in [regex]::Matches($html,'(?:src|href)="(/assets/[^"]+)"')){
   if(!$archive.GetEntry($prefix+'wwwroot'+$match.Groups[1].Value)){throw 'Falta un recurso del inicio'}
  }
  $audit=$archive.Entries | Where-Object {$_.FullName -like ($prefix+'wwwroot/assets/EmployeeAuditForm-*.js')} | Select-Object -First 1
  if(!$audit){throw 'Falta el módulo compilado de levantamientos'}
  $auditJs=Read-Entry $audit.FullName
  foreach($marker in @('La ubicación está encendida','lectura GPS reciente del mismo levantamiento','Abre el candado del navegador')){
   if(!$auditJs.Contains($marker)){throw "Falta corrección GPS compilada: $marker"}
  }
  $apiDll=$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V458.dll')
  $apiExe=$archive.GetEntry($prefix+'RegistroAgencias.SqlServer.V458.exe')
  if(!$apiDll -or !$apiExe){throw 'Falta API V458'}
 }
 if($archive.Entries.FullName | Where-Object {$_ -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;GpsAudit='verified';Release='V458'} | ConvertTo-Json
