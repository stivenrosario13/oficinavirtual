$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v428_ftp_safe_$stamp"
$zip = Join-Path $root "outputs/ACTUALIZACION_V428_FTP_SEGURO_$stamp.zip"

& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Error publicando API' }
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
Copy-Item -LiteralPath (Join-Path $root 'outputs/INSTRUCCIONES_V428_FTP_SEGURO.txt') -Destination (Join-Path $publish 'INSTRUCCIONES_V428_FTP_SEGURO.txt') -Force
$files = @(Get-ChildItem -LiteralPath $publish -Recurse -File)
if ($files | Where-Object { $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\' }) { throw 'Archivos privados en paquete' }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $name = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
    }
} finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($file in $files) {
        $name = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
        $entry = $archive.GetEntry($name)
        if (-not $entry) { throw "Falta $name" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if ($actual -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw "Copia incorrecta: $name" }
    }
    if ($archive.GetEntry('wwwroot/BouncyCastle.Crypto.dll')) { throw 'DLL duplicada bajo wwwroot' }
    foreach ($name in @('BouncyCastle.Crypto.dll', 'RegistroAgencias.SqlServer.V428.dll', 'web.config', 'wwwroot/index.html', 'wwwroot/version-web.json', 'wwwroot/release-guard.js')) {
        if (-not $archive.GetEntry($name)) { throw "Falta $name" }
    }
    Write-Host "VERIFICADO V428 FTP SEGURO: $($archive.Entries.Count) archivos, una sola ruta, hashes identicos"
} finally { $archive.Dispose() }
Get-Item -LiteralPath $zip | Select-Object FullName, Length
