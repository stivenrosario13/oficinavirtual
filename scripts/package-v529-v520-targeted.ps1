$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));Set-Location $root
$base=Get-ChildItem (Join-Path $root 'outputs') -Directory | Where-Object Name -Match '^_publish_v520_revision_integral_' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if(!$base){throw 'No se encontró la publicación original V520.'}
$stamp=Get-Date -Format 'yyyyMMddHHmmss';$publish=[IO.Path]::GetFullPath((Join-Path $root "outputs/_publish_v536_v520_targeted_$stamp"));$zip=Join-Path $root "outputs/V536_BASE_EXACTA_V520_PDF_PREGUNTA_RESPUESTA_RECUPERACION_DATOS_HISTORICOS_LOGOS_DOBLE_RUTA_MONSTERASP_$stamp.zip"
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained;if($LASTEXITCODE-ne 0){throw 'Error publicando V536.'}
$outputs=[IO.Path]::GetFullPath((Join-Path $root 'outputs'))+[IO.Path]::DirectorySeparatorChar
if(!$publish.StartsWith($outputs,[StringComparison]::OrdinalIgnoreCase)-or !(Split-Path $publish -Leaf).StartsWith('_publish_v536_')){throw 'Ruta de publicación V536 no válida.'}
$web=Join-Path $publish 'wwwroot';if(Test-Path $web){Remove-Item -LiteralPath $web -Recurse -Force};New-Item -ItemType Directory -Path $web|Out-Null
Copy-Item -Path (Join-Path $base.FullName 'wwwroot/*') -Destination $web -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'public/v520-targeted-enhancements.js') -Destination $web -Force
Copy-Item -LiteralPath (Join-Path $root 'public/v520-targeted-enhancements.css') -Destination $web -Force
Copy-Item -LiteralPath (Join-Path $root 'public/v520-findings-pdf.js') -Destination $web -Force
Copy-Item -LiteralPath (Join-Path $root 'node_modules/jspdf/dist/jspdf.umd.min.js') -Destination (Join-Path $web 'v520-jspdf.umd.min.js') -Force
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/INSTRUCCIONES_V536_V520_RECUPERACION_ORIGEN_HALLAZGOS.txt') -Destination $publish -Force
$release='V536-WEB-20261116-2120';$index=Join-Path $web 'index.html';$html=[IO.File]::ReadAllText($index)
$html=[Text.RegularExpressions.Regex]::Replace($html,'<meta name="app-release" content="[^"]+"\s*/>','<meta name="app-release" content="'+$release+'" />')
$html=[Text.RegularExpressions.Regex]::Replace($html,'release-guard\.js\?v=[^"'']+','release-guard.js?v='+$release)
$html=$html.Replace('</head>','  <link rel="stylesheet" href="/v520-targeted-enhancements.css?v='+$release+'" />' + "`r`n" + '  <script defer src="/v520-jspdf.umd.min.js?v='+$release+'"></script>' + "`r`n" + '  <script defer src="/v520-findings-pdf.js?v='+$release+'"></script>' + "`r`n" + '  <script defer src="/v520-targeted-enhancements.js?v='+$release+'"></script>' + "`r`n" + '</head>')
[IO.File]::WriteAllText($index,$html,[Text.UTF8Encoding]::new($false))
$guard=Join-Path $web 'release-guard.js';$guardText=[IO.File]::ReadAllText($guard);$guardText=[Text.RegularExpressions.Regex]::Replace($guardText,'V520-WEB-[0-9-]+',$release);[IO.File]::WriteAllText($guard,$guardText,[Text.UTF8Encoding]::new($false))
$versionFile=Join-Path $web 'version-web.json';$version=Get-Content $versionFile -Raw|ConvertFrom-Json;$version.release=$release;$version.apiRelease=$release;$version.builtAt=(Get-Date).ToUniversalTime().ToString('o');$version.changes=@('v520-historical-findings-origin-client-recovery-v536-20261116')+@($version.changes);$version.cacheGuard='synchronized-v520-targeted-v536-20261116';$version|ConvertTo-Json -Depth 30|Set-Content $versionFile -Encoding utf8
$files=@(Get-ChildItem -LiteralPath $publish -Recurse -File)
if($files|Where-Object {$_.Name-match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName.Contains('\App_Data\') -or $_.FullName.Contains('\uploads\') -or $_.FullName.Contains('\logs\')}){throw 'Archivos privados encontrados.'}
Add-Type -AssemblyName System.IO.Compression;Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create);try{foreach($prefix in @('','wwwroot/')){foreach($file in $files){$name=$prefix+$file.FullName.Substring($publish.Length+1).Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)}}}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($zip);try{$names=@($archive.Entries.FullName);if(($names|Select-Object -Unique).Count-ne$names.Count){throw 'Entradas duplicadas.'};foreach($prefix in @('','wwwroot/')){foreach($required in @('web.config','RegistroAgencias.SqlServer.V536.dll','RegistroAgencias.SqlServer.V536.exe','wwwroot/index.html','wwwroot/version-web.json','wwwroot/v520-targeted-enhancements.js','wwwroot/v520-targeted-enhancements.css','wwwroot/v520-findings-pdf.js','wwwroot/v520-jspdf.umd.min.js','INSTRUCCIONES_V536_V520_RECUPERACION_ORIGEN_HALLAZGOS.txt')){if(!$archive.GetEntry($prefix+$required)){throw "Falta $prefix$required"}}}}finally{$archive.Dispose()}
$hash=(Get-FileHash $zip -Algorithm SHA256).Hash;[IO.File]::WriteAllText($zip+'.sha256.txt',"$hash  $([IO.Path]::GetFileName($zip))`r`n",[Text.Encoding]::ASCII);[pscustomobject]@{Zip=$zip;Base=$base.FullName;Bytes=(Get-Item $zip).Length;Files=$files.Count*2;SHA256=$hash}|ConvertTo-Json
