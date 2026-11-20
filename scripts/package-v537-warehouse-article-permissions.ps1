$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=[IO.Path]::GetFullPath((Join-Path $root "outputs/_publish_v537_warehouse_permissions_$stamp"))
$zip=[IO.Path]::GetFullPath((Join-Path $root "outputs/V537_PERMISOS_COMPLETOS_CREAR_ARTICULOS_ALMACEN_INCREMENTAL_DOBLE_RUTA_MONSTERASP_$stamp.zip"))

& npm run build
if($LASTEXITCODE-ne 0){throw 'Error compilando la interfaz V537.'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE-ne 0){throw 'Error publicando la API V537.'}

$release='V537-WEB-'+(Get-Date -Format 'yyyyMMdd-HHmm')
$index=Join-Path $publish 'wwwroot/index.html'
$html=[IO.File]::ReadAllText($index)
$html=[Text.RegularExpressions.Regex]::Replace($html,'<meta name="app-release" content="[^"]+"\s*/>','<meta name="app-release" content="'+$release+'" />')
[IO.File]::WriteAllText($index,$html,[Text.UTF8Encoding]::new($false))
$versionFile=Join-Path $publish 'wwwroot/version-web.json'
$version=Get-Content $versionFile -Raw|ConvertFrom-Json
$version.release=$release;$version.apiRelease=$release;$version.builtAt=(Get-Date).ToUniversalTime().ToString('o')
$version.changes=@('warehouse-create-article-and-complete-user-permissions-v537-20261117')+@($version.changes)
$version.cacheGuard='warehouse-permissions-v537-20261117'
$version|ConvertTo-Json -Depth 30|Set-Content $versionFile -Encoding utf8

$instructions=Join-Path $root 'docs/INSTRUCCIONES_V537_PERMISOS_ARTICULOS_ALMACEN.txt'
Copy-Item -LiteralPath $instructions -Destination $publish -Force

# Paquete incremental: binarios y web actual, sin tocar configuración, datos ni cargas.
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File|Where-Object{
  $_.Name -notmatch '^\.env|^appsettings|^app_offline\.htm$|^web\.config$' -and
  $_.FullName -notmatch '\\(App_Data|uploads|logs|Data)\\'
})
if(!$files.Count){throw 'La publicación incremental está vacía.'}
if($files|Where-Object{$_.Name-match '^\.env|^appsettings|^app_offline\.htm$|^web\.config$' -or $_.FullName -match '\\(App_Data|uploads|logs|Data)\\'}){throw 'El paquete contiene archivos que deben conservarse en MonsterASP.'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($prefix in @('','wwwroot/')){
    foreach($file in $files){
      $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
      [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
    }
  }
}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
  $names=@($archive.Entries.FullName)
  if(($names|Select-Object -Unique).Count-ne$names.Count){throw 'El ZIP contiene entradas duplicadas.'}
  if($names|Where-Object{$_-match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm|web\.config)$|(^|/)(App_Data|uploads|logs|Data)/'}){throw 'El ZIP contiene configuración o datos protegidos.'}
  foreach($prefix in @('','wwwroot/')){
    foreach($required in @('RegistroAgencias.SqlServer.V536.dll','wwwroot/index.html','wwwroot/version-web.json','INSTRUCCIONES_V537_PERMISOS_ARTICULOS_ALMACEN.txt')){
      if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}
    }
  }
}finally{$archive.Dispose()}

$hash=(Get-FileHash $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zip+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zip))`r`n",[Text.Encoding]::ASCII)
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item $zip).Length;Files=$files.Count*2;SHA256=$hash;Preserves=@('web.config','App_Data','uploads','logs','appsettings','.env')}|ConvertTo-Json
