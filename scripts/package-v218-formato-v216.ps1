$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V218_RELEASE_FULL\RegistroAgencias.SqlServer.V193.dll"
$target = Join-Path $root "artifacts\V218_CAMARA_GPS_PANEL_MOVIL_CON_DLL.zip"

if (-not (Test-Path -LiteralPath $backendDll)) {
  throw "No se encontró la DLL publicada V218."
}
if (Test-Path -LiteralPath $target) {
  Remove-Item -LiteralPath $target -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($target, [System.IO.Compression.ZipArchiveMode]::Create)

try {
  Get-ChildItem -LiteralPath $frontend -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($frontend.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    foreach ($entryName in @($relative, "wwwroot/$relative")) {
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive,
        $_.FullName,
        $entryName,
        [System.IO.Compression.CompressionLevel]::Optimal
      ) | Out-Null
    }
  }

  foreach ($entryName in @("RegistroAgencias.SqlServer.V193.dll", "wwwroot/RegistroAgencias.SqlServer.V193.dll")) {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
      $archive,
      $backendDll,
      $entryName,
      [System.IO.Compression.CompressionLevel]::Optimal
    ) | Out-Null
  }

  $readme = $archive.CreateEntry("LEEME_PRIMERO.txt")
  $writer = [System.IO.StreamWriter]::new($readme.Open())
  try {
    $writer.Write(@"
ACTUALIZACION V218 - MISMO FORMATO DE ENTREGA QUE V216 Y V217

Incluye:
- Frontend en raíz y dentro de wwwroot.
- DLL principal V218 en raíz y dentro de wwwroot.
- Cámara trasera con ubicación GPS obligatoria para evidencias de construcción.
- Coordenadas, precisión y fecha almacenadas junto a cada fotografía.
- Panel administrativo móvil sin texto vertical.
- Cabecera móvil con notificaciones, chat, perfil y cerrar sesión visibles.

PASOS OBLIGATORIOS
1. Detén temporalmente el sitio con app_offline.htm.
2. Espera 10 segundos para liberar la DLL.
3. Extrae TODO el ZIP en la carpeta de web.config y confirma reemplazos.
4. Elimina app_offline.htm y reinicia el sitio.
5. Abre /api/version y confirma exactamente "release":"V218".

Si /api/version no muestra V218, la DLL anterior sigue activa y la fotografía
continuará devolviendo 404. No requiere ejecutar SQL: las columnas GPS se crean
automáticamente al abrir el módulo.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
