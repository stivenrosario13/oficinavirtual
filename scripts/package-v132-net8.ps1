$ErrorActionPreference="Stop"
$root=Split-Path -Parent $PSScriptRoot
$output=Join-Path $root "deploy\V132_NET8_MONSTER"
$publish=Join-Path $root "artifacts\V132_NET8"
New-Item -ItemType Directory -Force -Path $output|Out-Null
Copy-Item (Join-Path $root "api\web.config") (Join-Path $output "02_RENOMBRAR_COMO_WEB_CONFIG_NET8.txt") -Force
Copy-Item (Join-Path $root "api\wwwroot\index.html") (Join-Path $output "03_RENOMBRAR_COMO_INDEX_HTML_V132.txt") -Force
$zip=Join-Path $output "01_SUBIR_BACKEND_NET8_V132.zip"
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[System.IO.Compression.ZipFile]::Open($zip,[System.IO.Compression.ZipArchiveMode]::Create)
try{
 @("RegistroAgencias.SqlServer.V132.Net8.dll","RegistroAgencias.SqlServer.V132.Net8.deps.json","RegistroAgencias.SqlServer.V132.Net8.runtimeconfig.json","RegistroAgencias.SqlServer.V132.Net8.staticwebassets.endpoints.json")|ForEach-Object{[System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $publish $_),$_,[System.IO.Compression.CompressionLevel]::Optimal)|Out-Null}
}finally{$archive.Dispose()}
Get-ChildItem -LiteralPath $output|Select-Object Name,Length
