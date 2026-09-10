$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot ".package-v262-double-route"))
$publishRoot = Join-Path $stageRoot "publish"
$packageRoot = Join-Path $stageRoot "package"
$destinationZip = Join-Path $artifactRoot "V262_AUDITORIA_FOTOS_SIN_CADUCIDAD_MONSTER.zip"
$instructions = Join-Path $projectRoot "deploy\LEEME_V262_AUDITORIA_FOTOS_SIN_CADUCIDAD.txt"
$sourceWebConfig = Join-Path $projectRoot "api\web.config"

if (-not $stageRoot.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta temporal quedó fuera del directorio de artefactos."
}
if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot, $packageRoot -Force | Out-Null

Set-Location $projectRoot
Write-Host "1/4 Compilando la interfaz V262..." -ForegroundColor Cyan
& npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "La compilación del frontend falló." }

Write-Host "2/4 Publicando el backend V262..." -ForegroundColor Cyan
& dotnet publish "api\RegistroAgencias.Api.csproj" -c Release -o $publishRoot --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "La publicación del backend falló." }

Write-Host "3/4 Creando las dos ubicaciones compatibles..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $publishRoot "*") -Destination $packageRoot -Recurse -Force
$secondApplicationRoot = Join-Path $packageRoot "wwwroot"
Copy-Item -Path (Join-Path $publishRoot "*") -Destination $secondApplicationRoot -Recurse -Force
Copy-Item -LiteralPath $sourceWebConfig -Destination (Join-Path $packageRoot "web.config") -Force
Copy-Item -LiteralPath $sourceWebConfig -Destination (Join-Path $secondApplicationRoot "web.config") -Force
Copy-Item -LiteralPath $instructions -Destination (Join-Path $packageRoot "LEEME_V262_AUDITORIA_FOTOS_SIN_CADUCIDAD.txt") -Force
Copy-Item -LiteralPath $instructions -Destination (Join-Path $secondApplicationRoot "LEEME_V262_AUDITORIA_FOTOS_SIN_CADUCIDAD.txt") -Force

Write-Host "4/4 Generando ZIP estándar compatible con Monster..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $destinationZip) { Remove-Item -LiteralPath $destinationZip -Force }
$archive = [System.IO.Compression.ZipFile]::Open($destinationZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $packageRoot -Recurse -Directory | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($packageRoot.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        if ($relative) { [void]$archive.CreateEntry("$relative/") }
    }
    $files = Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object @{ Expression = {
        if ($_.Name -ieq "web.config") { 2 } elseif ($_.Name -like "LEEME*") { 3 } else { 1 }
    } }, FullName
    foreach ($file in $files) {
        $entryName = $file.FullName.Substring($packageRoot.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally { $archive.Dispose() }

Remove-Item -LiteralPath $stageRoot -Recurse -Force
Write-Host "Paquete V262 listo: $destinationZip" -ForegroundColor Green
