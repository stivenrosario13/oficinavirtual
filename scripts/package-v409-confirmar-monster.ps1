$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$publish=Join-Path $root "outputs/_publish_v409_$stamp"
$zip=Join-Path $root "outputs/ACTUALIZACION_V409_CONFIRMAR_MONSTER_$stamp.zip"
& npm run build
if($LASTEXITCODE -ne 0){throw 'Error compilando web'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Error publicando API'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
$files=Get-ChildItem $publish -File -Recurse
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\'}){throw 'Archivos privados en publicación'}
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
 foreach($required in @('RegistroAgencias.SqlServer.V409.exe','RegistroAgencias.SqlServer.V409.dll','web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js')){if(!$archive.GetEntry($required)){throw "Falta $required"}}
 foreach($name in @('web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js')){
  $reader=[IO.StreamReader]::new($archive.GetEntry($name).Open())
  try {$content=$reader.ReadToEnd()} finally {$reader.Dispose()}
  if($content -notmatch 'V409'){throw "Version incorrecta: $name"}
  if($name -eq 'web.config' -and $content -notmatch 'RegistroAgencias\.SqlServer\.V409\.exe'){throw 'Configuracion de arranque incorrecta'}
 }
 Write-Host "VERIFICADO: $($archive.Entries.Count) archivos; web y API V409"
} finally {$archive.Dispose()}
Get-Item $zip | Select-Object FullName,Length
Get-FileHash $zip -Algorithm SHA256
