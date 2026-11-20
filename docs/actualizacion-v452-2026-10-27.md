# V452: restaurar organización de Solicitudes de Almacén

La carga independiente de módulos dejó a WarehousePdfRequests sin los estilos que antes recibía indirectamente de MaintenanceCommunications y MaintenanceDocumentEditor. No cambió el contenido de las solicitudes: faltaban las reglas de distribución y presentación.

La corrección importa las dos hojas de estilo existentes desde WarehousePdfRequests. No modifica su JSX, filtros, permisos, acciones, consultas ni datos del inventario. Se mantienen las correcciones de V451; solo se actualizan los identificadores de publicación para distribuir esta reparación.

Validación: `node scripts/qa-warehouse-request-layout.mjs --baseline` reprodujo ambos estilos ausentes, resumen y catálogo en bloques sin distribución, en 1366 y 390 píxeles. Con la corrección, `node scripts/qa-warehouse-request-layout.mjs` comprueba distribución de escritorio, filtros arriba en móvil, ausencia de desbordamiento, estados Recibir/Autorizar/Asignar, búsqueda y vistas de lista y tarjetas. Una sola consulta de documentos por apertura, sin errores JavaScript. Datos simulados; no se modifica producción.

Las capturas y resultados quedan en outputs/warehouse-requests-before-* y outputs/warehouse-requests-after-*. El ZIP conserva el catálogo de 134 productos, el saldo base de 65,144 y Sumadora en 0. El paquete es privado porque incluye la configuración de almacenamiento autorizada.
