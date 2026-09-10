$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$output=Join-Path $root "deploy\V132_SOLO_FRONTEND_NOTIFICACIONES"
$webRoot=Join-Path $root "api\wwwroot"
New-Item -ItemType Directory -Force -Path $output|Out-Null
Copy-Item (Join-Path $webRoot "index.html") (Join-Path $output "02_RENOMBRAR_COMO_INDEX_HTML.txt") -Force
$zip=Join-Path $output "01_SUBIR_SOLO_ASSETS.zip"
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[System.IO.Compression.ZipFile]::Open($zip,[System.IO.Compression.ZipArchiveMode]::Create)
try{
 @("index-BDr0IgaU.css","purify.es-DP5U8-sc.js","index.es-DM0kcwho.js","html2canvas.esm-QH1iLAAe.js","jspdf.es.min-DVtF6mZk.js","index-BAdrjo13.js")|ForEach-Object{[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $webRoot "assets\$_"),"wwwroot/assets/$_",[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
}finally{$archive.Dispose()}
Get-ChildItem -LiteralPath $output|Select-Object Name,Length
