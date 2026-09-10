$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$target = Join-Path $root "artifacts\V209_CABECERA_Y_FILTROS_MOVIL.zip"

if (Test-Path -LiteralPath $target) {
  Remove-Item -LiteralPath $target -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::Open(
  $target,
  [System.IO.Compression.ZipArchiveMode]::Create
)

try {
  Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($source.Length).TrimStart([char[]]@('\','/')).Replace('\','/')
    foreach ($entryName in @($relative, "wwwroot/$relative")) {
      [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive,
        $_.FullName,
        $entryName,
        [System.IO.Compression.CompressionLevel]::Optimal
      ) | Out-Null
    }
  }

  $readme = $archive.CreateEntry("LEEME_PRIMERO.txt")
  $writer = [System.IO.StreamWriter]::new($readme.Open())
  try {
    $writer.Write(@"
ACTUALIZACION V209 - CABECERA Y FILTROS MOVIL

Incluye:
- Botones de notificaciones, chat y cerrar sesion completamente visibles en movil.
- Mayor contraste visual para chat y cerrar sesion.
- Cabecera adaptable sin acciones recortadas fuera de pantalla.
- Panel de filtros de tickets reorganizado y legible.
- Selectores de prioridad y categoria a ancho completo.
- Controles tactiles mas amplios y textos sin truncamiento.
- Conserva las funciones y flujos existentes.

Sube el contenido del ZIP al directorio publicado del sitio.
No requiere ejecutar SQL ni reemplazar DLL o web.config.
"@)
  }
  finally {
    $writer.Dispose()
  }
}
finally {
  $archive.Dispose()
}

Write-Output $target
