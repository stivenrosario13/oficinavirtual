$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v376_normal_$stamp"
$package = Join-Path $root "outputs/_double_v376_normal_$stamp"
$out = Join-Path $root "outputs/V376_FORMULARIOS_ADMINISTRATIVOS_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_COMPLETA_V376_FORMULARIOS_ADMINISTRATIVOS_$stamp.zip"
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
ACTUALIZACIÓN COMPLETA V376 · FORMULARIOS ADMINISTRATIVOS · MONSTER

CAMBIOS INCLUIDOS:
- Archivos y comunicaciones disponible para Administración, Tecnología,
  Servicios Generales y Recursos Humanos, con intercambio departamental.
- Catálogo de solicitudes administrativas con formularios específicos para:
  reposición de SIM Claro, renovación de marbete, entrega de SIM y equipo,
  mandamiento de pago y solicitud general.
- Cada formulario genera PDF institucional moderno con logo, folio, QR,
  firmas, responsable, identidad del solicitante y trazabilidad.
- Relaciones dinámicas de líneas telefónicas y vehículos con varias páginas.
- Responsables configurados para Administración, Recepción y Comunicaciones
  y Redes según la relación suministrada.
- Conserva solicitudes, documentos, inventarios, plantillas y datos existentes.

INSTALACIÓN:
1. Detén temporalmente el sitio desde el panel de MonsterASP.NET.
2. Conserva una copia de seguridad del sitio y de App_Data.
3. Extrae este ZIP donde está el web.config activo y acepta reemplazar.
4. No elimines App_Data ni uploads. El ZIP no incluye esos datos.
5. Confirma que web.config apunte a RegistroAgencias.SqlServer.V376.exe.
6. Inicia el sitio, abre /api/version y confirma V376. Luego pulsa Ctrl+F5.
7. Entra a Soporte > departamento > Archivos y comunicaciones > Solicitudes
   y abre Nueva solicitud para revisar el catálogo de formularios.

El ZIP no contiene credenciales, bases de datos, App_Data, uploads, logs,
appsettings, archivos .env ni app_offline.htm.
'@
Set-Content (Join-Path $out 'LEEME_ACTUALIZACION_V376.txt') $readme -Encoding UTF8
Set-Content (Join-Path $package 'LEEME_ACTUALIZACION_V376.txt') $readme -Encoding UTF8

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
    foreach($name in @('RegistroAgencias.SqlServer.V376.exe','RegistroAgencias.SqlServer.V376.dll','RegistroAgencias.SqlServer.V376.deps.json','RegistroAgencias.SqlServer.V376.runtimeconfig.json','BouncyCastle.Crypto.dll','web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js','Data/agency-locations-sonadora-20260907.json')){if(($prefix+$name) -notin $names){throw "Falta $prefix$name"}}
  }
  if($names -contains 'index.html'){throw 'El ZIP no conserva el formato normal de doble ruta.'}
  if($names | Where-Object {$_ -match 'RegistroAgencias\.SqlServer\.V(?!376)|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'}){throw 'El ZIP contiene otra versión, datos privados o entradas no permitidas.'}
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$'){$reader=[IO.StreamReader]::new($entry.Open());try{$metadata=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()};if($metadata.release -ne 'V376'){throw 'Versión web incorrecta.'}}
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)web\.config$'){$reader=[IO.StreamReader]::new($entry.Open());try{$config=$reader.ReadToEnd()}finally{$reader.Dispose()};if($config -notmatch 'RegistroAgencias\.SqlServer\.V376\.exe'){throw 'web.config no apunta a V376.'}}
  Write-Host "ZIP completo V376 verificado: $($archive.Entries.Count) entradas" -ForegroundColor Green
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName,Length,@{Name='SHA256';Expression={$hash}}
