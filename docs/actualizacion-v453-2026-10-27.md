# V453: código de recuperación en el inicio de sesión

Se retira el bloque de generación de Configuración y se agrega el botón Código de recuperación en PortalLogin. La generación confirma el usuario y la contraseña actual; no inicia sesión ni cambia la contraseña. Se conserva Olvidé mi contraseña y se actualizan sus indicaciones para la nueva ubicación.

El endpoint de generación desde el inicio usa el mismo límite de intentos que el acceso, respuestas sin caché, protección existente contra solicitudes entre sitios y verificación de contraseña en una transacción SQL. Rechaza usuarios inactivos y contraseñas temporales. Conserva la generación autenticada existente para compatibilidad, el almacenamiento del hash, el reemplazo del código previo y su uso único.

Validación: prueba de navegador a 390 y 1366 píxeles de generación, error de credenciales, recuperación, ausencia del bloque en Configuración y conservación del cambio de contraseña. Integración SQL en base temporal: rechazo de credenciales incorrectas, usuario inexistente, cuenta inactiva y contraseña temporal; reemplazo, recuperación de un solo uso y revocación de sesiones. No se cambian cuentas ni contraseñas reales.

Se mantienen los cambios de V452 y el inventario corregido. El paquete contiene la configuración privada de almacenamiento y no debe compartirse públicamente.
