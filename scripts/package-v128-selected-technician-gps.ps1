$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "deploy\V128_FICHA_TECNICO_GPS"
$publish = Join-Path $root "artifacts\V128_PUBLISH"
$webRoot = Join-Path $root "api\wwwroot"
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item (Join-Path $root "api\web.config") (Join-Path $output "02_RENOMBRAR_COMO_WEB_CONFIG_V128.txt") -Force
Copy-Item (Join-Path $webRoot "index.html") (Join-Path $output "03_RENOMBRAR_COMO_INDEX_HTML_V128.txt") -Force
$zip = Join-Path $output "01_SUBIR_ARCHIVOS_V128.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    @("RegistroAgencias.SqlServer.V128.dll", "RegistroAgencias.SqlServer.V128.deps.json", "RegistroAgencias.SqlServer.V128.runtimeconfig.json", "RegistroAgencias.SqlServer.V128.staticwebassets.endpoints.json") | ForEach-Object {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $publish $_), $_, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    @("index-EKHEoiAe.css", "purify.es-DP5U8-sc.js", "index.es-DrFD7lzL.js", "html2canvas.esm-QH1iLAAe.js", "jspdf.es.min-BVwDJUV7.js", "index-BmVlbuEu.js") | ForEach-Object {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $webRoot "assets\$_"), "wwwroot/assets/$_", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Get-ChildItem -LiteralPath $output | Select-Object Name, Length
