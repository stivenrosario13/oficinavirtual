$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$zip=Get-ChildItem -LiteralPath (Join-Path $root 'outputs') -Filter 'ACTUALIZACION_V450_OPTIMIZACION_MONSTERASP_*.zip' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip.FullName)
function Read-Entry([string]$name){
 $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
 $reader=[IO.StreamReader]::new($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}
}
try{
 foreach($prefix in @('','wwwroot/')){
  $version=(Read-Entry ($prefix+'wwwroot/version-web.json')) | ConvertFrom-Json
  if($version.release -ne 'V450' -or $version.agencyCoordinates -ne 'any-valid-decimal-precision-20261027'){throw 'Paquete anterior a la correccion de coordenadas'}
  if($version.accountPassword -ne 'rotate-sessions-disable-bootstrap-fallback' -or $version.supportRegions -ne 'four-regions-and-unassigned-access' -or $version.warehouseQueue -ne 'summary-inside-warehouse-only'){throw 'Faltan cambios V450'}
  $config=Read-Entry ($prefix+'web.config')
  if($config -notmatch 'RegistroAgencias.SqlServer.V450.exe'){throw 'Ejecutable incorrecto'}
  $html=Read-Entry ($prefix+'wwwroot/index.html')
  foreach($match in [regex]::Matches($html,'(?:src|href)="(/assets/[^"]+)"')){
   if(!$archive.GetEntry($prefix+'wwwroot'+$match.Groups[1].Value)){throw 'Falta un recurso del inicio'}
  }
  $ticket=$archive.Entries | Where-Object {$_.FullName -like ($prefix+'wwwroot/assets/TicketCenter-*.js')} | Select-Object -First 1
  if(!$ticket -or (Read-Entry $ticket.FullName) -notmatch 'step:"any"'){throw 'Falta correccion compilada de coordenadas'}
  if((Read-Entry $ticket.FullName) -notmatch 'Regiones del departamento'){throw 'Faltan tarjetas de regiones'}
 }
 if($archive.Entries.FullName | Where-Object {$_ -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'}){throw 'Contenido no permitido'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash
Set-Content -LiteralPath ($zip.FullName+'.sha256.txt') -Value "$hash  $($zip.Name)" -Encoding ascii
[pscustomobject]@{Zip=$zip.FullName;MB=[Math]::Round($zip.Length/1MB,2);SHA256=$hash;Coordinates='verified';Release='V450'} | ConvertTo-Json
