$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$target = Join-Path $root "artifacts\V215_AGENCIAS_CONSTRUCCION_Y_EVIDENCIAS.zip"

if (Test-Path -LiteralPath $target) {
  Remove-Item -LiteralPath $target -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($target, [System.IO.Compression.ZipArchiveMode]::Create)

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
ACTUALIZACION V215 - AGENCIAS EN CONSTRUCCION Y EVIDENCIAS

Incluye:
- Los tres departamentos aparecen completos y sin carrusel horizontal en móvil.
- Tarjetas de departamento del mismo tamaño y nombres en dos líneas cuando sea necesario.
- Historial de avances centrado, simétrico y sin desplazamiento hacia la derecha.
- Responsable, evidencias y acciones usan todo el ancho disponible.
- Preparación automática de fotografías grandes antes de subirlas.
- Compatibilidad móvil con JPG, PNG, WebP, HEIC y HEIF cuando el dispositivo puede leerlos.
- Conversión segura a JPEG optimizado para mantener compatibilidad con el servidor actual.
- Estado visible dentro de la tarjeta: preparando, subiendo, guardada o error.
- Conserva el dashboard, KPI y todas las mejoras anteriores.

Sube el contenido del ZIP al directorio publicado del sitio.
No requiere ejecutar SQL ni reemplazar DLL o web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
