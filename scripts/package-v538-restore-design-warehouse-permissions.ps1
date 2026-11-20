$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));Set-Location $root
$base=Get-ChildItem (Join-Path $root 'outputs') -Directory|Where-Object Name -Like '_publish_v536_v520_targeted_*'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
$backend=Get-ChildItem (Join-Path $root 'outputs') -Directory|Where-Object Name -Like '_publish_v537_warehouse_permissions_*'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
if(!$base){throw 'No se encontró la interfaz exacta V536/V520.'}
if(!$backend){throw 'No se encontró la DLL V537 validada.'}
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$stage=[IO.Path]::GetFullPath((Join-Path $root "outputs/_publish_v538_restore_design_$stamp"))
$zip=[IO.Path]::GetFullPath((Join-Path $root "outputs/V538_CORRECTIVO_RESTAURA_DISENO_EXACTO_V536_PERMISOS_ALMACEN_DOBLE_RUTA_MONSTERASP_$stamp.zip"))
New-Item -ItemType Directory -Path $stage|Out-Null

# La interfaz se copia byte por byte desde la publicación V536 que tenía el usuario.
Copy-Item -LiteralPath (Join-Path $base.FullName 'wwwroot') -Destination $stage -Recurse -Force

# Solo se toman los binarios del servidor nuevo. No se copia su interfaz ni configuración.
Get-ChildItem -LiteralPath $backend.FullName -File|Where-Object{$_.Name -notmatch '^web\.config$|^appsettings|^\.env|^app_offline\.htm$'}|ForEach-Object{
  Copy-Item -LiteralPath $_.FullName -Destination $stage -Force
}
Copy-Item -LiteralPath (Join-Path $root 'docs/INSTRUCCIONES_V538_RESTAURAR_DISENO_PERMISOS_ALMACEN.txt') -Destination $stage -Force

# Verificación estricta: todos los archivos web deben ser idénticos a V536.
$baseWeb=[IO.Path]::GetFullPath((Join-Path $base.FullName 'wwwroot'))
$stageWeb=[IO.Path]::GetFullPath((Join-Path $stage 'wwwroot'))
$baseFiles=@(Get-ChildItem $baseWeb -Recurse -File|ForEach-Object{$rel=$_.FullName.Substring($baseWeb.Length+1);[pscustomobject]@{Rel=$rel;Hash=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}}|Sort-Object Rel)
$stageFiles=@(Get-ChildItem $stageWeb -Recurse -File|ForEach-Object{$rel=$_.FullName.Substring($stageWeb.Length+1);[pscustomobject]@{Rel=$rel;Hash=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}}|Sort-Object Rel)
if(($baseFiles|ConvertTo-Json -Compress)-ne($stageFiles|ConvertTo-Json -Compress)){throw 'La interfaz V538 no coincide exactamente con V536.'}

$files=@(Get-ChildItem $stage -Recurse -File)
if($files|Where-Object{$_.Name-match '^\.env|^appsettings|^app_offline\.htm$|^web\.config$' -or $_.FullName -match '\\(App_Data|uploads|logs|Data)\\'}){throw 'Se detectó configuración o datos privados.'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($prefix in @('','wwwroot/')){foreach($file in $files){$name=$prefix+$file.FullName.Substring($stage.Length+1).Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)}}}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
  $names=@($archive.Entries.FullName)
  if(($names|Select-Object -Unique).Count-ne$names.Count){throw 'Entradas duplicadas.'}
  if($names|Where-Object{$_-match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm|web\.config)$|(^|/)(App_Data|uploads|logs|Data)/'}){throw 'El ZIP contiene archivos protegidos.'}
  foreach($prefix in @('','wwwroot/')){foreach($required in @('RegistroAgencias.SqlServer.V536.dll','wwwroot/index.html','wwwroot/version-web.json','INSTRUCCIONES_V538_RESTAURAR_DISENO_PERMISOS_ALMACEN.txt')){if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}}}
}finally{$archive.Dispose()}

$hash=(Get-FileHash $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zip+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zip))`r`n",[Text.Encoding]::ASCII)
[pscustomobject]@{Zip=$zip;Bytes=(Get-Item $zip).Length;Files=$files.Count*2;SHA256=$hash;WebDesign='Exacto V536/V520';Preserves=@('web.config','App_Data','uploads','logs','Data','appsettings','.env')}|ConvertTo-Json
