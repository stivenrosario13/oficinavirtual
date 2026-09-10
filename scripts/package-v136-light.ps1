$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\ENTREGA_MONSTER\02_DENTRO_DE_WWWROOT"
$target = Join-Path $root "artifacts\V155_MOVIL_REPORTES_USUARIOS_VOLVER_DENTRO_WWWROOT.zip"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
$archive = [System.IO.Compression.ZipFile]::Open($target, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $rootFiles = @(
        "RegistroAgencias.SqlServer.V155.dll",
        "RegistroAgencias.SqlServer.V155.deps.json",
        "RegistroAgencias.SqlServer.V155.runtimeconfig.json",
        "RegistroAgencias.SqlServer.V155.staticwebassets.endpoints.json",
        "web.config"
    )
    foreach ($name in $rootFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            (Join-Path $source $name),
            $name,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
    $webRoot = Join-Path $source "wwwroot"
    Get-ChildItem -LiteralPath $webRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($webRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $_.FullName,
            "wwwroot/$relative",
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

Get-Item -LiteralPath $target | Select-Object FullName, Length, LastWriteTime
