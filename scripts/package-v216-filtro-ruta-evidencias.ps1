$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $root "api\wwwroot"
$backendDll = Join-Path $root "artifacts\V216_RELEASE_CLEAN\RegistroAgencias.SqlServer.V193.dll"
$target = Join-Path $root "artifacts\V216_FILTRO_Y_RUTA_EVIDENCIAS_CON_DLL.zip"

if (-not (Test-Path -LiteralPath $backendDll)) {
  throw "No se encontró la DLL publicada del backend."
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
ACTUALIZACION V216 - FILTRO Y RUTA DE EVIDENCIAS

IMPORTANTE: esta versión incluye la DLL principal porque Monster todavía no tenía
publicada la ruta del servidor para cargar evidencias de Agencias en construcción.

Incluye:
- Estado del ticket centrado en la primera fila.
- Tickets en proceso ocupa todo el ancho disponible debajo del título.
- Endpoint del backend para subir evidencias de avances.
- Conserva la preparación móvil de JPG, PNG, WebP, HEIC y HEIF de la V215.
- Conserva el historial centrado y los departamentos sin scroll horizontal.

Actualización recomendada:
1. Detén temporalmente el sitio desde el panel de Monster si la DLL está bloqueada.
2. Extrae todo el contenido del ZIP en la raíz donde está publicada la aplicación.
3. Confirma que RegistroAgencias.SqlServer.V193.dll fue reemplazada.
4. Inicia o recicla nuevamente el sitio.
5. Cierra sesión, limpia la caché del navegador y vuelve a ingresar.

No requiere ejecutar SQL ni reemplazar web.config.
"@)
  }
  finally { $writer.Dispose() }
}
finally { $archive.Dispose() }

Write-Output $target
