# Almacenamiento de imágenes y PDF

La aplicación usa `https://storage-api.gptcomer.com` desde el backend. La credencial se guarda en `STORAGE_KEY`; nunca se envía al navegador ni se incluye en un paquete de publicación.

## Activación en Monster

1. Detener el sitio y extraer la actualización de cola en la misma carpeta donde se extrajo la actualización anterior. El ZIP incluye las dos rutas habituales, pero **no incluye ni reemplaza `web.config`**: conserva la clave que ya funciona. No borrar `App_Data`, donde está la cola persistente. Volver a iniciar el sitio.
2. En la configuración privada del servidor, establecer `STORAGE_ENABLED=true` y `STORAGE_KEY` con la credencial entregada. La copia local de la clave está en `.env.local`, excluido de Git. No subir ese archivo dentro de `wwwroot`.
3. Conservar la conexión `ConnectionStrings__DefaultConnection` que ya usa el sitio y reiniciarlo.
4. La primera vez que arranca esta versión, se prepara automáticamente la cola de archivos anteriores. Si se configuró `STORAGE_MIGRATE_EXISTING=false`, usar **Encolar archivos existentes**. No hace falta esperar a que el proveedor tenga cuota para preparar la cola.
5. Entrar como administrador principal y abrir **menú principal → Almacenamiento**. La cola muestra pendientes, verificados, rechazos y próximo intento. Se reanuda sola después de reiniciar. **Probar imagen y PDF** comprueba el proveedor real, sin confundir un guardado temporal con una subida terminada; se puede ejecutar durante la pausa automática de la cola. Solo se bloquea durante una prueba en curso o una espera obligatoria por Retry-After/presupuesto diario. Comparte la exclusión de transferencias, el límite por minuto y el presupuesto UTC; una prueba exitosa reanuda los pendientes. Después, guardar y abrir una imagen y un PDF desde los módulos habituales para verificar permisos y persistencia.

La prueba del panel se ejecuta desde el servidor, en segundo plano, con un máximo de cinco minutos. Usa dos ejemplos públicos del sistema y verifica la descarga privada de cada uno, con referencias e integridad comprobadas. Reutiliza el contenido y la clave de idempotencia; no borra archivos ni modifica registros de negocio. El resultado se reinicia a «sin verificar» cuando se reinicia el servidor. No sustituye la comprobación de los módulos ni el informe de migración. Las rutas de diagnóstico requieren una sesión de administrador.

La petición de migración y su fase se conservan en `App_Data/StorageQueue/migration.json`. Se continúa al arrancar, sin volver a empezar lo completado. La primera preparación automática solo se inicia cuando aún no hay una migración registrada; después de finalizar, una nueva revisión se solicita con el botón.

## Cola persistente y límites

- Los archivos nuevos se escriben de forma atómica en `App_Data/StorageQueue`, fuera de `wwwroot`, antes de confirmar su guardado. SQL recibe únicamente una referencia `REGISTRO-STORAGE-V1` con identificador de cola, tipo, tamaño y hash. Las rutas existentes siguen aplicando permisos. Mientras el archivo está pendiente, se puede leer desde la cola privada; una vez verificado se descarga del proveedor. Los archivos rechazados se conservan para revisión y no se sirven desde la cola.
- Un solo trabajador usa un bloqueo de archivo exclusivo, incluso durante reinicios solapados de IIS. Se limita a 60 subidas por minuto como máximo (el ritmo normal es menor), con peticiones de API separadas al menos 50 ms. La guía establece 120 subidas/minuto, 8 concurrentes y 50 peticiones/segundo por defecto.
- Antes de procesar se consulta `/v1/usage`; se actualizan tamaños y cantidad máxima de archivos en procesamiento. Si el proveedor ya está en `max_pending`, se espera 30 segundos antes de consultar de nuevo. Las respuestas 429 siguen imponiendo los límites efectivos aunque sean menores que los predeterminados.
- Presupuesto local conservador de 10,000,000,000 bytes por día UTC: se contabiliza cada intento de subida antes de enviarlo y se conserva el contador al reiniciar. Al agotarse se espera hasta medianoche UTC. Los usos de otros clientes quedan sujetos a los límites del proveedor y a `Retry-After`.
- Solo se reintentan automáticamente 429, 503, 507 y errores de red. Se conserva el `Retry-After` completo, aunque supere 60 segundos, y la misma `Idempotency-Key`. En ausencia de una espera mayor se usa retroceso exponencial con variación aleatoria. Una respuesta `storage_quota_exceeded` pausa las cargas al menos 30 minutos. Esperar no amplía el espacio contratado: el proveedor debe habilitar capacidad.
- El ID aceptado se guarda antes de consultar `processing`, con pausas de dos segundos. Tras un reinicio se consulta ese ID, sin repetir la subida. Solo después de `ready` y una descarga verificada se declara completo. Las imágenes admiten 25 MB y los PDF 50 MB; el proveedor valida las 200 páginas, antivirus y contenido activo. Los rechazos definitivos quedan visibles y no se reintentan en bucle. Un error de credencial bloquea la cola hasta corregirlo y pulsar **Reanudar cola tras corregir configuración**.
- Capacidad temporal predeterminada: 2 GiB. Cuando se llena, los originales SQL restantes siguen en su lugar; el escaneo espera a que se libere espacio antes de continuar. Una carga nueva que no cabe devuelve un error y nunca informa falsamente que quedó guardada. Solo se elimina el temporal de trabajo después de guardar una referencia remota verificada; el original SQL se sustituye en una fase posterior y los archivos remotos nunca se eliminan.
- IIS debe mantener la aplicación activa para ejecutar trabajo en segundo plano. Si Monster la suspende, los archivos y la espera quedan persistidos y continúan cuando la aplicación vuelve a iniciarse; una cola dentro del sitio no puede ejecutar trabajo mientras el alojamiento está detenido.

Variables opcionales del backend: `STORAGE_QUEUE_PATH` (carpeta persistente privada fuera de `wwwroot`), `STORAGE_QUEUE_MAX_BYTES` (capacidad temporal), `STORAGE_UPLOADS_PER_MINUTE` (1–60), `STORAGE_DAILY_BYTES` (hasta 10,000,000,000). Debe conservarse la misma carpeta entre despliegues. No habilitar limpieza automática de `App_Data`, ni volver a un backend anterior que no entienda referencias de cola.

## Qué se guarda

- Auditorías, fotos de perfil, evidencias de tickets y hallazgos, avances de agencias, documentos institucionales, PDF generados y plantillas PDF.
- Firmas e imágenes incrustadas en los campos de firmas, formularios administrativos y plantillas editables.
- Para archivos nuevos compatibles, SQL guarda una referencia versionada con ID, tipo, tamaño y SHA-256 de la descarga verificada. No guarda los bytes del archivo ni una URL firmada.
- Las rutas actuales siguen verificando permisos y sirven el contenido desde el backend. No se crea una ruta pública que permita descargar un ID arbitrario.
- Word, Excel y audio conservan su tratamiento actual: el proveedor admite JPEG, PNG, WebP, GIF, AVIF y PDF.

Las cargas primero confirman una referencia de cola después de conservar el temporal; la copia remota solo se considera terminada en estado `ready` y con descarga verificada. Las imágenes pueden cambiar de bytes porque el proveedor las recodifica y elimina EXIF/GPS; la ubicación que la aplicación guarda como datos del registro se conserva. Los errores del proveedor se muestran en el panel. Con el servicio activado no se usa la base de datos como alternativa para bytes de archivos nuevos compatibles.

## Copia de los archivos existentes

La actualización implementa el traslado solicitado: tras copiar y verificar un archivo, vuelve a descargarlo y comprueba su SHA-256 y tamaño. Solo entonces sustituye el contenido SQL por una referencia privada. Una transacción comprueba que el original no cambió y actualiza el diario junto con el registro. Si falla la descarga, el original permanece intacto. No se borran registros de negocio. Las versiones antiguas que ya no coinciden quedan identificadas como cambiadas.

La tabla aditiva `dbo.storage_migration_files` registra la tabla/columna/clave originales, SHA-256 y referencia verificada. Cada versión se identifica por su contenido. Se incluyen columnas binarias `photo_data`, `avatar_data`, `file_data`, firmas `signature_data` e imágenes en JSON `metadata`/`content`, incluidas tablas históricas con clave primaria. Una tabla sin clave primaria se informa para revisión y no se altera. Los formatos no admitidos se omiten. Un bloqueo en SQL evita ejecutar dos copias simultáneas.

La tabla aditiva `dbo.storage_migration_pending` conserva referencias de cola y metadatos de los registros anteriores. El finalizador solo agrega una entrada a `storage_migration_files` cuando todos los archivos de ese registro están verificados, incluidas firmas en JSON. Durante la espera consulta metadatos, sin descargar de nuevo todos los blobs SQL. Los archivos rechazados quedan pendientes en el informe. No se invoca `DELETE` en el proveedor ni sobre los originales. Los registros pendientes se leen desde sus originales. Los trasladados se leen desde la API, incluidos los datos incrustados en JSON; los nuevos se leen desde la cola privada o el servicio. El diario registra released_at, release_state y released_bytes. El contador indica reducción neta de contenido, no reducción del tamaño físico asignado a los archivos SQL. No se ejecuta SHRINK automáticamente.

## Herramienta de consola

Desde la raíz del proyecto, con la conexión de producción y la clave en variables privadas:

```powershell
# Inventario: no sube archivos ni modifica el esquema.
dotnet run --project tools/storage-migration/StorageMigration.csproj

# Copia y verifica; conserva los originales.
dotnet run --project tools/storage-migration/StorageMigration.csproj -- --apply
```

Se escribe un informe en `outputs/storage-migration-*.json`. No contiene credenciales ni URLs firmadas.

## Historial de comprobaciones del 23 de octubre de 2026

- Credencial validada para la aplicación `oficina-virtual`.
- Subida y descarga privada real de una imagen del sistema verificadas.
- El proveedor rechazó el PDF de prueba con HTTP 429, `storage_quota_exceeded`, solicitud `3694a20b4033a89cbff83599b1d671dd`. Debe habilitar cuota antes de repetir la prueba del PDF o ejecutar la copia completa.
- La conexión real suministrada por el usuario se guardó en `.env.local` y se verificó contra Monster. El inventario encontró 6,655 registros candidatos. El intento de copia se detuvo en el primer archivo por `storage_quota_exceeded` (solicitud `861a707c452f523dabd2a56e7b437d0d`): cero archivos de la base copiados y todos los originales conservados. Informes: `outputs/storage-migration-20261023-210435.json` (inventario) y `outputs/storage-migration-20261023-210348.json` (intento de copia). El archivo antiguo `deploy/VARIABLES_MONSTER.txt` sigue siendo un ejemplo y no debe usarse como conexión real.
- Pruebas locales: cliente HTTP, descargas privadas, integridad, idempotencia, rechazo, timeout de procesamiento, conservación y reanudación de la migración.
- Verificación de cola: 24 comprobaciones de persistencia, recuperación, cuota, Retry-After, presupuesto UTC, idempotencia, referencias, rechazos y capacidad temporal. Migración con SQL local: 17 comprobaciones, incluidas firmas JSON y separación de pendientes/verificados. Cliente y diagnóstico: 31 comprobaciones. No se usaron datos de producción en estas pruebas.
- El sitio ya tiene la credencial activa; el proveedor respondió `storage_quota_exceeded` en las primeras pruebas. Esta actualización agrega la cola, pero no cambia el límite del proveedor. `/api/version` debe mostrar `storage: warehouse-forms-profile-v5-20261024` después de publicarla. La cola puede esperar y reintentar, pero no se debe afirmar que la migración está terminada mientras haya pendientes.
- Otras suites revisadas no están todas en verde: mantenimiento tiene referencias ausentes en su proyecto de pruebas (`EnsureTicketWorkflowSchema`, `AddTicketHistory`, `AddTicketNotifications`); la suite de almacén falla en la reserva de dos unidades; una prueba de formato PDF espera dos páginas y obtiene una. La compilación completa de la aplicación sí pasa. Estos fallos se dejan informados y no se alteraron esas reglas de negocio para ocultarlos.

Desactivar `STORAGE_ENABLED` rechaza nuevas imágenes/PDF: no vuelve a guardar sus bytes en SQL. Las referencias ya creadas siguen necesitando la credencial para descargarse. Una versión antigua del backend no entiende las nuevas referencias; conservar esta actualización para leerlas.


## Actualización: Almacenamiento y liberación verificada de SQL

- Sección independiente **Almacenamiento**, visible solo al administrador principal. Todos sus endpoints exigen el rol Administrator; el panel administrativo recupera su contenido anterior.
- Colores azul oscuro y texto blanco. Desglose de imágenes/PDF, origen anterior/nuevo, módulo y perfil que incorporó el archivo a la cola. La atribución usa la sesión validada del servidor y se conserva al reiniciar. Los anteriores sin información comprobable aparecen como Sin clasificar. Las cifras cuentan archivos únicos de la cola, no cada uso repetido del mismo archivo.
- Contadores de bytes liberados en registros SQL, archivos verificados y temporales del servidor. Los archivos de formatos no admitidos por el proveedor permanecen intactos; las nuevas imágenes HEIC/HEIF/BMP/TIFF deben convertirse a JPG/PNG/WebP y se rechazan si no son compatibles.
- El 24/10/2026 a las 00:10:40 UTC, el proveedor respondió **Espacio agotado (0 MB)** para una imagen de 3,881 bytes. Código storage_quota_exceeded; request_id ae021a2ca9f80f3e9e1011b3e803f76f. Usage: 4,050 bytes, una imagen lista y cero pendientes. Es una restricción del espacio total configurado en el proveedor, que debe corregir quien administra ese servicio. No es un exceso de velocidad de la cola. El panel conserva ahora el mensaje exacto del proveedor.
- Comprobación real de SQL: 2,731,606,016 bytes asignados (2,605 MiB), 2,671,771,648 bytes usados. La fase real de liberación encontró cero copias verificadas: cero registros retirados y cero bytes liberados. No se debe declarar terminada la migración con esta cuota bloqueada.
- Herramienta: `--space` consulta tamaños; `--verify` prueba imagen y PDF; `--release` sustituye hasta diez originales ya verificados por ejecución. `--apply` continúa siendo solo copia y `--release` es explícito en consola; el sitio ejecuta ambas fases automáticamente con esta actualización.
- Instalar conservando **web.config y App_Data**. La clave y la cola persistente no se incluyen en el ZIP. No borrar los temporales ni restaurar una versión antigua que no interprete las referencias.

## Auditoría de campo y prueba manual

Desde «Reportar incidencia en mi agencia», el administrador principal y el supervisor seleccionan únicamente Tecnología, Servicios Generales o Recursos Humanos. Después aparece «Crear ticket» y el formulario habitual mantiene la agencia y su grupo. El directorio general de departamentos conserva sus opciones.

La prueba manual no elimina archivos ni sustituye la confirmación del proveedor: requiere subida y descarga verificadas tanto de imagen como de PDF. Si falla, se conserva la pausa y los archivos pendientes. El cambio no concede capacidad adicional a una cuenta con cuota agotada.

## Actualizar estado e inventario de la API

«Actualizar estado» consulta la copia, cola, pruebas, desglose local y uso del proveedor, incluso si las subidas están pausadas. Muestra el estado de carga y la hora de consulta. El inventario remoto muestra listos, imágenes, PDF, procesamiento, rechazos y última subida; los pendientes locales no se suman a lo recibido. Los archivos de prueba pueden estar incluidos. Las lecturas del proveedor se comparten durante 30 segundos y respetan Retry-After. Si fallan, se conserva la última lectura con su fecha y un aviso; nunca se reemplaza por un cero engañoso. El nuevo endpoint /api/admin/storage/usage está reservado al administrador principal.

## Lectura más reciente del proveedor

El 23/10/2026 a las 20:50:01 (Santo Domingo), GET /v1/usage respondió HTTP 200: 70 archivos listos, 69 imágenes y 1 PDF; 28,472,574 bytes lógicos; cero en proceso y cero rechazados. La última subida informada fue a las 20:49:57. Esta lectura confirma recepción reciente y reemplaza el diagnóstico anterior de inventario detenido. No equivale a una migración completa ni confirma por sí sola la liberación de SQL.

Validación de esta actualización: 41 comprobaciones de cola/uso del proveedor, 35 del cliente/diagnóstico y 39 verificaciones HTTP del paquete publicado (permisos y prueba manual durante espera). Compilación de interfaz y backend correcta. Navegador local: tres departamentos en auditoría, paso Crear ticket, agencia conservada, directorio general conservado, prueba habilitada durante pausa local, bloqueo durante Retry-After y botón Actualizar estado con inventario remoto. Las pruebas locales usan datos simulados.

## Solicitudes de almacén y formularios por perfil — 24/10/2026

Debajo de Perfil que incorporó el archivo se detalla el tipo de documento y el perfil, con imágenes, PDF, tamaño original, pendientes, verificados, revisión y contenido aún en SQL. Incluye solicitudes de almacén, formularios administrativos, formularios de plantillas y otros documentos archivados del canal de solicitudes. Clasifica también los anteriores por sus metadatos guardados, sin volver a subirlos ni alterar sus referencias. No atribuye un perfil histórico si no consta en la cola. Los formularios que solo se generan para descargar y no se archivan no cuentan como archivos guardados.

Cada referencia se cuenta una vez por categoría/perfil. Este detalle es un subconjunto del inventario: no se suma otra vez al total. La lectura usa /api/admin/storage/documents (solo administrador principal), se comparte durante 30 segundos y devuelve un aviso si SQL no responde.

Auditoría real del 24/10/2026 a las 12:16:26 (Santo Domingo): las 18 solicitudes de almacén creadas desde las 00:00 UTC del día contienen referencias de almacenamiento, 3.834 bytes en SQL frente a 537.774 bytes originales, y ningún archivo incrustado en sus metadatos. En las columnas binarias de archivos del sistema solo quedan dos imágenes/PDF anteriores sin migrar, que corresponden a los rechazos pendientes. Los formatos ajenos al alcance se conservan. Los registros de negocio y metadatos sí siguen en SQL; las nuevas imágenes y PDF pasan por la cola privada del servidor hacia la API, sin guardar sus bytes en SQL.

Validación: 31 comprobaciones de migración e inventario sobre una base local de prueba, incluidas referencias compartidas, perfil, verificación frente a pendientes y preservación de archivos antiguos. No se crearon solicitudes ficticias en producción.
