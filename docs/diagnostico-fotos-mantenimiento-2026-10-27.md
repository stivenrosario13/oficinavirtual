# Diagnóstico de fotos y consulta de Mantenimiento — 27/10/2026

## Hallazgos confirmados en el código

1. `EmployeeAuditForm.selected` esperaba la conversión de coordenadas a dirección después de cada fotografía. Los cuatro proveedores de dirección usaban `fetch` sin límite de tiempo y el indicador de captura se liberaba después de esas consultas. Una consulta que no terminaba podía mantener la captura ocupada indefinidamente.
2. La preparación de fotos decodificaba el original completo antes de reducirlo. Ni la decodificación ni la conversión del canvas tenían límite de espera. Si la conversión fallaba, se conservaba el original de hasta 20 MB y podía usarse para la vista previa. Las vistas previas tampoco se liberaban al salir del formulario.
3. La subida de fotografías tenía un temporizador, pero este se cancelaba al recibir los encabezados HTTP. La lectura posterior del JSON podía quedar esperando.
4. El aviso de la captura corresponde al temporizador de 20 segundos de `readTicketResource`. El listado `MaintenanceMovements` resolvía las firmas de todos los movimientos con `await StorageFiles.ReadDataUrl` dentro del bucle del lector SQL. Las firmas almacenadas externamente agregaban una lectura de almacenamiento por movimiento, con la conexión SQL abierta.

## Correcciones locales

- La cámara completa la foto y el GPS sin depender de los mapas. La dirección se consulta al entrar al paso de datos del empleado; ese paso se abre inmediatamente y permite completar los campos manualmente.
- Cada proveedor de dirección tiene una espera máxima de cuatro segundos. La respuesta incluye la lectura del cuerpo. Los datos escritos manualmente se conservan cuando termina la consulta.
- Se solicita una imagen reducida al decodificador compatible y se conserva una alternativa con `Image` para navegadores sin esa función. La salida mantiene su proporción y limita el lado mayor a 1600 píxeles.
- Se liberan bitmaps, canvas y URLs temporales. Se evita procesar fotos simultáneas y se limpia el selector después de cada selección para permitir reintentos.
- La preparación tiene límites de espera y mensajes recuperables; no recurre automáticamente a una vista previa del original pesado cuando falla la reducción.
- La subida y el registro mantienen su temporizador hasta terminar de leer el JSON.
- El listado de Mantenimiento omite las imágenes de firmas. El documento individual recupera la firma histórica cuando se abre, conservando la prioridad de las marcas existentes y las comprobaciones de acceso.

## Verificación y límites

- 30 pruebas automatizadas aprobadas: preparación de fotos, navegadores sin ImageBitmap, liberación de recursos, temporizadores, reintentos, consultas de tickets y documentos PDF.
- TypeScript y compilación de la API aprobados; la API compiló sin errores ni advertencias.
- El guion `scripts/qa-camera-memory.mjs` prueba el formulario real en Edge sin interfaz, con pantallas de 390×844 y 1366×900, foto sintética de 6000×8000 píxeles y cuatro proveedores de dirección sin respuesta. Comprueba ambas rutas de reducción, captura de tres fotos, avance al formulario y conservación de la dirección manual.
- La suite SQL existente `tests/maintenance-integration` no compila por referencias previas ausentes a `EnsureTicketWorkflowSchema`, `AddTicketHistory` y `AddTicketNotifications`. Su nueva comprobación de firma histórica queda pendiente de ejecución; no se considera aprobada.
- El servidor público reportó V448. El acceso de diagnóstico devolvió HTTP 401; no se midieron tiempos de consultas autenticadas ni se revisaron registros de producción.
- El mensaje de memoria del dispositivo es compatible con presión de memoria durante la fotografía, pero no se reprodujo en el equipo del usuario. Las pruebas de tamaño de pantalla no sustituyen pruebas físicas en Android/iPhone ni garantizan funcionamiento en cualquier hardware.
- Los cambios están en el proyecto local. No se desplegaron ni se modificaron datos de producción.
