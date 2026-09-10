$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$target = Join-Path $root "artifacts\V210_HELPDESK_MOVIL_PROFESIONAL.zip"

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
ACTUALIZACION V210 - HELPDESK MOVIL PROFESIONAL

Incluye:
- Filtros moviles alineados, legibles y con nombres completos.
- Estado del ticket destacado a todo el ancho.
- Nombre ampliado de Tickets en proceso.
- Prioridad y Categoria alineadas en una cuadricula uniforme.
- Boton Limpiar filtros cuando existe una busqueda o seleccion activa.
- Ticket completo limpio, sin mostrar accidentalmente el formulario de resolucion.
- El formulario operativo aparece solamente al seleccionar una accion autorizada.
- Conserva las mejoras V209 de cabecera, chat y cierre de sesion.
- Conserva permisos, datos y flujos existentes.

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
