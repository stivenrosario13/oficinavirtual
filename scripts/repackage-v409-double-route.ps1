$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Join-Path $root 'outputs/ACTUALIZACION_V409_CONFIRMAR_MONSTER_20260924-153300.zip'
$readme=Join-Path $root 'outputs/LEEME_ACTUALIZACION_V409_DOBLE_RUTA.txt'
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$destination=Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V409_CONFIRMAR_FORMATO_V405_$stamp.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$inputArchive=[IO.Compression.ZipFile]::OpenRead($source)
$outputArchive=[IO.Compression.ZipFile]::Open($destination,[IO.Compression.ZipArchiveMode]::Create)
try {
 $files=@($inputArchive.Entries | Where-Object {$_.Name})
 $directories=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($prefix in @('','wwwroot/')){
  foreach($entry in $files){
   $name=$prefix+$entry.FullName.Replace('\','/')
   if($name -match '(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|logs|node_modules)/'){throw "Archivo excluido: $name"}
   $parent=$name
   while($parent.Contains('/')){$parent=$parent.Substring(0,$parent.LastIndexOf('/'));[void]$directories.Add($parent+'/')}
   $created=$outputArchive.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
   $from=$entry.Open();$to=$created.Open()
   try {$from.CopyTo($to)} finally {$from.Dispose();$to.Dispose()}
  }
 }
 foreach($directory in ($directories | Sort-Object)){[void]$outputArchive.CreateEntry($directory)}
 [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($outputArchive,$readme,'LEEME_ACTUALIZACION_V409.txt',[IO.Compression.CompressionLevel]::Optimal)
} finally {$outputArchive.Dispose();$inputArchive.Dispose()}

function EntryHash($entry){$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{[Convert]::ToHexString($sha.ComputeHash($stream))}finally{$stream.Dispose();$sha.Dispose()}}
$inputArchive=[IO.Compression.ZipFile]::OpenRead($source)
$outputArchive=[IO.Compression.ZipFile]::OpenRead($destination)
try {
 $names=@($outputArchive.Entries.FullName)
 if(($names | Select-Object -Unique).Count -ne $names.Count){throw 'Entradas duplicadas'}
 foreach($entry in ($inputArchive.Entries | Where-Object {$_.Name})){
  $expected=EntryHash $entry
  foreach($prefix in @('','wwwroot/')){
   $copy=$outputArchive.GetEntry($prefix+$entry.FullName.Replace('\','/'))
   if(!$copy -or (EntryHash $copy) -ne $expected){throw "Copia incorrecta: $prefix$($entry.FullName)"}
  }
 }
 foreach($prefix in @('','wwwroot/')){
  foreach($name in @('web.config','RegistroAgencias.SqlServer.V409.exe','RegistroAgencias.SqlServer.V409.dll','wwwroot/index.html','wwwroot/version-web.json')){if(!$outputArchive.GetEntry($prefix+$name)){throw "Falta $prefix$name"}}
 }
 if($names -contains 'index.html' -or $names -match 'RegistroAgencias\.SqlServer\.V(?!409)'){throw 'Formato o ejecutable incorrecto'}
 [pscustomobject]@{Zip=$destination;Bytes=(Get-Item $destination).Length;Entries=$names.Count;CopiesVerified=2;SHA256=(Get-FileHash $destination -Algorithm SHA256).Hash}|ConvertTo-Json
} finally {$outputArchive.Dispose();$inputArchive.Dispose()}
