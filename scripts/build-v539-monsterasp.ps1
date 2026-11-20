param(
    [string]$Stamp = "20261117152000"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$outputs = Join-Path $root "outputs"
$base = Get-ChildItem -LiteralPath $outputs -Directory |
    Where-Object Name -Like "_publish_v536_v520_targeted*" |
    Select-Object -First 1
$backend = Join-Path $outputs "_publish_v539_backend_$Stamp"
$stage = Join-Path $outputs "_stage_v539_$Stamp"
$zip = Join-Path $outputs "V539_PERMISOS_COMPLETOS_BOTONES_AZULES_DISENO_ORIGINAL_MONSTERASP_$Stamp.zip"

if (-not $base) { throw "No se encontró el paquete visual base V536/V520." }
if (-not (Test-Path -LiteralPath $backend)) { throw "No se encontró el backend publicado: $backend" }
if (Test-Path -LiteralPath $stage) { throw "La carpeta de preparación ya existe: $stage" }
if (Test-Path -LiteralPath $zip) { throw "El ZIP ya existe: $zip" }

New-Item -ItemType Directory -Path $stage | Out-Null

Get-ChildItem -LiteralPath $backend -Force | Where-Object {
    $_.Name -notin @("wwwroot", "Data", "App_Data", "web.config", "appsettings.json", "appsettings.Production.json", ".env")
} | Copy-Item -Destination $stage -Recurse -Force

Copy-Item -LiteralPath (Join-Path $base.FullName "wwwroot") -Destination $stage -Recurse -Force
$webRoot = Join-Path $stage "wwwroot"

$permissionChunk = Get-ChildItem -LiteralPath (Join-Path $webRoot "assets") -Filter "UsersPermissionsWorkspace-*.js" | Select-Object -First 1
if (-not $permissionChunk) { throw "No se encontró el módulo de permisos del diseño original." }
$js = [IO.File]::ReadAllText($permissionChunk.FullName)

$replacements = @(
    @(
        '["canViewWarehouseInventory","Almacén","Consultar inventario"],["canDeleteWarehouseInventory"',
        '["canViewWarehouseInventory","Almacén","Consultar inventario"],["canCreateWarehouseArticles","Almacén","Crear artículos nuevos desde una entrada de inventario"],["canDeleteWarehouseInventory"'
    ),
    @(
        'd==="canRequestWarehouse"||d==="canViewWarehouseInventory"||d==="canCancelWarehouseRequests"||d==="canViewWarehouseReports"?o:d==="canDeleteWarehouseInventory"?!1:',
        'd==="canRequestWarehouse"||d==="canViewWarehouseInventory"||d==="canCancelWarehouseRequests"||d==="canViewWarehouseReports"?o:d==="canCreateWarehouseArticles"?r:d==="canDeleteWarehouseInventory"?!1:'
    ),
    @(
        'canCreateMaintenanceMovements:Be,canCreateTickets:Je',
        'canCreateMaintenanceMovements:Be,canCreateWarehouseArticles:!1,canCreateTickets:Je'
    ),
    @(
        'canLoadWarehouseInventory:!0,canAuthorizeWarehouseRequests:a==="DEPARTMENT_MANAGER"',
        'canLoadWarehouseInventory:!0,canCreateWarehouseArticles:a==="DEPARTMENT_MANAGER",canAuthorizeWarehouseRequests:a==="DEPARTMENT_MANAGER"'
    ),
    @(
        'disabled:C||f==="SUPPORT"||f==="WAREHOUSE_REQUESTER"||n.role==="ADMINISTRATOR"&&i.key==="canManageUsers"',
        'disabled:C||n.role==="ADMINISTRATOR"&&i.key==="canManageUsers"'
    )
)

foreach ($pair in $replacements) {
    $old = $pair[0]
    $new = $pair[1]
    $count = ([regex]::Matches($js, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Se esperaba una coincidencia y se encontraron $count para: $old" }
    $js = $js.Replace($old, $new)
}
[IO.File]::WriteAllText($permissionChunk.FullName, $js, [Text.UTF8Encoding]::new($false))

Copy-Item -LiteralPath (Join-Path $root "public\v539-button-polish.css") -Destination (Join-Path $webRoot "v539-button-polish.css") -Force
$indexPath = Join-Path $webRoot "index.html"
$index = [IO.File]::ReadAllText($indexPath)
$styleLink = '  <link rel="stylesheet" href="/v539-button-polish.css?v=539">' + [Environment]::NewLine
if (-not $index.Contains("</head>")) { throw "index.html no contiene </head>." }
$index = $index.Replace("</head>", $styleLink + "</head>")
[IO.File]::WriteAllText($indexPath, $index, [Text.UTF8Encoding]::new($false))

$instructions = @"
ACTUALIZACIÓN V539 PARA MONSTERASP

1. Detén temporalmente el sitio o el grupo de aplicaciones.
2. Extrae el contenido de este ZIP directamente sobre la carpeta publicada.
3. No reemplaces tu web.config, appsettings, .env, Data, App_Data ni uploads. Este ZIP no los incluye.
4. Inicia el sitio y fuerza una recarga del navegador con Ctrl+F5.

Incluye:
- Permisos personalizados que sí respetan valores activados y desactivados.
- Catálogo completo editable también para Soporte y solicitantes de Almacén.
- Permiso para crear artículos desde entradas de inventario.
- Botones sin estilo convertidos al azul institucional; ningún botón de respaldo queda gris.
- Eliminación del botón de volver duplicado en Tickets transferidos.
- Diseño original V536/V520 conservado; no se recompiló ni sustituyó el tema visual.

Usuarios con creación de artículos de almacén:
- diorly.abreu
- ana.rodriguez
- administrador principal
"@
[IO.File]::WriteAllText((Join-Path $stage "INSTRUCCIONES_V539_PERMISOS_Y_BOTONES.txt"), $instructions, [Text.UTF8Encoding]::new($false))

[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText("$zip.sha256.txt", "$hash  $([IO.Path]::GetFileName($zip))`r`n", [Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Zip = $zip
    Sha256 = $hash
    PermissionChunk = $permissionChunk.Name
    Stage = $stage
}
