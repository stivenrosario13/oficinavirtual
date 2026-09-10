$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$stage=Join-Path $root "artifacts/package-v294-tickets-asignados-$stamp"
$out=Join-Path $root "outputs/V295_PUSH_PWA_FORZADO_DOBLE_RUTA_$stamp"
$publish=Join-Path $stage 'publish'
$package=Join-Path $stage 'package'
$readme=Join-Path $root 'deploy/LEEME_V295_PUSH_PWA_MONSTER.txt'
$zipPath=Join-Path $out 'ACTUALIZACION_FORZADA_V295_PUSH_PWA_DOBLE_RUTA.zip'

New-Item -ItemType Directory -Path $publish,$package,$out -Force | Out-Null
Set-Location $root
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Falló dotnet publish'}

$unexpected=Get-ChildItem $publish -Recurse -File | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(uploads|preview|logs)\\'}
if($unexpected){throw 'La publicación contiene configuración o datos que requieren revisión.'}

# Mantiene el formato de doble ruta usado por el paquete V294: archivos en la
# raíz y una segunda copia completa bajo wwwroot. web.config se entrega como
# archivo .nuevo para que Monster conserve la configuración del servidor.
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
$second=Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse -Force
foreach($destination in @($package,$second)){
  Copy-Item (Join-Path $root 'api/web.config') (Join-Path $destination 'web.config') -Force
  Copy-Item $readme (Join-Path $destination 'LEEME_V295_PUSH_PWA_MONSTER.txt')
}
Copy-Item $readme $out

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
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
    foreach($name in @('RegistroAgencias.SqlServer.V295.exe','RegistroAgencias.SqlServer.V295.dll','RegistroAgencias.SqlServer.V295.deps.json','RegistroAgencias.SqlServer.V295.runtimeconfig.json','web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/sw.js','wwwroot/manifest.webmanifest')){
      if(($prefix+$name) -notin $names){throw ('Falta '+$prefix+$name)}
    }
  }
  if($names | Where-Object {$_ -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(preview|node_modules|uploads|logs)/|\\'}){throw 'Entrada no permitida en ZIP'}
  foreach($entry in $zip.Entries | Where-Object {$_.FullName -match '(^|/)index.html$'}){
    $reader=[IO.StreamReader]::new($entry.Open());try{$html=$reader.ReadToEnd()}finally{$reader.Dispose()}
    if($html -notmatch 'V295'){throw 'Interfaz con versión incorrecta'}
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
