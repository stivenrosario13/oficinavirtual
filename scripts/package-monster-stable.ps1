$ErrorActionPreference='Stop'
function Get-StableSha256([string]$file){
 $stream=[IO.File]::OpenRead($file)
 $sha=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$reference=Join-Path $root 'release-reference/v558'
$web=Join-Path $reference 'wwwroot'
$manifest=Get-Content (Join-Path $reference 'manifest.json') -Raw|ConvertFrom-Json
$outputs=Join-Path $root 'outputs'
New-Item -ItemType Directory -Path $outputs -Force|Out-Null
foreach($entry in $manifest.files){
 $file=Join-Path $web $entry.path
 if(!(Test-Path -LiteralPath $file)-or(Get-StableSha256 $file)-ne$entry.sha256){throw "Referencia modificada o incompleta: $($entry.path)"}
}
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$zip=Join-Path $outputs "V558_INTERFAZ_ORIGINAL_ESTABLE_$stamp.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,'Create')
try{
 foreach($prefix in @('','wwwroot/')){
  foreach($entry in $manifest.files){
   $name=$prefix+'wwwroot/'+$entry.path.Replace('\','/')
   if($name-match '(^|/)(web\.config|appsettings[^/]*|\.env[^/]*|app_offline\.htm)$|(^|/)(Data|App_Data|uploads|logs)/|\.(dll|exe)$'){throw "Archivo protegido: $name"}
   [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $web $entry.path),$name,'Optimal')
  }
 }
}finally{$archive.Dispose()}
$descriptor=Get-Content (Join-Path $reference 'qa.json') -Raw|ConvertFrom-Json
$descriptor.stage=$reference
$descriptorJson=$descriptor|ConvertTo-Json -Depth 30
[IO.File]::WriteAllText((Join-Path $outputs 'v558-package-result.json'),$descriptorJson,[Text.UTF8Encoding]::new($false))
[pscustomobject]@{Zip=$zip;Files=$manifest.files.Count*2;SHA256=(Get-StableSha256 $zip);Release=$descriptor.release}|ConvertTo-Json
