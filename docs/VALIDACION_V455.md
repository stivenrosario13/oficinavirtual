# Validación local V455 — 28 de octubre de 2026

Entrega: `outputs/ACTUALIZACION_V455_COMPLETA_PRIVADA_MONSTERASP_20261028191820.zip`.

SHA-256: `80F96F5E906D8504AB67A52FD6007357BAAB042CECADBE9AE5E26D0BA6234BB2`.

## Cambios

- Gasolina de Sandra como registro diario sin acciones de recepción, firma recibida, autorización ni entrega. Se ocultan estados y se eliminan presupuesto/tickets, tickets físicos y crédito disponible del formulario, Excel y PDF. Los registros históricos se presentan con el nuevo formato, conservando el original almacenado.
- GAS de Sulay separado y sujeto a autorización por otra persona. Filtros por fecha de consumo en ambas relaciones.
- Cambio de módem desde tickets internos abiertos de Tecnología para Avería Técnica; comprobante con agencia y ticket vinculados. El servidor comprueba acceso al ticket y coincidencia del identificador de agencia.
- Edición de solicitudes de Almacén solo para Diorly y Ana, antes de preparación o entrega. Artículos y cantidades deben estar disponibles. Conserva historial, folio y revisión; exige nueva recepción y autorización.
- Préstamos de fin de semana de Vittini dentro de Solicitudes a Almacén, con fechas, entrega habitual, declaración de uso por agencia y devolución de sobrantes confirmada por Almacén. Las confirmaciones repetidas no duplican el ingreso de existencias.

## Verificaciones

- `npm run build`: correcto; advertencia de tamaño de algunos paquetes JavaScript.
- Publicación .NET 8 Release: correcta, ejecutable y ensamblado V455.
- Vitest: 87 pruebas en nueve archivos, todas correctas (combustible, Excel, PDF, internet, estados de Almacén, agencias y autorización).
- Integración SQL: 55 comprobaciones correctas usando una base temporal aislada que se elimina al terminar. Incluye permisos por cuenta, revisión concurrente, inventario agotado, devolución simultánea, relación entre ticket y agencia y separación histórica de combustible.
- Navegador con API simulada: importación Excel, corrección de filas, reintento parcial, edición, gasolina agrupada e internet; filtros de Sandra, agencia bloqueada del módem, edición de inventario, declaración y confirmación de devolución, creación y envío del préstamo desde móvil.
- Capturas revisadas en `outputs/operations-forms/` y `outputs/administrative-excel/`.
- ZIP: 398 entradas, ambas rutas verificadas por hash, inventario oficial conservado, recursos de interfaz presentes y marcadores de V455 comprobados. No contiene base de datos, `.env`, `App_Data`, `uploads` ni logs.

No se realizó despliegue remoto. La prueba de navegador simula la API; las transacciones SQL se probaron directamente en la base temporal. El ZIP contiene configuración privada de almacenamiento para este servidor.
