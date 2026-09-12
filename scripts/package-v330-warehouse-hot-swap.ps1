$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v330_$stamp"
$package = Join-Path $root "outputs/_staged_v330_$stamp"
$out = Join-Path $root "outputs/V330_WAREHOUSE_HOT_SWAP_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_V330_WAREHOUSE_HOT_SWAP_$stamp.zip"
New-Item -ItemType Directory -Path $publish,$package,$out | Out-Null
Set-Location $root

# Publicación autocontenida de archivos de aplicación. No se incluye
# app_offline.htm: Monster puede recibir el paquete mientras la versión
# anterior sigue atendiendo tráfico.
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }
$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }

# Doble ruta compatible con las dos estructuras de Monster. Se suben primero
# los binarios y estáticos; web.config se reemplaza al final desde el panel.
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse
$second = Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config')
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $second 'web.config')

$readme = @'
ACTUALIZACIÓN V330 · MONSTER SIN PARADA MANUAL

1. Mantén abierta la versión actual.
2. Sube este ZIP y extrae/sobrescribe primero los archivos de la aplicación y wwwroot.
3. Reemplaza web.config al final. No subas app_offline.htm ni detengas el sitio.
4. Monster hará un único reciclado automático al detectar el nuevo web.config; las
   sesiones persistentes y los datos de SQL Server se conservan. Puede existir una
   reconexión automática de pocos segundos durante el cambio de proceso.
5. Comprueba /api/version: debe responder release V330 y
   deployment monster-staged-hot-swap-no-app-offline-v330.

Este paquete no contiene credenciales, App_Data, uploads ni app_offline.htm.
'@
Set-Content -Path (Join-Path $out 'LEEME_SIN_PARAR_MONSTER_V330.txt') -Value $readme -Encoding UTF8
Set-Content -Path (Join-Path $package 'LEEME_SIN_PARAR_MONSTER_V330.txt') -Value $readme -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($directory in (Get-ChildItem $package -Directory -Recurse | Sort-Object FullName)) {
    $name=$directory.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/')
    [void]$archive.CreateEntry($name+'/')
  }
  foreach ($file in (Get-ChildItem $package -File -Recurse | Sort-Object FullName)) {
    $name=$file.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
  }
} finally { $archive.Dispose() }

$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
  $names=@($archive.Entries.FullName)
  foreach($prefix in @('','wwwroot/')) {
    foreach($name in @('RegistroAgencias.SqlServer.V330.exe','RegistroAgencias.SqlServer.V330.dll','RegistroAgencias.SqlServer.V330.deps.json','RegistroAgencias.SqlServer.V330.runtimeconfig.json','web.config')) {
      if(($prefix+$name)-notin $names){throw "Falta $prefix$name"}
    }
  }
  if($names -contains 'index.html'){throw 'El ZIP no conserva la estructura de doble ruta.'}
  if($names | Where-Object {$_ -match 'V32[0-9]|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'}) {
    throw 'El ZIP contiene una versión anterior, datos privados o app_offline.htm.'
  }
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$') {
    $reader=[IO.StreamReader]::new($entry.Open())
    try {$json=$reader.ReadToEnd();$metadata=$json|ConvertFrom-Json} finally {$reader.Dispose()}
    if($metadata.release -ne 'V330'){throw 'Versión web incorrecta.'}
  }
  Write-Host "ZIP verificado sin app_offline.htm: $($archive.Entries.Count) entradas"
} finally { $archive.Dispose() }
$sha=[System.Security.Cryptography.SHA256]::Create()
try {$stream=[IO.File]::OpenRead($zipPath);try {$hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','')}finally {$stream.Dispose()}} finally {$sha.Dispose()}
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName,Length,@{Name='SHA256';Expression={$hash}}
