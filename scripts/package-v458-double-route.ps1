param([switch]$IncludePrivateStorage,[string]$Release='V458')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$releaseLower=$Release.ToLowerInvariant()
$publish=Join-Path $root "outputs/_publish_${releaseLower}_double_route_$stamp"
$zip=Join-Path $root "outputs/ACTUALIZACION_${Release}_COMPLETA_PRIVADA_MONSTERASP_$stamp.zip"

if(!(Test-Path -LiteralPath 'api/Data/warehouse-products-2026-10-27.json')){throw 'Falta confirmar y preparar el inventario corregido.'}

& npm run build
if($LASTEXITCODE -ne 0){throw 'Error compilando interfaz'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Error publicando API'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
$instructions="INSTRUCCIONES_${Release}_DOBLE_RUTA.txt"
Copy-Item -LiteralPath (Join-Path $root "docs/$instructions") -Destination (Join-Path $publish $instructions) -Force
if($IncludePrivateStorage){
 $storageKey=$null
 foreach($line in [IO.File]::ReadLines((Join-Path $root '.env.local'))){
  if($line -match '^\s*STORAGE_KEY\s*=(.*)$'){$storageKey=$Matches[1].Trim().Trim('"',"'")}
 }
 if([string]::IsNullOrWhiteSpace($storageKey)){throw 'Falta la clave privada de almacenamiento para este paquete.'}
 $configPath=Join-Path $publish 'web.config'
 $document=[Xml.XmlDocument]::new();$document.PreserveWhitespace=$true;$document.Load($configPath)
 $asp=$document.SelectSingleNode('//aspNetCore')
 $variables=$document.CreateElement('environmentVariables');[void]$asp.AppendChild($variables)
 foreach($setting in @(@('STORAGE_KEY',$storageKey),@('STORAGE_ENABLED','true'))){
  $entry=$document.CreateElement('environmentVariable');$entry.SetAttribute('name',$setting[0]);$entry.SetAttribute('value',$setting[1]);[void]$variables.AppendChild($entry)
 }
 $document.Save($configPath);$storageKey=$null
 Write-Host 'Configuración privada de almacenamiento incluida; no compartir este paquete.'
}
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName.Contains('\App_Data\') -or $_.FullName.Contains('\uploads\') -or $_.FullName.Contains('\logs\') -or $_.FullName.Contains('\node_modules\')}){throw 'Archivos privados en paquete'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{
 $directories=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($prefix in @('','wwwroot/')){foreach($file in $files){
  $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
  [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)
  $parent=$name
  while($parent.Contains('/')){$parent=$parent.Substring(0,$parent.LastIndexOf('/'));[void]$directories.Add($parent+'/')}
 }}
 foreach($directory in ($directories | Sort-Object)){[void]$archive.CreateEntry($directory)}
}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 $names=@($archive.Entries.FullName)
 if(($names | Select-Object -Unique).Count -ne $names.Count){throw 'Entradas duplicadas'}
 foreach($file in $files){foreach($prefix in @('','wwwroot/')){
  $name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/')
  $entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}
  finally{$stream.Dispose();$sha.Dispose()}
  if($actual -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}
 }}
 foreach($prefix in @('','wwwroot/')){foreach($name in @('web.config','BouncyCastle.Crypto.dll',"RegistroAgencias.SqlServer.$Release.dll","RegistroAgencias.SqlServer.$Release.exe",'wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js',$instructions)){
  if(!$archive.GetEntry($prefix+$name)){throw "Falta $prefix$name"}
 }}
 Write-Host "VERIFICADO $Release DOBLE RUTA: $($archive.Entries.Count) entradas; dos copias identicas"
}finally{$archive.Dispose()}
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item -LiteralPath $zip).Length;SHA256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash}|ConvertTo-Json
