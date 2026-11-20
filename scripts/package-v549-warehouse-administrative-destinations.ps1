$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$release='V549-WEB-'+(Get-Date -Format 'yyyyMMdd-HHmm')
$source=[IO.Path]::GetFullPath((Join-Path $root 'api/wwwroot'))
$stage=[IO.Path]::GetFullPath((Join-Path $root "outputs/_stage_v549_warehouse_admin_destinations_$stamp"))
$zip=[IO.Path]::GetFullPath((Join-Path $root "outputs/V549_SOLO_DESTINOS_SOLICITUDES_ALMACEN_ADMINISTRATIVOS_DOBLE_RUTA_MONSTERASP_$stamp.zip"))
$instructions=[IO.Path]::GetFullPath((Join-Path $root 'docs/INSTRUCCIONES_V549_DESTINOS_SOLICITUDES_ALMACEN.txt'))

if(!(Test-Path (Join-Path $source 'index.html'))){throw 'Primero debe existir una compilación web validada.'}
New-Item -ItemType Directory -Path $stage|Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $stage 'web') -Recurse -Force
Copy-Item -LiteralPath $instructions -Destination $stage -Force

$web=Join-Path $stage 'web'
$index=Join-Path $web 'index.html'
$html=[IO.File]::ReadAllText($index)
$html=[Text.RegularExpressions.Regex]::Replace($html,'<meta name="app-release" content="[^"]+"\s*/>','<meta name="app-release" content="'+$release+'" />')
[IO.File]::WriteAllText($index,$html,[Text.UTF8Encoding]::new($false))
$versionPath=Join-Path $web 'version-web.json'
$version=Get-Content $versionPath -Raw|ConvertFrom-Json
$version.release=$release
$version.apiRelease=$release
$version.builtAt=(Get-Date).ToUniversalTime().ToString('o')
$version.changes=@('warehouse-administrative-destinations-v549-20261120')+@($version.changes)
$version.cacheGuard='warehouse-administrative-destinations-v549-20261120'
$version|ConvertTo-Json -Depth 30|Set-Content $versionPath -Encoding utf8

$files=@(Get-ChildItem $web -Recurse -File)
if(!$files.Count){throw 'La interfaz compilada está vacía.'}
if($files|Where-Object{$_.Name-match '^\.env|^appsettings|^app_offline\.htm$|^web\.config$' -or $_.FullName -match '\\(App_Data|uploads|logs|Data)\\'}){throw 'La interfaz contiene configuración o datos protegidos.'}
$composer=@($files|Where-Object Name -Like 'WarehouseRequestComposer-*.js')
if($composer.Count-ne 1){throw 'No se encontró un único módulo compilado de Solicitudes a Almacén.'}
$composerText=[IO.File]::ReadAllText($composer[0].FullName)
foreach($marker in @('Para una agencia','Para varias agencias','Para mi departamento','Una solicitud y un PDF por cada agencia')){if(!$composerText.Contains($marker)){throw "Falta marcador funcional: $marker"}}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($prefix in @('','wwwroot/')){
    foreach($file in $files){
      $relative=$file.FullName.Substring($web.Length+1).Replace('\','/')
      [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$prefix+$relative,[IO.Compression.CompressionLevel]::Optimal)
    }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$instructions,$prefix+[IO.Path]::GetFileName($instructions),[IO.Compression.CompressionLevel]::Optimal)
  }
}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
  $names=@($archive.Entries.FullName)
  if(($names|Select-Object -Unique).Count-ne$names.Count){throw 'El ZIP contiene entradas duplicadas.'}
  if($names|Where-Object{$_-match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm|web\.config|.*\.dll)$|(^|/)(App_Data|uploads|logs|Data)/'}){throw 'El ZIP contiene binarios, configuración o datos protegidos.'}
  foreach($prefix in @('','wwwroot/')){
    foreach($required in @('index.html','version-web.json','INSTRUCCIONES_V549_DESTINOS_SOLICITUDES_ALMACEN.txt')){if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}}
  }
}finally{$archive.Dispose()}

$hash=(Get-FileHash $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zip+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zip))`r`n",[Text.Encoding]::ASCII)
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item $zip).Length;Entries=($files.Count+1)*2;SHA256=$hash;Release=$release;FrontendOnly=$true;Preserves=@('DLL','web.config','appsettings','.env','App_Data','uploads','logs','Data')}|ConvertTo-Json
