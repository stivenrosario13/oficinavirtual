$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v352_normal_$stamp"
$package = Join-Path $root "outputs/_double_v352_normal_$stamp"
$out = Join-Path $root "outputs/V352_ALMACEN_INVENTARIO_RETIRO_QR_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_V352_ALMACEN_INVENTARIO_RETIRO_QR_$stamp.zip"

New-Item -ItemType Directory -Path $publish, $package, $out -Force | Out-Null
Set-Location $root

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
ACTUALIZACIÓN V352 · ALMACÉN, INVENTARIO Y RETIRO POR QR · MONSTER

El departamento solicita sin serial de equipo; Almacén selecciona existencias,
registra seriales por unidad y reserva equipos en Pendientes de retiro.
El QR del sello autorizado confirma el retiro y genera el comprobante con
sellos de entregado y recibido, responsables, agencia, departamento y fecha.
El visor conserva la posición al recibir y actualizar el comprobante.
Incluye las correcciones anteriores del sistema, sin cambios en tickets.

Validación: 193 pruebas del proyecto y 31 comprobaciones SQL aisladas.
La base temporal de pruebas fue eliminada; los datos reales no se tocaron.

PASOS:
1. Detén temporalmente el sitio desde el panel de MonsterASP.NET.
2. Conserva tu copia de seguridad actual.
3. Extrae este ZIP completo donde está el web.config activo, aceptando
   reemplazar archivos.
4. Confirma que web.config apunte a RegistroAgencias.SqlServer.V352.exe.
5. Inicia nuevamente el sitio y abre /api/version; debe indicar V352.
6. Abre la aplicación con Ctrl+F5.

No contiene credenciales, bases de datos, App_Data, uploads, registros ni
app_offline.htm. La actualización de estados SQL se hace automáticamente.
Incluye el sistema completo.
'@
Set-Content -Path (Join-Path $out 'LEEME_ACTUALIZACION_V352.txt') -Value $readme -Encoding UTF8
Set-Content -Path (Join-Path $package 'LEEME_ACTUALIZACION_V352.txt') -Value $readme -Encoding UTF8

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
      'RegistroAgencias.SqlServer.V352.exe',
      'RegistroAgencias.SqlServer.V352.dll',
      'RegistroAgencias.SqlServer.V352.deps.json',
      'RegistroAgencias.SqlServer.V352.runtimeconfig.json',
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
    $_ -match 'RegistroAgencias\.SqlServer\.V(?!352)|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'
  }) { throw 'El ZIP contiene otra versión, datos privados o entradas no permitidas.' }
  foreach ($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$') {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $metadata = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($metadata.release -ne 'V352') { throw 'Versión web incorrecta.' }
  }
  foreach ($entry in $archive.Entries | Where-Object FullName -match '(^|/)web\.config$') {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $configText = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($configText -notmatch 'RegistroAgencias\.SqlServer\.V352\.exe') { throw 'web.config no apunta a V352.' }
  }
  Write-Host "ZIP normal V352 verificado: $($archive.Entries.Count) entradas" -ForegroundColor Green
}
finally { $archive.Dispose() }

$sha = [System.Security.Cryptography.SHA256]::Create()
try {
  $stream = [IO.File]::OpenRead($zipPath)
  try { $hash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','') }
  finally { $stream.Dispose() }
}
finally { $sha.Dispose() }
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName, Length, @{Name='SHA256'; Expression={ $hash }}
