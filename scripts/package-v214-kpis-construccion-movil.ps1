$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$target = Join-Path $root "artifacts\V214_KPIS_Y_CONSTRUCCION_MOVIL.zip"

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
ACTUALIZACION V214 - KPIS Y AGENCIAS EN CONSTRUCCION MOVIL

Incluye:
- Los seis KPI de reportes usan exactamente el mismo ancho y alto.
- Los KPI aparecen antes del bloque Vista administrativa.
- Corrección del conflicto que reducía únicamente la tarjeta En proceso.
- Cabecera y filtros de Agencias en construcción optimizados para móvil.
- Selector de etapas horizontal, legible y fácil de usar con el dedo.
- Historial, responsables y evidencias con mejor jerarquía visual.
- Botones de acciones táctiles, alineados y sin textos recortados.
- Formulario Nuevo registro más alto, legible y sin contenido comprimido.
- Acciones inferiores apiladas en móvil para mostrar los nombres completos.
- Conserva el dashboard, Excel, PDF y todas las mejoras anteriores.

Sube el contenido del ZIP al directorio publicado del sitio.
No requiere ejecutar SQL ni reemplazar DLL o web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
