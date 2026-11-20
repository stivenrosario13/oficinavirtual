$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$publish=Join-Path $root "outputs/_publish_v502_admin_incidentes_$stamp"
$zip=Join-Path $root "outputs/V502_ADMIN_FORMULARIOS_INCIDENTES_COMPLETO_DOBLE_RUTA_MONSTERASP_$stamp.zip"

& npm run build
if($LASTEXITCODE-ne 0){throw 'Error compilando la interfaz V502.'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE-ne 0){throw 'Error publicando la API V502.'}
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/INSTRUCCIONES_V502_ADMIN_FORMULARIOS_INCIDENTES.txt') -Destination $publish -Force

$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files|Where-Object {$_.Name-match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName.Contains('\App_Data\') -or $_.FullName.Contains('\uploads\') -or $_.FullName.Contains('\logs\')}){throw 'El paquete contiene datos privados o persistentes.'}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($prefix in @('','wwwroot/')){foreach($file in $files){$name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)}}}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
 $names=@($archive.Entries.FullName);if(($names|Select-Object -Unique).Count-ne$names.Count){throw 'Entradas duplicadas.'}
 foreach($file in $files){foreach($prefix in @('','wwwroot/')){$name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/');$entry=$archive.GetEntry($name);if(!$entry){throw "Falta $name"};$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()};if($actual-ne(Get-FileHash $file.FullName -Algorithm SHA256).Hash){throw "Copia incorrecta: $name"}}}
 foreach($prefix in @('','wwwroot/')){foreach($required in @('web.config','RegistroAgencias.SqlServer.V502.dll','RegistroAgencias.SqlServer.V502.exe','wwwroot/index.html','wwwroot/version-web.json','INSTRUCCIONES_V502_ADMIN_FORMULARIOS_INCIDENTES.txt')){if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}}}
}finally{$archive.Dispose()}
$hash=(Get-FileHash $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zip+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zip))`r`n",[Text.Encoding]::ASCII)
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item $zip).Length;Files=$files.Count*2;SHA256=$hash}|ConvertTo-Json
