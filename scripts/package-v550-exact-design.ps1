$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$base=Join-Path $root 'outputs/_stage_v548_original_design_forms_20261119205500'
$stamp=Get-Date -Format 'yyyyMMddHHmmss'
$stage=Join-Path $root "outputs/_stage_v550_exact_design_$stamp"
$zip=Join-Path $root "outputs/V550_DISENO_ORIGINAL_ALMACEN_AGENCIAS_Y_TRES_FORMULARIOS_MONSTERASP_$stamp.zip"
$release="V550-WEB-$stamp"
Copy-Item -LiteralPath $base -Destination $stage -Recurse
$baseWeb=Join-Path $base 'wwwroot/wwwroot'
$currentAssets=Join-Path $root 'api/wwwroot/assets'
$newComposer=Get-ChildItem $currentAssets -Filter 'WarehouseRequestComposer-*.js'|Select-Object -First 1
$oldComposer=Get-ChildItem (Join-Path $baseWeb 'assets') -Filter 'WarehouseRequestComposer-*.js'|Select-Object -First 1
$oldText=[IO.File]::ReadAllText($oldComposer.FullName)
$newText=[IO.File]::ReadAllText($newComposer.FullName)
$oldImports=@([regex]::Matches($oldText,'"\./([^"/]+\.js)"')|ForEach-Object{$_.Groups[1].Value}|Select-Object -Unique)
foreach($match in @([regex]::Matches($newText,'"\./([^"/]+\.js)"'))){
 $name=$match.Groups[1].Value
 $prefix=[regex]::Replace($name,'-[^-]+\.js$','')
 # Hashes may contain hyphens; use the known module names from the old imports.
 $candidate=@($oldImports|Where-Object{$_ -like ($name.Substring(0,$name.LastIndexOf('-'))+'-*')})
 if(!$candidate.Count){$candidate=@($oldImports|Where-Object{($_ -replace '-[A-Za-z0-9_-]{8}\.js$','') -eq ($name -replace '-[A-Za-z0-9_-]{8}\.js$','')})}
 if($candidate.Count-ne 1){throw "No se pudo enlazar dependencia $name"}
 $newText=$newText.Replace('"./'+$name+'"','"./'+$candidate[0]+'"')
}
foreach($web in @((Join-Path $stage 'wwwroot'),(Join-Path $stage 'wwwroot/wwwroot'))){
 [IO.File]::WriteAllText((Join-Path $web ('assets/'+$oldComposer.Name)),$newText,[Text.UTF8Encoding]::new($false))
 # Explicit query versions bypass immutable browser caches for the preserved bundle graph.
 foreach($file in Get-ChildItem (Join-Path $web 'assets') -Filter '*.js'){
  $text=[IO.File]::ReadAllText($file.FullName)
  $text=[regex]::Replace($text,'(["''])([^"''\s]+\.js)\1',('$1$2?v='+$release+'$1'))
  [IO.File]::WriteAllText($file.FullName,$text,[Text.UTF8Encoding]::new($false))
 }
 $index=Join-Path $web 'index.html';$html=[IO.File]::ReadAllText($index)
 $html=[regex]::Replace($html,'(<meta name="app-release" content=")[^"]+','$1'+$release)
 $html=[regex]::Replace($html,'(/assets/[^" ]+\.js)(?:\?[^" ]*)?',('$1?v='+$release))
 $html=[regex]::Replace($html,'release-guard\.js\?v=[^"]+',('release-guard.js?v='+$release))
 [IO.File]::WriteAllText($index,$html,[Text.UTF8Encoding]::new($false))
 $versionPath=Join-Path $web 'version-web.json';$version=Get-Content $versionPath -Raw|ConvertFrom-Json
 $version.release=$release;$version.cacheGuard=$release
 $version.changes=@('v550-administrative-warehouse-agency-multi-department-preserve-v548-three-forms')+@($version.changes)
 $version|ConvertTo-Json -Depth 30|Set-Content $versionPath -Encoding utf8
}
# Require all styles and backend files to remain byte-for-byte identical to V548.
foreach($file in Get-ChildItem $base -Recurse -File|Where-Object{$_.Extension -in @('.css','.dll','.exe','.json') -and $_.Name-ne 'version-web.json'}){
 $target=Join-Path $stage $file.FullName.Substring($base.Length+1)
 if((Get-FileHash $target).Hash-ne(Get-FileHash $file.FullName).Hash){throw "Cambio fuera de alcance: $target"}
}
$forms=Get-ChildItem (Join-Path $stage 'wwwroot/wwwroot/assets') -Filter 'AdministrativeRequestComposer-*.js'|Select-Object -First 1
$formText=[IO.File]::ReadAllText($forms.FullName)
foreach($marker in @('Solicitud de pago de prestaciones en cheque','Entrega de cheques en blanco a Contabilidad','Entrega de cheques firmados de prestaciones a Caja')){if(!$formText.Contains($marker)){throw "Falta formulario: $marker"}}
$files=@(Get-ChildItem $stage -Recurse -File)
if($files|Where-Object{$_.Name-match '^web\.config$|^appsettings|^\.env|^app_offline' -or $_.FullName-match '\\(Data|App_Data|uploads|logs)\\'}){throw 'Archivo protegido detectado'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
[pscustomobject]@{Zip=$zip;Stage=$stage;Bytes=(Get-Item $zip).Length;SHA256=(Get-FileHash $zip -Algorithm SHA256).Hash;Design='CSS exacto V548';Forms='Tres formularios incluidos';Backend='Idéntico V548'}|ConvertTo-Json
