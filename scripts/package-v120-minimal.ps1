$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$inside = Join-Path $root "artifacts\ENTREGA_MONSTER\02_DENTRO_DE_WWWROOT"
$staging = Join-Path $root "artifacts\V120_MINIMO_TEMP"
$destination = Join-Path $root "artifacts\SUBIR_V120_MINIMO_V2_SIN_FAVICON.zip"

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging | Out-Null

@(
    "RegistroAgencias.SqlServer.V120.dll",
    "RegistroAgencias.SqlServer.V120.deps.json",
    "RegistroAgencias.SqlServer.V120.runtimeconfig.json",
    "RegistroAgencias.SqlServer.V120.staticwebassets.endpoints.json",
    "web.config"
) | ForEach-Object {
    Copy-Item -LiteralPath (Join-Path $inside $_) -Destination $staging
}
$staticRoot = Join-Path $staging "wwwroot"
New-Item -ItemType Directory -Path $staticRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $inside "wwwroot\index.html") -Destination $staticRoot
Copy-Item -LiteralPath (Join-Path $inside "wwwroot\assets") -Destination $staticRoot -Recurse

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $destination, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item -LiteralPath $staging -Recurse -Force
Write-Host $destination
