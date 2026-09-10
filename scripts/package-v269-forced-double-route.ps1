$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$outputsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "outputs"))
$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot ".package-v269-forced-double-route"))
$publishRoot = Join-Path $stageRoot "publish"
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $outputsRoot "V269_FORZADO_DOBLE_RUTA"))
$packageRoot = Join-Path $outputRoot "paquete"
$destinationZip = Join-Path $outputRoot "FORZAR_VERSION_V269_DOBLE_RUTA.zip"
$instructions = Join-Path $projectRoot "deploy\LEEME_V269_EVIDENCIA_SUPERVISOR.txt"
$offlineMarker = Join-Path $projectRoot "deploy\app_offline.v269.nuevo"
$sourceWebConfig = Join-Path $projectRoot "api\web.config"

if (-not $stageRoot.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta temporal quedó fuera del directorio de artefactos."
}
if (-not $outputRoot.StartsWith($outputsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "La salida quedó fuera del directorio de paquetes."
}
if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot, $packageRoot -Force | Out-Null

Set-Location $projectRoot
Write-Host "1/5 Compilando la interfaz V269..." -ForegroundColor Cyan
& npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "La compilación del frontend falló." }

Write-Host "2/5 Publicando el backend V269..." -ForegroundColor Cyan
& dotnet publish "api\RegistroAgencias.Api.csproj" -c Release -o $publishRoot --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "La publicación del backend falló." }

Write-Host "3/5 Creando las dos rutas compatibles..." -ForegroundColor Cyan
Copy-Item -Path (Join-Path $publishRoot "*") -Destination $packageRoot -Recurse -Force
$secondApplicationRoot = Join-Path $packageRoot "wwwroot"
Copy-Item -Path (Join-Path $publishRoot "*") -Destination $secondApplicationRoot -Recurse -Force

Copy-Item -LiteralPath $sourceWebConfig -Destination (Join-Path $packageRoot "web.config") -Force
Copy-Item -LiteralPath $sourceWebConfig -Destination (Join-Path $secondApplicationRoot "web.config") -Force
Copy-Item -LiteralPath $instructions -Destination (Join-Path $packageRoot "LEEME_V269_EVIDENCIA_SUPERVISOR.txt") -Force
Copy-Item -LiteralPath $instructions -Destination (Join-Path $secondApplicationRoot "LEEME_V269_EVIDENCIA_SUPERVISOR.txt") -Force
Copy-Item -LiteralPath $offlineMarker -Destination (Join-Path $packageRoot "app_offline.v269.nuevo") -Force
Copy-Item -LiteralPath $offlineMarker -Destination (Join-Path $secondApplicationRoot "app_offline.v269.nuevo") -Force
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $outputRoot "crear_zip_forzado.ps1") -Force

Write-Host "4/5 Generando ZIP estándar compatible con Monster..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($destinationZip,[System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $packageRoot -Recurse -Directory | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($packageRoot.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        if ($relative) { [void]$archive.CreateEntry("$relative/") }
    }
    $files = Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Sort-Object @{ Expression = {
        if ($_.Name -ieq "web.config") { 2 } elseif ($_.Name -like "LEEME*" -or $_.Name -like "app_offline*") { 3 } else { 1 }
    } }, FullName
    foreach ($file in $files) {
        $entryName = $file.FullName.Substring($packageRoot.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entryName,[System.IO.Compression.CompressionLevel]::Optimal)
    }
}
finally { $archive.Dispose() }

Write-Host "5/5 Verificando contenido esencial..." -ForegroundColor Cyan
$required = @(
    "RegistroAgencias.SqlServer.V269.exe",
    "web.config",
    "wwwroot/index.html",
    "wwwroot/documents/conduce-taller-grupo-samana.pdf",
    "wwwroot/documents/comunicado-averias.pdf",
    "wwwroot/documents/manual-normas-politicas-procedimientos-almacen.pdf"
)
$zip = [System.IO.Compression.ZipFile]::OpenRead($destinationZip)
try {
    $entryNames = @($zip.Entries | ForEach-Object { $_.FullName })
    $missing = @($required | Where-Object { $_ -notin $entryNames })
    if ($missing.Count -gt 0) { throw "Faltan archivos esenciales en el ZIP: $($missing -join ', ')" }
}
finally { $zip.Dispose() }

Remove-Item -LiteralPath $stageRoot -Recurse -Force
Get-Item -LiteralPath $destinationZip | Select-Object FullName, Length, LastWriteTime
Get-FileHash -LiteralPath $destinationZip -Algorithm SHA256
Write-Host "Paquete V269 listo: $destinationZip" -ForegroundColor Green

