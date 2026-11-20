$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v365_normal_$stamp"
$package = Join-Path $root "outputs/_double_v365_normal_$stamp"
$out = Join-Path $root "outputs/V365_SERVICIOS_GENERALES_ADMIN_REPORTE_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_COMPLETA_V365_SERVICIOS_GENERALES_ADMIN_REPORTE_$stamp.zip"
New-Item -ItemType Directory -Path $publish,$package,$out -Force | Out-Null
Set-Location $root

& npm run build
if($LASTEXITCODE -ne 0){throw 'Falló la compilación web.'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Falló dotnet publish.'}

$unexpected=Get-ChildItem $publish -Recurse -File | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'}
if($unexpected){throw 'La publicación contiene configuración o datos no permitidos.'}
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
$second=Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config') -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $second 'web.config') -Force

$readme=@'
ACTUALIZACIÓN COMPLETA V365 · SERVICIOS GENERALES Y REPORTE DE AGENCIA · MONSTER

CAMBIOS INCLUIDOS:
- Servicios Generales conserva el panel operativo de seis tarjetas y elimina
  la tarjeta adicional Crear ticket.
- Restaura Reportar incidencia en mi agencia para Administrador y
  Administrador de grupo, conservando la agencia seleccionada.
- Corrección de compatibilidad del API de Taller en MonsterASP.NET: acepta
  respuesta JSON directa, codificada o envuelta sin mostrar una falsa falla.
- Solicitudes de equipos: selector sin cantidades y buscador de agencia por
  nombre, terminal, código o grupo.
- Almacén: tabla de equipos dañados recibidos y cola separada de inventario
  en reparación / reemplazo.
- Recepción dañada: permite guardar en cola o preparar inmediatamente el
  formulario de envío a Taller.
- Envío a Taller sin responsable asignado; conserva quien entrega y recibe.
- PDF de Taller con QR, sellos de entrega/recepción y firma a la derecha.
- Taller reforzado para registros antiguos o incompletos, evitando que un
  registro defectuoso cierre todo el módulo.
- Consulta de trazabilidad con escáner QR desde la cámara trasera del móvil,
  solicitando la máxima resolución compatible con el dispositivo.
- Incluye también los cambios solicitados en V362 sobre solicitudes, estados,
  hallazgos, tarjetas de tickets, autor de auditoría y total real de auditorías.

INSTALACIÓN:
1. Detén temporalmente el sitio desde el panel de MonsterASP.NET.
2. Conserva una copia de seguridad del sitio y de App_Data.
3. Extrae este ZIP donde está el web.config activo y acepta reemplazar.
4. No elimines App_Data ni uploads.
5. Confirma que web.config apunte a RegistroAgencias.SqlServer.V365.exe.
6. Inicia el sitio, abre /api/version y confirma V365. Luego pulsa Ctrl+F5.
7. Para el escáner móvil, abre el sitio por HTTPS y permite la cámara.

El ZIP no contiene credenciales, bases de datos, App_Data, uploads, logs,
appsettings, archivos .env ni app_offline.htm.
'@
Set-Content (Join-Path $out 'LEEME_ACTUALIZACION_V365.txt') $readme -Encoding UTF8
Set-Content (Join-Path $package 'LEEME_ACTUALIZACION_V365.txt') $readme -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($directory in Get-ChildItem $package -Directory -Recurse | Sort-Object FullName){$name=$directory.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/');[void]$archive.CreateEntry($name+'/')}
  foreach($file in Get-ChildItem $package -File -Recurse | Sort-Object FullName){$name=$file.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)}
}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try{
  $names=@($archive.Entries.FullName)
  foreach($prefix in @('','wwwroot/')){
    foreach($name in @('RegistroAgencias.SqlServer.V365.exe','RegistroAgencias.SqlServer.V365.dll','RegistroAgencias.SqlServer.V365.deps.json','RegistroAgencias.SqlServer.V365.runtimeconfig.json','BouncyCastle.Crypto.dll','web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js','Data/agency-locations-sonadora-20260907.json')){if(($prefix+$name) -notin $names){throw "Falta $prefix$name"}}
  }
  if($names -contains 'index.html'){throw 'El ZIP no conserva el formato normal de doble ruta.'}
  if($names | Where-Object {$_ -match 'RegistroAgencias\.SqlServer\.V(?!365)|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'}){throw 'El ZIP contiene otra versión, datos privados o entradas no permitidas.'}
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$'){$reader=[IO.StreamReader]::new($entry.Open());try{$metadata=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()};if($metadata.release -ne 'V365'){throw 'Versión web incorrecta.'}}
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)web\.config$'){$reader=[IO.StreamReader]::new($entry.Open());try{$config=$reader.ReadToEnd()}finally{$reader.Dispose()};if($config -notmatch 'RegistroAgencias\.SqlServer\.V365\.exe'){throw 'web.config no apunta a V365.'}}
  Write-Host "ZIP completo V365 verificado: $($archive.Entries.Count) entradas" -ForegroundColor Green
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName,Length,@{Name='SHA256';Expression={$hash}}


