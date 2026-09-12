$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v328_$stamp"
$package = Join-Path $root "outputs/_double_v328_$stamp"
$out = Join-Path $root "outputs/V328_LOGIN_RESILIENCE_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_V328_LOGIN_RESILIENCE_$stamp.zip"
New-Item -ItemType Directory -Path $publish,$package,$out | Out-Null
Set-Location $root
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }
$unexpected = Get-ChildItem $publish -Recurse -File | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'}
if ($unexpected) { throw 'La publicación contiene configuración o datos no permitidos.' }
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse
$second = Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config')
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $second 'web.config')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($directory in (Get-ChildItem $package -Directory -Recurse | Sort-Object FullName)) {$name=$directory.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/');[void]$archive.CreateEntry($name+'/')}
  foreach ($file in (Get-ChildItem $package -File -Recurse | Sort-Object FullName)) {$name=$file.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)}
} finally {$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
  $names=@($archive.Entries.FullName)
  foreach($prefix in @('','wwwroot/')){foreach($name in @('RegistroAgencias.SqlServer.V328.exe','RegistroAgencias.SqlServer.V328.dll','RegistroAgencias.SqlServer.V328.deps.json','RegistroAgencias.SqlServer.V328.runtimeconfig.json','web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js','Data/agency-locations-sonadora-20260907.json')){if(($prefix+$name)-notin $names){throw "Falta $prefix$name"}}}
  if($names-contains 'index.html'){throw 'El ZIP no conserva la estructura de doble ruta.'}
  if($names|Where-Object{$_-match 'V32[01234567]|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'}){throw 'El ZIP contiene una versión anterior o una entrada no permitida.'}
  foreach($entry in $archive.Entries|Where-Object FullName -match '(^|/)version-web\.json$'){$reader=[IO.StreamReader]::new($entry.Open());try{$json=$reader.ReadToEnd();$metadata=$json|ConvertFrom-Json}finally{$reader.Dispose()};if($metadata.release-ne'V328'){throw 'Versión web incorrecta.'}}
  Write-Host "ZIP verificado: $($archive.Entries.Count) entradas"
} finally {$archive.Dispose()}
$hash=(Get-FileHash $zipPath -Algorithm SHA256).Hash
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName,Length,@{Name='SHA256';Expression={$hash}}
