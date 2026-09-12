$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v330_normal_$stamp"
$package = Join-Path $root "outputs/_double_v330_normal_$stamp"
$out = Join-Path $root "outputs/V330_ACTUALIZACION_NORMAL_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_V330_COMPLETA_$stamp.zip"
New-Item -ItemType Directory -Path $publish, $package, $out -Force | Out-Null
Set-Location $root

# Formato normal compatible con la actualización V329 proporcionada: el sitio
# se detiene manualmente desde Monster antes de extraer el ZIP completo.
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }

$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {
  $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or
  $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'
}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }

# Conserva la doble ruta que empleaba el paquete V329 normal solicitado.
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
$second = Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config') -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $second 'web.config') -Force

$readme = @'
ACTUALIZACIÓN COMPLETA V330 · FORMATO NORMAL MONSTER

Este ZIP mantiene el formato de actualización V329: instala los archivos en la
raíz y también en wwwroot para las estructuras habituales de MonsterASP.NET.

PASOS:
1. En MonsterASP.NET detén el sitio temporalmente desde el panel.
2. Abre la carpeta donde está el web.config activo o la carpeta wwwroot usada
   por tu sitio y extrae este ZIP completo, aceptando reemplazar archivos.
3. Comprueba que web.config apunte a RegistroAgencias.SqlServer.V330.exe.
4. Inicia el sitio nuevamente desde MonsterASP.NET.
5. Abre /api/version: debe indicar release V330.
6. Finalmente, abre la aplicación con Ctrl+F5 para renovar los archivos web.

No contiene credenciales, App_Data, uploads, registros ni app_offline.htm.
'@
Set-Content -Path (Join-Path $out 'LEEME_ACTUALIZACION_NORMAL_V330.txt') -Value $readme -Encoding UTF8
Set-Content -Path (Join-Path $package 'LEEME_ACTUALIZACION_NORMAL_V330.txt') -Value $readme -Encoding UTF8

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
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
      $archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal
    )
  }
}
finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
  $names = @($archive.Entries.FullName)
  foreach ($prefix in @('', 'wwwroot/')) {
    foreach ($name in @(
      'RegistroAgencias.SqlServer.V330.exe',
      'RegistroAgencias.SqlServer.V330.dll',
      'RegistroAgencias.SqlServer.V330.deps.json',
      'RegistroAgencias.SqlServer.V330.runtimeconfig.json',
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
    $_ -match 'V32[0-9]|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'
  }) { throw 'El ZIP contiene una versión anterior, datos privados o entradas no permitidas.' }
  foreach ($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$') {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $metadata = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($metadata.release -ne 'V330') { throw 'Versión web incorrecta.' }
  }
  Write-Host "ZIP normal V330 verificado: $($archive.Entries.Count) entradas" -ForegroundColor Green
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
