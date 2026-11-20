# Importación Excel y edición de formularios administrativos

## Uso

1. En **Formularios administrativos**, abre **Nueva solicitud** y selecciona el tipo de formulario.
2. Pulsa **Importar masivo desde Excel** y descarga la plantilla correspondiente.
3. Completa la hoja **Formularios**. Cada fila crea una solicitud; repite el identificador de la columna **Formulario** para agrupar líneas, vehículos o consumos en una misma solicitud.
4. Carga el `.xlsx`, revisa los errores y usa **Revisar / editar** para corregir los datos. El sistema no guarda registros durante esta revisión.
5. Pulsa **Importar formularios**. El panel muestra los guardados y pendientes. Si se interrumpe, continúa desde el mismo panel para conservar los folios y evitar duplicados.

Se incluyen las 13 plantillas del catálogo administrativo y la de equipo de internet de Avería Técnica. Se mantienen los formularios asignados a cada cuenta. Límites: 10 MB por archivo, 1,000 filas, 100 formularios por archivo y 100 detalles por formulario. Se admite `.xlsx`; los archivos `.xls` deben guardarse como `.xlsx`. Las fórmulas deben pegarse como valores. Los códigos, teléfonos, cuentas y cédulas se conservan como texto.

## Edición

El listado muestra **Editar formulario** cuando el servidor autoriza la edición. Se recuperan los campos, se genera un PDF actualizado y se conserva el identificador y folio. Incluye los registros históricos de combustible con datos organizados por secciones.

Se pueden editar solicitudes pendientes, recibidas, rechazadas o con corrección solicitada. Al guardar vuelven a pendiente para una nueva recepción y revisión. Los datos anteriores y la identidad de quien edita quedan en el historial. Las solicitudes autorizadas o entregadas permanecen cerradas. Se comprueban permisos y versión dentro de una transacción para evitar sobrescribir cambios concurrentes.

## Validación técnica

- `npx vitest run tests/administrative-form-excel.test.ts tests/administrative-requests-saving.test.ts tests/administrative-forms.test.ts tests/administrative-internet-request.test.ts tests/administrative-form-frame.test.ts`
- `dotnet run --project tests/administrative-form-editing/AdministrativeFormEditing.csproj`: crea y elimina exclusivamente una base SQL Server local temporal de prueba.
- `npx vite --config scripts/qa-administrative-excel.config.ts` y, en otra terminal, `node scripts/qa-administrative-excel.mjs`: prueba de navegador con API simulada, sin datos de producción.
- `npm run build`

El cambio incluye frontend y API; ambos deben actualizarse juntos al desplegar.

Las 42 pruebas específicas, 21 comprobaciones SQL y el flujo de navegador pasaron. Una ejecución ampliada detectó dos fallos previos ajenos a este cambio: una expectativa antigua sobre destinos de Almacén en `administrative-profiles.test.tsx` y búsquedas literales desactualizadas en `administrative-virtual-reception.test.ts`. No se modificaron esas funcionalidades.
