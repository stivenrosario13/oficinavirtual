$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v360_normal_$stamp"
$package = Join-Path $root "outputs/_double_v360_normal_$stamp"
$out = Join-Path $root "outputs/V360_SOLICITUD_INVENTARIO_REAL_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_COMPLETA_V360_SOLICITUD_INVENTARIO_REAL_$stamp.zip"

New-Item -ItemType Directory -Path $publish, $package, $out -Force | Out-Null
Set-Location $root

& npm run build
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación web.' }
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }

$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {
  $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or
  $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'
}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }

Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
$second = Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config') -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $second 'web.config') -Force

$readme = @'
ACTUALIZACIÓN COMPLETA V360 · SOLICITUD SEGÚN INVENTARIO REAL · MONSTER

CAMBIOS:
- El formulario Solicitud de equipo ya no pide técnico instalador ni persona
  que recibirá; esa identidad se registra al entregar o retirar el equipo.
- Los departamentos solo pueden seleccionar equipos con existencia disponible
  en el inventario real de Almacén.
- La cantidad solicitada se limita a la disponibilidad y el servidor vuelve a
  validar el inventario antes de aceptar el formulario.
- Los equipos agotados no aparecen en las listas desplegables.
- Solicitudes a Almacén muestra únicamente el formulario Solicitud de equipo
  para todos los departamentos solicitantes.
- Se eliminaron los botones generales Escanear QR o código de Taller y Almacén.
- Se conserva Consulta de trazabilidad en cada solicitud y el retiro autorizado
  por QR, porque forman parte de las tres etapas de seguimiento.
- Incluye todas las funciones y correcciones de V359 y versiones anteriores.

PASOS:
1. Detén temporalmente el sitio desde el panel de MonsterASP.NET.
2. Conserva una copia de seguridad del sitio y de App_Data.
3. Extrae este ZIP completo donde está el web.config activo, aceptando
   reemplazar archivos. No elimines App_Data ni uploads.
4. Confirma que web.config apunte a RegistroAgencias.SqlServer.V360.exe.
5. Inicia el sitio y abre /api/version; debe indicar V360.
6. Abre la aplicación con Ctrl+F5.

El ZIP no contiene credenciales, bases de datos, App_Data, uploads, logs,
appsettings, archivos .env ni app_offline.htm. El esquema requerido se
actualiza automáticamente al iniciar la aplicación.
'@
Set-Content -Path (Join-Path $out 'LEEME_ACTUALIZACION_V360.txt') -Value $readme -Encoding UTF8
Set-Content -Path (Join-Path $package 'LEEME_ACTUALIZACION_V360.txt') -Value $readme -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($directory in (Get-ChildItem $package -Directory -Recurse | Sort-Object FullName)) {
    $name = $directory.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/')
    [void]$archive.CreateEntry($name + '/')
  }
  foreach ($file in (Get-ChildItem $package -File -Recurse | Sort-Object FullName)) {
    $name = $file.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
  }
}
finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
  $names = @($archive.Entries.FullName)
  foreach ($prefix in @('', 'wwwroot/')) {
    foreach ($name in @(
      'RegistroAgencias.SqlServer.V360.exe',
      'RegistroAgencias.SqlServer.V360.dll',
      'RegistroAgencias.SqlServer.V360.deps.json',
      'RegistroAgencias.SqlServer.V360.runtimeconfig.json',
      'BouncyCastle.Crypto.dll',
      'web.config',
      'wwwroot/index.html',
      'wwwroot/version-web.json',
      'wwwroot/release-guard.js',
      'Data/agency-locations-sonadora-20260907.json'
    )) {
      if (($prefix + $name) -notin $names) { throw "Falta $prefix$name" }
    }
  }
  if ($names -contains 'index.html') { throw 'El ZIP no conserva el formato normal de doble ruta.' }
  if ($names | Where-Object {
    $_ -match 'RegistroAgencias\.SqlServer\.V(?!360)|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'
  }) { throw 'El ZIP contiene otra versión, datos privados o entradas no permitidas.' }
  foreach ($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$') {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $metadata = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($metadata.release -ne 'V360') { throw 'Versión web incorrecta.' }
  }
  foreach ($entry in $archive.Entries | Where-Object FullName -match '(^|/)web\.config$') {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $configText = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($configText -notmatch 'RegistroAgencias\.SqlServer\.V360\.exe') { throw 'web.config no apunta a V360.' }
  }
  Write-Host "ZIP completo V360 verificado: $($archive.Entries.Count) entradas" -ForegroundColor Green
}
finally { $archive.Dispose() }

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName, Length, @{Name='SHA256'; Expression={ $hash }}
