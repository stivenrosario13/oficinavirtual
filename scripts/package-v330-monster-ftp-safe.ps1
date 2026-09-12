$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v330_ftp_$stamp"
$package = Join-Path $root "outputs/_staged_v330_ftp_$stamp"
$out = Join-Path $root "outputs/V330_MONSTER_FTP_SAFE_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_V330_MONSTER_FTP_SAFE_$stamp.zip"

New-Item -ItemType Directory -Path $publish, $package, $out -Force | Out-Null
Set-Location $root

# Publica una sola raíz de aplicación. El ZIP anterior copiaba el publish dos
# veces y creaba wwwroot/wwwroot; Monster intenta resolver esa ruta como un
# archivo local y falla con BouncyCastle.Crypto.dll.
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }

$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {
  $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or
  $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'
}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }

# La carpeta package representa exactamente la carpeta que contiene el
# web.config activo en Monster. wwwroot interno se conserva una sola vez.
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config') -Force

$readme = @'
ACTUALIZACIÓN V330 · MONSTER FTP SAFE · UNA SOLA RAÍZ

Este paquete corrige el error:
  Error during FTP upload, file not found: "/wwwroot/BouncyCastle.Crypto.dll"

La estructura es la publicación normal de ASP.NET Core: BouncyCastle.Crypto.dll,
RegistroAgencias.SqlServer.V330.exe y web.config están en la raíz de la aplicación;
los archivos estáticos están únicamente dentro de la carpeta interna wwwroot.
No existe wwwroot/wwwroot y no se incluye app_offline.htm.

Instalación sin parada manual:
1. En Monster abre la carpeta que contiene el web.config ACTIVO (la raíz de la
   aplicación). No abras ni extraigas dentro de la carpeta wwwroot interna.
2. Extrae/sube el contenido de este ZIP a esa misma carpeta, conservando la carpeta
   interna wwwroot.
3. Para minimizar la interrupción, sube primero DLL/EXE, dependencias y wwwroot;
   reemplaza web.config al final. No subas app_offline.htm ni borres archivos de
   datos existentes.
4. Si el panel FTP no extrae ZIP, descomprímelo localmente y sube los archivos con
   sus rutas relativas; no uses una ruta manual /wwwroot/BouncyCastle.Crypto.dll.
5. Comprueba /api/version: release V330 y deployment
   monster-staged-hot-swap-no-app-offline-v330.

Monster puede reciclar automáticamente el proceso al cambiar web.config; no se
requiere detener el sitio manualmente. Las sesiones y la base SQL Server se
conservan.
'@
Set-Content -Path (Join-Path $out 'LEEME_MONSTER_FTP_SAFE_V330.txt') -Value $readme -Encoding UTF8
Set-Content -Path (Join-Path $package 'LEEME_MONSTER_FTP_SAFE_V330.txt') -Value $readme -Encoding UTF8

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

# Verificación estricta de la estructura que Monster debe recibir.
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
  $names = @($archive.Entries.FullName)
  foreach ($name in @(
    'BouncyCastle.Crypto.dll',
    'RegistroAgencias.SqlServer.V330.exe',
    'RegistroAgencias.SqlServer.V330.dll',
    'RegistroAgencias.SqlServer.V330.deps.json',
    'RegistroAgencias.SqlServer.V330.runtimeconfig.json',
    'web.config',
    'wwwroot/index.html',
    'wwwroot/version-web.json',
    'wwwroot/release-guard.js'
  )) {
    if ($name -notin $names) { throw "Falta $name" }
  }
  if ($names | Where-Object { $_ -match '^wwwroot/(BouncyCastle|RegistroAgencias\.SqlServer\.V330|web\.config)' }) {
    throw 'La DLL o el backend quedaron duplicados dentro de wwwroot.'
  }
  if ($names | Where-Object { $_ -match '^wwwroot/wwwroot/' }) {
    throw 'El ZIP contiene la ruta inválida wwwroot/wwwroot.'
  }
  if ($names | Where-Object {
    $_ -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$' -or
    $_ -match '(^|/)(App_Data|uploads|preview|logs|node_modules)/' -or
    $_ -match '\\'
  }) { throw 'El ZIP contiene archivos privados, app_offline.htm o barras invertidas.' }
  $versionEntry = $archive.Entries | Where-Object FullName -eq 'wwwroot/version-web.json'
  $reader = [IO.StreamReader]::new($versionEntry.Open())
  try { $metadata = ($reader.ReadToEnd() | ConvertFrom-Json) } finally { $reader.Dispose() }
  if ($metadata.release -ne 'V330') { throw 'La versión web no es V330.' }
  Write-Host "ZIP verificado: una sola raíz, sin app_offline.htm ni wwwroot/wwwroot ($($archive.Entries.Count) entradas)" -ForegroundColor Green
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
