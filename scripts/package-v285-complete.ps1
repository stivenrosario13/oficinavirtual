$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stage=Join-Path $root 'artifacts/package-v285'
$out=Join-Path $root 'outputs/V285_ALMACEN_TALLER_COMPLETO'
if((Test-Path $stage) -or (Test-Path $out)){throw 'La salida V285 ya existe; no se sobrescribirá.'}
$publish=Join-Path $stage 'publish'
$package=Join-Path $stage 'package'
New-Item -ItemType Directory -Path $publish,$package,$out -Force | Out-Null
Set-Location $root
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Falló dotnet publish'}
# No include server-specific configuration or data. Keep web.config as an explicit activation reference.
$unexpected=Get-ChildItem $publish -Recurse -File | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(uploads|preview|logs)\\'}
if($unexpected){throw 'La publicación contiene configuración o datos que requieren revisión.'}
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
$second=Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse -Force
foreach($destination in @($package,$second)){
 Copy-Item (Join-Path $root 'api/web.config') (Join-Path $destination 'web.config.V285.nuevo')
 Copy-Item (Join-Path $root 'deploy/LEEME_V285_MONSTER.txt') (Join-Path $destination 'LEEME_V285_MONSTER.txt')
}
Copy-Item (Join-Path $root 'deploy/LEEME_V285_MONSTER.txt') $out
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath=Join-Path $out 'ACTUALIZACION_COMPLETA_V285_MONSTER.zip'
$zip=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($directory in (Get-ChildItem $package -Directory -Recurse | Sort-Object FullName)){
  $name=$directory.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/')
  [void]$zip.CreateEntry($name+'/')
 }
 foreach($file in (Get-ChildItem $package -File -Recurse | Sort-Object FullName)){
  $name=$file.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
 }
}finally{$zip.Dispose()}
$zip=[IO.Compression.ZipFile]::OpenRead($zipPath)
try{
 $names=@($zip.Entries.FullName)
 foreach($prefix in @('','wwwroot/')){
  foreach($name in @('RegistroAgencias.SqlServer.V285.exe','RegistroAgencias.SqlServer.V285.dll','RegistroAgencias.SqlServer.V285.deps.json','RegistroAgencias.SqlServer.V285.runtimeconfig.json','web.config.V285.nuevo','wwwroot/index.html','wwwroot/version-web.json','wwwroot/vendor/pdfjs/pdf.mjs','wwwroot/documents/plantilla-conduce-institucional.pdf')){
   if(($prefix+$name) -notin $names){throw ('Falta '+$prefix+$name)}
  }
 }
 if($names | Where-Object {$_ -match '(^|/)(web\.config|\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(preview|node_modules|uploads|logs)/|\\'}){throw 'Entrada no permitida en ZIP'}
 foreach($entry in $zip.Entries | Where-Object {$_.FullName -match '(^|/)index.html$'}){
  $reader=[IO.StreamReader]::new($entry.Open());try{$html=$reader.ReadToEnd()}finally{$reader.Dispose()}
  if($html -notmatch 'V285'){throw 'Interfaz con versión incorrecta'}
  $parent=$entry.FullName.Substring(0,$entry.FullName.LastIndexOf('/')+1)
  foreach($match in [regex]::Matches($html,'(?:src|href)="/(assets/[^"]+)"')){
   if(($parent+$match.Groups[1].Value) -notin $names){throw 'Falta un asset referenciado'}
  }
 }
 Write-Host ('ZIP verificado: '+$zip.Entries.Count+' entradas')
}finally{$zip.Dispose()}
$hash=(Get-FileHash $zipPath -Algorithm SHA256).Hash
($hash+'  '+[IO.Path]::GetFileName($zipPath)) | Set-Content (Join-Path $out 'SHA256.txt')
Get-Item $zipPath | Select-Object FullName,Length

