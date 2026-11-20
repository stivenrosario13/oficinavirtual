$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_v501_incidentes_$stamp"
$zip=Join-Path $root "outputs/INCIDENTES_V501_CORREGIDO_COMPLETO_DOBLE_RUTA_MONSTERASP_$stamp.zip"

& npm run build
if($LASTEXITCODE-ne 0){throw 'Error compilando la interfaz V501.'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE-ne 0){throw 'Error publicando la API V501.'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/INSTRUCCIONES_V501_INCIDENTES_COMPLETO_DOBLE_RUTA.txt') -Destination $publish -Force

$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files|Where-Object {$_.Name-match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName.Contains('\App_Data\') -or $_.FullName.Contains('\uploads\') -or $_.FullName.Contains('\logs\')}){throw 'El paquete contiene archivos privados o datos persistentes.'}

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
  foreach($file in $files){
    foreach($prefix in @('','wwwroot/')){
      $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
      $entry=$archive.GetEntry($name)
      if(!$entry){throw "Falta $name"}
      $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
      try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}
      finally{$stream.Dispose();$sha.Dispose()}
      if($actual-ne(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}
    }
  }
  foreach($prefix in @('','wwwroot/')){
    foreach($required in @('web.config','RegistroAgencias.SqlServer.V501.dll','RegistroAgencias.SqlServer.V501.exe','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js','INSTRUCCIONES_V501_INCIDENTES_COMPLETO_DOBLE_RUTA.txt')){
      if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}
    }
  }
}finally{$archive.Dispose()}

$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zip+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zip))`r`n",[Text.Encoding]::ASCII)
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item -LiteralPath $zip).Length;Files=$files.Count*2;SHA256=$hash}|ConvertTo-Json
