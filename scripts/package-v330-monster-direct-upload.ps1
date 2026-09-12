$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v330_direct_$stamp"
$stage = Join-Path $root "outputs/_staged_v330_direct_$stamp"
$out = Join-Path $root "outputs/V330_MONSTER_CARGA_DIRECTA_$stamp"
$filesZip = Join-Path $out "01_ARCHIVOS_V330_SIN_WEB_CONFIG_$stamp.zip"
$activateZip = Join-Path $out "02_ACTIVAR_WEB_CONFIG_V330_$stamp.zip"
$completeZip = Join-Path $out "ACTUALIZACION_V330_CARGA_DIRECTA_$stamp.zip"

New-Item -ItemType Directory -Path $publish, $stage, $out -Force | Out-Null
Set-Location $root

& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }

$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {
  $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or
  $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'
}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }

# Copia exactamente el resultado de dotnet publish: una raíz de aplicación y
# una única carpeta wwwroot para estáticos. No se replica el publish dentro de
# wwwroot ni se crean entradas ZIP de carpetas que algunos FTP de Monster no
# interpretan correctamente.
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $stage -Recurse -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $stage 'web.config') -Force

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-FlatZip([string]$zipPath, [System.IO.FileInfo[]]$files) {
  $archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
  try {
    foreach ($file in $files) {
      $entry = $file.FullName.Substring($stage.Length).TrimStart([char[]]'\/').Replace('\','/')
      # Solo entradas de archivo, usando barras / estándar. No se agregan
      # directorios explícitos ni se escribe wwwroot/BouncyCastle.Crypto.dll.
      [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal
      )
    }
  }
  finally { $archive.Dispose() }
}

$allFiles = @(Get-ChildItem $stage -Recurse -File | Sort-Object FullName)
$appFiles = @($allFiles | Where-Object Name -ne 'web.config')
$webConfig = @($allFiles | Where-Object Name -eq 'web.config')
if ($webConfig.Count -ne 1) { throw 'No se encontró un único web.config.' }

# Dos pasos: archivos y dependencias antes; activación del nuevo proceso al final.
New-FlatZip $filesZip $appFiles
New-FlatZip $activateZip $webConfig
# Paquete completo alternativo para paneles que admiten una única extracción;
# web.config se deja como última entrada para evitar activación prematura.
New-FlatZip $completeZip @($appFiles + $webConfig)

function Test-DirectZip([string]$path, [bool]$expectWebConfig) {
  $archive = [IO.Compression.ZipFile]::OpenRead($path)
  try {
    $names = @($archive.Entries.FullName)
    foreach ($required in @('BouncyCastle.Crypto.dll','RegistroAgencias.SqlServer.V330.exe','RegistroAgencias.SqlServer.V330.dll','wwwroot/index.html','wwwroot/version-web.json')) {
      if ($required -notin $names) { throw "Falta $required en $([IO.Path]::GetFileName($path))." }
    }
    if ($expectWebConfig -and 'web.config' -notin $names) { throw 'Falta web.config en el paquete completo.' }
    if (-not $expectWebConfig -and 'web.config' -in $names) { throw 'El paquete de archivos no puede activar web.config.' }
    if ($names | Where-Object { $_ -match '^wwwroot/(BouncyCastle\.Crypto\.dll|RegistroAgencias\.SqlServer|web\.config|wwwroot/)' }) {
      throw 'Se detectó una ruta no compatible dentro de wwwroot.'
    }
    if ($names | Where-Object { $_ -match '(^|/)(app_offline\.htm|\.env[^/]*|appsettings[^/]*)$|\\' }) {
      throw 'El paquete contiene archivos no permitidos.'
    }
    if ($archive.Entries | Where-Object { $_.FullName.EndsWith('/') }) {
      throw 'El ZIP contiene entradas explícitas de directorio.'
    }
  }
  finally { $archive.Dispose() }
}

Test-DirectZip $filesZip $false
Test-DirectZip $completeZip $true
$activation = [IO.Compression.ZipFile]::OpenRead($activateZip)
try {
  if ($activation.Entries.Count -ne 1 -or $activation.Entries[0].FullName -ne 'web.config') {
    throw 'El ZIP de activación solo debe incluir web.config.'
  }
}
finally { $activation.Dispose() }

$readme = @'
ACTUALIZACIÓN V330 · CARGA DIRECTA FTP MONSTER

Este formato sustituye el ZIP de doble ruta. Tiene una sola raíz de aplicación:
- BouncyCastle.Crypto.dll y RegistroAgencias.SqlServer.V330.exe están en la raíz.
- Los recursos web están una sola vez en wwwroot.
- No contiene wwwroot/wwwroot ni wwwroot/BouncyCastle.Crypto.dll.
- No contiene app_offline.htm.

Método recomendado sin parada manual:
1. En Monster abre la carpeta que contiene el web.config activo y el ejecutable V329.
2. Extrae 01_ARCHIVOS_V330_SIN_WEB_CONFIG_*.zip directamente en esa carpeta.
3. Confirma que BouncyCastle.Crypto.dll queda junto al ejecutable, no dentro de wwwroot.
4. Extrae 02_ACTIVAR_WEB_CONFIG_V330_*.zip en la misma carpeta.
5. Espera la reconexión automática del proceso y consulta /api/version: release V330.

Alternativa: el ZIP ACTUALIZACION_V330_CARGA_DIRECTA_*.zip incluye todo. Si el
administrador web de Monster vuelve a reportar un error de ZIP, descomprímelo
en tu PC y sube sus archivos por FTP preservando las rutas.
'@
Set-Content (Join-Path $out 'LEEME_CARGA_DIRECTA_V330.txt') $readme -Encoding UTF8

$sha = [System.Security.Cryptography.SHA256]::Create()
try {
  $lines = foreach ($path in @($filesZip,$activateZip,$completeZip)) {
    $stream = [IO.File]::OpenRead($path)
    try { "{0}  {1}" -f ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-',''), ([IO.Path]::GetFileName($path)) }
    finally { $stream.Dispose() }
  }
}
finally { $sha.Dispose() }
Set-Content (Join-Path $out 'SHA256.txt') $lines
Write-Host 'Paquetes V330 de carga directa verificados correctamente.' -ForegroundColor Green
Get-ChildItem $out -File | Select-Object Name, Length
