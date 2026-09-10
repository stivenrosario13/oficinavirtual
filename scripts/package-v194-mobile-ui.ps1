$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "api\wwwroot"
$target = Join-Path $root "artifacts\V208_CHAT_MOVIL_RESTAURADO_COMO_V193.zip"

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
ACTUALIZACION V208 - CHAT MOVIL RESTAURADO COMO V193

Incluye:
- Sistema visual moderno para todas las secciones.
- Navegacion, botones, filtros, formularios, tablas y modales unificados.
- Tarjetas de tickets profesionales y expansion dentro de la lista movil.
- Chat compacto mediante boton flotante a la derecha.
- Diseno responsive y controles tactiles ordenados.
- Conserva las funciones y flujos existentes.
- Restaura exclusivamente en telefono el diseno y las medidas del chat V193.
- La lista de conversaciones aparece sola antes de seleccionar un chat.
- Al seleccionar una conversacion, solo se muestra el chat con su boton de volver.
- El chat vuelve a ocupar correctamente la pantalla completa del telefono.
- Chat de soporte mas compacto, moderno y ordenado.
- Navegacion progresiva con Escape sin cerrar todo inesperadamente.
- Correccion de la duracion de las notas de voz.
- Control de version y pruebas automatizadas sincronizados.
- Modal de ticket completo y adaptable a cada pantalla.
- Barra superior para cambiar entre todas las acciones del ticket.
- Botones de Evidencias, Transferir, Responsable y Resolver redisenados.
- Resumen del ticket organizado sin eliminar informacion ni funciones.
- Ventana del chat mas baja y compacta.
- Mensajes con desplazamiento interno para conservar todos los controles.
- Evidencias, Transferir, Responsable y Resolver abren a todo el ancho.
- Eliminado el modal flotante y su desplazamiento interno.
- Los formularios completos usan el desplazamiento normal de la pagina.
- El formulario seleccionado aparece antes del resumen del ticket.
- Las acciones ya no se agregan debajo de la tarjeta.
- Tarjetas restauradas al diseno anterior solicitado.
- Retiradas las capas experimentales V197 a V200.
- Restaurado el comportamiento original V194 de las tarjetas.
- Restaurados los estilos, expansion y botones originales de V194.

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
