$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "deploy\V127_TECNICO_GPS_OBLIGATORIO"
$publish = Join-Path $root "artifacts\V127_PUBLISH"
$webRoot = Join-Path $root "api\wwwroot"
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item (Join-Path $root "api\web.config") (Join-Path $output "02_RENOMBRAR_COMO_WEB_CONFIG_V127.txt") -Force
Copy-Item (Join-Path $webRoot "index.html") (Join-Path $output "03_RENOMBRAR_COMO_INDEX_HTML_V127.txt") -Force
$zip = Join-Path $output "01_SUBIR_ARCHIVOS_V127.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    @("RegistroAgencias.SqlServer.V127.dll", "RegistroAgencias.SqlServer.V127.deps.json", "RegistroAgencias.SqlServer.V127.runtimeconfig.json", "RegistroAgencias.SqlServer.V127.staticwebassets.endpoints.json") | ForEach-Object {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $publish $_), $_, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    @("index-EKHEoiAe.css", "purify.es-DP5U8-sc.js", "index.es-DTI9Cky1.js", "html2canvas.esm-QH1iLAAe.js", "jspdf.es.min-B81nVzfV.js", "index-D_v-SgKZ.js") | ForEach-Object {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $webRoot "assets\$_"), "wwwroot/assets/$_", [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Get-ChildItem -LiteralPath $output | Select-Object Name, Length
