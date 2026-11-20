# Actualización V451

Estado: código y pruebas terminados. El usuario confirmó Rollo Papel Para Sumadora = 0. Inventario validado: 134 artículos, 65,144 unidades; las 134 filas coinciden con el Excel y la confirmación. ZIP V451 generado y verificado; no instalado en producción.

## Cambios

- Consultas de lectura: un reintento acotado para errores temporales dentro del plazo original; nunca repetir escrituras.
- Soporte: recursos independientes con dos consultas simultáneas, reutilización de directorios, cancelación al cambiar de cuenta y error específico recuperable. Se eliminó una consulta de estadísticas que no se utilizaba.
- Taller: catálogo de agencias independiente de solicitudes. Documentos: resumen sin descargar imágenes y firmas.
- Call Center: escalamiento interno a supervisor activo o Avería Técnica, dos destinos en Tickets transferidos, seguimiento del mismo ticket hasta la resolución y acceso a evidencia. Permisos de edición siguen separados del seguimiento.
- Almacenamiento: la clave local fue aceptada por GET /v1/usage (HTTP 200). El ZIP anterior podía borrar la variable al sustituir web.config. El paquete privado nuevo incorpora la configuración y la API conserva un respaldo fuera de wwwroot en App_Data/storage-credentials.json.
- Inventario: lectura de 134 filas, 133 cantidades válidas por un total parcial de 65144. Una cantidad ausente. Constructor preparado sin inventar cantidades. Se conserva el catálogo anterior mediante el mecanismo transaccional existente, sin borrar movimientos ni solicitudes.
- Se mantienen los cambios V450 (fotos, coordenadas, contraseñas, regiones y cola de Almacén).

## Validación local

- Compilación de interfaz y API correcta; typecheck correcto.
- Suite completa: 794 pruebas, 720 aprobadas, 74 fallos anteriores, ningún fallo nuevo frente a V450.
- 25 escenarios de rendimiento en navegador, en móvil y escritorio.
- Regiones, contraseñas y cola de almacén probados en 390 y 1366 px.
- Call Center probado en ambos tamaños: ambos destinos, aislamiento, recuperación de 503, error persistente identificable sin perder tickets, reintento manual y estado Resuelto.
- SQL Call Center: 16 comprobaciones sobre métodos de producción, incluyendo asignación, permisos y resolución por ambos destinatarios.
- Configuración privada de almacenamiento: 7 comprobaciones; servicio: 35; cola: 41.
- Snapshot de inventario: 8 comprobaciones SQL, concurrencia, idempotencia, respaldo y conservación de historial. Ejecutado también con las 134 filas: 9 comprobaciones correctas en total.
- Sin instalación en MonsterASP ni modificación de datos de producción. La única consulta al proveedor fue de lectura.

## Finalizar tras recibir la cantidad

1. Ejecutar scripts/prepare-corrected-inventory.py con el Python empaquetado y --sumadora-quantity CANTIDAD_CONFIRMADA. No asumir cero. Genera api/Data/warehouse-products-2026-10-27.json y outputs/corrected-inventory-validation.json.
2. Ejecutar tests/warehouse-corrected-snapshot/WarehouseCorrectedSnapshot.csproj de nuevo: debe incluir las 134 filas en SQL.
3. Revisar correspondencias del JSON contra el Excel y documentar el total final.
4. Ejecutar scripts/package-v451-double-route.ps1 -IncludePrivateStorage. Este ZIP contiene la clave del usuario en web.config; mantenerlo privado.
5. Ejecutar scripts/verify-v451-package.ps1, comprobar los hashes y entregar el ZIP e instrucciones. El empaquetador se niega a generar el ZIP si falta el inventario validado.

No mostrar credenciales ni el contenido del web.config privado en logs o mensajes.

## Paquete entregable

- outputs/ACTUALIZACION_V451_COMPLETA_PRIVADA_MONSTERASP_20261027175757.zip
- Tamaño: 20.06 MB; 386 entradas verificadas, doble ruta.
- SHA256: 5268D7D5E7947D8E972A25428E9C04455FDE4ADE45465AC23EB7BD526D2822D1
- Verificación del ZIP: versión, ejecutable, recursos, coordenadas, regiones, transferencias, configuración privada y las 134 filas del inventario con Sumadora = 0.
