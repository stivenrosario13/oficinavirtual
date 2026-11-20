$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v427_v418_format_$stamp"
$zip = Join-Path $root "outputs/ACTUALIZACION_COMPLETA_V427_FORMATO_V418_$stamp.zip"

& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Error publicando API' }
Copy-Item -LiteralPath (Join-Path $root 'api/web.config') -Destination (Join-Path $publish 'web.config') -Force
$files = @(Get-ChildItem -LiteralPath $publish -Recurse -File)
if ($files | Where-Object { $_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|logs|node_modules)\\' }) { throw 'Archivos privados en paquete' }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
        foreach ($name in @($relative, "wwwroot/$relative")) {
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal)
        }
    }
} finally { $archive.Dispose() }

$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($publish.Length + 1).Replace('\', '/')
        $expected = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        foreach ($name in @($relative, "wwwroot/$relative")) {
            $entry = $archive.GetEntry($name)
            if (-not $entry) { throw "Falta $name" }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($actual -ne $expected) { throw "Copia incorrecta: $name" }
        }
    }
    foreach ($name in @('web.config', 'wwwroot/web.config', 'wwwroot/index.html', 'wwwroot/wwwroot/index.html', 'RegistroAgencias.SqlServer.V427.dll', 'wwwroot/RegistroAgencias.SqlServer.V427.dll')) {
        if (-not $archive.GetEntry($name)) { throw "Falta el archivo esperado: $name" }
    }
    Write-Host "VERIFICADO V427 FORMATO V418: $($archive.Entries.Count) archivos, dos rutas completas, hashes identicos"
} finally { $archive.Dispose() }
Get-Item -LiteralPath $zip | Select-Object FullName, Length
