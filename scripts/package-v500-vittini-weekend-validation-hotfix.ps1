$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputs=Join-Path $root 'outputs'
$baseline=Get-ChildItem -LiteralPath $outputs -Filter 'ACTUALIZACION_V500_COMPLETA_PRIVADA_MONSTERASP_*.zip' |
  Where-Object {$_.Name -notmatch 'VITTINI|HOTFIX'} |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
if(!$baseline){throw 'No se encontró el paquete V500 base de Monster.'}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source=[IO.Compression.ZipFile]::OpenRead($baseline.FullName)
try{
  $prefix='wwwroot/'
  $composer=$source.Entries | Where-Object {$_.FullName -match '^wwwroot/assets/WarehouseRequestComposer-[^/]+\.js$'} | Select-Object -First 1
  if(!$composer){throw 'El paquete base no contiene WarehouseRequestComposer.'}

  function Read-Entry([IO.Compression.ZipArchiveEntry]$entry){
    $reader=[IO.StreamReader]::new($entry.Open())
    try{return $reader.ReadToEnd()}finally{$reader.Dispose()}
  }

  $sources=@{}
  foreach($entry in $source.Entries | Where-Object {$_.FullName -match '^wwwroot/assets/[^/]+\.js$'}){
    $sources[[IO.Path]::GetFileName($entry.FullName)]=Read-Entry $entry
  }
  $composerName=[IO.Path]::GetFileName($composer.FullName)
  $old=@'
if(N(""),j&&(!T||!M||!B||M<T||B<=M)){N("Indica el fin de semana y una fecha posterior para devolver los sobrantes.");return}
'@
  $replacement=@'
if(N(""),j){let loanErrorV501=!T||!M||!B?"Completa las tres fechas del préstamo.":"";const loanStartV501=new Date(`${T}T12:00:00Z`),loanEndV501=new Date(`${M}T12:00:00Z`),loanReturnV501=new Date(`${B}T12:00:00Z`),loanDaysV501=(a,s)=>(a-s)/864e5;if(!loanErrorV501){const loanSpanV501=loanDaysV501(loanEndV501,loanStartV501),returnSpanV501=loanDaysV501(loanReturnV501,loanEndV501);loanErrorV501=Number.isNaN(loanStartV501.getTime())||Number.isNaN(loanEndV501.getTime())||Number.isNaN(loanReturnV501.getTime())?"Las fechas del préstamo no son válidas.":![5,6].includes(loanStartV501.getUTCDay())||loanEndV501.getUTCDay()!==0||loanSpanV501<1||loanSpanV501>2?"El préstamo debe iniciar viernes o sábado y terminar el domingo.":returnSpanV501<1||returnSpanV501>7?"La devolución de sobrantes debe registrarse entre 1 y 7 días después del domingo.":""}if(loanErrorV501){N(loanErrorV501);return}}
'@
  if(([regex]::Matches($sources[$composerName],[regex]::Escape($old))).Count-ne 1){throw 'La validación base cambió; se canceló para no alterar código ajeno.'}
  $patchedComposer=$sources[$composerName].Replace($old,$replacement)

  $mapping=@{}
  function Hotfix-Name([string]$name){return $name.Substring(0,$name.Length-3)+'-vittini-fechas-20261107.js'}
  $mapping[$composerName]=Hotfix-Name $composerName
  $patched=@{$composerName=$patchedComposer}

  do{
    $added=$false
    foreach($name in @($sources.Keys)){
      if($mapping.ContainsKey($name)){continue}
      $content=$sources[$name]
      foreach($oldName in @($mapping.Keys)){$content=$content.Replace($oldName,$mapping[$oldName])}
      if($content-ne$sources[$name]){$mapping[$name]=Hotfix-Name $name;$patched[$name]=$content;$added=$true}
    }
  }while($added)

  foreach($name in @($mapping.Keys)){
    $content=if($name-eq$composerName){$patchedComposer}else{$sources[$name]}
    foreach($oldName in @($mapping.Keys)){$content=$content.Replace($oldName,$mapping[$oldName])}
    $patched[$name]=$content
  }

  $indexEntry=$source.GetEntry('wwwroot/index.html');if(!$indexEntry){throw 'Falta index.html en el paquete base.'}
  $index=Read-Entry $indexEntry
  foreach($oldName in @($mapping.Keys)){$index=$index.Replace($oldName,$mapping[$oldName])}
  $release='V500-HOTFIX-VITTINI-FECHAS-20261107'
  $index=[regex]::Replace($index,'V500-WEB-20261104-2010',$release)
  if($index-notmatch [regex]::Escape($release)){throw 'No se pudo activar el identificador de la corrección.'}

  $versionEntry=$source.GetEntry('wwwroot/version-web.json');if(!$versionEntry){throw 'Falta version-web.json en el paquete base.'}
  $version=Read-Entry $versionEntry
  $version=$version.Replace('V500-WEB-20261104-2010',$release)

  $stamp=Get-Date -Format 'yyyyMMddHHmmss'
  $zipPath=Join-Path $outputs "HOTFIX_V500_SOLO_VITTINI_FECHAS_MONSTER_$stamp.zip"
  $archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
  try{
    foreach($routePrefix in @('wwwroot/','wwwroot/wwwroot/')){
      foreach($oldName in @($mapping.Keys)){
        $entry=$archive.CreateEntry($routePrefix+'assets/'+$mapping[$oldName],[IO.Compression.CompressionLevel]::Optimal)
        $writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
        try{$writer.Write($patched[$oldName])}finally{$writer.Dispose()}
      }
      foreach($item in @(@('index.html',$index),@('version-web.json',$version))){
        $entry=$archive.CreateEntry($routePrefix+$item[0],[IO.Compression.CompressionLevel]::Optimal)
        $writer=[IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
        try{$writer.Write($item[1])}finally{$writer.Dispose()}
      }
    }
  }finally{$archive.Dispose()}

  $verify=[IO.Compression.ZipFile]::OpenRead($zipPath)
  try{
    $names=@($verify.Entries.FullName)
    if(($names|Select-Object -Unique).Count-ne$names.Count){throw 'El ZIP contiene rutas duplicadas.'}
    if($names|Where-Object {$_-notmatch '^wwwroot/(wwwroot/)?(assets/[^/]+\.js|index\.html|version-web\.json)$'}){throw 'El ZIP contiene archivos fuera del hotfix.'}
    foreach($routePrefix in @('wwwroot/','wwwroot/wwwroot/')){
      $hot=$verify.GetEntry($routePrefix+'assets/'+$mapping[$composerName]);if(!$hot){throw 'Falta el formulario corregido.'}
      $text=Read-Entry $hot
      foreach($marker in @('El préstamo debe iniciar viernes o sábado y terminar el domingo.','entre 1 y 7 días después del domingo.')){if(!$text.Contains($marker)){throw "Falta validación: $marker"}}
      if($text.Contains('Indica el fin de semana y una fecha posterior para devolver los sobrantes.')){throw 'La validación antigua sigue presente.'}
      $html=Read-Entry $verify.GetEntry($routePrefix+'index.html');if(!$html.Contains($release)){throw 'El navegador no activará el hotfix.'}
    }
  }finally{$verify.Dispose()}

  $hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
  [IO.File]::WriteAllText($zipPath+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zipPath))`r`n",[Text.Encoding]::ASCII)
  [pscustomobject]@{Zip=$zipPath;Bytes=(Get-Item $zipPath).Length;SHA256=$hash;Files=$mapping.Count*2+4;Baseline=$baseline.Name}|ConvertTo-Json
}finally{$source.Dispose()}
