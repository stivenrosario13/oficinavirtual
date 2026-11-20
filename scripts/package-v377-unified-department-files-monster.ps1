$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$publish = Join-Path $root "outputs/_publish_v405_normal_$stamp"
$package = Join-Path $root "outputs/_double_v405_normal_$stamp"
$out = Join-Path $root "outputs/V405_ALMACEN_VERIFICADO_$stamp"
$zipPath = Join-Path $out "ACTUALIZACION_COMPLETA_V405_ALMACEN_VERIFICADO_$stamp.zip"
New-Item -ItemType Directory -Path $publish,$package,$out -Force | Out-Null
Set-Location $root

& npm run build
if($LASTEXITCODE -ne 0){throw 'Falló la compilación web.'}
& dotnet publish api/RegistroAgencias.Api.csproj -c Release -o $publish --no-self-contained
if($LASTEXITCODE -ne 0){throw 'Falló dotnet publish.'}

$unexpected=Get-ChildItem $publish -Recurse -File | Where-Object {$_.Name -match '^\.env|^appsettings|^app_offline\.htm$' -or $_.FullName -match '\\(App_Data|uploads|preview|logs|node_modules)\\'}
if($unexpected){throw 'La publicación contiene configuración o datos no permitidos.'}
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $package -Recurse -Force
$second=Join-Path $package 'wwwroot'
Get-ChildItem $publish -Force | Where-Object Name -ne 'web.config' | Copy-Item -Destination $second -Recurse -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $package 'web.config') -Force
Copy-Item (Join-Path $root 'api/web.config') (Join-Path $second 'web.config') -Force

$readme=@'
ACTUALIZACIÓN COMPLETA V405 · ALMACÉN VERIFICADO · MONSTER

CAMBIOS INCLUIDOS:
- Verifica en SQL que cada serial asignado exista realmente, esté disponible y
  corresponda al artículo solicitado antes de reservar el despacho.
- Al iniciar la actualización por primera vez, elimina todas las solicitudes existentes
  dirigidas a Almacén, sus reservas pendientes y sus movimientos tipo solicitud.
  No elimina inventario, entregas, tickets, solicitudes administrativas ni Taller.
- Las nuevas solicitudes vinculadas a tickets quedan registradas inmediatamente
  en Pendientes de recibir hasta que Almacén confirme la recepción.
- Los técnicos de Servicios Generales conservan únicamente dos accesos: casos
  asignados y casos resueltos de su departamento; no ven la Cola en vivo.
- Pueden resolver sus casos con el flujo existente de solución, evidencia y GPS.
- Se reconocen asignaciones históricas guardadas con ID, correo o nombre de usuario.
- La dirección de cada ticket abre Google Maps y ofrece un botón para compartir.
- La consulta acepta el enlace completo y los caracteres alterados del QR grande.
- El lector usa detección nativa o un lector alternativo compatible con más móviles.
- Tecnología, Servicios Generales, Recursos Humanos, Almacén y Taller pueden
  escanear el formulario y consultar su etapa e historial autorizados.
- La trazabilidad distingue recepción, despacho, asignación de equipo y serial,
  autorización, rechazo y retiro final.
- El selector de Almacén mantiene visible el artículo seleccionado; el campo
  contiguo queda únicamente como buscador por nombre o ID.
- Las listas de artículos muestran solamente el nombre, sin cantidades disponibles.
- Archivos y comunicaciones presenta cuatro tarjetas azul claro/oscuro: Archivos,
  Solicitudes a Administrativos, Solicitudes a Almacén y Solicitudes a Taller.
- Cada departamento puede retirar equipos de una agencia y enviarlos directamente
  a su cola de Taller con formulario, PDF, QR, serial y trazabilidad.
- Taller opera las solicitudes separadas por Tecnología, Servicios Generales y
  Recursos Humanos; al finalizar envía el equipo reparado a Almacén.
- Los hallazgos automáticos respetan el departamento seleccionado en Cola en vivo.
- Tecnología ya no muestra categorías de Servicios Generales o Recursos Humanos.
- Cada departamento ve únicamente sus propios hallazgos; el Administrador
  principal puede usar Todos los departamentos para consultar el consolidado.
- Cola en vivo separa tickets y hallazgos automáticos por departamento.
- Tecnología, Servicios Generales y Recursos Humanos ven únicamente lo suyo.
- El Administrador principal conserva la vista consolidada de todos los departamentos.
- Soporte muestra solamente Tickets, Tickets internos y Hallazgos automáticos.
- Archivos y comunicaciones abre con tres tarjetas: Archivos, Solicitud a
  Administración y Solicitud a Almacén.
- Se incorpora adonis.batista como técnico de Servicios Generales; la cuenta se
  crea o actualiza automáticamente al iniciar la aplicación.
- Se eliminó la tabla grande y duplicada de Despachar del área principal.
- Despachar y Pendientes de autorizar permanecen únicamente como filtros en
  Estado de la solicitud.
- Los estados quedan ordenados así: Todas, Pendientes de recibir, Recibidas,
  Despachar, Pendientes de autorizar, Autorizadas y Rechazadas.
- La bandeja de Almacén abre inicialmente en Todas.
- Desde un ticket activo se puede crear una solicitud de equipo a Almacén ya
  vinculada al número de ticket, agencia y departamento correctos.
- El equipo y serial asignados por Almacén conservan el ticket_id durante la
  reserva, autorización, retiro y entrega.
- Un ticket vinculado no puede resolverse antes de que el equipo sea autorizado
  y retirado; el técnico debe instalarlo y subir la evidencia obligatoria.
- Al resolver, el formulario de Almacén recibe automáticamente la solución,
  técnico, fecha y fotografías del ticket en su historial de destino final.
- La evidencia vinculada conserva los permisos del formulario y no se expone
  a departamentos ajenos.
- Después de recibir una solicitud, pasa a la tabla interna Despachar.
- Todos los usuarios de Almacén pueden asignar el equipo y su serial.
- El despacho preparado pasa a Pendientes de autorización; solo un usuario
  con permiso de autorizar puede colocar el sello final.
- El departamento solicitante continúa viendo Recibida y no puede consultar
  equipos ni seriales internos hasta que Almacén autorice la solicitud.
- Si se rechaza o solicita corrección, la reserva preparada se libera.
- Actualizar agencias acepta y procesa hasta 5,000 filas reales por archivo.
- La importación divide automáticamente el trabajo en bloques seguros de
  1,000, muestra el avance y acumula un único resultado final.
- Si la conexión se interrumpe, el mismo archivo puede reenviarse sin crear
  duplicados: los códigos ya procesados se actualizan.
- El servidor acepta cuerpos JSON de hasta 50 MB y conserva el límite estricto
  de 5,000 agencias; 5,001 se rechazan con un mensaje claro.
- El sidebar de todos los usuarios de Almacén incluye ahora accesos propios a
  Almacén central y Cola en vivo.
- Cola en vivo abre una página exclusiva con pendientes, autorizadas,
  rechazadas, historial por fecha y acceso directo a cada solicitud.
- La cola en vivo ahora aparece directamente en la pantalla principal de
  Almacén central para todos los usuarios del departamento.
- Ya no es necesario entrar primero a Solicitudes PDF para verla.
- Almacén incorpora una cola en vivo separada en pendientes, autorizadas y
  rechazadas, con acceso directo a cada solicitud.
- La jornada diaria opera de 07:30 a. m. a 10:00 p. m.
- Al cierre, cada solicitud sin decisión queda en la tabla histórica como
  pendiente y conserva su fecha de entrada aunque continúe abierta otros días.
- La tabla permite filtrar por fecha, resultado y texto de la solicitud.
- Incluye los usuarios independientes de Almacén, sus roles y permisos, y el
  buscador de productos por nombre o ID para los departamentos solicitantes.
- Administración reutiliza exactamente el mismo componente de sello de
  Almacén, con idéntico tamaño, doble borde, diseño, responsable, fecha y QR.
- Se muestra un único sello vigente en la primera página: RECIBIDO,
  AUTORIZADO o RECHAZADO, según el estado actual de la solicitud.
- Ya no se agrega una página independiente ni dos sellos simultáneos.
- Corrige la identificación de los formularios administrativos modernos.
- El sello queda en una posición fija de la primera página.
- Los sellos AUTORIZADO, RECIBIDO, ENTREGADO y RECHAZADO quedan en áreas
  reservadas; ya no se pueden mover ni colocar sobre letras del documento.
- Los documentos generales agregan una página exclusiva de constancia con el
  sello institucional REAL / Grupo Tejeda, identidad, fecha, código y QR.
- Plantillas disponibles incluye seis formatos base: texto, carta,
  comunicación, formulario, comprobante y factura / cuenta interna.
- Los administradores principales pueden filtrar la cola por departamentos.
- Nueva tabla diaria con calendario y filtros de completados y pendientes.
- El ciclo operativo es de 07:30 a. m. a 10:00 p. m.; al cierre, los casos sin
  completar aparecen automáticamente como pendientes del día seleccionado.
- Corrige la regla base que mantenía los modales limitados a 440 píxeles.
- El catálogo de formularios administrativos ahora ocupa realmente casi todo
  el ancho de pantalla y muestra cuatro opciones por fila en escritorio.
- La solicitud seleccionada abre horizontalmente en una ventana mucho más
  grande, con campos distribuidos en varias columnas y desplazamiento interno.
- La distribución se adapta a escritorio, tableta y móvil sin perder controles.
- Nueva Recepción virtual para supervisores y propietarios representados.
- Solicitud de contrato y renovación con seguimiento hacia Administración.
- Contrato de alquiler, recibo de pago, carta de aumento y relación de
  combustible basados en los modelos institucionales suministrados.
- Mandamiento de pago actualizado sin duplicar el documento repetido.
- Todos los formularios usan identidad REAL / Grupo Tejeda, folio, QR,
  firmas, recepción, autorización, descarga, impresión e historial.
- Los supervisores solo ven sus propias solicitudes y únicamente pueden
  enviar solicitud de contrato o renovación desde Recepción virtual.
- Corrige el mensaje «No pudimos mostrar este módulo» en los perfiles de
  técnicos y supervisores provocado por archivos de módulos desactualizados.
- La reparación automática usa la versión instalada y fuerza una carga limpia.
- Los hallazgos, agencias y supervisores con datos heredados incompletos ya no
  interrumpen el módulo; se normalizan y permanecen disponibles para trabajar.
- La tarjeta de hallazgos automáticos por categoría de Santo Domingo queda
  aislada: si esa consulta falla, la cola principal continúa funcionando.
- Las comunicaciones PDF adoptan el diseño institucional moderno de Almacén:
  identidad REAL, azul oscuro y azul claro, secciones numeradas y trazabilidad.
- Los sellos de RECIBIDO, AUTORIZADO y RECHAZADO usan ahora el mismo diseño
  profesional de Almacén, con doble borde, responsable, fecha, código y QR.
- Las constancias de ENTREGA y RECEPCIÓN de Almacén y Administración conservan
  sus sellos originales y todas sus funciones.
- El contenido extenso y los saltos de página siguen paginándose sin recortes.
- Todos los departamentos utilizan el mismo diseño moderno de Biblioteca.
- PDF, imágenes y TXT se abren en línea con soporte de lectura por rangos.
- Word y Excel se descargan directamente para evitar pestañas en blanco.
- Los archivos movidos aparecen únicamente en su carpeta de destino.
- Arrastrar y soltar archivos sobre carpetas, como en el Explorador de Windows.
- Menú contextual de carpetas: abrir, mover, compartir/privatizar y eliminar.
- Compartidos organizado por departamentos y destinatarios, sin mostrar el
  departamento propio del usuario.
- Plantillas con interfaz profesional, controles claros, firma, sello, envío,
  impresión, descarga y archivo PDF integrados con Archivos.
- Las tarjetas Solicitud a Administración y Solicitud a Almacén ahora aparecen
  dentro de Archivos y comunicaciones > Solicitudes.
- Se eliminaron ambas tarjetas duplicadas de la pantalla inicial de Soporte.
- Las tarjetas no abren formularios directamente: Administración abre su
  bandeja completa y Almacén conserva íntegro su módulo original.
- Corrige la creación y recuperación automática de la columna departamental
  en SQL Server, incluso si la actualización V377 quedó aplicada parcialmente.
- Se eliminó el tab independiente Correspondencia.
- Los documentos históricos de Correspondencia se conservan y ahora aparecen
  dentro de Archivos junto con los documentos del departamento.
- Carpetas, navegación, resumen, búsqueda, filtros, cuadrícula, lista,
  carga, descarga, compartir, mover y papelera disponibles por departamento.
- Las carpetas quedan separadas por departamento para evitar cruces.
- Solicitudes, Plantillas, Flotas y Módems conservan su funcionamiento.
- La limpieza solicitada se ejecuta una sola vez y solo sobre solicitudes de Almacén.

INSTALACIÓN:
1. Detén temporalmente el sitio desde el panel de MonsterASP.NET.
2. Conserva una copia de seguridad del sitio y de App_Data.
3. Extrae este ZIP donde está el web.config activo y acepta reemplazar.
4. No elimines App_Data ni uploads. El ZIP no incluye esos datos.
5. Confirma que web.config apunte a RegistroAgencias.SqlServer.V405.exe.
6. Inicia el sitio, abre /api/version y confirma V405. Luego pulsa Ctrl+F5.
7. Entra a Soporte > departamento > Archivos y comunicaciones > Archivos.

El ZIP no contiene credenciales, bases de datos, App_Data, uploads, logs,
appsettings, archivos .env ni app_offline.htm.
'@
Set-Content (Join-Path $out 'LEEME_ACTUALIZACION_V405.txt') $readme -Encoding UTF8
Set-Content (Join-Path $package 'LEEME_ACTUALIZACION_V405.txt') $readme -Encoding UTF8

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{
  foreach($directory in Get-ChildItem $package -Directory -Recurse | Sort-Object FullName){$name=$directory.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/');[void]$archive.CreateEntry($name+'/')}
  foreach($file in Get-ChildItem $package -File -Recurse | Sort-Object FullName){$name=$file.FullName.Substring($package.Length).TrimStart([char[]]'\/').Replace('\','/');[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$name,[IO.Compression.CompressionLevel]::Optimal)}
}finally{$archive.Dispose()}

$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try{
  $names=@($archive.Entries.FullName)
  foreach($prefix in @('','wwwroot/')){
    foreach($name in @('RegistroAgencias.SqlServer.V405.exe','RegistroAgencias.SqlServer.V405.dll','RegistroAgencias.SqlServer.V405.deps.json','RegistroAgencias.SqlServer.V405.runtimeconfig.json','BouncyCastle.Crypto.dll','web.config','wwwroot/index.html','wwwroot/version-web.json','wwwroot/release-guard.js','Data/agency-locations-sonadora-20260907.json')){if(($prefix+$name) -notin $names){throw "Falta $prefix$name"}}
  }
  if($names -contains 'index.html'){throw 'El ZIP no conserva el formato normal de doble ruta.'}
  if($names | Where-Object {$_ -match 'RegistroAgencias\.SqlServer\.V(?!405)|(^|/)(\.env[^/]*|appsettings[^/]*|app_offline\.htm)$|(^|/)(App_Data|uploads|preview|logs|node_modules)/|\\'}){throw 'El ZIP contiene otra versión, datos privados o entradas no permitidos.'}
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)version-web\.json$'){$reader=[IO.StreamReader]::new($entry.Open());try{$metadata=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()};if($metadata.release -ne 'V405'){throw 'Versión web incorrecta.'}}
  foreach($entry in $archive.Entries | Where-Object FullName -match '(^|/)web\.config$'){$reader=[IO.StreamReader]::new($entry.Open());try{$config=$reader.ReadToEnd()}finally{$reader.Dispose()};if($config -notmatch 'RegistroAgencias\.SqlServer\.V405\.exe'){throw 'web.config no apunta a V405.'}}
  Write-Host "ZIP completo V405 verificado: $($archive.Entries.Count) entradas" -ForegroundColor Green
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content (Join-Path $out 'SHA256.txt') "$hash  $([IO.Path]::GetFileName($zipPath))"
Get-Item $zipPath | Select-Object FullName,Length,@{Name='SHA256';Expression={$hash}}
