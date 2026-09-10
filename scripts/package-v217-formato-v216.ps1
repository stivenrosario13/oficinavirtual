$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V217_RELEASE_FULL\RegistroAgencias.SqlServer.V193.dll"
$target = Join-Path $root "artifacts\V217_FILTROS_CABECERA_EVIDENCIAS_CON_DLL.zip"

if (-not (Test-Path -LiteralPath $backendDll)) {
  throw "No se encontró la DLL publicada V217."
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
ACTUALIZACION V217 - MISMO FORMATO DE ENTREGA QUE V216

Incluye:
- Frontend en raíz y dentro de wwwroot.
- DLL principal V217 en raíz y dentro de wwwroot.
- Dos estados distribuidos en todo el ancho.
- Cabecera móvil con notificaciones, chat, perfil y cerrar sesión.
- Ruta del backend para cargar evidencias de Agencias en construcción.

Detén temporalmente el sitio, extrae todo el ZIP, confirma que la DLL fue
reemplazada y reinicia el sitio. Después abre /api/version y confirma V217.

No requiere SQL ni reemplaza web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
