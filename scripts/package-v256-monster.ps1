$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$stageRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactRoot ".package-v256"))
$publishRoot = Join-Path $stageRoot "publish"
$backendRoot = Join-Path $stageRoot "01_backend"
$frontendRoot = Join-Path $stageRoot "02_frontend"
$activationRoot = Join-Path $stageRoot "03_activation"
$completeRoot = Join-Path $stageRoot "complete"

if (-not $stageRoot.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta temporal quedó fuera del directorio de artefactos."
}

$backendZip = Join-Path $artifactRoot "V256_01_BACKEND_NUEVO_SIN_BORRAR.zip"
$frontendZip = Join-Path $artifactRoot "V256_02_INTERFAZ_MODERNA_SIN_BORRAR.zip"
$frontendCompatibleZip = Join-Path $artifactRoot "V256_02_INTERFAZ_MONSTER_CORREGIDA.zip"
$activationZip = Join-Path $artifactRoot "V256_03_ACTIVAR_APLICACION.zip"
$completeZip = Join-Path $artifactRoot "V256_ACTUALIZACION_MONSTER_COMPLETA.zip"
$instructions = Join-Path $projectRoot "deploy\INSTRUCCIONES_ACTUALIZACION_V256.txt"

function Reset-Directory([string]$path) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($stageRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Se rechazó una limpieza fuera de la carpeta temporal V256: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function New-CompatibleZip([string]$sourceDirectory, [string]$destinationPath) {
    if (Test-Path -LiteralPath $destinationPath) {
        Remove-Item -LiteralPath $destinationPath -Force
    }
    $archive = [System.IO.Compression.ZipFile]::Open(
        $destinationPath,
        [System.IO.Compression.ZipArchiveMode]::Create
    )
    try {
        # Algunos paneles web de Monster no convierten las barras invertidas de
        # Windows. Creamos primero las carpetas y usamos siempre rutas ZIP POSIX.
        Get-ChildItem -LiteralPath $sourceDirectory -Recurse -Directory |
            Sort-Object FullName |
            ForEach-Object {
                $relative = $_.FullName.Substring($sourceDirectory.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
                if ($relative) { [void]$archive.CreateEntry("$relative/") }
            }
        Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                $entryName = $_.FullName.Substring($sourceDirectory.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
                [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive,
                    $_.FullName,
                    $entryName,
                    [System.IO.Compression.CompressionLevel]::Optimal
                )
            }
    }
    finally {
        $archive.Dispose()
    }
}

Set-Location $projectRoot
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

Reset-Directory $stageRoot
New-Item -ItemType Directory -Path $publishRoot, $backendRoot, $frontendRoot, $activationRoot, $completeRoot -Force | Out-Null

Write-Host "1/5 Validando y compilando la interfaz V256..." -ForegroundColor Cyan
& npm.cmd run lint
if ($LASTEXITCODE -ne 0) { throw "ESLint encontró errores." }
& npm.cmd test
if ($LASTEXITCODE -ne 0) { throw "Las pruebas automatizadas fallaron." }
& npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "La compilación del frontend falló." }

Write-Host "2/5 Publicando ASP.NET Core 8..." -ForegroundColor Cyan
& dotnet publish "api\RegistroAgencias.Api.csproj" -c Release -o $publishRoot --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "La publicación del backend falló." }

$backendFiles = @(
    "RegistroAgencias.SqlServer.V256.exe",
    "RegistroAgencias.SqlServer.V256.dll",
    "RegistroAgencias.SqlServer.V256.deps.json",
    "RegistroAgencias.SqlServer.V256.runtimeconfig.json"
)
foreach ($name in $backendFiles) {
    $source = Join-Path $publishRoot $name
    if (-not (Test-Path -LiteralPath $source)) { throw "Falta el archivo publicado: $name" }
    Copy-Item -LiteralPath $source -Destination $backendRoot
}
Copy-Item -LiteralPath $instructions -Destination (Join-Path $backendRoot "LEEME_PASO_1_V256.txt")

Write-Host "3/5 Preparando la interfaz para las dos rutas de Monster..." -ForegroundColor Cyan
$publishedWeb = Join-Path $publishRoot "wwwroot"
Copy-Item -Path (Join-Path $publishedWeb "*") -Destination $frontendRoot -Recurse -Force
$nestedWeb = Join-Path $frontendRoot "wwwroot"
New-Item -ItemType Directory -Path $nestedWeb -Force | Out-Null
Copy-Item -Path (Join-Path $publishedWeb "*") -Destination $nestedWeb -Recurse -Force
Copy-Item -LiteralPath $instructions -Destination (Join-Path $frontendRoot "LEEME_PASO_2_V256.txt")

Write-Host "4/5 Preparando la activación final..." -ForegroundColor Cyan
$publishedConfig = Join-Path $publishRoot "web.config"
if (-not (Test-Path -LiteralPath $publishedConfig)) { throw "La publicación no generó web.config." }
Copy-Item -LiteralPath $publishedConfig -Destination (Join-Path $activationRoot "web.config")
Copy-Item -LiteralPath $instructions -Destination (Join-Path $activationRoot "LEEME_PASO_3_V256.txt")

Copy-Item -Path (Join-Path $backendRoot "*") -Destination $completeRoot -Recurse -Force
Copy-Item -Path (Join-Path $frontendRoot "*") -Destination $completeRoot -Recurse -Force
Copy-Item -Path (Join-Path $activationRoot "*") -Destination $completeRoot -Recurse -Force
Copy-Item -LiteralPath $instructions -Destination (Join-Path $completeRoot "LEEME_ACTUALIZACION_V256.txt") -Force

Write-Host "5/5 Creando ZIP compatibles..." -ForegroundColor Cyan
New-CompatibleZip $backendRoot $backendZip
New-CompatibleZip $frontendRoot $frontendZip
Copy-Item -LiteralPath $frontendZip -Destination $frontendCompatibleZip -Force
New-CompatibleZip $activationRoot $activationZip
New-CompatibleZip $completeRoot $completeZip

Remove-Item -LiteralPath $stageRoot -Recurse -Force

Write-Host "Paquetes V256 listos:" -ForegroundColor Green
Write-Host $backendZip
Write-Host $frontendZip
Write-Host $frontendCompatibleZip
Write-Host $activationZip
Write-Host $completeZip
