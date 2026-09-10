$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "deploy\V129_HALLAZGOS_REPORTES"
$publish = Join-Path $root "artifacts\V129_PUBLISH"
$webRoot = Join-Path $root "api\wwwroot"
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item (Join-Path $root "api\web.config") (Join-Path $output "02_RENOMBRAR_COMO_WEB_CONFIG_V129.txt") -Force
Copy-Item (Join-Path $webRoot "index.html") (Join-Path $output "03_RENOMBRAR_COMO_INDEX_HTML_V129.txt") -Force
$zip = Join-Path $output "01_SUBIR_ARCHIVOS_V129.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    @("RegistroAgencias.SqlServer.V129.dll", "RegistroAgencias.SqlServer.V129.deps.json", "RegistroAgencias.SqlServer.V129.runtimeconfig.json", "RegistroAgencias.SqlServer.V129.staticwebassets.endpoints.json") | ForEach-Object {[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $publish $_),$_,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
    @("index-BZx8Ha5f.css", "purify.es-DP5U8-sc.js", "index.es-agBTA02c.js", "html2canvas.esm-QH1iLAAe.js", "jspdf.es.min-Cc5cQI_K.js", "index-BXq8jDJu.js") | ForEach-Object {[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $webRoot "assets\$_"),"wwwroot/assets/$_",[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
}
finally {$archive.Dispose()}
Get-ChildItem -LiteralPath $output | Select-Object Name,Length
