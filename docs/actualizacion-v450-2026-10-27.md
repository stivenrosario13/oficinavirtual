# Actualización V450

Incluye las mejoras anteriores de memoria, fotos, carga bajo demanda, consultas y coordenadas de V449, junto con estos cambios solicitados:

- Cambio de contraseña desde Configuración, incluida la cuenta principal de recuperación. Se convierte en cuenta permanente conservando la identidad existente cuando corresponde. La contraseña anterior deja de funcionar. Se revocan las cookies y los tokens de los demás dispositivos; el dispositivo que cambia la contraseña recibe una sesión nueva. Las pestañas abiertas comprueban la sesión cada 15 segundos o al volver a enfocarlas. Los dispositivos sin conexión lo detectan al reconectar. El código de recuperación anterior queda invalidado y se puede generar otro.
- Soporte de departamentos: cuatro tarjetas para Gran Santo Domingo, Norte, Este y Sur, seguidas por las tarjetas existentes de Tickets, Tickets internos y Hallazgos. Filtrado sobre el alcance ya autorizado por el servidor. La clasificación usa código y grupo de agencia, región registrada y, como respaldo, provincia. Los casos sin clasificación tienen un acceso separado. Se mantienen las bandejas personales y los enlaces de notificaciones.
- Cola de Almacén: retirada del menú lateral de escritorio y móvil; permanece en el inicio de Almacén. Consulta propia que no descarga firmas, formularios ni PDF. Conserva las comprobaciones de acceso de los documentos. Error visible y reintento; una falla de actualización no borra los registros ya cargados. Las tarjetas abren las solicitudes de su departamento.

## Verificación local

- TypeScript y compilación de publicación .NET/React.
- Pruebas SQL aisladas de cambio de contraseña, sesiones simultáneas, revocación de tokens y credencial principal antigua.
- Pruebas SQL de la cola con firmas grandes, destino de Almacén, documentos eliminados, estado recibido y aislamiento por usuario.
- Navegador Edge en 390 y 1366 píxeles: regiones, conteos, tickets, cambio de departamento, notificaciones, contraseña, error/reintento de cola y conservación de datos.
- Suite general: 785 pruebas, 711 aprobadas, 74 fallos anteriores; ninguna falla nueva respecto al informe anterior. Los fallos previos se conservan en los informes de outputs y no se presentan como aprobados.

Estas pruebas usan datos locales y respuestas de prueba. No se ha instalado este paquete en producción ni se ha verificado una sesión real de MonsterASP. Después de actualizar, comprobar `/api/version` y `/version-web.json` con V450 y probar las acciones con una cuenta real.
