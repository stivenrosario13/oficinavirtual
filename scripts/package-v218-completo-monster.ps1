$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$publish = Join-Path $root "artifacts\V218_RELEASE_FULL"
$target = Join-Path $root "artifacts\V218_COMPLETO_MONSTER_CAMARA_GPS.zip"

if (-not (Test-Path -LiteralPath (Join-Path $publish "RegistroAgencias.SqlServer.V193.exe"))) {
  throw "No se encontró la publicación completa V218."
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
ACTUALIZACION COMPLETA V218 PARA MONSTER

Esta versión corrige:
- Evidencia de construcción tomada con cámara trasera y GPS obligatorio.
- Guarda coordenadas, precisión y momento de captura de cada fotografía.
- Convierte imágenes compatibles a un formato seguro antes de cargarlas.
- Panel administrativo profesional en móvil, sin palabras partidas verticalmente.
- Muestra notificaciones, chat, perfil y cerrar sesión en la cabecera móvil.

PASOS OBLIGATORIOS PARA REEMPLAZAR EL BACKEND
1. En la carpeta de web.config, crea app_offline.htm para detener la aplicación.
2. Espera 10 segundos.
3. Extrae TODO este ZIP en esa carpeta y confirma todos los reemplazos.
4. Confirma que cambiaron RegistroAgencias.SqlServer.V193.exe y .dll.
5. Elimina app_offline.htm para iniciar el sitio.
6. Abre https://TU-DOMINIO/api/version.
7. Debe aparecer exactamente "release":"V218".
8. Cierra sesión, limpia la caché y vuelve a ingresar.

Si todavía aparece otra versión, el proceso anterior sigue activo y el endpoint
de evidencia responderá 404. No reemplaza web.config y no requiere SQL manual.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
