$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$delivery = Join-Path $artifacts "ENTREGA_MONSTER"
$outside = Join-Path $delivery "01_FUERA_DE_WWWROOT"
$inside = Join-Path $delivery "02_DENTRO_DE_WWWROOT"
$outsideZip = Join-Path $artifacts "01_CONFIGURACION_FUERA_DE_WWWROOT.zip"
$insideZip = Join-Path $artifacts "02_SUBIR_DENTRO_DE_WWWROOT.zip"
$insideZipV2 = Join-Path $artifacts "02_SUBIR_DENTRO_DE_WWWROOT_V2.zip"
$completeZip = Join-Path $artifacts "REGISTRO_AGENCIAS_MONSTER_COMPLETO.zip"
$monsterCompatibleZip = Join-Path $artifacts "SUBIR_V120_NOTIFICACIONES_TECNICOS_CON_DLL.zip"
$frontendOnlyZip = Join-Path $artifacts "SUBIR_V120_SOLO_FRONTEND_SIN_DLL.zip"
$backendOnlyZip = Join-Path $artifacts "SUBIR_V120_SOLO_BACKEND_CON_DLL.zip"
$rootExtractZip = Join-Path $artifacts "ACTUALIZAR_MONSTER_RAIZ_COMPLETO_V136_2026-08-20.zip"
$rootMonsterCompatibleZip = Join-Path $artifacts "ACTUALIZAR_MONSTER_V136_EXTRAER_EN_RAIZ.zip"
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCommand) { $dotnetCommand.Source } else { Join-Path $env:ProgramFiles "dotnet\dotnet.exe" }
if (-not (Test-Path $dotnet)) { throw ".NET SDK no está instalado o no se encuentra en PATH." }

Set-Location $root
Write-Host "1/4 Compilando React con Vite..." -ForegroundColor Cyan
& npm.cmd run build
if ($LASTEXITCODE -ne 0) { throw "Falló la compilación del frontend." }

if (Test-Path $delivery) { Remove-Item -LiteralPath $delivery -Recurse -Force }
New-Item -ItemType Directory -Path $outside -Force | Out-Null
New-Item -ItemType Directory -Path $inside -Force | Out-Null

Write-Host "2/4 Preparando archivos externos para SQL Server 2025..." -ForegroundColor Cyan
Copy-Item -LiteralPath (Join-Path $root "database-mssql\001_schema.sql") -Destination (Join-Path $outside "01_EJECUTAR_EN_SQL_SERVER.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\005_import_agencies_ltk.sql") -Destination (Join-Path $outside "05_CARGAR_3122_AGENCIAS_LTK.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\006_admin_roles.sql") -Destination (Join-Path $outside "06_CREAR_ROLES_Y_USUARIOS.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\007_import_groups_ltk.sql") -Destination (Join-Path $outside "07_CARGAR_161_GRUPOS_LTK.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\008_import_group_admins.sql") -Destination (Join-Path $outside "08_CREAR_ADMINISTRADORES_POR_GRUPO.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\009_equipment_evidence.sql") -Destination (Join-Path $outside "09_HABILITAR_FOTOS_INVERSOR_BATERIA.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\010_import_closed_agencies.sql") -Destination (Join-Path $outside "10_CARGAR_197_AGENCIAS_CERRADAS.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\011_import_closed_monitoring.sql") -Destination (Join-Path $outside "11_ACTUALIZAR_CERRADAS_DESDE_MONITOREO.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\027_internal_tickets_technician_assignment_and_location.sql") -Destination (Join-Path $outside "27_ACTUALIZAR_TICKETS_INTERNOS.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\028_department_ticket_workflow_history_notifications.sql") -Destination (Join-Path $outside "28_ACTUALIZAR_FLUJO_TICKETS_DEPARTAMENTOS.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\029_ticket_evidence_geolocation.sql") -Destination (Join-Path $outside "29_HABILITAR_GPS_EVIDENCIAS_TICKETS.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\029_notification_receipts.sql") -Destination (Join-Path $outside "29B_SINCRONIZAR_NOTIFICACIONES_LEIDAS.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\030_agency_transition_projects.sql") -Destination (Join-Path $outside "30_HABILITAR_PROCESO_AGENCIAS_CONSTRUCCION.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\031_technology_technicians_team.sql") -Destination (Join-Path $outside "31_CREAR_EQUIPO_TECNICOS_TECNOLOGIA.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\032_maintenance_inventory_movements.sql") -Destination (Join-Path $outside "32_HABILITAR_MANTENIMIENTO_E_INVENTARIO.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\033_general_services_team_users.sql") -Destination (Join-Path $outside "33_CREAR_EQUIPO_SERVICIOS_GENERALES.sql")
Copy-Item -LiteralPath (Join-Path $root "database-mssql\040_supervisor_assignments_pending_status.sql") -Destination (Join-Path $outside "40_HABILITAR_SUPERVISORES_Y_ESTADOS_PENDIENTES.sql")
Copy-Item -LiteralPath (Join-Path $root "deploy\CREDENCIALES_ADMINISTRADORES_GRUPOS.csv") -Destination $outside
Copy-Item -LiteralPath (Join-Path $root "deploy\ASIGNACIONES_ADMINISTRADORES_GRUPOS.csv") -Destination $outside
Copy-Item -LiteralPath (Join-Path $root "deploy\VARIABLES_MONSTER.txt") -Destination $outside
Copy-Item -LiteralPath (Join-Path $root "deploy\INSTRUCCIONES_DESPLIEGUE.md") -Destination $outside
Copy-Item -LiteralPath (Join-Path $root "deploy\app_offline.htm") -Destination $outside

Write-Host "3/4 Publicando ASP.NET Core..." -ForegroundColor Cyan
& $dotnet publish "api/RegistroAgencias.Api.csproj" -c Release -o $inside --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación de .NET." }

Write-Host "4/4 Creando paquetes ordenados..." -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function New-CompatibleZip([string]$sourceDirectory, [string]$destinationPath) {
    if (Test-Path $destinationPath) { Remove-Item -LiteralPath $destinationPath -Force }
    $archive = [System.IO.Compression.ZipFile]::Open($destinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File | ForEach-Object {
            $entryName = $_.FullName.Substring($sourceDirectory.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $archive.Dispose() }
}
function New-CompatibleZipExcluding([string]$sourceDirectory, [string]$destinationPath, [string[]]$excludedNames) {
    if (Test-Path $destinationPath) { Remove-Item -LiteralPath $destinationPath -Force }
    $archive = [System.IO.Compression.ZipFile]::Open($destinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $sourceDirectory -Recurse -File |
            Where-Object { $excludedNames -notcontains $_.Name } |
            ForEach-Object {
                $entryName = $_.FullName.Substring($sourceDirectory.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
    }
    finally { $archive.Dispose() }
}
foreach ($zip in @($outsideZip, $insideZip, $insideZipV2, $completeZip)) { if (Test-Path $zip) { Remove-Item -LiteralPath $zip -Force } }
New-CompatibleZip $outside $outsideZip
New-CompatibleZip $inside $insideZip
Copy-Item -LiteralPath $insideZip -Destination $insideZipV2
New-CompatibleZip $delivery $completeZip
New-CompatibleZipExcluding $inside $monsterCompatibleZip @("Microsoft.SqlServer.Server.dll")
$frontendPackage = Join-Path $artifacts "FRONTEND_V120_TEMP"
if (Test-Path $frontendPackage) { Remove-Item -LiteralPath $frontendPackage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $frontendPackage "wwwroot") -Force | Out-Null
Copy-Item -Path (Join-Path $inside "wwwroot\*") -Destination (Join-Path $frontendPackage "wwwroot") -Recurse -Force
New-CompatibleZip $frontendPackage $frontendOnlyZip
Remove-Item -LiteralPath $frontendPackage -Recurse -Force
$backendPackage = Join-Path $artifacts "BACKEND_V120_TEMP"
if (Test-Path $backendPackage) { Remove-Item -LiteralPath $backendPackage -Recurse -Force }
New-Item -ItemType Directory -Path $backendPackage -Force | Out-Null
Get-ChildItem -LiteralPath $inside -File | Where-Object { $_.Name -ne "Microsoft.SqlServer.Server.dll" } | Copy-Item -Destination $backendPackage -Force
Get-ChildItem -LiteralPath $inside -Directory | Where-Object { $_.Name -ne "wwwroot" } | Copy-Item -Destination $backendPackage -Recurse -Force
New-CompatibleZip $backendPackage $backendOnlyZip
Remove-Item -LiteralPath $backendPackage -Recurse -Force
$rootPackage = Join-Path $artifacts "MONSTER_RAIZ_COMPLETA_TEMP"
if (Test-Path $rootPackage) { Remove-Item -LiteralPath $rootPackage -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $rootPackage "wwwroot") -Force | Out-Null
Copy-Item -Path (Join-Path $inside "*") -Destination (Join-Path $rootPackage "wwwroot") -Recurse -Force
New-CompatibleZip $rootPackage $rootExtractZip
New-CompatibleZipExcluding $rootPackage $rootMonsterCompatibleZip @("Microsoft.SqlServer.Server.dll")
Remove-Item -LiteralPath $rootPackage -Recurse -Force

Write-Host "Entrega terminada:" -ForegroundColor Green
Write-Host "PRIMERO: $outsideZip" -ForegroundColor Yellow
Write-Host "DESPUÉS: $insideZip" -ForegroundColor Yellow
Write-Host "WEBFTP COMPATIBLE: $insideZipV2" -ForegroundColor Yellow
Write-Host "PAQUETE COMPLETO: $completeZip" -ForegroundColor Yellow
Write-Host "MONSTER COMPATIBLE CON DLL PRINCIPAL: $monsterCompatibleZip" -ForegroundColor Yellow
Write-Host "SOLO FRONTEND SIN DLL: $frontendOnlyZip" -ForegroundColor Yellow
Write-Host "SOLO BACKEND CON DLL: $backendOnlyZip" -ForegroundColor Yellow
Write-Host "EXTRAER DIRECTO EN LA RAIZ DE MONSTER: $rootExtractZip" -ForegroundColor Green
Write-Host "EXTRAER EN RAIZ, COMPATIBLE CON FTP MONSTER: $rootMonsterCompatibleZip" -ForegroundColor Green
