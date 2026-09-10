$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$publish = Join-Path $root "artifacts\V217_RELEASE_FULL"
$target = Join-Path $root "artifacts\V217_COMPLETO_MONSTER_EVIDENCIAS.zip"

if (-not (Test-Path -LiteralPath (Join-Path $publish "RegistroAgencias.SqlServer.V193.exe"))) {
  throw "No se encontró la publicación completa V217."
}
if (Test-Path -LiteralPath $target) {
  Remove-Item -LiteralPath $target -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($target, [System.IO.Compression.ZipArchiveMode]::Create)

function Add-Entry([string]$filePath, [string]$entryName) {
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
    $archive,
    $filePath,
    $entryName.Replace('\','/'),
    [System.IO.Compression.CompressionLevel]::Optimal
  ) | Out-Null
}

try {
  Get-ChildItem -LiteralPath $frontend -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($frontend.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    Add-Entry $_.FullName $relative
    Add-Entry $_.FullName "wwwroot/$relative"
  }

  Get-ChildItem -LiteralPath $publish -Recurse -File |
    Where-Object {
      -not $_.FullName.StartsWith((Join-Path $publish "wwwroot"), [System.StringComparison]::OrdinalIgnoreCase) -and
      $_.Name -notin @("web.config", "Microsoft.SqlServer.Server.dll", "RegistroAgencias.SqlServer.V193.pdb")
    } |
    ForEach-Object {
      $relative = $_.FullName.Substring($publish.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
      Add-Entry $_.FullName $relative
      Add-Entry $_.FullName "wwwroot/$relative"
    }

  $readme = $archive.CreateEntry("LEEME_PRIMERO.txt")
  $writer = [System.IO.StreamWriter]::new($readme.Open())
  try {
    $writer.Write(@"
ACTUALIZACION COMPLETA V217 PARA MONSTER

Esta versión corrige:
- Dos estados: Pendientes de asignar y Abiertos ocupan todo el ancho y quedan centrados.
- Un estado o tres estados también se distribuyen automáticamente en todo el espacio.
- En móvil aparecen notificaciones, chat, perfil y cerrar sesión al mismo tiempo.
- Incluye el endpoint real del servidor para subir evidencias de Agencias en construcción.
- Conserva la preparación de JPG, PNG, WebP, HEIC y HEIF.

PASOS OBLIGATORIOS PARA REEMPLAZAR EL BACKEND
1. En la carpeta donde está web.config, crea temporalmente app_offline.htm para detener la aplicación.
2. Espera unos 10 segundos.
3. Extrae TODO este ZIP en esa misma carpeta y confirma los reemplazos.
4. Verifica que se reemplazaron RegistroAgencias.SqlServer.V193.exe y RegistroAgencias.SqlServer.V193.dll.
5. Elimina app_offline.htm para iniciar nuevamente el sitio.
6. Abre https://TU-DOMINIO/api/version en el navegador.
7. Debe aparecer exactamente: "release":"V217".
8. Cierra sesión, limpia la caché del navegador y vuelve a ingresar.

Si /api/version todavía muestra V193 u otra versión, el servidor anterior sigue activo
y la carga de evidencias continuará respondiendo 404.

No reemplaza web.config y no requiere ejecutar SQL.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
