# Revisión de cargas y memoria — 27/10/2026

## Cambios realizados

- Transporte común `src/lib/api-fetch.ts` para las peticiones del portal Vite: máximo de seis lecturas simultáneas, unión de lecturas JSON idénticas en curso y límites que incluyen el cuerpo HTTP. No conserva resultados después de terminar ni reintenta escrituras automáticamente. Las escrituras no esperan a la cola de lecturas; si no llega su confirmación, se indica comprobar el guardado antes de repetir. Los archivos se consumen como flujo sin clonar una copia que retenga todo el contenido en memoria.
- Los módulos de formularios, archivos, chat, Taller y documentos se importan al abrir la sección correspondiente. Las importaciones lentas terminan con un error recuperable y un botón de reintento. Usuarios abre su propio módulo sin cargar previamente el panel completo.
- Tickets no precarga movimientos ni transiciones. Los directorios de agencias, técnicos y categorías se renuevan cada cinco minutos en actualizaciones silenciosas; la actualización explícita sigue consultándolos. La biblioteca PDF del reporte por agente deja de descargarse al abrir Tickets.
- Mantenimiento publica las partes recibidas de forma independiente: movimientos, inventario, compras y estadísticas. Una parte fallida no descarta las demás. Se conserva el descanso de cinco minutos ante estadísticas con HTTP 503. La cola operativa muestra los tickets antes de terminar sus consultas secundarias y comunica sus errores.
- Almacenamiento publica sus seis resultados por separado. Cambiar el estado del primer resultado ya no cancela y reinicia las seis consultas. Se evita la actualización periódica de una pestaña oculta. Chat deja de tener dos ciclos de actualización de conversaciones simultáneos.
- Archivos consulta destinatarios al abrir las acciones que los necesitan, incluida la creación de carpetas compartidas. Los formularios y editores documentales se cargan cuando se utilizan.
- Panel consulta el catálogo de agencias con una petición `/api/panel/catalog`, en lugar de una por grupo. La API conserva la autorización `PanelView` y el filtro SQL de grupos del usuario. Solo completa un resumen limitado cuando hace falta; conserva los conteos reales de pendientes, completadas y revisión en el modo de recuperación.
- `PdfFilePreview` e `InstitutionalPdfCanvas` comparten un ciclo de documento: una descarga y un trabajador PDF mientras permanece abierto; cambiar página solo dibuja esa página. Hay límite de espera, reintento, canvas de hasta 2000 píxeles por lado y liberación de trabajador/canvas al cerrar. Se mantiene el visor alternativo institucional.
- Se mantienen las correcciones de cámara y consulta de firmas descritas en `diagnostico-fotos-mantenimiento-2026-10-27.md`: fotos reducidas, recursos liberados, dirección independiente de la captura y firmas recuperadas únicamente al abrir el documento individual.

## Medición de paquetes

Comparación de compilaciones locales, suma del tamaño gzip de los módulos JavaScript estáticos necesarios para cada entrada, incluido el código común. No representa una medición de latencia de producción ni una reducción equivalente de tiempo.

| Entrada | Antes | Después | Cambio |
| --- | ---: | ---: | ---: |
| Tickets | 435 997 B | 193 724 B | −56 % |
| Portal administrativo | 177 867 B | 97 590 B | −45 % |
| Panel | 121 563 B | 124 303 B | +2 % |
| Inicio | 93 811 B | 95 384 B | +2 % |

La protección compartida de peticiones y secciones agrega algo de código al inicio. PDF y Excel conservan paquetes grandes cuando se solicitan; Vite sigue avisando sobre esos paquetes. El CSS general mantiene aproximadamente 147 kB gzip: no se modificó su cascada global para evitar alterar las secciones existentes.

Reproducir: `node scripts/measure-bundles.mjs <etiqueta>`. Evidencia en `outputs/performance-comparison.json` y `outputs/performance-audit-final.json`.

## Verificación

- `npm run build`: TypeScript y compilación de producción aprobados. Salida local actualizada en `api/wwwroot`.
- API compilada correctamente. `dotnet run --project tests/performance-sql/performance-sql.csproj`: seis comprobaciones aprobadas sobre el método de producción con una base temporal local, eliminada al finalizar. Incluye administrador, dos supervisores, usuario sin grupos, intento de consultar un grupo ajeno y compatibilidad de consulta por grupo.
- 104 pruebas focalizadas aprobadas: transporte, cancelación y respuestas bloqueadas, cámara, documentos, guardados, permisos, componentes diferidos y paginación.
- Suite completa: 772 pruebas, 697 aprobadas y 75 fallidas. La referencia guardada tenía 76 fallos; no quedan fallos nuevos respecto a esa referencia. Los fallos restantes incluyen contratos antiguos sobre texto de código, expectativas de formularios y páginas PDF. No se marcaron como aprobados ni se ocultaron. Detalle en `outputs/performance-test-comparison.json`.
- `scripts/qa-performance.mjs`: 25 escenarios aprobados con componentes reales en Edge, pantallas de 390×844 y 1366×900, con respuestas API simuladas. Comprueba cargas de Usuarios, Archivos, Tickets, Cola, Auditoría, Taller, Ubicaciones y Almacenamiento; consulta de destinatarios al crear carpetas; una consulta secundaria bloqueada; timeout y reintento; ambos visores PDF con una descarga, un trabajador y su terminación al cerrar; y recuperación de importaciones. Resultados en `outputs/qa-performance.json`.
- `scripts/qa-camera-memory.mjs`: verificación previa de tres capturas, imagen sintética de 48 MP, dos rutas de decodificación y proveedores de dirección sin respuesta en ambos tamaños de pantalla.

## Límites y publicación

Los cambios y la compilación están en el equipo local. No se publicaron ni se modificó la información de producción. El acceso autenticado de diagnóstico al servidor público devolvió HTTP 401 en la revisión inicial; falta medir las consultas con la carga real del servidor y probar dispositivos físicos Android/iPhone. La prueba SQL antigua `tests/maintenance-integration` sigue requiriendo reparar sus dependencias de compilación antes de ejecutar su comprobación de firma histórica.

Un límite de espera permite recuperar la interfaz y mostrar el problema; no garantiza que un servidor sin respuesta entregue los datos. La revisión no acredita ausencia de fallos en todos los flujos ni funcionamiento instantáneo en cualquier dispositivo o conexión.
