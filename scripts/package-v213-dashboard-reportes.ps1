$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$target = Join-Path $root "artifacts\V213_DASHBOARD_REPORTES_Y_ESTADISTICAS.zip"

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
ACTUALIZACION V213 - DASHBOARD DE REPORTES Y ESTADISTICAS

Incluye:
- Dashboard operativo con indicadores visibles desde la portada.
- Casos del periodo, resueltos, carga activa, críticos, promedio y evidencia.
- Alcance automático por permisos y rol del usuario.
- Administradores: visión global o por departamento.
- Soportes: casos del departamento y rendimiento de sus técnicos.
- Técnicos: únicamente sus estadísticas, casos, tiempos y evidencias.
- Filtros y botones de exportación completamente alineados en móvil y escritorio.
- Libro Excel con Dashboard, fórmulas, datos tipados, filtros, formato condicional y metodología.
- PDF ejecutivo multipágina con indicadores, distribución, responsables y trazabilidad.
- Conserva todas las mejoras anteriores de tickets, filtros, cabecera y móvil.

Sube el contenido del ZIP al directorio publicado del sitio.
No requiere ejecutar SQL ni reemplazar DLL o web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
